// Procedural Pixel Creatures - Godot self test (godot --headless --path . -- --self-test).
//
// Runs the engine independent core suite inside Godot (.NET of the engine, the engine's thread pool)
// and adds runtime checks that need the engine: sample presets from res://, asynchronous building
// with cancellation of stale results, frame rendering and texture upload, preset I/O through
// user://, invalid preset handling, spritesheet export with metadata and a node/memory leak check.
// Writes a structured JSON report and exits with code 0 (all passed) or 1 (failures).
//
// Options (after "--"): --report=<path>  --quick (10 seeds per family)  --filter=<text>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Export;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;
using PixelCreatures.Workshop;

namespace PixelCreatures.Tests
{
    public partial class SelfTest : Node
    {
        private readonly TestRunner _runner = new TestRunner();

        public override void _Ready()
        {
            _runner.Log = s => GD.Print(s);
            CallDeferred(nameof(Start));
        }

        private async void Start()
        {
            var args = OS.GetCmdlineUserArgs();
            bool quick = args.Contains("--quick");
            string filter = args.FirstOrDefault(a => a.StartsWith("--filter=", StringComparison.Ordinal))?.Substring(9) ?? string.Empty;
            string reportArg = args.FirstOrDefault(a => a.StartsWith("--report=", StringComparison.Ordinal))?.Substring(9) ?? string.Empty;
            _runner.Filter = filter;
            GD.Print("== Procedural Pixel Creatures self test ==");
            GD.Print(Environment());
            int exitCode = 1;
            try
            {
                CoreTests.RunAll(_runner, CreatureRuntime.Registry, quick ? 10 : 100);
                await RuntimeTests();
                exitCode = _runner.Failed == 0 ? 0 : 1;
            }
            catch (Exception e)
            {
                GD.PrintErr("Self test crashed: " + e);
            }
            string json = _runner.ReportJson(Environment());
            WriteReport("user://reports/self_test_report.json", json);
            if (!string.IsNullOrEmpty(reportArg)) WriteReport(reportArg, json);
            GD.Print(_runner.Summary());
            GD.Print($"Report: {ProjectSettings.GlobalizePath("user://reports/self_test_report.json")}{(string.IsNullOrEmpty(reportArg) ? "" : " and " + reportArg)}");
            GetTree().Quit(exitCode);
        }

        public static string Environment() =>
            $"Godot {Engine.GetVersionInfo()["string"]} | {OS.GetName()} {OS.GetVersion()} | CPU {OS.GetProcessorName()} x{OS.GetProcessorCount()} | " +
            $".NET {System.Environment.Version} | Renderer {RenderingServer.GetVideoAdapterName()} ({RenderingServer.GetVideoAdapterVendor()}) | " +
            $"Rendering method {ProjectSettings.GetSetting("rendering/renderer/rendering_method")} | Headless {DisplayServer.GetName() == "headless"}";

        private static void WriteReport(string path, string json)
        {
            string dir = path.GetBaseDir();
            if (!string.IsNullOrEmpty(dir)) DirAccess.MakeDirRecursiveAbsolute(dir);
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
            f?.StoreString(json);
        }

        private SignalAwaiter NextFrame() => ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        private async Task Frames(int n)
        {
            for (int i = 0; i < n; i++) await NextFrame();
        }

        private async Task RuntimeTests()
        {
            await _runner.RunAsync("runtime.samples_load_and_build", SamplesLoad);
            await _runner.RunAsync("runtime.async_build_discards_stale_results", AsyncBuild);
            await _runner.RunAsync("runtime.actor_renders_and_uploads", ActorRenders);
            await _runner.RunAsync("runtime.preset_io_and_invalid_presets", PresetIo);
            await _runner.RunAsync("runtime.spritesheet_export_metadata", SpritesheetExport);
            await _runner.RunAsync("runtime.no_node_or_memory_leaks", LeakCheck);
            await _runner.RunAsync("runtime.scene_switching_is_clean", SceneSwitching);
            await _runner.RunAsync("workshop.test_area_map_sizes", TestAreaMaps);
            await _runner.RunAsync("workshop.test_area_100_creatures_culling", TestAreaPopulation);
            await _runner.RunAsync("workshop.overview_100_creatures", WorkshopOverview);
            await _runner.RunAsync("workshop.preview_camera_stays_fixed", PreviewCameraStaysFixed);
        }

        // ------------------------------------------------------------------ tests

        private Task SamplesLoad(TestContext ctx)
        {
            var paths = CreatureRuntime.SamplePaths();
            var perFamily = new Dictionary<string, int>();
            foreach (var p in paths)
            {
                var res = CreatureRuntime.LoadPreset(p);
                ctx.True(res.Ok, $"{p}: {res.Describe()}");
                var model = CreatureRuntime.Build(res.Genome!);
                ctx.True(model.Anatomy.Primitives.Count >= 2, $"{p}: anatomy");
                perFamily[res.Genome!.FamilyId] = perFamily.TryGetValue(res.Genome.FamilyId, out int n) ? n + 1 : 1;
            }
            foreach (var f in CreatureRuntime.Registry.Families)
                ctx.True(perFamily.TryGetValue(f.Id, out int n) && n >= 6, $"{f.Id}: at least 6 example genomes (found {(perFamily.TryGetValue(f.Id, out int m) ? m : 0)})");
            ctx.Metric("samples", paths.Count);
            ctx.Metric("families", perFamily.Count);
            return Task.CompletedTask;
        }

        private CreatureActor NewActor()
        {
            var actor = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn").Instantiate<CreatureActor>();
            actor.FamilyId = string.Empty;
            AddChild(actor);
            return actor;
        }

        private async Task AsyncBuild(TestContext ctx)
        {
            var actor = NewActor();
            var a = CreatureRuntime.Sample("quadruped", 11);
            var b = CreatureRuntime.Sample("winged", 12);
            var c = CreatureRuntime.Sample("serpent", 13);
            actor.SetGenome(a, async: true);
            actor.SetGenome(b, async: true);
            actor.SetGenome(c, async: true);
            ulong waited = Time.GetTicksMsec();
            while (actor.BuildPending && Time.GetTicksMsec() - waited < 10000) await NextFrame();
            await Frames(2);
            ctx.True(actor.Model != null, "no model after the asynchronous build");
            ctx.Equal(c.ContentHash, actor.Model!.Genome.ContentHash, "only the most recently requested genome is applied");
            ctx.True(actor.DiscardedBuilds >= 2, $"stale results discarded ({actor.DiscardedBuilds})");
            ctx.Metric("discarded", actor.DiscardedBuilds);
            actor.QueueFree();
            await Frames(2);
        }

        private async Task ActorRenders(TestContext ctx)
        {
            var actors = new List<CreatureActor>();
            foreach (var f in CreatureRuntime.Registry.Families)
            {
                var actor = NewActor();
                actor.SetGenome(CreatureRuntime.Sample(f.Id, 77), async: false);
                actor.MoveVelocity = new Vector2(1, 0.3f) * 20f;
                actors.Add(actor);
            }
            await Frames(20);
            foreach (var actor in actors)
            {
                string fam = actor.Model?.Genome.FamilyId ?? "?";
                ctx.True(actor.RenderCount > 0, $"{fam}: no frames rendered");
                ctx.True(actor.Texture != null, $"{fam}: no texture");
                ctx.True(actor.ContentExtent.X > 2 && actor.ContentExtent.Y > 2, $"{fam}: empty image ({actor.ContentExtent})");
                ctx.True(!actor.LastStats.Overflow, $"{fam}: parts outside the canvas");
                ctx.True(string.IsNullOrEmpty(actor.LastError), $"{fam}: {actor.LastError}");
            }
            ctx.Metric("actors", actors.Count);
            foreach (var a in actors) a.QueueFree();
            await Frames(2);
        }

        private Task PresetIo(TestContext ctx)
        {
            // round trip through user://
            var g = CreatureRuntime.Sample("arthropod", 5).WithName("Self-test");
            string path = "user://selftest/roundtrip.json";
            var err = CreatureRuntime.SavePreset(path, g);
            ctx.True(err == null, "save: " + err);
            var back = CreatureRuntime.LoadPreset(path);
            ctx.True(back.Ok, "load: " + back.Describe());
            ctx.Equal(g.ContentHash, back.Genome!.ContentHash, "content after save/load");
            ctx.Equal("Self-test", back.Genome.Name, "name");

            // broken inputs must be rejected with a message instead of crashing
            var cases = new Dictionary<string, string>
            {
                ["not_json"] = "this is not JSON {",
                ["empty"] = "",
                ["wrong_format"] = "{\"format\":\"something.else\",\"formatVersion\":1}",
                ["unknown_family"] = "{\"format\":\"ppc.creature\",\"formatVersion\":1,\"generatorVersion\":1,\"family\":\"dragoncat\",\"genes\":{}}",
                ["future_version"] = "{\"format\":\"ppc.creature\",\"formatVersion\":999,\"generatorVersion\":1,\"family\":\"quadruped\",\"genes\":{}}",
                ["wrong_types"] = "{\"format\":\"ppc.creature\",\"formatVersion\":1,\"generatorVersion\":1,\"family\":\"quadruped\",\"genes\":{\"size\":\"huge\"}}",
            };
            foreach (var kv in cases)
            {
                string p = $"user://selftest/invalid_{kv.Key}.json";
                using (var f = Godot.FileAccess.Open(p, Godot.FileAccess.ModeFlags.Write)) f.StoreString(kv.Value);
                var res = CreatureRuntime.LoadPreset(p);
                ctx.True(!res.Ok || res.Warnings.Count > 0, $"{kv.Key}: should have been rejected or produced a warning");
                ctx.True(!string.IsNullOrEmpty(res.Describe()), $"{kv.Key}: no error message");
                ctx.Note($"{kv.Key}: {res.Describe().Replace('\n', ' ')}");
            }
            var missing = CreatureRuntime.LoadPreset("user://selftest/does_not_exist.json");
            ctx.True(!missing.Ok, "missing file");
            return Task.CompletedTask;
        }

        private Task SpritesheetExport(TestContext ctx)
        {
            foreach (var fam in new[] { "quadruped", "winged", "serpent", "amorphous" })
            {
                var model = CreatureRuntime.Build(CreatureRuntime.Sample(fam, 21));
                var opt = new SpritesheetOptions { Fps = 12 };
                opt.Clips.Clear();
                opt.Clips.AddRange(new[] { ClipKind.Idle, ClipKind.Walk, ClipKind.Run, ClipKind.Action });
                opt.Directions.Clear();
                opt.Directions.AddRange(new[] { 6, 0 });
                var r = SpritesheetExporter.Export(model, CreatureRuntime.Registry, opt, "selftest_" + fam);
                ctx.True(r.TotalFrames > 0 && r.Pages.Count > 0, $"{fam}: no frames");
                foreach (var clip in r.Clips)
                {
                    foreach (var f in clip.Frames)
                    {
                        ctx.True(f.Frame.ContentBounds(out int x0, out int y0, out int x1, out int y1), $"{fam}/{clip.Name}: empty frame");
                        // nothing may touch the (enlarged) capture canvas border: that would mean clipping
                        ctx.True(x0 > 0 && y0 > 0 && x1 < f.Frame.Width && y1 < f.Frame.Height, $"{fam}/{clip.Name}: content clipped at the border");
                    }
                    if (clip.Loop) ctx.True(clip.Frames.Count >= 4, $"{fam}/{clip.Name}: loop too short");
                }
                var meta = JsonNode.Parse(r.MetadataJson)!.AsObject();
                ctx.Equal(r.FrameWidth, (int)meta["frameWidth"]!, $"{fam}: frameWidth");
                ctx.Equal(r.OriginX, (int)meta["originX"]!, $"{fam}: originX");
                int pageW = (int)meta["pages"]![0]!["width"]!, pageH = (int)meta["pages"]![0]!["height"]!;
                foreach (var clip in meta["clips"]!.AsArray())
                {
                    ctx.True(clip!["frames"]!.AsArray().Count == (int)clip["frameCount"]!, $"{fam}: frameCount");
                    foreach (var fr in clip["frames"]!.AsArray())
                    {
                        int x = (int)fr!["x"]!, y = (int)fr["y"]!;
                        ctx.True(x >= 0 && y >= 0 && x + r.FrameWidth <= pageW && y + r.FrameHeight <= pageH, $"{fam}: frame rectangle outside the page");
                    }
                }
                ctx.Metric(fam + ".frames", r.TotalFrames);
                ctx.Metric(fam + ".frameW", r.FrameWidth);
                ctx.Metric(fam + ".frameH", r.FrameHeight);
            }
            return Task.CompletedTask;
        }

        private async Task LeakRound(int round)
        {
            var families = CreatureRuntime.Registry.Families;
            var batch = new List<CreatureActor>();
            for (int i = 0; i < 25; i++)
            {
                var actor = NewActor();
                var fam = families[(round * 25 + i) % families.Count];
                actor.SetGenome(CreatureRuntime.Sample(fam.Id, (ulong)(1000 + round * 25 + i)), async: (i & 1) == 0);
                actor.MoveVelocity = new Vector2(i % 3 - 1, i % 2) * 25f;
                batch.Add(actor);
            }
            await Frames(12);
            foreach (var a in batch)
            {
                a.TriggerAction();
                a.QueueFree();
            }
            await Frames(3);
        }

        /// <summary>
        /// Opens and closes the workshop and the test area repeatedly (like switching scenes in the app)
        /// while their creatures are still being built; nodes, orphans and objects must return to the
        /// level after the warm-up round.
        /// </summary>
        private async Task SceneSwitching(TestContext ctx)
        {
            PixelCreatures.Workshop.Main.EmbeddedForTests = true;
            try
            {
                string[] scenes = { "res://workshop/Main.tscn", "res://workshop/TestArea/TestArea.tscn" };
                await CycleScenes(scenes, 1);
                await Settle();
                double nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                double objects0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
                double orphans0 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
                int cycles = await CycleScenes(scenes, 3);
                await Settle();
                double nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
                double objects1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
                double orphans1 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
                ctx.Metric("sceneLoads", cycles);
                ctx.Metric("nodesBefore", nodes0);
                ctx.Metric("nodesAfter", nodes1);
                ctx.Metric("objectsBefore", objects0);
                ctx.Metric("objectsAfter", objects1);
                ctx.True(nodes1 <= nodes0, $"nodes after {cycles} scene switches: {nodes0} -> {nodes1}");
                ctx.True(orphans1 <= orphans0, $"orphan nodes: {orphans0} -> {orphans1}");
                ctx.True(objects1 - objects0 <= 64, $"objects: {objects0} -> {objects1}");
            }
            finally
            {
                PixelCreatures.Workshop.Main.EmbeddedForTests = false;
            }
        }

        private async Task<int> CycleScenes(string[] scenes, int rounds)
        {
            int loads = 0;
            for (int r = 0; r < rounds; r++)
            {
                foreach (var path in scenes)
                {
                    var node = GD.Load<PackedScene>(path).Instantiate();
                    AddChild(node);
                    // a few frames only: asynchronous builds are usually still running when the scene closes
                    await Frames(8 + r * 7);
                    node.QueueFree();
                    await Frames(2);
                    loads++;
                }
            }
            return loads;
        }

        /// <summary>
        /// Map generator of the test area: every size preset has water, sand, paths and props at the expected
        /// density on suitable ground; generation is deterministic, differs per seed and can be cancelled.
        /// </summary>
        private Task TestAreaMaps(TestContext ctx)
        {
            foreach (var size in MapSize.All)
            {
                var sw = Stopwatch.StartNew();
                var map = EnvironmentArt.GenerateMap(size.Width, size.Height, 9001);
                ctx.Metric($"generateMs_{size.Width}x{size.Height}", Math.Round(sw.Elapsed.TotalMilliseconds, 1));
                double water = map.Fraction(TestMap.Water), sand = map.Fraction(TestMap.Sand), path = map.Fraction(TestMap.Path);
                ctx.Metric($"water_{size.Width}x{size.Height}", Math.Round(water, 4));
                ctx.True(water > 0.03 && water < 0.22, $"{size}: water share {water:P1}");
                ctx.True(sand > 0.004, $"{size}: sand share {sand:P1}");
                ctx.True(path > 0.01 && path < 0.15, $"{size}: path share {path:P1}");
                ctx.True(map.Elevation.Max() >= 18, $"{size}: visible elevation range");
                ctx.True(map.WaterDepth.Max() > 0.7f, $"{size}: deep water habitat");
                ctx.True(map.Cliffs.Count > 0, $"{size}: cliff faces participate in depth ordering");
                int safeWater = 0, cliffs = 0;
                for (int y = 2; y < map.Height - 2; y += 2)
                    for (int x = 2; x < map.Width - 2; x += 2)
                    {
                        var p = new Vector2(x, y);
                        int index = y * map.Width + x;
                        ctx.True(map.WaterSurfaceRgba[index * 4 + 3] > 0 == map.IsWater(x, y), "surface sheet only covers water");
                        if (map.CanOccupy(p, true, false, 24))
                        {
                            safeWater++;
                            ctx.True(map.IsWater(x - 24, y) && map.IsWater(x + 24, y), "swimmer footprint remains inside shore");
                            ctx.True(!map.CanOccupy(p, false, false, 4), "land creatures cannot stand in water");
                        }
                        if (Math.Abs(map.Elevation[index] - map.Elevation[index + map.Width]) > 4)
                        {
                            cliffs++;
                            ctx.True(!map.CanTravel(p, p + Vector2.Down, false, false, 0), "walkers cannot cross a cliff");
                            ctx.True(map.CanTravel(p, p + Vector2.Down, false, true, 0), "airborne creatures can cross a cliff");
                        }
                        ctx.Near(map.Elevation[index], p.Y - map.Project(p).Y, 0.001, "actor and terrain elevation projection agree");
                    }
                ctx.True(safeWater > 100 && cliffs > 0, $"{size}: sufficient swimming space and blocked cliffs");
                int expected = (int)Math.Round(size.Width * (double)size.Height / 5600);
                ctx.True(map.Props.Count >= expected * 0.8 && map.Props.Count <= expected, $"{size}: {map.Props.Count} props, expected about {expected}");
                foreach (var p in map.Props)
                    ctx.True(map.Terrain[p.Y * map.Width + p.X] == TestMap.Grass, $"{size}: prop at {p.X},{p.Y} not on grass");
                ctx.True(map.Ponds.Count >= Math.Max(1, (int)Math.Round(size.Width * (double)size.Height / 300000.0) - 1), $"{size}: only {map.Ponds.Count} ponds");
                foreach (var pond in map.Ponds)
                {
                    var b = pond.Bounds;
                    ctx.True(b.Position.X >= 0 && b.Position.Y >= 0 && b.End.X <= map.Width && b.End.Y <= map.Height, $"{size}: pond bounds {b} outside the map");
                    ctx.True(pond.GlintFrames.Length == 4 && pond.GlintFrames.All(f => f.Length == b.Size.X * b.Size.Y * 4), $"{size}: glint frames");
                }
                ctx.True(map.MiniWidth > 0 && map.MiniWidth <= EnvironmentArt.MinimapMaxWidth && map.MiniHeight <= EnvironmentArt.MinimapMaxHeight
                         && map.MiniRgba.Length == map.MiniWidth * map.MiniHeight * 4, $"{size}: minimap {map.MiniWidth}x{map.MiniHeight}");
            }
            var a = EnvironmentArt.GenerateMap(960, 600, 77);
            var b2 = EnvironmentArt.GenerateMap(960, 600, 77);
            ctx.True(a.GroundRgba.AsSpan().SequenceEqual(b2.GroundRgba) && a.Terrain.AsSpan().SequenceEqual(b2.Terrain) && a.Props.Count == b2.Props.Count,
                "the same seed must produce the same map (parallel generation)");
            ctx.True(a.Elevation.AsSpan().SequenceEqual(b2.Elevation) && a.Clearance.AsSpan().SequenceEqual(b2.Clearance), "height and navigation are deterministic");
            var c = EnvironmentArt.GenerateMap(960, 600, 78);
            ctx.True(!a.Terrain.AsSpan().SequenceEqual(c.Terrain), "a different seed must produce a different map");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            bool cancelled = false;
            try { EnvironmentArt.GenerateMap(2560, 1600, 1, cancel.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            ctx.True(cancelled, "canceled generation must throw OperationCanceledException");
            return Task.CompletedTask;
        }

        /// <summary>
        /// The test area with 100 creatures on the largest map: population limit, creatures on suitable ground,
        /// creatures outside the view are culled (no rasterization) but keep moving, a background map change keeps
        /// everybody on the new map, camera and "take control" work.
        /// </summary>
        private async Task TestAreaPopulation(TestContext ctx)
        {
            var area = GD.Load<PackedScene>("res://workshop/TestArea/TestArea.tscn").Instantiate<TestArea>();
            AddChild(area);
            try
            {
                await Frames(2);
                area.SetMapSize(MapSize.All.Length - 1, immediate: true);
                ctx.Equal(2560, area.Map.Width, "map width");
                area.SetPopulation(150);
                ctx.Equal(TestArea.MaxCreatures, area.TargetPopulation, "creature limit");
                int frames = 0;
                while (area.CreatureCount < TestArea.MaxCreatures && frames++ < 900) await NextFrame();
                ctx.Metric("framesUntil100", frames);
                ctx.Equal(TestArea.MaxCreatures, area.CreatureCount, "creatures on the map");
                await Frames(30);
                ctx.True(OnSuitableGround(area, out string bad), "creature on unsuitable ground: " + bad);
                int visible = area.VisibleCreatureCount;
                ctx.Metric("visibleOf100", visible);
                ctx.True(visible > 0 && visible < TestArea.MaxCreatures, $"visible {visible} of 100: culling is not working");

                // watch the culled ones for a few seconds of game time (wanderers pause 0.5-3.5 s between walks);
                // creatures that walk into the view in between are left out
                var culled = area.AllActors().Where(x => x.IsCulled).ToList();
                var renders = culled.Select(x => x.RenderCount).ToArray();
                var positions = culled.Select(x => x.GroundPosition).ToArray();
                var everVisible = new bool[culled.Count];
                int still = 0, rendered = 0, moved = 0;
                for (double gameTime = 0; gameTime < 8.0;)
                {
                    await NextFrame();
                    gameTime += GetProcessDeltaTime();
                    still = rendered = moved = 0;
                    for (int i = 0; i < culled.Count; i++)
                    {
                        everVisible[i] |= !culled[i].IsCulled;
                        if (everVisible[i]) continue;
                        still++;
                        if (culled[i].RenderCount != renders[i]) rendered++;
                        if (culled[i].GroundPosition.DistanceTo(positions[i]) > 0.5f) moved++;
                    }
                    if (gameTime > 1.0 && moved >= 5) break;
                }
                ctx.Metric("culledChecked", still);
                ctx.Metric("culledMoved", moved);
                ctx.True(still > 0 && rendered == 0, $"rasterized outside the view: {rendered} of {still}");
                ctx.True(moved > 0, "creatures outside the view must keep moving");

                // camera: centre on a far corner, the view follows the request (clamped to the map)
                area.LookAt(new Vector2(area.Map.Width - 10, area.Map.Height - 10));
                await Frames(2);
                ctx.True(!area.CameraFollows && area.ViewRect.End.X >= area.Map.Width - 1 && area.ViewRect.End.Y >= area.Map.Height - 1, $"camera: {area.ViewRect}");

                // take control of a visible creature by its position on the map
                await Frames(5);
                var target = area.AllActors().FirstOrDefault(x => x != area.Player && !x.IsCulled && x.ContentExtent != Vector2I.Zero);
                if (target != null)
                {
                    ctx.True(area.TakeControlAt(target.Position + target.ContentCenter), "take control of a creature by clicking");
                    ctx.True(area.Player == target && area.CameraFollows, "the clicked creature is controlled, the camera follows");
                }
                else ctx.Note("no visible creature for the take-control test");

                // the real input path: keys, mouse wheel and right-button drag go through the viewport. The
                // headless window is tiny, so overlay (F1) and minimap (M) are hidden first to free the map.
                PushKey(Key.F1);
                PushKey(Key.M);
                ctx.True(!area.OverlayVisible && !area.MinimapVisible, "F1 and M hide the overlay and the minimap");
                var center = area.GetGlobalRect().GetCenter();
                int zoom0 = area.Zoom;
                // a wheel step arrives as press and release, like from a real mouse
                PushMouse(MouseButton.WheelDown, true, center);
                PushMouse(MouseButton.WheelDown, false, center);
                ctx.Equal(Math.Max(1, zoom0 - 1), area.Zoom, "mouse wheel zooms out");
                await Frames(2);
                var view0 = area.ViewRect.GetCenter();
                PushMouse(MouseButton.Right, true, center);
                GetViewport().PushInput(new InputEventMouseMotion { Position = center + new Vector2(60, 30), GlobalPosition = center + new Vector2(60, 30), Relative = new Vector2(60, 30), ButtonMask = MouseButtonMask.Right });
                PushMouse(MouseButton.Right, false, center + new Vector2(60, 30));
                await Frames(2);
                ctx.True(!area.CameraFollows && area.ViewRect.GetCenter() != view0, $"dragging with the right mouse button: {view0} -> {area.ViewRect.GetCenter()}");
                PushKey(Key.F);
                ctx.True(area.CameraFollows, "F: camera follows again");
                PushKey(Key.F1);
                PushKey(Key.M);
                ctx.True(area.OverlayVisible && area.MinimapVisible, "F1 and M show the overlay and the minimap again");
                PushKey(Key.Minus);
                ctx.Equal(90, area.TargetPopulation, "minus key");
                PushKey(Key.Equal);
                ctx.Equal(100, area.TargetPopulation, "plus key");
                PushKey(Key.Pagedown);
                ctx.True(area.MapBusy && area.MapIndex == MapSize.All.Length - 2, "Page Down: a smaller map is generated");
                frames = 0;
                while (area.MapBusy && frames++ < 900) await NextFrame();
                ctx.Equal(MapSize.All[MapSize.All.Length - 2].Width, area.Map.Width, "map width after Page Down");

                // smaller map in the background: everybody comes along, inside the map and on suitable ground
                var sw = Stopwatch.StartNew();
                area.SetMapSize(0);
                frames = 0;
                while (area.MapBusy && frames++ < 900) await NextFrame();
                ctx.Metric("mapSwitchMs", Math.Round(sw.Elapsed.TotalMilliseconds, 1));
                ctx.Equal(640, area.Map.Width, "map width after the switch");
                await Frames(3);
                ctx.Equal(TestArea.MaxCreatures, area.CreatureCount, "creatures after the map switch");
                ctx.True(OnSuitableGround(area, out bad), "after the map switch: " + bad);

                var oldPopulation = area.AllActors().Select(a => a.Genome!.ContentHash).ToHashSet();
                PushKey(Key.G);
                frames = 0;
                while (area.CreatureCount < TestArea.MaxCreatures && frames++ < 900) await NextFrame();
                ctx.Equal(TestArea.MaxCreatures, area.CreatureCount, "reroll preserves population size");
                ctx.True(area.AllActors().All(a => !oldPopulation.Contains(a.Genome!.ContentHash)), "reroll replaces every genome");
                ctx.True(OnSuitableGround(area, out bad), "after reroll: " + bad);

                area.SetPopulation(1);
                await Frames(2);
                ctx.Equal(1, area.CreatureCount, "only the controlled creature is left");
            }
            finally
            {
                area.QueueFree();
                await Frames(3);
            }
        }

        /// <summary>
        /// Workshop overview: 100 different creatures of all families at once in a 1600x940 window, only cells
        /// on screen are rasterized, the hidden editor pauses, selection by click and keys, favourites survive a
        /// reroll, offspring and archetype runs, double click opens the creature in the editor.
        /// </summary>
        private async Task WorkshopOverview(TestContext ctx)
        {
            PixelCreatures.Workshop.Main.EmbeddedForTests = true;
            var host = new SubViewport { Size = new Vector2I(1600, 940), Disable3D = true };
            AddChild(host);
            var main = GD.Load<PackedScene>("res://workshop/Main.tscn").Instantiate<PixelCreatures.Workshop.Main>();
            host.AddChild(main);
            try
            {
                await Frames(5);
                var ov = main.Overview;
                ctx.True(ov != null && !main.OverviewActive, "overview created, editor active");
                if (ov == null) return;

                // The editor reuses its actor across families: underwater tint must not leak into land previews.
                main.State.NewCreature("aquatic", 9);
                for (int i = 0; i < 300 && main.PreviewActor.Genome?.FamilyId != "aquatic"; i++) await NextFrame();
                await Frames(5);
                ctx.True(main.PreviewActor.WaterLevel == 1000 && !main.PreviewActor.DrawShadow, "aquatic editor preview is submerged without a land shadow");
                main.State.NewCreature("quadruped", 26);
                for (int i = 0; i < 300 && main.PreviewActor.Genome?.FamilyId != "quadruped"; i++) await NextFrame();
                await Frames(5);
                ctx.True(double.IsNaN(main.PreviewActor.WaterLevel) && main.PreviewActor.DrawShadow, "switching back to land restores normal shading and shadow");

                var sw = Stopwatch.StartNew();
                ov.Start(new OverviewView.GenerationSettings { Seed = 4242, Count = 150 }, null);
                main.ShowOverview(true);
                int frames = 0;
                while (ov.Busy && frames++ < 1800) await NextFrame();
                ctx.Metric("generateMs", Math.Round(ov.LastGenerationMs, 1));
                ctx.Metric("msUntilAllActors", Math.Round(sw.Elapsed.TotalMilliseconds, 1));
                ctx.Metric("framesUntilAllActors", frames);
                ctx.Equal(OverviewView.MaxCreatures, ov.Entries.Count, "creatures (limit 100)");
                ctx.Equal(OverviewView.MaxCreatures, ov.ActorCount, "actors in the grid");
                int distinct = ov.Entries.Select(e => e.Genome.ContentHash).Distinct().Count();
                int families = ov.Entries.Select(e => e.Family).Distinct().Count();
                int archetypes = ov.Entries.Select(e => e.Family + "/" + e.Archetype).Distinct().Count();
                int names = ov.Entries.Select(e => e.Genome.Name).Distinct().Count();
                ctx.Metric("distinctGenomes", distinct);
                ctx.Metric("families", families);
                ctx.Metric("archetypes", archetypes);
                ctx.Metric("distinctNames", names);
                ctx.Equal(OverviewView.MaxCreatures, distinct, "distinct genomes");
                ctx.Equal(CreatureRuntime.Registry.Families.Count, families, "all families present");
                ctx.True(archetypes >= 30, $"only {archetypes} archetypes");
                ctx.True(names >= 95, $"only {names} distinct names");
                ctx.True(ov.Grid.Columns >= 4 && ov.Grid.EffectiveZoom >= 2, $"grid with {ov.Grid.Columns} columns at zoom {ov.Grid.EffectiveZoom}x");

                // only creatures on screen are rasterized; the hidden editor preview pauses
                await Frames(20);
                var culled = ov.Entries.Where(e => e.Actor!.IsCulled).Select(e => e.Actor!).ToList();
                var shown = ov.Entries.Where(e => !e.Actor!.IsCulled).Select(e => e.Actor!).ToList();
                ctx.Metric("visibleOf100", shown.Count);
                ctx.True(shown.Count > 0 && culled.Count > 0, $"visible {shown.Count} of 100: culling is not working");
                var culledRenders = culled.Select(a => a.RenderCount).ToArray();
                var shownRenders = shown.Select(a => a.RenderCount).ToArray();
                long preview0 = main.PreviewActor.RenderCount;
                await Frames(30);
                int culledRendered = culled.Where((a, i) => a.RenderCount != culledRenders[i]).Count();
                int shownRendered = shown.Where((a, i) => a.RenderCount != shownRenders[i]).Count();
                ctx.True(culledRendered == 0, $"rasterized outside the view: {culledRendered} of {culled.Count}");
                ctx.True(shownRendered > shown.Count / 2, $"visible creatures animated: {shownRendered} of {shown.Count}");
                ctx.Equal(preview0, main.PreviewActor.RenderCount, "editor preview pauses in the overview");

                // selection: click on a cell, arrow keys, favourite key
                var grid = ov.Grid;
                Vector2 CellCentre(int i) => grid.GetGlobalRect().Position + grid.CellScreenRect(i).GetCenter();
                void Click(int i, bool twice = false)
                {
                    var at = CellCentre(i);
                    host.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, DoubleClick = twice, Position = at, GlobalPosition = at });
                    host.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
                }
                void Key1(Key key)
                {
                    host.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
                    host.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
                }
                Click(7);
                await Frames(2);
                ctx.True(ov.SelectedEntry == ov.Order[7] && grid.Selected == 7, $"click selects cell 7 (selected: {grid.Selected})");
                ctx.True(ov.InspectorActor.Model == ov.Order[7].Model, "inspector shows the selected creature");
                Key1(Key.Right);
                ctx.Equal(8, grid.Selected, "right arrow");
                Key1(Key.Down);
                ctx.Equal(8 + grid.Columns, grid.Selected, "down arrow");
                Key1(Key.End);
                await Frames(2);
                ctx.True(grid.Selected == 99 && grid.ScrollY > 0, $"End: last creature, grid scrolled ({grid.ScrollY})");
                Key1(Key.Home);
                Key1(Key.F);
                ctx.True(ov.Order[0].Favorite, "F marks as favorite");
                Click(3);
                Key1(Key.F);
                var favourites = ov.Entries.Where(e => e.Favorite).Select(e => e.Genome.ContentHash).ToList();
                var before = new HashSet<ulong>(ov.Entries.Select(e => e.Genome.ContentHash));

                // walk in place: everybody faces the new direction and moves, nobody leaves its cell
                Key1(Key.Key2);
                Key1(Key.Q);
                await Frames(20);
                var pos = ov.Entries.Select(e => e.Actor!.Position).ToArray();
                await Frames(20);
                ctx.True(ov.Mode == MoveMode.Walk && ov.Direction == 0, $"walk and turn by key (mode {ov.Mode}, direction {ov.Direction})");
                ctx.True(ov.Entries.All(e => e.Actor!.MoveVelocity.Length() > 0), "all creatures walk");
                ctx.True(ov.Entries.Select((e, i) => e.Actor!.Position == pos[i]).All(x => x), "creatures walk in place");
                Key1(Key.Key1);

                // reroll: favourites stay, everything else is new
                ov.RerollKeepFavorites();
                frames = 0;
                while (ov.Busy && frames++ < 1800) await NextFrame();
                var kept = ov.Entries.Where(e => e.Favorite).Select(e => e.Genome.ContentHash).ToList();
                int renewed = ov.Entries.Count(e => !before.Contains(e.Genome.ContentHash));
                ctx.Equal(OverviewView.MaxCreatures, ov.Entries.Count, "creatures after the reroll");
                ctx.True(favourites.Count == 2 && favourites.All(kept.Contains), $"favorites kept ({kept.Count} of {favourites.Count})");
                ctx.True(renewed >= OverviewView.MaxCreatures - favourites.Count - 2, $"newly generated: {renewed}");

                // offspring of the editor creature
                var parent = main.State.Genome;
                ov.Start(new OverviewView.GenerationSettings { Source = OverviewView.SourceKind.Offspring, Parent = parent, Count = 24, Seed = 9 }, null);
                frames = 0;
                while (ov.Busy && frames++ < 1800) await NextFrame();
                ctx.Equal(24, ov.Entries.Count, "offspring");
                ctx.True(ov.Entries.All(e => e.Genome.FamilyId == parent.FamilyId && e.Genome.Lineage.Generation == parent.Lineage.Generation + 1),
                    "offspring belong to the parent's family (one generation later)");
                ctx.Equal(24, ov.Entries.Select(e => e.Genome.ContentHash).Distinct().Count(), "distinct offspring");

                // one archetype of one family
                var fam = CreatureRuntime.Registry.GetFamily("quadruped");
                string arch = fam.Archetypes[fam.Archetypes.Count - 1].Id;
                ov.Start(new OverviewView.GenerationSettings { Family = fam.Id, Archetype = arch, Count = 12, Seed = 5 }, null);
                frames = 0;
                while (ov.Busy && frames++ < 1800) await NextFrame();
                ctx.True(ov.Entries.Count == 12 && ov.Entries.All(e => e.Family == fam.Id && e.Archetype == arch), $"12 creatures of archetype {arch}");

                // double click opens the creature in the editor and switches back
                Click(2, twice: true);
                await Frames(2);
                var chosen = ov.Order[2].Genome;
                ctx.True(!main.OverviewActive && main.State.Genome.ContentHash == chosen.ContentHash, "double click opens the creature in the editor");
                await Frames(10);
                ctx.True(main.PreviewActor.RenderCount > preview0, "editor preview runs again");
                main.ShowOverview(true);
                ctx.True(!ov.Busy && ov.Entries.Count == 12, "back in the overview without regenerating");
            }
            finally
            {
                main.QueueFree();
                host.QueueFree();
                PixelCreatures.Workshop.Main.EmbeddedForTests = false;
                await Frames(3);
            }
        }

        private void PushMouse(MouseButton button, bool pressed, Vector2 at) =>
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = at, GlobalPosition = at });

