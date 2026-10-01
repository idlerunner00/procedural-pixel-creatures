// Procedural Pixel Creatures - CreatureActor: the reusable runtime creature node.
//
// Drop CreatureActor.tscn into any Godot 2D scene. Give it a genome (preset path, family + seed or
// SetGenome from code), then drive it with MoveVelocity (ground pixels/second) or by setting
// GroundPosition every frame, and call TriggerAction / TriggerHit / Resting. The actor simulates
// on a fixed 60 Hz clock, renders its pixel frame at the chosen animation rate (worker threads) and
// shows it through a child Sprite2D whose origin is the creature's ground point.
//
// Coordinates: GroundPosition is (x = east, y = south) on the ground plane in logical pixels. The node
// position is the matching screen position under the fixed 30 degree camera (south is foreshortened).

using System;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Rendering;

namespace PixelCreatures.Runtime
{
    [GlobalClass]
    public partial class CreatureActor : Node2D
    {
        [Signal] public delegate void ModelChangedEventHandler();
        [Signal] public delegate void BuildFailedEventHandler(string message);
        [Signal] public delegate void MotionStateChangedEventHandler(string state);

        /// <summary>Optional preset to load on ready (res:// or user://).</summary>
        [Export(PropertyHint.File, "*.json")] public string PresetPath { get; set; } = string.Empty;
        /// <summary>Family used when no preset is given.</summary>
        [Export] public string FamilyId { get; set; } = "quadruped";
        /// <summary>Seed text (number or any text) used when no preset is given.</summary>
        [Export] public string SeedText { get; set; } = "1";
        /// <summary>Pose snapshots per second (pixel-art animation rate). 0 = every rendered frame.</summary>
        [Export(PropertyHint.Range, "0,60")] public int AnimationFps { get; set; } = 15;
        /// <summary>Classic stepped look: the node moves only on animation ticks.</summary>
        [Export] public bool SteppedMovement { get; set; }
        [Export] public FacingMode Facing { get; set; } = FacingMode.Eight;
        [Export] public bool DrawShadow { get; set; } = true;
        [Export(PropertyHint.Range, "0,2")] public int Quality { get; set; } = 2;
        private bool _showRig, _showContacts;
        [Export] public bool ShowRig { get => _showRig; set { if (_showRig == value) return; _showRig = value; QueueRedraw(); } }
        [Export] public bool ShowContacts { get => _showContacts; set { if (_showContacts == value) return; _showContacts = value; QueueRedraw(); } }
        [Export(PropertyHint.Range, "0,4,0.05")] public float TimeScale { get; set; } = 1f;
        [Export] public bool Paused { get; set; }
        /// <summary>true: MoveVelocity moves the creature. false: set GroundPosition every frame yourself.</summary>
        [Export] public bool AutoMove { get; set; } = true;
        /// <summary>Keep Node2D.Position in sync with GroundPosition.</summary>
        [Export] public bool SyncNodePosition { get; set; } = true;
        /// <summary>Water surface height for swimmers (NaN = no water). Parts below get tinted.</summary>
        public double WaterLevel { get; set; } = double.NaN;
        /// <summary>Optional ground-plane collision and elevation supplied by the host world.</summary>
        public Func<Vector2, Vector2, bool>? CanMove { get; set; }
        public Func<Vector2, float>? SurfaceHeight { get; set; }
        public Func<double, double, double>? GroundHeightAt { get; set; }

        public CreatureModel? Model { get; private set; }
        public CreatureMotor? Motor { get; private set; }
        public CreatureGenome? Genome => Model?.Genome;
        public Vector2 GroundPosition { get; set; }
        public Vector2 MoveVelocity { get; set; }
        public ImageTexture? Texture => _texture;
        public PixelFrame? Frame => _frame;
        public RenderStats LastStats { get; private set; }
        public int CanvasGrowths { get; private set; }
        /// <summary>Largest content size (pixels) seen since the current model was applied. Used for auto zoom.</summary>
        public Vector2I ContentExtent { get; private set; }
        /// <summary>Content centre offset relative to the ground point (pixels, y down), tracked with the extent.</summary>
        public Vector2 ContentCenter { get; private set; }
        public long RenderCount { get; private set; }
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Skips rasterization and drawing while the creature's canvas is outside the visible area of its
        /// viewport (plus <see cref="CullMargin"/>). Simulation, movement and state changes continue; the
        /// frame is refreshed as soon as the creature is visible again. For large maps with many creatures.
        /// </summary>
        [Export] public bool CullOffscreen { get; set; }
        /// <summary>Extra margin (viewport pixels) around the visible area before a creature counts as off screen.</summary>
        [Export(PropertyHint.Range, "0,256")] public int CullMargin { get; set; } = 16;
        /// <summary>True while the creature is off screen and <see cref="CullOffscreen"/> is enabled.</summary>
        public bool IsCulled => _culled;

