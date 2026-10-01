// Procedural Pixel Creatures - extension family "plantfolk": walking plants (mandrakes, saplings,
// mushroom folk, flytraps, cacti, blossoms). Uses the core blocks (legs, heads, dorsal spikes) and
// its own leaf block, palette rule and root-walk locomotion - all looked up through the registry.

using System;
using System.Collections.Generic;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Plantfolk
{
    public sealed class PlantfolkFamily : CreatureFamilyBase
    {
        public override string Id => "plantfolk";
        public override string DisplayName => "Plantfolk (Extension)";
        public override string Description => "Example extension: mandrakes, saplings, mushroom folk, flytraps, cacti and blossoms walking on roots, with a dedicated palette rule, leaf shape block and motion module.";
        public override int Order => 200;
        public override string PaletteRuleId => "plantfolk";

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("mandrake", "Mandrake", 1.1),
            new ArchetypeInfo("sapling", "Sapling", 1.0),
            new ArchetypeInfo("mushroom", "Mushroom folk", 1.1),
            new ArchetypeInfo("flytrap", "Flytrap", 0.9),
            new ArchetypeInfo("cactus", "Cactus", 0.8),
            new ArchetypeInfo("bloom", "Blossom", 0.9),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("mandrake", "Mandrake"), ("sapling", "Sapling"), ("mushroom", "Mushroom folk"), ("flytrap", "Flytrap"), ("cactus", "Cactus"), ("bloom", "Blossom"),
        };
        public static readonly (string, string)[] Crowns = { ("none", "None"), ("tuft", "Tuft"), ("foliage", "Foliage"), ("cap", "Cap"), ("flower", "Flower"), ("trap", "Trap") };
        public static readonly (string, string)[] ArmKinds = { ("none", "None"), ("twigs", "Twigs"), ("vines", "Vines"), ("leaves", "Leaves") };
        public static readonly (string, string)[] EyeStyles = { ("bead", "Beady"), ("button", "Large & cute"), ("glow", "Glowing"), ("round", "Round") };
        public static readonly (string, string)[] MouthKinds = { ("none", "None"), ("small", "Small"), ("wide", "Wide") };
        public static readonly (string, string)[] CrownShapes = { ("round", "Round"), ("conical", "Conical"), ("drooping", "Drooping") };
        public static readonly (string, string)[] CapStyles =
        {
            ("amanita", "Amanita"), ("bolete", "Bolete"), ("chanterelle", "Chanterelle"), ("liberty", "Pointed cap"), ("inkcap", "Inkcap"), ("glowcap", "Glowing mushroom"),
        };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "mandrake", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.45, "", 0.08)
             .Float("body.height", "Trunk height", 0, 1, 0.5, "", 0.1)
             .Float("body.width", "Trunk thickness", 0, 1, 0.5, "", 0.1)
             .Float("body.gnarl", "Body gnarl", 0, 1, 0.3, "Knots, bulbs and outgrowths.", 0.1)
             .Float("spines", "Spines", 0, 1, 0.0, "", 0.1);
            b.Group("crown", "Crown", GeneStream.Anatomy)
             .Choice("crown.kind", "Crown kind", Crowns, "tuft", "Growth at the top of the body.", 0.08)
             .Float("crown.size", "Crown size", 0, 1, 0.5, "", 0.1)
             .Float("crown.count", "Crown count", 0, 1, 0.5, "", 0.1)
             .Choice("crown.shape", "Crown shape", CrownShapes, "round", "Rounded canopy, conical conifer or drooping willow.", 0.08)
             .Choice("cap.style", "Cap style", CapStyles, "amanita", "Mushroom cap: fly agaric, bolete, chanterelle, pointed, inkcap or glowing.", 0.08);
            b.Group("face", "Face", GeneStream.Anatomy)
             .Float("face.eyeSize", "Face eye size", 0, 1, 0.5, "", 0.1)
             .Choice("face.eyeStyle", "Face eye style", EyeStyles, "bead", "", 0.08)
             .Choice("face.mouth", "Face mouth", MouthKinds, "small", "", 0.1);
            b.Group("limbs", "Limbs", GeneStream.Anatomy)
             .Choice("arms.kind", "Arms kind", ArmKinds, "twigs", "", 0.1)
             .Float("arms.length", "Arms length", 0, 1, 0.5, "", 0.1)
             .Float("legs.length", "Root length", 0, 1, 0.4, "", 0.1)
             .Float("legs.thickness", "Root thickness", 0, 1, 0.5, "", 0.1);
            b.Group("plantcolor", "Foliage & cap colors", GeneStream.Color)
             .Hue("color.leafHue", "Color leaf hue", 132, "Spring green, evergreen, autumn gold, autumn red or blossom pink.")
             .Hue("color.clothHue", "Cap hue", 27, "Mushroom cap hue.")
             .Float("color.clothSaturation", "Cap saturation", 0, 1, 0.85, "", 0.12)
             .Float("color.clothValue", "Cap brightness", 0, 1, 0.5, "", 0.12);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "bark");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Height, Width, Gnarl, Spines, CrownSize, CrownCount, EyeSize, ArmLen, LegLen, LegTh;
            public string Crown = "tuft";
            public (string, double)[] Arms = { ("twigs", 1) }, Eyes = { ("bead", 1) }, Mouth = { ("small", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "sapling" => new Prior { Size = 0.65, Height = 0.75, Width = 0.45, Gnarl = 0.5, Spines = 0, CrownSize = 0.7, CrownCount = 0.6, EyeSize = 0.4, ArmLen = 0.65, LegLen = 0.35, LegTh = 0.6, Crown = "foliage",
                Arms = new[] { ("twigs", 4.0) }, Eyes = new[] { ("glow", 1.5), ("bead", 2.0) }, Mouth = new[] { ("none", 2.0), ("wide", 1.0) } },
            "mushroom" => new Prior { Size = 0.4, Height = 0.45, Width = 0.6, Gnarl = 0.1, Spines = 0, CrownSize = 0.7, CrownCount = 0.5, EyeSize = 0.6, ArmLen = 0.3, LegLen = 0.3, LegTh = 0.6, Crown = "cap",
                Arms = new[] { ("none", 3.0), ("leaves", 1.0) }, Eyes = new[] { ("button", 3.0), ("bead", 1.0), ("glow", 0.6) }, Mouth = new[] { ("small", 2.0), ("none", 1.0) } },
            "flytrap" => new Prior { Size = 0.5, Height = 0.55, Width = 0.35, Gnarl = 0.1, Spines = 0, CrownSize = 0.65, CrownCount = 0.6, EyeSize = 0.4, ArmLen = 0.45, LegLen = 0.35, LegTh = 0.45, Crown = "trap",
                Arms = new[] { ("leaves", 3.0), ("vines", 1.0) }, Eyes = new[] { ("bead", 2.0), ("glow", 1.0) }, Mouth = new[] { ("none", 1.0) } },
            "cactus" => new Prior { Size = 0.45, Height = 0.6, Width = 0.7, Gnarl = 0.2, Spines = 0.7, CrownSize = 0.5, CrownCount = 0.5, EyeSize = 0.45, ArmLen = 0.4, LegLen = 0.25, LegTh = 0.7, Crown = "flower",
                Arms = new[] { ("twigs", 0.3), ("none", 2.0) }, Eyes = new[] { ("bead", 3.0), ("button", 1.0) }, Mouth = new[] { ("small", 2.0), ("wide", 1.0) } },
            "bloom" => new Prior { Size = 0.35, Height = 0.5, Width = 0.3, Gnarl = 0.0, Spines = 0, CrownSize = 0.75, CrownCount = 0.7, EyeSize = 0.6, ArmLen = 0.5, LegLen = 0.4, LegTh = 0.35, Crown = "flower",
                Arms = new[] { ("leaves", 3.0), ("vines", 1.0) }, Eyes = new[] { ("button", 3.0), ("round", 1.0) }, Mouth = new[] { ("small", 3.0) } },
            _ => new Prior { Size = 0.3, Height = 0.4, Width = 0.65, Gnarl = 0.55, Spines = 0, CrownSize = 0.55, CrownCount = 0.5, EyeSize = 0.5, ArmLen = 0.45, LegLen = 0.35, LegTh = 0.5, Crown = "tuft",
                Arms = new[] { ("twigs", 2.0), ("vines", 1.0) }, Eyes = new[] { ("bead", 3.0), ("button", 1.0) }, Mouth = new[] { ("wide", 2.0), ("small", 1.0) } },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.12);
            Around(v, rng, "body.height", p.Height, sp);
            Around(v, rng, "body.width", p.Width, sp);
            Around(v, rng, "body.gnarl", p.Gnarl, sp);
            Around(v, rng, "spines", p.Spines, sp * 0.6);
            SetChoice(v, "crown.kind", p.Crown);
            Around(v, rng, "crown.size", p.CrownSize, sp);
            Around(v, rng, "crown.count", p.CrownCount, sp);
            Around(v, rng, "face.eyeSize", p.EyeSize, sp);
            SetChoice(v, "face.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "face.mouth", PickWeighted(rng, p.Mouth));
            SetChoice(v, "arms.kind", PickWeighted(rng, p.Arms));
            Around(v, rng, "arms.length", p.ArmLen, sp);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            SetChoice(v, "crown.shape", arch == "sapling" ? PickWeighted(rng, ("round", 2.0), ("conical", 1.2), ("drooping", 0.9)) : "round");
            SetChoice(v, "cap.style", arch == "mushroom"
                ? PickWeighted(rng, ("amanita", 2.5), ("bolete", 1.5), ("chanterelle", 1.2), ("liberty", 1.0), ("inkcap", 0.8), ("glowcap", 0.8))
                : "amanita");
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["bark"] = 4, ["leaf"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 3, ["mottled"] = 1, ["stripes"] = 0.5 },
                Accents = new Dictionary<string, double> { ["bone"] = 1 },
                // bark: brown, dark oak, mossy olive, birch grey, pale driftwood, red cedar
                Coats = new[] { Coats.Brown(2), Coats.Chocolate(1), Coats.Olive(1), Coats.Grey(0.6), Coats.Sand(0.6), Coats.Rust(0.4) },
                CountershadeMin = 0.0, CountershadeMax = 0.0,
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 1, ["complementary"] = 1, ["triadic"] = 1, ["split"] = 0.5 },
                SocksChance = 0.0, MaskChance = 0.0, PiebaldChance = 0.0,
            };
            switch (arch)
            {
                case "mushroom":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 3, ["leaf"] = 1 };
                    h.Coats = new[] { Coats.Cream(2), Coats.Sand(1), Coats.Tan(0.6), Coats.Pink(0.3) };
                    h.GlowChance = 0.25;
                    h.PiebaldChance = 0.1;
                    break;
                case "flytrap":
                case "bloom":
                    h.Materials = new Dictionary<string, double> { ["leaf"] = 4 };
                    h.Coats = new[] { Coats.Green(3), Coats.Olive(1), Coats.Teal(0.5) };
                    break;
                case "cactus":
                    h.Materials = new Dictionary<string, double> { ["leaf"] = 3, ["hide"] = 1 };
                    h.Coats = new[] { Coats.Green(2), Coats.Teal(0.6), Coats.Olive(0.6), Coats.Sand(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["stripes"] = 2, ["none"] = 1 };
                    break;
            }
            h.EnergyMax = 0.7;
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            if (Schema.Get("face.eyeStyle").Options[(int)values[Schema.IndexOf("face.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
            // foliage: spring green, fir green, autumn gold and red, blossom pink (saplings); greens otherwise
            double leaf = arch == "sapling"
                ? PickWeighted(rng, ("125", 3.0), ("150", 2.0), ("95", 0.7), ("62", 0.8), ("32", 0.7), ("355", 0.5)) switch { var h => double.Parse(h, System.Globalization.CultureInfo.InvariantCulture) }
                : 132;
            Set(values, "color.leafHue", leaf + rng.Gaussian(0, 6, 2.0));
            // mushroom caps: red fly agaric, brown bolete, yellow chanterelle, tan liberty cap, white ink cap, violet glow cap
            string cap = Schema.Get("cap.style").Options[(int)values[Schema.IndexOf("cap.style")]].Id;
            var (hue, s0, s1, v0, v1) = cap switch
            {
                "bolete" => (55.0, 0.45, 0.6, 0.3, 0.42),
                "chanterelle" => (78.0, 0.75, 0.9, 0.62, 0.72),
                "liberty" => (62.0, 0.3, 0.45, 0.42, 0.55),
                "inkcap" => (90.0, 0.03, 0.08, 0.8, 0.9),
                "glowcap" => (rng.Chance(0.5) ? 285.0 : 205.0, 0.55, 0.75, 0.38, 0.5),
                _ => (rng.Chance(0.75) ? 27.0 : 42.0, 0.85, 1.0, 0.45, 0.55),
            };
            Set(values, "color.clothHue", hue + rng.Gaussian(0, 4, 2.0));
            Set(values, "color.clothSaturation", rng.Range(s0, s1));
            Set(values, "color.clothValue", rng.Range(v0, v1));
            if (cap == "glowcap") Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.7));
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            var leaf = registry.GetShapeBlock(LeafBlock.Id);
            double size = g.Get("size");
            double baseL = DMath.Lerp(12, 30, DMath.Pow(size, 1.05));
            double trunkH = baseL * DMath.Lerp(0.55, 1.2, g.Get("body.height"));
            double trunkR = baseL * DMath.Lerp(0.18, 0.42, g.Get("body.width"));
            double legLen = baseL * DMath.Lerp(0.3, 0.75, g.Get("legs.length"));
            double legTh = g.Get("legs.thickness");
            string crown = g.GetChoice("crown.kind");
            double crownSize = g.Get("crown.size");
            int crownCount = (int)Math.Round(DMath.Lerp(3, 7, g.Get("crown.count")));

            // ---- skeleton: upright trunk on root legs (frames: +X up the trunk, +Y backwards, +Z right)
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            double hipH = legLen * 0.85;
            Vec3 up = Vec3.UnitY, back = new Vec3(-1, 0, 0);
            Vec3 P = new Vec3(0, hipH, 0);
            Vec3 C = P + up * trunkH;
            int pelvis = b.BoneAlong("pelvis", root, P, up, back, BoneRole.Pelvis, 0, trunkH * 0.5);
            int spine = b.BoneAlong("spine", pelvis, P + up * (trunkH * 0.5), up, back, BoneRole.Spine, 0, trunkH * 0.5);
            int top = b.BoneAlong("top", spine, C, up, back, BoneRole.Chest, 0, trunkR);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = top;
            b.Rig.Upright = true;
            b.Rig.Spine = new ChainRig { Name = "trunk", Bones = new[] { pelvis, spine, top }, Lengths = new[] { trunkH * 0.5, trunkH * 0.5 }, TotalLength = trunkH };

            int body = b.Group("trunk", DMath.Clamp(trunkR * 0.4, 1.2, 3.5), 0, GroupFlags.CreaseShade);
            double dom = trunkH + trunkR * 2;
            bool bulb = form == "mandrake" || form == "cactus";
            if (bulb)
            {
                // a fat root bulb (mandrake) or barrel (cactus)
                var lowerB = b.Ellipsoid(body, pelvis, new Vec3(trunkH * 0.3, 0, 0), new Vec3(trunkH * 0.55, trunkR * 1.05, trunkR * 1.05), MaterialSlot.Primary, PatternDomain.Body, 0, 0.6);
                lowerB.DomainLength = dom; lowerB.Tag = "bulb";
                var upperB = b.Ellipsoid(body, spine, new Vec3(trunkH * 0.15, 0, 0), new Vec3(trunkH * 0.45, trunkR * 0.85, trunkR * 0.85), MaterialSlot.Primary, PatternDomain.Body, 0.4, 1.0);
                upperB.DomainLength = dom; upperB.Tag = "bulb.top";
            }
            else
            {
                var t0 = b.Segment(body, pelvis, trunkR * 1.05, spine, trunkR * 0.95, MaterialSlot.Primary, PatternDomain.Body, 0, 0.5);
                t0.DomainLength = dom; t0.Tag = "trunk";
                var t1 = b.Segment(body, spine, trunkR * 0.95, top, trunkR * (form == "bloom" ? 0.6 : 0.85), MaterialSlot.Primary, PatternDomain.Body, 0.5, 1.0);
                t1.DomainLength = dom; t1.Tag = "trunk";
                var baseE = b.Ellipsoid(body, pelvis, new Vec3(0, 0, 0), new Vec3(trunkR * 0.8, trunkR * 1.2, trunkR * 1.2), MaterialSlot.Primary, PatternDomain.Body, 0, 0.1);
                baseE.DomainLength = dom;
            }
            double gnarl = g.Get("body.gnarl");
            if (gnarl > 0.25)
            {
                var rng = b.RngFor("gnarl");
                int n = (int)Math.Round(DMath.Lerp(1, 5, gnarl));
                for (int i = 0; i < n; i++)
                {
                    double a = rng.Range(0.0, DMath.TwoPi), h = rng.Range(0.1, 0.8);
                    double r = trunkR * rng.Range(0.3, 0.5);
                    var k = b.Ellipsoid(body, h < 0.5 ? pelvis : spine, new Vec3(trunkH * (h < 0.5 ? h : h - 0.5), DMath.Cos(a) * trunkR * 0.85, DMath.Sin(a) * trunkR * 0.85), new Vec3(r, r, r),
                        MaterialSlot.Primary, PatternDomain.Body, 0.2, 0.8);
                    k.DomainLength = dom;
                }
            }

            // ---- face on the upper trunk (not for flytraps: their face is the trap)
            double eyeSize = DMath.Clamp(DMath.Lerp(1.4, 3.8, g.Get("face.eyeSize")) * Math.Sqrt(trunkR / 5.0), 1.3, 5.0);
            string eyeStyle = g.GetChoice("face.eyeStyle");
            var style = eyeStyle switch { "button" => EyeStyle.Button, "glow" => EyeStyle.Glow, "round" => EyeStyle.Round, _ => EyeStyle.Bead };
            int faceBone = form == "flytrap" || form == "bloom" ? -1 : (bulb ? spine : top);
            double faceY = bulb ? P.Y + trunkH * 0.72 : C.Y - trunkR * 0.3;
            // mushrooms: the face sits low on the stem, below the overhang of the cap (seen from above)
            if (form == "mushroom") faceY = Math.Max(P.Y + trunkH * 0.3, C.Y - trunkR * 0.6 - trunkH * 0.38);
            double faceR = bulb ? trunkR * 0.9 : trunkR * 0.9;
            int headBone = faceBone >= 0 ? faceBone : top;
            if (faceBone >= 0)
            {
                var fx = b.RestWorld(faceBone);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 dir = new Vec3(0.85, 0.15, side * 0.45).Normalized();
                    Vec3 pos = new Vec3(dir.X * faceR, faceY, dir.Z * faceR);
                    b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = faceBone, LocalPosition = fx.InversePoint(pos), LocalNormal = fx.InverseDir(dir), Size = eyeSize, Style = style, Side = side, Group = body });
                }
                string mouth = g.GetChoice("face.mouth");
                if (mouth != "none")
                {
                    // recessed mouth that swings out when opened (see the amorphous family)
                    double mw = trunkR * (mouth == "wide" ? 0.6 : 0.3);
                    double depthR = Math.Max(0.8, trunkR * 0.15);
                    Vec3 M = new Vec3(faceR * 0.92 - depthR - 0.4, faceY - eyeSize * 1.1 - 1, 0);
                    double d = Math.Max(2.0, trunkR * 0.5);
                    int jaw = b.BoneAlong("jaw", faceBone, M - new Vec3(0, d, 0), Vec3.UnitX, Vec3.UnitY, BoneRole.Jaw, 0, d);
                    int mg = b.Group("mouth", 0.6, 0, GroupFlags.NoOutline | GroupFlags.NoContour | GroupFlags.Interior, 0.2);
                    var m = b.Ellipsoid(mg, jaw, new Vec3(0, d, 0), new Vec3(depthR, Math.Max(0.8, trunkR * 0.12), Math.Max(1.0, mw)), MaterialSlot.Mouth);
                    m.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern;
                    b.Rig.Jaw = jaw;
                    b.Rig.JawOpenMax = 0.8;
                }
            }

            // ---- crown
            var hxTop = b.RestWorld(top);
            double crownMargin = 0;
            Vec3 crownBase = bulb ? P + up * (trunkH * 0.95) : C + up * (trunkR * 0.4);
            switch (crown)
            {
                case "tuft":
                case "foliage":
                {
                    bool foliage = crown == "foliage";
                    if (foliage)
                    {
                        // round foliage clusters in their own group (leaf green), plus a few leaves on the rim
                        string shapeC = g.GetChoice("crown.shape");
                        // fir tiers keep visible steps (small blend), round crowns melt into one cloud
                        int fg = b.Group("foliage", shapeC == "conical" ? 0.7 : 2.0, 0, GroupFlags.CreaseShade, 0.1);
                        double fr = baseL * DMath.Lerp(0.3, 0.55, crownSize);
                        var rng = b.RngFor("foliage");
                        PrimitiveDef F(Vec3 c, Vec3 r)
                        {
                            var e = b.Ellipsoid(fg, top, hxTop.InversePoint(c), r, MaterialSlot.Secondary, PatternDomain.None);
                            e.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                            return e;
                        }
                        if (shapeC == "conical")
                        {
                            // tall, wide tiers swing far when the trunk leans
                            crownMargin = fr * 0.8;
                            // fir: stacked tiers that shrink towards a pointed top
                            int tiers = 3 + (crownCount > 5 ? 1 : 0);
                            for (int i = 0; i < tiers; i++)
                            {
                                double t = i / (double)(tiers - 1);
                                double rr = fr * DMath.Lerp(1.2, 0.45, t);
                                // bone frame: +X up the trunk, so the flat axis of each tier is X
                                F(crownBase + up * (fr * DMath.Lerp(0.2, 1.5, t)), new Vec3(rr * 0.48, rr, rr));
                            }
                            var tip = b.Cone(fg, top, hxTop.InversePoint(crownBase + up * (fr * 1.55)), fr * 0.32, top, hxTop.InversePoint(crownBase + up * (fr * 2.2)), 0.45, MaterialSlot.Secondary);
                            tip.Flags |= PrimFlags.NoPattern;
                        }
                        else
                        {
                            for (int i = 0; i < crownCount; i++)
                            {
                                double a = i / (double)crownCount * DMath.TwoPi + rng.Range(-0.3, 0.3);
                                double rr = fr * rng.Range(0.45, 0.7);
                                Vec3 c = crownBase + new Vec3(DMath.Cos(a) * fr * 0.55, fr * rng.Range(0.4, 0.9), DMath.Sin(a) * fr * 0.55);
                                F(c, new Vec3(rr, rr, rr));
                            }
                            F(crownBase + up * (fr * 0.9), new Vec3(fr * 0.75, fr * 0.75, fr * 0.75));
                            if (shapeC == "drooping")
                            {
                                crownMargin = fr * 0.45;
                                // willow: curtains of foliage hang down around the trunk
                                for (int i = 0; i < crownCount + 1; i++)
                                {
                                    double a = (i + 0.5) / (crownCount + 1) * DMath.TwoPi;
                                    Vec3 o = new Vec3(DMath.Cos(a) * fr * 0.85, 0, DMath.Sin(a) * fr * 0.85);
                                    var hang = b.Cone(fg, top, hxTop.InversePoint(crownBase + up * (fr * 0.7) + o), fr * 0.32, top,
                                        hxTop.InversePoint(crownBase - up * (fr * DMath.Lerp(0.5, 0.95, rng.NextDouble())) + o * 1.15), fr * 0.14, MaterialSlot.Secondary);
                                    hang.Flags |= PrimFlags.NoPattern;
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < crownCount; i++)
                        {
                            double a = DMath.Lerp(-1.0, 1.0, crownCount == 1 ? 0.5 : i / (double)(crownCount - 1));
                            double yaw = i * 2.4;
                            Vec3 dir = new Vec3(DMath.Sin(a) * DMath.Cos(yaw) * 0.8, 1, DMath.Sin(a) * DMath.Sin(yaw) * 0.8).Normalized();
                            leaf.Build(b, new ShapeParams { Name = $"leaf.{i}", ParentBone = top, Position = crownBase, Direction = dir, Material = MaterialSlot.Secondary }
                                .Set("length", baseL * DMath.Lerp(0.25, 0.55, crownSize)).Set("width", baseL * DMath.Lerp(0.08, 0.18, crownSize)).Set("curl", 0.4));
                        }
                    }
                    break;
                }
                case "cap":
                {
                    // mushroom cap in its own colour (cap genes); shape and spots follow the cap style
                    string capStyle = g.GetChoice("cap.style");
                    int cg = b.Group("cap", 1.5, 0, GroupFlags.None, 0.2);
                    double capR = trunkR * DMath.Lerp(1.8, 3.0, crownSize);
                    double capH = capR * 0.55, capX = capH * 0.35;
                    switch (capStyle)
                    {
                        case "bolete": capH = capR * 0.66; capX = capH * 0.3; break;
                        case "chanterelle": capH = capR * 0.34; capX = capH * 0.6; break;
                        case "liberty": capR *= 0.72; capH = capR * 1.0; capX = capH * 0.25; break;
                        case "inkcap": capR *= 0.66; capH = capR * 1.3; capX = capH * 0.12; break;
                    }
                    var cap = b.Ellipsoid(cg, top, new Vec3(capX, 0, 0), new Vec3(capH, capR, capR), MaterialSlot.Cloth, PatternDomain.None);
                    cap.Flags |= PrimFlags.ShadowCaster | PrimFlags.NoPattern;
                    cap.Tag = "cap";
                    if (capStyle == "chanterelle")
                    {
                        // upturned wavy rim around a shallow funnel
                        var rim = b.Ellipsoid(cg, top, new Vec3(capX + capH * 0.55, 0, 0), new Vec3(capH * 0.38, capR * 1.04, capR * 1.04), MaterialSlot.Cloth, PatternDomain.None);
                        rim.Flags |= PrimFlags.NoPattern;
                        rim.Tag = "cap.rim";
                    }
                    else if (capStyle == "liberty")
                    {
                        var nip = b.Cone(cg, top, new Vec3(capX + capH * 0.85, 0, 0), capR * 0.32, top, new Vec3(capX + capH * 1.3, 0, 0), 0.45, MaterialSlot.Cloth);
                        nip.Flags |= PrimFlags.NoPattern;
                    }
                    else if (capStyle == "inkcap")
                    {
                        // dissolving dark rim ("ink")
                        int ig = b.Group("cap.ink", 0.6, 0, GroupFlags.None, 0.35);
                        var ink = b.Ellipsoid(ig, top, new Vec3(capX - capH * 0.78, 0, 0), new Vec3(Math.Max(0.6, capH * 0.12), capR * 0.82, capR * 0.82), MaterialSlot.Marking, PatternDomain.None);
                        ink.Flags |= PrimFlags.NoPattern;
                    }
                    if (capStyle == "amanita" || capStyle == "glowcap")
                    {
                        // raised warts (white) or glowing dots
                        var rng = b.RngFor("spots");
                        int spots = crownCount + (capStyle == "amanita" ? 3 : 1);
                        for (int i = 0; i < spots; i++)
                        {
                            double a = rng.Range(0.0, DMath.TwoPi), el = rng.Range(0.25, 1.15);
                            Vec3 n = new Vec3(DMath.Cos(el), DMath.Sin(el) * DMath.Cos(a), DMath.Sin(el) * DMath.Sin(a));
                            Vec3 pos = new Vec3(capX, 0, 0) + new Vec3(n.X * capH * 0.98, n.Y * capR * 0.98, n.Z * capR * 0.98);
                            double sr = capR * rng.Range(0.1, 0.16);
                            var sp = b.Ellipsoid(cg, top, pos, new Vec3(0.55, sr, sr), capStyle == "glowcap" ? MaterialSlot.Glow : MaterialSlot.White, PatternDomain.None);
                            sp.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                            if (capStyle == "glowcap") sp.Flags |= PrimFlags.Emissive;
                        }
                    }
                    b.Trait("cap:" + capStyle);
                    break;
                }
                case "flower":
                {
                    int fl = BuildFlower(b, top, crownBase + up * (trunkR * 0.2), baseL * DMath.Lerp(0.2, 0.42, crownSize), crownCount + 2, form == "bloom", eyeSize, style, g);
                    if (form == "bloom") headBone = fl;
                    break;
                }
                case "trap":
                    headBone = BuildTrap(b, top, crownBase, baseL * DMath.Lerp(0.3, 0.55, crownSize), eyeSize, style);
                    break;
            }
            b.Rig.Head = headBone;

            // ---- spines (cacti, thorny saplings)
            double spines = g.Get("spines");
            if (spines > 0.2)
            {
                var ds = new DorsalSpec { Kind = DorsalKind.Quills, Size = Math.Max(1.2, trunkR * 0.35 * (0.6 + spines)), Material = MaterialSlot.Teeth };
                int rings = bulb ? 3 : 4;
                for (int r = 0; r < rings; r++)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        double a = k / 5.0 * DMath.TwoPi + r * 0.6;
                        double h = (r + 0.5) / rings;
                        Vec3 nrm = new Vec3(DMath.Cos(a), 0.25, DMath.Sin(a)).Normalized();
                        double rad = bulb ? trunkR * (1.0 - Math.Abs(h - 0.45) * 0.5) : trunkR;
                        Vec3 pos = P + up * (trunkH * h) + new Vec3(DMath.Cos(a) * rad, 0, DMath.Sin(a) * rad);
                        ds.Anchors.Add((h < 0.5 ? pelvis : spine, pos, nrm, 0.8));
                    }
                }
                Dorsal.Build(b, ds);
            }

            // ---- arms
            string arms = g.GetChoice("arms.kind");
            if (arms != "none")
            {
                double armLen = baseL * DMath.Lerp(0.4, 0.95, g.Get("arms.length"));
                double armR = Math.Max(0.7, trunkR * (arms == "twigs" ? 0.2 : 0.16));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 sh = P + up * (trunkH * (bulb ? 0.55 : 0.7)) + new Vec3(0, 0, side * trunkR * 0.85);
                    Vec3 wrist = sh + new Vec3(armLen * 0.2, -armLen * 0.55, side * armLen * 0.45);
                    var arm = Limbs.Build(b, new LegSpec
                    {
                        Name = "arm" + (side < 0 ? "L" : "R"),
                        Side = side, Pair = 0, Front = true, IsArm = true, NoFoot = true,
                        AnchorBone = spine, Hip = sh, Contact = wrist, Style = LegStyle.Plantigrade,
                        Length = armLen, Upper = 0.48, Lower = 0.4, Distal = 0.12,
                        RadiusTop = armR * 1.2, RadiusKnee = armR, RadiusAnkle = armR * 0.8, RadiusFoot = armR * 0.7,
                        Haunch = 0, BlendRadius = 0.7, DomainLength = armLen,
                        Pole = new Vec3(-0.3, 0.2, 1.0),
                    });
                    b.Rig.Arms.Add(arm);
                    // leaf or twig ends
                    if (arms == "leaves" || arms == "vines")
                    {
                        leaf.Build(b, new ShapeParams
                        {
                            Name = $"handleaf.{(side < 0 ? "L" : "R")}", ParentBone = arm.JointBones[2], Position = b.RestWorld(arm.JointBones[2]).Origin,
                            Direction = new Vec3(0.3, -0.4, side * 1.0), Material = MaterialSlot.Secondary, Side = side,
                        }.Set("length", armLen * 0.55).Set("width", armLen * 0.28).Set("spring", 0));
                    }
                    else
                    {
                        // twig fingers
                        var hx = b.RestWorld(arm.JointBones[2]);
                        for (int k = -1; k <= 1; k++)
                        {
                            Vec3 tip = hx.Origin + new Vec3(0.4 + k * 0.5, -armLen * 0.18, side * (armLen * 0.12 + k * 0.4));
                            var tw = b.Cone(arm.Group, arm.JointBones[2], Vec3.Zero, Math.Max(0.5, armR * 0.6), arm.JointBones[2], hx.InversePoint(tip), 0.35, MaterialSlot.Primary);
                            tw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern;
                        }
                    }
                }
            }

            // ---- root legs (stump style) with little rootlet toes
            int legCount = form == "flytrap" ? 4 : 2;
            double legR = Math.Max(1.0, trunkR * DMath.Lerp(0.3, 0.55, legTh));
            int idx = 0;
            for (int pair = 0; pair < legCount / 2; pair++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    double fx = legCount == 4 ? (pair == 0 ? trunkR * 0.45 : -trunkR * 0.45) : 0;
                    Vec3 hip = P + new Vec3(fx, -trunkR * 0.1, side * trunkR * 0.55);
                    Vec3 contact = new Vec3(hip.X + trunkR * 0.15, 0, side * trunkR * (legCount == 4 ? 0.9 : 0.65));
                    double footLen = Math.Max(1.8, legR * 1.7);
                    double legPart = hip.Y * 1.02;
                    var spec = new LegSpec
                    {
                        Name = $"root{pair}{(side < 0 ? "L" : "R")}",
                        Side = side, Pair = pair, Front = pair == 0 && legCount == 4,
                        AnchorBone = pelvis, Hip = hip, Contact = contact, Style = LegStyle.Stump,
                        Length = legPart + footLen,
                        RadiusTop = legR, RadiusKnee = legR * 0.85, RadiusAnkle = legR * 0.75, RadiusFoot = legR * 0.7,
                        Haunch = 0, PawLength = footLen, PawRadius = legR * 0.85,
                        FootMaterial = MaterialSlot.Primary, Claws = 3, ClawSize = Math.Max(0.6, legR * 0.45),
                        LiftHeight = Math.Max(1.2, hipH * 0.35), BlendRadius = DMath.Clamp(legR * 0.4, 0.7, 2.0), DomainLength = legPart,
                    };
                    spec.Upper = legPart * 0.5 / spec.Length;
                    spec.Lower = legPart * 0.5 / spec.Length;
                    spec.Distal = footLen / spec.Length;
                    var leg = Limbs.Build(b, spec);
                    leg.Phase = legCount == 2 ? (side < 0 ? 0.0 : 0.5) : ((pair == 0) == (side < 0) ? 0.0 : 0.5);
                    b.Rig.Legs.Add(leg);
                    idx++;
                }
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = RootWalkLocomotion.ModuleId;
            b.Rig.Action = form switch
            {
                "flytrap" => ActionStyle.Bite,
                "mandrake" => ActionStyle.Roar,
                "sapling" => ActionStyle.Slam,
                "cactus" => ActionStyle.Slam,
                _ => ActionStyle.Spray,
            };
            b.Rig.Rest = RestStyle.SitSlump;
            b.Rig.Tuning["rest.bodyHeight"] = Math.Max(1.5, trunkR * 0.6);
            b.Rig.Tuning["gait.walkDuty"] = 0.66;
            b.Rig.Tuning["gait.runDuty"] = 0.45;
            b.Rig.Tuning["gait.gallop"] = 0;
            b.Metrics.LegLength = Math.Max(3, legLen);
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = C.Y;
            b.Metrics.BodyLength = (C.Y + trunkR) * 0.8;
            b.Metrics.Mass = DMath.Pow(baseL / 22.0, 3) * 1.1;
            b.Metrics.WalkSpeed = Math.Sqrt(0.25 * 400 * Math.Max(3, legLen)) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5));
            b.Metrics.RunSpeed = Math.Sqrt(1.4 * 400 * Math.Max(3, legLen)) * DMath.Lerp(0.8, 1.15, g.GetOr("motion.speed", 0.5));
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 22.0), 0.7, 1.4);
            b.AnimationMargin = Math.Max(7, baseL * 0.45) + crownMargin;
            b.ExtraHeadroom = Math.Max(6, baseL * 0.5);
            return b.Build(g.AnatomyKey);
        }

        /// <summary>A flower: disc (face for blossom creatures) with petals around it, facing forwards/up.</summary>
        private static int BuildFlower(AnatomyBuilder b, int top, Vec3 center, double radius, int petals, bool face, double eyeSize, EyeStyle style, CreatureGenome g)
        {
            var hx = b.RestWorld(top);
            int head = b.BoneAlong("flower", top, center, new Vec3(0.55, 0.85, 0), Vec3.UnitY, BoneRole.Head, 0, radius);
            var fx = b.RestWorld(head);
            int pg = b.Group("petals", 0.5, 0, GroupFlags.NoFarShade, -0.1);
            // petals radiate in the flower plane (bone local Y/Z plane)
            for (int i = 0; i < petals; i++)
            {
                double a = i / (double)petals * DMath.TwoPi;
                Vec3 dir = new Vec3(-0.15, DMath.Cos(a), DMath.Sin(a));
                Vec3 side = new Vec3(0, -DMath.Sin(a), DMath.Cos(a)) * (radius * 0.42);
                Vec3 tip = dir * (radius * 1.6);
                Vec3 mid = dir * (radius * 0.9);
                var t1 = b.Triangle(pg, head, Vec3.Zero, head, mid + side, head, tip, MaterialSlot.Accent, PatternDomain.Fin);
                var t2 = b.Triangle(pg, head, Vec3.Zero, head, tip, head, mid - side, MaterialSlot.Accent, PatternDomain.Fin);
                t1.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow; t2.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow;
            }
            int dg = b.Group("disc", 0.8, 0, GroupFlags.None, 0.3);
            var disc = b.Ellipsoid(dg, head, new Vec3(radius * 0.12, 0, 0), new Vec3(radius * 0.35, radius * 0.62, radius * 0.62), face ? MaterialSlot.Primary : MaterialSlot.Glow, PatternDomain.None);
            disc.Flags |= PrimFlags.NoPattern;
            if (!face) disc.Flags |= PrimFlags.Emissive;
            if (face)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 dirL = new Vec3(0.9, 0.15, side * 0.4).Normalized();
                    Vec3 pos = new Vec3(radius * 0.12, 0, 0) + new Vec3(dirL.X * radius * 0.35, dirL.Y * radius * 0.62, dirL.Z * radius * 0.62);
                    b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = head, LocalPosition = pos, LocalNormal = dirL, Size = eyeSize, Style = style, Side = side, Group = dg });
                }
            }
            if (face) b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { top, head }, Lengths = new[] { radius }, TotalLength = radius };
            _ = hx; _ = fx; _ = g;
            return head;
        }

        /// <summary>Venus flytrap head: two lobes with bristles, the lower lobe is the jaw.</summary>
        private static int BuildTrap(AnatomyBuilder b, int top, Vec3 at, double size, double eyeSize, EyeStyle style)
        {
            int stalk = b.BoneAlong("stalk", top, at, new Vec3(0.35, 1, 0), Vec3.UnitX * -1, BoneRole.Neck, 0, size * 0.9);
            Vec3 headPos = at + new Vec3(0.35, 1, 0).Normalized() * (size * 0.9);
            int head = b.BoneAlong("trap", stalk, headPos, new Vec3(1, 0.15, 0), Vec3.UnitY, BoneRole.Head, 0, size);
            var sg = b.Group("stalk", 1.0);
            var st = b.Segment(sg, stalk, Math.Max(0.9, size * 0.14), head, Math.Max(0.8, size * 0.12), MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
            st.DomainLength = size;
            int hg = b.Group("trap", 1.0, 0, GroupFlags.None, 0.2);
            var upper = b.Ellipsoid(hg, head, new Vec3(size * 0.5, size * 0.12, 0), new Vec3(size * 0.55, size * 0.2, size * 0.42), MaterialSlot.Secondary, PatternDomain.Head, 0, 1);
            upper.Tag = "trap.upper";
            int jaw = b.BoneAlong("trap.jaw", head, b.RestWorld(head).Point(new Vec3(0.2, -size * 0.05, 0)), b.RestWorld(head).Dir(new Vec3(1, -0.05, 0)), Vec3.UnitY, BoneRole.Jaw, 0, size);
            var lower = b.Ellipsoid(hg, jaw, new Vec3(size * 0.45, -size * 0.1, 0), new Vec3(size * 0.52, size * 0.18, size * 0.4), MaterialSlot.Secondary, PatternDomain.Head, 0, 1);
            lower.Tag = "trap.lower";
            // mouth interior (visible when the trap opens) and bristles on both rims
            int mg = b.Group("trap.mouth", 0.6, 0, GroupFlags.NoOutline | GroupFlags.NoContour | GroupFlags.Interior, -1.0);
            var inner = b.Ellipsoid(mg, head, new Vec3(size * 0.5, 0, 0), new Vec3(size * 0.45, size * 0.14, size * 0.32), MaterialSlot.Accent);
            inner.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern;
            int tg = b.Group("bristles", 0.3, 0, GroupFlags.NoContour, 0.4);
            for (int i = 0; i < 5; i++)
            {
                double t = (i + 0.5) / 5.0;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec3 a = new Vec3(size * DMath.Lerp(0.15, 0.95, t), size * 0.05, side * size * 0.38 * DMath.Sin(DMath.Pi * t));
                    var u = b.Cone(tg, head, a, 0.45, head, a + new Vec3(0, -size * 0.12, side * size * 0.06), 0.3, MaterialSlot.Teeth);
                    u.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Hard;
                    Vec3 c = new Vec3(size * DMath.Lerp(0.15, 0.95, t), -size * 0.02, side * size * 0.36 * DMath.Sin(DMath.Pi * t));
                    var l = b.Cone(tg, jaw, c, 0.45, jaw, c + new Vec3(0, size * 0.12, side * size * 0.05), 0.3, MaterialSlot.Teeth);
                    l.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Hard;
                }
            }
            // eyes on top of the upper lobe
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 dir = new Vec3(0.5, 0.8, side * 0.35).Normalized();
                Vec3 pos = new Vec3(size * 0.35, size * 0.12 + size * 0.2 * 0.9, side * size * 0.18);
                b.Feature(new FeatureDef { Kind = FeatureKind.Eye, Bone = head, LocalPosition = pos, LocalNormal = dir, Size = eyeSize, Style = style, Side = side, Group = hg });
            }
            b.Rig.Jaw = jaw;
            b.Rig.JawOpenMax = 0.9;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { stalk, head }, Lengths = new[] { size * 0.9 }, TotalLength = size * 0.9 };
            b.Rig.Tuning["idle.jaw"] = 0.15;
            return head;
        }
    }
}
