// Procedural Pixel Creature Workshop - walkable test area: procedural map in five sizes (640×400 up to
// 2560×1600) with ponds, paths, trees, rocks and bushes; a controllable creature and up to 100 creatures
// of every family wandering around. Creatures outside the view keep moving but are not rasterized
// (CreatureActor.CullOffscreen), so large maps with many creatures stay cheap.
//
// Controls: WASD / arrows move, Shift runs, Space = action, R = rest, H = get hit, Tab = nearest creature,
// left click on a creature = take control, mouse wheel = zoom, right/middle drag = move the camera,
// F = camera follows again, M = minimap, PageUp/PageDown = map size, N = new map, +/- = population,
// F1 = overlay, Esc = back to the workshop.
//
// Command line (after "--"): --testarea [--population=1..100] [--map=0..4] [--map-seed=N] [--zoom=1..8]

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class TestArea : Control
    {
        public const int MaxCreatures = 100;
        public const int DefaultMapIndex = 1;
        /// <summary>Main thread time per frame for spawning creatures; larger populations appear over a few frames.</summary>
        private const double SpawnBudgetMs = 4.0;

        private SubViewport _viewport = null!;
        private TextureRect _display = null!;
        private Node2D _world = null!;
        private Node2D _entities = null!;
        private Node2D _waterLayer = null!;
        private Sprite2D _waterSurface = null!;
        private Sprite2D _ground = null!;
        private Camera2D _camera = null!;
        private TestMap _map = null!;
        private int _mapIndex = DefaultMapIndex;
        private ulong _mapSeed = 9001;
        private Task<TestMap>? _mapTask;
        private CancellationTokenSource? _mapCancel;
        private readonly List<Sprite2D> _propSprites = new List<Sprite2D>();
        private readonly List<(Sprite2D Sprite, ImageTexture[] Frames)> _ponds = new List<(Sprite2D, ImageTexture[])>();
        private readonly List<ImageTexture> _mapTextures = new List<ImageTexture>();
        private ImageTexture? _minimapTexture;
        private CreatureActor? _player;
        private readonly List<Wanderer> _npcs = new List<Wanderer>();
        private int _targetPopulation = 10;
        private Label _hud = null!;
        private Label _perf = null!;
        private Label _status = null!;
        private Label _populationValue = null!;
        private Label _mapValue = null!;
        private Label _zoomValue = null!;
        private HSlider _populationSlider = null!;
        private Button _followButton = null!;
        private Button _minimapButton = null!;
        private PanelContainer _overlay = null!;
        private TestAreaMinimap _minimap = null!;
        private int _zoom = 3;
        private bool _follow = true;
        private bool _dragging;
        private Vector2 _cameraPos;
        private double _time;
        private int _waterFrame = -1;
        private readonly Stopwatch _frameWatch = new Stopwatch();
        private readonly List<double> _frameTimes = new List<double>();
        private int _nextNpc;

        private sealed class Wanderer
        {
            public CreatureActor Actor = null!;
            public Rng Rng = null!;
            public Vector2 Target;
            public double Wait;
            public bool Running;
            public bool Aquatic;
            public bool Flying;
            public double RestUntil;
        }

        // ------------------------------------------------------------------ public state (UI, tests)

        public TestMap Map => _map;
        public int MapIndex => _mapIndex;
        public MapSize MapSizePreset => MapSize.All[_mapIndex];
        /// <summary>True while a new map is generated in the background.</summary>
        public bool MapBusy => _mapTask != null;
        public int TargetPopulation => _targetPopulation;
        public int CreatureCount => _npcs.Count + (_player != null ? 1 : 0);
        public int VisibleCreatureCount => AllActors().Count(a => !a.IsCulled);
        public int Zoom => _zoom;
        public bool CameraFollows => _follow;
        public bool MinimapVisible => _minimap.Visible;
        public bool OverlayVisible => _overlay.Visible;

        /// <summary>The map area currently shown (screen pixels of the map).</summary>
        public Rect2 ViewRect
        {
            get
            {
                var size = (Vector2)_viewport.Size;
                return new Rect2(_camera.Position - size / 2, size);
            }
        }

        public IEnumerable<CreatureActor> AllActors()
        {
            if (_player != null) yield return _player;
            foreach (var w in _npcs) yield return w.Actor;
        }

        public CreatureActor? Player => _player;

        // ------------------------------------------------------------------ setup

        public override void _Ready()
        {
            Theme = UiKit.CreateTheme();
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            var bg = new ColorRect { Color = UiKit.Bg, MouseFilter = MouseFilterEnum.Ignore };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);
            _viewport = new SubViewport
            {
                Size = new Vector2I(400, 240),
                CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
                Snap2DTransformsToPixel = true,
                Snap2DVerticesToPixel = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Disable3D = true,
            };
            AddChild(_viewport);
            _display = new TextureRect
            {
                Texture = _viewport.GetTexture(),
                TextureFilter = TextureFilterEnum.Nearest,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(_display);
            _world = new Node2D();
            _viewport.AddChild(_world);
            _ground = new Sprite2D { Centered = false, ZIndex = -10 };
            _world.AddChild(_ground);
            _waterLayer = new Node2D { ZIndex = -1 };
            _world.AddChild(_waterLayer);
            _waterSurface = new Sprite2D { Centered = false, ZIndex = -1 };
            _world.AddChild(_waterSurface);
            _entities = new Node2D { YSortEnabled = true };
            _world.AddChild(_entities);
            _world.AddChild(new HabitatEffects { Area = this, ZIndex = -1 });
            _camera = new Camera2D { PositionSmoothingEnabled = false, LimitLeft = 0, LimitTop = 0 };
            _world.AddChild(_camera);
            _camera.MakeCurrent();

            var args = OS.GetCmdlineUserArgs();
            int population = 10;
            foreach (var a in args)
            {
                if (a.StartsWith("--population=") && int.TryParse(a.Substring(13), out int p)) population = p;
                else if (a.StartsWith("--map=") && int.TryParse(a.Substring(6), out int m)) _mapIndex = Math.Clamp(m, 0, MapSize.All.Length - 1);
                else if (a.StartsWith("--map-seed=") && ulong.TryParse(a.Substring(11), out ulong s)) _mapSeed = s;
                else if (a.StartsWith("--zoom=") && int.TryParse(a.Substring(7), out int z)) _zoom = Math.Clamp(z, 1, 8);
            }
            BuildOverlay();
            _minimap = new TestAreaMinimap { Area = this };
            AddChild(_minimap);
            var size = MapSize.All[_mapIndex];
            ApplyMap(EnvironmentArt.GenerateMap(size.Width, size.Height, _mapSeed));

            SpawnPlayer(TestAreaLauncher.PendingGenome ?? CreatureRuntime.Sample("quadruped", 7));
            SetPopulation(population);
            Resized += Relayout;
            Relayout();
            UpdateCamera();
        }

        public override void _ExitTree()
        {
            _mapCancel?.Cancel();
            _mapTask = null;
            // the nodes go with the scene; release the texture wrappers right away instead of at the next GC
            foreach (var t in _mapTextures) t.Dispose();
            _mapTextures.Clear();
        }

        private void BuildOverlay()
        {
            _overlay = new PanelContainer { Position = new Vector2(10, 10) };
            var v = UiKit.Column(5);
            v.AddChild(UiKit.Header("Test area"));
            _hud = UiKit.Label("", 13);
            _perf = UiKit.Label("", 12, UiKit.TextDim);
            v.AddChild(_hud);
            v.AddChild(_perf);

            _populationSlider = new HSlider
            {
                MinValue = 1,
                MaxValue = MaxCreatures,
                Step = 1,
                CustomMinimumSize = new Vector2(170, 0),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                FocusMode = FocusModeEnum.None,
                TooltipText = $"Number of creatures on the map (1–{MaxCreatures}, keys +/-)",
            };
            _populationSlider.ValueChanged += value => SetPopulation((int)Math.Round(value));
            _populationValue = UiKit.Label("", 13);
            _populationValue.CustomMinimumSize = new Vector2(34, 0);
            var populationRow = UiKit.Row(6, Caption("Creatures"), _populationSlider, _populationValue);
            foreach (int n in new[] { 1, 10, 25, 50, MaxCreatures })
            {
                int count = n;
                populationRow.AddChild(UiKit.Button($"{n}", () => SetPopulation(count), $"{n} {(n == 1 ? "creature" : "creatures")} on the map"));
            }
            v.AddChild(populationRow);
            populationRow.AddChild(UiKit.Button("Reroll", RerollPopulation, "Generate a fresh population in this landscape (G)"));

            _mapValue = UiKit.Label("", 13);
            _mapValue.CustomMinimumSize = new Vector2(150, 0);
            _mapValue.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(UiKit.Row(6, Caption("Map"),
                UiKit.Button("−", () => SetMapSize(_mapIndex - 1), "Smaller map (PgDn)"),
                _mapValue,
                UiKit.Button("+", () => SetMapSize(_mapIndex + 1), "Larger map (PgUp)"),
                UiKit.Button("New map", NewMap, "New map of the same size (N)")));

            _zoomValue = UiKit.Label("", 13);
            _zoomValue.CustomMinimumSize = new Vector2(26, 0);
            _zoomValue.HorizontalAlignment = HorizontalAlignment.Center;
            _followButton = UiKit.Toggle("Camera follows", true, SetFollow, "The camera follows the controlled creature (F). Drag with the right or middle mouse button to move the camera freely");
            _minimapButton = UiKit.Toggle("Minimap", true, ShowMinimap, "Minimap on/off (M); clicking the minimap moves the camera");
            v.AddChild(UiKit.Row(6, Caption("Zoom"),
                UiKit.Button("−", () => SetZoom(_zoom - 1), "Zoom out (mouse wheel)"),
                _zoomValue,
                UiKit.Button("+", () => SetZoom(_zoom + 1), "Zoom in (mouse wheel)"),
                _followButton, _minimapButton,
                UiKit.Button("Back to workshop", Back, "Return to the workshop (Esc)")));
            _status = UiKit.Label("", 12, UiKit.Accent);
            v.AddChild(_status);
            _overlay.AddChild(v);
            AddChild(_overlay);
            UpdatePopulationUi();
            UpdateMapUi();
            UpdateZoomUi();
        }

        private static Label Caption(string text)
        {
            var l = UiKit.Label(text, 13, UiKit.TextDim);
            l.CustomMinimumSize = new Vector2(70, 0);
            return l;
        }

        private void Relayout()
        {
            var s = Size;
            int lw = Math.Max(64, (int)Math.Ceiling(s.X / _zoom)), lh = Math.Max(64, (int)Math.Ceiling(s.Y / _zoom));
            lw = Math.Min(lw, _map.Width);
            lh = Math.Min(lh, _map.Height);
            if ((lw & 1) == 1) lw--;
            if ((lh & 1) == 1) lh--;
            _viewport.Size = new Vector2I(lw, lh);
            _display.Size = new Vector2(lw * _zoom, lh * _zoom);
            _display.Position = new Vector2(Mathf.Floor((s.X - lw * _zoom) / 2), Mathf.Floor((s.Y - lh * _zoom) / 2));
            if (_minimap != null) _minimap.Position = new Vector2(s.X - _minimap.Size.X - 14, s.Y - _minimap.Size.Y - 14);
        }

        // ------------------------------------------------------------------ map

        /// <summary>Selects a map size preset (0 = Small … 4 = Huge). The map is generated in the background unless immediate.</summary>
        public void SetMapSize(int index, bool immediate = false)
        {
            index = Math.Clamp(index, 0, MapSize.All.Length - 1);
            if (index == _mapIndex && _mapTask == null) return;
            _mapIndex = index;
            RequestMap(immediate);
        }

        /// <summary>New random layout in the current size.</summary>
        public void NewMap() => NewMap(false);

        public void NewMap(bool immediate)
        {
            _mapSeed = _mapSeed * 6364136223846793005UL + 1442695040888963407UL;
            RequestMap(immediate);
        }

        private void RequestMap(bool immediate)
        {
            _mapCancel?.Cancel();
            _mapCancel = null;
            _mapTask = null;
            var size = MapSize.All[_mapIndex];
            ulong seed = _mapSeed;
            if (immediate)
            {
                ApplyMap(EnvironmentArt.GenerateMap(size.Width, size.Height, seed));
                return;
            }
            var cancel = new CancellationTokenSource();
            _mapCancel = cancel;
            _mapTask = Task.Run(() => EnvironmentArt.GenerateMap(size.Width, size.Height, seed, cancel.Token), cancel.Token);
            UpdateMapUi();
        }

        private void PollMapTask()
        {
            if (_mapTask == null || !_mapTask.IsCompleted) return;
            var task = _mapTask;
            _mapTask = null;
            _mapCancel = null;
            if (task.Status == TaskStatus.RanToCompletion) ApplyMap(task.Result);
            else if (task.IsFaulted) GD.PushError("Map could not be generated: " + task.Exception?.GetBaseException().Message);
            UpdateMapUi();
        }

        private void ApplyMap(TestMap map)
        {
            var old = _map;
            ReleaseMap();
            _map = map;
            _ground.Texture = MapTexture(map.GroundRgba, map.Width, map.Height);
            _waterSurface.Texture = MapTexture(map.WaterSurfaceRgba, map.Width, map.Height);
            foreach (var face in map.Cliffs)
            {
                var cliff = new Sprite2D { Centered = false, Texture = MapTexture(face.Rgba, face.Width, face.Height), Position = new Vector2(face.X, face.Y), Offset = new Vector2(0, -face.Height) };
                _entities.AddChild(cliff);
                _propSprites.Add(cliff);
            }
            foreach (var pond in map.Ponds)
            {
                var b = pond.Bounds;
                if (b.Size.X <= 0 || b.Size.Y <= 0 || pond.GlintFrames.Length == 0) continue;
                var frames = pond.GlintFrames.Select(f => MapTexture(f, b.Size.X, b.Size.Y)).ToArray();
                var sprite = new Sprite2D { Centered = false, Position = b.Position, Texture = frames[0] };
                _waterLayer.AddChild(sprite);
                _ponds.Add((sprite, frames));
            }
            _waterFrame = -1;
            // props: a handful of variants per kind share their textures
            var rng = new Rng(map.Seed ^ 0x56415249UL);
            var trees = PropVariants(PropKind.Tree, EnvironmentArt.TreeVariants, rng);
            var bushes = PropVariants(PropKind.Bush, EnvironmentArt.BushVariants, rng);
            var rocks = PropVariants(PropKind.Rock, EnvironmentArt.RockVariants, rng);
            foreach (var p in map.Props)
            {
                var v = p.Kind == PropKind.Tree ? trees[p.Variant] : (p.Kind == PropKind.Bush ? bushes[p.Variant] : rocks[p.Variant]);
                var s = new Sprite2D { Texture = v.Texture, Centered = false, Offset = -new Vector2(v.Origin.X, v.Origin.Y), Position = map.Project(new Vector2(p.X, p.Y)) };
                _entities.AddChild(s);
                _propSprites.Add(s);
            }
            _camera.LimitRight = map.Width;
            _camera.LimitBottom = map.Height;
            _minimapTexture = MapTexture(map.MiniRgba, map.MiniWidth, map.MiniHeight);
            _minimap.SetMap(_minimapTexture, map.MiniWidth, map.MiniHeight);
            if (old != null) RelocateCreatures(old);
            Relayout();
            UpdateMapUi();
        }

        private ImageTexture MapTexture(byte[] rgba, int w, int h)
        {
            var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, rgba);
            var tex = ImageTexture.CreateFromImage(img);
            img.Dispose();
            _mapTextures.Add(tex);
            return tex;
        }

        private (ImageTexture Texture, Vector2I Origin)[] PropVariants(PropKind kind, int count, Rng rng)
        {
            var list = new (ImageTexture, Vector2I)[count];
            for (int i = 0; i < count; i++)
            {
                Image img;
                Vector2I origin;
                if (kind == PropKind.Tree) img = EnvironmentArt.Tree(rng.NextULong(), out origin, rng.Range(0.85, 1.2));
                else if (kind == PropKind.Bush) img = EnvironmentArt.Bush(rng.NextULong(), out origin, rng.Range(0.8, 1.2));
                else img = EnvironmentArt.Rock(rng.NextULong(), out origin, rng.Range(0.7, 1.3));
                var tex = ImageTexture.CreateFromImage(img);
                img.Dispose();
                _mapTextures.Add(tex);
                list[i] = (tex, origin);
            }
            return list;
        }

        /// <summary>Frees the nodes and textures of the current map (sprites go at the end of the frame).</summary>
        private void ReleaseMap()
        {
            foreach (var s in _propSprites) if (IsInstanceValid(s)) s.QueueFree();
            _propSprites.Clear();
            foreach (var (sprite, _) in _ponds) if (IsInstanceValid(sprite)) sprite.QueueFree();
            _ponds.Clear();
            if (IsInstanceValid(_ground)) _ground.Texture = null;
            if (IsInstanceValid(_waterSurface)) _waterSurface.Texture = null;
            _minimap?.SetMap(null, 0, 0);
            foreach (var t in _mapTextures) t.Dispose();
            _mapTextures.Clear();
            _minimapTexture = null;
        }

        /// <summary>Keeps creatures at the same relative place on the new map, moved to the nearest valid ground.</summary>
        private void RelocateCreatures(TestMap old)
        {
            float sx = _map.Width / (float)old.Width, sy = _map.Height / (float)old.Height;
            var rng = new Rng(_map.Seed ^ 0x52454C4FUL);
            foreach (var a in AllActors()) Relocate(a, sx, sy, rng);
            foreach (var w in _npcs)
            {
                w.Target = w.Actor.GroundPosition;
                w.Wait = rng.Range(0.0, 2.0);
            }
            _cameraPos *= new Vector2(sx, sy);
        }

        private void Relocate(CreatureActor a, float sx, float sy, Rng rng)
        {
            var s = GodotConvert.GroundToScreen(a.GroundPosition.X, a.GroundPosition.Y);
            a.Teleport(FindSpot(IsAquatic(a), false, rng, new Vector2(s.X * sx, s.Y * sy), HabitatRadius(a)));
        }

        private static bool IsAquatic(CreatureActor a) => a.Model?.Anatomy.Rig.Locomotion == LocomotionKind.Swim;

        private static bool IsFlying(CreatureActor a) => a.Model?.Anatomy.Rig.Locomotion == LocomotionKind.Fly;

        private static float HabitatRadius(CreatureActor a) => IsAquatic(a)
            ? (float)Math.Clamp((a.Model?.Anatomy.Metrics.HorizontalRadius ?? 22) + 3, 18, 42)
            : (float)Math.Clamp((a.Model?.Anatomy.Metrics.HorizontalRadius ?? 20) * 0.2, 4, 9);

        private bool SpotValid(Vector2 s, bool aquatic, bool flying, float radius)
        {
            return _map.CanOccupy(s, aquatic, flying, radius);
        }

        /// <summary>Ground position on suitable terrain: near <paramref name="near"/> (screen) when given, otherwise anywhere.</summary>
        private Vector2 FindSpot(bool aquatic, bool flying, Rng rng, Vector2? near = null, float radius = 6)
        {
            if (near.HasValue && SpotValid(near.Value, aquatic, flying, radius)) return GodotConvert.ScreenToGround(near.Value);
            for (int i = 0; i < 600; i++)
            {
                Vector2 s;
                if (near.HasValue && i < 300)
                {
                    float r = 24 + i * 2;
                    s = near.Value + new Vector2((float)rng.Range(-r, r), (float)rng.Range(-r * 0.6, r * 0.6));
                }
                else s = new Vector2(rng.Range(20, _map.Width - 20), rng.Range(24, _map.Height - 12));
                if (!SpotValid(s, aquatic, flying, radius)) continue;
                var ground = GodotConvert.ScreenToGround(s);
                // Prefer open space at spawn; dense populations can relax this soft spacing rule.
                if (i < 450 && AllActors().Any(a => a.GroundPosition.DistanceTo(ground) < (aquatic ? radius + 30 : 36))) continue;
                return ground;
            }
            for (int y = 24; y < _map.Height - 24; y += 3)
                for (int x = 24; x < _map.Width - 24; x += 3)
                    if (SpotValid(new Vector2(x, y), aquatic, flying, radius)) return GodotConvert.ScreenToGround(new Vector2(x, y));
            throw new InvalidOperationException("The map has no suitable habitat for this creature.");
        }

        // ------------------------------------------------------------------ creatures

        private CreatureActor NewActor(int fps)
        {
            var actor = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn").Instantiate<CreatureActor>();
            actor.FamilyId = string.Empty;
            actor.AnimationFps = fps;
            actor.CullOffscreen = true;
            return actor;
        }

        private void SpawnPlayer(CreatureGenome genome)
        {
            _player = NewActor(20);
            _entities.AddChild(_player);
            _player.SetGenome(genome, async: false);
            BindHabitat(_player);
            var start = FindSpot(IsAquatic(_player), false, new Rng(1), new Vector2(_map.Width * 0.3f, _map.Height * 0.45f), HabitatRadius(_player));
            _player.Teleport(start);
            _cameraPos = _player.Position;
        }

        /// <summary>Sets the number of creatures on the map including the controlled one (1..100). New ones appear over the next frames.</summary>
        public void SetPopulation(int count)
        {
            _targetPopulation = Math.Clamp(count, 1, MaxCreatures);
            UpdatePopulationUi();
        }

        private void UpdatePopulation()
        {
            int target = _targetPopulation - (_player != null ? 1 : 0);
            while (_npcs.Count > target)
            {
                var w = _npcs[_npcs.Count - 1];
                _npcs.RemoveAt(_npcs.Count - 1);
                w.Actor.QueueFree();
            }
            if (_npcs.Count >= target) return;
            long start = Stopwatch.GetTimestamp();
            long budget = (long)(Stopwatch.Frequency * SpawnBudgetMs / 1000.0);
            do SpawnWanderer();
            while (_npcs.Count < target && Stopwatch.GetTimestamp() - start < budget);
        }

        private void SpawnWanderer()
        {
            var families = CreatureRuntime.Registry.Families;
            int k = _nextNpc++;
            CreatureGenome g = GenomeFactory.Sample(families[k % families.Count], (ulong)(5000 + k * 7919));
            var model = CreatureRuntime.Build(g);
            var rig = model.Anatomy.Rig;
            var actor = NewActor(12);
            _entities.AddChild(actor);
            actor.ApplyModel(model);
            BindHabitat(actor);
            var w = new Wanderer
            {
                Actor = actor,
                Rng = new Rng((ulong)(900 + k)),
                Aquatic = rig.Locomotion == LocomotionKind.Swim,
                Flying = rig.Locomotion == LocomotionKind.Fly,
            };
            actor.Teleport(FindSpot(w.Aquatic, false, w.Rng, radius: HabitatRadius(actor)));
            w.Target = actor.GroundPosition;
            w.Wait = w.Aquatic ? 0 : w.Rng.Range(0.0, 3.0);
            _npcs.Add(w);
        }

        private void SwapPlayer()
        {
            if (_player == null || _npcs.Count == 0) return;
            var p = _player.GroundPosition;
            TakeControl(_npcs.OrderBy(n => n.Actor.GroundPosition.DistanceSquaredTo(p)).First());
        }

        /// <summary>Takes control of the creature under a map position (screen pixels of the map), if any.</summary>
        public bool TakeControlAt(Vector2 mapPoint)
        {
            Wanderer? best = null;
            float bestScore = 1f;
            foreach (var w in _npcs)
            {
                var a = w.Actor;
                var extent = a.ContentExtent;
                var center = a.Position + (extent == Vector2I.Zero ? new Vector2(0, -8) : a.ContentCenter);
                float rx = Math.Max(8, extent.X * 0.5f + 3), ry = Math.Max(8, extent.Y * 0.5f + 3);
                var d = mapPoint - center;
                float score = d.X * d.X / (rx * rx) + d.Y * d.Y / (ry * ry);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = w;
                }
            }
            if (best == null) return false;
            TakeControl(best);
            return true;
        }

        private void TakeControl(Wanderer target)
        {
            _npcs.Remove(target);
            var old = _player;
            _player = target.Actor;
            _player.MoveVelocity = Vector2.Zero;
            _player.Resting = false;
            _player.AnimationFps = 20;
            if (old != null)
            {
                old.AnimationFps = 12;
                _npcs.Add(new Wanderer { Actor = old, Rng = new Rng((ulong)_nextNpc++), Target = old.GroundPosition, Aquatic = IsAquatic(old), Flying = IsFlying(old) });
            }
            SetFollow(true);
        }

        // ------------------------------------------------------------------ camera

        public void SetZoom(int zoom, Vector2? anchor = null)
        {
            zoom = Math.Clamp(zoom, 1, 8);
            if (zoom == _zoom) return;
            Vector2? mapPoint = anchor.HasValue && !_follow ? ScreenToMap(anchor.Value) : null;
            _zoom = zoom;
            Relayout();
            if (mapPoint.HasValue)
            {
                // keep the point under the mouse in place
                var vp = (anchor!.Value - _display.Position) / _zoom;
                _cameraPos = mapPoint.Value - vp + (Vector2)_viewport.Size / 2;
            }
            UpdateCamera();
            UpdateZoomUi();
        }

        private void SetFollow(bool follow)
        {
            _follow = follow;
            _followButton?.SetPressedNoSignal(follow);
        }

        /// <summary>Shows the navigation inset; cinematic recordings can hide it independently of the controls.</summary>
        public void ShowMinimap(bool show)
        {
            _minimap.Visible = show;
            _minimapButton?.SetPressedNoSignal(show);
        }

        /// <summary>Centres the camera on a map position (screen pixels of the map) and stops following.</summary>
        public void LookAt(Vector2 mapPoint)
        {
            SetFollow(false);
            _cameraPos = mapPoint;
            UpdateCamera();
        }

        /// <summary>Converts a position inside this control to screen pixels of the map.</summary>
        public Vector2 ScreenToMap(Vector2 local)
        {
            var vp = (local - _display.Position) / _zoom;
            return _camera.Position - (Vector2)_viewport.Size / 2 + vp;
        }

        private Vector2 ClampCamera(Vector2 p)
        {
            var view = (Vector2)_viewport.Size;
            float x = _map.Width <= view.X ? _map.Width / 2f : Math.Clamp(p.X, view.X / 2, _map.Width - view.X / 2);
            float y = _map.Height <= view.Y ? _map.Height / 2f : Math.Clamp(p.Y, view.Y / 2, _map.Height - view.Y / 2);
            return new Vector2(x, y);
        }

        private void UpdateCamera()
        {
            if (_follow && _player != null) _cameraPos = new Vector2(_player.Position.X, _player.Position.Y - 12);
            _cameraPos = ClampCamera(_cameraPos);
            _camera.Position = new Vector2(Mathf.Round(_cameraPos.X), Mathf.Round(_cameraPos.Y));
        }

        // ------------------------------------------------------------------ input

        public override void _GuiInput(InputEvent e)
        {
            switch (e)
            {
                case InputEventMouseButton mb when mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp:
                    SetZoom(_zoom + 1, mb.Position);
                    AcceptEvent();
                    break;
                case InputEventMouseButton mb when mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown:
                    SetZoom(_zoom - 1, mb.Position);
                    AcceptEvent();
                    break;
                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Right || mb.ButtonIndex == MouseButton.Middle:
                    _dragging = mb.Pressed;
                    if (mb.Pressed) SetFollow(false);
                    AcceptEvent();
                    break;
                case InputEventMouseButton mb when mb.Pressed && mb.ButtonIndex == MouseButton.Left:
                    TakeControlAt(ScreenToMap(mb.Position));
                    AcceptEvent();
                    break;
                case InputEventMouseMotion mm when _dragging:
                    _cameraPos = ClampCamera(_cameraPos - mm.Relative / _zoom);
                    AcceptEvent();
                    break;
            }
        }

        public override void _UnhandledKeyInput(InputEvent e)
        {
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
            switch (k.Keycode)
            {
                case Key.Escape: Back(); break;
                case Key.Space: _player?.TriggerAction(); break;
                case Key.R: if (_player != null) _player.Resting = !_player.Resting; break;
                case Key.H: _player?.TriggerHit(new Vector2(1, 0.3f), 1f); break;
                case Key.F1: _overlay.Visible = !_overlay.Visible; break;
                case Key.Tab: SwapPlayer(); break;
                case Key.F: SetFollow(true); break;
                case Key.M: ShowMinimap(!_minimap.Visible); break;
                case Key.N: NewMap(); break;
                case Key.G: RerollPopulation(); break;
                case Key.Pageup: SetMapSize(_mapIndex + 1); break;
                case Key.Pagedown: SetMapSize(_mapIndex - 1); break;
                case Key.Equal: case Key.Plus: case Key.KpAdd: SetPopulation((_targetPopulation / 10 + 1) * 10); break;
                case Key.Minus: case Key.KpSubtract: SetPopulation((_targetPopulation - 1) / 10 * 10); break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
        }

        private void Back()
        {
            if (_player?.Genome != null) TestAreaLauncher.PendingGenome = _player.Genome;
            _mapCancel?.Cancel();
            GetTree().ChangeSceneToFile("res://workshop/Main.tscn");
        }

        // ------------------------------------------------------------------ frame loop

        public override void _Process(double delta)
        {
            _frameWatch.Stop();
            if (_frameWatch.ElapsedTicks > 0) _frameTimes.Add(_frameWatch.Elapsed.TotalMilliseconds);
            if (_frameTimes.Count > 240) _frameTimes.RemoveAt(0);
            _frameWatch.Restart();
            PollMapTask();
            UpdatePopulation();
            UpdatePlayer();
            foreach (var w in _npcs) UpdateWanderer(w, delta);
            UpdateCamera();
            _time += delta;
            int wf = (int)(_time * 3) % 4;
            if (wf != _waterFrame)
            {
                _waterFrame = wf;
                foreach (var (sprite, frames) in _ponds) sprite.Texture = frames[wf];
            }
            if (_minimap.Visible) _minimap.QueueRedraw();
            UpdateHud();
        }

        private void UpdatePlayer()
        {
            if (_player?.Model == null) return;
            var dir = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            bool run = Input.IsActionPressed("run_modifier");
            var m = _player.Model.Motion;
            float speed = (float)(run ? m.RunSpeed : m.WalkSpeed);
            // screen up/down maps to ground north/south
            Vector2 vel = dir.LengthSquared() > 0.01f ? dir.Normalized() * speed : Vector2.Zero;
            bool aquatic = IsAquatic(_player), flying = IsFlying(_player);
            if (vel != Vector2.Zero)
            {
                var next = _player.GroundPosition + vel * 0.25f;
                var s = GodotConvert.GroundToScreen(next.X, next.Y);
                var from = GodotConvert.GroundToScreen(_player.GroundPosition.X, _player.GroundPosition.Y);
                if (!_map.CanTravel(from, s, aquatic, flying && _player.Motor!.Altitude > 5, HabitatRadius(_player)))
                {
                    var vx = new Vector2(vel.X, 0);
                    var vy = new Vector2(0, vel.Y);
                    bool Valid(Vector2 v) { var p = _player.GroundPosition + v * 0.15f; return _map.CanTravel(from, GodotConvert.GroundToScreen(p.X, p.Y), aquatic, flying && _player.Motor!.Altitude > 5, HabitatRadius(_player)); }
                    vel = Valid(vx) ? vx : Valid(vy) ? vy : Vector2.Zero;
                }
            }
            _player.MoveVelocity = vel;
        }

        private void UpdateWanderer(Wanderer w, double dt)
        {
            var a = w.Actor;
            if (a.Model == null) return;
            var motion = a.Model.Motion;
            if (a.Resting)
            {
                if (_time > w.RestUntil) a.Resting = false;
                a.MoveVelocity = Vector2.Zero;
                return;
            }
            Vector2 to = w.Target - a.GroundPosition;
            if (to.Length() < (w.Aquatic ? 24 : 4) || w.Wait > 0)
            {
                a.MoveVelocity = Vector2.Zero;
                w.Wait -= dt;
                if (w.Wait <= 0)
                {
                    double r = w.Rng.NextDouble();
                    if (r < 0.08) a.TriggerAction();
                    else if (r < 0.13 && !w.Aquatic && !w.Flying) { a.Resting = true; w.RestUntil = _time + w.Rng.Range(4.0, 9.0); }
                    // new target on suitable terrain, mostly nearby, sometimes further away
                    for (int i = 0; i < 20; i++)
                    {
                        double range = w.Rng.Chance(0.15) ? 240.0 : 90.0;
                        var t = a.GroundPosition + new Vector2((float)w.Rng.Range(-range, range), (float)w.Rng.Range(-range, range));
                        var s = GodotConvert.GroundToScreen(t.X, t.Y);
                        if (s.X < 10 || s.Y < 16 || s.X > _map.Width - 10 || s.Y > _map.Height - 6) continue;
                        var origin = GodotConvert.GroundToScreen(a.GroundPosition.X, a.GroundPosition.Y);
                        if (!_map.CanTravel(origin, s, w.Aquatic, w.Flying, HabitatRadius(a))) continue;
                        w.Target = t;
                        break;
                    }
                    w.Running = w.Flying || w.Rng.Chance(0.25);
                    w.Wait = w.Aquatic ? 0 : w.Rng.Range(0.5, 3.5);
                    if (to.Length() >= 4) w.Wait = 0;
                }
                return;
            }
            var dir = to.Normalized();
            if (w.Aquatic)
            {
                // Keep a little space within a school and round turns before reaching a waypoint.
                var separation = Vector2.Zero;
                foreach (var other in _npcs)
                {
                    if (!other.Aquatic || other == w) continue;
                    var away = a.GroundPosition - other.Actor.GroundPosition;
                    float distance = away.Length();
                    if (distance > 0.1f && distance < 60)
                        separation += away / distance * (1 - distance / 60);
                }
                dir = (dir + separation.LimitLength(1.1f)).Normalized();
            }
            if (!w.Flying)
            {
                // walkers stop at the shore, swimmers stay in the water
                var ahead = a.GroundPosition + dir * (float)Math.Max(8, motion.RunSpeed * 0.2);
                var s = GodotConvert.GroundToScreen(ahead.X, ahead.Y);
                var origin = GodotConvert.GroundToScreen(a.GroundPosition.X, a.GroundPosition.Y);
                if (!_map.CanTravel(origin, s, w.Aquatic, w.Flying, HabitatRadius(a)))
                {
                    w.Target = a.GroundPosition;
                    w.Wait = w.Rng.Range(0.2, 1.0);
                    a.MoveVelocity = Vector2.Zero;
                    return;
                }
            }
            double speed = w.Running ? motion.RunSpeed * 0.85 : motion.WalkSpeed;
            a.MoveVelocity = w.Aquatic
                ? a.MoveVelocity.Lerp(dir * (float)(speed * 0.65), (float)(1 - Math.Exp(-dt * 2.5)))
                : dir * (float)speed;
        }

        private void BindHabitat(CreatureActor actor)
        {
            bool aquatic = IsAquatic(actor), flying = IsFlying(actor);
            actor.GroundHeightAt = aquatic ? null : (x, z) => _map.HeightAt(GodotConvert.GroundToScreen(x, z)) / 0.866025403784;
            actor.CanMove = (from, to) => _map.CanTravel(GodotConvert.GroundToScreen(from.X, from.Y), GodotConvert.GroundToScreen(to.X, to.Y), aquatic, flying && actor.Motor!.Altitude > 5, HabitatRadius(actor));
            actor.SurfaceHeight = ground =>
            {
                var p = GodotConvert.GroundToScreen(ground.X, ground.Y);
                if (!aquatic) return _map.HeightAt(p);
                double altitude = actor.Model!.Anatomy.Rig.Altitude;
                if (altitude <= 0) altitude = Math.Max(4, actor.Model.Anatomy.Metrics.BackHeight * 0.8);
                return -(float)(altitude * 0.866 + actor.Model.Anatomy.Metrics.BackHeight * 0.45 + 3 + Math.Sin(_time * 0.55 + actor.Genome!.ContentHash % 13) * 1.5);
            };
            actor.ZIndex = aquatic ? -2 : 0;
            actor.DrawShadow = !aquatic;
            actor.WaterLevel = aquatic ? 1000 : double.NaN;
        }

        public void ShowOverlay(bool show) => _overlay.Visible = show;

        public void RerollPopulation()
        {
            foreach (var w in _npcs) w.Actor.QueueFree();
            _npcs.Clear();
            if (_player != null)
            {
                _player.SetGenome(CreatureRuntime.Sample(_player.Genome!.FamilyId, (ulong)(90000 + _nextNpc++)), async: false);
                BindHabitat(_player);
                _player.Teleport(FindSpot(IsAquatic(_player), false, new Rng((ulong)_nextNpc), radius: HabitatRadius(_player)));
            }
        }

        // ------------------------------------------------------------------ UI state

        private void UpdatePopulationUi()
        {
            if (_populationSlider == null) return;
            _populationSlider.SetValueNoSignal(_targetPopulation);
            _populationValue.Text = _targetPopulation.ToString();
        }

        private void UpdateMapUi()
        {
            if (_mapValue == null) return;
            var size = MapSize.All[_mapIndex];
            _mapValue.Text = $"{size.Name} · {size.Width}×{size.Height}";
        }

        private void UpdateZoomUi()
        {
            if (_zoomValue != null) _zoomValue.Text = $"{_zoom}×";
        }

        private double _hudTimer;

        private void UpdateHud()
        {
            // text changes re-shape the labels: four updates per second are plenty
            _hudTimer -= GetProcessDeltaTime();
            if (_hudTimer > 0) return;
            _hudTimer = 0.25;
            string pname = _player?.Genome?.Name ?? "-";
            int visible = VisibleCreatureCount;
            _hud.Text = "WASD/arrow keys walk, Shift run, Space action, R rest, H hit, Tab/left click take control of a creature\n" +
                        "Mouse wheel zoom, right-drag camera, F follow, M minimap, PgUp/PgDn map size, N new map, G reroll, F1 overlay\n" +
                        $"Player: {pname} ({_player?.State})   Creatures: {CreatureCount}, {visible} visible";
            if (_frameTimes.Count > 10)
            {
                var sorted = _frameTimes.OrderBy(x => x).ToList();
                double p50 = sorted[sorted.Count / 2], p95 = sorted[(int)(sorted.Count * 0.95)], p99 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.99))];
                _perf.Text = $"{Engine.GetFramesPerSecond():0} FPS · Frame p50 {p50:0.0} ms / p95 {p95:0.0} ms / p99 {p99:0.0} ms · Raster {CreatureRenderQueue.LastRasterMs:0.00} ms for {CreatureRenderQueue.LastBatchSize} · Upload {CreatureRenderQueue.LastUploadMs:0.00} ms";
            }
            string status = string.Empty;
            if (_mapTask != null) status = $"Generating map {MapSize.All[_mapIndex]}…";
            else if (CreatureCount < _targetPopulation) status = $"Creatures appearing… {CreatureCount}/{_targetPopulation}";
            else if (_mapIndex < MapSize.All.Length - 1 && _map.Width * (double)_map.Height / CreatureCount < 9000)
                status = "Tip: it's getting crowded. Use “+” next to Map or PgUp for a larger map.";
            _status.Text = status;
            _status.Visible = status.Length > 0;
        }

    }
}
