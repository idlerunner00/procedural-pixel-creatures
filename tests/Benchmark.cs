// Procedural Pixel Creatures - performance benchmark and soak test.
//
//   godot --path . -- --benchmark [--populations=1,25,100] [--frames=600] [--warmup=120] [--fps=15]
//                                 [--immediate] [--report=<file.json>]
//   godot --path . -- --soak [--minutes=3] [--population=25] [--report=<file.json>]
//
// The benchmark spawns populations of animated creatures (all shipped samples, walking, running and
// acting) and measures per frame: total frame time, actor simulation time, raster time (parallel
// batch) and texture upload time, with percentiles, plus managed/engine memory, allocations per
// frame and GC counts. It records hardware, renderer and settings with the numbers. Headless or
// software rendered runs (e.g. in the cloud) measure the CPU side only and say so in the report.
// The soak test churns genomes, actions, hits and rest cycles for minutes and fails on unbounded
// growth of nodes, objects or memory.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;

namespace PixelCreatures.Tests
{
    public partial class Benchmark : Node2D
    {
        private readonly List<CreatureGenome> _genomes = new List<CreatureGenome>();
        private readonly List<CreatureActor> _actors = new List<CreatureActor>();
        private readonly List<Vector2> _velocities = new List<Vector2>();
        private Rect2 _area = new Rect2(-400, -300, 800, 600);
        private int _animFps = 15;
        private ulong _rng = 0x9E3779B97F4A7C15UL;

        public override void _Ready() => CallDeferred(nameof(Start));

        private double Rand()
        {
            _rng = StableHash.Mix64(_rng + 0x632BE59BD9B4E019UL);
            return (_rng >> 11) * (1.0 / 9007199254740992.0);
        }

