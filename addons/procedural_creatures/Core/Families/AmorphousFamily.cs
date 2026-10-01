// Procedural Pixel Creatures - family 8: amorphous creatures and tentacle beings (slimes, oozes,
// octopoids, jellyfish, floating eyes, wisps). Soft bodies that hop with squash and stretch or
// float with trailing, spring simulated tentacles.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class AmorphousFamily : CreatureFamilyBase
    {
        public override string Id => "amorphous";
        public override string DisplayName => "Slimes & Tentacled Creatures";
        public override string Description => "Slimes, oozes, octopuses, jellyfish, floating eyes and wisps: soft bodies that hop with squash and stretch or float with trailing, spring-simulated tentacles.";
        public override int Order => 80;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("slime", "Slime", 1.3),
            new ArchetypeInfo("ooze", "Ooze", 0.8),
            new ArchetypeInfo("octopus", "Octopus", 1.0),
            new ArchetypeInfo("jelly", "Jellyfish", 0.9),
            new ArchetypeInfo("beholder", "Floating eye", 0.8),
            new ArchetypeInfo("wisp", "Wisp", 0.7),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("slime", "Slime"), ("ooze", "Ooze"), ("octopus", "Octopus"), ("jelly", "Jellyfish"), ("beholder", "Floating eye"), ("wisp", "Wisp"),
        };
        public static readonly (string, string)[] Moves = { ("hop", "Hop"), ("float", "Float"), ("pulse", "Pulse") };
        public static readonly (string, string)[] EyeStyles = { ("button", "Large & cute"), ("round", "Round"), ("bead", "Beady"), ("glow", "Glowing"), ("slit", "Slit pupils") };
        public static readonly (string, string)[] MouthKinds = { ("none", "None"), ("small", "Small"), ("wide", "Wide"), ("maw", "Maw") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs") };
        public static readonly (string, string)[] Crowns = { ("none", "None"), ("spikes", "Spikes"), ("crystal", "Crystal"), ("stalks", "Stalks"), ("drip", "Drip") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "slime", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.45, "", 0.08)
             .Float("body.height", "Body height", 0, 1, 0.5, "Flat puddle to a tall dome.", 0.1)
             .Float("body.width", "Body width", 0, 1, 0.5, "", 0.1)
             .Float("body.lumps", "Body lumps", 0, 1, 0.2, "Irregular bulges on the surface.", 0.1)
             .Choice("move", "Locomotion", Moves, "hop", "", 0.05)
             .Float("hover", "Hover height", 0, 1, 0.4, "", 0.1);
            b.Group("face", "Face", GeneStream.Anatomy)
             .Float("eyes.count", "Eyes count", 0, 1, 0.3, "One, two or three eyes.", 0.1)
             .Float("eyes.size", "Eyes size", 0, 1, 0.55, "", 0.1)
             .Choice("eyes.style", "Eyes style", EyeStyles, "button", "", 0.08)
             .Float("eyes.spacing", "Eyes spacing", 0, 1, 0.5, "", 0.1)
             .Choice("mouth", "Mouth", MouthKinds, "small", "", 0.1)
             .Choice("mouth.teeth", "Mouth teeth", TeethKinds, "none", "", 0.1);
            b.Group("tentacles", "Tentacles", GeneStream.Anatomy)
             .Float("tentacles.count", "Tentacles count", 0, 1, 0.0, "", 0.1)
             .Float("tentacles.length", "Tentacles length", 0, 1, 0.5, "", 0.1)
             .Float("tentacles.thickness", "Tentacles thickness", 0, 1, 0.5, "", 0.1)
             .Choice("crown", "Crown", Crowns, "none", "", 0.1)
             .Float("crown.size", "Crown size", 0, 1, 0.5, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "slime");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Height, Width, Lumps, Hover, EyeCount, EyeSize, Spacing, TentCount, TentLen, TentTh, CrownSize;
            public string Move = "hop";
            public (string, double)[] Eyes = { ("button", 1) }, Mouth = { ("small", 1) }, Teeth = { ("none", 1) }, Crown = { ("none", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "ooze" => new Prior
            {
                Size = 0.55, Height = 0.3, Width = 0.8, Lumps = 0.6, Hover = 0.0, EyeCount = 0.75, EyeSize = 0.35, Spacing = 0.7, TentCount = 0.0, TentLen = 0.3, TentTh = 0.5, CrownSize = 0.5,
                Eyes = new[] { ("bead", 2.0), ("glow", 1.5), ("round", 1.0) }, Mouth = new[] { ("wide", 2.0), ("maw", 1.0), ("none", 1.0) },
                Teeth = new[] { ("none", 2.0), ("small", 1.0) }, Crown = new[] { ("drip", 2.0), ("none", 2.0), ("stalks", 0.6) },
            },
            "octopus" => new Prior
            {
                Size = 0.5, Height = 0.75, Width = 0.5, Lumps = 0.1, Hover = 0.35, EyeCount = 0.3, EyeSize = 0.55, Spacing = 0.8, TentCount = 0.85, TentLen = 0.65, TentTh = 0.6, CrownSize = 0.5,
                Move = "float", Eyes = new[] { ("slit", 2.0), ("round", 2.0), ("button", 1.0) }, Mouth = new[] { ("none", 3.0), ("small", 1.0) },
            },
            "jelly" => new Prior
            {
                Size = 0.45, Height = 0.4, Width = 0.65, Lumps = 0.0, Hover = 0.85, EyeCount = 0.3, EyeSize = 0.35, Spacing = 0.5, TentCount = 0.65, TentLen = 0.8, TentTh = 0.2, CrownSize = 0.5,
                Move = "pulse", Eyes = new[] { ("button", 2.0), ("glow", 1.0), ("bead", 1.0) }, Mouth = new[] { ("none", 3.0), ("small", 1.0) },
            },
            "beholder" => new Prior
            {
                Size = 0.55, Height = 0.6, Width = 0.6, Lumps = 0.15, Hover = 0.6, EyeCount = 0.0, EyeSize = 0.95, Spacing = 0.5, TentCount = 0.0, TentLen = 0.5, TentTh = 0.5, CrownSize = 0.6,
                Move = "float", Eyes = new[] { ("round", 2.0), ("slit", 2.0), ("glow", 1.0) }, Mouth = new[] { ("maw", 3.0), ("wide", 1.0) },
                Teeth = new[] { ("fangs", 3.0), ("small", 1.0) }, Crown = new[] { ("stalks", 4.0) },
            },
            "wisp" => new Prior
            {
                Size = 0.25, Height = 0.55, Width = 0.45, Lumps = 0.0, Hover = 0.8, EyeCount = 0.3, EyeSize = 0.6, Spacing = 0.5, TentCount = 0.35, TentLen = 0.75, TentTh = 0.3, CrownSize = 0.4,
                Move = "float", Eyes = new[] { ("glow", 2.0), ("button", 2.0) }, Mouth = new[] { ("none", 3.0), ("small", 1.0) }, Crown = new[] { ("none", 2.0), ("crystal", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.4, Height = 0.55, Width = 0.55, Lumps = 0.1, Hover = 0.0, EyeCount = 0.3, EyeSize = 0.6, Spacing = 0.45, TentCount = 0.0, TentLen = 0.3, TentTh = 0.5, CrownSize = 0.4,
                Eyes = new[] { ("button", 3.0), ("round", 1.0), ("bead", 1.0) }, Mouth = new[] { ("small", 3.0), ("wide", 1.0), ("none", 1.0) },
                Crown = new[] { ("none", 4.0), ("crystal", 0.8), ("spikes", 0.5), ("drip", 0.8) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "body.height", p.Height, sp);
            Around(v, rng, "body.width", p.Width, sp);
            Around(v, rng, "body.lumps", p.Lumps, sp);
            SetChoice(v, "move", p.Move);
            Around(v, rng, "hover", p.Hover, sp);
            Around(v, rng, "eyes.count", p.EyeCount, sp);
            Around(v, rng, "eyes.size", p.EyeSize, sp);
            SetChoice(v, "eyes.style", PickWeighted(rng, p.Eyes));
            Around(v, rng, "eyes.spacing", p.Spacing, sp);
            SetChoice(v, "mouth", PickWeighted(rng, p.Mouth));
            SetChoice(v, "mouth.teeth", PickWeighted(rng, p.Teeth));
            Around(v, rng, "tentacles.count", p.TentCount, sp);
            Around(v, rng, "tentacles.length", p.TentLen, sp);
            Around(v, rng, "tentacles.thickness", p.TentTh, sp);
            SetChoice(v, "crown", PickWeighted(rng, p.Crown));
            Around(v, rng, "crown.size", p.CrownSize, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            // jellies, slimes and spirits must read bright and wet: own, lighter coats
            CoatPrior Bright(string id, double w, double hue, double spread) => new CoatPrior(id, w, hue, spread, 0.5, 0.92, 0.5, 0.85);
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["slime"] = 5, ["hide"] = 0.6 },
                Patterns = new Dictionary<string, double> { ["none"] = 4, ["spots"] = 1, ["mottled"] = 0.6 },
                Accents = new Dictionary<string, double> { ["vivid"] = 2, ["bone"] = 1, ["gold"] = 0.6 },
                Coats = new[] { Bright("green", 1.4, 145, 20), Bright("teal", 1.2, 195, 15), Bright("blue", 1.0, 250, 15), Bright("violet", 0.8, 305, 15),
                                Bright("pink", 0.8, 355, 12), Bright("crimson", 0.6, 25, 10), Bright("yellow", 0.6, 100, 8), Coats.Vivid(1) },
                CountershadeMin = 0.3, CountershadeMax = 0.6,
                GlowChance = 0.3,
                EnergyMin = 0.3,
                SocksChance = 0.0, MaskChance = 0.0, PiebaldChance = 0.04,
            };
            switch (arch)
            {
                case "octopus":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 3, ["slime"] = 2 };
                    h.Coats = new[] { Coats.Rust(1.4), Coats.Crimson(1), Coats.Pink(0.8), Coats.Violet(0.8), Coats.Brown(0.6), Coats.Teal(0.4), Coats.Sand(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 2, ["mottled"] = 1.5, ["none"] = 1, ["bands"] = 0.6 };
                    h.GlowChance = 0.1;
                    break;
                case "jelly":
                    h.Coats = new[] { Bright("violet", 1.2, 305, 18), Bright("blue", 1.2, 235, 18), Bright("pink", 1.0, 345, 12), Bright("teal", 1.0, 185, 15), Coats.Albino(0.5), Bright("yellow", 0.3, 95, 8) };
                    h.GlowChance = 0.6;
                    break;
                case "beholder":
                    h.Materials = new Dictionary<string, double> { ["hide"] = 4, ["slime"] = 1 };
                    h.Coats = new[] { Coats.Violet(1), Coats.Green(1), Coats.Rust(0.8), Coats.Brown(0.6), Coats.Blue(0.6), Coats.Crimson(0.6), Coats.Muted(0.8) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["spots"] = 1.5, ["mottled"] = 1 };
                    break;
                case "wisp":
                    h.Coats = new[] { Bright("blue", 1, 225, 20), Bright("teal", 1, 190, 15), Bright("green", 0.8, 145, 15), Bright("yellow", 0.8, 90, 10), Bright("violet", 0.8, 300, 15), Coats.Albino(0.6) };
                    h.GlowChance = 1.0;
                    h.EnergyMin = 0.6;
                    break;
                case "ooze":
                    // toxic green, sulphur, violet and blood goo; bright enough to read as wet jelly
                    // (dark yellows turned into brown "potatoes" in the uncurated review)
                    h.Coats = new[] { Bright("green", 2, 140, 15), Bright("sulphur", 0.6, 102, 6), Bright("violet", 1, 300, 15), Bright("blood", 0.5, 25, 8), Bright("teal", 0.6, 185, 10) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["mottled"] = 2, ["spots"] = 1 };
                    h.WeightMin = 0.5; h.EnergyMax = 0.55;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            bool glowEye = Schema.Get("eyes.style").Options[(int)values[Schema.IndexOf("eyes.style")]].Id == "glow";
            if (glowEye || arch == "wisp") Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.65));
        }

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            string move = g.GetChoice("move");
            bool floating = move != "hop";
            double size = g.Get("size");
            double baseL = DMath.Lerp(10, 32, DMath.Pow(size, 1.05));
            double Rw = baseL * DMath.Lerp(0.3, 0.55, g.Get("body.width"));
            double Rh = baseL * DMath.Lerp(0.22, 0.62, g.Get("body.height"));
            bool wisp = form == "wisp";
            bool jelly = form == "jelly";
            bool beholder = form == "beholder";
            if (beholder || wisp) Rh = Rw * DMath.Lerp(0.9, 1.1, g.Get("body.height"));

            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            // lower mass (pelvis) and upper dome (chest). Both bones sit at the body base and point
            // forwards (+X, +Y up), so spine pitch/yaw sways the upper dome over the base (jelly wobble)
            // and the dorsal line of every primitive is the top (belly colours stay underneath).
            double y0 = floating ? Rh * 0.55 : Rh * 0.42;
            Vec3 P = new Vec3(0, y0, 0);
            int pelvis = b.BoneAlong("pelvis", root, P, Vec3.UnitX, Vec3.UnitY, BoneRole.Pelvis, 0, Rw);
            Vec3 C = P + new Vec3(0, Rh * 0.45, 0);
            int chest = b.BoneAlong("chest", pelvis, P, Vec3.UnitX, Vec3.UnitY, BoneRole.Chest, 0, Rw);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, chest }, Lengths = new[] { Rh * 0.45 }, TotalLength = Rh * 0.45 };

            int body = b.Group("body", DMath.Clamp(Rw * 0.4, 1.5, 4.5));
            double dom = Rw * 2.2;
            double up = C.Y - P.Y;
            // bone frames: +X forwards, +Y up, +Z right
            if (jelly)
            {
                var bell = b.Ellipsoid(body, chest, new Vec3(0, up - Rh * 0.1, 0), new Vec3(Rw, Rh * 0.75, Rw), MaterialSlot.Primary, PatternDomain.Body, 0.0, 1.0);
                bell.DomainLength = dom; bell.Tag = "bell";
                var skirt = b.Ellipsoid(body, pelvis, new Vec3(0, Rh * 0.15, 0), new Vec3(Rw * 1.05, Rh * 0.22, Rw * 1.05), MaterialSlot.Primary, PatternDomain.Body, 0.0, 1.0);
                skirt.DomainLength = dom; skirt.Tag = "skirt";
                skirt.Flags |= PrimFlags.Translucent;
            }
            else if (wisp)
            {
                var orb = b.Ellipsoid(body, chest, new Vec3(0, up - Rh * 0.2, 0), new Vec3(Rw * 0.85, Rh * 0.8, Rw * 0.85), MaterialSlot.Glow, PatternDomain.None);
                orb.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern;
                orb.Tag = "orb";
            }
            else
            {
                var lower = b.Ellipsoid(body, pelvis, new Vec3(0, -y0 * 0.25, 0), new Vec3(Rw, Rh * 0.55, Rw), MaterialSlot.Primary, PatternDomain.Body, 0.0, 1.0);
                lower.DomainLength = dom; lower.Tag = "lower";
                var upper = b.Ellipsoid(body, chest, new Vec3(-Rw * 0.05, up - Rh * 0.1, 0), new Vec3(Rw * 0.82, Rh * 0.62, Rw * 0.86), MaterialSlot.Primary, PatternDomain.Body, 0.05, 0.95);
                upper.DomainLength = dom; upper.Tag = "upper";
                if (form == "octopus")
                {
                    // the mantle leans backwards behind the head
                    var mantle = b.Ellipsoid(body, chest, new Vec3(-Rw * 0.45, up + Rh * 0.35, 0), new Vec3(Rw * 0.7, Rh * 0.65, Rw * 0.7), MaterialSlot.Primary, PatternDomain.Body, 0.0, 0.6);
                    mantle.DomainLength = dom; mantle.Tag = "mantle";
                }
            }
            // lumps: seeded bulges on the surface
            double lumps = g.Get("body.lumps");
            if (lumps > 0.2 && !wisp)
            {
                var rng = b.RngFor("lumps");
                int count = (int)Math.Round(DMath.Lerp(2, 7, lumps));
                for (int i = 0; i < count; i++)
                {
                    double a = rng.Range(0.0, DMath.TwoPi);
                    double h = rng.Range(-0.3, 0.6);
                    double r = Rw * rng.Range(0.25, 0.45);
                    bool high = h > 0.2;
                    Vec3 c = new Vec3(DMath.Cos(a) * Rw * 0.75, (high ? up : 0) + h * Rh * 0.6 - (high ? up * 0.5 : 0), DMath.Sin(a) * Rw * 0.75);
                    var e = b.Ellipsoid(body, high ? chest : pelvis, c, new Vec3(r, r, r), MaterialSlot.Primary, PatternDomain.Body, 0.2, 0.8);
                    e.DomainLength = dom;
                }
            }

            // ---- face: eyes on the front of the upper body
            double eyeCountG = g.Get("eyes.count");
            int eyeCount = beholder ? 1 : (eyeCountG < 0.33 ? 2 : (eyeCountG < 0.66 ? (eyeCountG < 0.5 ? 1 : 2) : 3));
            if (form == "ooze" && eyeCountG > 0.6) eyeCount = 3;
            string eyeStyle = g.GetChoice("eyes.style");
            double eyeSize = DMath.Clamp(DMath.Lerp(1.6, 5.0, g.Get("eyes.size")) * Math.Sqrt(Rw / 6.0) * (beholder ? 1.6 : 1.0), 1.4, beholder ? 9.0 : 6.0);
            double spacing = DMath.Lerp(0.25, 0.6, g.Get("eyes.spacing"));
            var eyeStyleEnum = eyeStyle switch { "round" => EyeStyle.Round, "bead" => EyeStyle.Bead, "glow" => EyeStyle.Glow, "slit" => EyeStyle.Slit, _ => EyeStyle.Button };
            for (int i = 0; i < eyeCount; i++)
            {
                double lat = eyeCount == 1 ? 0 : DMath.Lerp(-spacing, spacing, i / (double)(eyeCount - 1));
                double lift = eyeCount == 3 && i == 1 ? 0.3 : 0.12;
                if (form == "octopus") lift = 0.05;
                // point on the upper ellipsoid surface facing forwards (creature space)
                Vec3 dirC = new Vec3(Math.Sqrt(Math.Max(0.1, 1 - lat * lat)), lift, lat).Normalized();
                Vec3 center = C + new Vec3(0, -Rh * 0.1, 0);
                Vec3 surf = center + new Vec3(dirC.X * Rw * 0.8, dirC.Y * Rh * 0.62, dirC.Z * Rw * 0.84);
                int bone = chest;
                var xf = b.RestWorld(bone);
                b.Feature(new FeatureDef
                {
                    Kind = FeatureKind.Eye,
                    Bone = bone,
                    LocalPosition = xf.InversePoint(surf),
                    LocalNormal = xf.InverseDir(dirC),
                    Size = eyeSize,
                    Style = eyeStyleEnum,
                    Side = eyeCount == 1 ? 0 : (lat < 0 ? -1 : (lat > 0 ? 1 : 0)),
                    Aspect = eyeStyle == "slit" ? 1.2 : 1.0,
                    Group = body,
                });
            }

            // ---- mouth: a dark interior shape recessed just behind the front surface. It hangs above a
            // pivot below it, so opening the "jaw" swings it forwards through the surface; the renderer
            // only keeps interior pixels that are framed by the body, which reads as an opening mouth.
            string mouth = g.GetChoice("mouth");
            if (mouth != "none" && !wisp)
            {
                double mw = Rw * (mouth == "small" ? 0.28 : (mouth == "wide" ? 0.55 : 0.62));
                double mh = Math.Max(0.8, Rh * (mouth == "maw" ? 0.26 : 0.13));
                double depthR = Math.Max(0.8, Rw * 0.18);
                double my = C.Y - Rh * 0.1 - Rh * 0.62 * 0.45;
                double sx = -Rw * 0.05 + Rw * 0.82 * Math.Sqrt(1 - 0.45 * 0.45);
                Vec3 M = new Vec3(sx - depthR - 0.5, my, 0);
                double d = Math.Max(2.0, Rh * 0.4);
                int jaw = b.BoneAlong("jaw", chest, M - new Vec3(0, d, 0), new Vec3(1, 0, 0), Vec3.UnitY, BoneRole.Jaw, 0, d);
                int mg = b.Group("mouth", 0.6, 0, GroupFlags.NoOutline | GroupFlags.NoContour | GroupFlags.Interior, 0.2);
                var m = b.Ellipsoid(mg, jaw, new Vec3(0, d, 0), new Vec3(depthR, mh, Math.Max(1.0, mw)), MaterialSlot.Mouth);
                m.Flags |= PrimFlags.NoShadow | PrimFlags.NoPattern;
                m.Tag = "mouth";
                b.Rig.Jaw = jaw;
                b.Rig.JawOpenMax = 0.8;
                string teeth = g.GetChoice("mouth.teeth");
                if (teeth != "none")
                {
                    int tg = b.Group("teeth", 0.3, 0, GroupFlags.NoContour, 0.5);
                    int count = teeth == "fangs" ? 2 : Math.Max(2, (int)Math.Round(mw / 1.6));
                    for (int i = 0; i < count; i++)
                    {
                        double z = DMath.Lerp(-mw * 0.6, mw * 0.6, i / Math.Max(1.0, count - 1.0));
                        double len = teeth == "fangs" ? Math.Max(1.4, mh * 0.9) : Math.Max(0.9, mh * 0.45);
                        var t = b.Cone(tg, jaw, new Vec3(depthR * 0.6, d + mh * 0.6, z), 0.55, jaw, new Vec3(depthR * 0.7, d + mh * 0.6 - len, z * 0.95), 0.3, MaterialSlot.Teeth);
                        t.Flags |= PrimFlags.Thin | PrimFlags.Hard | PrimFlags.NoShadow | PrimFlags.NoPattern;
                    }
                }
            }

            // ---- tentacles hanging from the underside
            double tc = g.Get("tentacles.count");
            int tentacles = jelly || form == "octopus" || wisp ? (int)Math.Round(DMath.Lerp(3, 8, tc)) : (tc > 0.3 ? (int)Math.Round(DMath.Lerp(2, 6, tc)) : 0);
            double tLen = baseL * DMath.Lerp(0.35, 1.2, g.Get("tentacles.length"));
            double tTh = Math.Max(0.55, Rw * DMath.Lerp(0.08, 0.28, g.Get("tentacles.thickness")));
            for (int i = 0; i < tentacles; i++)
            {
                double a = (i + 0.5) / tentacles * DMath.TwoPi + 0.3;
                double ring = jelly ? Rw * 0.75 : Rw * 0.55;
                Vec3 basePos = P + new Vec3(DMath.Cos(a) * ring, -y0 * (jelly ? 0.05 : 0.45), DMath.Sin(a) * ring);
                Vec3 dir = new Vec3(DMath.Cos(a) * (form == "octopus" ? 0.55 : 0.15), -1, DMath.Sin(a) * (form == "octopus" ? 0.55 : 0.15));
                var chain = Chains.Build(b, new ChainSpec
                {
                    Name = $"tentacle.{i}",
                    ParentBone = pelvis,
                    Base = basePos,
                    Direction = dir,
                    Length = tLen * (wisp ? 0.8 : 1.0) * (1.0 + 0.12 * DMath.Sin(i * 2.3)),
                    Segments = DMath.Clamp((int)Math.Round(tLen / 3.0), 3, 7),
                    RadiusBase = tTh,
                    RadiusTip = Math.Max(0.35, tTh * 0.3),
                    Curl = form == "octopus" ? 1.6 : 0.4,
                    Material = wisp ? MaterialSlot.Glow : MaterialSlot.Primary,
                    Role = BoneRole.Tentacle,
                    Domain = PatternDomain.Appendage,
                    Side = Math.Sin(a) >= 0 ? 1 : -1,
                    Stiffness = form == "octopus" ? 0.45 : 0.25,
                    Droop = form == "octopus" ? 0.55 : 0.75,
                    Flags = (tTh < 0.9 ? PrimFlags.Thin : PrimFlags.None) | PrimFlags.NoShadow | (wisp ? PrimFlags.Emissive : PrimFlags.None),
                });
                b.Rig.Appendages.Add(chain);
                b.Rig.Tentacles.Add(chain);
            }

            // ---- crown
            string crown = g.GetChoice("crown");
            double cs = g.Get("crown.size");
            Vec3 top = C + new Vec3(0, Rh * 0.55, 0);
            switch (crown)
            {
                case "spikes":
                {
                    var ds = new DorsalSpec { Kind = DorsalKind.Spikes, Size = Rw * DMath.Lerp(0.3, 0.7, cs), Material = MaterialSlot.Accent };
                    for (int i = 0; i < 4; i++)
                    {
                        double a = i / 4.0 * DMath.TwoPi + 0.4;
                        Vec3 nrm = new Vec3(DMath.Cos(a) * 0.5, 1, DMath.Sin(a) * 0.5).Normalized();
                        ds.Anchors.Add((chest, top + new Vec3(DMath.Cos(a) * Rw * 0.35, -Rh * 0.08, DMath.Sin(a) * Rw * 0.35), nrm, 0.9));
                    }
                    Dorsal.Build(b, ds);
                    break;
                }
                case "crystal":
                {
                    int g2 = b.Group("crystal", 0.3, 0, GroupFlags.None, 0.2);
                    double h = Rw * DMath.Lerp(0.5, 1.1, cs);
                    var cr = b.Cone(g2, chest, b.RestWorld(chest).InversePoint(top - new Vec3(0, h * 0.2, 0)), Math.Max(0.9, h * 0.32), chest, b.RestWorld(chest).InversePoint(top + new Vec3(0, h, 0)), 0.35,
                        MaterialSlot.Glow);
                    cr.Flags |= PrimFlags.Faceted | PrimFlags.NoPattern | PrimFlags.Emissive;
                    cr.Tag = "crystal";
                    break;
                }
                case "stalks":
                {
                    int n = beholder ? (int)Math.Round(DMath.Lerp(4, 7, cs)) : 2;
                    for (int i = 0; i < n; i++)
                    {
                        double a = DMath.Lerp(-1.2, 1.2, n == 1 ? 0.5 : i / (double)(n - 1));
                        Vec3 basePos = top + new Vec3(-Rw * 0.2 + Math.Abs(a) * Rw * 0.1, -Rh * 0.12, DMath.Sin(a) * Rw * 0.5);
                        var chain = Chains.Build(b, new ChainSpec
                        {
                            Name = $"stalk.{i}",
                            ParentBone = chest,
                            Base = basePos,
                            Direction = new Vec3(-0.2, 1, DMath.Sin(a) * 0.8),
                            Length = Rh * DMath.Lerp(0.6, 1.2, cs),
                            Segments = 3,
                            RadiusBase = Math.Max(0.6, Rw * 0.1),
                            RadiusTip = Math.Max(0.5, Rw * 0.08),
                            Curl = 0.9,
                            Role = BoneRole.Tentacle,
                            Domain = PatternDomain.Appendage,
                            Side = a >= 0 ? 1 : -1,
                            Stiffness = 0.5,
                            Droop = 0.15,
                        });
                        b.Rig.Appendages.Add(chain);
                        int tip = chain.Bones[chain.Bones.Length - 1];
                        var bulb = b.Ellipsoid(b.Group($"stalkeye.{i}", 0.4, 0, GroupFlags.None, 0.3), tip, new Vec3(0.3, 0, 0), new Vec3(1, 1, 1) * Math.Max(1.2, Rw * 0.2), MaterialSlot.Primary, PatternDomain.None);
                        bulb.Flags |= PrimFlags.NoPattern;
                        b.Feature(new FeatureDef
                        {
                            Kind = FeatureKind.Eye,
                            Bone = tip,
                            LocalPosition = new Vec3(0.3, 0, 0) + b.RestWorld(tip).InverseDir(new Vec3(1, 0.3, 0).Normalized()) * Math.Max(1.2, Rw * 0.2),
                            LocalNormal = b.RestWorld(tip).InverseDir(new Vec3(1, 0.3, 0).Normalized()),
                            Size = Math.Max(1.3, Rw * 0.2),
                            Style = EyeStyle.Bead,
                            Side = a >= 0 ? 1 : -1,
                            Group = -1,
                        });
                    }
                    break;
                }
                case "drip":
                {
                    // drips running down the sides
                    var rng = b.RngFor("drips");
                    for (int i = 0; i < 4; i++)
                    {
                        double a = rng.Range(0.0, DMath.TwoPi);
                        Vec3 c = new Vec3(DMath.Cos(a) * Rw * 0.95, -y0 * 0.6, DMath.Sin(a) * Rw * 0.95);
                        var d = b.Ellipsoid(body, pelvis, c, new Vec3(Rw * 0.14, Rw * 0.18, Rw * 0.14), MaterialSlot.Primary, PatternDomain.Body, 0.1, 0.2);
                        d.DomainLength = dom;
                    }
                    break;
                }
            }

            // ---- rig semantics & metrics
            double hover = g.Get("hover");
            b.Rig.Locomotion = floating ? LocomotionKind.Float : LocomotionKind.Hop;
            b.Rig.LocomotionModule = floating ? "float" : "hop";
            if (floating)
            {
                double minAlt = form == "octopus" ? tLen * 0.35 : 2;
                b.Rig.Altitude = Math.Max(minAlt, DMath.Lerp(2, baseL * 0.9 + (jelly ? tLen * 0.6 : 0), hover));
                if (move == "pulse") b.Rig.Tuning["float.pulse"] = 1;
            }
            b.Rig.Tuning["hop.length"] = Math.Max(4, Rw * 1.8);
            b.Rig.Tuning["hop.height"] = Math.Max(3, Rh * 0.9);
            b.Rig.Action = floating ? (tentacles > 0 ? ActionStyle.TentacleLash : ActionStyle.Roar) : (mouth == "maw" || mouth == "wide" ? ActionStyle.Bite : ActionStyle.Spray);
            b.Rig.Rest = floating ? RestStyle.Settle : RestStyle.Puddle;
            if (mouth == "maw") b.Rig.Tuning["idle.jaw"] = 0.4;
            b.Rig.Tuning["rest.bodyHeight"] = y0 * 0.7;
            b.Metrics.LegLength = Math.Max(3, Rh);
            b.Metrics.HipHeight = y0;
            b.Metrics.BackHeight = C.Y + Rh * 0.55;
            b.Metrics.BodyLength = Rw * 2.2;
            b.Metrics.Mass = DMath.Pow(baseL / 24.0, 3) * 0.9;
            double spd = DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5));
            b.Metrics.WalkSpeed = Math.Sqrt(0.5 * 400 * Rw) * spd;
            b.Metrics.RunSpeed = Math.Sqrt(1.8 * 400 * Rw) * spd;
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 22.0), 0.7, 1.4);
            // hoppers: the anchored body leads/trails the logical position by up to half a ground phase
            b.AnimationMargin = Math.Max(6, Rw * 0.9 + (floating ? tLen * 0.3 : b.Metrics.RunSpeed * 0.2 + 2));
            b.ExtraHeadroom = Math.Max(6, (floating ? b.Rig.Altitude : Rh * 1.1) + Rh * 0.3);
            return b.Build(g.AnatomyKey);
        }
    }
}
