// Procedural Pixel Creatures - batch tools inside Godot (godot --headless --path . -- --export-samples).
//
// --export-samples exports every shipped example genome (res://addons/*/Samples) as spritesheet PNG +
// JSON metadata + genome preset through exactly the same exporter the workshop uses. After writing,
// each sheet is read back from disk and checked against its metadata (frame rectangles inside the
// page, non-empty frames), so the result is playable purely from the metadata.
//
// Options (after "--"): --out=<dir> (default user://exports/samples)  --dirs=4|8  --fps=<6..30>
//                       --filter=<text> (only sample paths containing the text)
// Exit code 0 when every sample was exported and verified, 1 otherwise.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using PixelCreatures.Core.Export;
using PixelCreatures.Runtime;

namespace PixelCreatures.Tests
{
    public partial class Tools : Node
    {
        public override void _Ready() => CallDeferred(nameof(Run));

        private void Run()
        {
            var args = OS.GetCmdlineUserArgs();
            int exit = 1;
            try
            {
                if (args.Contains("--export-samples")) exit = ExportSamples(args);
                else GD.PrintErr("Unknown tool. Available: --export-samples");
            }
            catch (Exception e)
            {
                GD.PrintErr("Tool aborted: " + e);
            }
            GetTree().Quit(exit);
        }

        private static string Arg(string[] args, string name, string fallback)
            => args.FirstOrDefault(a => a.StartsWith(name + "=", StringComparison.Ordinal))?.Substring(name.Length + 1) ?? fallback;

        private static int ExportSamples(string[] args)
        {
            string outDir = Arg(args, "--out", "user://exports/samples");
            string filter = Arg(args, "--filter", string.Empty);
            int fps = int.TryParse(Arg(args, "--fps", "12"), out int f) ? f : 12;
            var dirs = Arg(args, "--dirs", "4") == "8" ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 } : new List<int> { 0, 2, 4, 6 };
            string absOut = outDir.StartsWith("user://", StringComparison.Ordinal) || outDir.StartsWith("res://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(outDir) : outDir;
            DirAccess.MakeDirRecursiveAbsolute(absOut);
            GD.Print($"== Exporting example genomes to {absOut} ({dirs.Count} directions, {fps} FPS) ==");

            int ok = 0, failed = 0, frames = 0;
            var total = Stopwatch.StartNew();
            foreach (var path in CreatureRuntime.SamplePaths())
            {
                if (filter.Length > 0 && !path.Contains(filter, StringComparison.Ordinal)) continue;
                var sw = Stopwatch.StartNew();
                var res = CreatureRuntime.LoadPreset(path);
                if (!res.Ok)
                {
                    GD.PrintErr($"ERROR {path}: {res.Describe()}");
                    failed++;
                    continue;
                }
                var genome = res.Genome!;
                var model = CreatureRuntime.Build(genome);
                var opt = new SpritesheetOptions { Fps = fps, Directions = dirs };
                var result = SpritesheetExporter.Export(model, CreatureRuntime.Registry, opt, "spritesheet");
                string name = path.GetFile().GetBaseName();
                string dir = $"{absOut}/{genome.FamilyId}/{name}";
                DirAccess.MakeDirRecursiveAbsolute(dir);
                for (int i = 0; i < result.Pages.Count; i++)
                {
                    var (rgba, w, h) = result.Pages[i];
                    string file = result.Pages.Count == 1 ? "spritesheet.png" : $"spritesheet_{i}.png";
                    Write($"{dir}/{file}", PngEncoder.Encode(rgba, w, h, ("Software", "Procedural Pixel Creatures")));
                }
                using (var jf = FileAccess.Open($"{dir}/spritesheet.json", FileAccess.ModeFlags.Write)) jf?.StoreString(result.MetadataJson);
                CreatureRuntime.SavePreset($"{dir}/genome.json", genome);
                string problem = Verify(dir);
                if (problem.Length > 0)
                {
                    GD.PrintErr($"ERROR {name}: {problem}");
                    failed++;
                    continue;
                }
                ok++;
                frames += result.TotalFrames;
                GD.Print($"  {genome.FamilyId}/{name} \"{genome.Name}\": {result.Clips.Count} clips, {result.TotalFrames} frames {result.FrameWidth}x{result.FrameHeight}, {sw.Elapsed.TotalSeconds:0.00} s");
            }
            GD.Print($"{ok} exported and verified, {failed} errors, {frames} frames, {total.Elapsed.TotalSeconds:0.0} s");
            return failed == 0 && ok > 0 ? 0 : 1;
        }

        private static void Write(string path, byte[] data)
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file?.StoreBuffer(data);
        }

        /// <summary>Reads the written sheet back and checks every frame rectangle of the metadata.</summary>
        private static string Verify(string dir)
        {
            if (!FileAccess.FileExists($"{dir}/spritesheet.json")) return "metadata missing";
            var meta = JsonNode.Parse(FileAccess.GetFileAsString($"{dir}/spritesheet.json"))?.AsObject();
            if (meta == null) return "metadata not readable";
            var pages = new List<Image>();
            foreach (var p in meta["pages"]?.AsArray() ?? new JsonArray())
            {
                var img = Image.LoadFromFile($"{dir}/{p?["file"]?.GetValue<string>()}");
                if (img == null || img.IsEmpty()) return "page not readable";
                pages.Add(img);
            }
            if (pages.Count == 0) return "no pages";
            int fw = meta["frameWidth"]?.GetValue<int>() ?? 0, fh = meta["frameHeight"]?.GetValue<int>() ?? 0;
            int clips = 0;
            foreach (var clip in meta["clips"]?.AsArray() ?? new JsonArray())
            {
                clips++;
                int visible = 0;
                foreach (var fr in clip?["frames"]?.AsArray() ?? new JsonArray())
                {
                    int page = fr?["page"]?.GetValue<int>() ?? 0, x = fr?["x"]?.GetValue<int>() ?? -1, y = fr?["y"]?.GetValue<int>() ?? -1;
                    if (page < 0 || page >= pages.Count) return "invalid page index";
                    var img = pages[page];
                    if (x < 0 || y < 0 || x + fw > img.GetWidth() || y + fh > img.GetHeight()) return $"frame rectangle outside the page ({clip?["name"]})";
                    if (!img.GetRegion(new Rect2I(x, y, fw, fh)).IsInvisible()) visible++;
                }
                if (visible == 0) return $"clip {clip?["name"]} is empty";
            }
            return clips == 0 ? "no clips" : string.Empty;
        }
    }
}
