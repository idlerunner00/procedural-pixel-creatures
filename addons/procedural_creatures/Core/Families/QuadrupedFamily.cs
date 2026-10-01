// Procedural Pixel Creatures - family 1: quadruped mammals and heavy beasts.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class QuadrupedFamily : CreatureFamilyBase
    {
        public override string Id => "quadruped";
        public override string DisplayName => "Quadrupeds & Beasts";
        public override string Description => "Mammalian quadrupeds from rodents to heavy beasts: chest, belly and hips, a neck, expressive head, four legs with plantigrade, digitigrade or hoofed feet, and a tail.";
        public override int Order => 10;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("canine", "Canine", 1.2),
            new ArchetypeInfo("feline", "Feline", 1.1),
            new ArchetypeInfo("bovine", "Heavy grazer", 0.9),
            new ArchetypeInfo("cervine", "Cervine", 0.9),
            new ArchetypeInfo("ursine", "Ursine", 0.9),
            new ArchetypeInfo("rodent", "Rodent", 0.8),
            new ArchetypeInfo("boar", "Boar", 0.8),
            new ArchetypeInfo("beast", "Fantasy beast", 1.2),
            new ArchetypeInfo("equine", "Equine", 0.9),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("canine", "Canine"), ("feline", "Feline"), ("bovine", "Heavy grazer"), ("cervine", "Cervine"),
            ("ursine", "Ursine"), ("rodent", "Rodent"), ("boar", "Boar"), ("beast", "Fantasy beast"),
            ("equine", "Equine"),
        };

        public static readonly (string, string)[] EarKinds = { ("none", "None"), ("pointed", "Pointed"), ("round", "Round"), ("floppy", "Floppy"), ("long", "Long"), ("tufted", "Tufted"), ("fin", "Fin") };
        public static readonly (string, string)[] HornKinds = { ("none", "None"), ("short", "Short"), ("curved", "Curved"), ("ram", "Ram"), ("antlers", "Antlers"), ("unicorn", "Unicorn"), ("backswept", "Swept back"), ("nasal", "Nasal") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs"), ("sabre", "Sabre"), ("tusks", "Tusks") };
        public static readonly (string, string)[] EyeStyles = { ("bead", "Beady"), ("round", "Round"), ("slit", "Slit pupils"), ("glow", "Glowing"), ("button", "Large & cute") };
        public static readonly (string, string)[] Stances = { ("plantigrade", "Flat-footed"), ("digitigrade", "Toe walking"), ("unguligrade", "Hoofed") };
        public static readonly (string, string)[] TailTips = { ("plain", "Plain"), ("tuft", "Tuft"), ("fluffy", "Fluffy"), ("club", "Club"), ("spade", "Spade") };
        public static readonly (string, string)[] BackKinds = { ("none", "None"), ("mane", "Mane"), ("spikes", "Spikes"), ("ridge", "Ridge"), ("plates", "Plates"), ("quills", "Quills") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "canine", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.5, "Overall size in logical pixels.", 0.08)
             .Float("mass", "Mass", 0, 1, 0.5, "Slender to heavy.", 0.1)
             .Float("torso.length", "Torso length", 0, 1, 0.5, "Compact to elongated.", 0.1)
             .Float("torso.chest", "Torso chest", 0, 1, 0.5, "Distribution of mass between hips and chest.", 0.1)
             .Float("torso.belly", "Torso belly", 0, 1, 0.35, "Round, hanging belly.", 0.1)
             .Float("torso.hump", "Torso hump", 0, 1, 0.1, "Hump above the shoulders.", 0.1)
             .Float("torso.width", "Torso width", 0, 1, 0.5, "Width of the torso.", 0.1);
            b.Group("neck", "Neck", GeneStream.Anatomy)
             .Float("neck.length", "Neck length", 0, 1, 0.4, "", 0.1)
             .Float("neck.thickness", "Neck thickness", 0, 1, 0.6, "", 0.1)
             .Float("neck.carriage", "Neck carriage", 0, 1, 0.45, "Horizontal to upright carriage.", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("head.round", "Head round", 0, 1, 0.5, "Flat, elongated skull to round head.", 0.1)
             .Float("head.pitch", "Head pitch", 0, 1, 0.35, "Horizontal to downward tilted, as in grazers.", 0.1)
             .Float("head.snout", "Head snout", 0, 1, 0.5, "", 0.1)
             .Float("head.snoutWidth", "Head snout width", 0, 1, 0.5, "", 0.1)
             .Float("head.jaw", "Head jaw", 0, 1, 0.5, "", 0.1)
             .Float("head.brow", "Head brow", 0, 1, 0.3, "Heavy brow ridge for a fierce expression.", 0.1)
             .Float("head.cheeks", "Head cheeks", 0, 1, 0.2, "Cheeks and cheek fur.", 0.1)
             .Choice("head.teeth", "Head teeth", TeethKinds, "small", "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.5, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "bead", "", 0.08)
             .Float("head.eyeForward", "Head eye forward", 0, 1, 0.45, "Side-facing prey eyes to forward-facing predator eyes.", 0.1);
            b.Group("ears", "Ears & horns", GeneStream.Anatomy)
             .Choice("head.ears", "Head ears", EarKinds, "pointed", "", 0.1)
             .Float("head.earSize", "Head ear size", 0, 1, 0.5, "", 0.1)
             .Choice("head.horns", "Head horns", HornKinds, "none", "", 0.1)
             .Float("head.hornSize", "Head horn size", 0, 1, 0.5, "", 0.1);
            b.Group("legs", "Legs", GeneStream.Anatomy)
             .Float("legs.length", "Legs length", 0, 1, 0.5, "", 0.1)
             .Float("legs.thickness", "Legs thickness", 0, 1, 0.5, "", 0.1)
             .Choice("legs.stance", "Legs stance", Stances, "digitigrade", "", 0.08)
             .Float("legs.front", "Legs front", 0, 1, 0.5, "Shorter or longer front legs, changing the slope of the back.", 0.1)
             .Float("legs.paws", "Legs paws", 0, 1, 0.5, "", 0.1)
             .Float("legs.claws", "Legs claws", 0, 1, 0.4, "", 0.1);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Float("tail.length", "Tail length", 0, 1, 0.5, "", 0.1)
             .Float("tail.thickness", "Tail thickness", 0, 1, 0.4, "", 0.1)
             .Float("tail.carriage", "Tail carriage", 0, 1, 0.45, "Hanging to raised.", 0.1)
             .Float("tail.curl", "Tail curl", 0, 1, 0.3, "", 0.1)
             .Choice("tail.tip", "Tail tip", TailTips, "plain", "", 0.1);
            b.Group("back", "Back", GeneStream.Anatomy)
             .Choice("back.feature", "Back feature", BackKinds, "none", "", 0.1)
             .Float("back.size", "Back size", 0, 1, 0.5, "", 0.1);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "fur");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        // ------------------------------------------------------------------ sampling

        private sealed class Prior
        {
            public double Size, Mass, TorsoLen, Chest, Belly, Hump, Width;
            public double HeadSize, Round, Pitch, Snout, SnoutW, Jaw, Brow, Cheeks, EyeSize, EyeFwd;
            public double NeckLen, NeckTh, Carriage;
            public double LegLen, LegTh, Front, Paws, Claws;
            public double TailLen, TailTh, TailCarr, Curl;
            public double EarSize, HornSize, BackSize;
            public (string, double)[] Ears = { ("pointed", 1) };
            public (string, double)[] Horns = { ("none", 1) };
            public (string, double)[] Teeth = { ("small", 1) };
            public (string, double)[] Eyes = { ("bead", 1) };
            public (string, double)[] Stance = { ("digitigrade", 1) };
            public (string, double)[] Tips = { ("plain", 1) };
            public (string, double)[] Back = { ("none", 1) };
        }

        private static Prior PriorFor(string arch)
        {
            switch (arch)
            {
                case "feline":
                    return new Prior
                    {
                        Size = 0.45, Mass = 0.4, TorsoLen = 0.65, Chest = 0.5, Belly = 0.25, Hump = 0.05, Width = 0.45,
                        HeadSize = 0.42, Round = 0.85, Pitch = 0.15, Snout = 0.25, SnoutW = 0.5, Jaw = 0.4, Brow = 0.25, Cheeks = 0.55, EyeSize = 0.6, EyeFwd = 0.8,
                        NeckLen = 0.3, NeckTh = 0.6, Carriage = 0.35, LegLen = 0.45, LegTh = 0.45, Front = 0.45, Paws = 0.6, Claws = 0.8,
                        TailLen = 0.8, TailTh = 0.35, TailCarr = 0.4, Curl = 0.45, EarSize = 0.45, HornSize = 0.3, BackSize = 0.4,
                        Ears = new[] { ("pointed", 3.0), ("tufted", 1.0), ("round", 0.5) },
                        Teeth = new[] { ("fangs", 3.0), ("sabre", 0.6), ("small", 1.0) },
                        Eyes = new[] { ("slit", 3.0), ("round", 1.0), ("glow", 0.3) },
                        Tips = new[] { ("plain", 3.0), ("tuft", 0.6), ("fluffy", 0.5) },
                        Back = new[] { ("none", 5.0), ("mane", 0.8), ("spikes", 0.3) },
                    };
                case "bovine":
                    return new Prior
                    {
                        Size = 0.75, Mass = 0.85, TorsoLen = 0.5, Chest = 0.75, Belly = 0.6, Hump = 0.45, Width = 0.75,
                        HeadSize = 0.55, Round = 0.45, Pitch = 0.6, Snout = 0.55, SnoutW = 0.85, Jaw = 0.6, Brow = 0.5, Cheeks = 0.1, EyeSize = 0.35, EyeFwd = 0.2,
                        NeckLen = 0.25, NeckTh = 0.9, Carriage = 0.25, LegLen = 0.38, LegTh = 0.75, Front = 0.55, Paws = 0.55, Claws = 0.0,
                        TailLen = 0.35, TailTh = 0.3, TailCarr = 0.2, Curl = 0.15, EarSize = 0.45, HornSize = 0.55, BackSize = 0.5,
                        Ears = new[] { ("round", 2.0), ("pointed", 1.0), ("floppy", 0.6) },
                        Horns = new[] { ("curved", 3.0), ("short", 1.5), ("backswept", 1.0), ("none", 1.0), ("ram", 0.5) },
                        Teeth = new[] { ("none", 2.0), ("small", 1.0) },
                        Eyes = new[] { ("bead", 3.0), ("round", 1.0) },
                        Stance = new[] { ("unguligrade", 4.0), ("plantigrade", 0.5) },
                        Tips = new[] { ("tuft", 4.0), ("plain", 1.0) },
                        Back = new[] { ("none", 3.0), ("mane", 1.2), ("ridge", 0.5) },
                    };
                case "equine":
                    // horses, zebras and unicorns: long legs, high neck, long flat head, mane and flowing tail
                    return new Prior
                    {
                        Size = 0.66, Mass = 0.45, TorsoLen = 0.55, Chest = 0.58, Belly = 0.3, Hump = 0.04, Width = 0.45,
                        HeadSize = 0.52, Round = 0.12, Pitch = 0.55, Snout = 0.88, SnoutW = 0.45, Jaw = 0.45, Brow = 0.08, Cheeks = 0.25, EyeSize = 0.5, EyeFwd = 0.12,
                        NeckLen = 0.82, NeckTh = 0.75, Carriage = 0.82, LegLen = 0.92, LegTh = 0.42, Front = 0.5, Paws = 0.45, Claws = 0.0,
                        TailLen = 0.6, TailTh = 0.38, TailCarr = 0.04, Curl = 0.0, EarSize = 0.42, HornSize = 0.62, BackSize = 0.62,
                        Ears = new[] { ("pointed", 4.0) },
                        Horns = new[] { ("none", 6.0), ("unicorn", 1.0) },
                        Teeth = new[] { ("none", 2.0), ("small", 1.0) },
                        Eyes = new[] { ("bead", 2.0), ("round", 1.0) },
                        Stance = new[] { ("unguligrade", 5.0) },
                        Tips = new[] { ("fluffy", 3.0), ("tuft", 1.0) },
                        Back = new[] { ("mane", 8.0), ("none", 0.5) },
                    };
                case "cervine":
                    return new Prior
                    {
                        Size = 0.55, Mass = 0.3, TorsoLen = 0.45, Chest = 0.5, Belly = 0.25, Hump = 0.05, Width = 0.4,
                        HeadSize = 0.4, Round = 0.35, Pitch = 0.5, Snout = 0.7, SnoutW = 0.35, Jaw = 0.35, Brow = 0.15, Cheeks = 0.0, EyeSize = 0.65, EyeFwd = 0.15,
                        NeckLen = 0.8, NeckTh = 0.4, Carriage = 0.75, LegLen = 0.88, LegTh = 0.28, Front = 0.55, Paws = 0.35, Claws = 0.0,
                        TailLen = 0.15, TailTh = 0.35, TailCarr = 0.65, Curl = 0.1, EarSize = 0.6, HornSize = 0.6, BackSize = 0.3,
                        Ears = new[] { ("long", 1.0), ("pointed", 2.0) },
                        Horns = new[] { ("antlers", 3.0), ("backswept", 1.0), ("none", 1.5), ("unicorn", 0.4) },
                        Teeth = new[] { ("none", 3.0), ("small", 0.5) },
                        Eyes = new[] { ("bead", 2.0), ("button", 1.5), ("round", 1.0) },
                        Stance = new[] { ("unguligrade", 5.0) },
                        Tips = new[] { ("plain", 3.0), ("tuft", 0.5) },
                        Back = new[] { ("none", 6.0), ("mane", 0.5) },
                    };
                case "ursine":
                    return new Prior
                    {
                        Size = 0.72, Mass = 0.85, TorsoLen = 0.45, Chest = 0.65, Belly = 0.55, Hump = 0.35, Width = 0.75,
                        HeadSize = 0.55, Round = 0.8, Pitch = 0.3, Snout = 0.45, SnoutW = 0.55, Jaw = 0.55, Brow = 0.4, Cheeks = 0.35, EyeSize = 0.3, EyeFwd = 0.55,
                        NeckLen = 0.3, NeckTh = 0.85, Carriage = 0.3, LegLen = 0.36, LegTh = 0.8, Front = 0.5, Paws = 0.7, Claws = 0.6,
                        TailLen = 0.08, TailTh = 0.5, TailCarr = 0.4, Curl = 0.1, EarSize = 0.35, HornSize = 0.3, BackSize = 0.4,
                        Ears = new[] { ("round", 5.0), ("pointed", 0.5) },
                        Teeth = new[] { ("small", 2.0), ("fangs", 1.5) },
                        Eyes = new[] { ("bead", 4.0), ("round", 0.5) },
                        Stance = new[] { ("plantigrade", 5.0) },
                        Back = new[] { ("none", 5.0), ("mane", 0.6), ("spikes", 0.4) },
                    };
                case "rodent":
                    return new Prior
                    {
                        Size = 0.18, Mass = 0.6, TorsoLen = 0.45, Chest = 0.4, Belly = 0.5, Hump = 0.2, Width = 0.6,
                        HeadSize = 0.75, Round = 0.85, Pitch = 0.25, Snout = 0.35, SnoutW = 0.35, Jaw = 0.3, Brow = 0.05, Cheeks = 0.6, EyeSize = 0.75, EyeFwd = 0.3,
                        NeckLen = 0.15, NeckTh = 0.8, Carriage = 0.4, LegLen = 0.28, LegTh = 0.4, Front = 0.4, Paws = 0.5, Claws = 0.3,
                        TailLen = 0.85, TailTh = 0.15, TailCarr = 0.3, Curl = 0.35, EarSize = 0.7, HornSize = 0.3, BackSize = 0.3,
                        Ears = new[] { ("round", 3.0), ("long", 1.2), ("pointed", 0.8) },
                        Teeth = new[] { ("small", 3.0) },
                        Eyes = new[] { ("button", 2.5), ("bead", 2.0) },
                        Stance = new[] { ("plantigrade", 3.0), ("digitigrade", 1.0) },
                        Tips = new[] { ("plain", 3.0), ("tuft", 1.0), ("fluffy", 1.0) },
                        Back = new[] { ("none", 5.0), ("quills", 0.8) },
                    };
                case "boar":
                    return new Prior
                    {
                        Size = 0.55, Mass = 0.72, TorsoLen = 0.45, Chest = 0.78, Belly = 0.45, Hump = 0.4, Width = 0.62,
                        HeadSize = 0.6, Round = 0.4, Pitch = 0.45, Snout = 0.75, SnoutW = 0.6, Jaw = 0.6, Brow = 0.5, Cheeks = 0.1, EyeSize = 0.25, EyeFwd = 0.35,
                        NeckLen = 0.15, NeckTh = 0.95, Carriage = 0.2, LegLen = 0.34, LegTh = 0.55, Front = 0.5, Paws = 0.4, Claws = 0.0,
                        TailLen = 0.2, TailTh = 0.2, TailCarr = 0.55, Curl = 0.65, EarSize = 0.4, HornSize = 0.3, BackSize = 0.5,
                        Ears = new[] { ("pointed", 3.0), ("floppy", 1.0) },
                        Horns = new[] { ("none", 5.0), ("short", 0.6), ("nasal", 0.5) },
                        Teeth = new[] { ("tusks", 5.0) },
                        Eyes = new[] { ("bead", 4.0), ("glow", 0.4) },
                        Stance = new[] { ("unguligrade", 5.0) },
                        Tips = new[] { ("tuft", 3.0), ("plain", 1.0) },
                        Back = new[] { ("quills", 2.0), ("mane", 2.0), ("ridge", 1.0), ("none", 1.0) },
                    };
                case "beast":
                    return new Prior
                    {
                        Size = 0.7, Mass = 0.68, TorsoLen = 0.55, Chest = 0.75, Belly = 0.35, Hump = 0.45, Width = 0.68,
                        HeadSize = 0.6, Round = 0.5, Pitch = 0.3, Snout = 0.55, SnoutW = 0.6, Jaw = 0.8, Brow = 0.75, Cheeks = 0.2, EyeSize = 0.45, EyeFwd = 0.6,
                        NeckLen = 0.4, NeckTh = 0.75, Carriage = 0.4, LegLen = 0.52, LegTh = 0.7, Front = 0.58, Paws = 0.65, Claws = 0.85,
                        TailLen = 0.6, TailTh = 0.5, TailCarr = 0.4, Curl = 0.3, EarSize = 0.5, HornSize = 0.62, BackSize = 0.6,
                        Ears = new[] { ("pointed", 2.0), ("fin", 1.0), ("none", 0.8), ("tufted", 0.5) },
                        Horns = new[] { ("curved", 2.0), ("ram", 1.0), ("backswept", 1.5), ("short", 0.8), ("none", 1.0), ("unicorn", 0.3) },
                        Teeth = new[] { ("fangs", 3.0), ("sabre", 1.0), ("tusks", 1.0) },
                        Eyes = new[] { ("slit", 2.0), ("glow", 1.5), ("round", 1.0), ("bead", 0.8) },
                        Stance = new[] { ("digitigrade", 3.0), ("plantigrade", 1.5), ("unguligrade", 0.5) },
                        Tips = new[] { ("plain", 2.0), ("club", 1.0), ("spade", 1.0), ("tuft", 0.6) },
                        Back = new[] { ("spikes", 2.0), ("plates", 1.0), ("mane", 1.2), ("ridge", 0.8), ("none", 1.0) },
                    };
                default: // canine
                    return new Prior
                    {
                        Size = 0.5, Mass = 0.45, TorsoLen = 0.55, Chest = 0.62, Belly = 0.25, Hump = 0.1, Width = 0.5,
                        HeadSize = 0.48, Round = 0.45, Pitch = 0.3, Snout = 0.7, SnoutW = 0.45, Jaw = 0.5, Brow = 0.3, Cheeks = 0.1, EyeSize = 0.45, EyeFwd = 0.55,
                        NeckLen = 0.45, NeckTh = 0.55, Carriage = 0.45, LegLen = 0.58, LegTh = 0.45, Front = 0.5, Paws = 0.5, Claws = 0.5,
                        TailLen = 0.55, TailTh = 0.45, TailCarr = 0.45, Curl = 0.3, EarSize = 0.6, HornSize = 0.3, BackSize = 0.4,
                        Ears = new[] { ("pointed", 4.0), ("floppy", 1.2), ("tufted", 0.4) },
                        Teeth = new[] { ("small", 2.0), ("fangs", 2.0) },
                        Eyes = new[] { ("bead", 2.5), ("round", 1.5), ("glow", 0.2) },
                        Tips = new[] { ("plain", 2.0), ("fluffy", 1.5), ("tuft", 0.3) },
                        Back = new[] { ("none", 5.0), ("mane", 0.8), ("spikes", 0.3) },
                    };
            }
        }

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.12;
            Around(v, rng, "size", p.Size, 0.14);
            Around(v, rng, "mass", p.Mass, sp);
            Around(v, rng, "torso.length", p.TorsoLen, sp);
            Around(v, rng, "torso.chest", p.Chest, sp);
            Around(v, rng, "torso.belly", p.Belly, sp);
            Around(v, rng, "torso.hump", p.Hump, sp);
            Around(v, rng, "torso.width", p.Width, sp);
            Around(v, rng, "neck.length", p.NeckLen, sp);
            Around(v, rng, "neck.thickness", p.NeckTh, sp);
            Around(v, rng, "neck.carriage", p.Carriage, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            Around(v, rng, "head.round", p.Round, sp);
            Around(v, rng, "head.pitch", p.Pitch, sp);
            Around(v, rng, "head.snout", p.Snout, sp);
            Around(v, rng, "head.snoutWidth", p.SnoutW, sp);
            Around(v, rng, "head.jaw", p.Jaw, sp);
            Around(v, rng, "head.brow", p.Brow, sp);
            Around(v, rng, "head.cheeks", p.Cheeks, sp);
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            Around(v, rng, "head.eyeForward", p.EyeFwd, sp);
            SetChoice(v, "head.teeth", PickWeighted(rng, p.Teeth));
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "head.ears", PickWeighted(rng, p.Ears));
            Around(v, rng, "head.earSize", p.EarSize, sp);
            SetChoice(v, "head.horns", PickWeighted(rng, p.Horns));
            Around(v, rng, "head.hornSize", p.HornSize, sp);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            SetChoice(v, "legs.stance", PickWeighted(rng, p.Stance));
            Around(v, rng, "legs.front", p.Front, sp * 0.7);
            Around(v, rng, "legs.paws", p.Paws, sp);
            Around(v, rng, "legs.claws", p.Claws, sp);
            Around(v, rng, "tail.length", p.TailLen, sp);
            Around(v, rng, "tail.thickness", p.TailTh, sp);
            Around(v, rng, "tail.carriage", p.TailCarr, sp);
            Around(v, rng, "tail.curl", p.Curl, sp);
            SetChoice(v, "tail.tip", PickWeighted(rng, p.Tips));
            SetChoice(v, "back.feature", PickWeighted(rng, p.Back));
            Around(v, rng, "back.size", p.BackSize, sp);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["fur"] = 5, ["hide"] = 1 },
                Patterns = new Dictionary<string, double> { ["none"] = 3, ["saddle"] = 1, ["dorsal"] = 1, ["spots"] = 1, ["stripes"] = 1 },
                // natural animals: ivory, horn and dark keratin only (blue hooves looked wrong)
                Accents = new Dictionary<string, double> { ["bone"] = 3, ["dark"] = 2.5 },
                Coats = new[] { Coats.Brown(2), Coats.Tan(1.5), Coats.Grey(1), Coats.Black(0.8), Coats.Cream(0.6), Coats.Red(0.6), Coats.Golden(0.6), Coats.Chocolate(0.6) },
                CountershadeMin = 0.25, CountershadeMax = 0.75,
                Schemes = new Dictionary<string, double> { ["natural"] = 5, ["analogous"] = 2, ["monochrome"] = 1.5, ["complementary"] = 0.4, ["split"] = 0.3 },
                SocksChance = 0.2, MaskChance = 0.08, PiebaldChance = 0.08,
            };
            switch (arch)
            {
                case "canine":
                    // wolf grey, fox red, jackal tan, arctic white, black, golden retriever
                    h.Coats = new[] { Coats.Grey(2.2), Coats.Red(1.6), Coats.Brown(1.5), Coats.Black(1.2), Coats.Tan(1.2), Coats.Cream(1.0), Coats.Golden(1.0), Coats.Slate(0.5), Coats.Vivid(0.12) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["saddle"] = 1.5, ["dorsal"] = 0.6, ["spots"] = 0.4, ["stripes"] = 0.3 };
                    h.SocksChance = 0.3; h.MaskChance = 0.2; h.PiebaldChance = 0.15;
                    break;
                case "feline":
                    // lion/leopard gold, tiger ginger, puma tan, panther black, snow leopard cream, grey cats
                    h.Coats = new[] { Coats.Golden(2.2), Coats.Ginger(1.6), Coats.Tan(1.2), Coats.Grey(1.0), Coats.Black(1.0), Coats.Cream(0.8), Coats.Chocolate(0.4), Coats.Vivid(0.25) };
                    h.Patterns = new Dictionary<string, double> { ["spots"] = 2, ["rosettes"] = 2, ["stripes"] = 2, ["none"] = 1.5 };
                    h.SocksChance = 0.15; h.MaskChance = 0.05; h.PiebaldChance = 0.08;
                    break;
                case "bovine":
                    h.Coats = new[] { Coats.Brown(2), Coats.Black(1.6), Coats.Red(1.2), Coats.Chocolate(1), Coats.Cream(1), Coats.Grey(0.8), Coats.Tan(0.8), Coats.Vivid(0.15) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["blotches"] = 1, ["dorsal"] = 0.5 };
                    h.Materials = new Dictionary<string, double> { ["fur"] = 3, ["hide"] = 2 };
                    h.PiebaldChance = 0.35; h.SocksChance = 0.2; h.MaskChance = 0.04;
                    h.EnergyMax = 0.55; h.WeightMin = 0.6; h.WeightMax = 1.0;
                    break;
                case "equine":
                    // bay, chestnut, black, dapple grey, palomino, cream, white; pinto and white socks are common
                    h.Coats = new[] { Coats.Brown(2.2), Coats.Red(1.2), Coats.Chocolate(1.0), Coats.Black(1.2), Coats.Grey(1.0), Coats.Golden(0.8), Coats.Cream(0.8), Coats.Silver(0.4), Coats.Vivid(0.1) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 4, ["stripes"] = 0.7, ["spots"] = 0.5, ["dorsal"] = 0.3 };
                    h.SocksChance = 0.4; h.MaskChance = 0.0; h.PiebaldChance = 0.22;
                    h.EnergyMin = 0.45;
                    break;
                case "cervine":
                    h.Coats = new[] { Coats.Brown(2), Coats.Tan(2), Coats.Red(1.2), Coats.Golden(0.8), Coats.Chocolate(0.6), Coats.Grey(0.5), Coats.Cream(0.3) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["spots"] = 1.5 };
                    h.SocksChance = 0.15; h.MaskChance = 0.03; h.PiebaldChance = 0.05;
                    h.WeightMax = 0.4;
                    break;
                case "ursine":
                    // brown, black, polar white, panda (piebald with dark stockings and eye mask)
                    h.Coats = new[] { Coats.Brown(2.5), Coats.Black(1.6), Coats.Chocolate(1.2), Coats.Cream(0.9), Coats.Golden(0.6), Coats.Grey(0.4), Coats.Vivid(0.15) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 5, ["saddle"] = 0.5 };
                    h.SocksChance = 0.12; h.MaskChance = 0.12; h.PiebaldChance = 0.12;
                    h.WeightMin = 0.6; h.WeightMax = 1.0; h.EnergyMax = 0.6;
                    break;
                case "rodent":
                    h.Coats = new[] { Coats.Brown(2), Coats.Grey(1.6), Coats.Tan(1.2), Coats.Ginger(0.8), Coats.Cream(0.8), Coats.Black(0.6), Coats.Chocolate(0.6), Coats.Golden(0.5), Coats.Vivid(0.2) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["dorsal"] = 1, ["stripes"] = 0.5 };
                    h.SocksChance = 0.12; h.MaskChance = 0.1; h.PiebaldChance = 0.15;
                    h.EnergyMin = 0.55; h.WeightMax = 0.4;
                    break;
                case "boar":
                    h.Coats = new[] { Coats.Brown(2), Coats.Black(1.6), Coats.Grey(1), Coats.Chocolate(1), Coats.Red(0.6), Coats.Pink(0.4), Coats.Tan(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["dorsal"] = 1, ["stripes"] = 1 };
                    h.SocksChance = 0.08; h.PiebaldChance = 0.12;
                    h.WeightMin = 0.5;
                    break;
                case "beast":
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1, ["stripes"] = 1.5, ["spots"] = 1, ["mottled"] = 1, ["dorsal"] = 1, ["blotches"] = 0.6 };
                    h.Materials = new Dictionary<string, double> { ["fur"] = 2, ["hide"] = 2, ["scales"] = 1 };
                    h.Coats = new[] { Coats.Vivid(2.5), Coats.Muted(1.2), Coats.Teal(0.8), Coats.Violet(0.8), Coats.Crimson(0.8), Coats.Blue(0.8), Coats.Green(0.6), Coats.Grey(0.5), Coats.Black(0.5), Coats.Brown(0.5) };
                    h.Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1.5, ["vivid"] = 1, ["gold"] = 0.5 };
                    h.Schemes = new Dictionary<string, double> { ["natural"] = 2, ["analogous"] = 2, ["complementary"] = 1.2, ["split"] = 0.8, ["triadic"] = 0.6, ["monochrome"] = 1 };
                    h.SocksChance = 0.15; h.MaskChance = 0.1; h.PiebaldChance = 0.05;
                    h.GlowChance = 0.2;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            // glowing eyes only make sense with a glow gene
            if (Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
        }

        // ------------------------------------------------------------------ anatomy

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);

            double size = g.Get("size");
            double baseL = DMath.Lerp(11, 40, DMath.Pow(size, 1.05));
            double L = baseL * DMath.Lerp(0.8, 1.32, g.Get("torso.length"));
            double R = baseL * DMath.Lerp(0.18, 0.33, g.Get("mass"));
            double chestBias = g.Get("torso.chest");
            double chestR = R * DMath.Lerp(0.9, 1.25, chestBias);
            double hipR = R * DMath.Lerp(1.02, 0.78, chestBias);
            double wz = DMath.Lerp(0.9, 1.28, g.Get("torso.width"));
            double belly = g.Get("torso.belly");
            double hump = g.Get("torso.hump");

            string stance = g.GetChoice("legs.stance");
            double hipH = baseL * DMath.Lerp(0.42, 1.08, g.Get("legs.length"));
            double minHip = hipR * 0.92 + 1.5;
            if (hipH < minHip)
            {
                b.Repair($"Leg length increased: hip height {hipH:0.0} px -> {minHip:0.0} px to keep the belly off the ground.");
                hipH = minHip;
            }
            double shoulderH = hipH * DMath.Lerp(0.88, 1.14, g.Get("legs.front"));
            if (shoulderH < chestR * 0.92 + 1.5) shoulderH = chestR * 0.92 + 1.5;

            // ---- skeleton: root -> pelvis -> spine -> chest
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            Vec3 P = new Vec3(-L / 2, hipH, 0);
            Vec3 C = new Vec3(L / 2, shoulderH, 0);
            Vec3 spineDir = (C - P).Normalized();
            int pelvis = b.BoneAlong("pelvis", root, P, spineDir, Vec3.UnitY, BoneRole.Pelvis, 0, L / 2);
            int spine = b.BoneAlong("spine", pelvis, (P + C) * 0.5, spineDir, Vec3.UnitY, BoneRole.Spine, 0, L / 2);
            int chest = b.BoneAlong("chest", spine, C, spineDir, Vec3.UnitY, BoneRole.Chest, 0, chestR);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { L / 2, L / 2 }, TotalLength = L };

            // ---- torso masses (one smooth group with neck, head and tail)
            double blend = DMath.Clamp(R * 0.32, 1.6, 4.5);
            int body = b.Group("body", blend, 0, GroupFlags.None);
            double bodyDomain = L + hipR + chestR;
            double uRump0 = 0, uRump1 = (hipR * 2.2) / bodyDomain;
            double uChest0 = (L - chestR * 0.2) / bodyDomain, uChest1 = 1.0;
            var rump = b.Ellipsoid(body, pelvis, new Vec3(-hipR * 0.12, hipR * 0.16, 0), new Vec3(hipR * 1.15, hipR * 1.0, hipR * wz * 0.93), MaterialSlot.Primary, PatternDomain.Body, uRump0, uRump1);
            rump.DomainLength = bodyDomain; rump.Tag = "rump";
            var chestP = b.Ellipsoid(body, chest, new Vec3(chestR * 0.1, chestR * 0.04, 0), new Vec3(chestR * 1.18, chestR * 1.04, chestR * wz * 0.9), MaterialSlot.Primary, PatternDomain.Body, uChest0, uChest1);
            chestP.DomainLength = bodyDomain; chestP.Tag = "chest";
            double midR = (hipR + chestR) * 0.5 * DMath.Lerp(0.8, 1.08, belly);
            double drop = midR * DMath.Lerp(0.02, 0.36, belly);
            var barrel = b.Ellipsoid(body, spine, new Vec3(0, -drop, 0), new Vec3(L * 0.43, midR, midR * wz * 0.94), MaterialSlot.Primary, PatternDomain.Body, 0.15, 0.85);
            barrel.DomainLength = bodyDomain; barrel.Tag = "barrel";
            if (hump > 0.12)
            {
                var hp = b.Ellipsoid(body, chest, new Vec3(-chestR * 0.3, chestR * (0.55 + hump * 0.35), 0), new Vec3(chestR * 0.85, chestR * 0.5 * hump * 1.6, chestR * 0.62 * wz),
                    MaterialSlot.Primary, PatternDomain.Body, 0.6, 0.95);
                hp.DomainLength = bodyDomain; hp.Tag = "hump";
            }

            // ---- neck
            double neckLen = baseL * DMath.Lerp(0.1, 0.6, g.Get("neck.length"));
            double carriage = DMath.Lerp(10, 64, g.Get("neck.carriage")) * DMath.Deg2Rad;
            Vec3 neckDir = new Vec3(DMath.Cos(carriage), DMath.Sin(carriage), 0);
            Vec3 N0 = C + new Vec3(chestR * 0.5, chestR * 0.45, 0);
            double headSize = g.Get("head.size");
            double round = g.Get("head.round");
            double skullL = baseL * DMath.Lerp(0.27, 0.42, headSize) * DMath.Lerp(1.08, 0.9, round);
            if (form == "rodent") skullL *= 1.15;
            double skullH = skullL * DMath.Lerp(0.66, 0.96, round);
            double skullW = skullL * DMath.Lerp(0.6, 0.9, round) * DMath.Lerp(0.9, 1.1, g.Get("head.cheeks"));
            double neckTh = g.Get("neck.thickness");
            double nr0 = Math.Max(1.2, chestR * DMath.Lerp(0.42, 0.8, neckTh));
            double nr1 = Math.Max(1.0, Math.Min(nr0, skullH * DMath.Lerp(0.38, 0.55, neckTh)));
            int neck0 = b.BoneAlong("neck.0", chest, N0, neckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            Vec3 N1 = N0 + neckDir * (neckLen * 0.5);
            int neck1 = b.BoneAlong("neck.1", neck0, N1, neckDir, Vec3.UnitY, BoneRole.Neck, 0, neckLen * 0.5);
            Vec3 Hj = N0 + neckDir * neckLen;
            double neckDomain = neckLen + 2;
            var nk0 = b.Segment(body, neck0, nr0, neck1, (nr0 + nr1) * 0.5, MaterialSlot.Primary, PatternDomain.Neck, 0, 0.5);
            nk0.DomainLength = neckDomain;

            // ---- head
            double pitch = -DMath.Lerp(4, 52, g.Get("head.pitch")) * DMath.Deg2Rad;
            Vec3 headFwd = new Vec3(DMath.Cos(pitch), DMath.Sin(pitch), 0);
            double snoutGene = g.Get("head.snout");
            double snoutW = g.Get("head.snoutWidth");
            string teeth = g.GetChoice("head.teeth");
            var hs = new HeadSpec
            {
                ParentBone = neck1,
                Position = Hj,
                Forward = headFwd,
                SkullLength = skullL,
                SkullHeight = skullH,
                SkullWidth = skullW,
                SkullOffset = 0.28,
                SnoutLength = skullL * DMath.Lerp(0.12, 1.15, snoutGene),
                SnoutRadius = skullH * DMath.Lerp(0.26, 0.44, snoutW),
                SnoutTaper = DMath.Lerp(0.58, 0.9, snoutW),
                SnoutDrop = form == "bovine" || form == "cervine" ? 0.12 : 0.05,
                SnoutWidth = DMath.Lerp(0.85, 1.35, snoutW),
                SnoutHeight = form == "feline" || form == "rodent" ? -0.2 : -0.12,
                NoseSize = form == "boar" ? 1.35 : (form == "bovine" ? 1.2 : 1.0),
                JawLength = DMath.Lerp(0.72, 1.0, g.Get("head.jaw")),
                JawRadius = skullH * DMath.Lerp(0.16, 0.3, g.Get("head.jaw")),
                JawDepth = DMath.Lerp(0.6, 1.1, g.Get("head.jaw")),
                Brow = g.Get("head.brow"),
                Cheeks = g.Get("head.cheeks"),
                EyeSize = DMath.Clamp(DMath.Lerp(1.4, 4.2, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 9.0), 1.3, 5.5),
                EyeStyle = ParseEye(g.GetChoice("head.eyeStyle")),
                EyeForward = g.Get("head.eyeForward"),
                EyeHeight = DMath.Lerp(0.05, 0.4, round),
                EyeAspect = g.GetChoice("head.eyeStyle") == "slit" ? 1.25 : 1.0,
                Teeth = teeth switch { "none" => TeethKind.None, "fangs" => TeethKind.Fangs, "sabre" => TeethKind.Sabre, "tusks" => TeethKind.Tusks, _ => TeethKind.Small },
                TeethSize = DMath.Clamp(skullL / 8.0, 0.8, 1.6),
                Group = body,
                DomainLength = skullL + skullL * 0.8,
                JawOpenMax = form == "feline" || form == "canine" || form == "beast" ? 0.75 : 0.5,
            };
            var head = Heads.Build(b, hs);
            // neck upper segment ends inside the skull so the join is seamless
            var nk1 = b.Cone(body, neck1, Vec3.Zero, (nr0 + nr1) * 0.5, head.Head, head.SkullCenterLocal * 0.5, nr1, MaterialSlot.Primary, PatternDomain.Neck, 0.5, 1.0);
            nk1.DomainLength = neckDomain;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck0, neck1, head.Head }, Lengths = new[] { neckLen * 0.5, neckLen * 0.5 }, TotalLength = neckLen };

            // ears and horns
            string ears = g.GetChoice("head.ears");
            Ears.Build(b, new EarSpec
            {
                Kind = ParseEar(ears),
                HeadBone = head.Head,
                Base = head.SkullCenterLocal + new Vec3(-skullL * 0.12, skullH * 0.38, skullW * 0.3),
                Size = skullL * DMath.Lerp(0.3, 0.85, g.Get("head.earSize")),
                Width = form == "ursine" || form == "rodent" ? 1.2 : 1.0,
            });
            string horns = g.GetChoice("head.horns");
            if (horns != "none")
            {
                Horns.Build(b, new HornSpec
                {
                    Kind = ParseHorn(horns),
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(skullL * 0.12, skullH * 0.42, skullW * 0.22),
                    Front = horns == "nasal" ? head.SnoutTipLocal + new Vec3(-hs.SnoutLength * 0.35, hs.SnoutRadius * 0.7, 0)
                                             : head.SkullCenterLocal + new Vec3(skullL * 0.35, skullH * 0.4, 0),
                    Size = skullL * DMath.Lerp(0.45, 1.5, g.Get("head.hornSize")),
                    Thickness = DMath.Lerp(0.8, 1.25, g.Get("mass")),
                });
                b.Trait("horns:" + horns);
            }

            // ---- tail
            double tailLen = baseL * DMath.Lerp(0.12, 1.5, g.Get("tail.length"));
            string tip = g.GetChoice("tail.tip");
            if (tailLen > 2.5)
            {
                double tc = DMath.Lerp(-62, 58, g.Get("tail.carriage")) * DMath.Deg2Rad;
                Vec3 tailDir = new Vec3(-DMath.Cos(tc), DMath.Sin(tc), 0);
                double tr0 = Math.Max(0.9, hipR * DMath.Lerp(0.14, 0.42, g.Get("tail.thickness")));
                bool fluffy = tip == "fluffy";
                var tailSpec = new ChainSpec
                {
                    Name = "tail",
                    ParentBone = pelvis,
                    Base = P + new Vec3(-hipR * 0.95, hipR * 0.42, 0),
                    Direction = tailDir,
                    Length = tailLen,
                    Segments = DMath.Clamp((int)Math.Round(tailLen / 3.2), 3, 10),
                    RadiusBase = tr0,
                    RadiusTip = fluffy ? tr0 * 0.55 : Math.Max(0.45, tr0 * 0.32),
                    Bulge = fluffy ? 0.6 : 0,
                    Curl = DMath.Lerp(-0.3, 2.6, g.Get("tail.curl")) * (tc < -0.2 ? 0.8 : 1.0),
                    Tip = tip switch { "tuft" => TailTip.Tuft, "club" => TailTip.Club, "spade" => TailTip.Spade, _ => TailTip.Plain },
                    TipSize = DMath.Clamp(tr0 * 0.9, 0.8, 2.4),
                    Group = body,
                    Stiffness = DMath.Lerp(0.3, 0.75, g.Get("tail.thickness")),
                };
                b.Rig.Tail = Chains.Build(b, tailSpec);
            }

            // ---- legs
            var style = stance switch { "plantigrade" => LegStyle.Plantigrade, "unguligrade" => LegStyle.Unguligrade, _ => LegStyle.Digitigrade };
            double legTh = g.Get("legs.thickness");
            double paws = g.Get("legs.paws");
            double claws = g.Get("legs.claws");
            double[] phase = { 0.0, 0.5, 0.25, 0.75 }; // LH, RH, LF, RF (lateral sequence walk)
            int legIndex = 0;
            for (int pair = 0; pair < 2; pair++)
            {
                bool front = pair == 0;
                for (int side = -1; side <= 1; side += 2)
                {
                    double bodyR = front ? chestR : hipR;
                    Vec3 hipJ = front
                        ? C + new Vec3(chestR * 0.12, -chestR * 0.22, side * chestR * wz * 0.5)
                        : P + new Vec3(hipR * 0.05, -hipR * 0.12, side * hipR * wz * 0.55);
                    Vec3 contact = new Vec3(hipJ.X + (front ? L * 0.03 : -L * 0.02), 0, side * bodyR * wz * (front ? 0.6 : 0.66));
                    double factor, fu, fl, fd, meta;
                    switch (style)
                    {
                        case LegStyle.Plantigrade:
                            factor = front ? 1.06 : 1.1; fu = 0.5; fl = 0.5; fd = 0.34; meta = 0; break;
                        case LegStyle.Unguligrade:
                            factor = front ? 1.2 : 1.32; fu = front ? 0.38 : 0.33; fl = front ? 0.36 : 0.35; fd = front ? 0.26 : 0.32; meta = front ? 6 : 14; break;
                        default:
                            factor = front ? 1.16 : 1.34; fu = front ? 0.42 : 0.36; fl = front ? 0.4 : 0.38; fd = front ? 0.18 : 0.26; meta = front ? 12 : 24; break;
                    }
                    double hipHeight = hipJ.Y;
                    double total = hipHeight * factor;
                    double rTop = Math.Max(1.2, bodyR * DMath.Lerp(0.3, 0.55, legTh));
                    double pawR = Math.Max(0.9, rTop * DMath.Lerp(0.42, 0.72, paws));
                    var spec = new LegSpec
                    {
                        Name = (front ? "front" : "hind") + (side < 0 ? "L" : "R"),
                        Side = side,
                        Pair = pair,
                        Front = front,
                        AnchorBone = front ? chest : pelvis,
                        Hip = hipJ,
                        Contact = contact,
                        Style = style,
                        Length = style == LegStyle.Plantigrade ? total : total,
                        Upper = style == LegStyle.Plantigrade ? fu * (1 - 0.0) : fu,
                        Lower = fl,
                        Distal = style == LegStyle.Plantigrade ? fd * 0.5 : fd,
                        RadiusTop = rTop,
                        RadiusKnee = Math.Max(0.85, rTop * 0.62),
                        RadiusAnkle = Math.Max(0.7, rTop * 0.44),
                        RadiusFoot = Math.Max(0.7, rTop * 0.5),
                        Haunch = front ? 0.85 : 1.1,
                        PawLength = pawR * DMath.Lerp(1.6, 2.6, paws),
                        PawRadius = pawR,
                        FootMaterial = MaterialSlot.Primary,
                        Claws = style == LegStyle.Unguligrade || claws < 0.35 ? 0 : 3,
                        ClawSize = DMath.Lerp(0.5, 1.6, claws) * Math.Sqrt(baseL / 26.0),
                        Phase = phase[legIndex],
                        LiftHeight = Math.Max(1.5, total * 0.14),
                        MetaAngle = meta,
                        BlendRadius = DMath.Clamp(rTop * 0.45, 0.8, 2.2),
                        DomainLength = total,
                    };
                    // plantigrade: segment lengths hip->knee->ankle cover the leg; distal = foot length
                    if (style == LegStyle.Plantigrade)
                    {
                        spec.Length = hipHeight * 1.08 / (fu + fl) * (fu + fl) + pawR * 2.4;
                        double legPart = hipHeight * 1.05;
                        double footLen = pawR * 2.6;
                        spec.Length = legPart + footLen;
                        spec.Upper = legPart * 0.5 / spec.Length;
                        spec.Lower = legPart * 0.5 / spec.Length;
                        spec.Distal = footLen / spec.Length;
                        spec.PawLength = footLen;
                    }
                    b.Rig.Legs.Add(Limbs.Build(b, spec));
                    legIndex++;
                }
            }
            // gait phases in rig order: hindL, hindR from the loop above are pair 1; we built front first
            FixPhases(b.Rig.Legs);

            // ---- dorsal features
            string back = g.GetChoice("back.feature");
            if (back != "none")
            {
                double bs = g.Get("back.size");
                var ds = new DorsalSpec
                {
                    Kind = back switch { "mane" => DorsalKind.Mane, "spikes" => DorsalKind.Spikes, "ridge" => DorsalKind.Ridge, "plates" => DorsalKind.Plates, "quills" => DorsalKind.Quills, _ => DorsalKind.None },
                    Size = baseL * DMath.Lerp(0.08, 0.2, bs),
                    Material = back == "mane" ? MaterialSlot.Marking : (back == "ridge" ? MaterialSlot.Primary : MaterialSlot.Accent),
                    BodyGroup = back == "ridge" ? body : -1,
                };
                if (back == "mane")
                {
                    // along the neck and shoulders
                    for (int i = 0; i <= 4; i++)
                    {
                        double t = i / 4.0;
                        Vec3 pos = Vec3.Lerp(N0 - neckDir * (chestR * 0.3), Hj, t * 0.9);
                        double r = DMath.Lerp(nr0, nr1, t);
                        Vec3 up = new Vec3(-neckDir.Y, neckDir.X, 0);
                        ds.Anchors.Add((t < 0.5 ? neck0 : neck1, pos + up * r * 0.8, up, DMath.Lerp(1.2, 0.8, t)));
                    }
                    ds.Anchors.Add((chest, C + new Vec3(-chestR * 0.2, chestR * 0.95, 0), Vec3.UnitY, 1.15));
                }
                else
                {
                    int n = DMath.Clamp((int)Math.Round(L / DMath.Lerp(4.5, 3.0, bs)), 3, 9);
                    for (int i = 0; i < n; i++)
                    {
                        double t = (i + 0.5) / n;
                        double x = DMath.Lerp(-L / 2 - hipR * 0.5, L / 2 + chestR * 0.3, t);
                        double top = BodyTop(x, P, C, hipR, chestR, midR, drop, hump, L);
                        int bone = t < 0.33 ? pelvis : (t < 0.66 ? spine : chest);
                        double scale = 0.7 + 0.5 * DMath.Sin(DMath.Pi * DMath.Lerp(0.25, 0.85, t));
                        ds.Anchors.Add((bone, new Vec3(x, top, 0), new Vec3(0, 1, 0), scale));
                    }
                }
                Dorsal.Build(b, ds);
                b.Trait("back:" + back);
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = "legged";
            b.Rig.Action = form == "bovine" || form == "boar" || horns != "none" && form != "feline" ? ActionStyle.Pounce : (form == "cervine" || form == "rodent" ? ActionStyle.Roar : ActionStyle.Bite);
            if (form == "bovine" || form == "boar" || (horns != "none" && (form == "beast" || form == "cervine"))) b.Rig.Action = ActionStyle.Slam;
            if (form == "ursine") b.Rig.Action = ActionStyle.Roar;
            b.Rig.Rest = RestStyle.LieDown;
            b.Rig.Tuning["gait.walkDuty"] = 0.64;
            b.Rig.Tuning["gait.runDuty"] = style == LegStyle.Unguligrade ? 0.38 : 0.34;
            b.Metrics.LegLength = (hipH + shoulderH) * 0.5;
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = Math.Max(hipH + hipR, shoulderH + chestR);
            b.Metrics.BodyLength = L + hipR + chestR + neckLen + skullL + tailLen * 0.7;
            b.Metrics.Mass = DMath.Pow(baseL / 26.0, 3) * DMath.Lerp(0.6, 1.6, g.Get("mass"));
            double legL = b.Metrics.LegLength;
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * legL) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5));
            b.Metrics.RunSpeed = Math.Sqrt(2.2 * 400 * legL) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5));
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 26.0), 0.75, 1.5);
            // big horns sweep forwards when the head dips (bites, charges): reserve room for them
            double hornReach = horns != "none" ? skullL * DMath.Lerp(0.45, 1.5, g.Get("head.hornSize")) * 0.3 : 0;
            b.AnimationMargin = Math.Max(7, baseL * 0.32 + hornReach);
            b.ExtraHeadroom = Math.Max(4, skullL * 0.8);
            return b.Build(g.AnatomyKey);
        }

        private static void FixPhases(List<LegRig> legs)
        {
            // lateral-sequence walk: LH -> LF -> RH -> RF
            foreach (var l in legs)
            {
                bool front = l.Pair == 0;
                bool left = l.Side < 0;
                l.Phase = (front, left) switch
                {
                    (false, true) => 0.0,
                    (true, true) => 0.25,
                    (false, false) => 0.5,
                    _ => 0.75,
                };
            }
        }

        private static double BodyTop(double x, Vec3 P, Vec3 C, double hipR, double chestR, double midR, double drop, double hump, double L)
        {
            double best = 0;
            best = Math.Max(best, EllTop(x, P.X - hipR * 0.12, P.Y + hipR * 0.16, hipR * 1.15, hipR));
            best = Math.Max(best, EllTop(x, C.X + chestR * 0.1, C.Y + chestR * 0.04, chestR * 1.18, chestR * 1.04));
            best = Math.Max(best, EllTop(x, (P.X + C.X) / 2, (P.Y + C.Y) / 2 - drop, L * 0.43, midR));
            if (hump > 0.12) best = Math.Max(best, EllTop(x, C.X - chestR * 0.3, C.Y + chestR * (0.55 + hump * 0.35), chestR * 0.85, chestR * 0.5 * hump * 1.6));
            return best;
        }

        private static double EllTop(double x, double cx, double cy, double rx, double ry)
        {
            double t = (x - cx) / rx;
            if (Math.Abs(t) >= 1) return 0;
            return cy + ry * Math.Sqrt(1 - t * t);
        }

        public static EyeStyle ParseEye(string id) => id switch
        {
            "round" => EyeStyle.Round,
            "slit" => EyeStyle.Slit,
            "glow" => EyeStyle.Glow,
            "button" => EyeStyle.Button,
            "compound" => EyeStyle.Compound,
            _ => EyeStyle.Bead,
        };

        public static EarKind ParseEar(string id) => id switch
        {
            "pointed" => EarKind.Pointed,
            "round" => EarKind.Round,
            "floppy" => EarKind.Floppy,
            "long" => EarKind.Long,
            "tufted" => EarKind.Tufted,
            "fin" => EarKind.Fin,
            _ => EarKind.None,
        };

        public static HornKind ParseHorn(string id) => id switch
        {
            "short" => HornKind.Short,
            "curved" => HornKind.Curved,
            "ram" => HornKind.Ram,
            "antlers" => HornKind.Antlers,
            "unicorn" => HornKind.Unicorn,
            "backswept" => HornKind.Backswept,
            "nasal" => HornKind.Nasal,
            _ => HornKind.None,
        };
    }
}