        internal bool QueuedForRender;

        private Sprite2D? _sprite;
        private ImageTexture? _texture;
        private Image? _image;
        private PixelFrame? _frame;
        private byte[] _rgba = Array.Empty<byte>();
        private readonly byte[] _lut = new byte[256 * 4];
        private CanvasSpec? _canvas;
        private readonly FixedClock _clock = new FixedClock();
        private readonly CreatureBuildRequester _requester = new CreatureBuildRequester();
        private double _sinceRender = 1e9;
        private bool _phased;
        private Vector2 _snapped;
        private bool _dirty = true;
        private bool _needRaster = true;
        private double _lastFlash;
        private ViewSpec _view;
        private CreaturePose? _renderPose;
        private int _pendingGrowL, _pendingGrowR, _pendingGrowT, _pendingGrowB;
        private int _pendingSteps;
        private MotionStateKind _lastState = MotionStateKind.Idle;
        private double? _facingRequest;
        private bool _restRequest;
        private bool _started;
        private bool _culled;
        private Vector2I _extentMin, _extentMax;

        public override void _Ready()
        {
            _sprite = GetNodeOrNull<Sprite2D>("Sprite");
            if (_sprite == null)
            {
                _sprite = new Sprite2D { Name = "Sprite" };
                AddChild(_sprite);
            }
            _sprite.Centered = false;
            _sprite.TextureFilter = TextureFilterEnum.Nearest;
            // the debug overlays (_Draw of this node: rig, contacts, canvas) must stay above the sprite
            _sprite.ShowBehindParent = true;
            _started = true;
            // actors placed in the editor start where they were placed
            if (GroundPosition == Vector2.Zero && Position != Vector2.Zero) GroundPosition = GodotConvert.ScreenToGround(Position);
            if (Model == null)
            {
                if (!string.IsNullOrEmpty(PresetPath)) LoadPreset(PresetPath);
                else if (!string.IsNullOrEmpty(FamilyId) && CreatureRuntime.Registry.TryGetFamily(FamilyId, out _))
                    SetGenome(CreatureRuntime.Sample(FamilyId, StableHash.SeedFromText(SeedText)), async: false);
            }
        }

        public override void _ExitTree()
        {
            CreatureRenderQueue.Remove(this);
            _requester.Cancel();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationPredelete)
            {
                _requester.Dispose();
                if (_sprite != null && IsInstanceValid(_sprite)) _sprite.Texture = null;
                _texture?.Dispose();
                _image?.Dispose();
                _texture = null;
                _image = null;
            }
        }

        // ------------------------------------------------------------------ genome & model

        /// <summary>Loads a preset file; errors are reported through BuildFailed and LastError.</summary>
        public bool LoadPreset(string path)
        {
            var res = CreatureRuntime.LoadPreset(path);
            if (!res.Ok)
            {
                LastError = res.Describe();
                EmitSignal(SignalName.BuildFailed, LastError);
                return false;
            }
            SetGenome(res.Genome!, async: false);
            return true;
        }

        /// <summary>Replaces the creature. Async builds keep showing the old creature until the new one is ready.</summary>
        public void SetGenome(CreatureGenome genome, bool async = true)
        {
            if (!async)
            {
                try { ApplyModel(CreatureRuntime.Build(genome)); }
                catch (Exception e)
                {
                    LastError = e.Message;
                    EmitSignal(SignalName.BuildFailed, e.Message);
                }
                return;
            }
            _requester.Request(genome, ApplyModel, msg =>
            {
                LastError = msg;
                EmitSignal(SignalName.BuildFailed, msg);
            });
        }

        public bool BuildPending => _requester.Busy;
        public long DiscardedBuilds => _requester.Discarded;

