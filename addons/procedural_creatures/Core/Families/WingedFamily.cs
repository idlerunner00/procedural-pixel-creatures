// Procedural Pixel Creatures - family 5: winged creatures (songbirds, raptors, owls, bats, moths,
// dragonflies and wasps). Feathered, membrane or insect wings; birds walk and take off when they
// run, bats and insects hover.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class WingedFamily : CreatureFamilyBase
    {
        public override string Id => "winged";
        public override string DisplayName => "Winged Creatures";
        public override string Description => "Songbirds, raptors, owls, bats, moths and dragonflies with feathered, membrane or insect wings. Birds walk and take off when running; bats and insects hover.";
        public override int Order => 50;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("songbird", "Songbird", 1.2),
            new ArchetypeInfo("raptor", "Raptor", 1.0),
            new ArchetypeInfo("owl", "Owl", 0.9),
            new ArchetypeInfo("bat", "Bat", 1.0),
            new ArchetypeInfo("moth", "Moth", 0.8),
            new ArchetypeInfo("dragonfly", "Dragonfly", 0.8),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("songbird", "Songbird"), ("raptor", "Raptor"), ("owl", "Owl"), ("bat", "Bat"), ("moth", "Moth"), ("dragonfly", "Dragonfly"),
        };
        public static readonly (string, string)[] WingKinds = { ("feathered", "Feathered"), ("membrane", "Membrane"), ("insect", "Insect") };
        public static readonly (string, string)[] Crests = { ("none", "None"), ("tuft", "Tuft"), ("crest", "Crest"), ("tufts", "Tufts") };
        public static readonly (string, string)[] EarKinds = { ("none", "None"), ("pointed", "Pointed"), ("long", "Long"), ("round", "Round") };
        public static readonly (string, string)[] EyeStyles = { ("round", "Round"), ("bead", "Beady"), ("button", "Large & cute"), ("compound", "Compound"), ("glow", "Glowing") };
        public static readonly (string, string)[] TailShapes = { ("none", "None"), ("fan", "Fan"), ("forked", "Forked"), ("long", "Long"), ("stinger", "Stinger") };
        public static readonly (string, string)[] WingPatterns = { ("none", "None"), ("eyespot", "Eye spots"), ("border", "Border"), ("bands", "Bands"), ("veins", "Veins") };
        public static readonly (string, string)[] AntennaKinds = { ("none", "None"), ("thin", "Thin"), ("feathery", "Feathery") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "songbird", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.4, "", 0.08)
             .Float("body.mass", "Body mass", 0, 1, 0.5, "Slender to spherical.", 0.1)
             .Float("body.length", "Body length", 0, 1, 0.5, "", 0.1)
             .Float("body.tilt", "Body tilt", 0, 1, 0.45, "Horizontal to upright posture.", 0.1)
             .Float("abdomen.length", "Abdomen length", 0, 1, 0.3, "Insects only: short to very long abdomen.", 0.1)
             .Float("neck.length", "Neck length", 0, 1, 0.3, "", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("head.beak", "Head beak", 0, 1, 0.45, "", 0.1)
             .Float("head.beakCurve", "Head beak curve", 0, 1, 0.2, "", 0.1)
             .Float("head.beakDepth", "Head beak depth", 0, 1, 0.45, "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.55, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "round", "", 0.08)
             .Choice("head.crest", "Head crest", Crests, "none", "", 0.1)
             .Float("head.crestSize", "Head crest size", 0, 1, 0.5, "", 0.1)
             .Float("head.disc", "Head disc", 0, 1, 0.0, "Pale facial disc typical of owls.", 0.1)
             .Choice("head.ears", "Head ears", EarKinds, "none", "", 0.1)
             .Float("head.earSize", "Head ear size", 0, 1, 0.5, "", 0.1)
             .Choice("head.antennae", "Head antennae", AntennaKinds, "none", "", 0.1);
            b.Group("wings", "Wings", GeneStream.Anatomy)
             .Choice("wings.kind", "Wings kind", WingKinds, "feathered", "", 0.05)
             .Float("wings.span", "Wings span", 0, 1, 0.55, "", 0.1)
             .Float("wings.chord", "Wings chord", 0, 1, 0.5, "Narrow wings for swift flight to broad gliding wings.", 0.1)
             .Float("wings.feathers", "Wings feathers", 0, 1, 0.5, "Number of spread primary feathers or wing fingers.", 0.1);
            b.Group("legs", "Legs", GeneStream.Anatomy)
             .Float("legs.length", "Legs length", 0, 1, 0.4, "", 0.1)
             .Float("legs.thickness", "Legs thickness", 0, 1, 0.4, "", 0.1)
             .Float("legs.talons", "Legs talons", 0, 1, 0.4, "", 0.1)
             .Choice("tail.shape", "Tail shape", TailShapes, "fan", "", 0.1)
             .Float("tail.length", "Tail length", 0, 1, 0.5, "", 0.1);
            b.Group("wingpattern", "Wing markings", GeneStream.Pattern)
             .Choice("wings.pattern", "Wings pattern", WingPatterns, "none", "Insect wings only: eye spots, dotted borders, crossbands or veins.", 0.08);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "feather");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Mass, Len, Tilt, AbLen, Neck, HeadSize, Beak, Curve, Depth, EyeSize, CrestSize, Disc, EarSize;
            public double Span, Chord, Feathers, LegLen, LegTh, Talons, TailLen;
            public string Wings = "feathered";
            public (string, double)[] Eyes = { ("round", 1) }, Crest = { ("none", 1) }, Ears = { ("none", 1) }, Tail = { ("fan", 1) }, Ant = { ("none", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "raptor" => new Prior
            {
                Size = 0.6, Mass = 0.55, Len = 0.55, Tilt = 0.55, AbLen = 0.3, Neck = 0.35, HeadSize = 0.45, Beak = 0.35, Curve = 0.85, Depth = 0.6, EyeSize = 0.45, CrestSize = 0.4, Disc = 0,
                EarSize = 0.3, Span = 0.8, Chord = 0.55, Feathers = 0.75, LegLen = 0.45, LegTh = 0.6, Talons = 0.9, TailLen = 0.55,
                Eyes = new[] { ("round", 3.0), ("glow", 0.3) }, Crest = new[] { ("none", 3.0), ("crest", 0.8), ("tuft", 0.5) }, Tail = new[] { ("fan", 3.0), ("long", 0.6) },
            },
            "owl" => new Prior
            {
                Size = 0.5, Mass = 0.8, Len = 0.35, Tilt = 0.85, AbLen = 0.3, Neck = 0.1, HeadSize = 0.85, Beak = 0.12, Curve = 0.8, Depth = 0.5, EyeSize = 0.95, CrestSize = 0.5, Disc = 0.85,
                EarSize = 0.3, Span = 0.65, Chord = 0.7, Feathers = 0.6, LegLen = 0.3, LegTh = 0.65, Talons = 0.7, TailLen = 0.3,
                Eyes = new[] { ("round", 3.0), ("button", 1.5), ("glow", 0.4) }, Crest = new[] { ("tufts", 2.0), ("none", 1.5) }, Tail = new[] { ("fan", 3.0) },
            },
            "bat" => new Prior
            {
                Size = 0.35, Mass = 0.6, Len = 0.4, Tilt = 0.35, AbLen = 0.3, Neck = 0.1, HeadSize = 0.7, Beak = 0.25, Curve = 0.0, Depth = 0.5, EyeSize = 0.45, CrestSize = 0.4, Disc = 0,
                EarSize = 0.8, Span = 0.75, Chord = 0.6, Feathers = 0.6, LegLen = 0.2, LegTh = 0.3, Talons = 0.5, TailLen = 0.25, Wings = "membrane",
                Eyes = new[] { ("bead", 3.0), ("button", 1.5), ("glow", 0.6) }, Ears = new[] { ("pointed", 3.0), ("long", 1.0), ("round", 1.0) },
                Tail = new[] { ("none", 2.0), ("long", 1.0) },
            },
            "moth" => new Prior
            {
                Size = 0.35, Mass = 0.65, Len = 0.45, Tilt = 0.3, AbLen = 0.4, Neck = 0.0, HeadSize = 0.45, Beak = 0.0, Curve = 0.0, Depth = 0.5, EyeSize = 0.6, CrestSize = 0.4, Disc = 0,
                EarSize = 0.3, Span = 0.7, Chord = 0.85, Feathers = 0.5, LegLen = 0.3, LegTh = 0.2, Talons = 0.0, TailLen = 0.3, Wings = "insect",
                Eyes = new[] { ("compound", 3.0), ("bead", 1.0) }, Ant = new[] { ("feathery", 3.0), ("thin", 1.0) }, Tail = new[] { ("none", 1.0) },
            },
            "dragonfly" => new Prior
            {
                Size = 0.4, Mass = 0.25, Len = 0.5, Tilt = 0.1, AbLen = 0.9, Neck = 0.0, HeadSize = 0.5, Beak = 0.0, Curve = 0.0, Depth = 0.5, EyeSize = 0.85, CrestSize = 0.4, Disc = 0,
                EarSize = 0.3, Span = 0.75, Chord = 0.3, Feathers = 0.5, LegLen = 0.3, LegTh = 0.15, Talons = 0.0, TailLen = 0.3, Wings = "insect",
                Eyes = new[] { ("compound", 5.0) }, Ant = new[] { ("thin", 1.0), ("none", 2.0) }, Tail = new[] { ("none", 3.0), ("stinger", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.3, Mass = 0.65, Len = 0.45, Tilt = 0.5, AbLen = 0.3, Neck = 0.2, HeadSize = 0.65, Beak = 0.35, Curve = 0.1, Depth = 0.4, EyeSize = 0.6, CrestSize = 0.45, Disc = 0,
                EarSize = 0.3, Span = 0.55, Chord = 0.5, Feathers = 0.45, LegLen = 0.35, LegTh = 0.35, Talons = 0.3, TailLen = 0.55,
                Eyes = new[] { ("bead", 3.0), ("round", 1.0), ("button", 1.0) }, Crest = new[] { ("none", 3.0), ("tuft", 1.5), ("crest", 1.0) },
                Tail = new[] { ("fan", 2.0), ("forked", 1.5), ("long", 1.0) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.12);
            Around(v, rng, "body.mass", p.Mass, sp);
            Around(v, rng, "body.length", p.Len, sp);
            Around(v, rng, "body.tilt", p.Tilt, sp);
            Around(v, rng, "abdomen.length", p.AbLen, sp);
            Around(v, rng, "neck.length", p.Neck, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            Around(v, rng, "head.beak", p.Beak, sp);
            Around(v, rng, "head.beakCurve", p.Curve, sp);
            Around(v, rng, "head.beakDepth", p.Depth, sp);
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "head.crest", PickWeighted(rng, p.Crest));
            Around(v, rng, "head.crestSize", p.CrestSize, sp);
            Around(v, rng, "head.disc", p.Disc, sp * 0.6);
            SetChoice(v, "head.ears", PickWeighted(rng, p.Ears));
            Around(v, rng, "head.earSize", p.EarSize, sp);
            SetChoice(v, "head.antennae", PickWeighted(rng, p.Ant));
            SetChoice(v, "wings.kind", p.Wings);
            Around(v, rng, "wings.span", p.Span, sp);
            Around(v, rng, "wings.chord", p.Chord, sp);
            Around(v, rng, "wings.feathers", p.Feathers, sp);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            Around(v, rng, "legs.talons", p.Talons, sp);
            SetChoice(v, "tail.shape", PickWeighted(rng, p.Tail));
            Around(v, rng, "tail.length", p.TailLen, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["feather"] = 6 },
                Patterns = new Dictionary<string, double> { ["none"] = 2, ["dorsal"] = 0.8, ["spots"] = 1, ["bands"] = 0.8, ["mottled"] = 0.8 },
                Accents = new Dictionary<string, double> { ["gold"] = 2, ["dark"] = 2, ["bone"] = 1 },
                CountershadeMin = 0.35, CountershadeMax = 0.85,
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 2, ["complementary"] = 1, ["triadic"] = 0.5, ["monochrome"] = 1 },
                SocksChance = 0.0, MaskChance = 0.12, PiebaldChance = 0.04,
            };
            switch (arch)
            {
                case "raptor":
                    h.Coats = new[] { Coats.Brown(2), Coats.Chocolate(1.2), Coats.Grey(1), Coats.Tan(0.8), Coats.Black(0.6), Coats.Golden(0.6), Coats.Rust(0.5), Coats.Cream(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1.5, ["bands"] = 1.5, ["mottled"] = 1, ["spots"] = 0.6 };
                    h.Accents = new Dictionary<string, double> { ["gold"] = 4, ["dark"] = 1 };
                    h.MaskChance = 0.15;
                    break;
                case "owl":
                    h.Coats = new[] { Coats.Brown(1.6), Coats.Tan(1.2), Coats.Grey(1.2), Coats.Cream(1), Coats.Ginger(0.6), Coats.Chocolate(0.6), Coats.Golden(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["mottled"] = 2, ["spots"] = 1.5, ["bands"] = 1, ["none"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 2, ["gold"] = 1.5, ["bone"] = 1 };
                    h.MaskChance = 0.05;
                    h.EnergyMax = 0.55;
                    break;
                case "bat":
                    h.Materials = new Dictionary<string, double> { ["fur"] = 6 };
                    h.Coats = new[] { Coats.Brown(1.6), Coats.Chocolate(1.2), Coats.Black(1.2), Coats.Grey(0.8), Coats.Ginger(0.6), Coats.Violet(0.4), Coats.Vivid(0.2) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 4, ["saddle"] = 0.6 };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 1 };
                    h.MaskChance = 0.1;
                    h.GlowChance = 0.2;
                    h.EnergyMin = 0.5;
                    break;
                case "moth":
                    // tan and cream moths, luna green, rosy maple pink, tiger yellow, silver grey
                    h.Materials = new Dictionary<string, double> { ["fur"] = 5 };
                    h.Coats = new[] { Coats.Tan(1.2), Coats.Brown(1), Coats.Cream(1), Coats.Grey(0.8), Coats.Green(0.8), Coats.Pink(0.6), Coats.Yellow(0.6), Coats.Golden(0.5), Coats.Violet(0.4), Coats.Vivid(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 2, ["bands"] = 1, ["none"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 0.5 };
                    h.MaskChance = 0.0;
                    h.GlowChance = 0.25;
                    break;
                case "dragonfly":
                    h.Materials = new Dictionary<string, double> { ["chitin"] = 5 };
                    // metallic blues and greens dominate; dark ambers read as brown at this size
                    h.Coats = new[] { Coats.Blue(1.6), Coats.Teal(1.4), Coats.Green(1.2), Coats.Crimson(0.8), Coats.Violet(0.6), Coats.Yellow(0.4), Coats.Vivid(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["bands"] = 3, ["none"] = 1, ["dorsal"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 0.5 };
                    h.MaskChance = 0.0;
                    h.EnergyMin = 0.6;
                    break;
                default:
                    // songbirds: sparrow brown, bluebird, cardinal red, canary yellow, robin ginger, blackbird, finch green
                    h.Coats = new[] { Coats.Brown(1.2), Coats.Blue(1.2), Coats.Crimson(1), Coats.Yellow(1), Coats.Grey(0.8), Coats.Black(0.8), Coats.Ginger(0.6), Coats.Green(0.6), Coats.Teal(0.6), Coats.Cream(0.4), Coats.Violet(0.3) };
                    h.EnergyMin = 0.55; h.WeightMax = 0.4;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            if (Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
            // insect wings: moths show eyespots, bands and borders, dragonflies veined glass wings
            string wp = arch switch
            {
                "moth" => PickWeighted(rng, ("eyespot", 2.0), ("border", 1.5), ("bands", 1.5), ("veins", 0.6), ("none", 0.4)),
                "dragonfly" => PickWeighted(rng, ("veins", 3.0), ("bands", 0.6), ("none", 0.4)),
                _ => "none",
            };
            SetChoice(values, "wings.pattern", wp);
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            string wingKind = g.GetChoice("wings.kind");
            bool insect = wingKind == "insect" || form == "moth" || form == "dragonfly";
            bool bat = form == "bat";
            double size = g.Get("size");
            double baseL = DMath.Lerp(10, 30, DMath.Pow(size, 1.05));
            double mass = g.Get("body.mass");
            double bodyL = baseL * DMath.Lerp(0.55, 0.95, g.Get("body.length"));
            double bodyR = baseL * DMath.Lerp(0.2, 0.36, mass) * (insect ? 0.62 : 1.0);
            double tilt = DMath.Lerp(0, 60, g.Get("body.tilt")) * DMath.Deg2Rad * (insect ? 0.3 : 1.0);
            double legLen = baseL * DMath.Lerp(0.3, 0.85, g.Get("legs.length")) * (insect ? 0.7 : 1.0);

            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            // body axis from the tail end (pelvis) up/forward to the chest
            Vec3 axis = new Vec3(DMath.Cos(tilt), DMath.Sin(tilt), 0);
            double hipH = insect ? bodyR + legLen * 0.45 : Math.Max(bodyR * 0.9, legLen * 0.8 + bodyR * 0.25);
            Vec3 P = new Vec3(-bodyL * 0.3, hipH, 0);
            Vec3 C = P + axis * (bodyL * 0.55);
            int pelvis = b.BoneAlong("pelvis", root, P, axis, Vec3.UnitY, BoneRole.Pelvis, 0, bodyL * 0.3);
            int chest = b.BoneAlong("chest", pelvis, C, axis, Vec3.UnitY, BoneRole.Chest, 0, bodyL * 0.3);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, chest }, Lengths = new[] { bodyL * 0.55 }, TotalLength = bodyL * 0.55 };

            int body = b.Group("body", DMath.Clamp(bodyR * 0.35, 1.2, 3.0));
            double dom = bodyL + bodyR * 2;
            if (insect)
            {
                var th = b.Ellipsoid(body, chest, new Vec3(-bodyR * 0.2, 0, 0), new Vec3(bodyR * 1.1, bodyR * 0.95, bodyR * 0.95), MaterialSlot.Primary, PatternDomain.Body, 0.35, 0.6);
                th.DomainLength = dom; th.Tag = "thorax";
            }
            else
            {
                var belly = b.Ellipsoid(body, pelvis, new Vec3(bodyL * 0.12, -bodyR * 0.05, 0), new Vec3(bodyL * 0.42, bodyR * 0.95, bodyR * 0.95), MaterialSlot.Primary, PatternDomain.Body, 0.1, 0.55);
                belly.DomainLength = dom; belly.Tag = "belly";
                var breast = b.Ellipsoid(body, chest, new Vec3(-bodyR * 0.1, 0, 0), new Vec3(bodyR * 1.1, bodyR * 1.05, bodyR * 1.0), MaterialSlot.Primary, PatternDomain.Body, 0.5, 1.0);
                breast.DomainLength = dom; breast.Tag = "breast";
            }

            // ---- insect abdomen (sways as a spring chain)
            if (insect)
            {
                double abL = baseL * DMath.Lerp(0.35, 1.3, g.Get("abdomen.length"));
                double abR = bodyR * (form == "dragonfly" ? 0.5 : 0.95);
                Vec3 abBase = C + new Vec3(-bodyR * 0.9, -bodyR * 0.1, 0);
                Vec3 abDir = new Vec3(-1, form == "dragonfly" ? 0.02 : -0.15, 0).Normalized();
                int ab0 = b.BoneAlong("abdomen.0", chest, abBase, abDir, Vec3.UnitY, BoneRole.Tail, 0, abL);
                int ab1 = b.BoneAt("abdomen.1", ab0, abBase + abDir * abL, BoneRole.Tail);
                var abP = form == "dragonfly"
                    ? b.Cone(body, ab0, Vec3.Zero, abR, ab1, Vec3.Zero, Math.Max(0.5, abR * 0.6), MaterialSlot.Primary, PatternDomain.Body, 0, 0.35)
                    : b.Ellipsoid(body, ab0, new Vec3(abL * 0.5, 0, 0), new Vec3(abL * 0.55, abR, abR), MaterialSlot.Primary, PatternDomain.Body, 0, 0.35);
                abP.DomainLength = dom + abL; abP.Tag = "abdomen";
                b.Rig.Tail = new ChainRig
                {
                    Name = "abdomen", Bones = new[] { ab0, ab1 }, Lengths = new[] { abL }, TotalLength = abL, Radii = new[] { abR, abR * 0.6 },
                    Stiffness = 0.85, RestPitch = new[] { 0.0 }, RestYaw = new[] { 0.0 }, Droop = 0.1,
                };
                b.Rig.Tuning["tail.gravity"] = 6;
                if (g.GetChoice("tail.shape") == "stinger")
                {
                    var st = b.Cone(body, ab1, Vec3.Zero, Math.Max(0.5, abR * 0.5), ab1, abDir * Math.Max(1.5, abR * 1.6), 0.3, MaterialSlot.Accent);
                    st.Flags |= PrimFlags.Thin | PrimFlags.NoPattern;
                }
            }

            // ---- neck & head
            double headR = baseL * DMath.Lerp(0.12, 0.26, g.Get("head.size")) * (insect ? 0.8 : 1.0);
            double neckLen = insect ? 0 : baseL * DMath.Lerp(0.02, 0.35, g.Get("neck.length"));
            Vec3 N0 = C + axis * (bodyR * 0.6) + new Vec3(0, bodyR * 0.35, 0);
            Vec3 neckDir = Vec3.Lerp(Vec3.UnitY, Vec3.UnitX, 0.35).Normalized();
            int neck = b.BoneAlong("neck", chest, N0, neckDir, Vec3.UnitY, BoneRole.Neck, 0, Math.Max(0.5, neckLen));
            Vec3 Hj = N0 + neckDir * neckLen + (insect ? new Vec3(bodyR * 0.6, -bodyR * 0.4, 0) : Vec3.Zero);
            string eyeStyle = g.GetChoice("head.eyeStyle");
            double beak = g.Get("head.beak");
            bool birdBeak = wingKind == "feathered" && !insect;
            var hs = new HeadSpec
            {
                ParentBone = neck,
                Position = Hj,
                Forward = new Vec3(1, insect ? -0.35 : -0.05, 0),
                SkullLength = headR * 2.0,
                SkullHeight = headR * (form == "owl" ? 2.05 : 1.85),
                SkullWidth = headR * (form == "owl" ? 2.3 : 1.8),
                SkullOffset = 0.3,
                SnoutLength = birdBeak ? headR * DMath.Lerp(0.5, 2.6, beak) : headR * (bat ? DMath.Lerp(0.3, 1.0, beak) : 0.25),
                SnoutRadius = birdBeak ? headR * DMath.Lerp(0.32, 0.62, g.Get("head.beakDepth")) : headR * 0.55,
                SnoutTaper = birdBeak ? 0.22 : 0.7,
                SnoutDrop = birdBeak ? DMath.Lerp(0.05, 0.45, g.Get("head.beakCurve")) : 0.15,
                SnoutHeight = birdBeak ? -0.05 : -0.2,
                NoseSize = bat ? 1.0 : 0,
                DarkNose = true,
                SnoutMaterial = birdBeak ? MaterialSlot.Accent : MaterialSlot.Primary,
                SnoutPattern = !birdBeak,
                JawMaterial = birdBeak ? MaterialSlot.Accent : MaterialSlot.Primary,
                JawLength = birdBeak ? 0.9 : 0.8,
                JawRadius = headR * (birdBeak ? 0.25 : 0.3),
                JawDepth = 0.6,
                EyeSize = DMath.Clamp(DMath.Lerp(1.3, 4.2, g.Get("head.eyeSize")) * Math.Sqrt(headR / 4.0), 1.3, 5.5),
                EyeStyle = eyeStyle switch { "bead" => EyeStyle.Bead, "button" => EyeStyle.Button, "compound" => EyeStyle.Compound, "glow" => EyeStyle.Glow, _ => EyeStyle.Round },
                EyeForward = form == "owl" ? 0.95 : (insect ? 0.3 : 0.45),
                EyeHeight = 0.35,
                Teeth = birdBeak ? (g.Get("head.beakCurve") > 0.55 ? TeethKind.Beak : TeethKind.None) : (bat ? TeethKind.Fangs : (insect ? TeethKind.None : TeethKind.None)),
                TeethSize = DMath.Clamp(headR / 3.0, 0.8, 1.6),
                Group = -1,
                BlendRadius = DMath.Clamp(headR * 0.4, 1.0, 2.4),
                DomainLength = headR * 3,
                JawOpenMax = 0.55,
                Nostrils = bat,
            };
            var head = Heads.Build(b, hs);
            var nk = b.Cone(body, neck, Vec3.Zero, Math.Max(1.0, bodyR * 0.6), head.Head, head.SkullCenterLocal * 0.3, Math.Max(0.9, headR * 0.75),
                MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
            nk.DomainLength = neckLen + 2;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck, head.Head }, Lengths = new[] { Math.Max(0.5, neckLen) }, TotalLength = Math.Max(0.5, neckLen) };

            // owl facial disc: a light, flat mask around the eyes
            double disc = g.Get("head.disc");
            if (disc > 0.2 && !insect)
            {
                int dg = b.Group("disc", 0.6, 0, GroupFlags.NoContour, 0.15);
                var d = b.Ellipsoid(dg, head.Head, head.SkullCenterLocal + new Vec3(headR * 0.62, 0, 0), new Vec3(headR * 0.42, headR * 0.85 * disc, headR * 1.05 * disc),
                    MaterialSlot.Secondary, PatternDomain.None);
                d.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                d.Tag = "disc";
            }
            string crest = g.GetChoice("head.crest");
            if (crest != "none" && !insect) BuildCrest(b, crest, head, headR, g.Get("head.crestSize"));
            string ears = g.GetChoice("head.ears");
            if (ears != "none")
            {
                Ears.Build(b, new EarSpec
                {
                    Kind = QuadrupedFamily.ParseEar(ears),
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(-headR * 0.1, headR * 0.6, headR * 0.5),
                    Size = headR * DMath.Lerp(1.0, 2.6, g.Get("head.earSize")),
                    Width = 1.15,
                });
            }
            string ant = g.GetChoice("head.antennae");
            if (ant != "none")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var hx = b.RestWorld(head.Head);
                    var chain = Chains.Build(b, new ChainSpec
                    {
                        Name = "antenna." + (side < 0 ? "L" : "R"),
                        ParentBone = head.Head,
                        Base = hx.Point(head.SkullCenterLocal + new Vec3(headR * 0.5, headR * 0.7, side * headR * 0.4)),
                        Direction = new Vec3(0.55, 0.8, side * 0.45),
                        Length = baseL * (ant == "feathery" ? 0.45 : 0.55),
                        Segments = 3,
                        RadiusBase = ant == "feathery" ? 0.8 : 0.5,
                        RadiusTip = 0.35,
                        Bulge = ant == "feathery" ? 0.8 : 0,
                        Curl = -0.9,
                        Role = BoneRole.Antenna,
                        Domain = PatternDomain.Appendage,
                        Side = side,
                        Stiffness = 0.7,
                        Droop = 0.05,
                        Flags = PrimFlags.NoShadow | (ant == "feathery" ? PrimFlags.None : PrimFlags.Thin),
                    });
                    b.Rig.Appendages.Add(chain);
                }
            }

            // ---- wings
            double span = baseL * DMath.Lerp(0.9, 2.1, g.Get("wings.span"));
            double chord = baseL * DMath.Lerp(0.25, 0.6, g.Get("wings.chord"));
            double feathers = g.Get("wings.feathers");
            if (insect)
            {
                for (int pair = 0; pair < 2; pair++)
                {
                    Wings.BuildPair(b, new WingSpec
                    {
                        Kind = WingKind.Insect,
                        Name = "wing" + pair,
                        AnchorBone = chest,
                        Root = C + new Vec3(-bodyR * (0.1 + pair * 0.55), bodyR * 0.75, bodyR * 0.35),
                        Span = span * (pair == 0 ? 0.5 : 0.44),
                        Chord = chord * (form == "moth" ? 1.0 : 0.45) * (pair == 0 ? 1.0 : 0.85),
                        Raise = 0.25,
                        SweepBack = form == "moth" ? 0.2 + pair * 0.35 : 0.05 + pair * 0.25,
                        MembraneMaterial = form == "moth" ? MaterialSlot.Primary : MaterialSlot.Membrane,
                        Pair = pair,
                    });
                }
            }
            else
            {
                Wings.BuildPair(b, new WingSpec
                {
                    Kind = wingKind == "membrane" ? WingKind.Membrane : WingKind.Feathered,
                    AnchorBone = chest,
                    Root = C + axis * (bodyR * 0.2) + new Vec3(-bodyR * 0.25, bodyR * 0.45, bodyR * 0.55),
                    Span = span,
                    Chord = chord,
                    Raise = 0.3,
                    SweepBack = 0.3,
                    BoneRadius = Math.Max(0.6, bodyR * 0.2),
                    Fingers = (int)Math.Round(DMath.Lerp(2, 4, feathers)),
                    Primaries = (int)Math.Round(DMath.Lerp(2, 5, feathers)),
                    BodyBone = pelvis,
                    BodyAttach = P + new Vec3(bodyL * 0.05, bodyR * 0.2, bodyR * 0.55),
                    ClawSize = bat ? 0.8 : 0.0,
                    ThumbClaw = bat,
                });
            }

            // ---- legs (two for vertebrates, six small dangling legs for insects)
            double talons = g.Get("legs.talons");
            double legTh = g.Get("legs.thickness");
            if (insect)
            {
                for (int pair = 0; pair < 3; pair++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vec3 hip = C + new Vec3(-bodyR * (0.2 + pair * 0.35), -bodyR * 0.6, side * bodyR * 0.45);
                        Vec3 contact = new Vec3(hip.X + (1 - pair) * legLen * 0.35, 0, side * (bodyR + legLen * 0.45));
                        double len = Math.Max((contact - hip).Length * 1.3, legLen);
                        var leg = Limbs.Build(b, new LegSpec
                        {
                            Name = $"leg{pair}{(side < 0 ? "L" : "R")}",
                            Side = side, Pair = pair, Front = pair == 0, AnchorBone = chest, Hip = hip, Contact = contact,
                            Style = LegStyle.Arthropod, Length = len, Upper = 0.36, Lower = 0.38, Distal = 0.26,
                            RadiusTop = 0.6, RadiusKnee = 0.5, RadiusAnkle = 0.45, RadiusFoot = 0.4, Haunch = 0, PawRadius = 0.5,
                            LiftHeight = Math.Max(1.2, hipH * 0.4), BlendRadius = 0.5, DomainLength = len,
                        });
                        leg.Phase = DMath.Frac(pair / 3.0 * 0.5 + (side < 0 ? 0 : 0.5));
                        b.Rig.Legs.Add(leg);
                    }
                }
            }
            else
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 hip = P + new Vec3(bodyL * 0.12, -bodyR * 0.45, side * bodyR * 0.45);
                    Vec3 contact = new Vec3(hip.X + bodyR * 0.2, 0, side * bodyR * 0.5);
                    double rTop = Math.Max(0.7, bodyR * DMath.Lerp(0.16, 0.3, legTh));
                    double len = Math.Max(hip.Y * 1.25, 2.5);
                    var leg = Limbs.Build(b, new LegSpec
                    {
                        Name = "leg" + (side < 0 ? "L" : "R"),
                        Side = side, Pair = 0, Front = false, AnchorBone = pelvis, Hip = hip, Contact = contact,
                        Style = LegStyle.Digitigrade, Length = len, Upper = 0.34, Lower = 0.36, Distal = 0.3,
                        RadiusTop = rTop * 1.6, RadiusKnee = rTop, RadiusAnkle = rTop * 0.75, RadiusFoot = rTop * 0.7,
                        Haunch = 0.6,
                        FootMaterial = MaterialSlot.Accent,
                        PawLength = Math.Max(1.6, bodyR * 0.55), PawRadius = Math.Max(0.6, rTop * 0.8),
                        Claws = talons > 0.3 ? 3 : 0, ClawSize = DMath.Lerp(0.5, 1.4, talons),
                        LiftHeight = Math.Max(1.2, hip.Y * 0.35), MetaAngle = 14, BlendRadius = 0.6, DomainLength = len,
                        Phase = side < 0 ? 0.0 : 0.5,
                    });
                    b.Rig.Legs.Add(leg);
                }
            }

            // ---- tail
            string tail = g.GetChoice("tail.shape");
            double tailLen = baseL * DMath.Lerp(0.25, 0.9, g.Get("tail.length"));
            if (!insect && tail != "none")
            {
                if (bat)
                {
                    b.Rig.Tail = Chains.Build(b, new ChainSpec
                    {
                        Name = "tail", ParentBone = pelvis, Base = P + new Vec3(-bodyL * 0.2, 0, 0), Direction = new Vec3(-1, -0.3, 0),
                        Length = tailLen * 0.7, Segments = 3, RadiusBase = Math.Max(0.6, bodyR * 0.2), RadiusTip = 0.4, Group = body, Stiffness = 0.5, Droop = 0.5,
                    });
                }
                else
                {
                    // feathered tail: fan of feather plates on a short stiff chain
                    int tg = b.Group("tailfeathers", 0.6, 0, GroupFlags.NoFarShade, -0.2);
                    Vec3 tb = P + new Vec3(-bodyL * 0.3, bodyR * 0.2, 0);
                    Vec3 tdir = new Vec3(-1, -0.15 + tilt * 0.4, 0).Normalized();
                    int t0 = b.BoneAlong("tail.0", pelvis, tb, tdir, Vec3.UnitY, BoneRole.Tail, 0, tailLen);
                    int t1 = b.BoneAt("tail.1", t0, tb + tdir * tailLen, BoneRole.Tail);
                    double w = Math.Max(1.5, bodyR * (tail == "long" ? 0.5 : 0.85));
                    int nf = tail == "forked" ? 2 : 3;
                    for (int k = 0; k < nf; k++)
                    {
                        double u = nf == 1 ? 0 : k / (double)(nf - 1) - 0.5;
                        double len = tailLen * (tail == "forked" ? 1.0 + Math.Abs(u) * 0.8 : (tail == "long" ? 1.35 - Math.Abs(u) * 0.4 : 1.0 - Math.Abs(u) * 0.25));
                        Vec3 tip = new Vec3(len, 0, u * w * 2.4);
                        var tri = b.Triangle(tg, t0, new Vec3(0, 0, -w * 0.35 + u * w), t0, tip, t0, new Vec3(0, 0, w * 0.35 + u * w), k == nf / 2 ? MaterialSlot.Primary : MaterialSlot.Primary, PatternDomain.Tail);
                        tri.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow;
                        tri.DomainLength = tailLen;
                        tri.Tag = "tailfeather";
                    }
                    b.Rig.Tail = new ChainRig
                    {
                        Name = "tail", Bones = new[] { t0, t1 }, Lengths = new[] { tailLen }, TotalLength = tailLen, Radii = new[] { 0.8, 0.6 },
                        Stiffness = 0.8, RestPitch = new[] { 0.0 }, RestYaw = new[] { 0.0 }, Droop = 0.2,
                    };
                }
            }

            // ---- rig semantics & metrics
            bool hover = insect || bat;
            b.Rig.Locomotion = LocomotionKind.Fly;
            b.Rig.LocomotionModule = "flight";
            b.Rig.Tuning["flight.hover"] = hover ? 1 : 0;
            b.Rig.Tuning["flight.flapHz"] = insect ? DMath.Lerp(7.5, 5.5, size) : (bat ? 4.2 : DMath.Lerp(4.2, 2.4, size));
            b.Rig.Altitude = hover ? Math.Max(8, hipH * 0.6 + baseL * 0.55) : Math.Max(10, hipH + baseL * 0.8);
            b.Rig.Tuning["tail.gravity"] = b.Rig.Tune("tail.gravity", 15);
            b.Rig.Action = insect ? (tail == "stinger" ? ActionStyle.Sting : ActionStyle.WingBuffet) : ActionStyle.WingBuffet;
            b.Rig.Rest = RestStyle.Perch;
            b.Rig.Tuning["gait.walkDuty"] = 0.6;
            b.Rig.Tuning["gait.runDuty"] = 0.4;
            b.Rig.Tuning["rest.bodyHeight"] = Math.Max(bodyR * 0.9, 1.5);
            b.Metrics.LegLength = Math.Max(2.5, hipH);
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = hipH + bodyR;
            b.Metrics.BodyLength = bodyL + headR * 2 + (insect ? baseL * 0.5 : tailLen * 0.5);
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * DMath.Lerp(0.4, 1.0, mass);
            double legL = Math.Max(3, hipH);
            b.Metrics.WalkSpeed = hover ? Math.Sqrt(0.5 * 400 * baseL * 0.3) : Math.Sqrt(0.3 * 400 * legL);
            b.Metrics.RunSpeed = Math.Sqrt(3.0 * 400 * baseL * 0.4) * DMath.Lerp(0.85, 1.2, g.GetOr("motion.speed", 0.5));
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 22.0), 0.7, 1.4);
            b.AnimationMargin = Math.Max(6, span * 0.12);
            b.ExtraHeadroom = Math.Max(8, span * 0.95 + b.Rig.Altitude);
            return b.Build(g.AnatomyKey);
        }

        private static void BuildCrest(AnatomyBuilder b, string crest, HeadResult head, double headR, double size)
        {
            var hx = b.RestWorld(head.Head);
            if (crest == "tufts")
            {
                Ears.Build(b, new EarSpec
                {
                    Kind = EarKind.Pointed,
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(headR * 0.1, headR * 0.8, headR * 0.6),
                    Size = headR * DMath.Lerp(0.7, 1.4, size),
                    Width = 0.9,
                    Name = "tuft",
                });
                return;
            }
            // feather crest: a few fanned feather plates rising from the crown
            int g = b.Group("crest", 0.4, 0, GroupFlags.None, -0.2);
            int n = crest == "crest" ? 4 : 3;
            for (int i = 0; i < n; i++)
            {
                double a = DMath.Lerp(-0.2, 0.9, i / (double)Math.Max(1, n - 1));
                Vec3 basePos = head.SkullCenterLocal + new Vec3(headR * (0.3 - i * 0.35), headR * 0.75, 0);
                double len = headR * DMath.Lerp(0.8, 1.8, size) * (crest == "crest" ? 1.0 - i * 0.12 : 1.0);
                Vec3 dir = new Vec3(-DMath.Sin(a) , DMath.Cos(a), 0);
                var t = b.Triangle(g, head.Head, basePos + new Vec3(0.6, 0, 0), head.Head, basePos + dir * len, head.Head, basePos - new Vec3(0.6, 0, 0),
                    MaterialSlot.Marking, PatternDomain.None);
                t.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern | PrimFlags.NoShadow;
            }
        }
    }
}
