// Procedural Pixel Creature Workshop - schema driven genome editor with group and gene locks.
// Every family (including extensions) gets its editor automatically from its GeneSchema.

using System;
using System.Collections.Generic;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class GenomeEditor : VBoxContainer
    {
        private WorkshopState _state = null!;
        private readonly Dictionary<string, Action> _refreshers = new Dictionary<string, Action>();
        private readonly Dictionary<string, Button> _geneLocks = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _groupLocks = new Dictionary<string, Button>();
        private readonly Dictionary<string, VBoxContainer> _groupBodies = new Dictionary<string, VBoxContainer>();
        private readonly HashSet<string> _collapsed = new HashSet<string>();
        private HBoxContainer? _swatches;
        private string _schemaFamily = string.Empty;
        private bool _updating;
        private VBoxContainer _content = null!;

        public void Bind(WorkshopState state)
        {
            _state = state;
            AddThemeConstantOverride("separation", 4);
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            AddChild(scroll);
            _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _content.AddThemeConstantOverride("separation", 3);
            scroll.AddChild(_content);
            state.GenomeChanged += _ => Refresh();
            state.LocksChanged += RefreshLocks;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (var c in _content.GetChildren()) c.QueueFree();
            _refreshers.Clear();
            _geneLocks.Clear();
            _groupLocks.Clear();
            _groupBodies.Clear();
            var schema = _state.Genome.Schema;
            _schemaFamily = schema.FamilyId;
            foreach (var group in schema.Groups)
            {
                var header = new HBoxContainer();
                var fold = new Button { Icon = Icons.Get(_collapsed.Contains(group.Id) ? "fold_closed" : "fold_open", 1), Flat = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(22, 0) };
                var title = UiKit.Label(group.Label, 15, UiKit.Accent);
                title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                string gid = group.Id;
                var glock = new Button { Icon = Icons.Get("unlock", 1, UiKit.TextDim), ToggleMode = true, Flat = true, FocusMode = FocusModeEnum.None, TooltipText = $"Lock group “{group.Label}”: it is kept when rerolling and mutating." };
                glock.Toggled += on => { if (!_updating) _state.SetLock(null, gid, on); };
                _groupLocks[gid] = glock;
                header.AddChild(fold);
                header.AddChild(title);
                header.AddChild(UiKit.Label(StreamLabel(group.Stream), 11, UiKit.TextDim));
                header.AddChild(glock);
                _content.AddChild(header);
                var body = new VBoxContainer { Visible = !_collapsed.Contains(gid) };
                body.AddThemeConstantOverride("separation", 2);
                _groupBodies[gid] = body;
                fold.Pressed += () =>
                {
                    body.Visible = !body.Visible;
                    fold.Icon = Icons.Get(body.Visible ? "fold_open" : "fold_closed", 1);
                    if (body.Visible) _collapsed.Remove(gid); else _collapsed.Add(gid);
                };
                if (group.Stream == GeneStream.Color)
                {
                    _swatches = new HBoxContainer();
                    _swatches.AddThemeConstantOverride("separation", 1);
                    body.AddChild(_swatches);
                }
                foreach (var gene in schema.GenesInGroup(gid)) body.AddChild(GeneRow(gene));
                _content.AddChild(body);
                _content.AddChild(new HSeparator());
            }
            Refresh();
        }

        private static string StreamLabel(GeneStream s) => s switch
        {
            GeneStream.Anatomy => "Anatomy",
            GeneStream.Color => "Color",
            GeneStream.Pattern => "Pattern",
            _ => "Motion",
        };

        private Control GeneRow(GeneDef gene)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            var label = UiKit.Label(gene.Label, 13);
            label.CustomMinimumSize = new Vector2(132, 0);
            label.TooltipText = string.IsNullOrEmpty(gene.Description) ? gene.Id : gene.Description + $"\n({gene.Id})";
            label.MouseFilter = MouseFilterEnum.Pass;
            label.ClipText = true;
            row.AddChild(label);
            string id = gene.Id;
            switch (gene.Kind)
            {
                case GeneKind.Choice:
                {
                    var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, TooltipText = label.TooltipText };
                    foreach (var o in gene.Options) opt.AddItem(o.Label);
                    opt.ItemSelected += idx => { if (!_updating) _state.SetGene(id, idx, false); };
                    row.AddChild(opt);
                    _refreshers[id] = () => opt.Selected = (int)_state.Genome.Get(id);
                    break;
                }
                case GeneKind.Bool:
                {
                    var cb = new CheckBox { SizeFlagsHorizontal = SizeFlags.ExpandFill, FocusMode = FocusModeEnum.None, TooltipText = label.TooltipText };
                    cb.Toggled += on => { if (!_updating) _state.SetGene(id, on ? 1 : 0, false); };
                    row.AddChild(cb);
                    _refreshers[id] = () => cb.ButtonPressed = _state.Genome.Get(id) >= 0.5;
                    break;
                }
                default:
                {
                    var slider = new HSlider
                    {
                        MinValue = gene.Min,
                        MaxValue = gene.Max,
                        Step = gene.Kind == GeneKind.Int ? 1 : gene.Step,
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                        SizeFlagsVertical = SizeFlags.ShrinkCenter,
                        FocusMode = FocusModeEnum.None,
                        TooltipText = label.TooltipText,
                        CustomMinimumSize = new Vector2(90, 16),
                    };
                    var value = UiKit.Label("", 12, UiKit.TextDim);
                    value.CustomMinimumSize = new Vector2(46, 0);
                    value.HorizontalAlignment = HorizontalAlignment.Right;
                    bool dragging = false;
                    slider.DragStarted += () => dragging = true;
                    slider.DragEnded += changed => { dragging = false; _state.EndDrag(); };
                    slider.ValueChanged += v =>
                    {
                        value.Text = gene.FormatValue(v);
                        if (!_updating) _state.SetGene(id, v, dragging);
                    };
                    row.AddChild(slider);
                    row.AddChild(value);
                    _refreshers[id] = () =>
                    {
                        slider.SetValueNoSignal(_state.Genome.Get(id));
                        value.Text = gene.FormatValue(_state.Genome.Get(id));
                    };
                    break;
                }
            }
            var lk = new Button { Icon = Icons.Get("unlock", 1, UiKit.Line), ToggleMode = true, Flat = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(20, 0), TooltipText = "Lock this trait" };
            lk.Toggled += on => { if (!_updating) _state.SetLock(id, null, on); };
            _geneLocks[id] = lk;
            row.AddChild(lk);
            return row;
        }

        public void Refresh()
        {
            if (_state.Genome.Schema.FamilyId != _schemaFamily)
            {
                Rebuild();
                return;
            }
            _updating = true;
            foreach (var r in _refreshers.Values) r();
            _updating = false;
            RefreshLocks();
        }

        private void RefreshLocks()
        {
            _updating = true;
            foreach (var kv in _groupLocks)
            {
                bool on = _state.Locks.IsGroupLocked(kv.Key);
                kv.Value.ButtonPressed = on;
                kv.Value.Icon = on ? Icons.Get("lock", 1, UiKit.Accent, UiKit.AccentDim) : Icons.Get("unlock", 1, UiKit.TextDim);
            }
            foreach (var kv in _geneLocks)
            {
                var def = _state.Genome.Schema.Get(kv.Key);
                bool on = _state.Locks.IsGeneLocked(kv.Key);
                kv.Value.ButtonPressed = on;
                kv.Value.Icon = on ? Icons.Get("lock", 1, UiKit.Accent, UiKit.AccentDim)
                    : (_state.Locks.IsLocked(def) ? Icons.Get("lock", 1, UiKit.AccentDim, UiKit.Line) : Icons.Get("unlock", 1, UiKit.Line));
            }
            _updating = false;
        }

        /// <summary>Shows the resolved palette colours above the colour genes.</summary>
        public void ShowPalette(CreaturePalette? palette)
        {
            if (_swatches == null) return;
            foreach (var c in _swatches.GetChildren()) c.QueueFree();
            if (palette == null) return;
            for (int i = CreaturePalette.FirstColorIndex; i < palette.Colors.Count; i++)
            {
                var c = palette.Colors[i];
                _swatches.AddChild(new ColorRect { Color = GodotConvert.ToColor(c), CustomMinimumSize = new Vector2(10, 14), TooltipText = palette.ColorNames[i] + " " + c.ToHex() });
            }
        }
    }
}