        private async void Start()
        {
            var args = OS.GetCmdlineUserArgs();
            string Arg(string key, string fallback) => args.FirstOrDefault(a => a.StartsWith("--" + key + "=", StringComparison.Ordinal))?.Substring(key.Length + 3) ?? fallback;
            _animFps = int.Parse(Arg("fps", "15"), CultureInfo.InvariantCulture);
            if (args.Contains("--immediate")) CreatureRenderQueue.Mode = RenderQueueMode.Immediate;
            Engine.MaxFps = 0;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            foreach (var p in CreatureRuntime.SamplePaths())
            {
                var r = CreatureRuntime.LoadPreset(p);
                if (r.Ok) _genomes.Add(r.Genome!);
            }
            if (_genomes.Count == 0)
                foreach (var f in CreatureRuntime.Registry.Families) _genomes.Add(CreatureRuntime.Sample(f.Id, 1));
            // generation costs are measured separately (cold and warm), then the shared model cache is
            // warmed so that building is not part of the frame measurements
            var generation = args.Contains("--soak") ? null : MeasureGeneration();
            foreach (var g in _genomes) CreatureRuntime.Build(g);

            string reportPath = Arg("report", string.Empty);
            int exit = 0;
            JsonObject report;
            if (args.Contains("--soak"))
            {
                report = await Soak(double.Parse(Arg("minutes", "3"), CultureInfo.InvariantCulture), int.Parse(Arg("population", "25"), CultureInfo.InvariantCulture));
                exit = (bool)report["passed"]! ? 0 : 1;
            }
            else
            {
                var pops = Arg("populations", "1,25,100").Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                report = await RunBenchmark(pops, int.Parse(Arg("frames", "600"), CultureInfo.InvariantCulture), int.Parse(Arg("warmup", "120"), CultureInfo.InvariantCulture));
                if (generation != null) report["generation"] = generation;
            }
            string json = report.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
            string md = ToMarkdown(report);
            string baseName = args.Contains("--soak") ? "soak" : "benchmark";
            Write($"user://reports/{baseName}.json", json);
            Write($"user://reports/{baseName}.md", md);
            if (!string.IsNullOrEmpty(reportPath))
            {
                Write(reportPath, json);
                Write(System.IO.Path.ChangeExtension(reportPath, ".md"), md);
            }
            GD.Print(md);
            GD.Print($"Report: {ProjectSettings.GlobalizePath($"user://reports/{baseName}.json")}");
            GetTree().Quit(exit);
        }

        /// <summary>
        /// Generation costs on the main thread: the very first build (includes JIT), cold builds with an
        /// empty model cache (fresh factory, JIT warm), warm builds (cache hits) and sampling genomes from
        /// seeds. Uses all shipped example genomes.
        /// </summary>
        private JsonObject MeasureGeneration()
        {
            var registry = CreatureRuntime.Registry;
            var sw = new Stopwatch();
            sw.Start();
            new CreatureFactory(registry).Build(_genomes[0]);
            double firstMs = sw.Elapsed.TotalMilliseconds;
            // JIT warm-up for every family on a throwaway factory
            var warmup = new CreatureFactory(registry);
            foreach (var f in registry.Families) warmup.Build(GenomeFactory.Sample(f, 424242));
            var factory = new CreatureFactory(registry);
            var cold = new List<double>();
            var warm = new List<double>();
            foreach (var g in _genomes)
            {
                sw.Restart();
                factory.Build(g);
                cold.Add(sw.Elapsed.TotalMilliseconds);
            }
            foreach (var g in _genomes)
            {
                sw.Restart();
                factory.Build(g);
                warm.Add(sw.Elapsed.TotalMilliseconds);
            }
            var sample = new List<double>();
            foreach (var f in registry.Families)
            {
                for (ulong seed = 1; seed <= 20; seed++)
                {
                    sw.Restart();
                    GenomeFactory.Sample(f, 90000 + seed);
                    sample.Add(sw.Elapsed.TotalMilliseconds);
                }
            }
            JsonObject Stats(List<double> v)
            {
                var sorted = v.OrderBy(x => x).ToList();
                double P(double q) => sorted[Math.Min(sorted.Count - 1, (int)Math.Floor(q * (sorted.Count - 1) + 0.5))];
                return new JsonObject
                {
                    ["count"] = v.Count,
                    ["mean"] = Math.Round(v.Average(), 3),
                    ["p50"] = Math.Round(P(0.5), 3),
                    ["p95"] = Math.Round(P(0.95), 3),
                    ["max"] = Math.Round(sorted[^1], 3),
                };
            }
            return new JsonObject
            {
                ["firstBuildIncludingJitMs"] = Math.Round(firstMs, 2),
                ["coldBuildMs"] = Stats(cold),
                ["warmBuildMs"] = Stats(warm),
                ["sampleGenomeMs"] = Stats(sample),
            };
        }

        private static void Write(string path, string text)
        {
            string dir = path.GetBaseDir();
            if (!string.IsNullOrEmpty(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
            f?.StoreString(text);
        }

        private JsonObject EnvironmentInfo()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            bool headless = DisplayServer.GetName() == "headless";
            string adapter = RenderingServer.GetVideoAdapterName();
            bool software = headless || adapter.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) || adapter.Contains("SwiftShader", StringComparison.OrdinalIgnoreCase);
            return new JsonObject
            {
                ["godot"] = (string)Engine.GetVersionInfo()["string"],
                ["os"] = $"{OS.GetName()} {OS.GetVersion()}",
                ["cpu"] = OS.GetProcessorName(),
                ["cpuThreads"] = OS.GetProcessorCount(),
                ["dotnet"] = System.Environment.Version.ToString(),
                ["renderer"] = headless ? "headless (no GPU rendering)" : $"{adapter} ({RenderingServer.GetVideoAdapterVendor()})",
                ["renderingMethod"] = (string)ProjectSettings.GetSetting("rendering/renderer/rendering_method"),
                ["display"] = DisplayServer.GetName(),
                ["viewport"] = $"{vp.X}x{vp.Y}",
                ["animationFps"] = _animFps,
                ["renderQueue"] = CreatureRenderQueue.Mode.ToString(),
                ["rasterThreads"] = CreatureRenderQueue.MaxParallelism,
                ["vsync"] = "off (MaxFps 0)",
                ["note"] = software
                    ? "Software/headless run: measures the CPU side (simulation, rasterization, upload). The numbers say nothing about desktop GPU frame rates."
                    : "Hardware rendering.",
            };
        }

        private void Spawn(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var actor = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn").Instantiate<CreatureActor>();
                actor.FamilyId = string.Empty;
                actor.AnimationFps = _animFps;
                AddChild(actor);
                var g = _genomes[(_actors.Count) % _genomes.Count];
                actor.SetGenome(g, async: false);
                actor.Teleport(new Vector2((float)(_area.Position.X + Rand() * _area.Size.X), (float)(_area.Position.Y + Rand() * _area.Size.Y)));
                double angle = Rand() * Math.PI * 2;
                var model = actor.Model!;
                double speed = Rand() < 0.35 ? model.Motion.RunSpeed : model.Motion.WalkSpeed;
                var v = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (float)speed;
                actor.MoveVelocity = v;
                _actors.Add(actor);
                _velocities.Add(v);
            }
        }

