// Procedural Pixel Creature Workshop - overview: generate up to 100 different creatures at once (random
// per family/archetype or as offspring of a parent), show them animated in a grid and inspect any of
// them in a large preview. Models are built on worker threads; the grid only rasterizes visible cells.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class OverviewView : HBoxContainer
    {
        public const int MaxCreatures = 100;

        public enum SourceKind
        {
            /// <summary>Fresh creatures from seeds (all families round robin, one family or one archetype).</summary>
            Random,
            /// <summary>Mutations of a parent genome (the editor creature or a selected one).</summary>
            Offspring,
        }

        public enum SortKind
        {
            Generation,
            Family,
            Archetype,
            Size,
            Colour,
            FavoritesFirst,
        }

        /// <summary>Everything a generation run depends on (snapshot, used on a worker thread).</summary>
        public sealed class GenerationSettings
        {
            public SourceKind Source = SourceKind.Random;
            public string? Family;
            public string? Archetype;
            public int Count = MaxCreatures;
            public ulong Seed = 1;
            public CreatureGenome? Parent;
            public MutationSettings Mutation = new MutationSettings { Strength = 0.35 };
            public GeneLocks? Locks;
        }

        // ------------------------------------------------------------------ state

        public OverviewGrid Grid { get; private set; } = null!;
        public IReadOnlyList<OverviewEntry> Entries => _entries;
        /// <summary>Display order (sorted view of the entries).</summary>
        public IReadOnlyList<OverviewEntry> Order => _order;
        public OverviewEntry? SelectedEntry => _selected;
        public CreatureActor InspectorActor { get; private set; } = null!;
        public bool Busy => _job != null || _pendingActors.Count > 0;
        public int ActorCount => _entries.Count(e => e.Actor != null);
        public MoveMode Mode { get; private set; } = MoveMode.Stand;
        public int Direction { get; private set; } = 7;
        public SortKind Sort { get; private set; } = SortKind.Generation;
        public GenerationSettings Settings { get; } = new GenerationSettings();
        public double LastGenerationMs { get; private set; }
        public bool HasGenerated { get; private set; }

        private Main _main = null!;
        private readonly List<OverviewEntry> _entries = new List<OverviewEntry>();
        private List<OverviewEntry> _order = new List<OverviewEntry>();
        private OverviewEntry? _selected;
        private readonly Queue<OverviewEntry> _pendingActors = new Queue<OverviewEntry>();
        private Task<List<OverviewEntry>>? _job;
        private CancellationTokenSource? _jobCancel;
        private Stopwatch _jobWatch = new Stopwatch();
        private Vector2I _cellSize = new Vector2I(112, 88);
        private readonly List<(CreatureActor actor, double delay)> _staggered = new List<(CreatureActor, double)>();
        private bool _restAll;
        private MoveMode _inspMode = MoveMode.Stand;
        private int _inspDir = 7;
        private ulong _rerollCounter;

        // controls
        private OptionButton _source = null!, _family = null!, _archetype = null!, _sort = null!, _bg = null!;
        private HSlider _count = null!, _strength = null!;
        private Label _countLabel = null!, _strengthLabel = null!, _parentLabel = null!, _status = null!, _dirLabel = null!;
        private LineEdit _seed = null!;
        private Control _mutationBox = null!, _filterBox = null!;
        private Button _respectLocks = null!, _restButton = null!;
        private Button[] _modeButtons = Array.Empty<Button>();

        // inspector
        private PixelStage _inspStage = null!;
        private Label _inspTitle = null!, _inspSub = null!;
        private RichTextLabel _inspInfo = null!;
        private HFlowContainer _swatches = null!;
        private Button _favButton = null!;
        private Button[] _inspModeButtons = Array.Empty<Button>();
        private Control _inspBody = null!;
        private Label _inspEmpty = null!;

        public void Bind(Main main)
        {
            _main = main;
            AddThemeConstantOverride("separation", 6);
            AddChild(BuildSettings());
            Grid = new OverviewGrid { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            var gridCard = UiKit.Card(Grid);
            gridCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddChild(gridCard);
            AddChild(BuildInspector());
            AttachInspectorActor();
            Grid.CellClicked += i => Select(i);
            Grid.CellActivated += i => { Select(i); OpenInEditor(); };
            Grid.HoverChanged += OnHover;
            Settings.Seed = RandomSeed();
            _seed.Text = Settings.Seed.ToString(CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------ settings column

        private Control BuildSettings()
        {
            var col = UiKit.Column(5);
            col.CustomMinimumSize = new Vector2(250, 0);
            col.AddChild(UiKit.Header("Overview"));
            var intro = UiKit.Label("Generate up to 100 different creatures at once, compare them in motion and inspect each one.", 12, UiKit.TextDim);
            intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            intro.CustomMinimumSize = new Vector2(236, 0);
            col.AddChild(intro);

            col.AddChild(UiKit.Label("Source", 13, UiKit.TextDim));
            _source = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "New creatures from seeds, or offspring (mutations) of a parent" };
            _source.AddItem("New creatures from seeds");
            _source.AddItem("Offspring of a parent");
            _source.ItemSelected += i => { Settings.Source = (SourceKind)(int)i; UpdateSourceUi(); };
            col.AddChild(_source);

            // --- random: family and archetype
            _filterBox = UiKit.Column(4);
            _family = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "All families in turn, or just one family" };
            _family.AddItem("All families");
            foreach (var f in CreatureRuntime.Registry.Families) _family.AddItem(f.DisplayName);
            _family.ItemSelected += _ => { FillArchetypes(); };
            _archetype = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Only creatures of one archetype (requires a selected family)" };
            _filterBox.AddChild(_family);
            _filterBox.AddChild(_archetype);
            col.AddChild(_filterBox);
            FillArchetypes();

            // --- offspring: parent and mutation strength
            _mutationBox = UiKit.Column(4);
            _parentLabel = UiKit.Label("Parent: –", 12, UiKit.Text);
            _parentLabel.ClipText = true;
            _parentLabel.CustomMinimumSize = new Vector2(236, 0);
            _mutationBox.AddChild(_parentLabel);
            _mutationBox.AddChild(UiKit.Button("Use editor creature as parent", () => SetParent(_main.State.Genome), "The creature from the editor becomes the parent"));
            _strengthLabel = UiKit.Label("Mutation strength 35%", 12, UiKit.TextDim);
            _mutationBox.AddChild(_strengthLabel);
            _strength = new HSlider { MinValue = 0.05, MaxValue = 1, Step = 0.01, Value = 0.35, FocusMode = FocusModeEnum.None };
            _strength.ValueChanged += v => { Settings.Mutation.Strength = v; _strengthLabel.Text = $"Mutation strength {v * 100:0}%"; };
            _mutationBox.AddChild(_strength);
            _respectLocks = UiKit.Toggle("Respect editor locks", true, _ => { }, "Traits locked in the editor stay the same in all offspring");
            _mutationBox.AddChild(_respectLocks);
            col.AddChild(_mutationBox);

            // --- count and seed
            _countLabel = UiKit.Label("Count 100", 13, UiKit.TextDim);
            col.AddChild(_countLabel);
            _count = new HSlider { MinValue = 1, MaxValue = MaxCreatures, Step = 1, Value = MaxCreatures, FocusMode = FocusModeEnum.None, TooltipText = "How many creatures to generate (1–100)" };
            _count.ValueChanged += v => { Settings.Count = (int)v; _countLabel.Text = $"Count {(int)v}"; };
            col.AddChild(_count);
            var quick = new HBoxContainer();
            quick.AddThemeConstantOverride("separation", 3);
            foreach (int n in new[] { 9, 25, 50, 100 })
            {
                int c = n;
                var b = UiKit.Button($"{n}", () => _count.Value = c, $"{n} creatures");
                b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                quick.AddChild(b);
            }
            col.AddChild(quick);
            col.AddChild(UiKit.Label("Start seed", 13, UiKit.TextDim));
            _seed = new LineEdit { PlaceholderText = "Number or text", SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Same seed + same settings = same creatures" };
            _seed.TextSubmitted += _ => Generate();
            var dice = new Button { Icon = Icons.Get("dice", 1), FocusMode = FocusModeEnum.None, TooltipText = "Random start seed" };
            dice.Pressed += () => { _seed.Text = RandomSeed().ToString(CultureInfo.InvariantCulture); };
            col.AddChild(UiKit.Row(4, _seed, dice));

            var gen = new Button { Text = "Generate", Icon = Icons.Get("new", 1), FocusMode = FocusModeEnum.None, TooltipText = "Generate creatures with these settings (Enter in the seed field)" };
            gen.Pressed += Generate;
            gen.AddThemeColorOverride("font_color", UiKit.Accent);
            var next = new Button { Text = "New roll", Icon = Icons.Get("dice", 1), FocusMode = FocusModeEnum.None, TooltipText = "New start seed, generate right away (G)" };
            next.Pressed += NewThrow;
            gen.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            next.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(UiKit.Row(4, gen, next));
            var keep = UiKit.Button("Reroll, keep favorites", RerollKeepFavorites, "All creatures without a star are replaced");
            col.AddChild(keep);
            col.AddChild(UiKit.Button("Save favorites as presets", SaveFavorites, "Saves all marked creatures to user://presets"));

            col.AddChild(UiKit.Separator());
            col.AddChild(UiKit.Label("Display", 13, UiKit.TextDim));
            _sort = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Order in the grid" };
            foreach (var s in new[] { "Order: generation", "Order: family", "Order: archetype", "Order: size", "Order: color", "Order: favorites first" }) _sort.AddItem(s);
            _sort.ItemSelected += i => SetSort((SortKind)(int)i);
            col.AddChild(_sort);
            _bg = new OptionButton { FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "Ground of the cells (aquatic creatures always stand in water)" };
            foreach (var b in new[] { "Grass", "Meadow", "Dirt", "Sand", "Water", "Neutral", "Dark" }) _bg.AddItem(b);
            _bg.ItemSelected += i => { Grid.Background = (StageBackground)(int)i; Grid.Relayout(); };
            var zoomOut = UiKit.Button("-", () => Grid.SetZoom(Math.Max(1, Grid.EffectiveZoom - 1)), "Zoom out (-, Ctrl+mouse wheel)");
            var zoomAuto = UiKit.Button("Auto", () => Grid.SetZoom(0), "Automatic zoom");
            var zoomIn = UiKit.Button("+", () => Grid.SetZoom(Math.Min(6, Grid.EffectiveZoom + 1)), "Zoom in (+, Ctrl+mouse wheel)");
            col.AddChild(UiKit.Row(4, _bg, zoomOut, zoomAuto, zoomIn));
            col.AddChild(UiKit.Toggle("Show names", true, on => { Grid.ShowNames = on; Grid.PlaceActors(); }, "Name and archetype below each creature"));

            col.AddChild(UiKit.Label("Movement of all creatures", 13, UiKit.TextDim));
            var modes = new HBoxContainer();
            modes.AddThemeConstantOverride("separation", 3);
            _modeButtons = new[]
            {
                UiKit.Button("Stand", () => SetMode(MoveMode.Stand), "All stand (1)"),
                UiKit.Button("Walk", () => SetMode(MoveMode.Walk), "All walk in place (2)"),
                UiKit.Button("Run", () => SetMode(MoveMode.Run), "All run in place (3)"),
            };
            foreach (var b in _modeButtons) { b.ToggleMode = true; b.SizeFlagsHorizontal = SizeFlags.ExpandFill; modes.AddChild(b); }
            col.AddChild(modes);
            var left = UiKit.Button("⟲", () => SetDirection(Direction + 1), "Turn all 45° to the left (Q)");
            var right = UiKit.Button("⟳", () => SetDirection(Direction - 1), "Turn all 45° to the right (E)");
            _dirLabel = UiKit.Label($"Facing {PixelCamera.DirectionLabels[7]}", 12, UiKit.Text);
            _dirLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _dirLabel.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(UiKit.Row(4, left, _dirLabel, right));
            var actAll = UiKit.Button("Action (all)", ActionAll, "All perform their action, slightly staggered (Shift+Space)");
            _restButton = UiKit.Toggle("Rest", false, on => SetRestAll(on), "All lie down or get back up (R)");
            actAll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(UiKit.Row(4, actAll, _restButton));

            _status = UiKit.Label("", 12, UiKit.TextDim);
            _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _status.CustomMinimumSize = new Vector2(236, 0);
            col.AddChild(_status);
            var help = UiKit.Label("Click: select · Double-click/Enter: open in editor · Arrow keys: move selection · F: favorite · Space: action · Mouse wheel: scroll", 11, UiKit.TextDim);
            help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            help.CustomMinimumSize = new Vector2(236, 0);
            col.AddChild(help);

            UpdateSourceUi();
            SetModeButtons();
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(262, 0) };
            scroll.AddChild(col);
            return UiKit.Card(scroll);
        }

        private void FillArchetypes()
        {
            _archetype.Clear();
            _archetype.AddItem("All archetypes");
            var fam = SelectedFamily();
            if (fam != null) foreach (var a in fam.Archetypes) _archetype.AddItem(a.Label);
            _archetype.Disabled = fam == null;
            _archetype.Selected = 0;
        }

        private ICreatureFamily? SelectedFamily() =>
            _family.Selected <= 0 ? null : CreatureRuntime.Registry.Families[_family.Selected - 1];

        private void UpdateSourceUi()
        {
            bool offspring = Settings.Source == SourceKind.Offspring;
            _filterBox.Visible = !offspring;
            _mutationBox.Visible = offspring;
            if (offspring && Settings.Parent == null) SetParent(_main.State.Genome, select: false);
            if (_source.Selected != (int)Settings.Source) _source.Selected = (int)Settings.Source;
        }

        private void SetParent(CreatureGenome g, bool select = true)
        {
            Settings.Parent = g;
            string fam = CreatureRuntime.Registry.GetFamily(g.FamilyId).DisplayName;
            _parentLabel.Text = $"Parent: {(string.IsNullOrEmpty(g.Name) ? "(unnamed)" : g.Name)} ({fam})";
            _parentLabel.TooltipText = _parentLabel.Text;
            if (select)
            {
                Settings.Source = SourceKind.Offspring;
                UpdateSourceUi();
            }
        }

        // ------------------------------------------------------------------ inspector

        private Control BuildInspector()
        {
            var col = UiKit.Column(5);
            col.CustomMinimumSize = new Vector2(300, 0);
            col.AddChild(UiKit.Header("Inspector"));
            _inspEmpty = UiKit.Label("Click a creature in the grid.", 13, UiKit.TextDim);
            col.AddChild(_inspEmpty);
            _inspBody = UiKit.Column(5);
            _inspBody.SizeFlagsVertical = SizeFlags.ExpandFill;
            col.AddChild(_inspBody);
            _inspTitle = UiKit.Label("", 17, UiKit.Accent);
            _inspTitle.ClipText = true;
            _inspSub = UiKit.Label("", 12, UiKit.TextDim);
            _inspSub.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _inspSub.CustomMinimumSize = new Vector2(284, 0);
            _inspBody.AddChild(_inspTitle);
            _inspBody.AddChild(_inspSub);
            _inspStage = new PixelStage { FixedFraming = true, CustomMinimumSize = new Vector2(284, 220), SizeFlagsHorizontal = SizeFlags.ExpandFill, TargetLogical = new Vector2I(96, 70) };
            _inspBody.AddChild(_inspStage);
            InspectorActor = _main.CreateActor();
            InspectorActor.Name = "InspectorActor";
            InspectorActor.AnimationFps = 15;

            var modes = new HBoxContainer();
            modes.AddThemeConstantOverride("separation", 3);
            _inspModeButtons = new[]
            {
                UiKit.Button("Stand", () => SetInspectorMode(MoveMode.Stand)),
                UiKit.Button("Walk", () => SetInspectorMode(MoveMode.Walk)),
                UiKit.Button("Run", () => SetInspectorMode(MoveMode.Run)),
            };
            foreach (var b in _inspModeButtons) { b.ToggleMode = true; b.SizeFlagsHorizontal = SizeFlags.ExpandFill; modes.AddChild(b); }
            _inspBody.AddChild(modes);
            var acts = new HBoxContainer();
            acts.AddThemeConstantOverride("separation", 3);
            var action = UiKit.Button("Action", () => InspectorActor.TriggerAction(), "Action of the selected creature (Space)");
            var hit = UiKit.Button("Hit", () => InspectorActor.TriggerHit(AnimationPanel.DirVector(_inspDir), 1f));
            var rest = UiKit.Toggle("Rest", false, on => InspectorActor.Resting = on);
            var turnL = UiKit.Button("⟲", () => SetInspectorDirection(_inspDir + 1), "Turn left");
            var turnR = UiKit.Button("⟳", () => SetInspectorDirection(_inspDir - 1), "Turn right");
            foreach (var b in new[] { action, hit, rest }) b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            foreach (var b in new Control[] { action, hit, rest, turnL, turnR }) acts.AddChild(b);
            _inspBody.AddChild(acts);

            _swatches = new HFlowContainer();
            _swatches.AddThemeConstantOverride("h_separation", 2);
            _swatches.AddThemeConstantOverride("v_separation", 2);
            _inspBody.AddChild(_swatches);
            _inspInfo = new RichTextLabel { BbcodeEnabled = true, FitContent = true, SelectionEnabled = true, CustomMinimumSize = new Vector2(284, 0), ScrollActive = false };
            _inspInfo.AddThemeFontSizeOverride("normal_font_size", 12);
            var infoScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 80) };
            infoScroll.AddChild(_inspInfo);
            _inspBody.AddChild(infoScroll);

            var open = new Button { Text = "Open in editor", Icon = Icons.Get("load", 1), FocusMode = FocusModeEnum.None, TooltipText = "Load the creature into the editor (Enter, double-click)" };
            open.Pressed += OpenInEditor;
            open.AddThemeColorOverride("font_color", UiKit.Accent);
            open.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _favButton = UiKit.Toggle("★ Favorite", false, on => { if (_selected != null) { _selected.Favorite = on; UpdateStatus(); } }, "Mark (F); favorites are kept when rerolling");
            _inspBody.AddChild(UiKit.Row(4, open, _favButton));
            var save = UiKit.Button("Save", SaveSelected, "Save as a preset to user://presets");
            var reroll = UiKit.Button("Replace", RerollSelected, "Replace only this creature with a new one");
            var offspring = UiKit.Button("Offspring of this", () => { if (_selected != null) { SetParent(_selected.Genome); Generate(); } }, "Generates offspring of this creature in the grid");
            foreach (var b in new[] { save, reroll, offspring }) b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _inspBody.AddChild(UiKit.Row(3, save, reroll, offspring));
            _inspBody.Visible = false;
            return UiKit.Card(col);
        }

        /// <summary>Hidden views neither process nor render (called by the workshop when switching views).</summary>
        public void SetActive(bool on)
        {
            Visible = on;
            ProcessMode = on ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
            var mode = on ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
            if (Grid?.Viewport != null) Grid.Viewport.RenderTargetUpdateMode = mode;
            if (_inspStage?.Viewport != null) _inspStage.Viewport.RenderTargetUpdateMode = mode;
        }

        private void AttachInspectorActor()
        {
            _inspStage.Entities.AddChild(InspectorActor);
            _inspStage.Follow = InspectorActor;
            SetInspectorMode(MoveMode.Stand);
        }

        private void SetInspectorMode(MoveMode mode)
        {
            _inspMode = mode;
            for (int i = 0; i < _inspModeButtons.Length; i++) _inspModeButtons[i].SetPressedNoSignal(i == (int)mode);
            ApplyInspectorMovement();
        }

        private void SetInspectorDirection(int dir)
        {
            _inspDir = ((dir % 8) + 8) % 8;
            InspectorActor.FaceDirection(_inspDir);
            ApplyInspectorMovement();
        }

        private void ApplyInspectorMovement()
        {
            var m = InspectorActor.Model;
            if (m == null) return;
            double speed = _inspMode switch { MoveMode.Walk => m.Motion.WalkSpeed, MoveMode.Run => m.Motion.RunSpeed, _ => 0 };
            InspectorActor.FaceDirection(_inspDir);
            InspectorActor.MoveVelocity = AnimationPanel.DirVector(_inspDir) * (float)speed;
        }

        private void ShowInspector(OverviewEntry? e)
        {
            _inspBody.Visible = e != null;
            _inspEmpty.Visible = e == null;
            if (e == null) return;
            var g = e.Genome;
            _inspTitle.Text = string.IsNullOrEmpty(g.Name) ? "(unnamed)" : g.Name;
            _inspSub.Text = Settings.Source == SourceKind.Offspring && g.Lineage.Generation > 0
                ? $"{e.ArchetypeLabel} · {e.FamilyLabel} · Offspring (generation {g.Lineage.Generation})"
                : $"{e.ArchetypeLabel} · {e.FamilyLabel} · Seed {e.Seed}";
            _favButton.SetPressedNoSignal(e.Favorite);
            if (InspectorActor.Model != e.Model) InspectorActor.ApplyModel(e.Model);
            _inspStage.Background = e.Aquatic ? StageBackground.Water : (Grid.Background == StageBackground.Water ? StageBackground.Grass : Grid.Background);
            InspectorActor.WaterLevel = e.Aquatic ? 1000 : double.NaN;
            ApplyInspectorMovement();

            foreach (var c in _swatches.GetChildren()) c.QueueFree();
            var pal = e.Model.Palette;
            for (int i = CreaturePalette.FirstColorIndex; i < pal.Colors.Count; i++)
                _swatches.AddChild(new ColorRect { Color = GodotConvert.ToColor(pal.Colors[i]), CustomMinimumSize = new Vector2(11, 11), TooltipText = $"#{pal.Colors[i].R:x2}{pal.Colors[i].G:x2}{pal.Colors[i].B:x2}" });

            var a = e.Model.Anatomy;
            var sb = new StringBuilder();
            sb.Append($"[color=#e2ac4c]Traits[/color] {string.Join(", ", a.Traits)}\n");
            sb.Append(string.Format(CultureInfo.InvariantCulture, "Body length {0:0} px · leg length {1:0} px · walk {2:0} px/s · run {3:0} px/s\n",
                a.Metrics.BodyLength, a.Metrics.LegLength, e.Model.Motion.WalkSpeed, e.Model.Motion.RunSpeed));
            sb.Append($"Parts: {a.Primitives.Count} primitives in {a.Groups.Count} groups, {a.Skeleton.Count} bones · canvas {a.Canvas.Width}×{a.Canvas.Height}\n");
            sb.Append($"Palette: {pal.Colors.Count - CreaturePalette.FirstColorIndex} colors · content ID {g.ContentId}\n");
            if (g.Lineage.Generation == 0)
                sb.Append($"[color=#969aa0]Same creature in the editor: family “{e.FamilyLabel}”, seed {e.Seed}[/color]\n");
            sb.Append("\n[color=#e2ac4c]Genes[/color]\n");
            foreach (var group in g.Schema.Groups)
            {
                var parts = new List<string>();
                for (int gi = 0; gi < g.Schema.Genes.Count; gi++)
                {
                    var d = g.Schema.Genes[gi];
                    if (d.Group != group.Id || d.Advanced) continue;
                    double v = g.Get(d.Id);
                    if (d.Kind == GeneKind.Bool) { if (v >= 0.5) parts.Add(d.Label); }
                    else parts.Add($"{d.Label} {d.FormatValue(v)}");
                }
                if (parts.Count > 0) sb.Append($"[color=#969aa0]{group.Label}:[/color] {string.Join(" · ", parts)}\n");
            }
            _inspInfo.Text = sb.ToString();
        }

        // ------------------------------------------------------------------ generation

        public static ulong RandomSeed() => StableHash.Mix64((ulong)DateTime.UtcNow.Ticks * 0x9E3779B97F4A7C15UL) % 1_000_000UL;

        /// <summary>Starts generation with the current UI settings (models are built on worker threads).</summary>
        public void Generate() => GenerateFromUi(null);

        private void GenerateFromUi(List<OverviewEntry>? keep)
        {
            string text = _seed.Text.Trim();
            if (text.Length == 0) { Settings.Seed = RandomSeed(); _seed.Text = Settings.Seed.ToString(CultureInfo.InvariantCulture); }
            else Settings.Seed = ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong s) ? s : StableHash.SeedFromText(text);
            var fam = SelectedFamily();
            Settings.Family = Settings.Source == SourceKind.Random ? fam?.Id : null;
            Settings.Archetype = Settings.Source == SourceKind.Random && fam != null && _archetype.Selected > 0 ? fam.Archetypes[_archetype.Selected - 1].Id : null;
            Settings.Count = (int)_count.Value;
            Settings.Locks = _respectLocks.ButtonPressed ? _main.State.Locks : null;
            if (Settings.Source == SourceKind.Offspring && Settings.Parent == null) Settings.Parent = _main.State.Genome;
            Start(Settings, keep);
        }

        /// <summary>Starts a generation run directly (tests, captures). Favourites in <paramref name="keep"/> stay in place.</summary>
        public void Start(GenerationSettings s, List<OverviewEntry>? keep)
        {
            _jobCancel?.Cancel();
            _jobCancel = new CancellationTokenSource();
            var token = _jobCancel.Token;
            var snapshot = new GenerationSettings
            {
                Source = s.Source, Family = s.Family, Archetype = s.Archetype, Count = Math.Clamp(s.Count, 1, MaxCreatures), Seed = s.Seed,
                Parent = s.Parent, Mutation = s.Mutation.Clone(), Locks = s.Locks,
            };
            SyncUi(snapshot);
            var keepSet = keep ?? new List<OverviewEntry>();
            _jobWatch = Stopwatch.StartNew();
            _job = Task.Run(() => BuildEntries(snapshot, keepSet, token), token);
            HasGenerated = true;
            UpdateStatus();
        }

        /// <summary>Shows the settings of a run in the controls (runs can be started from code).</summary>
        private void SyncUi(GenerationSettings s)
        {
            if (_seed == null) return;
            _seed.Text = s.Seed.ToString(CultureInfo.InvariantCulture);
            _count.SetValueNoSignal(s.Count);
            _countLabel.Text = $"Count {s.Count}";
            Settings.Source = s.Source;
            Settings.Parent = s.Parent ?? Settings.Parent;
            if (s.Parent != null) SetParent(s.Parent, select: false);
            UpdateSourceUi();
            if (s.Source == SourceKind.Random)
            {
                var fams = CreatureRuntime.Registry.Families;
                int fi = s.Family == null ? 0 : fams.ToList().FindIndex(f => f.Id == s.Family) + 1;
                if (_family.Selected != fi) { _family.Selected = Math.Max(0, fi); FillArchetypes(); }
                if (s.Family != null && s.Archetype != null)
                {
                    var fam = fams[fi - 1];
                    int ai = fam.Archetypes.ToList().FindIndex(a => a.Id == s.Archetype) + 1;
                    _archetype.Selected = Math.Max(0, ai);
                }
            }
        }

        private void NewThrow()
        {
            _seed.Text = RandomSeed().ToString(CultureInfo.InvariantCulture);
            Generate();
        }

        public void RerollKeepFavorites()
        {
            var keep = _entries.Where(e => e.Favorite).ToList();
            _seed.Text = RandomSeed().ToString(CultureInfo.InvariantCulture);
            GenerateFromUi(keep);
        }

        /// <summary>Worker thread: genomes, models and the settled idle pose bounds of every creature.</summary>
        private static List<OverviewEntry> BuildEntries(GenerationSettings s, List<OverviewEntry> keep, CancellationToken token)
        {
            int fresh = Math.Max(0, s.Count - keep.Count);
            var genomes = MakeGenomes(s, fresh, token);
            var result = new OverviewEntry[genomes.Count];
            var renderer = new ThreadLocal<CreatureRenderer>(() => new CreatureRenderer());
            Parallel.For(0, genomes.Count, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, System.Environment.ProcessorCount - 1) }, i =>
            {
                result[i] = MakeEntry(genomes[i].g, genomes[i].seed, renderer.Value!);
            });
            renderer.Dispose();
            var list = new List<OverviewEntry>(keep.Count + result.Length);
            foreach (var k in keep)
            {
                list.Add(new OverviewEntry
                {
                    Genome = k.Genome, Model = k.Model, Favorite = true, Family = k.Family, FamilyLabel = k.FamilyLabel, Archetype = k.Archetype,
                    ArchetypeLabel = k.ArchetypeLabel, Seed = k.Seed, ContentCenter = k.ContentCenter, ContentSize = k.ContentSize,
                    Aquatic = k.Aquatic, Size = k.Size, Hue = k.Hue,
                });
            }
            list.AddRange(result);
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list;
        }

        public static List<(CreatureGenome g, ulong seed)> MakeGenomes(GenerationSettings s, int count, CancellationToken token)
        {
            var reg = CreatureRuntime.Registry;
            var list = new List<(CreatureGenome, ulong)>(count);
            if (s.Source == SourceKind.Offspring && s.Parent != null)
            {
                var parent = s.Parent;
                string baseName = string.IsNullOrEmpty(parent.Name) ? "Child" : parent.Name;
                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    ulong ms = StableHash.Derive(StableHash.Combine(parent.ContentHash, s.Seed), "overview", i);
                    var child = GenomeOps.Mutate(parent, s.Mutation, ms, s.Locks).WithName($"{baseName} {i + 1}");
                    list.Add((child, ms));
                }
                return list;
            }
            var families = s.Family == null ? reg.Families.ToList() : new List<ICreatureFamily> { reg.GetFamily(s.Family) };
            if (s.Archetype != null)
            {
                var f = families[0];
                for (ulong seed = s.Seed; list.Count < count && seed < s.Seed + 50000; seed++)
                {
                    token.ThrowIfCancellationRequested();
                    var g = GenomeFactory.Sample(f, seed);
                    if (g.Schema.Contains("form") && g.GetChoice("form") == s.Archetype) list.Add((g, seed));
                }
                return list;
            }
            // all families take turns; every creature gets its own seed (start seed + position)
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();
                var f = families[i % families.Count];
                ulong seed = s.Seed + (ulong)i;
                list.Add((GenomeFactory.Sample(f, seed), seed));
            }
            return list;
        }

        public static OverviewEntry MakeEntry(CreatureGenome g, ulong seed, CreatureRenderer renderer)
        {
            var reg = CreatureRuntime.Registry;
            var model = CreatureRuntime.Build(g);
            var fam = reg.GetFamily(g.FamilyId);
            string arch = g.Schema.Contains("form") ? g.GetChoice("form") : string.Empty;
            string archLabel = fam.Archetypes.FirstOrDefault(a => a.Id == arch)?.Label ?? arch;
            // settle the idle pose facing south east (snakes coil, birds fold their wings) and measure it
            var motor = new CreatureMotor(model, reg, new MotorOptions { ExportMode = true, ExportLoop = 2.0 });
            double yaw = PixelCamera.DirectionYaw(7);
            motor.Teleport(Vec3.Zero, yaw);
            motor.FacingOverride = yaw;
            for (int k = 0; k < 40; k++) motor.Step(FixedClock.DefaultStep);
            var c = model.Anatomy.Canvas;
            var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            renderer.Render(model.Anatomy, model.Palette, model.Surface, motor.Pose, ViewSpec.Default(motor.Yaw), frame);
            Vector2 centre = new Vector2(0, -c.Height * 0.25f);
            Vector2I size = new Vector2I(c.Width / 2, c.Height / 2);
            if (frame.ContentBounds(out int x0, out int y0, out int x1, out int y1))
            {
                centre = new Vector2((x0 + x1) * 0.5f - c.OriginX, (y0 + y1) * 0.5f - c.OriginY);
                size = new Vector2I(x1 - x0 + 1, y1 - y0 + 1);
            }
            return new OverviewEntry
            {
                Genome = g,
                Model = model,
                Family = fam.Id,
                FamilyLabel = fam.DisplayName,
                Archetype = arch,
                ArchetypeLabel = archLabel,
                Seed = seed,
                ContentCenter = centre,
                ContentSize = size,
                Aquatic = model.Anatomy.Rig.Locomotion == LocomotionKind.Swim,
                Size = size.X * (double)size.Y,
                Hue = g.GetOr("color.hue", 0),
            };
        }

        /// <summary>Cells fit most creatures (85th percentile of the measured idle sizes) plus room for the name.</summary>
        private static Vector2I CellFor(IReadOnlyList<OverviewEntry> entries)
        {
            if (entries.Count == 0) return new Vector2I(112, 88);
            var ws = entries.Select(e => e.ContentSize.X).OrderBy(x => x).ToList();
            var hs = entries.Select(e => e.ContentSize.Y).OrderBy(x => x).ToList();
            int p = Math.Min(ws.Count - 1, (int)Math.Round((ws.Count - 1) * 0.85));
            int w = Math.Clamp(ws[p] + 16, 56, 200);
            int h = Math.Clamp(hs[p] + 24, 52, 180);
            return new Vector2I(w + (w & 1), h + (h & 1));
        }

        private void ApplyGenerated(List<OverviewEntry> entries)
        {
            foreach (var e in _entries) if (e.Actor != null && IsInstanceValid(e.Actor)) e.Actor.QueueFree();
            _pendingActors.Clear();
            _staggered.Clear();
            _entries.Clear();
            _entries.AddRange(entries);
            _cellSize = CellFor(_entries);
            foreach (var e in _entries) _pendingActors.Enqueue(e);
            _selected = null;
            ApplyOrder();
            if (_entries.Count > 0) Select(0);
            UpdateStatus();
        }

        private void CreateActors(double budgetMs)
        {
            var sw = Stopwatch.StartNew();
            bool any = false;
            while (_pendingActors.Count > 0 && sw.Elapsed.TotalMilliseconds < budgetMs)
            {
                var e = _pendingActors.Dequeue();
                if (!_entries.Contains(e)) continue;
                var a = _main.CreateActor();
                a.AnimationFps = 12;
                a.CullOffscreen = true;
                a.CullMargin = 8;
                a.SyncNodePosition = false;
                a.DrawShadow = !e.Aquatic;
                Grid.Entities.AddChild(a);
                a.ApplyModel(e.Model);
                if (e.Aquatic) a.WaterLevel = 1000;
                a.Resting = _restAll;
                e.Actor = a;
                ApplyMovement(e);
                any = true;
            }
            if (any)
            {
                Grid.PlaceActors();
                UpdateStatus();
            }
        }

        // ------------------------------------------------------------------ order & selection

        public void SetSort(SortKind kind)
        {
            Sort = kind;
            if (_sort != null && _sort.Selected != (int)kind) _sort.Selected = (int)kind;
            ApplyOrder();
        }

        private void ApplyOrder()
        {
            IEnumerable<OverviewEntry> q = _entries;
            q = Sort switch
            {
                SortKind.Family => q.OrderBy(e => e.FamilyLabel, StringComparer.CurrentCulture).ThenBy(e => e.Index),
                SortKind.Archetype => q.OrderBy(e => e.FamilyLabel, StringComparer.CurrentCulture).ThenBy(e => e.ArchetypeLabel, StringComparer.CurrentCulture).ThenBy(e => e.Index),
                SortKind.Size => q.OrderByDescending(e => e.Size).ThenBy(e => e.Index),
                SortKind.Colour => q.OrderBy(e => e.Hue).ThenBy(e => e.Index),
                SortKind.FavoritesFirst => q.OrderBy(e => e.Favorite ? 0 : 1).ThenBy(e => e.Index),
                _ => q.OrderBy(e => e.Index),
            };
            _order = q.ToList();
            Grid.SetEntries(_order, _cellSize);
            Grid.Selected = _selected != null ? _order.IndexOf(_selected) : -1;
            if (Grid.Selected >= 0) Grid.EnsureVisible(Grid.Selected);
        }

        /// <summary>Selects a creature by display index and shows it in the inspector.</summary>
        public void Select(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= _order.Count) return;
            _selected = _order[displayIndex];
            Grid.Selected = displayIndex;
            Grid.EnsureVisible(displayIndex);
            ShowInspector(_selected);
            UpdateStatus();
        }

        private void MoveSelection(int delta)
        {
            if (_order.Count == 0) return;
            int i = Grid.Selected < 0 ? 0 : Math.Clamp(Grid.Selected + delta, 0, _order.Count - 1);
            Select(i);
        }

        private void OnHover(int i)
        {
            if (i < 0 || i >= _order.Count) { UpdateStatus(); return; }
            var e = _order[i];
            _main.Status($"{e.Genome.Name} · {e.ArchetypeLabel} · {e.FamilyLabel} · Seed {e.Seed}{(e.Favorite ? " · ★" : "")}", UiKit.TextDim);
        }

        public void OpenInEditor()
        {
            if (_selected == null) return;
            var g = _selected.Genome;
            _main.LoadGenome(g, $"Loaded from the overview: {g.Name}");
            _main.ShowOverview(false);
        }

        public void ToggleFavorite()
        {
            if (_selected == null) return;
            _selected.Favorite = !_selected.Favorite;
            _favButton.SetPressedNoSignal(_selected.Favorite);
            if (Sort == SortKind.FavoritesFirst) ApplyOrder();
            UpdateStatus();
        }

        private void SaveSelected()
        {
            if (_selected == null) return;
            string path = SavePresetUnique(_selected.Genome);
            _main.Status(path.StartsWith("Error", StringComparison.Ordinal) ? path : $"Saved: {path}", path.StartsWith("Error", StringComparison.Ordinal) ? UiKit.Bad : UiKit.Good);
        }

        private void SaveFavorites()
        {
            int n = 0;
            foreach (var e in _entries.Where(e => e.Favorite))
            {
                string p = SavePresetUnique(e.Genome);
                if (!p.StartsWith("Error", StringComparison.Ordinal)) n++;
            }
            _main.Status(n == 0 ? "No favorites marked (F or ★ in the inspector)." : $"{n} {(n == 1 ? "favorite" : "favorites")} saved to user://presets.", n == 0 ? UiKit.Info : UiKit.Good);
        }

        private static string SavePresetUnique(CreatureGenome g)
        {
            DirAccess.MakeDirRecursiveAbsolute("user://presets");
            string baseName = new string((string.IsNullOrEmpty(g.Name) ? "creature" : g.Name.ToLowerInvariant()).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            string path = $"user://presets/{baseName}.json";
            for (int k = 2; FileAccess.FileExists(path) && k < 1000; k++) path = $"user://presets/{baseName}_{k}.json";
            var err = CreatureRuntime.SavePreset(path, g);
            return err != null ? "Error: " + err : path;
        }

        private void RerollSelected()
        {
            if (_selected == null) return;
            var e = _selected;
            _rerollCounter++;
            CreatureGenome g;
            ulong seed;
            if (Settings.Source == SourceKind.Offspring && Settings.Parent != null)
            {
                seed = StableHash.Derive(StableHash.Combine(Settings.Parent.ContentHash, Settings.Seed), "overview.replace", (int)_rerollCounter);
                g = GenomeOps.Mutate(Settings.Parent, Settings.Mutation, seed, Settings.Locks).WithName(e.Genome.Name);
            }
            else
            {
                var fam = CreatureRuntime.Registry.GetFamily(e.Family);
                seed = Settings.Seed + 100_000UL + _rerollCounter;
                g = GenomeFactory.Sample(fam, seed);
            }
            var fresh = MakeEntry(g, seed, new CreatureRenderer());
            e.Genome = fresh.Genome; e.Model = fresh.Model; e.Archetype = fresh.Archetype; e.ArchetypeLabel = fresh.ArchetypeLabel;
            e.Seed = fresh.Seed; e.ContentCenter = fresh.ContentCenter; e.ContentSize = fresh.ContentSize; e.Aquatic = fresh.Aquatic;
            e.Size = fresh.Size; e.Hue = fresh.Hue; e.Favorite = false;
            if (e.Actor != null && IsInstanceValid(e.Actor))
            {
                e.Actor.ApplyModel(e.Model);
                e.Actor.WaterLevel = e.Aquatic ? 1000 : double.NaN;
                ApplyMovement(e);
            }
            Grid.PlaceActors();
            ShowInspector(e);
            _main.Status($"Replaced with {g.Name} ({e.ArchetypeLabel}).", UiKit.TextDim);
        }

        // ------------------------------------------------------------------ movement of all creatures

        public void SetMode(MoveMode mode)
        {
            Mode = mode;
            if (mode != MoveMode.Stand && _restAll) SetRestAll(false);
            SetModeButtons();
            foreach (var e in _entries) ApplyMovement(e);
        }

        private void SetModeButtons()
        {
            for (int i = 0; i < _modeButtons.Length; i++) _modeButtons[i].SetPressedNoSignal(i == (int)Mode);
        }

        public void SetDirection(int dir)
        {
            Direction = ((dir % 8) + 8) % 8;
            if (_dirLabel != null) _dirLabel.Text = $"Facing {PixelCamera.DirectionLabels[Direction]}";
            foreach (var e in _entries) ApplyMovement(e);
        }

        private void ApplyMovement(OverviewEntry e)
        {
            var a = e.Actor;
            if (a == null || !IsInstanceValid(a) || a.Model == null) return;
            a.FaceDirection(Direction);
            double speed = Mode switch { MoveMode.Walk => a.Model.Motion.WalkSpeed, MoveMode.Run => a.Model.Motion.RunSpeed, _ => 0 };
            a.MoveVelocity = AnimationPanel.DirVector(Direction) * (float)speed;
        }

        public void ActionAll()
        {
            var rng = new Rng((ulong)Time.GetTicksMsec());
            _staggered.Clear();
            foreach (var e in _entries)
                if (e.Actor != null && IsInstanceValid(e.Actor)) _staggered.Add((e.Actor, rng.Range(0.0, 0.45)));
        }

        public void SetRestAll(bool on)
        {
            _restAll = on;
            _restButton?.SetPressedNoSignal(on);
            if (on && Mode != MoveMode.Stand) { Mode = MoveMode.Stand; SetModeButtons(); foreach (var e in _entries) ApplyMovement(e); }
            foreach (var e in _entries) if (e.Actor != null && IsInstanceValid(e.Actor)) e.Actor.Resting = on;
        }

        // ------------------------------------------------------------------ frame & input

        public override void _Process(double delta)
        {
            if (_job != null && _job.IsCompleted)
            {
                var job = _job;
                _job = null;
                if (job.Status == TaskStatus.RanToCompletion)
                {
                    LastGenerationMs = _jobWatch.Elapsed.TotalMilliseconds;
                    ApplyGenerated(job.Result);
                }
                else if (job.IsFaulted) _main.Status("Generation failed: " + job.Exception?.GetBaseException().Message, UiKit.Bad);
            }
            if (_pendingActors.Count > 0) CreateActors(6.0);
            for (int i = _staggered.Count - 1; i >= 0; i--)
            {
                var (a, d) = _staggered[i];
                d -= delta;
                if (d <= 0)
                {
                    if (IsInstanceValid(a)) a.TriggerAction();
                    _staggered.RemoveAt(i);
                }
                else _staggered[i] = (a, d);
            }
            _statusTimer -= delta;
            if (_statusTimer <= 0) { _statusTimer = 0.5; UpdateStatus(); }
        }

        private double _statusTimer;

        private void UpdateStatus()
        {
            if (_status == null) return;
            if (_job != null) { _status.Text = "Generating creatures…"; return; }
            int n = _entries.Count;
            if (n == 0) { _status.Text = "No creatures generated yet."; return; }
            int fams = _entries.Select(e => e.Family).Distinct().Count();
            int archs = _entries.Select(e => e.Family + "/" + e.Archetype).Distinct().Count();
            int favs = _entries.Count(e => e.Favorite);
            int built = n - _pendingActors.Count;
            _status.Text = $"{n} {(n == 1 ? "creature" : "creatures")} · {fams} {(fams == 1 ? "family" : "families")} · {archs} {(archs == 1 ? "archetype" : "archetypes")}{(favs > 0 ? $" · {favs} ★" : "")}\n" +
                           $"{(built < n ? $"appearing… {built}/{n} · " : "")}visible {Grid.VisibleCount()} · generated in {LastGenerationMs:0} ms · zoom {Grid.EffectiveZoom}x";
        }

        public override void _UnhandledKeyInput(InputEvent e)
        {
            if (!IsVisibleInTree() || e is not InputEventKey k || !k.Pressed || k.Echo) return;
            if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
            switch (k.Keycode)
            {
                case Key.Left: MoveSelection(-1); break;
                case Key.Right: MoveSelection(1); break;
                case Key.Up: MoveSelection(-Grid.Columns); break;
                case Key.Down: MoveSelection(Grid.Columns); break;
                case Key.Pageup: MoveSelection(-Grid.Columns * Math.Max(1, (int)(Grid.VisibleWorldRect.Size.Y / Grid.CellSize.Y))); break;
                case Key.Pagedown: MoveSelection(Grid.Columns * Math.Max(1, (int)(Grid.VisibleWorldRect.Size.Y / Grid.CellSize.Y))); break;
                case Key.Home: Select(0); break;
                case Key.End: Select(_order.Count - 1); break;
                case Key.Enter: case Key.KpEnter: OpenInEditor(); break;
                case Key.F: ToggleFavorite(); break;
                case Key.Space when k.ShiftPressed: ActionAll(); break;
                case Key.Space: InspectorActor.TriggerAction(); _selected?.Actor?.TriggerAction(); break;
                case Key.G: NewThrow(); break;
                case Key.R: SetRestAll(!_restAll); break;
                case Key.Key1: SetMode(MoveMode.Stand); break;
                case Key.Key2: SetMode(MoveMode.Walk); break;
                case Key.Key3: SetMode(MoveMode.Run); break;
                case Key.Q: SetDirection(Direction + 1); break;
                case Key.E: SetDirection(Direction - 1); break;
                case Key.Equal: case Key.Plus: case Key.KpAdd: Grid.SetZoom(Math.Min(6, Grid.EffectiveZoom + 1)); break;
                case Key.Minus: case Key.KpSubtract: Grid.SetZoom(Math.Max(1, Grid.EffectiveZoom - 1)); break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
        }

        public override void _ExitTree()
        {
            _jobCancel?.Cancel();
        }
    }
}
