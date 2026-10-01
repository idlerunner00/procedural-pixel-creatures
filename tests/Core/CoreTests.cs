// Procedural Pixel Creatures - engine independent test suite (runs in the console harness and inside
// Godot via --self-test). Everything here is deterministic.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Rendering;
using PixelCreatures.Core.Serialization;

namespace PixelCreatures.Tests
{
    public static class CoreTests
    {
        public static void RunAll(TestRunner r, CreatureRegistry registry, int seedsPerFamily = 100)
        {
            var factory = new CreatureFactory(registry);
            r.Run("math.deterministic_kernels", ctx => DeterministicMath(ctx));
            r.Run("random.golden_sequences", ctx => RandomGolden(ctx));
            r.Run("genome.sampling_is_deterministic", ctx => SamplingDeterminism(ctx, registry));
            r.Run("genome.streams_are_independent", ctx => StreamIndependence(ctx, registry, factory));
            r.Run("genome.quantized_json_roundtrip", ctx => JsonRoundTrip(ctx, registry));
            r.Run("serializer.rejects_invalid_input", ctx => SerializerValidation(ctx, registry));
            r.Run("serializer.migration_chain", ctx => MigrationChain(ctx, registry));
            r.Run("genetics.mutation_deterministic_and_locked", ctx => MutationTests(ctx, registry));
            r.Run("genetics.crossover_and_compatibility", ctx => CrossoverTests(ctx, registry));
            r.Run("genetics.regenerate_respects_locks", ctx => RegenerateTests(ctx, registry));
            r.Run($"anatomy.valid_for_{seedsPerFamily}_seeds_per_family", ctx => AnatomyValidity(ctx, registry, factory, seedsPerFamily));
            r.Run("render.deterministic_and_golden", ctx => RenderDeterminism(ctx, registry, factory));
            r.Run("motion.framerate_independent", ctx => MotionFramerateIndependence(ctx, registry, factory));
            r.Run("motion.planted_feet_do_not_slide", ctx => FootSliding(ctx, registry, factory));
            r.Run("motion.states_and_directions_stay_in_canvas", ctx => CanvasSafety(ctx, registry, factory));
            r.Run("motion.turning_in_place_reorients_body", ctx => TurnInPlace(ctx, registry, factory));
            r.Run("anatomy.extreme_genes_build_move_and_render", ctx => ExtremeGenes(ctx, registry, factory));
        }

        // ------------------------------------------------------------------ math & random