        private void Despawn()
        {
            foreach (var a in _actors) a.QueueFree();
            _actors.Clear();
            _velocities.Clear();
        }

        private void Steer(int frame)
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                var p = a.GroundPosition;
                var v = _velocities[i];
                if (p.X < _area.Position.X && v.X < 0 || p.X > _area.End.X && v.X > 0) v.X = -v.X;
                if (p.Y < _area.Position.Y && v.Y < 0 || p.Y > _area.End.Y && v.Y > 0) v.Y = -v.Y;
                _velocities[i] = v;
                // every creature stops, acts or gets hit now and then
                int phase = (frame + i * 37) % 360;
                a.MoveVelocity = phase < 280 ? v : Vector2.Zero;
                if (phase == 300) a.TriggerAction();
                if (phase == 340) a.TriggerHit(-v.Normalized(), 1f);
            }
        }

        private SignalAwaiter NextFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double idx = (sorted.Count - 1) * p;
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (idx - lo);
        }

        private static JsonObject Stats(List<double> values)
        {
            var s = values.OrderBy(x => x).ToList();
            return new JsonObject
            {
                ["mean"] = Math.Round(values.Count > 0 ? values.Average() : 0, 3),
                ["p50"] = Math.Round(Percentile(s, 0.5), 3),
                ["p90"] = Math.Round(Percentile(s, 0.9), 3),
                ["p95"] = Math.Round(Percentile(s, 0.95), 3),
                ["p99"] = Math.Round(Percentile(s, 0.99), 3),
                ["max"] = Math.Round(s.Count > 0 ? s[^1] : 0, 3),
            };
        }

        private async Task<JsonObject> RunBenchmark(int[] populations, int frames, int warmup)
        {
            var results = new JsonArray();
            foreach (int n in populations)
            {
                Despawn();
                await NextFrame();
                await NextFrame();
                GC.Collect();
                Spawn(n);
                for (int i = 0; i < warmup; i++) { Steer(i); await NextFrame(); }
                var frameMs = new List<double>();
                var simMs = new List<double>();
                var rasterMs = new List<double>();
                var uploadMs = new List<double>();
                var batch = new List<double>();
                long alloc0 = GC.GetTotalAllocatedBytes(false);
                int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                long renders0 = CreatureRenderQueue.TotalRenders;
                double lastRaster = -1, lastUpload = -1;
                ulong prev = Time.GetTicksUsec();
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < frames; i++)
                {
                    Steer(warmup + i);
                    long p0 = CreatureActor.ProcessTicks;
                    await NextFrame();
                    ulong now = Time.GetTicksUsec();
                    frameMs.Add((now - prev) / 1000.0);
                    prev = now;
                    simMs.Add((CreatureActor.ProcessTicks - p0) * 1000.0 / Stopwatch.Frequency);
                    // raster/upload statistics describe the last flushed batch (one per frame at most)
                    if (CreatureRenderQueue.LastRasterMs != lastRaster || CreatureRenderQueue.LastUploadMs != lastUpload)
                    {
                        rasterMs.Add(CreatureRenderQueue.LastRasterMs);
                        uploadMs.Add(CreatureRenderQueue.LastUploadMs);
                        batch.Add(CreatureRenderQueue.LastBatchSize);
                        lastRaster = CreatureRenderQueue.LastRasterMs;
                        lastUpload = CreatureRenderQueue.LastUploadMs;
                    }
                }
                sw.Stop();
                long alloc = GC.GetTotalAllocatedBytes(false) - alloc0;
                var res = new JsonObject
                {
                    ["population"] = n,
                    ["frames"] = frames,
                    ["seconds"] = Math.Round(sw.Elapsed.TotalSeconds, 3),
                    ["fps"] = Math.Round(frames / sw.Elapsed.TotalSeconds, 1),
                    ["frameMs"] = Stats(frameMs),
                    ["simulationMs"] = Stats(simMs),
                    ["rasterBatchMs"] = Stats(rasterMs),
                    ["uploadBatchMs"] = Stats(uploadMs),
                    ["meanBatchSize"] = Math.Round(batch.Count > 0 ? batch.Average() : 0, 2),
                    ["rendersPerSecond"] = Math.Round((CreatureRenderQueue.TotalRenders - renders0) / sw.Elapsed.TotalSeconds, 1),
                    ["allocatedKBPerFrame"] = Math.Round(alloc / 1024.0 / frames, 2),
                    ["gcGen0"] = GC.CollectionCount(0) - gc0,
                    ["gcGen1"] = GC.CollectionCount(1) - gc1,
                    ["gcGen2"] = GC.CollectionCount(2) - gc2,
                    ["managedMB"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 2),
                    ["engineStaticMB"] = Math.Round(OS.GetStaticMemoryUsage() / 1048576.0, 2),
                    ["textureMB"] = Math.Round(Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / 1048576.0, 2),
                    ["nodes"] = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),
                };
                results.Add(res);
                GD.Print($"Population {n}: {res["fps"]} FPS, Frame p50 {res["frameMs"]!["p50"]} ms p99 {res["frameMs"]!["p99"]} ms, Sim p50 {res["simulationMs"]!["p50"]} ms, Raster p50 {res["rasterBatchMs"]!["p50"]} ms");
            }
            Despawn();
            return new JsonObject
            {
                ["kind"] = "benchmark",
                ["environment"] = EnvironmentInfo(),
                ["samples"] = _genomes.Count,
                ["results"] = results,
            };
        }

        private async Task<JsonObject> Soak(double minutes, int population)
        {
            Spawn(population);
            var samples = new JsonArray();
            var sw = Stopwatch.StartNew();
            int frame = 0;
            int churn = 0;
            double nextSample = 0;
            double managedFirst = -1, objectsFirst = -1, nodesFirst = -1;
            double managedLast = 0, objectsLast = 0, nodesLast = 0;
            var frameMs = new List<double>();
            ulong prev = Time.GetTicksUsec();
            while (sw.Elapsed.TotalMinutes < minutes)
            {
                Steer(frame);
                // churn: replace genomes (async), rest cycles, respawn a few actors
                if (frame % 120 == 0 && _actors.Count > 0)
                {
                    churn++;
                    var a = _actors[churn % _actors.Count];
                    a.SetGenome(_genomes[(churn * 7) % _genomes.Count], async: true);
                    var b = _actors[(churn * 3) % _actors.Count];
                    b.Resting = !b.Resting;
                }
                if (frame % 600 == 300 && _actors.Count > 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        _actors[0].QueueFree();
                        _actors.RemoveAt(0);
                        _velocities.RemoveAt(0);
                    }
                    Spawn(3);
                }
                await NextFrame();
                ulong now = Time.GetTicksUsec();
                frameMs.Add((now - prev) / 1000.0);
                prev = now;
                frame++;
                if (sw.Elapsed.TotalSeconds >= nextSample)
                {
                    nextSample += 10;
                    double managed = GC.GetTotalMemory(false) / 1048576.0;
                    double objects = Performance.GetMonitor(Performance.Monitor.ObjectCount);
                    double nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                    var sorted = frameMs.OrderBy(x => x).ToList();
                    samples.Add(new JsonObject
                    {
                        ["t"] = Math.Round(sw.Elapsed.TotalSeconds, 1),
                        ["managedMB"] = Math.Round(managed, 2),
                        ["engineStaticMB"] = Math.Round(OS.GetStaticMemoryUsage() / 1048576.0, 2),
                        ["objects"] = objects,
                        ["nodes"] = nodes,
                        ["frameP95Ms"] = Math.Round(Percentile(sorted, 0.95), 3),
                    });
                    frameMs.Clear();
                    // the first sample after 30 s of warm-up is the reference
                    if (managedFirst < 0 && sw.Elapsed.TotalSeconds >= 30)
                    {
                        GC.Collect();
                        managedFirst = GC.GetTotalMemory(true) / 1048576.0;
                        objectsFirst = objects;
                        nodesFirst = nodes;
                    }
                }
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await NextFrame();
            managedLast = GC.GetTotalMemory(true) / 1048576.0;
            objectsLast = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            nodesLast = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            if (managedFirst < 0) { managedFirst = managedLast; objectsFirst = objectsLast; nodesFirst = nodesLast; }
            bool passed = managedLast - managedFirst < 48 && objectsLast - objectsFirst < 200 && nodesLast - nodesFirst <= 0;
            Despawn();
            return new JsonObject
            {
                ["kind"] = "soak",
                ["environment"] = EnvironmentInfo(),
                ["minutes"] = minutes,
                ["population"] = population,
                ["frames"] = frame,
                ["genomeSwaps"] = churn,
                ["managedMBStart"] = Math.Round(managedFirst, 2),
                ["managedMBEnd"] = Math.Round(managedLast, 2),
                ["objectsStart"] = objectsFirst,
                ["objectsEnd"] = objectsLast,
                ["nodesStart"] = nodesFirst,
                ["nodesEnd"] = nodesLast,
                ["passed"] = passed,
                ["samples"] = samples,
            };
        }

        private static string ToMarkdown(JsonObject r)
        {
            var sb = new StringBuilder();
            var env = r["environment"]!.AsObject();
            sb.AppendLine(r["kind"]!.ToString() == "soak" ? "# Soak test" : "# Benchmark");
            sb.AppendLine();
            foreach (var kv in env) sb.AppendLine($"- **{kv.Key}**: {kv.Value}");
            sb.AppendLine();
            if (r["kind"]!.ToString() == "soak")
            {
                sb.AppendLine($"Duration {r["minutes"]} min, population {r["population"]}, {r["frames"]} frames, {r["genomeSwaps"]} genome swaps.");
                sb.AppendLine($"Managed memory {r["managedMBStart"]} -> {r["managedMBEnd"]} MB, objects {r["objectsStart"]} -> {r["objectsEnd"]}, nodes {r["nodesStart"]} -> {r["nodesEnd"]}: **{((bool)r["passed"]! ? "passed" : "FAILED")}**");
                sb.AppendLine();
                sb.AppendLine("| t (s) | managed MB | engine MB | objects | nodes | frame p95 ms |");
                sb.AppendLine("|---:|---:|---:|---:|---:|---:|");
                foreach (var s in r["samples"]!.AsArray())
                    sb.AppendLine($"| {s!["t"]} | {s["managedMB"]} | {s["engineStaticMB"]} | {s["objects"]} | {s["nodes"]} | {s["frameP95Ms"]} |");
                return sb.ToString();
            }
            if (r["generation"] is JsonObject gen)
            {
                string S(string key) { var o = gen[key]!; return $"{o["p50"]} / {o["p95"]} / {o["max"]} (mean {o["mean"]}, n = {o["count"]})"; }
                sb.AppendLine("Generation on the main thread (ms, p50 / p95 / max):");
                sb.AppendLine();
                sb.AppendLine($"- first build incl. JIT: {gen["firstBuildIncludingJitMs"]}");
                sb.AppendLine($"- cold (empty model cache): {S("coldBuildMs")}");
                sb.AppendLine($"- warm (cache hits): {S("warmBuildMs")}");
                sb.AppendLine($"- roll a genome from a seed: {S("sampleGenomeMs")}");
                sb.AppendLine();
            }
            sb.AppendLine("| Population | FPS | Frame p50 / p95 / p99 / max (ms) | Simulation p50 / p99 (ms) | Raster batch p50 / p99 (ms) | Upload p50 (ms) | Batch | Alloc KB/frame | GC0/1/2 | Managed MB | Texture MB |");
            sb.AppendLine("|---:|---:|---|---|---|---:|---:|---:|---|---:|---:|");
            foreach (var x in r["results"]!.AsArray())
            {
                var f = x!["frameMs"]!; var s = x["simulationMs"]!; var ra = x["rasterBatchMs"]!; var u = x["uploadBatchMs"]!;
                sb.AppendLine($"| {x["population"]} | {x["fps"]} | {f["p50"]} / {f["p95"]} / {f["p99"]} / {f["max"]} | {s["p50"]} / {s["p99"]} | {ra["p50"]} / {ra["p99"]} | {u["p50"]} | {x["meanBatchSize"]} | {x["allocatedKBPerFrame"]} | {x["gcGen0"]}/{x["gcGen1"]}/{x["gcGen2"]} | {x["managedMB"]} | {x["textureMB"]} |");
            }
            return sb.ToString();
        }
    }
}
