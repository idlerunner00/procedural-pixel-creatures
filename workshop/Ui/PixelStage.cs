// Procedural Pixel Creature Workshop - pixel-perfect stage: a low resolution SubViewport shown with an
// integer zoom and nearest filtering, a world anchored procedural background and a follow camera.

using System;
using System.Collections.Generic;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public enum StageBackground
    {
        Grass,
        Meadow,
        Dirt,
        Sand,
        Water,
        Neutral,
        Dark,
    }

    /// <summary>Draws the ground as world anchored tiles around the camera.</summary>
    public partial class GroundLayer : Node2D
    {
        private static readonly Dictionary<StageBackground, ImageTexture> Cache = new Dictionary<StageBackground, ImageTexture>();
        public StageBackground Kind = StageBackground.Grass;
        public new Rect2 Visible;
        public const int TileSize = 128;

        public static ImageTexture TileFor(StageBackground kind)
        {
            if (Cache.TryGetValue(kind, out var t)) return t;
            var gk = kind switch
            {
                StageBackground.Meadow => GroundKind.Meadow,
                StageBackground.Dirt => GroundKind.Dirt,
                StageBackground.Sand => GroundKind.Sand,
                StageBackground.Water => GroundKind.Water,
                _ => GroundKind.Grass,
            };
            t = ImageTexture.CreateFromImage(EnvironmentArt.GroundTile(gk, TileSize, 1234 + (ulong)kind));
            Cache[kind] = t;
            return t;
        }

        public override void _Draw()
        {
            var r = Visible.Grow(TileSize);
            if (Kind == StageBackground.Neutral || Kind == StageBackground.Dark)
            {
                var bg = Kind == StageBackground.Neutral ? Color.Color8(70, 76, 84) : Color.Color8(24, 26, 32);
                var dot = Kind == StageBackground.Neutral ? Color.Color8(84, 91, 100) : Color.Color8(34, 37, 45);
                DrawRect(r, bg);
                int step = 8;
                int x0 = (int)Math.Floor(r.Position.X / step) * step, y0 = (int)Math.Floor(r.Position.Y / step) * step;
                for (int y = y0; y < r.End.Y; y += step)
                    for (int x = x0; x < r.End.X; x += step)
                        if (((x / step + y / step) & 1) == 0) DrawRect(new Rect2(x, y, 1, 1), dot);
                return;
            }
            var tex = TileFor(Kind);
            float tx = Mathf.Floor(r.Position.X / TileSize) * TileSize, ty = Mathf.Floor(r.Position.Y / TileSize) * TileSize;
            DrawTextureRect(tex, new Rect2(tx, ty, Mathf.Ceil(r.Size.X / TileSize + 1) * TileSize, Mathf.Ceil(r.Size.Y / TileSize + 1) * TileSize), true);
        }
    }

    public partial class PixelStage : Control
    {
        public SubViewport Viewport { get; private set; } = null!;
        public TextureRect Display { get; private set; } = null!;
        public Node2D World { get; private set; } = null!;
        public Camera2D Camera { get; private set; } = null!;
        public GroundLayer Ground { get; private set; } = null!;
        public Node2D Entities { get; private set; } = null!;
        private StageWater _water = null!;
        private bool _managedSwimmer;

        /// <summary>Integer zoom. 0 = automatic (largest integer zoom that fits the logical target size).</summary>
        public int Zoom { get; set; }
        public int EffectiveZoom { get; private set; } = 4;
        /// <summary>Logical pixels the automatic zoom tries to fit (width, height).</summary>
        public Vector2I TargetLogical { get; set; } = new Vector2I(160, 100);
        public Node2D? Follow { get; set; }
        /// <summary>Frame the model's complete animation canvas, independent of its current pose.</summary>
        public bool FixedFraming { get; set; }
        /// <summary>Centre the camera on the creature's content instead of its ground point.</summary>
        public bool AutoCenter { get; set; } = true;
        public Vector2 FollowOffset { get; set; }
        public StageBackground Background
        {
            get => Ground?.Kind ?? StageBackground.Grass;
            set { if (Ground != null) { Ground.Kind = value; Ground.QueueRedraw(); } }
        }

        public override void _Ready()
        {
            ClipContents = true;
            MouseFilter = MouseFilterEnum.Pass;
            Viewport = new SubViewport
            {
                Name = "Viewport",
                Size = new Vector2I(160, 100),
                TransparentBg = false,
                CanvasItemDefaultTextureFilter = Godot.Viewport.DefaultCanvasItemTextureFilter.Nearest,
                Snap2DTransformsToPixel = true,
                Snap2DVerticesToPixel = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Disable3D = true,
            };
            AddChild(Viewport);
            World = new Node2D { Name = "World" };
            Viewport.AddChild(World);
            Ground = new GroundLayer { Name = "Ground" };
            World.AddChild(Ground);
            Entities = new Node2D { Name = "Entities", YSortEnabled = true };
            World.AddChild(Entities);
            _water = new StageWater { Stage = this };
            World.AddChild(_water);
            Camera = new Camera2D { Name = "Camera", PositionSmoothingEnabled = false };
            World.AddChild(Camera);
            Camera.MakeCurrent();
            Display = new TextureRect
            {
                Name = "Display",
                Texture = Viewport.GetTexture(),
                TextureFilter = TextureFilterEnum.Nearest,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(Display);
            Resized += Relayout;
            Relayout();
        }

        private void Relayout()
        {
            if (Display == null) return;
            var s = Size;
            int zoom = Zoom;
            if (zoom <= 0)
            {
                var target = TargetLogical;
                if (Follow is CreatureActor ca)
                {
                    // Workshop previews reserve room for every pose once, rather than zooming out
                    // whenever walking, turning, a wingbeat or an action expands the silhouette.
                    if (FixedFraming && ca.Model != null)
                    {
                        var canvas = ca.Model.Anatomy.Canvas;
                        target = new Vector2I(canvas.Width, canvas.Height);
                    }
                    else if (ca.ContentExtent.X > 0)
                        target = new Vector2I(Math.Max(ca.ContentExtent.X + 24, 48), Math.Max(ca.ContentExtent.Y + 20, 40));
                }
                zoom = Math.Max(1, Math.Min((int)(s.X / Math.Max(1, target.X)), (int)(s.Y / Math.Max(1, target.Y))));
            }
            EffectiveZoom = Math.Clamp(zoom, 1, 16);
            int lw = Math.Max(16, (int)Math.Ceiling(s.X / EffectiveZoom));
            int lh = Math.Max(16, (int)Math.Ceiling(s.Y / EffectiveZoom));
            // odd sizes would put the camera centre between pixels
            if ((lw & 1) == 1) lw++;
            if ((lh & 1) == 1) lh++;
            if (Viewport.Size != new Vector2I(lw, lh)) Viewport.Size = new Vector2I(lw, lh);
            var dispSize = new Vector2(lw * EffectiveZoom, lh * EffectiveZoom);
            Display.Size = dispSize;
            Display.Position = new Vector2(Mathf.Floor((s.X - dispSize.X) / 2), Mathf.Floor((s.Y - dispSize.Y) / 2));
        }

        public void SetZoom(int zoom)
        {
            Zoom = zoom;
            Relayout();
        }

        public override void _Process(double delta)
        {
            if (Viewport == null) return;
            Relayout();
            Vector2 target = Camera.Position;
            if (Follow != null && IsInstanceValid(Follow))
            {
                target = Follow.GlobalPosition + FollowOffset;
                if (AutoCenter && Follow is CreatureActor ca)
                {
                    if (FixedFraming && ca.Model != null)
                    {
                        var canvas = ca.Model.Anatomy.Canvas;
                        target = Follow.GlobalPosition + new Vector2(canvas.Width * 0.5f - canvas.OriginX, canvas.Height * 0.5f - canvas.OriginY);
                    }
                    else if (ca.ContentExtent.X > 0) target = Follow.GlobalPosition + ca.ContentCenter;
                }
            }
            Camera.Position = new Vector2(Mathf.Round(target.X), Mathf.Round(target.Y));
            var half = new Vector2(Viewport.Size.X, Viewport.Size.Y) / 2;
            Ground.Visible = new Rect2(Camera.Position - half, Viewport.Size);
            Ground.QueueRedraw();
            _water.Visible = Background == StageBackground.Water;
            _water.QueueRedraw();
            if (Follow is CreatureActor swimmer)
            {
                bool aquatic = swimmer.Model?.Anatomy.Rig.Locomotion == LocomotionKind.Swim;
                bool submerged = aquatic && Background == StageBackground.Water;
                swimmer.WaterLevel = submerged ? 1000 : double.NaN;
                if (aquatic || _managedSwimmer) swimmer.DrawShadow = !submerged;
                _managedSwimmer = submerged;
            }
        }

        /// <summary>Converts a position inside this control to world coordinates of the stage.</summary>
        public Vector2 ControlToWorld(Vector2 local)
        {
            var p = (local - Display.Position) / EffectiveZoom;
            return Camera.Position - new Vector2(Viewport.Size.X, Viewport.Size.Y) / 2 + p;
        }
    }

    public partial class StageWater : Node2D
    {
        public PixelStage Stage = null!;
        private double _time;
        public override void _Process(double delta) => _time += delta;
        public override void _Draw()
        {
            var rect = Stage.Ground.Visible;
            DrawRect(rect, new Color(0.1f, 0.43f, 0.52f, 0.12f));
            int x0 = (int)Math.Floor(rect.Position.X / 23) * 23;
            int y0 = (int)Math.Floor(rect.Position.Y / 19) * 19;
            for (int y = y0; y < rect.End.Y; y += 19)
                for (int x = x0; x < rect.End.X; x += 23)
                {
                    double phase = _time * 0.65 + x * 0.13 + y * 0.21;
                    float alpha = (float)(0.08 + 0.12 * Math.Max(0, Math.Sin(phase)));
                    var p = new Vector2(x + (float)Math.Sin(phase * 0.3) * 3, y);
                    DrawLine(p, p + new Vector2(6, 0), new Color(0.65f, 0.92f, 0.88f, alpha));
                }
        }
    }
}
