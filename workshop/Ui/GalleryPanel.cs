// Procedural Pixel Creature Workshop - gallery of example genomes, the fixed uncurated seed sample and
// the user's own presets. Thumbnails are rendered lazily with the framework renderer.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public static class SampleLibrary
    {
        public sealed class Entry
        {
            public CreatureGenome Genome = null!;
            public string Path = string.Empty;
            public bool BuiltIn;
        }

        private static List<Entry>? _builtIn;

        /// <summary>All sample presets shipped in addons/*/Samples/&lt;family&gt;/*.json (cached).</summary>
        public static IReadOnlyList<Entry> Load()
        {
            if (_builtIn != null) return _builtIn;
            _builtIn = new List<Entry>();
            var addons = DirAccess.GetDirectoriesAt("res://addons");
            foreach (var addon in addons.OrderBy(x => x, StringComparer.Ordinal))
            {
                string root = $"res://addons/{addon}/Samples";
                if (!DirAccess.DirExistsAbsolute(root)) continue;
                foreach (var fam in DirAccess.GetDirectoriesAt(root).OrderBy(x => x, StringComparer.Ordinal))
                    foreach (var file in DirAccess.GetFilesAt($"{root}/{fam}").Where(f => f.EndsWith(".json")).OrderBy(x => x, StringComparer.Ordinal))
                    {
                        string path = $"{root}/{fam}/{file}";
                        var res = CreatureRuntime.LoadPreset(path);
                        if (res.Ok) _builtIn.Add(new Entry { Genome = res.Genome!, Path = path, BuiltIn = true });
                        else GD.PushWarning($"Sample {path} could not be loaded: {res.Describe()}");
                    }
            }
            return _builtIn;
        }

        public static List<Entry> LoadUser()
        {
            var list = new List<Entry>();
            const string dir = "user://presets";
            if (!DirAccess.DirExistsAbsolute(dir)) return list;
            foreach (var file in DirAccess.GetFilesAt(dir).Where(f => f.EndsWith(".json")).OrderBy(x => x, StringComparer.Ordinal))
            {
                var res = CreatureRuntime.LoadPreset($"{dir}/{file}");
                if (res.Ok) list.Add(new Entry { Genome = res.Genome!, Path = $"{dir}/{file}" });
            }
            return list;
        }

        /// <summary>Fixed, uncurated seed sample used for quality reviews (never edited by hand).</summary>
        public static ulong[] UncuratedSeeds => GenomeFactory.UncuratedSeeds;
    }

    public partial class GalleryPanel : VBoxContainer
    {
        private Main _main = null!;
        private OptionButton _family = null!;
        private OptionButton _mode = null!;
        private HFlowContainer _grid = null!;
        private Label _hint = null!;
        private readonly Queue<(CreatureGenome g, TextureButton b)> _pending = new Queue<(CreatureGenome, TextureButton)>();
        private readonly Dictionary<ulong, Texture2D> _thumbs = new Dictionary<ulong, Texture2D>();
        private readonly CreatureRenderer _renderer = new CreatureRenderer();
        private bool _dirty = true;

        public void Bind(Main main)
        {
            _main = main;
            AddThemeConstantOverride("separation", 4);
            var controls = new HBoxContainer();
            controls.AddThemeConstantOverride("separation", 6);
            _mode = new OptionButton { FocusMode = FocusModeEnum.None };
            _mode.AddItem("Sample genomes (curated)");
            _mode.AddItem("Fixed random sample (uncurated)");
            _mode.AddItem("User presets (user://presets)");
            _mode.ItemSelected += _ => Rebuild();
            _family = new OptionButton { FocusMode = FocusModeEnum.None };
            _family.AddItem("All families");
            foreach (var f in CreatureRuntime.Registry.Families) _family.AddItem(f.DisplayName);
            _family.ItemSelected += _ => Rebuild();
            controls.AddChild(_mode);
            controls.AddChild(_family);
            controls.AddChild(UiKit.Button("Refresh", Rebuild, "Reload the list"));
            _hint = UiKit.Label("Click a creature to load it into the editor.", 12, UiKit.TextDim);
            controls.AddChild(_hint);
            AddChild(controls);
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _grid = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _grid.AddThemeConstantOverride("h_separation", 6);
            _grid.AddThemeConstantOverride("v_separation", 6);
            scroll.AddChild(_grid);
            AddChild(scroll);
            VisibilityChanged += () => { if (IsVisibleInTree() && _dirty) Rebuild(); };
        }

        private void Rebuild()
        {
            _dirty = false;
            foreach (var c in _grid.GetChildren()) c.QueueFree();
            _pending.Clear();
            var families = CreatureRuntime.Registry.Families;
            string? famFilter = _family.Selected <= 0 ? null : families[_family.Selected - 1].Id;
            var entries = new List<(CreatureGenome g, string label)>();
            switch (_mode.Selected)
            {
                case 1:
                    foreach (var f in families)
                    {
                        if (famFilter != null && f.Id != famFilter) continue;
                        foreach (var s in SampleLibrary.UncuratedSeeds)
                        {
                            var g = GenomeFactory.Sample(f, s);
                            entries.Add((g, $"{g.Name} · Seed {s}"));
                        }
                    }
                    break;
                case 2:
                    foreach (var e in SampleLibrary.LoadUser())
                        if (famFilter == null || e.Genome.FamilyId == famFilter) entries.Add((e.Genome, e.Genome.Name));
                    break;
                default:
                    foreach (var e in SampleLibrary.Load())
                        if (famFilter == null || e.Genome.FamilyId == famFilter) entries.Add((e.Genome, e.Genome.Name));
                    break;
            }
            _hint.Text = $"{entries.Count} {(entries.Count == 1 ? "creature" : "creatures")}. Click a creature to load it into the editor.";
            foreach (var (g, label) in entries)
            {
                var box = new VBoxContainer { CustomMinimumSize = new Vector2(128, 0) };
                box.AddThemeConstantOverride("separation", 0);
                var btn = new TextureButton { CustomMinimumSize = new Vector2(128, 96), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, TooltipText = $"{label}\n{CreatureRuntime.Registry.GetFamily(g.FamilyId).DisplayName}" };
                var genome = g;
                btn.Pressed += () => _main.LoadGenome(genome, $"Loaded from gallery: {genome.Name}");
                var bg = new PanelContainer();
                bg.AddChild(btn);
                box.AddChild(bg);
                var l = UiKit.Label(label, 11, UiKit.TextDim);
                l.ClipText = true;
                l.CustomMinimumSize = new Vector2(128, 0);
                box.AddChild(l);
                _grid.AddChild(box);
                if (_thumbs.TryGetValue(g.ContentHash, out var tex)) btn.TextureNormal = tex;
                else _pending.Enqueue((g, btn));
            }
        }

        public override void _Process(double delta)
        {
            // render a few thumbnails per frame so the UI never stalls
            for (int k = 0; k < 3 && _pending.Count > 0; k++)
            {
                var (g, btn) = _pending.Dequeue();
                if (!IsInstanceValid(btn)) continue;
                btn.TextureNormal = Thumbnail(g);
            }
        }

        public Texture2D Thumbnail(CreatureGenome g)
        {
            if (_thumbs.TryGetValue(g.ContentHash, out var t)) return t;
            var model = CreatureRuntime.Build(g);
            var pose = new CreaturePose(model.Anatomy.Skeleton);
            var c = model.Anatomy.Canvas;
            var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            _renderer.Render(model.Anatomy, model.Palette, model.Surface, pose, ViewSpec.Default(PixelCamera.DirectionYaw(7)), frame);
            frame.ContentBounds(out int x0, out int y0, out int x1, out int y1);
            x0 = Math.Max(0, x0 - 2); y0 = Math.Max(0, y0 - 2); x1 = Math.Min(c.Width, x1 + 2); y1 = Math.Min(c.Height, y1 + 2);
            var lut = new byte[1024];
            model.Palette.BuildLut(lut, 0);
            var rgba = new byte[c.Width * c.Height * 4];
            frame.ToRgba(lut, rgba, model.Palette.WaterTint);
            int w = Math.Max(1, x1 - x0), h = Math.Max(1, y1 - y0);
            var crop = new byte[w * h * 4];
            for (int y = 0; y < h; y++) Array.Copy(rgba, ((y0 + y) * c.Width + x0) * 4, crop, y * w * 4, w * 4);
            var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, crop);
            var tex = ImageTexture.CreateFromImage(img);
            _thumbs[g.ContentHash] = tex;
            if (_thumbs.Count > 400) _thumbs.Clear();
            return tex;
        }
    }
}
