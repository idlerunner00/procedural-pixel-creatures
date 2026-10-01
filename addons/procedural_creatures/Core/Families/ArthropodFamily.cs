// Procedural Pixel Creatures - family 4: arthropods (spiders, beetles, ants, scorpions, crabs,
// mantises). Segmented bodies (head, thorax, abdomen) on four to eight jointed legs whose knees
// rise above the body, optional pincers or raptorial blades, stinger tails and antennae.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class ArthropodFamily : CreatureFamilyBase
    {
        public override string Id => "arthropod";
        public override string DisplayName => "Arthropods";
        public override string Description => "Spiders, beetles, ants, scorpions, crabs and mantises: segmented chitin bodies, four to eight jointed legs, pincers or grasping arms, stinger tails and antennae.";
        public override int Order => 40;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("spider", "Spider", 1.2),
            new ArchetypeInfo("beetle", "Beetle", 1.1),
            new ArchetypeInfo("ant", "Ant", 0.9),
            new ArchetypeInfo("scorpion", "Scorpion", 1.0),
            new ArchetypeInfo("crab", "Crab", 0.9),
            new ArchetypeInfo("mantis", "Mantis", 0.8),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("spider", "Spider"), ("beetle", "Beetle"), ("ant", "Ant"), ("scorpion", "Scorpion"), ("crab", "Crab"), ("mantis", "Mantis"),
        };
        public static readonly (string, string)[] LegCounts = { ("4", "Four"), ("6", "Six"), ("8", "Eight") };
        public static readonly (string, string)[] ArmKinds = { ("none", "None"), ("pincers", "Pincers"), ("blades", "Blades") };
        public static readonly (string, string)[] AbdomenShapes = { ("round", "Round"), ("oval", "Oval"), ("long", "Long"), ("flat", "Flat") };
        public static readonly (string, string)[] EyeKinds = { ("compound", "Compound"), ("cluster", "Cluster"), ("stalks", "Stalks"), ("bead", "Beady") };
        public static readonly (string, string)[] MouthKinds = { ("none", "None"), ("mandibles", "Mandibles"), ("fangs", "Fangs") };
        public static readonly (string, string)[] AntennaKinds = { ("none", "None"), ("short", "Short"), ("long", "Long"), ("clubbed", "Clubbed") };
        public static readonly (string, string)[] TailKinds = { ("none", "None"), ("stinger", "Stinger"), ("cerci", "Cerci") };
        public static readonly (string, string)[] HornKinds = { ("none", "None"), ("rhino", "Rhino"), ("stag", "Stag") };
        public static readonly (string, string)[] ShellKinds = { ("none", "None"), ("elytra", "Elytra"), ("carapace", "Carapace"), ("spiky", "Spiky") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "spider", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("thorax.size", "Thorax size", 0, 1, 0.5, "", 0.1)
             .Float("thorax.length", "Thorax length", 0, 1, 0.3, "Elongated prothorax, as in mantises.", 0.1)
             .Float("abdomen.size", "Abdomen size", 0, 1, 0.6, "", 0.1)
             .Choice("abdomen.shape", "Abdomen shape", AbdomenShapes, "round", "", 0.1)
             .Float("abdomen.lift", "Abdomen lift", 0, 1, 0.3, "", 0.1)
             .Float("waist", "Waist", 0, 1, 0.3, "Narrow waist between thorax and abdomen.", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.45, "", 0.1)
             .Choice("head.eyes", "Head eyes", EyeKinds, "cluster", "", 0.08)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.5, "", 0.1)
             .Choice("head.mouth", "Head mouth", MouthKinds, "fangs", "", 0.1)
             .Float("head.mouthSize", "Head mouth size", 0, 1, 0.5, "", 0.1)
             .Choice("head.antennae", "Head antennae", AntennaKinds, "none", "", 0.1)
             .Float("head.antennaLength", "Head antenna length", 0, 1, 0.5, "", 0.1)
             .Choice("head.horn", "Head horn", HornKinds, "none", "", 0.1)
             .Float("head.hornSize", "Head horn size", 0, 1, 0.5, "", 0.1);
            b.Group("legs", "Legs", GeneStream.Anatomy)
             .Choice("legs.count", "Legs count", LegCounts, "8", "", 0.05)
             .Float("legs.length", "Legs length", 0, 1, 0.55, "", 0.1)
             .Float("legs.thickness", "Legs thickness", 0, 1, 0.4, "", 0.1)
             .Float("legs.spread", "Legs spread", 0, 1, 0.55, "", 0.1)
             .Float("legs.height", "Legs height", 0, 1, 0.45, "Height of the body above the ground.", 0.1);
            b.Group("arms", "Arms", GeneStream.Anatomy)
             .Choice("arms.kind", "Arms kind", ArmKinds, "none", "", 0.08)
             .Float("arms.size", "Arms size", 0, 1, 0.5, "", 0.1)
             .Float("arms.length", "Arms length", 0, 1, 0.5, "", 0.1);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Choice("tail.kind", "Tail kind", TailKinds, "none", "", 0.08)
             .Float("tail.length", "Tail length", 0, 1, 0.6, "", 0.1)
             .Choice("shell.kind", "Shell kind", ShellKinds, "none", "", 0.1)
             .Float("shell.size", "Shell size", 0, 1, 0.5, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "chitin");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, ThSize, ThLen, AbSize, AbLift, Waist, HeadSize, EyeSize, MouthSize, AntLen, HornSize;
            public double LegLen, LegTh, Spread, Height, ArmSize, ArmLen, TailLen, ShellSize;
            public string Legs = "8";
            public (string, double)[] Ab = { ("round", 1) }, Eyes = { ("cluster", 1) }, Mouth = { ("fangs", 1) }, Ant = { ("none", 1) };
            public (string, double)[] Horn = { ("none", 1) }, Arms = { ("none", 1) }, Tail = { ("none", 1) }, Shell = { ("none", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "beetle" => new Prior
            {
                Size = 0.5, ThSize = 0.55, ThLen = 0.2, AbSize = 0.75, AbLift = 0.1, Waist = 0.15, HeadSize = 0.4, EyeSize = 0.35, MouthSize = 0.5, AntLen = 0.4, HornSize = 0.6,
                // short, stout legs tucked under the shell (long thin legs read as spiders)
                LegLen = 0.18, LegTh = 0.65, Spread = 0.36, Height = 0.25, ArmSize = 0.5, ArmLen = 0.5, TailLen = 0.4, ShellSize = 0.7, Legs = "6",
                Ab = new[] { ("oval", 3.0), ("round", 1.0) }, Eyes = new[] { ("compound", 2.0), ("bead", 2.0) }, Mouth = new[] { ("mandibles", 3.0) },
                Ant = new[] { ("clubbed", 2.0), ("short", 2.0) }, Horn = new[] { ("rhino", 2.0), ("stag", 1.5), ("none", 1.5) }, Shell = new[] { ("elytra", 4.0) },
            },
            "ant" => new Prior
            {
                Size = 0.45, ThSize = 0.3, ThLen = 0.3, AbSize = 0.55, AbLift = 0.2, Waist = 0.95, HeadSize = 0.82, EyeSize = 0.4, MouthSize = 0.7, AntLen = 0.65, HornSize = 0.3,
                LegLen = 0.45, LegTh = 0.15, Spread = 0.4, Height = 0.65, ArmSize = 0.5, ArmLen = 0.5, TailLen = 0.4, ShellSize = 0.4, Legs = "6",
                Ab = new[] { ("oval", 3.0), ("round", 1.0) }, Eyes = new[] { ("compound", 3.0) }, Mouth = new[] { ("mandibles", 4.0) },
                Ant = new[] { ("long", 3.0), ("clubbed", 1.0) }, Tail = new[] { ("none", 1.0) },
            },
            "scorpion" => new Prior
            {
                Size = 0.5, ThSize = 0.5, ThLen = 0.3, AbSize = 0.55, AbLift = 0.0, Waist = 0.1, HeadSize = 0.3, EyeSize = 0.3, MouthSize = 0.4, AntLen = 0.3, HornSize = 0.3,
                LegLen = 0.45, LegTh = 0.4, Spread = 0.55, Height = 0.3, ArmSize = 0.7, ArmLen = 0.6, TailLen = 0.75, ShellSize = 0.5, Legs = "8",
                Ab = new[] { ("long", 3.0), ("flat", 1.0) }, Eyes = new[] { ("bead", 3.0), ("cluster", 1.0) }, Mouth = new[] { ("fangs", 2.0), ("none", 1.0) },
                Arms = new[] { ("pincers", 1.0) }, Tail = new[] { ("stinger", 1.0) }, Shell = new[] { ("carapace", 2.0), ("none", 1.0), ("spiky", 0.5) },
            },
            "crab" => new Prior
            {
                Size = 0.45, ThSize = 0.8, ThLen = 0.1, AbSize = 0.2, AbLift = 0.0, Waist = 0.0, HeadSize = 0.25, EyeSize = 0.45, MouthSize = 0.4, AntLen = 0.3, HornSize = 0.3,
                LegLen = 0.45, LegTh = 0.5, Spread = 0.7, Height = 0.4, ArmSize = 0.75, ArmLen = 0.45, TailLen = 0.3, ShellSize = 0.8, Legs = "8",
                Ab = new[] { ("flat", 3.0) }, Eyes = new[] { ("stalks", 4.0) }, Mouth = new[] { ("none", 2.0), ("mandibles", 1.0) },
                Ant = new[] { ("short", 2.0), ("none", 1.0) }, Arms = new[] { ("pincers", 1.0) }, Shell = new[] { ("carapace", 3.0), ("spiky", 1.0) },
            },
            "mantis" => new Prior
            {
                Size = 0.45, ThSize = 0.3, ThLen = 0.85, AbSize = 0.55, AbLift = 0.45, Waist = 0.3, HeadSize = 0.45, EyeSize = 0.75, MouthSize = 0.35, AntLen = 0.6, HornSize = 0.3,
                LegLen = 0.6, LegTh = 0.25, Spread = 0.4, Height = 0.55, ArmSize = 0.6, ArmLen = 0.7, TailLen = 0.4, ShellSize = 0.4, Legs = "4",
                Ab = new[] { ("long", 3.0), ("oval", 1.0) }, Eyes = new[] { ("compound", 4.0) }, Mouth = new[] { ("mandibles", 2.0), ("none", 1.0) },
                Ant = new[] { ("long", 3.0), ("short", 1.0) }, Arms = new[] { ("blades", 1.0) }, Shell = new[] { ("none", 3.0), ("elytra", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.45, ThSize = 0.5, ThLen = 0.2, AbSize = 0.75, AbLift = 0.3, Waist = 0.45, HeadSize = 0.35, EyeSize = 0.5, MouthSize = 0.5, AntLen = 0.3, HornSize = 0.3,
                LegLen = 0.7, LegTh = 0.35, Spread = 0.6, Height = 0.45, ArmSize = 0.4, ArmLen = 0.3, TailLen = 0.3, ShellSize = 0.4, Legs = "8",
                Ab = new[] { ("round", 3.0), ("oval", 1.5) }, Eyes = new[] { ("cluster", 5.0) }, Mouth = new[] { ("fangs", 4.0) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "thorax.size", p.ThSize, sp);
            Around(v, rng, "thorax.length", p.ThLen, sp);
            Around(v, rng, "abdomen.size", p.AbSize, sp);
            SetChoice(v, "abdomen.shape", PickWeighted(rng, p.Ab));
            Around(v, rng, "abdomen.lift", p.AbLift, sp);
            Around(v, rng, "waist", p.Waist, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            SetChoice(v, "head.eyes", PickWeighted(rng, p.Eyes));
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.mouth", PickWeighted(rng, p.Mouth));
            Around(v, rng, "head.mouthSize", p.MouthSize, sp);
            SetChoice(v, "head.antennae", PickWeighted(rng, p.Ant));
            Around(v, rng, "head.antennaLength", p.AntLen, sp);
            SetChoice(v, "head.horn", PickWeighted(rng, p.Horn));
            Around(v, rng, "head.hornSize", p.HornSize, sp);
            SetChoice(v, "legs.count", p.Legs);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            Around(v, rng, "legs.spread", p.Spread, sp);
            Around(v, rng, "legs.height", p.Height, sp);
            SetChoice(v, "arms.kind", PickWeighted(rng, p.Arms));
            Around(v, rng, "arms.size", p.ArmSize, sp);
            Around(v, rng, "arms.length", p.ArmLen, sp);
            SetChoice(v, "tail.kind", PickWeighted(rng, p.Tail));
            Around(v, rng, "tail.length", p.TailLen, sp);
            SetChoice(v, "shell.kind", PickWeighted(rng, p.Shell));
            Around(v, rng, "shell.size", p.ShellSize, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["chitin"] = 6, ["hide"] = 0.5, ["fur"] = 0.3 },
                Patterns = new Dictionary<string, double> { ["none"] = 2, ["bands"] = 1.5, ["spots"] = 1, ["dorsal"] = 1, ["stripes"] = 0.6 },
                Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 1, ["vivid"] = 0.3 },
                Coats = new[] { Coats.Black(1.5), Coats.Brown(1.5), Coats.Rust(1), Coats.Chocolate(0.8), Coats.Sand(0.6), Coats.Green(0.5), Coats.Blue(0.4), Coats.Vivid(0.4) },
                CountershadeMin = 0.1, CountershadeMax = 0.45,
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 2, ["complementary"] = 1, ["monochrome"] = 1.5, ["split"] = 0.5 },
                SocksChance = 0.12, MaskChance = 0.0, PiebaldChance = 0.0,
                GlowChance = 0.12,
            };
            switch (arch)
            {
                case "spider":
                    // tarantula black and brown, banded legs, orange and blue tarantulas, sand spiders
                    h.Materials = new Dictionary<string, double> { ["chitin"] = 3, ["fur"] = 2 };
                    h.Coats = new[] { Coats.Black(2), Coats.Brown(1.5), Coats.Chocolate(1), Coats.Grey(0.8), Coats.Sand(0.6), Coats.Rust(0.5), Coats.Yellow(0.3), Coats.Blue(0.3), Coats.Vivid(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1.5, ["spots"] = 1, ["dorsal"] = 1.2, ["bands"] = 1, ["saddle"] = 0.6 };
                    h.SocksChance = 0.25;
                    break;
                case "beetle":
                    // black, metallic green and blue, ladybird red, stag brown, violet, wasp yellow
                    h.Coats = new[] { Coats.Black(1.4), Coats.Green(1.2), Coats.Blue(1), Coats.Crimson(1), Coats.Brown(0.8), Coats.Rust(0.8), Coats.Violet(0.6), Coats.Yellow(0.5), Coats.Golden(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["spots"] = 1.5, ["stripes"] = 1 };
                    break;
                case "ant":
                    h.Coats = new[] { Coats.Black(2), Coats.Rust(1.6), Coats.Brown(1.2), Coats.Chocolate(0.8), Coats.Yellow(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 4, ["bands"] = 0.5 };
                    h.EnergyMin = 0.5;
                    break;
                case "scorpion":
                    h.Coats = new[] { Coats.Sand(1.8), Coats.Black(1.4), Coats.Brown(1), Coats.Chocolate(0.8), Coats.Rust(0.6), Coats.Yellow(0.4), Coats.Blue(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["bands"] = 2, ["dorsal"] = 1 };
                    break;
                case "crab":
                    h.Coats = new[] { Coats.Crimson(1.6), Coats.Rust(1.4), Coats.Blue(1), Coats.Brown(0.8), Coats.Sand(0.6), Coats.Violet(0.4), Coats.Teal(0.4) };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 1 };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["spots"] = 1, ["mottled"] = 1 };
                    h.SocksChance = 0.2;
                    break;
                case "mantis":
                    h.Coats = new[] { Coats.Green(2.5), Coats.Olive(1), Coats.Sand(0.8), Coats.Pink(0.6), Coats.Brown(0.6) };
                    h.Materials = new Dictionary<string, double> { ["chitin"] = 3, ["leaf"] = 1 };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["stripes"] = 0.6 };
                    h.CountershadeMin = 0.3; h.CountershadeMax = 0.6;
                    h.SocksChance = 0.05;
                    break;
            }
            return h;
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            double size = g.Get("size");
            double baseL = DMath.Lerp(11, 32, DMath.Pow(size, 1.05));
            int legCount = int.Parse(g.GetChoice("legs.count"), System.Globalization.CultureInfo.InvariantCulture);
            int pairs = legCount / 2;
            string armKind = g.GetChoice("arms.kind");
            string tailKind = g.GetChoice("tail.kind");
            string shell = g.GetChoice("shell.kind");
            string abShape = g.GetChoice("abdomen.shape");

            double thR = baseL * DMath.Lerp(0.16, 0.3, g.Get("thorax.size"));
            double thL = thR * DMath.Lerp(1.0, 3.2, g.Get("thorax.length"));
            // beetles carry short, stout legs under the shell
            double legLen = baseL * DMath.Lerp(0.55, 1.45, g.Get("legs.length")) * (form == "beetle" ? 0.62 : 1.0);
            double height = g.Get("legs.height");
            double bodyH = Math.Max(thR * 0.9 + 0.8, legLen * DMath.Lerp(0.16, 0.42, height));
            bool crab = form == "crab";

            // ---- skeleton: thorax carries head (front) and abdomen (back)
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            Vec3 T = new Vec3(0, bodyH, 0);
            int thorax = b.BoneAlong("thorax", root, T, Vec3.UnitX, Vec3.UnitY, BoneRole.Pelvis, 0, thL);
            b.Rig.Root = root; b.Rig.Pelvis = thorax;
            int body = b.Group("body", DMath.Clamp(thR * 0.3, 1.0, 3.0), 0, GroupFlags.CreaseShade);
            double domain = thL + baseL;
            var thP = b.Ellipsoid(body, thorax, new Vec3(0, 0, 0), new Vec3(thL * 0.55 + thR * 0.35, thR * (crab ? 0.62 : 0.8), thR * (crab ? 1.35 : 0.95)),
                MaterialSlot.Primary, PatternDomain.Body, 0.35, 0.65);
            thP.DomainLength = domain; thP.Tag = "thorax";

            // ---- abdomen (spring chain: sways and bobs with follow-through)
            double abSize = g.Get("abdomen.size");
            double abR = baseL * DMath.Lerp(0.16, 0.36, abSize) * (abShape == "flat" ? 0.8 : 1.0);
            double abL = abR * abShape switch { "round" => 1.15, "oval" => 1.55, "long" => 2.3, _ => 1.4 };
            if (crab) { abR = thR * 0.5; abL = thR * 0.7; }
            double lift = DMath.Lerp(-0.05, 0.6, g.Get("abdomen.lift"));
            double waist = g.Get("waist");
            Vec3 abBase = T + new Vec3(-(thL * 0.5 + thR * 0.35) - DMath.Lerp(0.0, thR * 0.45, waist), thR * 0.1, 0);
            Vec3 abDir = new Vec3(-DMath.Cos(lift), DMath.Sin(lift), 0);
            int ab0 = b.BoneAlong("abdomen.0", thorax, abBase, abDir, Vec3.UnitY, BoneRole.Tail, 0, abL * 2);
            int ab1 = b.BoneAt("abdomen.1", ab0, abBase + abDir * (abL * 2), BoneRole.Tail);
            int abGroup = waist > 0.4 ? b.Group("abdomen", DMath.Clamp(abR * 0.25, 0.8, 2.5)) : body;
            double abFlat = abShape == "flat" ? 0.62 : 0.88;
            var abP = b.Ellipsoid(abGroup, ab0, new Vec3(abL * 0.95, abR * 0.05, 0), new Vec3(abL, abR * abFlat, abR), MaterialSlot.Primary, PatternDomain.Body, 0.0, 0.35);
            abP.DomainLength = domain; abP.Tag = "abdomen";
            if (waist > 0.4)
            {
                // petiole: a thin waist bead between thorax and abdomen
                var pet = b.Ellipsoid(body, ab0, new Vec3(-thR * 0.15 * waist, 0, 0), new Vec3(thR * 0.4, thR * 0.32, thR * 0.32), MaterialSlot.Primary, PatternDomain.Body, 0.34, 0.36);
                pet.DomainLength = domain;
            }
            b.Rig.Tail = new ChainRig
            {
                Name = "abdomen", Bones = new[] { ab0, ab1 }, Lengths = new[] { abL * 2 }, TotalLength = abL * 2, Radii = new[] { abR * 0.6, abR * 0.6 },
                Stiffness = 0.9, RestPitch = new[] { 0.0 }, RestYaw = new[] { 0.0 }, Droop = 0.1,
            };
            b.Rig.Tuning["tail.gravity"] = 8;

            // ---- prothorax: long-necked arthropods (mantis) carry the head on a raised front segment
            double headR = baseL * DMath.Lerp(0.1, 0.22, g.Get("head.size"));
            if (crab) headR = thR * 0.35;
            double proLen = baseL * DMath.Lerp(0.0, 0.75, DMath.SmoothStep(0.35, 1.0, g.Get("thorax.length")));
            int headParent = thorax;
            Vec3 Hj = T + new Vec3(thL * 0.5 + thR * 0.25, thR * 0.05, 0);
            Vec3 proEnd = Hj;
            if (proLen > thR * 0.8)
            {
                double raise = DMath.Lerp(0.35, 0.85, g.Get("thorax.length"));
                Vec3 proDir = new Vec3(DMath.Cos(raise), DMath.Sin(raise), 0);
                Vec3 pro0 = T + new Vec3(thL * 0.45, thR * 0.1, 0);
                int pro = b.BoneAlong("prothorax", thorax, pro0, proDir, Vec3.UnitY, BoneRole.Neck, 0, proLen);
                var pp = b.Cone(body, thorax, new Vec3(thL * 0.3, thR * 0.1, 0), thR * 0.62, pro, new Vec3(proLen, 0, 0), Math.Max(0.8, thR * 0.42), MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
                pp.DomainLength = proLen;
                pp.Tag = "prothorax";
                proEnd = pro0 + proDir * proLen;
                Hj = proEnd + proDir * (headR * 0.3);
                headParent = pro;
                b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { pro, -1 }, Lengths = new[] { proLen }, TotalLength = proLen };
            }
            string eyes = g.GetChoice("head.eyes");
            string mouth = g.GetChoice("head.mouth");
            double mouthSize = g.Get("head.mouthSize");
            var hs = new HeadSpec
            {
                ParentBone = headParent,
                Position = Hj,
                Forward = new Vec3(1, headParent != thorax ? -0.55 : -0.15, 0),
                SkullLength = headR * 2.0,
                SkullHeight = headR * (form == "mantis" ? 1.5 : 1.7),
                SkullWidth = headR * (form == "mantis" ? 2.6 : 1.9),
                SkullOffset = 0.35,
                SnoutLength = headR * (form == "ant" || form == "beetle" ? 0.5 : 0.25),
                SnoutRadius = headR * 0.6,
                SnoutTaper = 0.8,
                SnoutDrop = 0.3,
                NoseSize = 0,
                JawLength = mouth == "none" ? 0.6 : 0.9,
                JawRadius = headR * 0.35,
                JawDepth = 0.7,
                EyeSize = DMath.Clamp(DMath.Lerp(1.4, 3.8, g.Get("head.eyeSize")) * Math.Sqrt(headR / 3.5), 1.3, 5.0),
                EyeStyle = eyes == "compound" ? EyeStyle.Compound : EyeStyle.Bead,
                EyeForward = eyes == "compound" ? 0.35 : 0.85,
                EyeHeight = 0.45,
                Teeth = mouth switch { "mandibles" => TeethKind.Mandibles, "fangs" => TeethKind.Fangs, _ => TeethKind.None },
                TeethSize = DMath.Clamp(headR / 2.5 * DMath.Lerp(0.8, 1.6, mouthSize), 0.8, 2.4),
                Group = crab ? body : -1,
                BlendRadius = 1.0,
                DomainLength = headR * 3,
                JawOpenMax = 0.5,
                Nostrils = false,
                SnoutPattern = false,
            };
            if (eyes == "stalks") hs.EyeCount = 0; // stalk eyes are built separately
            var head = Heads.Build(b, hs);
            if (b.Rig.Neck != null) b.Rig.Neck.Bones[1] = head.Head;
            if (eyes == "stalks") BuildEyeStalks(b, head, headR, g.Get("head.eyeSize"));
            if (eyes == "cluster") AddEyeCluster(b, head, headR, hs.EyeSize);

            string ant = g.GetChoice("head.antennae");
            if (ant != "none")
            {
                double aLen = baseL * DMath.Lerp(0.25, 0.9, g.Get("head.antennaLength")) * (ant == "short" ? 0.55 : 1.0);
                for (int side = -1; side <= 1; side += 2)
                {
                    var hx = b.RestWorld(head.Head);
                    Vec3 basePos = hx.Point(head.SkullCenterLocal + new Vec3(headR * 0.7, headR * 0.55, side * headR * 0.45));
                    var chain = Chains.Build(b, new ChainSpec
                    {
                        Name = "antenna." + (side < 0 ? "L" : "R"),
                        ParentBone = head.Head,
                        Base = basePos,
                        Direction = new Vec3(0.75, 0.6, side * 0.35),
                        Length = aLen,
                        Segments = 3,
                        RadiusBase = 0.5,
                        RadiusTip = ant == "clubbed" ? 0.5 : 0.35,
                        Curl = form == "ant" ? -1.4 : -0.6,
                        Tip = ant == "clubbed" ? TailTip.Tuft : TailTip.Plain,
                        TipSize = 0.5,
                        Material = MaterialSlot.Primary,
                        Role = BoneRole.Antenna,
                        Domain = PatternDomain.Appendage,
                        Side = side,
                        Stiffness = 0.7,
                        Droop = 0.05,
                        Flags = PrimFlags.Thin | PrimFlags.NoShadow,
                    });
                    b.Rig.Appendages.Add(chain);
                }
            }
            string horn = g.GetChoice("head.horn");
            if (horn != "none") BuildHorn(b, horn, head, headR, baseL * DMath.Lerp(0.2, 0.55, g.Get("head.hornSize")));

            // ---- legs: 2..4 pairs around the thorax
            double spread = g.Get("legs.spread");
            double legTh = g.Get("legs.thickness");
            double rTop = Math.Max(0.7, thR * DMath.Lerp(0.18, 0.36, legTh));
            for (int pair = 0; pair < pairs; pair++)
            {
                double t = pairs == 1 ? 0.5 : pair / (double)(pairs - 1);
                double hx = DMath.Lerp(thL * 0.42 + thR * 0.2, -thL * 0.42 - thR * 0.2, t);
                if (form == "mantis") hx = DMath.Lerp(-thL * 0.25, -thL * 0.5 - thR * 0.3, t);
                double fan = DMath.Lerp(0.95, -0.95, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 hip = T + new Vec3(hx, -thR * 0.25, side * thR * (crab ? 1.1 : 0.78));
                    double reach = legLen * DMath.Lerp(0.42, 0.62, spread);
                    Vec3 contact = new Vec3(hip.X + fan * reach * 0.55, 0, side * (Math.Abs(hip.Z) + reach * DMath.Lerp(0.8, 0.95, 1 - Math.Abs(fan) * 0.5)));
                    double dist = (contact - hip).Length;
                    double len = Math.Max(dist * 1.3, legLen * (pair == 0 && form == "spider" ? 1.08 : 1.0));
                    var spec = new LegSpec
                    {
                        Name = $"leg{pair}{(side < 0 ? "L" : "R")}",
                        Side = side, Pair = pair, Front = pair < pairs / 2,
                        AnchorBone = thorax,
                        Hip = hip, Contact = contact,
                        Style = LegStyle.Arthropod,
                        Length = len,
                        Upper = 0.36, Lower = 0.38, Distal = 0.26,
                        RadiusTop = rTop,
                        RadiusKnee = Math.Max(0.6, rTop * 0.8),
                        RadiusAnkle = Math.Max(0.5, rTop * 0.6),
                        RadiusFoot = Math.Max(0.45, rTop * 0.45),
                        Haunch = 0,
                        PawRadius = 0.6,
                        LiftHeight = Math.Max(1.5, bodyH * 0.5),
                        BlendRadius = 0.6,
                        DomainLength = len,
                    };
                    var leg = Limbs.Build(b, spec);
                    bool left = side < 0;
                    // metachronal wave: back to front on each side, sides in antiphase
                    leg.Phase = DMath.Frac((pairs - 1 - pair) / (double)pairs * 0.5 + (left ? 0 : 0.5));
                    b.Rig.Legs.Add(leg);
                }
            }

            // ---- pincers / raptorial blades
            if (armKind != "none")
            {
                double armLen = baseL * DMath.Lerp(0.4, 0.95, g.Get("arms.length"));
                double clawSize = baseL * DMath.Lerp(0.12, 0.34, g.Get("arms.size"));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 sh = headParent != thorax
                        ? proEnd + new Vec3(-headR * 0.6, -headR * 0.9, side * thR * 0.45)
                        : T + new Vec3(thL * 0.5 + thR * 0.1, -thR * 0.1, side * thR * (crab ? 0.95 : 0.6));
                    Vec3 wrist = armKind == "blades"
                        ? sh + new Vec3(armLen * 0.3, -armLen * 0.25, side * armLen * 0.1)
                        : sh + new Vec3(armLen * 0.55, -Math.Min(sh.Y * 0.55, armLen * 0.25), side * armLen * 0.38);
                    var spec = new LegSpec
                    {
                        Name = "arm" + (side < 0 ? "L" : "R"),
                        Side = side, Pair = 0, Front = true, IsArm = true, NoFoot = true,
                        AnchorBone = headParent,
                        Hip = sh, Contact = wrist,
                        Style = LegStyle.Arthropod,
                        Length = armLen,
                        Upper = 0.42, Lower = 0.4, Distal = 0.18,
                        RadiusTop = Math.Max(0.8, rTop * 1.25),
                        RadiusKnee = Math.Max(0.7, rTop * 1.1),
                        RadiusAnkle = Math.Max(0.6, rTop),
                        RadiusFoot = Math.Max(0.6, rTop),
                        Haunch = 0,
                        Pole = armKind == "blades" ? new Vec3(-0.2, 1.0, 0.3) : new Vec3(-0.3, 0.7, 1.0),
                        BlendRadius = 0.7,
                        DomainLength = armLen,
                    };
                    var arm = Limbs.Build(b, spec);
                    Pincers.Build(b, new PincerSpec
                    {
                        Name = "pincer" + (side < 0 ? "L" : "R"),
                        HandBone = arm.JointBones[2],
                        Side = side,
                        Size = clawSize,
                        Bulk = crab ? 1.25 : 1.0,
                        Group = arm.Group,
                        Blade = armKind == "blades",
                    });
                    b.Rig.Arms.Add(arm);
                }
                b.Trait(armKind);
            }

            // ---- tail: scorpion metasoma curled over the back, or cerci
            if (tailKind == "stinger")
            {
                double tl = baseL * DMath.Lerp(0.6, 1.5, g.Get("tail.length"));
                double tr = Math.Max(0.7, abR * 0.34);
                bool scorpion = form == "scorpion" || tl > baseL * 0.9;
                var tail = Chains.Build(b, new ChainSpec
                {
                    Name = "stinger",
                    ParentBone = ab1,
                    Base = abBase + abDir * (abL * 1.9),
                    Direction = scorpion ? new Vec3(-0.5, 0.86, 0) : abDir,
                    Length = scorpion ? tl : tl * 0.3,
                    Segments = scorpion ? 5 : 2,
                    RadiusBase = tr,
                    RadiusTip = tr * 0.75,
                    Curl = scorpion ? 3.1 : 0,
                    Tip = TailTip.Stinger,
                    TipSize = DMath.Clamp(tr * 1.1, 0.8, 2.2),
                    Group = abGroup,
                    Material = MaterialSlot.Primary,
                    Stiffness = 0.8,
                    Droop = 0.1,
                });
                if (scorpion)
                {
                    // the abdomen chain stays rigid; the curled metasoma is the animated tail
                    b.Rig.Tail = tail;
                    b.Rig.Tuning["tail.gravity"] = 10;
                }
            }
            else if (tailKind == "cerci")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var c = Chains.Build(b, new ChainSpec
                    {
                        Name = "cercus." + (side < 0 ? "L" : "R"),
                        ParentBone = ab1,
                        Base = abBase + abDir * (abL * 1.9) + new Vec3(0, 0, side * abR * 0.2),
                        Direction = abDir + new Vec3(0, 0.2, side * 0.35),
                        Length = baseL * DMath.Lerp(0.2, 0.6, g.Get("tail.length")),
                        Segments = 2, RadiusBase = 0.5, RadiusTip = 0.35, Side = side,
                        Role = BoneRole.Antenna, Domain = PatternDomain.Appendage, Flags = PrimFlags.Thin | PrimFlags.NoShadow, Stiffness = 0.6, Droop = 0.2,
                    });
                    b.Rig.Appendages.Add(c);
                }
            }

            // ---- shells
            double shs = g.Get("shell.size");
            switch (shell)
            {
                case "elytra":
                {
                    int eg = b.Group("elytra", 0.5, 0, GroupFlags.None, 0.4);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var e = b.Ellipsoid(eg, ab0, new Vec3(abL * 0.9, abR * 0.35, side * abR * 0.5), new Vec3(abL * DMath.Lerp(1.0, 1.12, shs), abR * 0.72, abR * 0.58),
                            MaterialSlot.Primary, PatternDomain.Wing, 0, 1, null, side);
                        e.DomainLength = abL * 2;
                        e.Tag = "elytron";
                    }
                    b.Trait("elytra");
                    break;
                }
                case "carapace":
                case "spiky":
                {
                    var c = b.Ellipsoid(body, thorax, new Vec3(-thR * 0.05, thR * 0.3, 0), new Vec3((thL * 0.55 + thR * 0.35) * 1.05, thR * DMath.Lerp(0.45, 0.62, shs), thR * (crab ? 1.5 : 1.05)),
                        MaterialSlot.Primary, PatternDomain.Body, 0.35, 0.65);
                    c.DomainLength = domain;
                    c.Tag = "carapace";
                    if (shell == "spiky")
                    {
                        var ds = new DorsalSpec { Kind = DorsalKind.Spikes, Size = thR * 0.5, Material = MaterialSlot.Accent };
                        for (int i = 0; i < 4; i++)
                        {
                            double a = DMath.Lerp(-1.1, 1.1, i / 3.0);
                            Vec3 pos = T + new Vec3(thR * 0.3 * (i % 2 == 0 ? 1 : -0.4), thR * 0.75, DMath.Sin(a) * thR * (crab ? 1.2 : 0.8));
                            ds.Anchors.Add((thorax, pos, new Vec3(0, 1, DMath.Sin(a) * 0.7).Normalized(), 0.9));
                        }
                        Dorsal.Build(b, ds);
                    }
                    break;
                }
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = "legged";
            b.Rig.Action = form switch
            {
                "scorpion" => ActionStyle.Sting,
                "spider" => tailKind == "stinger" ? ActionStyle.Sting : ActionStyle.Rear,
                "crab" => ActionStyle.PincerSnap,
                "mantis" => ActionStyle.PincerSnap,
                "beetle" => horn != "none" ? ActionStyle.Slam : ActionStyle.Bite,
                _ => tailKind == "stinger" ? ActionStyle.Sting : ActionStyle.Bite,
            };
            b.Rig.Rest = RestStyle.Fold;
            b.Rig.Tuning["gait.walkDuty"] = 0.6;
            b.Rig.Tuning["gait.runDuty"] = 0.46;
            b.Rig.Tuning["gait.gallop"] = 0;
            b.Rig.Tuning["rest.bodyHeight"] = Math.Max(thR * 0.75, 1.2);
            b.Metrics.LegLength = Math.Max(bodyH * 1.6, legLen * 0.45);
            b.Metrics.HipHeight = bodyH;
            b.Metrics.BackHeight = bodyH + thR;
            b.Metrics.BodyLength = thL + thR + abL * 2 + headR * 2;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * 0.8;
            double legL = b.Metrics.LegLength;
            b.Metrics.WalkSpeed = Math.Sqrt(0.35 * 400 * legL) * DMath.Lerp(0.8, 1.1, g.GetOr("motion.speed", 0.5));
            b.Metrics.RunSpeed = Math.Sqrt(2.3 * 400 * legL) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5));
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 24.0), 0.7, 1.4);
            double armReach = armKind != "none" ? baseL * DMath.Lerp(0.4, 0.95, g.Get("arms.length")) * 0.45 : 0;
            b.AnimationMargin = Math.Max(7, baseL * 0.38 + armReach);
            b.ExtraHeadroom = Math.Max(4, legLen * 0.25 + (tailKind == "stinger" ? baseL * 0.4 : 0));
            return b.Build(g.AnatomyKey);
        }

        private static void BuildEyeStalks(AnatomyBuilder b, HeadResult head, double headR, double eyeSize)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var hx = b.RestWorld(head.Head);
                Vec3 basePos = hx.Point(head.SkullCenterLocal + new Vec3(headR * 0.4, headR * 0.5, side * headR * 0.5));
                double len = headR * 1.6;
                var chain = Chains.Build(b, new ChainSpec
                {
                    Name = "eyestalk." + (side < 0 ? "L" : "R"),
                    ParentBone = head.Head,
                    Base = basePos,
                    Direction = new Vec3(0.3, 1, side * 0.35),
                    Length = len,
                    Segments = 2,
                    RadiusBase = Math.Max(0.55, headR * 0.22),
                    RadiusTip = Math.Max(0.5, headR * 0.2),
                    Role = BoneRole.Antenna,
                    Domain = PatternDomain.Appendage,
                    Side = side,
                    Stiffness = 0.85,
                    Droop = 0.05,
                });
                b.Rig.Appendages.Add(chain);
                int tip = chain.Bones[chain.Bones.Length - 1];
                b.Feature(new FeatureDef
                {
                    Kind = FeatureKind.Eye,
                    Bone = tip,
                    LocalPosition = new Vec3(0.2, 0, 0),
                    LocalNormal = b.RestWorld(tip).InverseDir(new Vec3(0.8, 0.3, side * 0.5).Normalized()),
                    Size = DMath.Clamp(DMath.Lerp(1.4, 2.6, eyeSize), 1.3, 3.0),
                    Style = EyeStyle.Bead,
                    Side = side,
                    Group = -1,
                });
            }
        }

        private static void AddEyeCluster(AnatomyBuilder b, HeadResult head, double headR, double mainSize)
        {
            // spiders: two big front eyes (from the head block) plus a row of small ones above them
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vec3 pos = head.SkullCenterLocal + new Vec3(headR * (0.62 - k * 0.3), headR * (0.62 + k * 0.12), side * headR * (0.35 + k * 0.28));
                    b.Feature(new FeatureDef
                    {
                        Kind = FeatureKind.Eye,
                        Bone = head.Head,
                        LocalPosition = pos,
                        LocalNormal = new Vec3(0.8 - k * 0.3, 0.5, side * (0.3 + k * 0.4)).Normalized(),
                        Size = Math.Max(1.0, mainSize * 0.45),
                        Style = EyeStyle.Bead,
                        Side = side,
                        Group = head.Group,
                    });
                }
            }
        }

        private static void BuildHorn(AnatomyBuilder b, string horn, HeadResult head, double headR, double size)
        {
            if (horn == "rhino")
            {
                Horns.Build(b, new HornSpec
                {
                    Kind = HornKind.Nasal,
                    HeadBone = head.Head,
                    Front = head.SkullCenterLocal + new Vec3(headR * 0.7, headR * 0.35, 0),
                    Size = size * 1.3,
                    Thickness = 1.1,
                    Material = MaterialSlot.Accent,
                });
                return;
            }
            // stag beetle: two antler-like mandibles forward
            int g = b.Group("stag", 0.5, 0, GroupFlags.None, 0.3);
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 a = head.SkullCenterLocal + new Vec3(headR * 0.8, -headR * 0.2, side * headR * 0.45);
                Vec3 m = a + new Vec3(size * 0.6, size * 0.15, side * size * 0.35);
                Vec3 tip = m + new Vec3(size * 0.45, 0, -side * size * 0.3);
                var c1 = b.Cone(g, head.Head, a, Math.Max(0.6, headR * 0.28), head.Head, m, Math.Max(0.5, headR * 0.2), MaterialSlot.Accent);
                var c2 = b.Cone(g, head.Head, m, Math.Max(0.5, headR * 0.2), head.Head, tip, 0.35, MaterialSlot.Accent);
                c1.Flags |= PrimFlags.NoPattern; c2.Flags |= PrimFlags.NoPattern | PrimFlags.Thin;
                var tine = b.Cone(g, head.Head, m, 0.45, head.Head, m + new Vec3(size * 0.1, size * 0.28, 0), 0.3, MaterialSlot.Accent);
                tine.Flags |= PrimFlags.NoPattern | PrimFlags.Thin;
            }
        }
    }
}
