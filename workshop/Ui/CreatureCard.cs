// Procedural Pixel Creature Workshop - small animated creature preview (own pixel stage + actor).

using System;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class CreatureCard : VBoxContainer
    {
        public PixelStage Stage { get; private set; } = null!;
        public CreatureActor Actor { get; private set; } = null!;
        public CreatureGenome? Genome { get; private set; }
        private Label _title = null!;
        private Label _subtitle = null!;
        private HBoxContainer _buttons = null!;
        private bool _built;

        public CreatureCard() { }

        public void Build(Main main, string title, Vector2 minSize, StageBackground bg = StageBackground.Grass)
        {
            if (_built) return;
            _built = true;
            AddThemeConstantOverride("separation", 2);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _title = UiKit.Label(title, 13, UiKit.Accent);
            // the creature name always stays readable; the (long) inheritance summary gets the rest
            _title.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            _subtitle = UiKit.Label("", 11, UiKit.TextDim);
            _subtitle.ClipText = true;
            _subtitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _subtitle.HorizontalAlignment = HorizontalAlignment.Right;
            _subtitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            var head = new HBoxContainer();
            head.AddChild(_title);
            head.AddChild(_subtitle);
            AddChild(head);
            Stage = new PixelStage { FixedFraming = true, CustomMinimumSize = minSize, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, TargetLogical = new Vector2I(88, 60) };
            AddChild(Stage);
            _buttons = new HBoxContainer();
            _buttons.AddThemeConstantOverride("separation", 3);
            AddChild(_buttons);
            Actor = main.CreateActor();
            Actor.AnimationFps = 12;
            Actor.DrawShadow = true;
            Actor.ModelChanged += () =>
            {
                bool aquatic = Actor.Model?.Anatomy.Rig.Locomotion == LocomotionKind.Swim;
                Stage.Background = aquatic ? StageBackground.Water : bg;
                Actor.WaterLevel = aquatic ? 1000 : double.NaN;
                Actor.DrawShadow = !aquatic;
            };
            CallDeferred(nameof(Attach), (int)bg);
        }

        private void Attach(int bg)
        {
            Stage.Entities.AddChild(Actor);
            Stage.Follow = Actor;
            Stage.FollowOffset = new Vector2(0, -8);
            Stage.Background = (StageBackground)bg;
            if (Genome != null) Actor.SetGenome(Genome, async: true);
        }

        public void AddButton(string text, Action onPressed, string tooltip = "")
        {
            var b = UiKit.Button(text, onPressed, tooltip);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            b.AddThemeFontSizeOverride("font_size", 12);
            _buttons.AddChild(b);
        }

        public void SetGenome(CreatureGenome? genome, string title, string subtitle)
        {
            Genome = genome;
            _title.Text = title;
            _subtitle.Text = subtitle;
            _subtitle.TooltipText = subtitle;
            if (genome == null) { Actor.Visible = false; return; }
            Actor.Visible = true;
            if (Actor.IsInsideTree()) Actor.SetGenome(genome, async: true);
        }

        public void Mirror(MoveMode mode, int dir, double speedBlend)
        {
            if (Actor?.Model == null) return;
            Actor.FaceDirection(dir);
            double speed = mode == MoveMode.Stand ? 0 : DMath.Lerp(Actor.Model.Motion.WalkSpeed, Actor.Model.Motion.RunSpeed, speedBlend);
            Actor.MoveVelocity = AnimationPanel.DirVector(dir) * (float)speed;
        }
    }
}
