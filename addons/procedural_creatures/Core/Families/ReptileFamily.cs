// Procedural Pixel Creatures - family 3: reptiles and dragons (lizards, crocodiles, tortoises,
// salamanders, drakes and winged dragons). Low slung bodies on sprawling or semi-erect legs, long
// heavy tails, scales; dragons may carry membrane wings and fly.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class ReptileFamily : CreatureFamilyBase
    {
        public override string Id => "reptile";
        public override string DisplayName => "Reptiles & Dragons";
        public override string Description => "Lizards, crocodiles, turtles, salamanders, wyrms and winged dragons: low bodies on splayed or partly upright legs, long tails and scales. Membrane-winged dragons can fly.";
        public override int Order => 30;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("lizard", "Lizard", 1.2),
            new ArchetypeInfo("crocodile", "Crocodile", 0.9),
            new ArchetypeInfo("tortoise", "Tortoise", 0.8),
            new ArchetypeInfo("salamander", "Salamander", 0.8),
            new ArchetypeInfo("drake", "Drake", 1.0),
            new ArchetypeInfo("dragon", "Dragon", 1.1),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("lizard", "Lizard"), ("crocodile", "Crocodile"), ("tortoise", "Tortoise"), ("salamander", "Salamander"), ("drake", "Drake"), ("dragon", "Dragon"),
        };
        public static readonly (string, string)[] Stances = { ("sprawling", "Sprawling"), ("erect", "Erect") };
        public static readonly (string, string)[] Crests = { ("none", "None"), ("frill", "Frill"), ("crest", "Crest"), ("spines", "Spines") };
        public static readonly (string, string)[] HornKinds = { ("none", "None"), ("short", "Short"), ("curved", "Curved"), ("backswept", "Swept back"), ("ram", "Ram"), ("nasal", "Nasal") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs"), ("sabre", "Sabre") };
        public static readonly (string, string)[] EyeStyles = { ("slit", "Slit pupils"), ("bead", "Beady"), ("round", "Round"), ("glow", "Glowing") };
        public static readonly (string, string)[] TailTips = { ("plain", "Plain"), ("club", "Club"), ("spade", "Spade"), ("fin", "Fin"), ("rattle", "Rattle") };
        public static readonly (string, string)[] BackKinds = { ("none", "None"), ("spikes", "Spikes"), ("plates", "Plates"), ("ridge", "Ridge"), ("sail", "Sail"), ("shell", "Shell") };
        public static readonly (string, string)[] WingKinds = { ("none", "None"), ("wings", "Wings") };
        public static readonly (string, string)[] ShellShapes = { ("dome", "Dome"), ("flat", "Flat"), ("high", "High"), ("spiked", "Spiked") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "lizard", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.5, "", 0.08)
             .Float("mass", "Mass", 0, 1, 0.5, "Slender to heavy.", 0.1)
             .Float("torso.length", "Torso length", 0, 1, 0.55, "", 0.1)
             .Float("torso.width", "Torso width", 0, 1, 0.55, "", 0.1)
             .Float("torso.chest", "Torso chest", 0, 1, 0.5, "", 0.1);
            b.Group("neck", "Neck", GeneStream.Anatomy)
             .Float("neck.length", "Neck length", 0, 1, 0.3, "", 0.1)
             .Float("neck.thickness", "Neck thickness", 0, 1, 0.6, "", 0.1)
             .Float("neck.carriage", "Neck carriage", 0, 1, 0.35, "", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("head.snout", "Head snout", 0, 1, 0.5, "", 0.1)
             .Float("head.snoutWidth", "Head snout width", 0, 1, 0.45, "", 0.1)
             .Float("head.jaw", "Head jaw", 0, 1, 0.5, "", 0.1)
             .Float("head.brow", "Head brow", 0, 1, 0.45, "", 0.1)
             .Choice("head.teeth", "Head teeth", TeethKinds, "small", "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.45, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "slit", "", 0.08)
             .Choice("head.crest", "Head crest", Crests, "none", "", 0.1)
             .Choice("head.horns", "Head horns", HornKinds, "none", "", 0.1)
             .Float("head.hornSize", "Head horn size", 0, 1, 0.5, "", 0.1);
            b.Group("legs", "Legs", GeneStream.Anatomy)
             .Float("legs.length", "Legs length", 0, 1, 0.35, "", 0.1)
             .Float("legs.thickness", "Legs thickness", 0, 1, 0.5, "", 0.1)
             .Choice("legs.stance", "Legs stance", Stances, "sprawling", "", 0.08)
             .Float("legs.claws", "Legs claws", 0, 1, 0.5, "", 0.1);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Float("tail.length", "Tail length", 0, 1, 0.65, "", 0.1)
             .Float("tail.thickness", "Tail thickness", 0, 1, 0.55, "", 0.1)
             .Float("tail.curl", "Tail curl", 0, 1, 0.2, "", 0.1)
             .Choice("tail.tip", "Tail tip", TailTips, "plain", "", 0.1);
            b.Group("back", "Back", GeneStream.Anatomy)
             .Choice("back.feature", "Back feature", BackKinds, "none", "", 0.1)
             .Float("back.size", "Back size", 0, 1, 0.5, "", 0.1)
             .Choice("shell.shape", "Shell shape", ShellShapes, "dome", "Only applies to creatures with a shell.", 0.08)
             .Choice("wings.kind", "Wings kind", WingKinds, "none", "Winged dragons can fly.", 0.08)
             .Float("wings.span", "Wings span", 0, 1, 0.55, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "scales");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        private sealed class Prior
        {
            public double Size, Mass, TorsoLen, Width, Chest, NeckLen, NeckTh, Carriage, HeadSize, Snout, SnoutW, Jaw, Brow, EyeSize, HornSize;
            public double LegLen, LegTh, Claws, TailLen, TailTh, Curl, BackSize, Span;
            public (string, double)[] Stance = { ("sprawling", 1) }, Crest = { ("none", 1) }, Horns = { ("none", 1) }, Teeth = { ("small", 1) };
            public (string, double)[] Eyes = { ("slit", 1) }, Tips = { ("plain", 1) }, Back = { ("none", 1) }, Wings = { ("none", 1) };
            public (string, double)[] Shell = { ("dome", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            "crocodile" => new Prior
            {
                Size = 0.72, Mass = 0.6, TorsoLen = 0.75, Width = 0.7, Chest = 0.45, NeckLen = 0.15, NeckTh = 0.85, Carriage = 0.1,
                HeadSize = 0.55, Snout = 0.95, SnoutW = 0.35, Jaw = 0.55, Brow = 0.5, EyeSize = 0.3, HornSize = 0.3,
                LegLen = 0.2, LegTh = 0.6, Claws = 0.5, TailLen = 0.85, TailTh = 0.8, Curl = 0.05, BackSize = 0.45, Span = 0.3,
                Teeth = new[] { ("small", 3.0), ("fangs", 1.0) }, Eyes = new[] { ("slit", 3.0), ("bead", 1.0) },
                Back = new[] { ("ridge", 3.0), ("plates", 1.5) }, Tips = new[] { ("plain", 4.0), ("fin", 0.8) },
            },
            "tortoise" => new Prior
            {
                Size = 0.5, Mass = 0.8, TorsoLen = 0.35, Width = 0.85, Chest = 0.5, NeckLen = 0.4, NeckTh = 0.5, Carriage = 0.45,
                HeadSize = 0.62, Snout = 0.2, SnoutW = 0.5, Jaw = 0.5, Brow = 0.3, EyeSize = 0.4, HornSize = 0.3,
                LegLen = 0.25, LegTh = 0.75, Claws = 0.3, TailLen = 0.1, TailTh = 0.4, Curl = 0.0, BackSize = 0.75, Span = 0.3,
                Stance = new[] { ("erect", 3.0), ("sprawling", 1.0) }, Teeth = new[] { ("none", 4.0) }, Eyes = new[] { ("bead", 4.0), ("round", 1.0) },
                Back = new[] { ("shell", 1.0) }, Horns = new[] { ("none", 5.0), ("nasal", 0.5) },
                Shell = new[] { ("dome", 2.0), ("flat", 1.5), ("high", 1.2), ("spiked", 1.0) },
            },
            "salamander" => new Prior
            {
                Size = 0.3, Mass = 0.45, TorsoLen = 0.65, Width = 0.55, Chest = 0.45, NeckLen = 0.1, NeckTh = 0.9, Carriage = 0.15,
                HeadSize = 0.6, Snout = 0.2, SnoutW = 0.85, Jaw = 0.35, Brow = 0.1, EyeSize = 0.6, HornSize = 0.3,
                LegLen = 0.25, LegTh = 0.45, Claws = 0.0, TailLen = 0.7, TailTh = 0.6, Curl = 0.1, BackSize = 0.4, Span = 0.3,
                Teeth = new[] { ("none", 3.0), ("small", 1.0) }, Eyes = new[] { ("bead", 3.0), ("round", 1.0), ("button", 0.0) },
                Crest = new[] { ("none", 2.0), ("frill", 1.5) }, Tips = new[] { ("fin", 3.0), ("plain", 1.0) },
                Back = new[] { ("none", 2.0), ("ridge", 1.0), ("sail", 0.6) },
            },
            "drake" => new Prior
            {
                Size = 0.75, Mass = 0.72, TorsoLen = 0.55, Width = 0.65, Chest = 0.7, NeckLen = 0.45, NeckTh = 0.75, Carriage = 0.5,
                HeadSize = 0.5, Snout = 0.6, SnoutW = 0.5, Jaw = 0.75, Brow = 0.8, EyeSize = 0.35, HornSize = 0.6,
                LegLen = 0.55, LegTh = 0.75, Claws = 0.8, TailLen = 0.65, TailTh = 0.7, Curl = 0.15, BackSize = 0.6, Span = 0.4,
                Stance = new[] { ("erect", 4.0), ("sprawling", 1.0) }, Teeth = new[] { ("fangs", 3.0), ("sabre", 0.6) },
                Eyes = new[] { ("slit", 2.0), ("glow", 1.2) }, Horns = new[] { ("curved", 2.0), ("backswept", 2.0), ("ram", 0.8), ("short", 1.0) },
                Crest = new[] { ("none", 2.0), ("spines", 1.5), ("crest", 0.6) }, Back = new[] { ("spikes", 3.0), ("plates", 1.5), ("ridge", 1.0) },
                Tips = new[] { ("club", 1.5), ("spade", 1.0), ("plain", 1.5) },
            },
            "dragon" => new Prior
            {
                Size = 0.7, Mass = 0.55, TorsoLen = 0.55, Width = 0.55, Chest = 0.75, NeckLen = 0.72, NeckTh = 0.55, Carriage = 0.78,
                HeadSize = 0.6, Snout = 0.6, SnoutW = 0.45, Jaw = 0.6, Brow = 0.7, EyeSize = 0.4, HornSize = 0.7,
                LegLen = 0.55, LegTh = 0.55, Claws = 0.8, TailLen = 0.8, TailTh = 0.5, Curl = 0.25, BackSize = 0.5, Span = 0.78,
                Stance = new[] { ("erect", 5.0) }, Teeth = new[] { ("fangs", 3.0), ("small", 1.0) },
                Eyes = new[] { ("slit", 2.0), ("glow", 1.5) }, Horns = new[] { ("backswept", 3.0), ("curved", 2.0), ("ram", 0.6) },
                Crest = new[] { ("none", 2.0), ("spines", 1.0), ("frill", 0.6) }, Back = new[] { ("spikes", 3.0), ("none", 1.0), ("sail", 0.4) },
                Tips = new[] { ("spade", 3.0), ("plain", 1.0), ("club", 0.5) }, Wings = new[] { ("wings", 1.0) },
            },
            _ => new Prior
            {
                Size = 0.4, Mass = 0.4, TorsoLen = 0.55, Width = 0.5, Chest = 0.5, NeckLen = 0.25, NeckTh = 0.6, Carriage = 0.3,
                HeadSize = 0.5, Snout = 0.45, SnoutW = 0.45, Jaw = 0.45, Brow = 0.4, EyeSize = 0.5, HornSize = 0.4,
                LegLen = 0.35, LegTh = 0.45, Claws = 0.6, TailLen = 0.8, TailTh = 0.45, Curl = 0.2, BackSize = 0.45, Span = 0.3,
                Teeth = new[] { ("small", 3.0), ("none", 1.0) }, Eyes = new[] { ("slit", 2.0), ("bead", 1.5), ("round", 1.0) },
                Crest = new[] { ("none", 3.0), ("frill", 1.0), ("crest", 1.0), ("spines", 0.6) }, Horns = new[] { ("none", 4.0), ("short", 1.0), ("nasal", 0.4) },
                Back = new[] { ("none", 2.0), ("spikes", 1.5), ("ridge", 1.0), ("sail", 0.5) }, Tips = new[] { ("plain", 4.0), ("rattle", 0.4), ("club", 0.3) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.13);
            Around(v, rng, "mass", p.Mass, sp);
            Around(v, rng, "torso.length", p.TorsoLen, sp);
            Around(v, rng, "torso.width", p.Width, sp);
            Around(v, rng, "torso.chest", p.Chest, sp);
            Around(v, rng, "neck.length", p.NeckLen, sp);
            Around(v, rng, "neck.thickness", p.NeckTh, sp);
            Around(v, rng, "neck.carriage", p.Carriage, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            Around(v, rng, "head.snout", p.Snout, sp);
            Around(v, rng, "head.snoutWidth", p.SnoutW, sp);
            Around(v, rng, "head.jaw", p.Jaw, sp);
            Around(v, rng, "head.brow", p.Brow, sp);
            SetChoice(v, "head.teeth", PickWeighted(rng, p.Teeth));
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "head.crest", PickWeighted(rng, p.Crest));
            SetChoice(v, "head.horns", PickWeighted(rng, p.Horns));
            Around(v, rng, "head.hornSize", p.HornSize, sp);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            SetChoice(v, "legs.stance", PickWeighted(rng, p.Stance));
            Around(v, rng, "legs.claws", p.Claws, sp);
            Around(v, rng, "tail.length", p.TailLen, sp);
            Around(v, rng, "tail.thickness", p.TailTh, sp);
            Around(v, rng, "tail.curl", p.Curl, sp);
            SetChoice(v, "tail.tip", PickWeighted(rng, p.Tips));
            SetChoice(v, "back.feature", PickWeighted(rng, p.Back));
            Around(v, rng, "back.size", p.BackSize, sp);
            SetChoice(v, "wings.kind", PickWeighted(rng, p.Wings));
            Around(v, rng, "wings.span", p.Span, sp);
            SetChoice(v, "shell.shape", PickWeighted(rng, p.Shell));
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["scales"] = 5, ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 1.5, ["bands"] = 1, ["dorsal"] = 1, ["spots"] = 1, ["mottled"] = 1, ["stripes"] = 0.6 },
                Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 2 },
                // lizards: green, olive, desert sand, brown, teal, blue-tailed, rust, yellow, grey
                Coats = new[] { Coats.Green(2), Coats.Olive(1.5), Coats.Sand(1.2), Coats.Brown(1), Coats.Teal(0.6), Coats.Rust(0.5), Coats.Grey(0.4), Coats.Blue(0.4), Coats.Yellow(0.3) },
                CountershadeMin = 0.35, CountershadeMax = 0.85,
                Schemes = new Dictionary<string, double> { ["natural"] = 3, ["analogous"] = 2, ["complementary"] = 1, ["split"] = 0.6, ["monochrome"] = 1 },
                SocksChance = 0.04, MaskChance = 0.03, PiebaldChance = 0.02,
            };
            switch (arch)
            {
                case "crocodile":
                    h.Coats = new[] { Coats.Olive(2.2), Coats.Brown(1), Coats.Grey(1), Coats.Green(0.6), Coats.Slate(0.5), Coats.Sand(0.4), Coats.Albino(0.1) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1, ["bands"] = 2, ["mottled"] = 1 };
                    h.WeightMin = 0.5; h.EnergyMax = 0.5;
                    break;
                case "tortoise":
                    h.Coats = new[] { Coats.Brown(1.8), Coats.Olive(1.5), Coats.Sand(1.2), Coats.Chocolate(1), Coats.Grey(0.6), Coats.Green(0.5) };
                    h.Materials = new Dictionary<string, double> { ["scales"] = 3, ["hide"] = 2 };
                    h.WeightMin = 0.6; h.EnergyMax = 0.4;
                    break;
                case "salamander":
                    // fire salamander black, red and orange efts, blue, axolotl pink, green newts, albino
                    h.Materials = new Dictionary<string, double> { ["hide"] = 4, ["slime"] = 1.5 };
                    h.Coats = new[] { Coats.Crimson(1.2), Coats.Black(1.0), Coats.Yellow(0.8), Coats.Rust(1.0), Coats.Blue(0.6), Coats.Pink(0.7), Coats.Green(0.6), Coats.Albino(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 3, ["none"] = 1, ["stripes"] = 1 };
                    h.EnergyMin = 0.35;
                    break;
                case "drake":
                    h.Coats = new[] { Coats.Crimson(1), Coats.Rust(1), Coats.Green(1), Coats.Blue(0.8), Coats.Violet(0.6), Coats.Black(0.6), Coats.Sand(0.5), Coats.Vivid(1), Coats.Muted(0.5) };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 2, ["gold"] = 1 };
                    h.GlowChance = 0.25;
                    h.WeightMin = 0.55;
                    break;
                case "dragon":
                    h.Coats = new[] { Coats.Crimson(1.2), Coats.Green(1), Coats.Blue(1), Coats.Black(0.8), Coats.Violet(0.8), Coats.Golden(0.6), Coats.Silver(0.5), Coats.Teal(0.6), Coats.Vivid(1) };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1.5, ["gold"] = 1.2, ["vivid"] = 0.6 };
                    h.Schemes = new Dictionary<string, double> { ["natural"] = 2, ["analogous"] = 2, ["complementary"] = 1.4, ["split"] = 1, ["monochrome"] = 1 };
                    h.GlowChance = 0.3;
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
            string backF = g.GetChoice("back.feature");
            bool shell = backF == "shell";
            bool winged = g.GetChoice("wings.kind") == "wings";
            string stance = g.GetChoice("legs.stance");
            bool sprawl = stance == "sprawling";

            double size = g.Get("size");
            double baseL = DMath.Lerp(11, 36, DMath.Pow(size, 1.05));
            double L = baseL * DMath.Lerp(0.85, 1.45, g.Get("torso.length"));
            double mass = g.Get("mass");
            double R = baseL * DMath.Lerp(0.16, 0.28, mass);
            double chestR = R * DMath.Lerp(0.95, 1.2, g.Get("torso.chest"));
            double hipR = R * DMath.Lerp(1.02, 0.85, g.Get("torso.chest"));
            double wz = DMath.Lerp(0.95, 1.45, g.Get("torso.width"));
            double legLenG = g.Get("legs.length");
            double hipH = sprawl ? Math.Max(hipR * 0.9 + 0.8, baseL * DMath.Lerp(0.22, 0.42, legLenG))
                                 : Math.Max(hipR * 0.95 + 1.2, baseL * DMath.Lerp(0.38, 0.85, legLenG));
            double shoulderH = hipH * (winged ? 1.06 : 1.0);

            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            Vec3 P = new Vec3(-L / 2, hipH, 0);
            Vec3 C = new Vec3(L / 2, shoulderH, 0);
            Vec3 spineDir = (C - P).Normalized();
            int pelvis = b.BoneAlong("pelvis", root, P, spineDir, Vec3.UnitY, BoneRole.Pelvis, 0, L / 2);
            int spine = b.BoneAlong("spine", pelvis, (P + C) * 0.5, spineDir, Vec3.UnitY, BoneRole.Spine, 0, L / 2);
            int chest = b.BoneAlong("chest", spine, C, spineDir, Vec3.UnitY, BoneRole.Chest, 0, chestR);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { L / 2, L / 2 }, TotalLength = L };

            double blend = DMath.Clamp(R * 0.35, 1.5, 4.0);
            int body = b.Group("body", blend);
            double dom = L + hipR + chestR;
            var rump = b.Ellipsoid(body, pelvis, new Vec3(-hipR * 0.2, 0, 0), new Vec3(hipR * 1.2, hipR * 0.85, hipR * wz * 0.95), MaterialSlot.Primary, PatternDomain.Body, 0, 0.3);
            rump.DomainLength = dom; rump.Tag = "rump";
            var mid = b.Ellipsoid(body, spine, new Vec3(0, -R * 0.08, 0), new Vec3(L * 0.5, R * 0.9, R * wz), MaterialSlot.Primary, PatternDomain.Body, 0.15, 0.85);
            mid.DomainLength = dom; mid.Tag = "barrel";
            var chestP = b.Ellipsoid(body, chest, new Vec3(chestR * 0.05, 0, 0), new Vec3(chestR * 1.15, chestR * 0.92, chestR * wz * 0.92), MaterialSlot.Primary, PatternDomain.Body, 0.7, 1.0);
            chestP.DomainLength = dom; chestP.Tag = "chest";

            // ---- neck & head
            double neckLen = baseL * DMath.Lerp(0.08, 0.7, g.Get("neck.length"));
            double carriage = DMath.Lerp(4, 58, g.Get("neck.carriage")) * DMath.Deg2Rad;
            Vec3 neckDir = new Vec3(DMath.Cos(carriage), DMath.Sin(carriage), 0);
            double skullL = baseL * DMath.Lerp(0.26, 0.4, g.Get("head.size"));
            double skullH = skullL * (form == "salamander" ? 0.62 : 0.7);
            double skullW = skullL * (form == "salamander" ? 1.0 : 0.8);
            double neckTh = g.Get("neck.thickness");
            double nr0 = Math.Max(1.2, chestR * DMath.Lerp(0.5, 0.85, neckTh));
            double nr1 = Math.Max(1.0, Math.Min(nr0, skullH * DMath.Lerp(0.45, 0.6, neckTh)));
            Vec3 N0 = C + new Vec3(chestR * 0.55, chestR * 0.2, 0);
            int neck0 = b.BoneAlong("neck.0", chest, N0, neckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            int neck1 = b.BoneAlong("neck.1", neck0, N0 + neckDir * (neckLen * 0.5), neckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            Vec3 Hj = N0 + neckDir * neckLen;
            string teeth = g.GetChoice("head.teeth");
            string eye = g.GetChoice("head.eyeStyle");
            var hs = new HeadSpec
            {
                ParentBone = neck1,
                Position = Hj,
                Forward = new Vec3(1, -DMath.Lerp(0.0, 0.35, g.Get("neck.carriage")), 0),
                SkullLength = skullL,
                SkullHeight = skullH,
                SkullWidth = skullW,
                SkullOffset = 0.25,
                SnoutLength = skullL * DMath.Lerp(0.2, 1.6, g.Get("head.snout")),
                SnoutRadius = skullH * DMath.Lerp(0.3, 0.42, g.Get("head.snoutWidth")),
                SnoutTaper = DMath.Lerp(0.6, 0.95, g.Get("head.snoutWidth")),
                SnoutDrop = 0.04,
                SnoutHeight = -0.1,
                SnoutWidth = DMath.Lerp(0.9, 1.45, g.Get("head.snoutWidth")),
                NoseSize = form == "salamander" ? 0.0 : 0.55,
                DarkNose = false,
                JawLength = DMath.Lerp(0.85, 1.0, g.Get("head.jaw")),
                JawRadius = skullH * DMath.Lerp(0.18, 0.3, g.Get("head.jaw")),
                JawDepth = DMath.Lerp(0.6, 1.0, g.Get("head.jaw")),
                Brow = g.Get("head.brow"),
                Cheeks = form == "salamander" ? 0.3 : 0.0,
                EyeSize = DMath.Clamp(DMath.Lerp(1.3, 3.6, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 9.0), 1.3, 5.0),
                EyeStyle = QuadrupedFamily.ParseEye(eye),
                EyeForward = form == "crocodile" ? 0.25 : 0.4,
                EyeHeight = form == "crocodile" ? 0.75 : 0.4,
                EyeAspect = eye == "slit" ? 1.2 : 1.0,
                Teeth = teeth switch { "none" => TeethKind.None, "fangs" => TeethKind.Fangs, "sabre" => TeethKind.Sabre, _ => TeethKind.Small },
                TeethSize = DMath.Clamp(skullL / 8.0, 0.8, 1.6),
                Group = body,
                DomainLength = skullL * 2.2,
                JawOpenMax = form == "crocodile" ? 0.8 : 0.65,
                Nostrils = form != "salamander",
            };
            if (form == "tortoise") { hs.Teeth = TeethKind.Beak; hs.SnoutMaterial = MaterialSlot.Primary; hs.JawOpenMax = 0.45; }
            var head = Heads.Build(b, hs);
            var nk0 = b.Segment(body, neck0, nr0, neck1, (nr0 + nr1) * 0.5, MaterialSlot.Primary, PatternDomain.Neck, 0, 0.5);
            nk0.DomainLength = neckLen + 2;
            var nk1 = b.Cone(body, neck1, Vec3.Zero, (nr0 + nr1) * 0.5, head.Head, head.SkullCenterLocal * 0.5, nr1, MaterialSlot.Primary, PatternDomain.Neck, 0.5, 1.0);
            nk1.DomainLength = neckLen + 2;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck0, neck1, head.Head }, Lengths = new[] { neckLen * 0.5, neckLen * 0.5 }, TotalLength = neckLen };

            string crest = g.GetChoice("head.crest");
            if (crest != "none") BuildCrest(b, crest, head, skullL, skullH, skullW);
            string horns = g.GetChoice("head.horns");
            if (horns != "none")
            {
                Horns.Build(b, new HornSpec
                {
                    Kind = QuadrupedFamily.ParseHorn(horns),
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(-skullL * 0.1, skullH * 0.4, skullW * 0.25),
                    Front = head.SnoutTipLocal + new Vec3(-hs.SnoutLength * 0.3, hs.SnoutRadius * 0.7, 0),
                    Size = skullL * DMath.Lerp(0.45, 1.4, g.Get("head.hornSize")),
                    Thickness = DMath.Lerp(0.8, 1.2, mass),
                });
                b.Trait("horns:" + horns);
            }

            // ---- tail (heavy, low; drags when it droops)
            double tailLen = baseL * DMath.Lerp(0.4, 1.9, g.Get("tail.length"));
            string tip = g.GetChoice("tail.tip");
            double tr0 = Math.Max(1.0, hipR * DMath.Lerp(0.45, 0.85, g.Get("tail.thickness")));
            if (tailLen > 3)
            {
                b.Rig.Tail = Chains.Build(b, new ChainSpec
                {
                    Name = "tail",
                    ParentBone = pelvis,
                    Base = P + new Vec3(-hipR * 0.95, 0, 0),
                    Direction = new Vec3(-1, -0.18, 0),
                    Length = tailLen,
                    Segments = DMath.Clamp((int)Math.Round(tailLen / 3.2), 4, 12),
                    RadiusBase = tr0,
                    RadiusTip = Math.Max(0.45, tr0 * 0.2),
                    TaperPower = 0.8,
                    Curl = DMath.Lerp(-0.2, 2.2, g.Get("tail.curl")),
                    Tip = tip switch { "club" => TailTip.Club, "spade" => TailTip.Spade, "fin" => TailTip.Fin, "rattle" => TailTip.Rattle, _ => TailTip.Plain },
                    TipSize = DMath.Clamp(tr0 * 0.75, 0.9, 2.6),
                    Group = body,
                    Stiffness = DMath.Lerp(0.35, 0.7, g.Get("tail.thickness")),
                    Droop = 0.75,
                });
            }

            // ---- legs
            double legTh = g.Get("legs.thickness");
            double claws = g.Get("legs.claws");
            for (int pair = 0; pair < 2; pair++)
            {
                bool front = pair == 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    double bodyR = front ? chestR : hipR;
                    Vec3 hipJ = front
                        ? C + new Vec3(chestR * 0.15, -chestR * 0.35, side * chestR * wz * 0.72)
                        : P + new Vec3(hipR * 0.1, -hipR * 0.3, side * hipR * wz * 0.78);
                    double spread = sprawl ? bodyR * wz * 1.65 + hipJ.Y * 0.55 : bodyR * wz * 0.85;
                    Vec3 contact = new Vec3(hipJ.X + (front ? L * 0.06 : -L * 0.03), 0, side * spread);
                    double rTop = Math.Max(1.2, bodyR * DMath.Lerp(0.36, 0.6, legTh));
                    double dist = (contact - hipJ).Length;
                    var spec = new LegSpec
                    {
                        Name = (front ? "front" : "hind") + (side < 0 ? "L" : "R"),
                        Side = side, Pair = pair, Front = front,
                        AnchorBone = front ? chest : pelvis,
                        Hip = hipJ, Contact = contact,
                        Style = sprawl ? LegStyle.Sprawling : LegStyle.Digitigrade,
                        Length = sprawl ? dist * 1.25 + rTop : hipJ.Y * (front ? 1.18 : 1.3),
                        Upper = sprawl ? 0.44 : 0.38, Lower = sprawl ? 0.4 : 0.37, Distal = sprawl ? 0.16 : 0.25,
                        RadiusTop = rTop,
                        RadiusKnee = Math.Max(0.9, rTop * 0.7),
                        RadiusAnkle = Math.Max(0.75, rTop * 0.5),
                        RadiusFoot = Math.Max(0.75, rTop * 0.5),
                        Haunch = front ? 0.8 : 1.05,
                        PawLength = Math.Max(1.8, rTop * 1.9),
                        PawRadius = Math.Max(0.8, rTop * 0.6),
                        Claws = claws > 0.3 ? 3 : 0,
                        ClawSize = DMath.Lerp(0.5, 1.5, claws) * Math.Sqrt(baseL / 24.0),
                        LiftHeight = Math.Max(1.4, hipJ.Y * 0.28),
                        MetaAngle = front ? 10 : 22,
                        BlendRadius = DMath.Clamp(rTop * 0.45, 0.8, 2.2),
                        DomainLength = dist,
                    };
                    var leg = Limbs.Build(b, spec);
                    bool left = side < 0;
                    // lateral sequence walk: LH -> LF -> RH -> RF
                    leg.Phase = (front, left) switch { (false, true) => 0.0, (true, true) => 0.25, (false, false) => 0.5, _ => 0.75 };
                    b.Rig.Legs.Add(leg);
                }
            }

            // ---- back features
            double bsz = g.Get("back.size");
            if (shell)
            {
                // domed carapace with a rim, plates texture from the accent ramp
                int sg = b.Group("shell", 1.2, 0, GroupFlags.CreaseShade, 0.3);
                string shape = g.GetChoice("shell.shape");
                double shL = (L + hipR + chestR) * 0.6 * DMath.Lerp(0.9, 1.12, bsz);
                double shH = shL * shape switch { "flat" => DMath.Lerp(0.26, 0.34, bsz), "high" => DMath.Lerp(0.6, 0.74, bsz), _ => DMath.Lerp(0.42, 0.58, bsz) };
                double shW = Math.Max(R * wz * 1.45, shL * 0.78) * (shape == "flat" ? 1.1 : 1.0);
                var dome = b.Ellipsoid(sg, spine, new Vec3(-L * 0.03, R * 0.35, 0), new Vec3(shL, shH, shW), MaterialSlot.Marking, PatternDomain.Body, 0.1, 0.9);
                dome.Flags |= PrimFlags.Scutes | PrimFlags.ShadowCaster;
                dome.FacetSeed = (uint)(g.AnatomySeed & 0xFFFFF) | 1u;
                dome.DomainLength = dom;
                dome.Tag = "shell";
                var rim = b.Ellipsoid(sg, spine, new Vec3(-L * 0.03, R * 0.0, 0), new Vec3(shL * 1.05, Math.Max(1.0, R * 0.4), shW * 1.06), MaterialSlot.Secondary, PatternDomain.None);
                rim.Flags |= PrimFlags.NoPattern;
                rim.ShadeBias = -1;
                rim.Tag = "shell.rim";
                b.Trait("shell");
                if (shape == "spiked")
                {
                    // three keels of pointed scutes (alligator snapping turtle)
                    var sx = b.RestWorld(spine);
                    Vec3 sc = new Vec3(-L * 0.03, R * 0.35, 0);
                    var ks = new DorsalSpec { Kind = DorsalKind.Spikes, Size = Math.Max(1.4, shH * 0.32), Material = MaterialSlot.Marking, Name = "keel" };
                    for (int row = -1; row <= 1; row++)
                    {
                        double zr = row * 0.55;
                        for (int i = 0; i < 4; i++)
                        {
                            double xr = DMath.Lerp(-0.62, 0.55, i / 3.0);
                            double yr = Math.Sqrt(Math.Max(0.02, 1 - xr * xr - zr * zr));
                            Vec3 local = sc + new Vec3(xr * shL, yr * shH, zr * shW);
                            Vec3 nrm = sx.Dir(new Vec3(xr / shL, yr / shH, zr / shW)).NormalizedOr(Vec3.UnitY);
                            ks.Anchors.Add((spine, sx.Point(local), (nrm + Vec3.UnitY * 0.6).Normalized(), row == 0 ? 1.0 : 0.75));
                        }
                    }
                    Dorsal.Build(b, ks);
                    b.Trait("shell:spiked");
                }
                else if (shape != "dome") b.Trait("shell:" + shape);
            }
            else if (backF != "none")
            {
                var ds = new DorsalSpec
                {
                    Kind = backF switch { "spikes" => DorsalKind.Spikes, "plates" => DorsalKind.Plates, "ridge" => DorsalKind.Ridge, "sail" => DorsalKind.Sail, _ => DorsalKind.None },
                    Size = baseL * DMath.Lerp(0.07, 0.2, bsz) * (backF == "sail" ? 2.2 : 1.0),
                    Material = backF == "ridge" ? MaterialSlot.Primary : MaterialSlot.Accent,
                    BodyGroup = backF == "ridge" ? body : -1,
                };
                int n = DMath.Clamp((int)Math.Round((L + neckLen * 0.5) / DMath.Lerp(4.5, 3.0, bsz)), 3, 10);
                for (int i = 0; i < n; i++)
                {
                    double t = (i + 0.5) / n;
                    double x = DMath.Lerp(-L / 2 - hipR * 0.6, L / 2 + chestR * 0.4, t);
                    double top = Math.Max(EllTop(x, P.X - hipR * 0.2, P.Y, hipR * 1.2, hipR * 0.85), Math.Max(EllTop(x, (P.X + C.X) / 2, (P.Y + C.Y) / 2 - R * 0.08, L * 0.5, R * 0.9), EllTop(x, C.X + chestR * 0.05, C.Y, chestR * 1.15, chestR * 0.92)));
                    int bone = t < 0.33 ? pelvis : (t < 0.66 ? spine : chest);
                    double scale = backF == "sail" ? 0.4 + 0.8 * DMath.Sin(DMath.Pi * t) : 0.7 + 0.5 * DMath.Sin(DMath.Pi * DMath.Lerp(0.2, 0.9, t));
                    ds.Anchors.Add((bone, new Vec3(x, top - 0.3, 0), Vec3.UnitY, scale));
                }
                Dorsal.Build(b, ds);
                b.Trait("back:" + backF);
            }

            // ---- wings (dragons)
            double span = 0;
            if (winged)
            {
                span = baseL * DMath.Lerp(1.0, 2.1, g.Get("wings.span"));
                Wings.BuildPair(b, new WingSpec
                {
                    Kind = WingKind.Membrane,
                    AnchorBone = chest,
                    Root = C + new Vec3(-chestR * 0.1, chestR * 0.55, chestR * wz * 0.45),
                    Span = span,
                    Chord = span * 0.45,
                    Raise = 0.35,
                    SweepBack = 0.35,
                    BoneRadius = Math.Max(0.7, R * 0.22),
                    Fingers = 3,
                    BodyBone = pelvis,
                    BodyAttach = P + new Vec3(hipR * 0.3, hipR * 0.5, hipR * wz * 0.5),
                    ClawSize = DMath.Clamp(baseL / 20.0, 0.7, 1.5),
                });
                b.Trait("wings");
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = winged ? LocomotionKind.Fly : LocomotionKind.Legged;
            b.Rig.LocomotionModule = winged ? "flight" : "legged";
            if (winged)
            {
                b.Rig.Tuning["flight.flapHz"] = DMath.Lerp(2.6, 1.6, size);
                b.Rig.Altitude = Math.Max(12, hipH + baseL * 0.9);
            }
            b.Rig.Action = form switch
            {
                "crocodile" => ActionStyle.Bite,
                "tortoise" => ActionStyle.Bite,
                "salamander" => ActionStyle.TailSwipe,
                "drake" => tip == "club" ? ActionStyle.TailSwipe : ActionStyle.Roar,
                "dragon" => winged ? ActionStyle.WingBuffet : ActionStyle.Roar,
                _ => tip == "club" || tip == "spade" ? ActionStyle.TailSwipe : ActionStyle.Bite,
            };
            b.Rig.Rest = RestStyle.LieDown;
            b.Rig.Tuning["gait.walkDuty"] = sprawl ? 0.7 : 0.64;
            b.Rig.Tuning["gait.runDuty"] = sprawl ? 0.45 : 0.36;
            b.Rig.Tuning["gait.gallop"] = sprawl || form == "tortoise" ? 0 : 0.6;
            b.Rig.Tuning["rest.bodyHeight"] = Math.Max(R * 0.95, 1.5);
            b.Metrics.LegLength = Math.Max(hipH, 3) * (sprawl ? 1.3 : 1.0);
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = hipH + R;
            b.Metrics.BodyLength = L + hipR + chestR + neckLen + skullL + tailLen * 0.6;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * DMath.Lerp(0.6, 1.6, mass) * (shell ? 1.4 : 1.0);
            double legL = b.Metrics.LegLength;
            double slow = form == "tortoise" ? 0.55 : 1.0;
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * legL) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5)) * slow;
            b.Metrics.RunSpeed = Math.Sqrt(2.0 * 400 * legL) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * slow * (winged ? 1.5 : 1.0);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.5);
            // big horns sweep forwards when the head dips (bites, charges): reserve room for them
            double hornReach = horns != "none" ? skullL * DMath.Lerp(0.45, 1.4, g.Get("head.hornSize")) * 0.3 : 0;
            // long heavy tails lag behind when the body turns quickly (spring chain): extra room for the tip
            b.AnimationMargin = Math.Max(7, baseL * 0.32 + hornReach + tailLen * 0.06);
            b.ExtraHeadroom = winged ? Math.Max(8, span * 0.9 + b.Rig.Altitude) : Math.Max(4, skullL * 0.9 + neckLen * 0.3);
            return b.Build(g.AnatomyKey);
        }

        private static void BuildCrest(AnatomyBuilder b, string crest, HeadResult head, double skullL, double skullH, double skullW)
        {
            var hx = b.RestWorld(head.Head);
            switch (crest)
            {
                case "frill":
                {
                    // neck frill: a fan of membrane triangles behind the skull
                    int g = b.Group("frill", 0.4, 0, GroupFlags.NoFarShade, -0.4);
                    Vec3 c = head.SkullCenterLocal + new Vec3(-skullL * 0.45, 0, 0);
                    double r = skullH * 0.95;
                    for (int i = 0; i < 5; i++)
                    {
                        double a0 = DMath.Lerp(-1.4, 1.4, i / 5.0), a1 = DMath.Lerp(-1.4, 1.4, (i + 1) / 5.0);
                        Vec3 p0 = c + new Vec3(-r * 0.2, DMath.Cos(a0) * r, DMath.Sin(a0) * r * 1.1);
                        Vec3 p1 = c + new Vec3(-r * 0.2, DMath.Cos(a1) * r, DMath.Sin(a1) * r * 1.1);
                        var t = b.Triangle(g, head.Head, c, head.Head, p0, head.Head, p1, MaterialSlot.Membrane, PatternDomain.Fin);
                        t.Flags |= PrimFlags.TwoSided | PrimFlags.Translucent | PrimFlags.NoShadow;
                        t.DomainLength = r;
                    }
                    break;
                }
                case "crest":
                {
                    int g = b.Group("crest", 0.4, 0, GroupFlags.None, -0.2);
                    Vec3 a = head.SkullCenterLocal + new Vec3(skullL * 0.3, skullH * 0.4, 0);
                    Vec3 z = head.SkullCenterLocal + new Vec3(-skullL * 0.6, skullH * 0.3, 0);
                    Vec3 top = head.SkullCenterLocal + new Vec3(-skullL * 0.35, skullH * 1.2, 0);
                    var t = b.Triangle(g, head.Head, a, head.Head, top, head.Head, z, MaterialSlot.Accent, PatternDomain.None);
                    t.Flags |= PrimFlags.TwoSided | PrimFlags.NoPattern;
                    break;
                }
                default: // spines around the back of the skull
                {
                    var ds = new DorsalSpec { Kind = DorsalKind.Spikes, Size = skullL * 0.45, Material = MaterialSlot.Accent };
                    for (int i = 0; i < 5; i++)
                    {
                        double a = DMath.Lerp(-1.2, 1.2, i / 4.0);
                        Vec3 local = head.SkullCenterLocal + new Vec3(-skullL * 0.4, DMath.Cos(a) * skullH * 0.45, DMath.Sin(a) * skullW * 0.45);
                        Vec3 nrm = hx.Dir(new Vec3(-0.8, DMath.Cos(a), DMath.Sin(a))).Normalized();
                        ds.Anchors.Add((head.Head, hx.Point(local), nrm, 0.8));
                    }
                    Dorsal.Build(b, ds);
                    break;
                }
            }
        }

        private static double EllTop(double x, double cx, double cy, double rx, double ry)
        {
            double t = (x - cx) / rx;
            if (Math.Abs(t) >= 1) return 0;
            return cy + ry * Math.Sqrt(1 - t * t);
        }
    }
}