        /// <summary>Uses an already built model (keeps position, facing and rest state).</summary>
        public void ApplyModel(CreatureModel model)
        {
            var oldMotor = Motor;
            Model = model;
            Motor = new CreatureMotor(model, CreatureRuntime.Registry, new MotorOptions { Facing = Facing, InternalDrive = AutoMove });
            double yaw = oldMotor?.Yaw ?? PixelCamera.DirectionYaw(7);
            Motor.Teleport(new Vec3(GroundPosition.X, 0, GroundPosition.Y), yaw);
            if (_facingRequest.HasValue) Motor.FacingOverride = _facingRequest;
            if (oldMotor != null && oldMotor.Resting) Motor.SnapRest(true);
            else if (_restRequest) Motor.SnapRest(true);
            Motor.Resting = _restRequest;
            _renderPose = new CreaturePose(model.Anatomy.Skeleton);
            ContentExtent = Vector2I.Zero;
            _extentMin = new Vector2I(int.MaxValue, int.MaxValue);
            _extentMax = new Vector2I(int.MinValue, int.MinValue);
            SetCanvas(model.Anatomy.Canvas);
            _clock.Reset();
            _dirty = true;
            _needRaster = true;
            LastError = string.Empty;
            if (_started) RequestRender();
            EmitSignal(SignalName.ModelChanged);
        }

        private void SetCanvas(CanvasSpec c)
        {
            _canvas = c;
            if (_frame != null && _frame.Width == c.Width && _frame.Height == c.Height && _image != null && _texture != null)
            {
                // same canvas size: keep buffers, image and texture (no engine allocations)
                _frame.OriginX = c.OriginX;
                _frame.OriginY = c.OriginY;
                if (_sprite != null) _sprite.Offset = new Vector2(-c.OriginX, -c.OriginY);
                return;
            }
            var oldImage = _image;
            var oldTexture = _texture;
            _frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            _rgba = new byte[c.Width * c.Height * 4];
            _image = Image.CreateEmpty(c.Width, c.Height, false, Image.Format.Rgba8);
            _texture = ImageTexture.CreateFromImage(_image);
            if (_sprite != null)
            {
                _sprite.Texture = _texture;
                _sprite.Offset = new Vector2(-c.OriginX, -c.OriginY);
            }
            // release the engine side of the replaced resources now instead of at the next GC
            oldTexture?.Dispose();
            oldImage?.Dispose();
        }

        // ------------------------------------------------------------------ commands

        public void FaceYaw(double yaw)
        {
            _facingRequest = yaw;
            if (Motor != null) Motor.FacingOverride = yaw;
        }

        public void FaceDirection(int dir8) => FaceYaw(PixelCamera.DirectionYaw(dir8));

        public bool Resting
        {
            get => _restRequest;
            set
            {
                _restRequest = value;
                if (Motor != null) Motor.Resting = value;
            }
        }

        public void TriggerAction() => Motor?.TriggerAction();

        /// <summary>Hit coming from a ground direction (x east, y south) pointing from the creature to the attacker.</summary>
        public void TriggerHit(Vector2 fromDirection, float strength = 1f)
        {
            Motor?.TriggerHit(new Vec3(fromDirection.X, 0, fromDirection.Y), strength);
        }

        public void Teleport(Vector2 groundPosition)
        {
            GroundPosition = groundPosition;
            Motor?.Teleport(new Vec3(groundPosition.X, 0, groundPosition.Y), Motor.Yaw);
            _dirty = true;
        }

        /// <summary>Advances a paused actor by a number of animation frames.</summary>
        public void StepFrames(int frames)
        {
            int fps = AnimationFps > 0 ? AnimationFps : 60;
            _pendingSteps += Math.Max(1, frames) * Math.Max(1, (int)Math.Round(60.0 / fps));
        }

        public MotionStateKind State => Motor?.State ?? MotionStateKind.Idle;

        public void RequestRender()
        {
            _dirty = true;
            _needRaster = true;
        }

        // ------------------------------------------------------------------ frame loop

        /// <summary>Accumulated Stopwatch ticks spent in actor _Process (simulation + bookkeeping), all actors.</summary>
        public static long ProcessTicks;

        public override void _Process(double delta)
        {
            if (Motor == null) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            try { ProcessFrame(delta); }
            finally { ProcessTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; }
        }

