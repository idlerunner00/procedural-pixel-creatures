// Procedural Pixel Creature Workshop - genome, anatomy and runtime information of the current creature.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    public partial class InfoPanel : HBoxContainer
    {
        private Main _main = null!;
        private RichTextLabel _text = null!;
        private LineEdit _name = null!;

        public void Bind(Main main)
        {
            _main = main;
            AddThemeConstantOverride("separation", 10);
            var left = UiKit.Column(4);
            left.CustomMinimumSize = new Vector2(240, 0);
            left.AddChild(UiKit.Label("Name", 13, UiKit.TextDim));
            _name = new LineEdit { PlaceholderText = "Creature name" };
            _name.TextSubmitted += n => _main.State.Set(_main.State.Genome.WithName(n.Trim()), $"Renamed to {n.Trim()}");
            left.AddChild(_name);
            left.AddChild(UiKit.Button("Apply name", () => _main.State.Set(_main.State.Genome.WithName(_name.Text.Trim()), $"Renamed to {_name.Text.Trim()}")));
            left.AddChild(UiKit.Button("New random name", () =>
            {
                var g = _main.State.Genome;
                _main.State.Set(g.WithName(NameGenerator.Generate(new Rng(g.ContentHash ^ (ulong)DateTime.UtcNow.Ticks))), "New name");
            }));
            AddChild(left);
            _text = new RichTextLabel { BbcodeEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ScrollActive = true, SelectionEnabled = true };
            _text.AddThemeFontSizeOverride("normal_font_size", 13);
            AddChild(_text);
        }

        public void Refresh()
        {
            if (_text == null) return;
            var g = _main.State.Genome;
            if (!_name.HasFocus()) _name.Text = g.Name;
            var sb = new StringBuilder();
            var fam = CreatureRuntime.Registry.GetFamily(g.FamilyId);
            sb.Append($"[color=#e2ac4c]{fam.DisplayName}[/color]  ·  Content ID {g.ContentId}  ·  Generator v{g.GeneratorVersion}\n");
            sb.Append($"Seeds: anatomy {g.AnatomySeed}, color {g.ColorSeed}, pattern {g.PatternSeed}, motion {g.MotionSeed}\n");
            var lin = g.Lineage;
            sb.Append($"Origin: {Origin(lin.Origin)}, generation {lin.Generation}");
            if (lin.ParentIds.Count > 0) sb.Append($", parents {string.Join(" + ", lin.ParentIds.Select(p => p.Substring(0, Math.Min(8, p.Length))))}");
            if (lin.Provenance.Count > 0) sb.Append($"\nInheritance: {string.Join(", ", lin.Provenance.Select(kv => $"{kv.Key}={kv.Value}"))}");
            sb.Append('\n');
            var m = _main.PreviewActor.Model;
            if (m != null && m.Genome.ContentHash == g.ContentHash)
            {
                var a = m.Anatomy;
                sb.Append($"Traits: {string.Join(", ", a.Traits)}\n");
                sb.Append($"Bones {a.Skeleton.Count}, primitives {a.Primitives.Count}, groups {a.Groups.Count}, facial features {a.Features.Count}, canvas {a.Canvas}\n");
                sb.Append(string.Format(CultureInfo.InvariantCulture, "Body length {0:0} px, leg length {1:0} px, walk {2:0} px/s, run {3:0} px/s, time scale {4:0.00}\n",
                    a.Metrics.BodyLength, a.Metrics.LegLength, m.Motion.WalkSpeed, m.Motion.RunSpeed, m.Motion.TimeScale));
                int visible = CountVisibleColors();
                sb.Append($"Palette: {m.Palette.Colors.Count - CreaturePalette.FirstColorIndex} colors defined, {visible} visible in the current frame\n");
                if (a.Repairs.Count > 0) sb.Append($"[color=#78b0e2]Repairs:[/color] {string.Join(" | ", a.Repairs)}\n");
                if (m.Warnings.Count > 0) sb.Append($"[color=#e26860]Warnings:[/color] {string.Join(" | ", m.Warnings)}\n");
                var st = _main.PreviewActor.LastStats;
                sb.Append($"Last frame: {st.PrimitivesDrawn} primitives, {st.PixelsTested} pixel tests, {st.PixelsCovered} pixels covered{(st.Overflow ? ", [color=#e26860]overflow[/color]" : "")}; canvas growths: {_main.PreviewActor.CanvasGrowths}\n");
            }
            var f = CreatureRuntime.Factory;
            var (h, miss, n) = f.AnatomyCacheStats;
            sb.Append($"Anatomy cache: {n} entries, {h} hits, {miss} misses · discarded stale builds: {_main.PreviewActor.DiscardedBuilds}\n");
            sb.Append($"Undo: {_main.State.History.Index + 1}/{_main.State.History.Count} · Saved: {(_main.State.SavedPath ?? "–")}{(_main.State.IsDirty ? " (modified)" : "")}");
            _text.Text = sb.ToString();
        }

        private int CountVisibleColors()
        {
            var f = _main.PreviewActor.Frame;
            if (f == null) return 0;
            var seen = new bool[256];
            int count = 0;
            foreach (var b in f.Indices)
            {
                if (b < CreaturePalette.FirstColorIndex || seen[b]) continue;
                seen[b] = true;
                count++;
            }
            return count;
        }

        private static string Origin(string o) => o switch
        {
            "sampled" => "generated from seed",
            "mutation" => "mutation",
            "crossover" => "crossover",
            "edited" => "edited",
            "regenerated" => "partially regenerated",
            _ => o,
        };
    }
}
