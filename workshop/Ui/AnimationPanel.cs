// Procedural Pixel Creature Workshop - animation controls for the preview actor.

using System;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public enum MoveMode
    {
        Stand,
        Walk,
        Run,
    }

    public partial class AnimationPanel : VBoxContainer
    {
        public CreatureActor Actor { get; private set; } = null!;
        public PixelStage Stage { get; private set; } = null!;
        public MoveMode Mode { get; private set; } = MoveMode.Stand;
        public int Direction { get; private set; } = 7;
        public double SpeedBlend { get; private set; } = 0.5;
        public event Action? SettingsChanged;

        private Label _status = null!;
        private OptionButton _bgOption = null!;
        private bool _userBackground;
        private bool _autoWater;
        private Button[] _modeButtons = Array.Empty<Button>();
        private Button _restButton = null!;
        private Button _pauseButton = null!;
        private readonly Button[] _dirButtons = new Button[8];
        private HSlider _speed = null!;
        private Label _speedLabel = null!;
        private HSlider _slowmo = null!;
        private Label _slowLabel = null!;

        public void Bind(CreatureActor actor, PixelStage stage)
        {
            Actor = actor;
            Stage = stage;
            AddThemeConstantOverride("separation", 6);
            AddChild(UiKit.Header("Animation"));

            // --- movement state
            var modes = new HBoxContainer();
            modes.AddThemeConstantOverride("separation", 4);
            _modeButtons = new[]
            {
                UiKit.Button("Stand", () => SetMode(MoveMode.Stand), "Idle: breathing, blinking, small gestures (1)"),
                UiKit.Button("Walk", () => SetMode(MoveMode.Walk), "Slow locomotion (2)"),
                UiKit.Button("Run", () => SetMode(MoveMode.Run), "Fast locomotion (3)"),
            };
            foreach (var b in _modeButtons) { b.ToggleMode = true; b.SizeFlagsHorizontal = SizeFlags.ExpandFill; modes.AddChild(b); }
            AddChild(modes);
            var acts = new HBoxContainer();
            acts.AddThemeConstantOverride("separation", 4);
            var action = UiKit.Button("Action", () => Actor.TriggerAction(), "Expressive action (Space)");
            var hit = UiKit.Button("Hit", () => Actor.TriggerHit(DirVector(Direction), 1f), "Hit reaction from the front (H)");
            _restButton = UiKit.Toggle("Rest", false, on => { Actor.Resting = on; Emit(); }, "Lie down / sleep, with transitions (R)");
            foreach (var b in new[] { action, hit, _restButton }) { b.SizeFlagsHorizontal = SizeFlags.ExpandFill; acts.AddChild(b); }
            AddChild(acts);

            // --- direction pad
            AddChild(UiKit.Label("Direction", 13, UiKit.TextDim));
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 3);
            grid.AddThemeConstantOverride("v_separation", 3);
            int[] layout = { 3, 2, 1, 4, -1, 0, 5, 6, 7 };
            foreach (int d in layout)
            {
                if (d < 0)
                {
                    var turn = new Button { Text = "45°", FocusMode = FocusModeEnum.None, TooltipText = "Turn by 45° (Q/E)", CustomMinimumSize = new Vector2(34, 30) };
                    turn.Pressed += () => SetDirection((Direction + 7) % 8);
                    grid.AddChild(turn);
                    continue;
                }
                int dd = d;
                var b = new Button { Icon = Icons.Direction(d, 2), ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = PixelCamera.DirectionLabels[d], CustomMinimumSize = new Vector2(34, 30) };
                b.Pressed += () => SetDirection(dd);
                _dirButtons[d] = b;
                grid.AddChild(b);
            }
            var gridRow = new HBoxContainer();
            gridRow.AddChild(grid);
            AddChild(gridRow);

            // --- speed
            _speedLabel = UiKit.Label("Speed", 13, UiKit.TextDim);
            AddChild(_speedLabel);
            _speed = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.01, Value = SpeedBlend, FocusMode = FocusModeEnum.None, TooltipText = "Blend between walking and running speed" };
            _speed.ValueChanged += v => { SpeedBlend = v; ApplyMovement(); };
            AddChild(_speed);

            // --- time controls
            AddChild(UiKit.Label("Time", 13, UiKit.TextDim));
            var time = new HBoxContainer();
            time.AddThemeConstantOverride("separation", 4);
            _pauseButton = new Button { Icon = Icons.Get("pause"), ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = "Pause (P)" };
            _pauseButton.Toggled += on => { Actor.Paused = on; _pauseButton.Icon = Icons.Get(on ? "play" : "pause"); Emit(); };
            var step = new Button { Icon = Icons.Get("step"), FocusMode = FocusModeEnum.None, TooltipText = "Single step: next frame (N)" };
            step.Pressed += () => { if (!Actor.Paused) _pauseButton.ButtonPressed = true; Actor.StepFrames(1); };
            time.AddChild(_pauseButton);
            time.AddChild(step);
            _slowmo = new HSlider { MinValue = 0.05, MaxValue = 1.0, Step = 0.05, Value = 1.0, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None, TooltipText = "Slow motion" };
            _slowLabel = UiKit.Label("100%", 12, UiKit.TextDim);
            _slowmo.ValueChanged += v => { Actor.TimeScale = (float)v; _slowLabel.Text = $"{v * 100:0}%"; };
            time.AddChild(_slowmo);
            time.AddChild(_slowLabel);
            AddChild(time);

            var rate = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Pixel animation rate: how often per second a new pose is rasterized" };
            int[] rates = { 8, 12, 15, 24, 30, 60 };
            foreach (int r in rates) rate.AddItem($"{r} fps");
            rate.Selected = Array.IndexOf(rates, Actor.AnimationFps) >= 0 ? Array.IndexOf(rates, Actor.AnimationFps) : 2;
            rate.ItemSelected += i => Actor.AnimationFps = rates[i];
            var stepped = UiKit.Toggle("Stepped", Actor.SteppedMovement, on => Actor.SteppedMovement = on, "Update the position only on animation frames (classic pixel look)");
            AddChild(UiKit.Row(4, rate, stepped));

            // --- overlays
            AddChild(UiKit.Label("Display", 13, UiKit.TextDim));
            var ov = new HBoxContainer();
            ov.AddThemeConstantOverride("separation", 4);
            ov.AddChild(UiKit.Toggle("Rig", false, on => Actor.ShowRig = on, "Show the bones of the rig"));
            ov.AddChild(UiKit.Toggle("Contacts", false, on => Actor.ShowContacts = on, "Show foot contacts (green = planted) and the canvas bounds"));
            ov.AddChild(UiKit.Toggle("Shadow", true, on => { Actor.DrawShadow = on; Actor.RequestRender(); }, "Ground shadow"));
            AddChild(ov);
            var bg = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Preview background (aquatic creatures switch to water automatically)" };
            string[] bgs = { "Grass", "Meadow", "Dirt", "Sand", "Water", "Neutral", "Dark" };
            foreach (var b in bgs) bg.AddItem(b);
            bg.ItemSelected += i => { Stage.Background = (StageBackground)(int)i; _userBackground = true; Emit(); };
            _bgOption = bg;
            var zoomOut = UiKit.Button("-", () => Stage.SetZoom(Math.Max(1, Stage.EffectiveZoom - 1)), "Zoom out");
            var zoomAuto = UiKit.Button("Auto", () => Stage.SetZoom(0), "Automatic integer zoom");
            var zoomIn = UiKit.Button("+", () => Stage.SetZoom(Math.Min(16, Stage.EffectiveZoom + 1)), "Zoom in");
            AddChild(UiKit.Row(4, bg, zoomOut, zoomAuto, zoomIn));

            _status = UiKit.Label("", 12, UiKit.TextDim);
            _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _status.CustomMinimumSize = new Vector2(200, 0);
            AddChild(_status);
            SetMode(MoveMode.Stand);
            SetDirection(7);
        }

        private void Emit() => SettingsChanged?.Invoke();

        public static Vector2 DirVector(int dir8)
        {
            double yaw = PixelCamera.DirectionYaw(dir8);
            return new Vector2((float)DMath.Cos(yaw), (float)-DMath.Sin(yaw));
        }

        public void SetMode(MoveMode mode)
        {
            Mode = mode;
            for (int i = 0; i < _modeButtons.Length; i++) _modeButtons[i].SetPressedNoSignal(i == (int)mode);
            if (mode == MoveMode.Walk) _speed.SetValueNoSignal(SpeedBlend = 0.0);
            if (mode == MoveMode.Run) _speed.SetValueNoSignal(SpeedBlend = 1.0);
            if (mode != MoveMode.Stand && _restButton.ButtonPressed) _restButton.ButtonPressed = false;
            ApplyMovement();
        }

        public void SetDirection(int dir8)
        {
            Direction = ((dir8 % 8) + 8) % 8;
            for (int i = 0; i < 8; i++) _dirButtons[i]?.SetPressedNoSignal(i == Direction);
            Actor.FaceDirection(Direction);
            ApplyMovement();
        }

        public void ToggleRest() => _restButton.ButtonPressed = !_restButton.ButtonPressed;
        public void TogglePause() => _pauseButton.ButtonPressed = !_pauseButton.ButtonPressed;

        /// <summary>Swimmers are previewed over water, everything else keeps the chosen ground.</summary>
        public void AutoBackground()
        {
            var model = Actor.Model;
            if (model == null || _bgOption == null) return;
            bool swimmer = model.Anatomy.Rig.Locomotion == LocomotionKind.Swim;
            if (swimmer && Stage.Background != StageBackground.Water && (!_userBackground || _autoWater))
            {
                Stage.Background = StageBackground.Water;
                _bgOption.Selected = (int)StageBackground.Water;
                _autoWater = true;
            }
            else if (!swimmer && _autoWater && Stage.Background == StageBackground.Water)
            {
                Stage.Background = StageBackground.Grass;
                _bgOption.Selected = (int)StageBackground.Grass;
                _autoWater = false;
            }
        }

        public void ApplyMovement()
        {
            var model = Actor.Model;
            if (model == null) return;
            double speed = 0;
            if (Mode != MoveMode.Stand)
                speed = DMath.Lerp(model.Motion.WalkSpeed, model.Motion.RunSpeed, SpeedBlend);
            Actor.MoveVelocity = DirVector(Direction) * (float)speed;
            _speedLabel.Text = Mode == MoveMode.Stand ? "Speed (standing)" : $"Speed {speed:0} px/s";
            Emit();
        }

        public override void _Process(double delta)
        {
            if (Actor?.Motor == null || _status == null) return;
            var m = Actor.Motor;
            var loco = Actor.Model?.Anatomy.Rig.Locomotion ?? LocomotionKind.Legged;
            bool airborne = m.Altitude > 2.0 && loco != LocomotionKind.Swim;
            string state = m.State switch
            {
                MotionStateKind.Idle => loco == LocomotionKind.Swim ? "Floating in water" : (airborne ? "Hovering" : "Standing"),
                MotionStateKind.Walk => loco switch { LocomotionKind.Swim => "Swimming", LocomotionKind.Serpentine => "Slithering", LocomotionKind.Hop => "Hopping", LocomotionKind.Float => "Gliding", _ => airborne ? "Flying" : "Walking" },
                MotionStateKind.Run => loco switch { LocomotionKind.Swim => "Swimming fast", LocomotionKind.Serpentine => "Slithering fast", LocomotionKind.Hop => "Hopping far", LocomotionKind.Float => "Gliding fast", _ => airborne ? "Flying" : "Running" },
                MotionStateKind.Action => "Action",
                MotionStateKind.Hit => "Hit",
                MotionStateKind.Rest => "Rest/sleep",
                _ => "Transition",
            };
            _status.Text = $"State: {state}\nSpeed: {m.Speed:0.0} px/s  Facing: {PixelCamera.DirectionLabels[PixelCamera.YawToDirection8(m.Yaw)]}\nFrame rate: {Actor.AnimationFps} fps  Zoom: {Stage.EffectiveZoom}x\nFrame: {Actor.Frame?.Width}x{Actor.Frame?.Height}";
        }
    }
}