        private void ProcessFrame(double delta)
        {
            if (Motor == null) return;
            if (Motor.Options.InternalDrive != AutoMove)
            {
                Motor.Options.InternalDrive = AutoMove;
            }
            Motor.Options.Facing = Facing;
            Motor.Context.GroundHeightAt = GroundHeightAt;
            int steps = 0;
            if (!Paused) steps = _clock.Advance(delta * Math.Max(0, TimeScale));
            steps += _pendingSteps;
            _pendingSteps = 0;
            for (int i = 0; i < steps; i++)
            {
                var before = Motor.Position;
                if (AutoMove) Motor.DesiredVelocity = new Vec3(MoveVelocity.X, 0, MoveVelocity.Y);
                else Motor.SetWorldPosition(new Vec3(GroundPosition.X, 0, GroundPosition.Y));
                Motor.Step(_clock.StepSize);
                if (AutoMove && CanMove != null && !CanMove(new Vector2((float)before.X, (float)before.Z), new Vector2((float)Motor.Position.X, (float)Motor.Position.Z)))
                    Motor.Teleport(before, Motor.Yaw);
                _sinceRender += _clock.StepSize;
            }
            if (AutoMove) GroundPosition = new Vector2((float)Motor.Position.X, (float)Motor.Position.Z);

            var state = Motor.State;
            if (state != _lastState)
            {
                _lastState = state;
                EmitSignal(SignalName.MotionStateChanged, state.ToString());
            }

            double frameInterval = AnimationFps > 0 ? 1.0 / AnimationFps : 0;
            bool tick = steps > 0 && _sinceRender + 1e-9 >= frameInterval;
            Vector2 screen = GodotConvert.GroundToScreen(GroundPosition.X, GroundPosition.Y);
            if (SurfaceHeight != null) screen.Y -= SurfaceHeight(GroundPosition);
            Vector2 snapped = new Vector2(Mathf.Round(screen.X), Mathf.Round(screen.Y));
            bool moved = snapped != _snapped;
            if (SyncNodePosition && (!SteppedMovement || tick || _dirty)) Position = snapped;
            double flash = Motor.HitFlash;
            bool flashChanged = Math.Abs(flash - _lastFlash) > 1e-4;
            UpdateCulling();
            // the image is re-rasterized only at the animation frame rate; between animation frames the
            // node follows the ground position pixel by pixel (smooth) or with the frames (stepped)
            bool render = (_dirty || tick) && !_culled;
            if (moved && !SteppedMovement) _snapped = snapped;
            if (render)
            {
                _snapped = snapped;
                _needRaster = true;
                PrepareView(screen);
                // desynchronize actors: each one gets its own phase inside the animation frame interval,
                // so a crowd spreads its rasterization evenly over the display frames
                if (!_phased && frameInterval > 0)
                {
                    _phased = true;
                    _sinceRender = -frameInterval * StableHash.Hash01(GetInstanceId(), 7, 11);
                }
                else _sinceRender = 0;
                _dirty = false;
                CreatureRenderQueue.Enqueue(this);
            }
            else if (flashChanged && !_culled)
            {
                _needRaster = false;
                CreatureRenderQueue.Enqueue(this);
            }
            _lastFlash = flash;
            if ((ShowRig || ShowContacts) && !_culled) QueueRedraw();
        }

        private void UpdateCulling()
        {
            bool culled = CullOffscreen && _canvas != null && !CanvasOnScreen(_canvas);
            if (culled == _culled) return;
            _culled = culled;
            if (_sprite != null) _sprite.Visible = !culled;
            // the last uploaded frame is stale after a culled stretch: refresh it right away
            if (!culled) _dirty = true;
        }

        private bool CanvasOnScreen(CanvasSpec c)
        {
            var local = new Rect2(-c.OriginX, -c.OriginY, c.Width, c.Height);
            var onCanvas = GetGlobalTransformWithCanvas() * local;
            return GetViewportRect().Grow(CullMargin).Intersects(onCanvas);
        }

        private void PrepareView(Vector2 exactScreen)
        {
            if (Motor == null || _renderPose == null) return;
            _renderPose.CopyFrom(Motor.Pose);
            double sinE = DMath.Sin(PixelCamera.DefaultElevation);
            double dx = exactScreen.X - _snapped.X, dy = exactScreen.Y - _snapped.Y;
            _view = ViewSpec.Default(Motor.Yaw);
            _view.RootOffset = new Vec3(dx, 0, dy / sinE);
            _view.DrawShadow = DrawShadow;
            _view.Quality = Quality;
            _view.WaterLevel = WaterLevel;
        }