        private void PushKey(Key key)
        {
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
            GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        }

        /// <summary>Walkers stand on land, swimmers in water (a few pixels of slack at the shore), everybody inside the map.</summary>
        private static bool OnSuitableGround(TestArea area, out string problem)
        {
            var map = area.Map;
            foreach (var a in area.AllActors())
            {
                var s = GodotConvert.GroundToScreen(a.GroundPosition.X, a.GroundPosition.Y);
                int x = (int)s.X, y = (int)s.Y;
                problem = $"{a.Genome?.Name} at {x},{y}";
                if (!map.InBounds(x, y)) return false;
                var kind = a.Model?.Anatomy.Rig.Locomotion;
                if (kind == PixelCreatures.Core.Anatomy.LocomotionKind.Fly) continue;
                bool swimmer = kind == PixelCreatures.Core.Anatomy.LocomotionKind.Swim;
                bool near = false;
                for (int dy = -3; dy <= 3 && !near; dy += 3)
                    for (int dx = -6; dx <= 6; dx += 3)
                        if (map.IsWater(x + dx, y + dy) == swimmer) { near = true; break; }
                if (!near) return false;
            }
            problem = string.Empty;
            return true;
        }

        private async Task Settle()
        {
            await Frames(10);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Frames(5);
        }

        private async Task LeakCheck(TestContext ctx)
        {
            // one warm-up round first: scene/script caches and pooled engine objects are created once
            await LeakRound(99);
            await Frames(5);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            double nodes0 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            double objects0 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            double orphans0 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            long managed0 = GC.GetTotalMemory(true);
            var perRound = new List<double>();
            for (int round = 0; round < 6; round++)
            {
                await LeakRound(round);
                double o = Performance.GetMonitor(Performance.Monitor.ObjectCount);
                perRound.Add(o);
                ctx.Metric($"objectsAfterRound{round}", o);
            }
            // Godot frees some engine objects lazily (every other frame batch), so counts oscillate;
            // a leak shows up as growth between rounds in the same phase
            double slope = Math.Max(perRound[4] - perRound[0], perRound[5] - perRound[1]) / 4.0;
            ctx.Metric("objectGrowthPerRound", slope);
            ctx.True(slope <= 2.0, $"objects grow by {slope:0.0} per round (25 actors)");
            await Frames(10);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            double nodes1 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            double objects1 = Performance.GetMonitor(Performance.Monitor.ObjectCount);
            double orphans1 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            long managed1 = GC.GetTotalMemory(true);
            ctx.Metric("nodesBefore", nodes0);
            ctx.Metric("nodesAfter", nodes1);
            ctx.Metric("objectsBefore", objects0);
            ctx.Metric("objectsAfter", objects1);
            ctx.Metric("orphansAfter", orphans1);
            ctx.Metric("managedMBBefore", managed0 / 1048576.0);
            ctx.Metric("managedMBAfter", managed1 / 1048576.0);
            ctx.True(nodes1 <= nodes0, $"nodes after 150 actors: {nodes0} -> {nodes1}");
            ctx.True(orphans1 <= orphans0, $"orphan nodes: {orphans0} -> {orphans1}");
            ctx.True(objects1 - objects0 <= 64, $"objects: {objects0} -> {objects1}");
            // the model cache is bounded (LRU); 150 creatures must not grow the managed heap without limit
            ctx.True(managed1 - managed0 < 64L * 1024 * 1024, $"managed memory: {managed0 / 1048576.0:0.0} -> {managed1 / 1048576.0:0.0} MB");
        }
    }
}
