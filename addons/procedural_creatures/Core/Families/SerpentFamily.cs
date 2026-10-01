// Procedural Pixel Creatures - family 6: serpents and worms (snakes, vipers, cobras, constrictors,
// earthworms, sandworms). Legless bodies that follow the head's trail (serpentine locomotion).

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class SerpentFamily : CreatureFamilyBase
    {
        public override string Id => "serpent";
        public override string DisplayName => "Serpents & Worms";
        public override string Description => "Legless snakes, vipers, cobras, constrictors, earthworms and sandworms. Their bodies follow the head trail, slither or crawl with contraction waves, and coil when resting.";
        public override int Order => 60;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("snake", "Snake", 1.1),
            new ArchetypeInfo("viper", "Viper", 1.0),
            new ArchetypeInfo("cobra", "Cobra", 0.9),
            new ArchetypeInfo("constrictor", "Constrictor", 0.9),
            new ArchetypeInfo("worm", "Worm", 0.9),
            new ArchetypeInfo("sandworm", "Sandworm", 0.8),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("snake", "Snake"), ("viper", "Viper"), ("cobra", "Cobra"), ("constrictor", "Constrictor"), ("worm", "Worm"), ("sandworm", "Sandworm"),
        };
        public static readonly (string, string)[] Gaits = { ("lateral", "Lateral"), ("rectilinear", "Rectilinear") };
        public static readonly (string, string)[] HeadShapes = { ("round", "Round"), ("triangular", "Triangular"), ("blunt", "Blunt"), ("maw", "Maw") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs"), ("ring", "Ring") };
        public static readonly (string, string)[] EyeStyles = { ("slit", "Slit pupils"), ("round", "Round"), ("bead", "Beady"), ("glow", "Glowing"), ("none", "None") };
        public static readonly (string, string)[] HornKinds = { ("none", "None"), ("brow", "Brow"), ("nasal", "Nasal") };
        public static readonly (string, string)[] TailTips = { ("plain", "Plain"), ("rattle", "Rattle"), ("spike", "Spike"), ("fin", "Fin"), ("blunt", "Blunt") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "snake", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("body.thickness", "Body thickness", 0, 1, 0.45, "", 0.1)
             .Float("body.taper", "Body taper", 0, 1, 0.55, "How sharply the tail tapers.", 0.1)
             .Float("body.segments", "Body segments", 0, 1, 0.1, "Visible body rings in worms.", 0.1)
             .Choice("body.gait", "Body gait", Gaits, "lateral", "Slithering for snakes or crawling with contraction waves for worms.", 0.05)
             .Float("body.raise", "Body raise", 0, 1, 0.2, "Height of the front of the body.", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Choice("head.shape", "Head shape", HeadShapes, "round", "", 0.08)
             .Float("head.jaw", "Head jaw", 0, 1, 0.5, "", 0.1)
             .Choice("head.teeth", "Head teeth", TeethKinds, "small", "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.5, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "slit", "", 0.08)
             .Float("head.hood", "Head hood", 0, 1, 0.0, "Cobra hood.", 0.1)
             .Choice("head.horns", "Head horns", HornKinds, "none", "", 0.1)
             .Float("head.hornSize", "Head horn size", 0, 1, 0.5, "", 0.1)
             .Bool("head.tongue", "Head tongue", true, "Tongue flicking while idle.", 0.05);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Choice("tail.tip", "Tail tip", TailTips, "plain", "", 0.1)
             .Float("back.spikes", "Back spikes", 0, 1, 0.0, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "scales");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Thick, Taper, Segments, Raise, HeadSize, Jaw, EyeSize, Hood, HornSize, Spikes;
            public string Gait = "lateral";
            public bool Tongue = true;
            public (string, double)[] Shape = { ("round", 1) }, Teeth = { ("small", 1) }, Eyes = { ("slit", 1) }, Horns = { ("none", 1) }, Tips = { ("plain", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "viper" => new Prior
            {
                Size = 0.45, Thick = 0.55, Taper = 0.75, Segments = 0.0, Raise = 0.2, HeadSize = 0.7, Jaw = 0.65, EyeSize = 0.45, Hood = 0, HornSize = 0.4, Spikes = 0,
                Shape = new[] { ("triangular", 4.0) }, Teeth = new[] { ("fangs", 4.0) }, Eyes = new[] { ("slit", 4.0), ("glow", 0.4) },
                Horns = new[] { ("none", 2.0), ("brow", 1.0), ("nasal", 0.6) }, Tips = new[] { ("plain", 2.0), ("rattle", 1.5) },
            },
            "cobra" => new Prior
            {
                Size = 0.55, Thick = 0.45, Taper = 0.65, Segments = 0.0, Raise = 0.9, HeadSize = 0.5, Jaw = 0.5, EyeSize = 0.45, Hood = 0.8, HornSize = 0.3, Spikes = 0,
                Shape = new[] { ("round", 3.0), ("triangular", 1.0) }, Teeth = new[] { ("fangs", 3.0) }, Eyes = new[] { ("round", 2.0), ("slit", 1.0), ("glow", 0.3) },
            },
            "constrictor" => new Prior
            {
                Size = 0.8, Thick = 0.85, Taper = 0.5, Segments = 0.0, Raise = 0.1, HeadSize = 0.4, Jaw = 0.55, EyeSize = 0.35, Hood = 0, HornSize = 0.3, Spikes = 0.05,
                Shape = new[] { ("blunt", 2.0), ("round", 1.0) }, Teeth = new[] { ("small", 3.0) }, Eyes = new[] { ("slit", 2.0), ("round", 1.0) },
            },
            "worm" => new Prior
            {
                Size = 0.35, Thick = 0.55, Taper = 0.15, Segments = 0.8, Raise = 0.25, HeadSize = 0.35, Jaw = 0.3, EyeSize = 0.3, Hood = 0, HornSize = 0.3, Spikes = 0,
                Gait = "rectilinear", Tongue = false,
                Shape = new[] { ("blunt", 4.0) }, Teeth = new[] { ("none", 3.0) }, Eyes = new[] { ("none", 3.0), ("bead", 1.0) }, Tips = new[] { ("blunt", 4.0) },
            },
            "sandworm" => new Prior
            {
                Size = 0.75, Thick = 0.8, Taper = 0.35, Segments = 0.65, Raise = 0.45, HeadSize = 0.6, Jaw = 0.85, EyeSize = 0.3, Hood = 0, HornSize = 0.3, Spikes = 0.45,
                Gait = "rectilinear", Tongue = false,
                Shape = new[] { ("maw", 4.0) }, Teeth = new[] { ("ring", 4.0) }, Eyes = new[] { ("none", 2.0), ("glow", 1.0), ("bead", 0.6) }, Tips = new[] { ("blunt", 2.0), ("spike", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.45, Thick = 0.35, Taper = 0.7, Segments = 0.0, Raise = 0.25, HeadSize = 0.45, Jaw = 0.5, EyeSize = 0.55, Hood = 0, HornSize = 0.3, Spikes = 0,
                Shape = new[] { ("round", 3.0), ("triangular", 1.0) }, Teeth = new[] { ("small", 3.0), ("fangs", 1.0) }, Eyes = new[] { ("round", 2.0), ("slit", 2.0), ("bead", 1.0) },
                Tips = new[] { ("plain", 4.0), ("fin", 0.4) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "body.thickness", p.Thick, sp);
            Around(v, rng, "body.taper", p.Taper, sp);
            Around(v, rng, "body.segments", p.Segments, sp);
            SetChoice(v, "body.gait", p.Gait);
            Around(v, rng, "body.raise", p.Raise, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            SetChoice(v, "head.shape", PickWeighted(rng, p.Shape));
            Around(v, rng, "head.jaw", p.Jaw, sp);
            SetChoice(v, "head.teeth", PickWeighted(rng, p.Teeth));
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            Around(v, rng, "head.hood", p.Hood, sp * 0.6);
            SetChoice(v, "head.horns", PickWeighted(rng, p.Horns));
            Around(v, rng, "head.hornSize", p.HornSize, sp);
            Set(v, "head.tongue", p.Tongue ? 1 : 0);
            SetChoice(v, "tail.tip", PickWeighted(rng, p.Tips));
            Around(v, rng, "back.spikes", p.Spikes, sp * 0.6);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["scales"] = 6 },
                Patterns = new Dictionary<string, double> { ["bands"] = 1.5, ["blotches"] = 1, ["dorsal"] = 1, ["spots"] = 1, ["none"] = 1, ["saddle"] = 0.6, ["mottled"] = 0.8 },
                Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 2 },
                // grass snake green, olive, brown, black racer, grey, yellow, coral red, blue racer
                Coats = new[] { Coats.Green(1.6), Coats.Olive(1.2), Coats.Brown(1.2), Coats.Black(0.8), Coats.Grey(0.8), Coats.Yellow(0.4), Coats.Crimson(0.4), Coats.Blue(0.3) },
                CountershadeMin = 0.45, CountershadeMax = 0.85,
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 2, ["complementary"] = 1, ["monochrome"] = 1 },
                SocksChance = 0.0, MaskChance = 0.06, PiebaldChance = 0.02,
            };
            switch (arch)
            {
                case "viper":
                    h.Patterns = new Dictionary<string, double> { ["blotches"] = 2, ["saddle"] = 1, ["bands"] = 1 };
                    h.Coats = new[] { Coats.Sand(1.5), Coats.Brown(1.5), Coats.Olive(1), Coats.Grey(0.8), Coats.Rust(0.6), Coats.Green(0.6) };
                    break;
                case "cobra":
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["bands"] = 1.5 };
                    h.Coats = new[] { Coats.Black(1.4), Coats.Brown(1.2), Coats.Sand(1.2), Coats.Golden(0.8), Coats.Olive(0.6) };
                    break;
                case "constrictor":
                    h.Patterns = new Dictionary<string, double> { ["blotches"] = 3, ["saddle"] = 1, ["mottled"] = 1 };
                    h.Coats = new[] { Coats.Olive(1.4), Coats.Brown(1.4), Coats.Tan(1), Coats.Green(1), Coats.Chocolate(0.6), Coats.Yellow(0.6), Coats.Grey(0.5) };
                    h.WeightMin = 0.55; h.EnergyMax = 0.5;
                    break;
                case "worm":
                    h.Materials = new Dictionary<string, double> { ["slime"] = 3, ["hide"] = 2 };
                    h.Coats = new[] { Coats.Pink(2), Coats.Brown(1), Coats.Crimson(0.8), Coats.Sand(0.6), Coats.Violet(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["bands"] = 1 };
                    h.CountershadeMin = 0.2; h.CountershadeMax = 0.5;
                    h.MaskChance = 0.0;
                    break;
                case "sandworm":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 3, ["stone"] = 1, ["chitin"] = 1 };
                    h.Coats = new[] { Coats.Sand(2), Coats.Tan(1.2), Coats.Brown(1), Coats.Rust(0.8), Coats.Grey(0.5), Coats.Muted(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["bands"] = 2, ["none"] = 2, ["mottled"] = 1 };
                    h.MaskChance = 0.0;
                    h.WeightMin = 0.6; h.EnergyMax = 0.5;
                    h.GlowChance = 0.25;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            if (Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            double size = g.Get("size");
            double L = DMath.Lerp(30, 92, DMath.Pow(size, 1.0));
            double R = L * DMath.Lerp(0.035, 0.085, g.Get("body.thickness"));
            R = Math.Max(1.4, R);
            double taper = g.Get("body.taper");
            string shape = g.GetChoice("head.shape");
            bool maw = shape == "maw";
            double segGene = g.Get("body.segments");
            bool rect = g.GetChoice("body.gait") == "rectilinear";

            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            b.Rig.Root = root;
            int n = DMath.Clamp((int)Math.Round(L / Math.Max(2.2, R * 1.1)), 10, 26);
            double seg = L / n;
            var bones = new int[n + 1];
            var radii = new double[n + 1];
            var lengths = new double[n];
            int parent = root;
            for (int i = 0; i <= n; i++)
            {
                double t = i / (double)n;
                // slender neck behind the head (snakes; worms stay cylindrical), full in the middle, tapering tail
                double neck = DMath.SmoothStep(0.0, 0.16, t);
                double neckMin = form == "worm" || form == "sandworm" ? 0.8 : 0.64;
                double tail = 1.0 - DMath.SmoothStep(DMath.Lerp(0.8, 0.4, taper), 1.0, t) * DMath.Lerp(0.35, 0.9, taper);
                radii[i] = Math.Max(0.6, R * DMath.Lerp(neckMin, 1.0, neck) * tail);
                bones[i] = b.BoneAlong($"body.{i}", parent, new Vec3(-i * seg, radii[i], 0), new Vec3(-1, 0, 0), Vec3.UnitY, BoneRole.Segment, 0, i < n ? seg : 0);
                parent = bones[i];
                if (i < n) lengths[i] = seg;
            }
            int body = b.Group("body", DMath.Clamp(R * 0.45, 1.0, 3.0), 0, segGene > 0.4 ? GroupFlags.CreaseShade : GroupFlags.None);
            for (int i = 0; i < n; i++)
            {
                var p = b.Segment(body, bones[i], radii[i], bones[i + 1], radii[i + 1], MaterialSlot.Primary, PatternDomain.Body, i / (double)n, (i + 1) / (double)n);
                p.DomainLength = L;
                p.Tag = "body";
                if (segGene > 0.35 && (i % 2 == 0))
                {
                    // segment rings: slight bulges that show as creases
                    var ring = b.Ellipsoid(body, bones[i], new Vec3(-seg * 0.5, 0, 0), new Vec3(seg * 0.42, radii[i] * DMath.Lerp(1.02, 1.12, segGene), radii[i] * DMath.Lerp(1.02, 1.12, segGene)),
                        MaterialSlot.Primary, PatternDomain.Body, i / (double)n, (i + 1) / (double)n);
                    ring.DomainLength = L;
                    ring.Tag = "ring";
                }
            }
            if (rect && form == "worm")
            {
                // clitellum: a lighter saddle band a third along the body
                int ci = Math.Max(1, n / 4);
                var cl = b.Ellipsoid(body, bones[ci], new Vec3(-seg * 0.5, 0, 0), new Vec3(seg * 1.4, radii[ci] * 1.1, radii[ci] * 1.1), MaterialSlot.Secondary, PatternDomain.None);
                cl.Flags |= PrimFlags.NoPattern;
            }
            b.Rig.Body = new ChainRig { Name = "body", Bones = bones, Lengths = lengths, TotalLength = L, Radii = radii, Stiffness = 0.5 };

            // ---- head
            double headSize = g.Get("head.size");
            double skullL = Math.Max(3.2, R * DMath.Lerp(2.2, 3.6, headSize) * (maw ? 1.1 : 1.0));
            string teeth = g.GetChoice("head.teeth");
            string eye = g.GetChoice("head.eyeStyle");
            var hs = new HeadSpec
            {
                ParentBone = bones[0],
                Position = new Vec3(R * 0.3, radii[0], 0),
                Forward = Vec3.UnitX,
                SkullLength = skullL,
                SkullHeight = skullL * (shape == "triangular" ? 0.55 : (maw ? 0.9 : 0.62)),
                SkullWidth = skullL * (shape == "triangular" ? 0.95 : (maw ? 0.95 : 0.7)),
                SkullOffset = maw ? 0.2 : 0.3,
                SnoutLength = maw ? 0.2 : skullL * (shape == "blunt" ? 0.25 : 0.55),
                SnoutRadius = skullL * (shape == "triangular" ? 0.2 : 0.24),
                SnoutTaper = shape == "blunt" ? 0.95 : 0.7,
                SnoutDrop = 0.02,
                SnoutHeight = -0.05,
                SnoutWidth = shape == "triangular" ? 1.25 : 1.0,
                NoseSize = 0,
                JawLength = maw ? 0 : DMath.Lerp(0.8, 1.0, g.Get("head.jaw")),
                JawRadius = skullL * 0.16,
                JawDepth = 0.6,
                Brow = shape == "triangular" ? 0.4 : 0.1,
                EyeSize = eye == "none" ? 0 : DMath.Clamp(DMath.Lerp(1.2, 3.0, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 7.0), 1.2, 4.0),
                EyeCount = eye == "none" ? 0 : 2,
                EyeStyle = QuadrupedFamily.ParseEye(eye),
                EyeForward = 0.35,
                EyeHeight = 0.5,
                EyeAspect = eye == "slit" ? 1.2 : 1.0,
                Teeth = teeth switch { "fangs" => TeethKind.Fangs, "small" => TeethKind.Small, _ => TeethKind.None },
                TeethSize = DMath.Clamp(skullL / 7.0, 0.8, 1.6),
                // snakes: the head is its own part with a crease towards the neck; worm heads melt into the body
                Group = form == "worm" || maw ? body : -1,
                BlendRadius = DMath.Clamp(R * 0.3, 0.8, 1.8),
                DomainLength = L,
                JawOpenMax = form == "viper" || form == "cobra" ? 1.1 : 0.7,
                Nostrils = false,
            };
            var head = Heads.Build(b, hs);
            if (maw) BuildMaw(b, head, skullL, hs.SkullHeight, teeth == "ring", DMath.Lerp(0.8, 1.2, g.Get("head.jaw")));

            // forked tongue on its own bone (retracted inside the head, flicks out along +X)
            if (g.GetOr("head.tongue", 1) >= 0.5 && !maw)
            {
                double tl = Math.Max(2.5, skullL * 0.7);
                int tongue = b.Bone("tongue", head.Head, head.SnoutTipLocal + new Vec3(-tl - 0.5, -hs.SnoutRadius * 0.45, 0), Quat.Identity, tl, BoneRole.Other);
                int tg = b.Group("tongue", 0.3, 0, GroupFlags.None, -0.8);
                var t0 = b.Cone(tg, tongue, Vec3.Zero, 0.45, tongue, new Vec3(tl * 0.75, 0, 0), 0.4, MaterialSlot.Mouth);
                var t1 = b.Cone(tg, tongue, new Vec3(tl * 0.75, 0, 0), 0.4, tongue, new Vec3(tl, 0.2, 0.55), 0.3, MaterialSlot.Mouth);
                var t2 = b.Cone(tg, tongue, new Vec3(tl * 0.75, 0, 0), 0.4, tongue, new Vec3(tl, 0.2, -0.55), 0.3, MaterialSlot.Mouth);
                foreach (var t in new[] { t0, t1, t2 }) { t.Flags |= PrimFlags.Thin | PrimFlags.NoShadow | PrimFlags.NoPattern | PrimFlags.Hard; t.ShadeBias = 1; }
                b.Rig.Tongue = tongue;
                b.Rig.TongueLength = tl + 0.5;
            }

            // cobra hood: two flattened wings behind the head on the first body segments
            double hood = g.Get("head.hood");
            if (hood > 0.25)
            {
                int hg = b.Group("hood", 1.0, 0, GroupFlags.None, -0.3);
                int hb = bones[Math.Min(2, n)];
                double hr = R * DMath.Lerp(1.6, 2.8, hood);
                var e = b.Ellipsoid(hg, hb, new Vec3(-seg * 0.2, radii[2] * 0.2, 0), new Vec3(seg * 2.2, radii[2] * 0.55, hr), MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
                e.DomainLength = L;
                e.Tag = "hood";
                b.Trait("hood");
            }

            string horns = g.GetChoice("head.horns");
            if (horns != "none")
            {
                Horns.Build(b, new HornSpec
                {
                    Kind = horns == "nasal" ? HornKind.Nasal : HornKind.Short,
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(skullL * 0.05, hs.SkullHeight * 0.4, hs.SkullWidth * 0.3),
                    Front = head.SnoutTipLocal + new Vec3(-hs.SnoutLength * 0.4, hs.SnoutRadius * 0.7, 0),
                    Size = skullL * DMath.Lerp(0.3, 0.8, g.Get("head.hornSize")),
                });
            }

            // ---- tail tip
            string tip = g.GetChoice("tail.tip");
            int last = bones[n];
            double tr = radii[n];
            switch (tip)
            {
                case "rattle":
                    for (int i = 0; i < 3; i++)
                    {
                        var r = b.Ellipsoid(body, last, new Vec3(0.3 + i * 1.3 * Math.Max(0.8, tr), 0, 0), new Vec3(Math.Max(0.7, tr) * 0.8, Math.Max(0.8, tr) * (1.2 - i * 0.12), Math.Max(0.7, tr)), MaterialSlot.Accent);
                        r.Flags |= PrimFlags.NoPattern;
                    }
                    b.Trait("rattle");
                    break;
                case "spike":
                {
                    var s = b.Cone(body, last, Vec3.Zero, Math.Max(0.7, tr), last, new Vec3(Math.Max(2.5, R * 1.6), 0, 0), 0.3, MaterialSlot.Accent);
                    s.Flags |= PrimFlags.NoPattern;
                    break;
                }
                case "fin":
                {
                    int fg = b.Group("tailfin", 0.4, 0, GroupFlags.NoFarShade, -0.2);
                    double fl = Math.Max(3, R * 2.5);
                    var t1 = b.Triangle(fg, bones[n - 2], new Vec3(0, radii[n - 2] * 0.6, 0), last, new Vec3(fl * 0.6, fl * 0.55, 0), last, new Vec3(fl, 0, 0), MaterialSlot.Membrane, PatternDomain.Fin);
                    var t2 = b.Triangle(fg, bones[n - 2], new Vec3(0, -radii[n - 2] * 0.6, 0), last, new Vec3(fl * 0.6, -fl * 0.55, 0), last, new Vec3(fl, 0, 0), MaterialSlot.Membrane, PatternDomain.Fin);
                    t1.Flags |= PrimFlags.Translucent; t2.Flags |= PrimFlags.Translucent;
                    break;
                }
            }

            // ---- back spikes
            double spikes = g.Get("back.spikes");
            if (spikes > 0.2)
            {
                var ds = new DorsalSpec { Kind = DorsalKind.Spikes, Size = R * DMath.Lerp(0.8, 1.8, spikes), Material = MaterialSlot.Accent };
                for (int i = 2; i < n - 1; i += 2)
                {
                    Vec3 pos = new Vec3(-i * seg, radii[i] * 2 - 0.2, 0);
                    ds.Anchors.Add((bones[i], pos, Vec3.UnitY, DMath.Lerp(1.0, 0.6, i / (double)n)));
                }
                Dorsal.Build(b, ds);
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Serpentine;
            b.Rig.LocomotionModule = "serpentine";
            b.Rig.Tuning["serpent.style"] = rect ? SerpentineLocomotion.Rectilinear : SerpentineLocomotion.Lateral;
            b.Rig.Tuning["serpent.raise"] = R * DMath.Lerp(0.3, 4.5, g.Get("body.raise"));
            b.Rig.Tuning["serpent.raiseLength"] = L * DMath.Lerp(0.12, 0.3, g.Get("body.raise"));
            // cobras rear up when idle; sandworms tower out of the ground
            double rear = DMath.Saturate((g.Get("body.raise") - 0.55) / 0.3) + (form == "sandworm" ? 0.9 : 0);
            b.Rig.Tuning["serpent.rear"] = DMath.Saturate(rear);
            b.Rig.Tuning["serpent.wavelength"] = L * DMath.Lerp(0.45, 0.62, g.Get("body.thickness"));
            b.Rig.Tuning["serpent.amplitude"] = L * DMath.Lerp(0.1, 0.065, g.Get("body.thickness"));
            b.Rig.Action = maw ? ActionStyle.Slam : ActionStyle.Bite;
            b.Rig.Rest = RestStyle.Coil;
            b.Metrics.LegLength = Math.Max(3, R * 3);
            b.Metrics.HipHeight = R;
            b.Metrics.BackHeight = R * 2;
            b.Metrics.BodyLength = L * 0.55;
            b.Metrics.Mass = DMath.Pow(L / 60.0, 2) * DMath.Lerp(0.5, 1.5, g.Get("body.thickness"));
            double spd = DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * (rect ? 0.55 : 1.0);
            b.Metrics.WalkSpeed = Math.Sqrt(0.5 * 400 * R * 3) * spd;
            b.Metrics.RunSpeed = Math.Sqrt(2.2 * 400 * R * 3) * spd;
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(R / 3.0), 0.7, 1.4);
            b.AnimationMargin = Math.Max(6, R * 2.5);
            double rearH = b.Rig.Tune("serpent.rear", 0) * Math.Max(4, b.Rig.Tune("serpent.raiseLength", 4) * 1.6) * 0.8;
            b.ExtraHeadroom = Math.Max(4, Math.Max(b.Rig.Tune("serpent.raise", 2), rearH) + skullL);
            return b.Build(g.AnatomyKey);
        }

        /// <summary>Round lamprey-like maw at the front of the head, lined with a ring of teeth.</summary>
        private static void BuildMaw(AnatomyBuilder b, HeadResult head, double skullL, double skullH, bool teethRing, double jaw)
        {
            Vec3 c = head.SkullCenterLocal + new Vec3(skullL * 0.46, 0, 0);
            double r = skullH * 0.36 * jaw;
            int mg = b.Group("maw", 0.5, 0, GroupFlags.NoOutline | GroupFlags.NoContour, 0.2);
            var mouth = b.Ellipsoid(mg, head.Head, c, new Vec3(Math.Max(0.6, r * 0.35), r, r), MaterialSlot.Mouth);
            mouth.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern | PrimFlags.FrontOnly;
            mouth.ShadeBias = -1;
            if (!teethRing) return;
            int tg = b.Group("mawteeth", 0.3, 0, GroupFlags.NoContour, 0.4);
            int count = 8;
            for (int i = 0; i < count; i++)
            {
                double a = i / (double)count * DMath.TwoPi;
                Vec3 rim = c + new Vec3(r * 0.25, DMath.Cos(a) * r * 1.05, DMath.Sin(a) * r * 1.05);
                Vec3 tip = c + new Vec3(r * 0.35, DMath.Cos(a) * r * 0.45, DMath.Sin(a) * r * 0.45);
                var t = b.Cone(tg, head.Head, rim, Math.Max(0.45, r * 0.18), head.Head, tip, 0.3, MaterialSlot.Teeth);
                t.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern | PrimFlags.Thin | PrimFlags.Hard;
            }
        }
    }
}