        /// <summary>Worker thread: rasterizes the snapshot pose into the RGBA buffer. No engine calls.</summary>
        internal void RenderToBuffer(CreatureRenderer renderer)
        {
            var model = Model;
            var frame = _frame;
            var pose = _renderPose;
            if (model == null || frame == null || pose == null) return;
            if (_needRaster)
            {
                var stats = renderer.Render(model.Anatomy, model.Palette, model.Surface, pose, _view, frame);
                LastStats = stats;
                if (stats.Overflow)
                {
                    _pendingGrowL = Math.Max(_pendingGrowL, stats.OverflowLeft);
                    _pendingGrowR = Math.Max(_pendingGrowR, stats.OverflowRight);
                    _pendingGrowT = Math.Max(_pendingGrowT, stats.OverflowTop);
                    _pendingGrowB = Math.Max(_pendingGrowB, stats.OverflowBottom);
                }
            }
            double flash = Motor?.HitFlash ?? 0;
            model.Palette.BuildLut(_lut, flash);
            frame.ToRgba(_lut, _rgba, model.Palette.WaterTint);
            RenderCount++;
        }

        /// <summary>Main thread: pushes the RGBA buffer into the texture (and grows the canvas if needed).</summary>
        internal void UploadTexture()
        {
            if (_frame == null || _image == null || _texture == null || _canvas == null) return;
            if (_pendingGrowL + _pendingGrowR + _pendingGrowT + _pendingGrowB > 0)
            {
                var c = _canvas;
                int l = _pendingGrowL + 2, r = _pendingGrowR + 2, t = _pendingGrowT + 2, bo = _pendingGrowB + 2;
                _pendingGrowL = _pendingGrowR = _pendingGrowT = _pendingGrowB = 0;
                SetCanvas(new CanvasSpec(Math.Min(1024, c.Width + l + r), Math.Min(1024, c.Height + t + bo), c.OriginX + l, c.OriginY + t));
                CanvasGrowths++;
                RequestRender();
                return;
            }
            _image.SetData(_frame.Width, _frame.Height, false, Image.Format.Rgba8, _rgba);
            _texture.Update(_image);
            if (_frame.ContentBounds(out int x0, out int y0, out int x1, out int y1))
            {
                _extentMin = new Vector2I(Math.Min(_extentMin.X, x0 - _frame.OriginX), Math.Min(_extentMin.Y, y0 - _frame.OriginY));
                _extentMax = new Vector2I(Math.Max(_extentMax.X, x1 - _frame.OriginX), Math.Max(_extentMax.Y, y1 - _frame.OriginY));
                ContentExtent = _extentMax - _extentMin;
                ContentCenter = new Vector2((_extentMin.X + _extentMax.X) * 0.5f, (_extentMin.Y + _extentMax.Y) * 0.5f);
            }
        }

        // ------------------------------------------------------------------ debug overlay

        public override void _Draw()
        {
            if (Motor == null || Model == null || (!ShowRig && !ShowContacts)) return;
            var pose = Motor.Pose;
            double yaw = Motor.Yaw;
            var off = _view.RootOffset;
            Vector2 P(Vec3 p)
            {
                var s = PixelCamera.CreatureToScreen(p, yaw, PixelCamera.DefaultElevation, off);
                return new Vector2((float)s.X, (float)s.Y);
            }
            if (ShowRig)
            {
                var sk = Model.Anatomy.Skeleton;
                for (int i = 0; i < sk.Count; i++)
                {
                    int parent = sk[i].Parent;
                    if (parent < 0) continue;
                    DrawLine(P(pose.Skeleton.World[parent].Origin), P(pose.Skeleton.World[i].Origin), new Color(0.3f, 0.9f, 1f, 0.9f), 1f);
                }
                for (int i = 0; i < sk.Count; i++) DrawRect(new Rect2(P(pose.Skeleton.World[i].Origin) - new Vector2(0.5f, 0.5f), new Vector2(1, 1)), new Color(1, 1, 1, 0.9f));
            }
            if (ShowContacts)
            {
                foreach (var c in pose.Contacts)
                {
                    var p = P(c.Position);
                    DrawRect(new Rect2(p - new Vector2(1, 1), new Vector2(2, 2)), c.Planted ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.6f, 0.1f));
                }
                if (_canvas != null) DrawRect(new Rect2(-_canvas.OriginX, -_canvas.OriginY, _canvas.Width, _canvas.Height), new Color(1, 1, 0, 0.35f), false, 1f);
            }
        }
    }
}
