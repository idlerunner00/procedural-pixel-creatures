// Procedural Pixel Creatures - family 7: aquatic creatures (reef fish, sharks, rays, pufferfish,
// anglerfish, eels). Streamlined bodies with paired and median fins that swim in open water.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class AquaticFamily : CreatureFamilyBase
    {
        public override string Id => "aquatic";
        public override string DisplayName => "Aquatic Creatures";
        public override string Description => "Reef fish, sharks, rays, pufferfish, anglerfish and eels: streamlined bodies with paired and unpaired fins, tail strokes or undulating pectoral fins, hovering in open water and resting on the bottom.";
        public override int Order => 70;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("reef", "Reef fish", 1.3),
            new ArchetypeInfo("shark", "Shark", 1.0),
            new ArchetypeInfo("ray", "Ray", 0.9),
            new ArchetypeInfo("puffer", "Pufferfish", 0.8),
            new ArchetypeInfo("angler", "Anglerfish", 0.8),
            new ArchetypeInfo("eel", "Eel", 0.8),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("reef", "Reef fish"), ("shark", "Shark"), ("ray", "Ray"), ("puffer", "Pufferfish"), ("angler", "Anglerfish"), ("eel", "Eel"),
        };
        public static readonly (string, string)[] SwimStyles = { ("tail", "Tail"), ("wings", "Wings"), ("undulate", "Undulate") };
        public static readonly (string, string)[] DorsalShapes = { ("none", "None"), ("pointed", "Pointed"), ("blade", "Blade"), ("sail", "Sail"), ("spines", "Spines") };
        public static readonly (string, string)[] TailShapes = { ("fan", "Fan"), ("forked", "Forked"), ("crescent", "Crescent"), ("pointed", "Pointed"), ("whip", "Whip") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs"), ("sabre", "Sabre") };
        public static readonly (string, string)[] EyeStyles = { ("round", "Round"), ("bead", "Beady"), ("button", "Large & cute"), ("glow", "Glowing") };
        public static readonly (string, string)[] Extras = { ("none", "None"), ("lure", "Lure"), ("barbels", "Barbels"), ("spikes", "Spikes") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "reef", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("body.length", "Body length", 0, 1, 0.5, "", 0.1)
             .Float("body.depth", "Body depth", 0, 1, 0.5, "Flat like an eel to a deep-bodied fish.", 0.1)
             .Float("body.width", "Body width", 0, 1, 0.45, "Laterally flattened to round.", 0.1)
             .Choice("swim.style", "Swim style", SwimStyles, "tail", "", 0.05);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("head.snout", "Head snout", 0, 1, 0.4, "", 0.1)
             .Float("head.jaw", "Head jaw", 0, 1, 0.45, "", 0.1)
             .Choice("head.teeth", "Head teeth", TeethKinds, "none", "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.55, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "round", "", 0.08)
             .Choice("head.extra", "Head extra", Extras, "none", "Glowing lure, barbels or spines.", 0.1);
            b.Group("fins", "Fins", GeneStream.Anatomy)
             .Float("fins.pectoral", "Fins pectoral", 0, 1, 0.5, "", 0.1)
             .Float("fins.pelvic", "Fins pelvic", 0, 1, 0.35, "", 0.1)
             .Choice("fins.dorsalShape", "Fins dorsal shape", DorsalShapes, "pointed", "", 0.1)
             .Float("fins.dorsal", "Fins dorsal", 0, 1, 0.5, "", 0.1)
             .Float("fins.anal", "Fins anal", 0, 1, 0.3, "", 0.1);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Float("tail.length", "Tail length", 0, 1, 0.45, "", 0.1)
             .Choice("tail.shape", "Tail shape", TailShapes, "fan", "", 0.1)
             .Float("tail.size", "Tail size", 0, 1, 0.5, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "scales");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Len, Depth, Width, HeadSize, Snout, Jaw, EyeSize, Pect, Pelvic, Dorsal, Anal, TailLen, TailSize;
            public string Style = "tail";
            public (string, double)[] Teeth = { ("none", 1) }, Eyes = { ("round", 1) }, Extra = { ("none", 1) }, DorsalShape = { ("pointed", 1) }, Tail = { ("fan", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "shark" => new Prior
            {
                Size = 0.7, Len = 0.8, Depth = 0.35, Width = 0.5, HeadSize = 0.5, Snout = 0.75, Jaw = 0.65, EyeSize = 0.3, Pect = 0.8, Pelvic = 0.3, Dorsal = 0.8, Anal = 0.2, TailLen = 0.6, TailSize = 0.7,
                Teeth = new[] { ("fangs", 3.0), ("small", 1.0) }, Eyes = new[] { ("bead", 4.0), ("glow", 0.3) }, DorsalShape = new[] { ("blade", 4.0) }, Tail = new[] { ("crescent", 4.0), ("forked", 1.0) },
            },
            "ray" => new Prior
            {
                Size = 0.6, Len = 0.45, Depth = 0.1, Width = 0.95, HeadSize = 0.4, Snout = 0.3, Jaw = 0.3, EyeSize = 0.35, Pect = 1.0, Pelvic = 0.2, Dorsal = 0.1, Anal = 0.0, TailLen = 0.9, TailSize = 0.3,
                Style = "wings", Eyes = new[] { ("bead", 3.0), ("round", 1.0) }, DorsalShape = new[] { ("none", 4.0) }, Tail = new[] { ("whip", 4.0) },
            },
            "puffer" => new Prior
            {
                Size = 0.35, Len = 0.2, Depth = 0.9, Width = 0.95, HeadSize = 0.55, Snout = 0.15, Jaw = 0.3, EyeSize = 0.8, Pect = 0.45, Pelvic = 0.0, Dorsal = 0.35, Anal = 0.3, TailLen = 0.25, TailSize = 0.45,
                Eyes = new[] { ("button", 3.0), ("round", 1.0) }, Extra = new[] { ("spikes", 4.0), ("none", 1.0) }, DorsalShape = new[] { ("pointed", 2.0), ("none", 1.0) }, Tail = new[] { ("fan", 4.0) },
            },
            "angler" => new Prior
            {
                Size = 0.5, Len = 0.35, Depth = 0.75, Width = 0.7, HeadSize = 0.95, Snout = 0.2, Jaw = 0.95, EyeSize = 0.4, Pect = 0.4, Pelvic = 0.2, Dorsal = 0.3, Anal = 0.3, TailLen = 0.3, TailSize = 0.4,
                Teeth = new[] { ("sabre", 3.0), ("fangs", 2.0) }, Eyes = new[] { ("bead", 2.0), ("glow", 2.0) }, Extra = new[] { ("lure", 5.0) }, DorsalShape = new[] { ("spines", 2.0), ("none", 1.0) }, Tail = new[] { ("fan", 3.0) },
            },
            "eel" => new Prior
            {
                Size = 0.55, Len = 0.95, Depth = 0.15, Width = 0.5, HeadSize = 0.4, Snout = 0.55, Jaw = 0.55, EyeSize = 0.35, Pect = 0.25, Pelvic = 0.0, Dorsal = 0.4, Anal = 0.4, TailLen = 1.0, TailSize = 0.35,
                Style = "undulate", Teeth = new[] { ("small", 2.0), ("fangs", 1.0) }, Eyes = new[] { ("bead", 3.0), ("round", 1.0) }, Extra = new[] { ("none", 3.0), ("barbels", 1.0) },
                DorsalShape = new[] { ("sail", 3.0), ("none", 1.0) }, Tail = new[] { ("pointed", 3.0), ("fan", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.35, Len = 0.35, Depth = 0.75, Width = 0.35, HeadSize = 0.5, Snout = 0.3, Jaw = 0.35, EyeSize = 0.65, Pect = 0.55, Pelvic = 0.45, Dorsal = 0.6, Anal = 0.5, TailLen = 0.35, TailSize = 0.6,
                Eyes = new[] { ("round", 3.0), ("button", 1.0) }, Extra = new[] { ("none", 4.0), ("barbels", 0.6) },
                DorsalShape = new[] { ("pointed", 2.0), ("sail", 1.5), ("spines", 1.0) }, Tail = new[] { ("fan", 2.0), ("forked", 2.0), ("crescent", 0.6) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "body.length", p.Len, sp);
            Around(v, rng, "body.depth", p.Depth, sp);
            Around(v, rng, "body.width", p.Width, sp);
            SetChoice(v, "swim.style", p.Style);
            Around(v, rng, "head.size", p.HeadSize, sp);
            Around(v, rng, "head.snout", p.Snout, sp);
            Around(v, rng, "head.jaw", p.Jaw, sp);
            SetChoice(v, "head.teeth", PickWeighted(rng, p.Teeth));
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "head.extra", PickWeighted(rng, p.Extra));
            Around(v, rng, "fins.pectoral", p.Pect, sp);
            Around(v, rng, "fins.pelvic", p.Pelvic, sp);
            SetChoice(v, "fins.dorsalShape", PickWeighted(rng, p.DorsalShape));
            Around(v, rng, "fins.dorsal", p.Dorsal, sp);
            Around(v, rng, "fins.anal", p.Anal, sp);
            Around(v, rng, "tail.length", p.TailLen, sp);
            SetChoice(v, "tail.shape", PickWeighted(rng, p.Tail));
            Around(v, rng, "tail.size", p.TailSize, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["scales"] = 5, ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["bands"] = 1.5, ["stripes"] = 1, ["spots"] = 1.2, ["none"] = 1, ["dorsal"] = 0.6, ["blotches"] = 0.6 },
                Accents = new Dictionary<string, double> { ["bone"] = 2, ["vivid"] = 1.2, ["gold"] = 0.6, ["dark"] = 1 },
                // reef fish: vivid colours, clownfish orange, tang blue, yellow, wrasse green, violet, pink
                Coats = new[] { Coats.Vivid(2), Coats.Blue(1.2), Coats.Yellow(1), Coats.Teal(1), Coats.Ginger(1), Coats.Crimson(0.8), Coats.Violet(0.8), Coats.Pink(0.5), Coats.Green(0.5), Coats.Silver(0.5) },
                CountershadeMin = 0.4, CountershadeMax = 0.85,
                GlowChance = 0.12,
                SocksChance = 0.0, MaskChance = 0.08, PiebaldChance = 0.03,
            };
            switch (arch)
            {
                case "shark":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 5 };
                    h.Coats = new[] { Coats.Grey(2), Coats.Slate(1.6), Coats.Silver(1.2), Coats.Blue(0.4), Coats.Sand(0.4), Coats.Brown(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 4, ["spots"] = 0.6, ["bands"] = 0.5 };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 3, ["dark"] = 1 };
                    h.CountershadeMin = 0.65; h.CountershadeMax = 0.9;
                    h.MaskChance = 0.0;
                    h.EnergyMax = 0.6;
                    break;
                case "ray":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 5 };
                    h.Coats = new[] { Coats.Sand(1.2), Coats.Grey(1.2), Coats.Brown(1), Coats.Slate(0.8), Coats.Black(0.6), Coats.Blue(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 2, ["none"] = 2, ["mottled"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 3, ["dark"] = 1 };
                    h.MaskChance = 0.0;
                    break;
                case "puffer":
                    h.Coats = new[] { Coats.Yellow(1.2), Coats.Sand(1.2), Coats.Golden(0.8), Coats.Brown(0.6), Coats.Teal(0.6), Coats.Grey(0.5), Coats.Vivid(0.6) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 3, ["none"] = 1 };
                    break;
                case "angler":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 3, ["slime"] = 2 };
                    h.Coats = new[] { Coats.Brown(1.2), Coats.Chocolate(1), Coats.Black(1), Coats.Violet(0.6), Coats.Slate(0.6), Coats.Rust(0.5) };
                    h.GlowChance = 1.0;
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["mottled"] = 2, ["spots"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 3, ["dark"] = 1 };
                    break;
                case "eel":
                    h.Materials = new Dictionary<string, double> { ["slime"] = 3, ["hide"] = 2 };
                    h.Coats = new[] { Coats.Olive(1.4), Coats.Brown(1), Coats.Green(1), Coats.Grey(0.8), Coats.Black(0.6), Coats.Teal(0.5), Coats.Yellow(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["mottled"] = 2, ["spots"] = 1, ["none"] = 1.5, ["bands"] = 1 };
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            bool glowEye = Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow";
            bool lure = Schema.Get("head.extra").Options[(int)values[Schema.IndexOf("head.extra")]].Id == "lure";
            if (glowEye || lure) Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            string style = g.GetChoice("swim.style");
            bool ray = style == "wings";
            double size = g.Get("size");
            double baseL = DMath.Lerp(12, 38, DMath.Pow(size, 1.05));
            double L = baseL * DMath.Lerp(0.55, 1.1, g.Get("body.length"));
            double H = baseL * DMath.Lerp(0.12, 0.42, g.Get("body.depth"));
            double W = baseL * DMath.Lerp(0.1, 0.34, g.Get("body.width"));
            if (ray) { H = Math.Max(1.6, baseL * 0.1); W = baseL * DMath.Lerp(0.25, 0.4, g.Get("body.width")); }
            H = Math.Max(1.5, H);
            W = Math.Max(1.4, W);

            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            double y0 = H + 0.6;
            Vec3 P = new Vec3(-L * 0.28, y0, 0);
            Vec3 C = new Vec3(L * 0.22, y0, 0);
            int pelvis = b.BoneAlong("pelvis", root, P, Vec3.UnitX, Vec3.UnitY, BoneRole.Pelvis, 0, L * 0.25);
            int spine = b.BoneAlong("spine", pelvis, (P + C) * 0.5, Vec3.UnitX, Vec3.UnitY, BoneRole.Spine, 0, L * 0.25);
            int chest = b.BoneAlong("chest", spine, C, Vec3.UnitX, Vec3.UnitY, BoneRole.Chest, 0, L * 0.3);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { L * 0.25, L * 0.25 }, TotalLength = L * 0.5 };

            int body = b.Group("body", DMath.Clamp(Math.Min(H, W) * 0.45, 1.2, 3.5));
            double dom = L * 1.6;
            var front = b.Ellipsoid(body, chest, new Vec3(-L * 0.05, 0, 0), new Vec3(L * 0.34, H, W), MaterialSlot.Primary, PatternDomain.Body, 0.45, 0.95);
            front.DomainLength = dom; front.Tag = "front";
            var rear = b.Ellipsoid(body, pelvis, new Vec3(0, 0, 0), new Vec3(L * 0.38, H * 0.85, W * 0.8), MaterialSlot.Primary, PatternDomain.Body, 0.05, 0.5);
            rear.DomainLength = dom; rear.Tag = "rear";

            // ---- head (in the body group: fish heads blend into the body)
            double headSize = g.Get("head.size");
            double skullL = Math.Max(3.0, Math.Max(H, W) * DMath.Lerp(1.1, 2.0, headSize));
            if (ray) skullL = Math.Max(3.0, W * 0.9);
            string teeth = g.GetChoice("head.teeth");
            string eye = g.GetChoice("head.eyeStyle");
            double jaw = g.Get("head.jaw");
            Vec3 Hj = C + new Vec3(L * 0.18, 0, 0);
            var hs = new HeadSpec
            {
                ParentBone = chest,
                Position = Hj,
                Forward = Vec3.UnitX,
                SkullLength = skullL,
                SkullHeight = Math.Max(2.4, H * (form == "angler" ? 2.2 : 1.7) * (ray ? 1.2 : 1.0)),
                SkullWidth = Math.Max(2.2, W * 1.7),
                SkullOffset = 0.2,
                SnoutLength = skullL * DMath.Lerp(0.15, 1.0, g.Get("head.snout")),
                SnoutRadius = Math.Max(1.0, Math.Min(H, W) * 0.7),
                SnoutTaper = 0.7,
                SnoutDrop = form == "shark" ? -0.15 : 0.0,
                SnoutHeight = form == "shark" ? 0.05 : -0.1,
                NoseSize = 0,
                JawLength = DMath.Lerp(0.7, 1.05, jaw),
                JawRadius = Math.Max(0.9, H * DMath.Lerp(0.35, 0.8, jaw)),
                JawDepth = DMath.Lerp(0.6, 1.4, jaw),
                EyeSize = DMath.Clamp(DMath.Lerp(1.3, 3.8, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 7.0), 1.3, 5.0),
                EyeStyle = QuadrupedFamily.ParseEye(eye),
                EyeForward = 0.18,
                EyeHeight = ray ? 0.85 : 0.35,
                Teeth = teeth switch { "small" => TeethKind.Small, "fangs" => TeethKind.Fangs, "sabre" => TeethKind.Sabre, _ => TeethKind.None },
                TeethSize = DMath.Clamp(skullL / 7.0, 0.8, 1.8),
                Group = body,
                DomainLength = dom,
                JawOpenMax = form == "angler" ? 1.0 : 0.6,
                Nostrils = false,
            };
            var head = Heads.Build(b, hs);

            string extra = g.GetChoice("head.extra");
            if (extra == "lure")
            {
                var hx = b.RestWorld(head.Head);
                var lure = Chains.Build(b, new ChainSpec
                {
                    Name = "lure",
                    ParentBone = head.Head,
                    Base = hx.Point(head.SkullCenterLocal + new Vec3(skullL * 0.1, hs.SkullHeight * 0.48, 0)),
                    Direction = new Vec3(0.55, 1, 0),
                    Length = skullL * 1.2,
                    Segments = 3,
                    RadiusBase = 0.6,
                    RadiusTip = 0.4,
                    Curl = -1.6,
                    Role = BoneRole.Antenna,
                    Domain = PatternDomain.Appendage,
                    Stiffness = 0.55,
                    Droop = 0.3,
                    Flags = PrimFlags.Thin | PrimFlags.NoShadow,
                });
                b.Rig.Appendages.Add(lure);
                int tip = lure.Bones[lure.Bones.Length - 1];
                var bulb = b.Ellipsoid(b.Group("lure.bulb", 0.4, 0, GroupFlags.None, 0.5), tip, new Vec3(0.4, 0, 0), new Vec3(1.2, 1.2, 1.2) * Math.Max(1.0, skullL * 0.12),
                    MaterialSlot.Glow, PatternDomain.None);
                bulb.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
                b.Trait("lure");
            }
            else if (extra == "barbels")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var hx = b.RestWorld(head.Head);
                    var bar = Chains.Build(b, new ChainSpec
                    {
                        Name = "barbel." + (side < 0 ? "L" : "R"),
                        ParentBone = head.Head,
                        Base = hx.Point(head.SnoutTipLocal + new Vec3(-hs.SnoutRadius * 0.4, -hs.SnoutRadius * 0.5, side * hs.SnoutRadius * 0.5)),
                        Direction = new Vec3(-0.3, -0.6, side * 0.8),
                        Length = skullL * 0.9,
                        Segments = 3, RadiusBase = 0.5, RadiusTip = 0.35, Curl = 0.6, Side = side,
                        Role = BoneRole.Antenna, Domain = PatternDomain.Appendage, Stiffness = 0.35, Droop = 0.35, Flags = PrimFlags.Thin | PrimFlags.NoShadow,
                    });
                    b.Rig.Appendages.Add(bar);
                }
            }

            // ---- tail (the caudal fin sits on a spring chain: it lags and whips with the beat)
            double tailLen = L * DMath.Lerp(0.2, 0.55, g.Get("tail.length")) * (form == "eel" ? 1.8 : 1.0) * (ray ? 1.6 : 1.0);
            string tailShape = g.GetChoice("tail.shape");
            double tailSize = g.Get("tail.size");
            double tr0 = Math.Max(0.8, Math.Min(H, W) * (ray ? 0.35 : 0.55));
            var tspec = new ChainSpec
            {
                Name = "tail",
                ParentBone = pelvis,
                Base = P + new Vec3(-L * 0.3, 0, 0),
                Direction = new Vec3(-1, 0, 0),
                Length = tailLen,
                Segments = DMath.Clamp((int)Math.Round(tailLen / 3.0), 3, 10),
                RadiusBase = tr0,
                RadiusTip = Math.Max(0.45, tr0 * (tailShape == "whip" ? 0.2 : 0.45)),
                Tip = tailShape switch { "whip" => TailTip.Plain, "pointed" => TailTip.Plain, _ => TailTip.Fin },
                TipSize = DMath.Clamp(H * DMath.Lerp(0.35, 0.8, tailSize), 0.9, 4.5),
                Group = body,
                Stiffness = form == "eel" ? 0.35 : 0.6,
                Droop = 0.0,
            };
            var tail = Chains.Build(b, tspec);
            b.Rig.Tail = tail;
            b.Rig.Tuning["tail.gravity"] = 0;
            int tailTip = tail.Bones[tail.Bones.Length - 1];
            int tailPrev = tail.Bones[tail.Bones.Length - 2];
            if (tailShape == "crescent" || tailShape == "forked" || tailShape == "pointed")
                BuildCaudal(b, tailShape, tailPrev, tailTip, H, tailSize);

            // ---- fins
            double pect = g.Get("fins.pectoral");
            if (ray)
            {
                // ray wings: broad flat paddles along the flanks that flap together (with a slight ripple)
                double span = baseL * DMath.Lerp(0.45, 0.8, pect);
                Fins.Build(b, new FinSpec
                {
                    Name = "raywing",
                    Kind = FinKind.RayWing,
                    Shape = FinShape.Pointed,
                    AnchorBone = spine,
                    Base = (P + C) * 0.5 + new Vec3(L * 0.08, 0, W * 0.75),
                    Direction = new Vec3(-0.3, 0, 1),
                    Length = span,
                    Width = L * 1.05,
                    Material = MaterialSlot.Primary,
                    Axis = Vec3.UnitZ,
                    Amplitude = 0.6,
                    SidePhase = 0,
                });
            }
            else if (pect > 0.1)
            {
                Fins.Build(b, new FinSpec
                {
                    Name = "pectoral",
                    Kind = FinKind.Pectoral,
                    Shape = form == "shark" ? FinShape.Blade : FinShape.Paddle,
                    AnchorBone = chest,
                    Base = C + new Vec3(-L * 0.02, -H * 0.35, W * 0.8),
                    Direction = form == "shark" ? new Vec3(-0.5, -0.45, 1) : new Vec3(-0.7, -0.2, 0.7),
                    Length = Math.Max(2.0, H * DMath.Lerp(0.6, 1.4, pect)),
                    Width = Math.Max(1.5, H * 0.7),
                    Axis = Vec3.UnitZ,
                    Amplitude = 0.55,
                });
            }
            double pelvic = g.Get("fins.pelvic");
            if (pelvic > 0.2 && !ray)
            {
                Fins.Build(b, new FinSpec
                {
                    Name = "pelvic", Kind = FinKind.Pelvic, Shape = FinShape.Pointed, AnchorBone = spine,
                    Base = (P + C) * 0.5 + new Vec3(0, -H * 0.85, W * 0.45), Direction = new Vec3(-0.6, -1, 0.35),
                    Length = Math.Max(1.5, H * DMath.Lerp(0.4, 0.9, pelvic)), Width = Math.Max(1.2, H * 0.5),
                    Axis = Vec3.UnitZ, Amplitude = 0.3, Phase = 1.2,
                });
            }
            string dorsalShape = g.GetChoice("fins.dorsalShape");
            double dorsal = g.Get("fins.dorsal");
            if (dorsalShape != "none" && dorsal > 0.08)
            {
                if (dorsalShape == "sail" || dorsalShape == "spines")
                {
                    var ds = new DorsalSpec
                    {
                        Kind = dorsalShape == "sail" ? DorsalKind.Sail : DorsalKind.Spikes,
                        Size = Math.Max(1.5, H * DMath.Lerp(0.5, 1.5, dorsal)),
                        Material = dorsalShape == "sail" ? MaterialSlot.Membrane : MaterialSlot.Accent,
                    };
                    int n = form == "eel" ? 7 : 5;
                    for (int i = 0; i < n; i++)
                    {
                        double t = (i + 0.5) / n;
                        double x = DMath.Lerp(-L * 0.45, L * 0.3, t);
                        double top = y0 + (x > 0 ? H * Math.Sqrt(Math.Max(0, 1 - DMath.Sq((x - C.X + L * 0.05) / (L * 0.34)))) : H * 0.85 * Math.Sqrt(Math.Max(0, 1 - DMath.Sq((x - P.X) / (L * 0.38)))));
                        double scale = dorsalShape == "sail" ? 0.5 + 0.7 * DMath.Sin(DMath.Pi * t) : 0.8;
                        ds.Anchors.Add((t < 0.5 ? pelvis : chest, new Vec3(x, Math.Max(y0, top) - 0.3, 0), Vec3.UnitY, scale));
                    }
                    Dorsal.Build(b, ds);
                }
                else
                {
                    Fins.Build(b, new FinSpec
                    {
                        Name = "dorsal", Kind = FinKind.Dorsal, Paired = false, Animated = false,
                        Shape = dorsalShape == "blade" ? FinShape.Blade : FinShape.Pointed,
                        AnchorBone = spine, Base = (P + C) * 0.5 + new Vec3(L * 0.06, H * 0.82, 0), Direction = new Vec3(-0.45, 1, 0),
                        Length = Math.Max(2.0, H * DMath.Lerp(0.6, 1.6, dorsal)), Width = Math.Max(2.0, L * 0.28),
                    });
                }
            }
            double anal = g.Get("fins.anal");
            if (anal > 0.2 && !ray)
            {
                Fins.Build(b, new FinSpec
                {
                    Name = "anal", Kind = FinKind.Anal, Paired = false, Animated = false, Shape = FinShape.Pointed,
                    AnchorBone = pelvis, Base = P + new Vec3(-L * 0.1, -H * 0.7, 0), Direction = new Vec3(-0.6, -1, 0),
                    Length = Math.Max(1.5, H * DMath.Lerp(0.4, 1.0, anal)), Width = Math.Max(1.5, L * 0.2),
                });
            }
            if (extra == "spikes")
            {
                var ds = new DorsalSpec { Kind = DorsalKind.Quills, Size = Math.Max(1.2, H * 0.4), Material = MaterialSlot.Accent };
                for (int i = 0; i < 12; i++)
                {
                    double a = DMath.Lerp(-2.2, 2.2, (i % 6) / 5.0);
                    double x = i < 6 ? L * 0.12 : -L * 0.22;
                    Vec3 nrm = new Vec3(0.1, DMath.Cos(a), DMath.Sin(a)).Normalized();
                    Vec3 pos = new Vec3(x, y0, 0) + new Vec3(0, DMath.Cos(a) * H * 0.95, DMath.Sin(a) * W * 0.95);
                    ds.Anchors.Add((i < 6 ? chest : pelvis, pos, nrm, 0.9));
                }
                Dorsal.Build(b, ds);
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Swim;
            b.Rig.LocomotionModule = "swim";
            b.Rig.Tuning["swim.style"] = ray ? SwimLocomotion.Rajiform : (style == "undulate" ? SwimLocomotion.Anguilliform : SwimLocomotion.Carangiform);
            b.Rig.Altitude = Math.Max(5, H * 1.2 + baseL * 0.35);
            b.Rig.Action = form == "puffer" ? ActionStyle.Spray : ActionStyle.Bite;
            if (form == "angler") b.Rig.Tuning["idle.jaw"] = 0.3;
            b.Rig.Rest = RestStyle.Settle;
            b.Metrics.LegLength = Math.Max(3, H);
            b.Metrics.HipHeight = y0;
            b.Metrics.BackHeight = y0 + H;
            b.Metrics.BodyLength = L + tailLen * 0.6 + skullL * 0.5;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * 0.8;
            double spd = DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * (form == "puffer" ? 0.6 : 1.0);
            b.Metrics.WalkSpeed = Math.Sqrt(0.6 * 400 * baseL * 0.3) * spd;
            b.Metrics.RunSpeed = Math.Sqrt(2.6 * 400 * baseL * 0.35) * spd;
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 24.0), 0.7, 1.5);
            b.AnimationMargin = Math.Max(6, baseL * 0.25);
            b.ExtraHeadroom = Math.Max(6, b.Rig.Altitude + H);
            return b.Build(g.AnatomyKey);
        }

        /// <summary>Caudal fins with lobes (crescent: shark/tuna, forked, pointed).</summary>
        private static void BuildCaudal(AnatomyBuilder b, string shape, int prev, int tip, double H, double size)
        {
            int g = b.Group("caudal", 0.4, 0, GroupFlags.NoFarShade, -0.1);
            double k = H * DMath.Lerp(0.8, 1.7, size);
            PrimitiveDef Tri(Vec3 a, Vec3 c, Vec3 d)
            {
                var t = b.Triangle(g, prev, a, tip, c, tip, d, MaterialSlot.Membrane, PatternDomain.Fin);
                t.Flags |= PrimFlags.Translucent | PrimFlags.TwoSided;
                t.DomainLength = k;
                return t;
            }
            switch (shape)
            {
                case "crescent":
                    Tri(new Vec3(0, H * 0.2, 0), new Vec3(k * 1.0, k * 1.1, 0), new Vec3(k * 0.35, 0, 0));
                    Tri(new Vec3(0, -H * 0.2, 0), new Vec3(k * 0.8, -k * 0.8, 0), new Vec3(k * 0.35, 0, 0));
                    break;
                case "forked":
                    Tri(new Vec3(0, H * 0.15, 0), new Vec3(k * 0.9, k * 0.75, 0), new Vec3(k * 0.4, 0, 0));
                    Tri(new Vec3(0, -H * 0.15, 0), new Vec3(k * 0.9, -k * 0.75, 0), new Vec3(k * 0.4, 0, 0));
                    break;
                default: // pointed
                    Tri(new Vec3(0, H * 0.3, 0), new Vec3(k * 1.2, 0, 0), new Vec3(0, -H * 0.3, 0));
                    break;
            }
        }
    }
}