        private static void DeterministicMath(TestContext ctx)
        {
            // Bit exact reference values of the DMath kernels. They must be identical on every OS/CPU.
            ctx.Equal(0x3FEAED548F090CEEL, BitConverter.DoubleToInt64Bits(DMath.Sin(1.0)), "sin(1)");
            ctx.Equal(0x3FE14A280FB5068CL, BitConverter.DoubleToInt64Bits(DMath.Cos(1.0)), "cos(1)");
            // DMath.Exp(1) is one ulp above the correctly rounded value; determinism, not rounding, is what matters
            ctx.Equal(0x4005BF0A8B14576AL, BitConverter.DoubleToInt64Bits(DMath.Exp(1.0)), "exp(1)");
            ctx.Equal(0x3FE62E42FEFA39EFL, BitConverter.DoubleToInt64Bits(DMath.Log(2.0)), "log(2)");
            ctx.Equal(0x3FE921FB54442D18L, BitConverter.DoubleToInt64Bits(DMath.Atan2(1.0, 1.0)), "atan2(1,1)");
            double maxErr = 0;
            for (int i = -2000; i <= 2000; i++)
            {
                double x = i * 0.0137;
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Sin(x) - Math.Sin(x)));
                maxErr = Math.Max(maxErr, Math.Abs(DMath.Cos(x) - Math.Cos(x)));
            }
            ctx.True(maxErr < 1e-15, $"sin/cos accuracy {maxErr}");
            ctx.Metric("maxTrigError", maxErr);
        }

        private static void RandomGolden(TestContext ctx)
        {
            var rng = new Rng(42);
            var seq = new uint[] { rng.NextUInt(), rng.NextUInt(), rng.NextUInt(), rng.NextUInt() };
            ctx.Note("pcg32(42): " + string.Join(",", seq));
            ctx.Equal(Golden.Pcg42[0], seq[0], "pcg32[0]");
            ctx.Equal(Golden.Pcg42[1], seq[1], "pcg32[1]");
            ctx.Equal(Golden.Pcg42[2], seq[2], "pcg32[2]");
            ctx.Equal(Golden.Pcg42[3], seq[3], "pcg32[3]");
            ctx.Equal(Golden.Fnv, StableHash.Fnv1a64("Procedural Pixel Creatures"), "fnv1a64");
            ctx.Equal(Golden.Derived, StableHash.Derive(123456789UL, "stream.anatomy"), "derive");
            // unbiased bounded ints stay in range
            var r2 = new Rng(7);
            for (int i = 0; i < 10000; i++)
            {
                int v = r2.NextInt(37);
                ctx.True(v >= 0 && v < 37, "NextInt range");
            }
        }

        // ------------------------------------------------------------------ genome

        private static void SamplingDeterminism(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var a = GenomeFactory.Sample(f, 424242);
                var b = GenomeFactory.Sample(f, 424242);
                var c = GenomeFactory.Sample(f, 424243);
                ctx.True(a.ContentEquals(b), $"{f.Id}: same seed must give same genome");
                ctx.True(!a.ContentEquals(c), $"{f.Id}: different seed should differ");
                ctx.Equal(a.ContentHash, b.ContentHash, $"{f.Id}: content hash");
            }
        }

        private static void StreamIndependence(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 99);
                var colored = g.With("color.hue", (g.Get("color.hue") + 137) % 360).With("color.saturation", 1 - g.Get("color.saturation"));
                ctx.Equal(g.AnatomyKey, colored.AnatomyKey, $"{f.Id}: anatomy key must ignore colour genes");
                ctx.Equal(g.PatternKey, colored.PatternKey, $"{f.Id}: pattern key must ignore colour genes");
                ctx.True(g.ColorKey != colored.ColorKey, $"{f.Id}: colour key must change");
                var a1 = f.BuildAnatomy(g, reg);
                var a2 = f.BuildAnatomy(colored, reg);
                ctx.Equal(AnatomyHash(a1), AnatomyHash(a2), $"{f.Id}: anatomy geometry after colour change");
                var patterned = g.With("pattern.density", 1 - g.Get("pattern.density"));
                ctx.Equal(AnatomyHash(a1), AnatomyHash(f.BuildAnatomy(patterned, reg)), $"{f.Id}: anatomy after pattern change");
                var moved = g.With("motion.energy", 1 - g.Get("motion.energy"));
                ctx.Equal(g.AnatomyKey, moved.AnatomyKey, $"{f.Id}: anatomy key must ignore motion genes");
            }
        }

        public static ulong AnatomyHash(CreatureAnatomy a)
        {
            ulong h = 17;
            void D(double v) => h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(v));
            foreach (var b in a.Skeleton.Bones)
            {
                h = StableHash.Combine(h, (ulong)(b.Parent + 7));
                D(b.RestOffset.X); D(b.RestOffset.Y); D(b.RestOffset.Z);
                D(b.RestRotation.X); D(b.RestRotation.Y); D(b.RestRotation.Z); D(b.RestRotation.W);
            }
            foreach (var p in a.Primitives)
            {
                h = StableHash.Combine(h, (ulong)p.Kind * 31 + (ulong)p.Material);
                D(p.LocalA.X); D(p.LocalA.Y); D(p.LocalA.Z); D(p.LocalB.X); D(p.LocalB.Y); D(p.LocalB.Z);
                D(p.RadiusA); D(p.RadiusB); D(p.Radii.X); D(p.Radii.Y); D(p.Radii.Z);
            }
            foreach (var fe in a.Features) { D(fe.LocalPosition.X); D(fe.LocalPosition.Y); D(fe.LocalPosition.Z); D(fe.Size); }
            return h;
        }

        private static void JsonRoundTrip(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            foreach (var f in reg.Families)
            {
                for (ulong s = 1; s <= 20; s++)
                {
                    var g = GenomeFactory.Sample(f, s * 7919);
                    string json = ser.Serialize(g);
                    var res = ser.Deserialize(json);
                    ctx.True(res.Ok, $"{f.Id}/{s}: reload failed: {res.Describe()}");
                    ctx.True(res.Warnings.Count == 0, $"{f.Id}/{s}: unexpected warnings: {res.Describe()}");
                    ctx.True(res.Genome!.ContentEquals(g), $"{f.Id}/{s}: genome changed by save/load");
                    ctx.Equal(g.Name, res.Genome.Name, "name");
                    // second round trip is byte identical
                    ctx.Equal(json, ser.Serialize(res.Genome), $"{f.Id}/{s}: serializer output stable");
                }
            }
        }

        private static void SerializerValidation(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            var f = reg.Families[0];
            var g = GenomeFactory.Sample(f, 5);
            string good = ser.Serialize(g);

            void ExpectError(string json, string contains, string what)
            {
                var r = ser.Deserialize(json);
                ctx.True(!r.Ok, $"{what}: must fail");
                ctx.True(r.Errors.Any(e => e.Contains(contains, StringComparison.OrdinalIgnoreCase)), $"{what}: error should mention '{contains}', got: {r.Describe()}");
            }

            JsonObject Mut(Action<JsonObject> change)
            {
                var o = (JsonObject)JsonNode.Parse(good)!;
                change(o);
                return o;
            }

            ExpectError("{ not json", "Invalid JSON", "broken json");
            ExpectError("[1,2]", "does not contain a JSON object", "array root");
            ExpectError(Mut(o => o["format"] = "something").ToJsonString(), "file format", "wrong format");
            ExpectError(Mut(o => o.Remove("formatVersion")).ToJsonString(), "formatVersion", "missing version");
            ExpectError(Mut(o => o["formatVersion"] = 99).ToJsonString(), "format version 99", "newer format");
            ExpectError(Mut(o => o["generatorVersion"] = 42).ToJsonString(), "generator version 42", "newer generator");
            ExpectError(Mut(o => o["family"] = "dragons-from-mars").ToJsonString(), "Unknown body-plan family", "unknown family");
            ExpectError(Mut(o => o["seeds"]!["color"] = "-5").ToJsonString(), "Seed 'color'", "negative seed");
            ExpectError(Mut(o => o["seeds"]!.AsObject().Remove("motion")).ToJsonString(), "Seed 'motion'", "missing seed");
            ExpectError(Mut(o => o["genes"]!["size"] = "big").ToJsonString(), "Expected a number", "string for float");
            ExpectError(Mut(o => o["genes"]!["pattern.dark"] = 3).ToJsonString(), "Boolean", "number for bool");

            // recoverable problems become warnings
            var w1 = ser.Deserialize(Mut(o => o["genes"]!["size"] = 5.0).ToJsonString());
            ctx.True(w1.Ok && w1.Warnings.Any(x => x.Contains("clamped")), "out of range clamps with warning: " + w1.Describe());
            ctx.Near(1.0, w1.Genome!.Get("size"), 1e-9, "clamped size");
            var w2 = ser.Deserialize(Mut(o => o["genes"]!.AsObject().Remove("mass")).ToJsonString());
            ctx.True(w2.Ok && w2.Warnings.Any(x => x.Contains("'mass'")), "missing gene defaults with warning");
            var w3 = ser.Deserialize(Mut(o => o["genes"]!["legacy.thing"] = 1).ToJsonString());
            ctx.True(w3.Ok && w3.Warnings.Any(x => x.Contains("legacy.thing")), "unknown gene ignored with warning");
            var w4 = ser.Deserialize(Mut(o => o["genes"]!["color.scheme"] = "neon").ToJsonString());
            ctx.True(w4.Ok && w4.Warnings.Any(x => x.Contains("neon")), "unknown option falls back with warning");
        }

        private sealed class TestMigrationV0 : IGenomeMigration
        {
            public int FromVersion => 0;
            public string Description => "Test migration: 'traits' -> 'genes', 'species' -> 'family'";
            public void Apply(JsonObject root)
            {
                if (root["traits"] is JsonNode t) { root.Remove("traits"); root["genes"] = t; }
                if (root["species"] is JsonNode s) { root.Remove("species"); root["family"] = s; }
            }
        }

        private static void MigrationChain(TestContext ctx, CreatureRegistry reg)
        {
            var ser = new GenomeSerializer(reg);
            var f = reg.Families[0];
            var g = GenomeFactory.Sample(f, 77);
            var o = (JsonObject)JsonNode.Parse(ser.Serialize(g))!;
            // build a synthetic legacy (format 0) file
            o["formatVersion"] = 0;
            var genes = o["genes"]!;
            o.Remove("genes");
            o["traits"] = genes;
            var fam = o["family"]!;
            o.Remove("family");
            o["species"] = fam;
            string legacy = o.ToJsonString();
            var noMigration = ser.Deserialize(legacy);
            ctx.True(!noMigration.Ok && noMigration.Errors.Any(e => e.Contains("No migration")), "missing migration must be an explicit error");
            ser.RegisterMigration(new TestMigrationV0());
            var migrated = ser.Deserialize(legacy);
            ctx.True(migrated.Ok, "migrated load: " + migrated.Describe());
            ctx.True(migrated.Migrations.Count == 1, "one migration step recorded");
            ctx.True(migrated.Genome!.ContentEquals(g), "migration preserves the creature");
            // gene alias (renamed gene)
            var ser2 = new GenomeSerializer(reg);
            ser2.RegisterGeneAlias(f.Id, "bodySize", "size");
            var o2 = (JsonObject)JsonNode.Parse(ser2.Serialize(g))!;
            var size = o2["genes"]!["size"]!.DeepClone();
            o2["genes"]!.AsObject().Remove("size");
            o2["genes"]!["bodySize"] = size;
            var aliased = ser2.Deserialize(o2.ToJsonString());
            ctx.True(aliased.Ok && aliased.Genome!.ContentEquals(g), "gene alias migration: " + aliased.Describe());
        }

        // ------------------------------------------------------------------ genetics

        private static void MutationTests(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var parent = GenomeFactory.Sample(f, 1234);
                var settings = new MutationSettings { Strength = 0.5 };
                var a = GenomeOps.Mutate(parent, settings, 55);
                var b = GenomeOps.Mutate(parent, settings, 55);
                ctx.True(a.ContentEquals(b), $"{f.Id}: mutation deterministic");
                ctx.True(!a.ContentEquals(parent), $"{f.Id}: mutation changes something");
                var locks = new GeneLocks();
                locks.SetGroup("palette", true);
                locks.SetGene("size", true);
                var c = GenomeOps.Mutate(parent, new MutationSettings { Strength = 1.0 }, 56, locks);
                foreach (var def in f.Schema.Genes)
                {
                    if (def.Group == "palette" || def.Id == "size")
                        ctx.Equal(parent.Get(def.Id), c.Get(def.Id), $"{f.Id}: locked gene {def.Id}");
                }
                ctx.Equal(parent.ColorSeed, c.ColorSeed, $"{f.Id}: locked colour stream seed");
                var only = GenomeOps.Mutate(parent, new MutationSettings { Strength = 1.0, Anatomy = false, Pattern = false, Motion = false }, 57);
                ctx.Equal(parent.AnatomyKey, only.AnatomyKey, $"{f.Id}: colour-only mutation keeps anatomy");
                // strength 0 is identity for genes
                var zero = GenomeOps.Mutate(parent, new MutationSettings { Strength = 0 }, 58);
                ctx.True(zero.CopyValues().SequenceEqual(parent.CopyValues()), $"{f.Id}: zero strength keeps genes");
            }
        }

        private static void CrossoverTests(TestContext ctx, CreatureRegistry reg)
        {
            var fams = reg.Families;
            var f = fams[0];
            var a = GenomeFactory.Sample(f, 1);
            var b = GenomeFactory.Sample(f, 2);
            var r1 = GenomeOps.Cross(a, b, 9, new MutationSettings { Strength = 0.1 });
            var r2 = GenomeOps.Cross(a, b, 9, new MutationSettings { Strength = 0.1 });
            ctx.True(r1.Child.ContentEquals(r2.Child), "crossover deterministic");
            ctx.True(r1.Provenance.Count > 0, "provenance recorded");
            ctx.Equal("crossover", r1.Child.Lineage.Origin, "lineage origin");
            ctx.Equal(2, r1.Child.Lineage.ParentIds.Count, "two parents recorded");
            // without mutation every linked group comes from A, B or a blend of both
            var pure = GenomeOps.Cross(a, b, 10, new MutationSettings { Strength = 0 });
            foreach (var def in f.Schema.Genes)
            {
                double va = a.Get(def.Id), vb = b.Get(def.Id), vc = pure.Child.Get(def.Id);
                string src = pure.Provenance[def.Linkage];
                if (src == "A") ctx.Equal(va, vc, $"{def.Id} from A");
                else if (src == "B") ctx.Equal(vb, vc, $"{def.Id} from B");
                else if (def.Inheritance == InheritanceMode.Blend && def.Kind == GeneKind.Float)
                    ctx.True(vc >= Math.Min(va, vb) - 1e-6 && vc <= Math.Max(va, vb) + 1e-6, $"{def.Id} blended within parents");
            }
            if (fams.Count > 1)
            {
                var other = GenomeFactory.Sample(fams[1], 3);
                ctx.True(!GenomeOps.CanCross(a, other, out string reason) && reason.Length > 0, "different families are incompatible");
            }
        }

        private static void RegenerateTests(TestContext ctx, CreatureRegistry reg)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 31337);
                var locks = new GeneLocks();
                string lockedGroup = f.Schema.Groups.First(x => x.Stream == GeneStream.Anatomy && x.Id != "body").Id;
                locks.SetGroup(lockedGroup, true);
                locks.SetGroup("palette", true);
                var n = GenomeFactory.RegenerateUnlocked(f, g, locks, 777);
                int changed = 0;
                foreach (var def in f.Schema.Genes)
                {
                    if (locks.IsLocked(def)) ctx.Equal(g.Get(def.Id), n.Get(def.Id), $"{f.Id}: locked {def.Id}");
                    else if (g.Get(def.Id) != n.Get(def.Id)) changed++;
                }
                ctx.True(changed > 3, $"{f.Id}: unlocked genes should change ({changed})");
                ctx.Equal(g.ColorSeed, n.ColorSeed, $"{f.Id}: colour seed kept with locked palette");
            }
        }

        // ------------------------------------------------------------------ anatomy & rendering

        private static void AnatomyValidity(TestContext ctx, CreatureRegistry reg, CreatureFactory factory, int seeds)
        {
            int total = 0, repairs = 0;
            int maxW = 0, maxH = 0;
            foreach (var f in reg.Families)
            {
                for (int i = 0; i < seeds; i++)
                {
                    var g = GenomeFactory.Sample(f, (ulong)(50000 + i * 131));
                    var m = factory.Build(g);
                    var a = m.Anatomy;
                    ctx.True(a.Primitives.Count >= 2, $"{f.Id}#{i}: primitives");
                    ctx.True(a.Skeleton.Count > 2, $"{f.Id}#{i}: bones");
                    foreach (var p in a.Primitives)
                    {
                        ctx.True(p.LocalA.IsFinite && p.LocalB.IsFinite && p.LocalC.IsFinite && p.Radii.IsFinite && DMath.IsFinite(p.RadiusA) && DMath.IsFinite(p.RadiusB), $"{f.Id}#{i}: finite primitive {p.Tag}");
                    }
                    ctx.True(a.Canvas.Width > 8 && a.Canvas.Width <= 320 && a.Canvas.Height > 8 && a.Canvas.Height <= 320, $"{f.Id}#{i}: canvas {a.Canvas}");
                    ctx.True(a.Metrics.BodyLength > 4 && DMath.IsFinite(a.Metrics.WalkSpeed) && a.Metrics.WalkSpeed > 0, $"{f.Id}#{i}: metrics");
                    maxW = Math.Max(maxW, a.Canvas.Width);
                    maxH = Math.Max(maxH, a.Canvas.Height);
                    repairs += a.Repairs.Count;
                    total++;
                }
            }
            ctx.Metric("anatomies", total);
            ctx.Metric("repairs", repairs);
            ctx.Metric("maxCanvasW", maxW);
            ctx.Metric("maxCanvasH", maxH);
        }

        public static ulong RenderHash(CreatureModel m, CreaturePose pose, double yaw)
        {
            var r = new CreatureRenderer();
            var c = m.Anatomy.Canvas;
            var f = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
            r.Render(m.Anatomy, m.Palette, m.Surface, pose, ViewSpec.Default(yaw), f);
            return f.ContentHash();
        }

        /// <summary>
        /// Values checked against the golden table: genome content hash of seed 2024, the rest pose
        /// image and an image after 1.5 s of simulated walking (motion + rendering). Keys: family id,
        /// "id:genome", "id:walk".
        /// </summary>
        public static IEnumerable<(string key, ulong value)> GoldenValues(CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var g = GenomeFactory.Sample(f, 2024);
                var m = factory.Build(g);
                yield return ($"{f.Id}:genome", g.ContentHash);
                yield return (f.Id, RenderHash(m, new CreaturePose(m.Anatomy.Skeleton), PixelCamera.DirectionYaw(7)));
                var motor = new CreatureMotor(m, reg);
                double yaw = PixelCamera.DirectionYaw(7);
                motor.Teleport(Vec3.Zero, yaw);
                motor.FacingOverride = yaw;
                motor.DesiredVelocity = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw)) * m.Motion.WalkSpeed;
                for (int i = 0; i < 90; i++) motor.Step(FixedClock.DefaultStep);
                yield return ($"{f.Id}:walk", RenderHash(m, motor.Pose, motor.Yaw));
            }
        }

        private static void RenderDeterminism(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var m = factory.Build(GenomeFactory.Sample(f, 2024));
                var pose = new CreaturePose(m.Anatomy.Skeleton);
                ulong h1 = RenderHash(m, pose, PixelCamera.DirectionYaw(7));
                ulong h2 = RenderHash(m, pose, PixelCamera.DirectionYaw(7));
                ctx.Equal(h1, h2, $"{f.Id}: identical render twice");
                // a fresh factory (cold caches) produces the same image
                var m2 = new CreatureFactory(reg).Build(GenomeFactory.Sample(f, 2024));
                ctx.Equal(h1, RenderHash(m2, new CreaturePose(m2.Anatomy.Skeleton), PixelCamera.DirectionYaw(7)), $"{f.Id}: cold cache render");
            }
            // golden values (produced on Linux x64): identical results on every platform prove that
            // sampling, motion and rasterization are bit-exact (DMath instead of platform libm)
            var values = GoldenValues(reg, new CreatureFactory(reg)).ToList();
            int checkedCount = 0;
            foreach (var (key, value) in values)
            {
                if (!Golden.Values.TryGetValue(key, out ulong golden)) { ctx.Note($"no golden value for {key}"); continue; }
                ctx.Equal(golden, value, $"golden {key}");
                checkedCount++;
            }
            ctx.Metric("goldenChecked", checkedCount);
            ctx.Note("values: " + string.Join(" ", values.Select(v => $"{v.key}={v.value:x16}")));
        }

        private static CreatureMotor Scenario(CreatureRegistry reg, CreatureModel m, Func<int, double> frameDeltas, double seconds, out long steps)
        {
            var motor = new CreatureMotor(m, reg);
            motor.Teleport(Vec3.Zero, 0);
            var clock = new FixedClock();
            double t = 0;
            int frame = 0;
            while (t < seconds)
            {
                double d = frameDeltas(frame++);
                t += d;
                int n = clock.Advance(d);
                for (int i = 0; i < n; i++)
                {
                    double st = motor.Time;
                    motor.DesiredVelocity = st > 0.3 && st < 1.6 ? new Vec3(m.Motion.WalkSpeed, 0, -m.Motion.WalkSpeed * 0.4) : Vec3.Zero;
                    if (Math.Abs(st - 1.9) < 1e-9 || (st >= 1.9 && st < 1.9 + FixedClock.DefaultStep)) motor.TriggerAction();
                    motor.Step(clock.StepSize);
                }
            }
            steps = motor.StepCount;
            return motor;
        }

        private static ulong PoseHash(CreaturePose p)
        {
            ulong h = 1;
            foreach (var x in p.Skeleton.World)
            {
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.X));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.Y));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Origin.Z));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(x.Basis.C0.X));
            }
            return h;
        }

        private static void MotionFramerateIndependence(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            foreach (var f in reg.Families)
            {
                var m = factory.Build(GenomeFactory.Sample(f, 8080));
                // exactly 3 seconds of simulation fed with 60 fps, 144 fps and irregular frame times
                var a = Scenario(reg, m, i => 1.0 / 60.0, 3.0, out long sa);
                var b = Scenario(reg, m, i => 1.0 / 144.0, 3.0, out long sb);
                var c = Scenario(reg, m, i => (i % 3 == 0 ? 0.031 : 0.007), 3.0, out long sc);
                long steps = Math.Min(sa, Math.Min(sb, sc));
                // run all to the same step count
                void Advance(CreatureMotor motor, long have)
                {
                    for (long k = have; k < steps + 3; k++)
                    {
                        motor.DesiredVelocity = Vec3.Zero;
                        motor.Step(FixedClock.DefaultStep);
                    }
                }
                Advance(a, sa); Advance(b, sb); Advance(c, sc);
                ctx.Equal(a.StepCount, b.StepCount, $"{f.Id}: step count");
                ctx.Equal(PoseHash(a.Pose), PoseHash(b.Pose), $"{f.Id}: 60 vs 144 fps pose");
                ctx.Equal(PoseHash(a.Pose), PoseHash(c.Pose), $"{f.Id}: 60 fps vs irregular frames pose");
            }
        }

        private static void FootSliding(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            double worst = 0;
            int checkedContacts = 0;
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 6; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(300 + s)));
                    if (m.Anatomy.Rig.Locomotion != LocomotionKind.Legged || m.Anatomy.Rig.Legs.Count == 0) continue;
                    var motor = new CreatureMotor(m, reg);
                    motor.Teleport(Vec3.Zero, 0);
                    var prev = new Dictionary<int, Vec3>();
                    for (int i = 0; i < 360; i++)
                    {
                        double t = i / 60.0;
                        // walk, run, turn and stop
                        double speed = t < 2 ? m.Motion.WalkSpeed : (t < 4 ? m.Motion.RunSpeed : 0);
                        double yaw = t < 3 ? 0 : 1.2;
                        motor.DesiredVelocity = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw)) * speed;
                        motor.Step(FixedClock.DefaultStep);
                        var ctxM = motor.Context;
                        foreach (var c in motor.Pose.Contacts)
                        {
                            if (!c.Planted) { prev.Remove(c.Leg); continue; }
                            Vec3 w = ctxM.CreatureToWorld(c.Position);
                            if (prev.TryGetValue(c.Leg, out Vec3 pw))
                            {
                                double slide = new Vec2(w.X - pw.X, w.Z - pw.Z).Length;
                                worst = Math.Max(worst, slide);
                                checkedContacts++;
                            }
                            prev[c.Leg] = w;
                        }
                    }
                }
            }
            ctx.Metric("maxPlantedSlidePx", worst);
            ctx.Metric("contactSamples", checkedContacts);
            ctx.True(worst < 0.05, $"planted feet slid by {worst:0.000} px");
            // A shallow world-space slope must also meet each planted foot when the root moves.
            var slopeModel = factory.Build(GenomeFactory.Sample(reg.GetFamily("quadruped"), 26));
            var slopeMotor = new CreatureMotor(slopeModel, reg);
            slopeMotor.Context.GroundHeightAt = (x, z) => x * 0.04 + z * 0.03;
            slopeMotor.DesiredVelocity = new Vec3(slopeModel.Motion.WalkSpeed, 0, 0);
            double worstHeight = 0;
            int slopeContacts = 0;
            for (int i = 0; i < 180; i++)
            {
                slopeMotor.Step(FixedClock.DefaultStep);
                var context = slopeMotor.Context;
                foreach (var c in slopeMotor.Pose.Contacts.Where(c => c.Planted))
                {
                    var world = context.CreatureToWorld(c.Position);
                    double rootHeight = context.GroundHeightAt(context.Position.X, context.Position.Z);
                    double groundHeight = context.GroundHeightAt(world.X, world.Z);
                    worstHeight = Math.Max(worstHeight, Math.Abs(world.Y + rootHeight - groundHeight));
                    slopeContacts++;
                }
            }
            ctx.Metric("maxSlopeContactErrorPx", worstHeight);
            ctx.True(slopeContacts > 100 && worstHeight < 0.05, $"feet must follow a shallow slope: {worstHeight:0.000} px");
        }

        private static void CanvasSafety(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var renderer = new CreatureRenderer();
            int overflow = 0, frames = 0;
            var problems = new List<string>();
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 8; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(900 + s * 17)));
                    var c = m.Anatomy.Canvas;
                    var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
                    var motor = new CreatureMotor(m, reg);
                    for (int i = 0; i < 60 * 7; i++)
                    {
                        double t = i / 60.0;
                        int dir = (int)(t * 1.5) % 8;
                        motor.FacingOverride = PixelCamera.DirectionYaw(dir);
                        motor.DesiredVelocity = t > 1 && t < 2.5 ? new Vec3(DMath.Cos(dir * 0.785), 0, -DMath.Sin(dir * 0.785)) * m.Motion.RunSpeed : Vec3.Zero;
                        if (i == 60 * 3) motor.TriggerAction();
                        if (i == 60 * 4) motor.TriggerHit(new Vec3(1, 0, 0));
                        motor.Resting = t > 4.6 && t < 6.2;
                        motor.Step(FixedClock.DefaultStep);
                        if (i % 6 != 0) continue;
                        var st = renderer.Render(m.Anatomy, m.Palette, m.Surface, motor.Pose, ViewSpec.Default(motor.Yaw), frame);
                        frames++;
                        if (st.Overflow)
                        {
                            overflow++;
                            if (problems.Count < 6) problems.Add($"{f.Id}/{s} t={t:0.0} L{st.OverflowLeft} R{st.OverflowRight} T{st.OverflowTop} B{st.OverflowBottom}");
                        }
                    }
                }
            }
            ctx.Metric("framesChecked", frames);
            ctx.Metric("overflowFrames", overflow);
            ctx.True(overflow == 0, "parts left the canvas: " + string.Join("; ", problems));
        }

        /// <summary>
        /// Every gene at its extremes (all minimum, all maximum, alternating) and every option of every
        /// choice gene: the creature must build, stay finite, move through all states without leaving
        /// its canvas and render visible pixels. Extremes are legal genomes (the editor allows them).
        /// </summary>
        /// <summary>Variant 0: all genes minimal, 1: all maximal, 2+: alternating; choices cycle through all options.</summary>
        public static CreatureGenome ExtremeGenome(ICreatureFamily f, int variant)
        {
            var schema = f.Schema;
            var baseGenome = GenomeFactory.Sample(f, (ulong)(900 + variant));
            var values = baseGenome.CopyValues();
            for (int gi = 0; gi < schema.Count; gi++)
            {
                var def = schema.Genes[gi];
                bool high = (variant % 3) switch { 0 => false, 1 => true, _ => ((gi + variant) & 1) == 0 };
                switch (def.Kind)
                {
                    case GeneKind.Float:
                    case GeneKind.Int:
                        values[gi] = high ? def.Max : def.Min;
                        break;
                    case GeneKind.Bool:
                        values[gi] = high ? 1 : 0;
                        break;
                    case GeneKind.Choice:
                        values[gi] = (variant + gi) % def.Options.Count;
                        break;
                }
            }
            return baseGenome.WithValues(values).WithName($"Extrem {variant}");
        }

        private static void ExtremeGenes(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var renderer = new CreatureRenderer();
            var problems = new List<string>();
            int cases = 0, frames = 0, maxCanvas = 0;
            foreach (var f in reg.Families)
            {
                var schema = f.Schema;
                int maxOptions = schema.Genes.Where(d => d.Kind == GeneKind.Choice).Select(d => d.Options.Count).DefaultIfEmpty(1).Max();
                int variants = Math.Max(6, maxOptions);
                for (int variant = 0; variant < variants; variant++)
                {
                    var genome = ExtremeGenome(f, variant);
                    string label = $"{f.Id}/v{variant}({genome.GetChoice("form")})";
                    cases++;
                    try
                    {
                        var m = factory.Build(genome);
                        var a = m.Anatomy;
                        bool finite = a.Primitives.All(p => p.LocalA.IsFinite && p.LocalB.IsFinite && p.LocalC.IsFinite && p.Radii.IsFinite && DMath.IsFinite(p.RadiusA) && DMath.IsFinite(p.RadiusB));
                        if (!finite) { problems.Add(label + ": non-finite primitive"); continue; }
                        // all-maximum genomes are allowed to be larger than sampled creatures, but bounded
                        maxCanvas = Math.Max(maxCanvas, Math.Max(a.Canvas.Width, a.Canvas.Height));
                        if (a.Canvas.Width > 400 || a.Canvas.Height > 400) { problems.Add($"{label}: canvas {a.Canvas.Width}x{a.Canvas.Height}"); continue; }
                        var c = a.Canvas;
                        var frame = new PixelFrame(c.Width, c.Height) { OriginX = c.OriginX, OriginY = c.OriginY };
                        var motor = new CreatureMotor(m, reg);
                        double yaw = PixelCamera.DirectionYaw(variant % 8);
                        motor.Teleport(Vec3.Zero, yaw);
                        bool visible = false;
                        string? issue = null;
                        for (int i = 0; i < 60 * 6 && issue == null; i++)
                        {
                            double t = i / 60.0;
                            int dir = (variant + (int)(t * 2)) % 8;
                            double dyaw = PixelCamera.DirectionYaw(dir);
                            motor.FacingOverride = dyaw;
                            double speed = t < 1 ? 0 : (t < 2 ? m.Motion.WalkSpeed : (t < 3 ? m.Motion.RunSpeed : 0));
                            motor.DesiredVelocity = new Vec3(DMath.Cos(dyaw), 0, -DMath.Sin(dyaw)) * speed;
                            if (i == 60 * 3) motor.TriggerAction();
                            if (i == 60 * 4) motor.TriggerHit(new Vec3(1, 0, 0));
                            motor.Resting = t > 4.4 && t < 5.6;
                            motor.Step(FixedClock.DefaultStep);
                            if (i % 10 != 0) continue;
                            var world = motor.Pose.Skeleton.World;
                            for (int b = 0; b < world.Length; b++)
                                if (!world[b].Origin.IsFinite) { issue = $"non-finite bone at t={t:0.00}"; break; }
                            if (issue != null) break;
                            var st = renderer.Render(a, m.Palette, m.Surface, motor.Pose, ViewSpec.Default(motor.Yaw), frame);
                            frames++;
                            if (st.Overflow) issue = $"left the canvas at t={t:0.00} (L{st.OverflowLeft} R{st.OverflowRight} T{st.OverflowTop} B{st.OverflowBottom})";
                            if (frame.ContentBounds(out _, out _, out _, out _)) visible = true;
                        }
                        if (issue == null && !visible) issue = "no visible pixels";
                        if (issue != null) problems.Add(label + ": " + issue);
                    }
                    catch (Exception e)
                    {
                        problems.Add($"{label}: {e.GetType().Name} {e.Message}");
                    }
                }
            }
            ctx.Metric("cases", cases);
            ctx.Metric("framesChecked", frames);
            ctx.Metric("maxCanvas", maxCanvas);
            ctx.True(problems.Count == 0, $"{problems.Count} of {cases} extreme genomes failed: " + string.Join("; ", problems.Take(12)));
        }

        /// <summary>
        /// Facing changes while standing still must turn the whole creature. Trail based bodies
        /// (snakes, worms) cannot pivot, their head has to crawl around and the body must follow.
        /// </summary>
        private static void TurnInPlace(TestContext ctx, CreatureRegistry reg, CreatureFactory factory)
        {
            var problems = new List<string>();
            int creatures = 0, trailBodies = 0;
            Vec3 want = PixelCamera.YawRotation(DMath.Pi).Mul(Vec3.UnitX);
            foreach (var f in reg.Families)
            {
                for (int s = 0; s < 4; s++)
                {
                    var m = factory.Build(GenomeFactory.Sample(f, (ulong)(500 + s * 13)));
                    var rig = m.Anatomy.Rig;
                    var motor = new CreatureMotor(m, reg);
                    motor.Teleport(Vec3.Zero, 0);
                    motor.FacingOverride = 0;
                    for (int i = 0; i < 60; i++) motor.Step(FixedClock.DefaultStep);
                    motor.FacingOverride = DMath.Pi;
                    for (int i = 0; i < 60 * 6; i++) motor.Step(FixedClock.DefaultStep);
                    creatures++;
                    if (Math.Abs(DMath.WrapAngle(motor.Yaw - DMath.Pi)) > 0.05)
                        problems.Add($"{f.Id}/{s}: yaw {motor.Yaw:0.00}");
                    if (rig.LocomotionModule == "serpentine" && rig.Body != null && rig.Body.Bones.Length > 2)
                    {
                        trailBodies++;
                        var yawM = PixelCamera.YawRotation(motor.Yaw);
                        var world = motor.Pose.Skeleton.World;
                        Vec3 head = yawM.Mul(world[rig.Body.Bones[0]].Origin);
                        Vec3 mid = yawM.Mul(world[rig.Body.Bones[rig.Body.Bones.Length / 2]].Origin);
                        Vec3 d = new Vec3(head.X - mid.X, 0, head.Z - mid.Z).NormalizedOr(Vec3.Zero);
                        double dot = Vec3.Dot(d, want);
                        if (dot < 0.6) problems.Add($"{f.Id}/{s}: body not turned (dot {dot:0.00})");
                    }
                }
            }
            ctx.Metric("creatures", creatures);
            ctx.Metric("trailBodies", trailBodies);
            ctx.True(trailBodies > 0, "no trail based body was checked");
            ctx.True(problems.Count == 0, string.Join("; ", problems));
        }
    }

    /// <summary>Golden reference values (produced on Linux x64; must match on every platform).</summary>
    public static class Golden
    {
        public static readonly uint[] Pcg42 = { 1210624746u, 4125136630u, 2074946571u, 3309376910u };
        public static readonly ulong Fnv = 114984965007321940UL;
        public static readonly ulong Derived = 16299427822044037963UL;
        /// <summary>Regenerate with: CoreHarness goldens (after an intended change of generation rules).</summary>
        public static readonly Dictionary<string, ulong> Values = new Dictionary<string, ulong>(StringComparer.Ordinal)
        {
            ["quadruped:genome"] = 0xb347eafec5ed47edUL,
            ["quadruped"] = 0xc24f96a97be5a1f5UL,
            ["quadruped:walk"] = 0x37257c62ce5e5420UL,
            ["biped:genome"] = 0x5ef08293f551f95aUL,
            ["biped"] = 0x9d5f6ba037ea81f1UL,
            ["biped:walk"] = 0x5f13d3534d41b54fUL,
            ["reptile:genome"] = 0x53993ac7a73b23b3UL,
            ["reptile"] = 0x5207d3574a0a7b7bUL,
            ["reptile:walk"] = 0x56604dac7c734731UL,
            ["arthropod:genome"] = 0x17a527c0f16e660eUL,
            ["arthropod"] = 0x653189d69a32ee55UL,
            ["arthropod:walk"] = 0xa4df8330c029a0a1UL,
            ["winged:genome"] = 0x773625d175b173b9UL,
            ["winged"] = 0x1ddf0f2d627a3dc8UL,
            ["winged:walk"] = 0xd1ba207325472d3fUL,
            ["serpent:genome"] = 0xb6bd6dcb29822ee6UL,
            ["serpent"] = 0x76e6faaf990bc548UL,
            ["serpent:walk"] = 0x228b7ca9a57b9021UL,
            ["aquatic:genome"] = 0x44b268859a62d96fUL,
            ["aquatic"] = 0xd8ea4b1e8583f131UL,
            ["aquatic:walk"] = 0x52c411ea737c831eUL,
            ["amorphous:genome"] = 0x96fa889d3f873a1eUL,
            ["amorphous"] = 0x29545e9a6eb3f823UL,
            ["amorphous:walk"] = 0x62f57528386e2cbbUL,
            ["plantfolk:genome"] = 0x5e86703c03aa3251UL,
            ["plantfolk"] = 0x0365c2d8cc0518cbUL,
            ["plantfolk:walk"] = 0xacc0ea74aee3ec28UL,
        };
    }
}
