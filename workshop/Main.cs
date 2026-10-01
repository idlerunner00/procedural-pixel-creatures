// Procedural Pixel Creature Workshop - main scene: routes command line modes and builds the workshop UI.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class Main : Control
    {
        public WorkshopState State { get; private set; } = null!;
        public CreatureActor PreviewActor { get; private set; } = null!;
        public PixelStage PreviewStage { get; private set; } = null!;
        public AnimationPanel AnimPanel { get; private set; } = null!;
        public GenomeEditor Editor { get; private set; } = null!;
        /// <summary>Second workshop view: up to 100 creatures at once.</summary>
        public OverviewView Overview { get; private set; } = null!;
        public bool OverviewActive => Overview != null && Overview.Visible;
        public void SelectTab(int index) => _tabs.CurrentTab = Math.Clamp(index, 0, _tabs.GetTabCount() - 1);
        public void ExportPreviewSheet() => _export.ExportSheet();

        private OptionButton _family = null!;
        private LineEdit _seed = null!;
        private Label _title = null!;
        private Label _statusText = null!;
        private Label _perf = null!;
        private Button _undo = null!, _redo = null!, _reset = null!;
        private Button _viewEditor = null!, _viewOverview = null!;
        private Control _editorTools = null!;
        private Control _editorView = null!;
        private TabContainer _tabs = null!;
        private OffspringPanel _offspring = null!;
        private BreedingPanel _breeding = null!;
        private GalleryPanel _gallery = null!;
        private ExportPanel _export = null!;
        private InfoPanel _info = null!;
        private FileDialog? _dialog;
        private double _statusTimer;
        private readonly List<ICreatureFamily> _families = new List<ICreatureFamily>();
        private ulong _seedCounter;

        public static readonly string[] ToolArgs = { "--self-test", "--benchmark", "--soak", "--export-samples" };

        /// <summary>Set by the self test when it instantiates the workshop: command line routing is ignored.</summary>
        public static bool EmbeddedForTests;

        public override void _Ready()
        {
            var args = EmbeddedForTests ? Array.Empty<string>() : OS.GetCmdlineUserArgs();
            if (args.Contains("--self-test")) { CallDeferred(nameof(Goto), "res://tests/SelfTest.tscn"); return; }
            if (args.Contains("--benchmark") || args.Contains("--soak")) { CallDeferred(nameof(Goto), "res://tests/Benchmark.tscn"); return; }
            if (args.Contains("--export-samples")) { CallDeferred(nameof(Goto), "res://tests/Tools.tscn"); return; }
            if (args.Contains("--testarea")) { CallDeferred(nameof(Goto), "res://workshop/TestArea/TestArea.tscn"); return; }
            BuildUi();
            if (args.Contains("--overview")) CallDeferred(nameof(ShowOverview), true); // after FinishSetup
        }

        private void Goto(string scene) => GetTree().ChangeSceneToFile(scene);

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            Theme = UiKit.CreateTheme();
            SetAnchorsPreset(LayoutPreset.FullRect);
            var bg = new ColorRect { Color = UiKit.Bg };
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(bg);
            _families.AddRange(CreatureRuntime.Registry.Families);

            var initial = LoadStartGenome();
            State = new WorkshopState(CreatureRuntime.Registry, initial);

            var root = new VBoxContainer();
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            root.OffsetLeft = 8; root.OffsetTop = 6; root.OffsetRight = -8; root.OffsetBottom = -6;
            root.AddThemeConstantOverride("separation", 6);
            AddChild(root);
            root.AddChild(BuildToolbar());

            var main = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            main.SplitOffset = 330;
            root.AddChild(main);
            _editorView = main;

            // second view: overview of many creatures (hidden until selected in the toolbar)
            Overview = new OverviewView { Name = "Overview", SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false, ProcessMode = ProcessModeEnum.Disabled };
            root.AddChild(Overview);

            // left: genome editor
            Editor = new GenomeEditor { CustomMinimumSize = new Vector2(320, 0) };
            var leftCard = UiKit.Card(Editor);
            main.AddChild(leftCard);

            var rightSplit = new HSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            main.AddChild(rightSplit);
            var centerV = new VSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SplitOffset = 380 };
            rightSplit.AddChild(centerV);

            // center: preview stage
            PreviewStage = new PixelStage { FixedFraming = true, CustomMinimumSize = new Vector2(420, 260), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, TargetLogical = new Vector2I(150, 92) };
            var stageCard = UiKit.Card(PreviewStage);
            centerV.AddChild(stageCard);
            PreviewActor = CreateActor();
            PreviewActor.Name = "PreviewActor";
            PreviewStage.Ready += () => { };
            stageCard.Ready += () => { };

            // bottom tabs
            _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 230) };
            centerV.AddChild(_tabs);

            // right: animation panel
            AnimPanel = new AnimationPanel { CustomMinimumSize = new Vector2(236, 0) };
            var rightScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(250, 0) };
            rightScroll.AddChild(AnimPanel);
            rightSplit.AddChild(UiKit.Card(rightScroll));

            // status bar
            var status = new HBoxContainer();
            _statusText = UiKit.Label("Ready.", 13, UiKit.TextDim);
            _statusText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _statusText.ClipText = true;
            _perf = UiKit.Label("", 12, UiKit.TextDim);
            status.AddChild(_statusText);
            status.AddChild(_perf);
            root.AddChild(status);

            // everything that needs the tree
            CallDeferred(nameof(FinishSetup));
        }

        private void FinishSetup()
        {
            PreviewStage.Entities.AddChild(PreviewActor);
            PreviewStage.Follow = PreviewActor;
            PreviewStage.FollowOffset = new Vector2(0, -10);
            Editor.Bind(State);
            AnimPanel.Bind(PreviewActor, PreviewStage);

            _offspring = new OffspringPanel { Name = "Offspring" };
            _breeding = new BreedingPanel { Name = "Crossover" };
            _gallery = new GalleryPanel { Name = "Gallery" };
            _export = new ExportPanel { Name = "Export" };
            _info = new InfoPanel { Name = "Info" };
            _tabs.AddChild(_offspring);
            _tabs.AddChild(_breeding);
            _tabs.AddChild(_gallery);
            _tabs.AddChild(_export);
            _tabs.AddChild(_info);
            _offspring.Bind(this);
            _breeding.Bind(this);
            _gallery.Bind(this);
            _export.Bind(this);
            _info.Bind(this);

            State.GenomeChanged += OnGenomeChanged;
            PreviewActor.ModelChanged += () =>
            {
                Editor.ShowPalette(PreviewActor.Model?.Palette);
                AnimPanel.AutoBackground();
                AnimPanel.ApplyMovement();
                _info.Refresh();
                if (PreviewActor.Model != null && PreviewActor.Model.Warnings.Count > 0) Status(string.Join(" | ", PreviewActor.Model.Warnings), UiKit.Info);
            };
            PreviewActor.BuildFailed += msg => Status("Error: " + msg, UiKit.Bad);
            PreviewActor.SetGenome(State.Genome, async: false);
            OnGenomeChanged("Start");
            Overview.Bind(this);
            Overview.SetActive(false);
        }

        /// <summary>Switches between the editor and the overview of many creatures.</summary>
        public void ShowOverview(bool on)
        {
            if (Overview == null) return;
            // the hidden view neither processes nor renders
            Overview.SetActive(on);
            _editorView.Visible = !on;
            _editorView.ProcessMode = on ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
            PreviewStage.Viewport.RenderTargetUpdateMode = on ? SubViewport.UpdateMode.Disabled : SubViewport.UpdateMode.Always;
            _editorTools.Visible = !on;
            _viewEditor.SetPressedNoSignal(!on);
            _viewOverview.SetPressedNoSignal(on);
            if (on && !Overview.HasGenerated) Overview.Generate();
            Status(on ? "Overview: click to select, double-click to open in the editor." : "Editor.", UiKit.TextDim);
        }

        public CreatureActor CreateActor()
        {
            var scene = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn");
            var actor = scene.Instantiate<CreatureActor>();
            actor.FamilyId = string.Empty;
            actor.AnimationFps = 15;
            // Simulate travel for the gait, but keep the preview and its background in place.
            actor.SyncNodePosition = false;
            return actor;
        }

        private Control BuildToolbar()
        {
            var bar = new HBoxContainer();
            bar.AddThemeConstantOverride("separation", 6);
            _title = UiKit.Label("Procedural Pixel Creature Workshop", 18, UiKit.Accent);
            bar.AddChild(_title);
            bar.AddChild(UiKit.Spacer(8));
            _viewEditor = UiKit.Toggle("Editor", true, _ => ShowOverview(false), "Edit one creature (F2 switches the view)");
            _viewOverview = UiKit.Toggle("Overview", false, _ => ShowOverview(true), "Generate and inspect up to 100 creatures at once (F2 switches the view)");
            bar.AddChild(_viewEditor);
            bar.AddChild(_viewOverview);
            bar.AddChild(UiKit.Spacer(8));
            var tools = new HBoxContainer();
            tools.AddThemeConstantOverride("separation", 6);
            _editorTools = tools;
            bar.AddChild(tools);
            _family = new OptionButton { FocusMode = FocusModeEnum.None, TooltipText = "Body-plan family", CustomMinimumSize = new Vector2(190, 0) };
            foreach (var f in _families) _family.AddItem(f.DisplayName);
            tools.AddChild(_family);
            tools.AddChild(UiKit.Label("Seed", 13, UiKit.TextDim));
            _seed = new LineEdit { CustomMinimumSize = new Vector2(150, 0), PlaceholderText = "Number or text", TooltipText = "Seed: a number or any text. Same seed + family = same creature." };
            _seed.TextSubmitted += _ => NewFromSeedField();
            tools.AddChild(_seed);
            var create = new Button { Text = "New creature", Icon = Icons.Get("new", 1), FocusMode = FocusModeEnum.None, TooltipText = "New creature from family and seed (locked traits are kept)" };
            create.Pressed += NewFromSeedField;
            tools.AddChild(create);
            var dice = new Button { Text = "Roll", Icon = Icons.Get("dice", 1), FocusMode = FocusModeEnum.None, TooltipText = "New random seed: regenerate all unlocked traits (F5)" };
            dice.Pressed += Reroll;
            tools.AddChild(dice);
            tools.AddChild(new VSeparator());
            _undo = new Button { Icon = Icons.Get("undo", 1), FocusMode = FocusModeEnum.None, TooltipText = "Undo (Ctrl+Z)" };
            _undo.Pressed += () => State.Undo();
            _redo = new Button { Icon = Icons.Get("redo", 1), FocusMode = FocusModeEnum.None, TooltipText = "Redo (Ctrl+Y)" };
            _redo.Pressed += () => State.Redo();
            tools.AddChild(_undo);
            tools.AddChild(_redo);
            tools.AddChild(new VSeparator());
            var load = new Button { Text = "Load", Icon = Icons.Get("load", 1), FocusMode = FocusModeEnum.None, TooltipText = "Load preset (JSON) (Ctrl+O)" };
            load.Pressed += () => OpenDialog(false);
            var save = new Button { Text = "Save", Icon = Icons.Get("save", 1), FocusMode = FocusModeEnum.None, TooltipText = "Save preset (JSON) (Ctrl+S)" };
            save.Pressed += () => OpenDialog(true);
            _reset = new Button { Text = "Reset", Icon = Icons.Get("reset", 1), FocusMode = FocusModeEnum.None, TooltipText = "Reset to the last saved/loaded state" };
            _reset.Pressed += () => State.ResetToSaved();
            tools.AddChild(load);
            tools.AddChild(save);
            tools.AddChild(_reset);
            bar.AddChild(UiKit.Spacer(0, 0, true));
            var test = new Button { Text = "Test area", FocusMode = FocusModeEnum.None, TooltipText = "Walkable test area with a controllable creature and other creatures" };
            test.Pressed += OpenTestArea;
            bar.AddChild(test);
            return bar;
        }

        // ------------------------------------------------------------------ genome flow

        private CreatureGenome LoadStartGenome()
        {
            // Optional preset at startup: --sample=res://addons/.../x.json
            var sampleArg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--sample=", StringComparison.Ordinal));
            if (sampleArg != null)
            {
                var s = CreatureRuntime.LoadPreset(sampleArg.Substring("--sample=".Length));
                if (s.Ok) return s.Genome!;
            }
            // a curated sample if present, otherwise a fixed seed
            var res = CreatureRuntime.LoadPreset("res://addons/procedural_creatures/Samples/quadruped/quadruped_01.json");
            if (res.Ok) return res.Genome!;
            return CreatureRuntime.Sample(_families.Count > 0 ? _families[0].Id : "quadruped", 1);
        }

        private void OnGenomeChanged(string reason)
        {
            var g = State.Genome;
            int fi = _families.FindIndex(f => f.Id == g.FamilyId);
            if (fi >= 0 && _family.Selected != fi) _family.Selected = fi;
            PreviewActor.SetGenome(g, async: true);
            _undo.Disabled = !State.History.CanUndo;
            _redo.Disabled = !State.History.CanRedo;
            _undo.TooltipText = State.History.CanUndo ? $"Undo: {State.History.UndoLabel} (Ctrl+Z)" : "Undo (Ctrl+Z)";
            _redo.TooltipText = State.History.CanRedo ? $"Redo: {State.History.RedoLabel} (Ctrl+Y)" : "Redo (Ctrl+Y)";
            _reset.Disabled = !State.IsDirty;
            string name = string.IsNullOrEmpty(g.Name) ? "(unnamed)" : g.Name;
            string fam = CreatureRuntime.Registry.GetFamily(g.FamilyId).DisplayName;
            DisplayServer.WindowSetTitle($"{name} – {fam}{(State.IsDirty ? " *" : "")} – Procedural Pixel Creature Workshop");
            if (reason != "Start") Status(reason, UiKit.TextDim);
            _offspring?.ParentChanged();
            _breeding?.ParentChanged();
            _info?.Refresh();
        }

        private string SelectedFamily() => _families[Math.Max(0, _family.Selected)].Id;

        private void NewFromSeedField()
        {
            string text = _seed.Text.Trim();
            ulong seed = text.Length == 0 ? NextSeed() : StableHash.SeedFromText(text);
            if (text.Length == 0) _seed.Text = seed.ToString(CultureInfo.InvariantCulture);
            State.NewCreature(SelectedFamily(), seed);
        }

        private ulong NextSeed()
        {
            // seeds for the "Roll" button: time based entropy is fine here (the seed is shown and reproducible)
            _seedCounter++;
            return StableHash.Mix64((ulong)DateTime.UtcNow.Ticks ^ (_seedCounter * 0x9E3779B97F4A7C15UL)) % 1_000_000_000UL;
        }

        private void Reroll()
        {
            ulong seed = NextSeed();
            _seed.Text = seed.ToString(CultureInfo.InvariantCulture);
            if (SelectedFamily() != State.Genome.FamilyId) State.NewCreature(SelectedFamily(), seed);
            else State.RegenerateUnlocked(seed);
        }

        public void Status(string text, Color color)
        {
            _statusText.Text = text;
            _statusText.AddThemeColorOverride("font_color", color);
            _statusTimer = 8;
        }

        // ------------------------------------------------------------------ files

        private void OpenDialog(bool save)
        {
            _dialog?.QueueFree();
            _dialog = new FileDialog
            {
                Access = FileDialog.AccessEnum.Userdata,
                FileMode = save ? FileDialog.FileModeEnum.SaveFile : FileDialog.FileModeEnum.OpenFile,
                Filters = new[] { "*.json ; Creature presets" },
                CurrentDir = "user://presets",
                Title = save ? "Save preset" : "Load preset",
                Size = new Vector2I(760, 520),
                UseNativeDialog = false,
            };
            DirAccess.MakeDirRecursiveAbsolute("user://presets");
            if (save) _dialog.CurrentFile = (string.IsNullOrEmpty(State.Genome.Name) ? "creature" : State.Genome.Name.ToLowerInvariant()) + ".json";
            _dialog.FileSelected += path =>
            {
                if (save) SavePreset(path);
                else LoadPreset(path);
            };
            AddChild(_dialog);
            _dialog.PopupCentered();
        }

        public void SavePreset(string path)
        {
            var err = CreatureRuntime.SavePreset(path, State.Genome);
            if (err != null) { Status(err, UiKit.Bad); return; }
            State.MarkSaved(path);
            Status($"Saved: {path}", UiKit.Good);
        }

        public void LoadPreset(string path)
        {
            var res = CreatureRuntime.LoadPreset(path);
            if (!res.Ok)
            {
                Status($"Loading failed: {res.Describe()}", UiKit.Bad);
                return;
            }
            State.Load(res.Genome!, path, $"Loaded: {path.GetFile()}");
            if (res.Warnings.Count > 0 || res.Migrations.Count > 0) Status(res.Describe(), UiKit.Info);
        }

        public void LoadGenome(CreatureGenome g, string reason) => State.Load(g, null, reason);

        private void OpenTestArea()
        {
            TestAreaLauncher.PendingGenome = State.Genome;
            GetTree().ChangeSceneToFile("res://workshop/TestArea/TestArea.tscn");
        }

        // ------------------------------------------------------------------ input & frame

        public override void _UnhandledKeyInput(InputEvent e)
        {
            if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
            if (k.Keycode == Key.F2) { ShowOverview(!OverviewActive); GetViewport().SetInputAsHandled(); return; }
            if (OverviewActive) return; // the overview handles its own keys
            if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
            bool ctrl = k.CtrlPressed || k.MetaPressed;
            switch (k.Keycode)
            {
                case Key.Z when ctrl && k.ShiftPressed: State.Redo(); break;
                case Key.Z when ctrl: State.Undo(); break;
                case Key.Y when ctrl: State.Redo(); break;
                case Key.S when ctrl: OpenDialog(true); break;
                case Key.O when ctrl: OpenDialog(false); break;
                case Key.F5: Reroll(); break;
                case Key.Space: PreviewActor.TriggerAction(); break;
                case Key.H: PreviewActor.TriggerHit(AnimationPanel.DirVector(AnimPanel.Direction), 1f); break;
                case Key.R: AnimPanel.ToggleRest(); break;
                case Key.P: AnimPanel.TogglePause(); break;
                case Key.N: PreviewActor.StepFrames(1); break;
                case Key.Key1: AnimPanel.SetMode(MoveMode.Stand); break;
                case Key.Key2: AnimPanel.SetMode(MoveMode.Walk); break;
                case Key.Key3: AnimPanel.SetMode(MoveMode.Run); break;
                case Key.Q: AnimPanel.SetDirection(AnimPanel.Direction + 1); break;
                case Key.E: AnimPanel.SetDirection(AnimPanel.Direction - 1); break;
                default: return;
            }
            GetViewport().SetInputAsHandled();
        }

        public override void _Process(double delta)
        {
            if (_perf == null) return;
            if (_statusTimer > 0)
            {
                _statusTimer -= delta;
                if (_statusTimer <= 0) _statusText.AddThemeColorOverride("font_color", UiKit.TextDim);
            }
            _perf.Text = $"{Engine.GetFramesPerSecond():0} FPS | Raster {CreatureRenderQueue.LastRasterMs:0.00} ms ({CreatureRenderQueue.LastBatchSize}) | Upload {CreatureRenderQueue.LastUploadMs:0.00} ms";
        }

    }

    /// <summary>Passes the current genome to the test area scene.</summary>
    public static class TestAreaLauncher
    {
        public static CreatureGenome? PendingGenome;
    }
}
