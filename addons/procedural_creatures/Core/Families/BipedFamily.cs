// Procedural Pixel Creatures - family 2: bipeds and humanoids (goblins, trolls, golems, ogres, imps,
// kobolds, yetis, orcs, dwarves). Upright torso on two legs, arms with hands, procedural weapons and
// gear: clothing, head gear, shields, pauldrons and beards in their own groups and colours.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Families
{
    public sealed class BipedFamily : CreatureFamilyBase
    {
        public override string Id => "biped";
        public override string DisplayName => "Bipeds & Humanoids";
        public override string Description => "Upright creatures on two legs: goblins, trolls, golems, ogres, imps, kobolds, yetis, orcs and dwarves. Articulated torsos, expressive faces, arms, hands and optional procedural equipment.";
        public override int Order => 20;

        private static readonly ArchetypeInfo[] ArchetypeList =
        {
            new ArchetypeInfo("goblin", "Goblin", 1.3),
            new ArchetypeInfo("troll", "Troll", 1.0),
            new ArchetypeInfo("golem", "Golem", 0.9),
            new ArchetypeInfo("ogre", "Ogre", 0.9),
            new ArchetypeInfo("imp", "Imp", 0.9),
            new ArchetypeInfo("kobold", "Kobold", 0.9),
            new ArchetypeInfo("yeti", "Yeti", 0.8),
            new ArchetypeInfo("orc", "Orc", 1.1),
            new ArchetypeInfo("dwarf", "Dwarf", 1.0),
        };

        public override IReadOnlyList<ArchetypeInfo> Archetypes => ArchetypeList;

        public static readonly (string, string)[] Forms =
        {
            ("goblin", "Goblin"), ("troll", "Troll"), ("golem", "Golem"), ("ogre", "Ogre"), ("imp", "Imp"), ("kobold", "Kobold"), ("yeti", "Yeti"),
            ("orc", "Orc"), ("dwarf", "Dwarf"),
        };
        public static readonly (string, string)[] EarKinds = { ("none", "None"), ("elf", "Elf"), ("pointed", "Pointed"), ("round", "Round"), ("floppy", "Floppy") };
        public static readonly (string, string)[] HornKinds = { ("none", "None"), ("short", "Short"), ("curved", "Curved"), ("ram", "Ram"), ("backswept", "Swept back") };
        public static readonly (string, string)[] TeethKinds = { ("none", "None"), ("small", "Small"), ("fangs", "Fangs"), ("tusks", "Tusks") };
        public static readonly (string, string)[] EyeStyles = { ("bead", "Beady"), ("round", "Round"), ("slit", "Slit pupils"), ("glow", "Glowing"), ("button", "Large & cute") };
        public static readonly (string, string)[] Stances = { ("plantigrade", "Flat-footed"), ("digitigrade", "Toe walking") };
        public static readonly (string, string)[] TailKinds = { ("none", "None"), ("plain", "Plain"), ("spade", "Spade"), ("tuft", "Tuft"), ("club", "Club") };
        public static readonly (string, string)[] BackKinds = { ("none", "None"), ("mane", "Mane"), ("spikes", "Spikes"), ("crystals", "Crystals"), ("wings", "Wings") };
        public static readonly (string, string)[] Weapons = { ("none", "None"), ("club", "Club"), ("spear", "Spear"), ("dagger", "Dagger"), ("staff", "Staff"), ("axe", "Axe"), ("hammer", "Hammer") };
        public static readonly (string, string)[] Outfits = { ("none", "None"), ("loincloth", "Loincloth"), ("belt", "Belt"), ("tunic", "Tunic"), ("armor", "Armor"), ("robe", "Robe") };
        public static readonly (string, string)[] NoseShapes = { ("pad", "Pad"), ("round", "Round"), ("long", "Long"), ("flat", "Flat") };
        public static readonly (string, string)[] Metals = { ("steel", "Steel"), ("iron", "Iron"), ("bronze", "Bronze"), ("gold", "Gold") };
        public static readonly (string, string)[] Helmets = { ("none", "None"), ("cap", "Cap"), ("helmet", "Helmet"), ("horned", "Horned"), ("hood", "Hood"), ("crown", "Crown") };

        protected override GeneSchema CreateSchema()
        {
            var b = new GeneSchemaBuilder(Id);
            b.Group("body", "Body", GeneStream.Anatomy)
             .Choice("form", "Body type", Forms, "goblin", "Archetype used as a template for proportions and features when rerolling.", 0.05)
             .Float("size", "Size", 0, 1, 0.45, "Overall size in logical pixels.", 0.08)
             .Float("mass", "Mass", 0, 1, 0.5, "Slender to heavy.", 0.1)
             .Float("posture", "Posture", 0, 1, 0.3, "Upright to deeply hunched.", 0.1)
             .Float("torso.length", "Torso length", 0, 1, 0.5, "", 0.1)
             .Float("torso.shoulders", "Torso shoulders", 0, 1, 0.5, "", 0.1)
             .Float("torso.belly", "Torso belly", 0, 1, 0.35, "", 0.1);
            b.Group("head", "Head", GeneStream.Anatomy)
             .Float("head.size", "Head size", 0, 1, 0.5, "", 0.1)
             .Float("head.round", "Head round", 0, 1, 0.55, "", 0.1)
             .Float("head.snout", "Head snout", 0, 1, 0.25, "Flat face to a lizard-like muzzle.", 0.1)
             .Float("head.nose", "Head nose", 0, 1, 0.5, "Nose size.", 0.1)
             .Choice("head.noseShape", "Head nose shape", NoseShapes, "pad", "Animal nose on the muzzle or humanoid nose on the face.", 0.08)
             .Float("head.jaw", "Head jaw", 0, 1, 0.5, "", 0.1)
             .Float("head.brow", "Head brow", 0, 1, 0.4, "", 0.1)
             .Choice("head.teeth", "Head teeth", TeethKinds, "small", "", 0.1)
             .Float("head.eyeSize", "Head eye size", 0, 1, 0.5, "", 0.1)
             .Choice("head.eyeStyle", "Head eye style", EyeStyles, "round", "", 0.08);
            b.Group("ears", "Ears & horns", GeneStream.Anatomy)
             .Choice("head.ears", "Head ears", EarKinds, "elf", "", 0.1)
             .Float("head.earSize", "Head ear size", 0, 1, 0.5, "", 0.1)
             .Choice("head.horns", "Head horns", HornKinds, "none", "", 0.1)
             .Float("head.hornSize", "Head horn size", 0, 1, 0.5, "", 0.1);
            b.Group("arms", "Arms", GeneStream.Anatomy)
             .Float("arms.length", "Arms length", 0, 1, 0.5, "Short arms to ground-reaching troll arms.", 0.1)
             .Float("arms.thickness", "Arms thickness", 0, 1, 0.5, "", 0.1)
             .Float("arms.hands", "Arms hands", 0, 1, 0.5, "", 0.1)
             .Float("arms.claws", "Arms claws", 0, 1, 0.2, "", 0.1)
             .Choice("gear.weapon", "Gear weapon", Weapons, "none", "Procedural item held in the right hand.", 0.1)
             .Float("gear.size", "Gear size", 0, 1, 0.5, "", 0.1);
            b.Group("legs", "Legs", GeneStream.Anatomy)
             .Float("legs.length", "Legs length", 0, 1, 0.45, "", 0.1)
             .Float("legs.thickness", "Legs thickness", 0, 1, 0.5, "", 0.1)
             .Choice("legs.stance", "Legs stance", Stances, "plantigrade", "", 0.08)
             .Float("legs.feet", "Legs feet", 0, 1, 0.5, "", 0.1);
            b.Group("tail", "Tail", GeneStream.Anatomy)
             .Choice("tail.kind", "Tail kind", TailKinds, "none", "", 0.1)
             .Float("tail.length", "Tail length", 0, 1, 0.5, "", 0.1)
             .Choice("back.feature", "Back feature", BackKinds, "none", "", 0.1)
             .Float("back.size", "Back size", 0, 1, 0.5, "", 0.1);
            b.Group("outfit", "Outfit", GeneStream.Anatomy)
             .Choice("gear.outfit", "Gear outfit", Outfits, "none", "Loincloth, belt, tunic, armor or robe in a separate fabric color.", 0.1)
             .Choice("gear.helmet", "Gear helmet", Helmets, "none", "", 0.1)
             .Bool("gear.shield", "Gear shield", false, "Round shield on the left arm.", 0.08)
             .Bool("gear.pauldrons", "Gear pauldrons", false, "", 0.08)
             .Float("head.beard", "Head beard", 0, 1, 0, "Beard length; zero means no beard.", 0.12);
            b.Group("gearcolor", "Equipment colors", GeneStream.Color)
             .Hue("color.clothHue", "Color cloth hue", 55, "Hue of clothing, leather and shield covering.")
             .Float("color.clothSaturation", "Color cloth saturation", 0, 1, 0.4, "", 0.12)
             .Float("color.clothValue", "Color cloth value", 0, 1, 0.3, "", 0.12)
             .Choice("color.metal", "Color metal", Metals, "steel", "Material of blades, helmets, armor, shield rims and crowns.", 0.1)
             .Hue("color.hairHue", "Color hair hue", 40, "Beard and hair hue.")
             .Float("color.hairSaturation", "Color hair saturation", 0, 1, 0.35, "", 0.12)
             .Float("color.hairValue", "Color hair value", 0, 1, 0.25, "", 0.12);
            SharedGenes.AddColorGenes(b);
            SharedGenes.AddPatternGenes(b, "hide");
            SharedGenes.AddMotionGenes(b);
            return b.Build();
        }

        // ------------------------------------------------------------------ sampling

        private sealed class Prior
        {
            public double Size, Mass, Posture, TorsoLen, Shoulders, Belly;
            public double HeadSize, Round, Snout, Nose, Jaw, Brow, EyeSize, EarSize, HornSize;
            public double ArmLen, ArmTh, Hands, Claws, GearSize, LegLen, LegTh, Feet, TailLen, BackSize;
            public double Beard, BeardChance, ShieldChance, PauldronChance;
            public (string, double)[] Ears = { ("elf", 1) }, Horns = { ("none", 1) }, Teeth = { ("small", 1) }, Eyes = { ("round", 1) };
            public (string, double)[] Stance = { ("plantigrade", 1) }, Tail = { ("none", 1) }, Back = { ("none", 1) }, Weapon = { ("none", 1) };
            public (string, double)[] Outfit = { ("none", 1) }, Helmet = { ("none", 1) }, NoseShape = { ("pad", 1) };
        }

        private static Prior PriorFor(string arch) => arch switch
        {
            // lanky, long-armed and hunched with a long nose
            "troll" => new Prior
            {
                Size = 0.72, Mass = 0.42, Posture = 0.75, TorsoLen = 0.6, Shoulders = 0.5, Belly = 0.2,
                HeadSize = 0.5, Round = 0.55, Snout = 0.1, Nose = 0.95, Jaw = 0.6, Brow = 0.8, EyeSize = 0.3, EarSize = 0.55, HornSize = 0.4,
                ArmLen = 0.95, ArmTh = 0.5, Hands = 0.8, Claws = 0.4, GearSize = 0.7, LegLen = 0.55, LegTh = 0.5, Feet = 0.75, TailLen = 0.3, BackSize = 0.5,
                BeardChance = 0.12, Beard = 0.4,
                NoseShape = new[] { ("long", 3.0), ("flat", 0.6) },
                Ears = new[] { ("pointed", 2.0), ("elf", 1.0), ("floppy", 0.6) }, Teeth = new[] { ("tusks", 3.0), ("fangs", 1.0) },
                Eyes = new[] { ("bead", 3.0), ("glow", 0.6) }, Back = new[] { ("mane", 2.0), ("none", 2.0), ("spikes", 0.5) },
                Weapon = new[] { ("club", 3.0), ("none", 2.0), ("hammer", 0.4) },
                Outfit = new[] { ("loincloth", 3.0), ("none", 2.0), ("belt", 0.8) }, Helmet = new[] { ("none", 6.0), ("hood", 0.4), ("horned", 0.3) },
            },
            // blocky stone: broad shoulders, tiny head, no clothes
            "golem" => new Prior
            {
                Size = 0.7, Mass = 0.9, Posture = 0.2, TorsoLen = 0.45, Shoulders = 0.95, Belly = 0.1,
                HeadSize = 0.32, Round = 0.15, Snout = 0.05, Nose = 0.15, Jaw = 0.85, Brow = 0.9, EyeSize = 0.35, EarSize = 0.3, HornSize = 0.4,
                ArmLen = 0.75, ArmTh = 0.95, Hands = 0.9, Claws = 0.0, GearSize = 0.5, LegLen = 0.3, LegTh = 0.9, Feet = 0.7, TailLen = 0.2, BackSize = 0.6,
                Ears = new[] { ("none", 1.0) }, Teeth = new[] { ("none", 3.0) }, Eyes = new[] { ("glow", 5.0) },
                Back = new[] { ("crystals", 2.0), ("none", 1.5), ("spikes", 0.5) }, Horns = new[] { ("none", 3.0), ("short", 1.0) },
            },
            // pot-bellied heavy with a huge jaw
            "ogre" => new Prior
            {
                Size = 0.8, Mass = 0.8, Posture = 0.35, TorsoLen = 0.45, Shoulders = 0.55, Belly = 0.95,
                HeadSize = 0.55, Round = 0.55, Snout = 0.08, Nose = 0.75, Jaw = 0.95, Brow = 0.7, EyeSize = 0.25, EarSize = 0.45, HornSize = 0.4,
                ArmLen = 0.6, ArmTh = 0.8, Hands = 0.75, Claws = 0.1, GearSize = 0.75, LegLen = 0.32, LegTh = 0.85, Feet = 0.6, TailLen = 0.3, BackSize = 0.4,
                BeardChance = 0.2, Beard = 0.45, ShieldChance = 0.06, PauldronChance = 0.12,
                NoseShape = new[] { ("round", 2.0), ("flat", 1.5) },
                Ears = new[] { ("round", 2.0), ("elf", 1.0) }, Teeth = new[] { ("tusks", 4.0), ("small", 1.0) },
                Eyes = new[] { ("bead", 4.0) }, Back = new[] { ("none", 3.0), ("mane", 1.0) }, Weapon = new[] { ("club", 3.0), ("none", 1.5), ("hammer", 0.6) },
                Outfit = new[] { ("loincloth", 2.0), ("belt", 2.0), ("tunic", 1.0) }, Helmet = new[] { ("none", 4.0), ("cap", 0.8), ("horned", 0.5) },
            },
            "imp" => new Prior
            {
                Size = 0.22, Mass = 0.3, Posture = 0.35, TorsoLen = 0.4, Shoulders = 0.4, Belly = 0.35,
                HeadSize = 0.75, Round = 0.6, Snout = 0.15, Nose = 0.35, Jaw = 0.4, Brow = 0.6, EyeSize = 0.7, EarSize = 0.65, HornSize = 0.6,
                ArmLen = 0.45, ArmTh = 0.3, Hands = 0.5, Claws = 0.7, GearSize = 0.5, LegLen = 0.5, LegTh = 0.3, Feet = 0.45, TailLen = 0.75, BackSize = 0.55,
                NoseShape = new[] { ("round", 1.0), ("pad", 1.0), ("long", 0.6) },
                Ears = new[] { ("elf", 2.0), ("pointed", 1.5), ("none", 0.5) }, Horns = new[] { ("curved", 2.0), ("short", 2.0), ("ram", 0.6), ("backswept", 1.0) },
                Teeth = new[] { ("fangs", 3.0), ("small", 1.0) }, Eyes = new[] { ("slit", 2.0), ("glow", 1.5), ("button", 1.0) },
                Stance = new[] { ("digitigrade", 4.0), ("plantigrade", 0.5) }, Tail = new[] { ("spade", 4.0), ("plain", 1.0) },
                Back = new[] { ("wings", 3.0), ("none", 1.0) }, Weapon = new[] { ("none", 2.0), ("spear", 1.0), ("staff", 0.6) },
                Outfit = new[] { ("none", 3.0), ("loincloth", 1.0) }, Helmet = new[] { ("none", 8.0), ("crown", 0.4) },
            },
            "kobold" => new Prior
            {
                Size = 0.3, Mass = 0.4, Posture = 0.45, TorsoLen = 0.5, Shoulders = 0.4, Belly = 0.3,
                HeadSize = 0.6, Round = 0.35, Snout = 0.8, Nose = 0.25, Jaw = 0.5, Brow = 0.4, EyeSize = 0.5, EarSize = 0.4, HornSize = 0.35,
                ArmLen = 0.5, ArmTh = 0.35, Hands = 0.45, Claws = 0.7, GearSize = 0.5, LegLen = 0.45, LegTh = 0.4, Feet = 0.5, TailLen = 0.7, BackSize = 0.4,
                ShieldChance = 0.2,
                Ears = new[] { ("none", 2.0), ("pointed", 1.0) }, Horns = new[] { ("none", 2.0), ("short", 1.0), ("backswept", 1.5) },
                Teeth = new[] { ("small", 2.0), ("fangs", 1.0) }, Eyes = new[] { ("slit", 3.0), ("round", 1.0) },
                Stance = new[] { ("digitigrade", 3.0), ("plantigrade", 1.0) }, Tail = new[] { ("plain", 4.0), ("club", 0.5) },
                Back = new[] { ("none", 2.0), ("spikes", 2.0) }, Weapon = new[] { ("spear", 2.0), ("dagger", 1.5), ("none", 1.5) },
                Outfit = new[] { ("none", 2.0), ("loincloth", 1.5), ("belt", 1.0) }, Helmet = new[] { ("none", 4.0), ("cap", 0.6), ("crown", 0.2) },
            },
            // bulky ape shape, shaggy
            "yeti" => new Prior
            {
                Size = 0.7, Mass = 0.8, Posture = 0.55, TorsoLen = 0.5, Shoulders = 0.85, Belly = 0.5,
                HeadSize = 0.42, Round = 0.8, Snout = 0.15, Nose = 0.3, Jaw = 0.6, Brow = 0.7, EyeSize = 0.3, EarSize = 0.3, HornSize = 0.5,
                ArmLen = 0.8, ArmTh = 0.75, Hands = 0.7, Claws = 0.5, GearSize = 0.5, LegLen = 0.4, LegTh = 0.75, Feet = 0.8, TailLen = 0.2, BackSize = 0.6,
                NoseShape = new[] { ("flat", 3.0), ("pad", 1.0) },
                Ears = new[] { ("none", 2.0), ("round", 1.0) }, Horns = new[] { ("none", 3.0), ("ram", 1.0), ("curved", 0.6) },
                Teeth = new[] { ("fangs", 2.0), ("small", 1.0) }, Eyes = new[] { ("bead", 3.0), ("glow", 0.5) },
                Back = new[] { ("mane", 4.0), ("none", 1.0) },
            },
            // upright, muscular warrior with tusks, armour and an axe
            "orc" => new Prior
            {
                Size = 0.6, Mass = 0.62, Posture = 0.25, TorsoLen = 0.5, Shoulders = 0.8, Belly = 0.2,
                HeadSize = 0.55, Round = 0.4, Snout = 0.12, Nose = 0.55, Jaw = 0.85, Brow = 0.75, EyeSize = 0.35, EarSize = 0.5, HornSize = 0.4,
                ArmLen = 0.6, ArmTh = 0.65, Hands = 0.6, Claws = 0.15, GearSize = 0.65, LegLen = 0.5, LegTh = 0.6, Feet = 0.55, TailLen = 0.2, BackSize = 0.45,
                BeardChance = 0.1, Beard = 0.35, ShieldChance = 0.35, PauldronChance = 0.45,
                NoseShape = new[] { ("flat", 3.0), ("round", 0.6) },
                Ears = new[] { ("pointed", 3.0), ("elf", 1.0) }, Teeth = new[] { ("tusks", 5.0), ("fangs", 1.0) },
                Eyes = new[] { ("bead", 2.0), ("slit", 1.0), ("glow", 0.4) }, Back = new[] { ("none", 4.0), ("mane", 1.5), ("spikes", 0.3) },
                Weapon = new[] { ("axe", 3.0), ("club", 1.5), ("spear", 1.0), ("hammer", 1.0) },
                Outfit = new[] { ("armor", 2.5), ("belt", 1.5), ("loincloth", 1.5), ("tunic", 1.0) }, Helmet = new[] { ("none", 2.0), ("helmet", 1.5), ("horned", 1.2), ("cap", 0.5) },
            },
            // short, stocky, bearded, helmeted
            "dwarf" => new Prior
            {
                Size = 0.34, Mass = 0.75, Posture = 0.15, TorsoLen = 0.45, Shoulders = 0.7, Belly = 0.6,
                HeadSize = 0.7, Round = 0.6, Snout = 0.05, Nose = 0.9, Jaw = 0.5, Brow = 0.7, EyeSize = 0.35, EarSize = 0.35, HornSize = 0.3,
                ArmLen = 0.5, ArmTh = 0.6, Hands = 0.6, Claws = 0.0, GearSize = 0.55, LegLen = 0.2, LegTh = 0.65, Feet = 0.6, TailLen = 0.0, BackSize = 0.3,
                BeardChance = 0.95, Beard = 0.75, ShieldChance = 0.3, PauldronChance = 0.3,
                NoseShape = new[] { ("round", 4.0), ("long", 0.6) },
                Ears = new[] { ("round", 2.0), ("pointed", 0.5) }, Teeth = new[] { ("small", 3.0), ("none", 1.0) },
                Eyes = new[] { ("bead", 3.0), ("round", 1.0) }, Back = new[] { ("none", 5.0) },
                Weapon = new[] { ("axe", 3.0), ("hammer", 2.5), ("none", 0.5) },
                Outfit = new[] { ("armor", 2.0), ("tunic", 2.0), ("belt", 1.0) }, Helmet = new[] { ("helmet", 2.0), ("horned", 1.5), ("cap", 1.0), ("none", 1.0) },
            },
            // goblin: small, scrawny, big head and ears, pot belly
            _ => new Prior
            {
                Size = 0.3, Mass = 0.3, Posture = 0.4, TorsoLen = 0.45, Shoulders = 0.35, Belly = 0.45,
                HeadSize = 0.8, Round = 0.55, Snout = 0.15, Nose = 0.8, Jaw = 0.45, Brow = 0.45, EyeSize = 0.65, EarSize = 0.8, HornSize = 0.35,
                ArmLen = 0.6, ArmTh = 0.25, Hands = 0.55, Claws = 0.35, GearSize = 0.5, LegLen = 0.4, LegTh = 0.3, Feet = 0.65, TailLen = 0.4, BackSize = 0.4,
                ShieldChance = 0.12,
                NoseShape = new[] { ("long", 3.0), ("round", 1.0) },
                Ears = new[] { ("elf", 5.0), ("pointed", 1.0) }, Teeth = new[] { ("small", 2.0), ("fangs", 1.5), ("tusks", 0.6) },
                Eyes = new[] { ("round", 2.0), ("slit", 1.0), ("bead", 1.0), ("glow", 0.3) },
                Back = new[] { ("none", 4.0), ("mane", 1.0) }, Weapon = new[] { ("dagger", 2.0), ("spear", 1.5), ("club", 1.0), ("none", 2.0), ("staff", 0.4) },
                Outfit = new[] { ("loincloth", 3.0), ("none", 1.0), ("belt", 1.5), ("tunic", 1.0), ("robe", 0.3) }, Helmet = new[] { ("none", 4.0), ("cap", 1.2), ("hood", 1.5), ("crown", 0.15) },
            },
        };

        public override void SampleAnatomy(double[] v, Rng rng, string arch)
        {
            var p = PriorFor(arch);
            SetChoice(v, "form", arch);
            const double sp = 0.11;
            Around(v, rng, "size", p.Size, 0.12);
            Around(v, rng, "mass", p.Mass, sp);
            Around(v, rng, "posture", p.Posture, sp);
            Around(v, rng, "torso.length", p.TorsoLen, sp);
            Around(v, rng, "torso.shoulders", p.Shoulders, sp);
            Around(v, rng, "torso.belly", p.Belly, sp);
            Around(v, rng, "head.size", p.HeadSize, sp);
            Around(v, rng, "head.round", p.Round, sp);
            Around(v, rng, "head.snout", p.Snout, sp);
            Around(v, rng, "head.nose", p.Nose, sp);
            Around(v, rng, "head.jaw", p.Jaw, sp);
            Around(v, rng, "head.brow", p.Brow, sp);
            SetChoice(v, "head.teeth", PickWeighted(rng, p.Teeth));
            Around(v, rng, "head.eyeSize", p.EyeSize, sp);
            SetChoice(v, "head.eyeStyle", PickWeighted(rng, p.Eyes));
            SetChoice(v, "head.ears", PickWeighted(rng, p.Ears));
            Around(v, rng, "head.earSize", p.EarSize, sp);
            SetChoice(v, "head.horns", PickWeighted(rng, p.Horns));
            Around(v, rng, "head.hornSize", p.HornSize, sp);
            Around(v, rng, "arms.length", p.ArmLen, sp);
            Around(v, rng, "arms.thickness", p.ArmTh, sp);
            Around(v, rng, "arms.hands", p.Hands, sp);
            Around(v, rng, "arms.claws", p.Claws, sp);
            SetChoice(v, "gear.weapon", PickWeighted(rng, p.Weapon));
            Around(v, rng, "gear.size", p.GearSize, sp);
            Around(v, rng, "legs.length", p.LegLen, sp);
            Around(v, rng, "legs.thickness", p.LegTh, sp);
            SetChoice(v, "legs.stance", PickWeighted(rng, p.Stance));
            Around(v, rng, "legs.feet", p.Feet, sp);
            SetChoice(v, "tail.kind", PickWeighted(rng, p.Tail));
            Around(v, rng, "tail.length", p.TailLen, sp);
            SetChoice(v, "back.feature", PickWeighted(rng, p.Back));
            Around(v, rng, "back.size", p.BackSize, sp);
            SetChoice(v, "head.noseShape", PickWeighted(rng, p.NoseShape));
            SetChoice(v, "gear.outfit", PickWeighted(rng, p.Outfit));
            SetChoice(v, "gear.helmet", PickWeighted(rng, p.Helmet));
            Set(v, "gear.shield", rng.Chance(p.ShieldChance) ? 1 : 0);
            Set(v, "gear.pauldrons", rng.Chance(p.PauldronChance) ? 1 : 0);
            if (rng.Chance(p.BeardChance)) Around(v, rng, "head.beard", p.Beard, 0.15);
            else Set(v, "head.beard", 0);
        }

        public override FamilyStyleHints StyleHints(string arch)
        {
            var h = new FamilyStyleHints
            {
                Materials = new Dictionary<string, double> { ["hide"] = 4, ["scales"] = 0.5 },
                Patterns = new Dictionary<string, double> { ["none"] = 4, ["mottled"] = 1, ["spots"] = 0.6, ["stripes"] = 0.4 },
                Accents = new Dictionary<string, double> { ["bone"] = 3, ["dark"] = 2, ["gold"] = 0.4 },
                // goblin greens, olive, grey-green, teal, ochre, dusty violet
                Coats = new[] { Coats.Green(2.5), Coats.Olive(1.5), Coats.Teal(0.6), Coats.Grey(0.5), Coats.Sand(0.4), Coats.Muted(0.6), Coats.Vivid(0.3) },
                CountershadeMin = 0.15, CountershadeMax = 0.5,
                Schemes = new Dictionary<string, double> { ["natural"] = 4, ["analogous"] = 2, ["monochrome"] = 1.2, ["complementary"] = 0.5, ["split"] = 0.3 },
                SocksChance = 0.05, MaskChance = 0.04, PiebaldChance = 0.02,
            };
            switch (arch)
            {
                case "troll":
                    h.Coats = new[] { Coats.Green(1.4), Coats.Olive(1.2), Coats.Teal(1.2), Coats.Slate(1.0), Coats.Grey(1.0), Coats.Muted(0.8), Coats.Brown(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["mottled"] = 2, ["blotches"] = 0.6 };
                    h.WeightMin = 0.5; h.EnergyMax = 0.6;
                    break;
                case "golem":
                    h.Materials = new Dictionary<string, double> { ["stone"] = 6 };
                    h.Coats = new[] { Coats.Grey(2), Coats.Slate(1.2), Coats.Sand(1), Coats.Silver(0.6), Coats.Rust(0.5), Coats.Brown(0.6),
                        new CoatPrior("lichen", 0.4, 120, 20, 0.08, 0.25, 0.3, 0.55) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 3, ["mottled"] = 1.5, ["bands"] = 0.5 };
                    h.GlowChance = 1.0;
                    h.WeightMin = 0.75; h.WeightMax = 1.0; h.EnergyMax = 0.45;
                    break;
                case "ogre":
                    // green, olive and grey skins; warm tans stay rare (yellow-brown hide read as a bear)
                    h.Coats = new[] { Coats.Olive(1.6), Coats.Green(1.2), Coats.Grey(0.8), Coats.Slate(0.6), Coats.Muted(0.8), Coats.Teal(0.4), Coats.Tan(0.3) };
                    h.WeightMin = 0.65; h.EnergyMax = 0.55;
                    break;
                case "imp":
                    h.Coats = new[] { Coats.Crimson(2.5), Coats.Rust(1.2), Coats.Violet(1), Coats.Pink(0.6), Coats.Vivid(0.8), Coats.Black(0.4) };
                    h.GlowChance = 0.35; h.EnergyMin = 0.55; h.WeightMax = 0.4;
                    break;
                case "kobold":
                    h.Materials = new Dictionary<string, double> { ["scales"] = 4, ["hide"] = 1 };
                    h.Coats = new[] { Coats.Rust(1.2), Coats.Brown(1), Coats.Green(1), Coats.Blue(0.8), Coats.Sand(0.8), Coats.Teal(0.6), Coats.Crimson(0.5), Coats.Vivid(0.5) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 2, ["dorsal"] = 1.5, ["bands"] = 1, ["spots"] = 1 };
                    h.EnergyMin = 0.5;
                    break;
                case "orc":
                    h.Coats = new[] { Coats.Green(2), Coats.Olive(1.2), Coats.Grey(1), Coats.Slate(0.6), Coats.Brown(0.6), Coats.Rust(0.3) };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 3, ["bone"] = 1.5, ["gold"] = 0.4 };
                    h.WeightMin = 0.4;
                    break;
                case "dwarf":
                    // human skin tones; beards take the marking colour (red-brown to black) or grey
                    h.Coats = new[] { Coats.Pink(1.5), Coats.Tan(1.2), Coats.Brown(0.8), Coats.Chocolate(0.4) };
                    h.Schemes = new Dictionary<string, double> { ["natural"] = 5, ["analogous"] = 1 };
                    h.Accents = new Dictionary<string, double> { ["dark"] = 2, ["gold"] = 1.2, ["bone"] = 0.8 };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 1 };
                    h.WeightMin = 0.5; h.EnergyMax = 0.6;
                    break;
                case "yeti":
                    // snow white, silver, grey and the brown forest giant
                    h.Materials = new Dictionary<string, double> { ["fur"] = 6 };
                    h.Coats = new[] { Coats.Cream(2.5), Coats.Silver(1.2), Coats.Grey(1), Coats.Albino(0.6), Coats.Brown(0.5), Coats.Tan(0.4) };
                    h.Patterns = new Dictionary<string, double> { ["none"] = 4, ["saddle"] = 0.5 };
                    h.MaskChance = 0.08;
                    h.WeightMin = 0.55;
                    break;
            }
            return h;
        }

        public override void AdjustSample(double[] values, Rng rng, string arch)
        {
            if (Schema.Get("head.eyeStyle").Options[(int)values[Schema.IndexOf("head.eyeStyle")]].Id == "glow")
                Set(values, "color.glow", Math.Max(values[Schema.IndexOf("color.glow")], 0.6));
            // clothing dyes per culture: hue, saturation range, value range
            var dyes = arch switch
            {
                "dwarf" => new[] { (250.0, 0.4, 0.65, 0.25, 0.42, 1.2), (25.0, 0.5, 0.75, 0.28, 0.45, 1.2), (140.0, 0.35, 0.6, 0.22, 0.4, 1.0), (55.0, 0.35, 0.5, 0.25, 0.42, 1.0), (305.0, 0.35, 0.55, 0.22, 0.38, 0.5), (80.0, 0.45, 0.65, 0.42, 0.58, 0.5) },
                "orc" => new[] { (45.0, 0.3, 0.45, 0.1, 0.25, 1.5), (25.0, 0.5, 0.75, 0.22, 0.38, 1.0), (265.0, 0.04, 0.1, 0.05, 0.15, 1.0), (55.0, 0.35, 0.5, 0.22, 0.4, 1.0), (38.0, 0.5, 0.7, 0.28, 0.42, 0.6) },
                "imp" => new[] { (265.0, 0.04, 0.1, 0.05, 0.15, 1.5), (25.0, 0.55, 0.8, 0.25, 0.4, 1.0), (305.0, 0.4, 0.65, 0.2, 0.35, 1.0) },
                _ => new[] { (55.0, 0.35, 0.5, 0.22, 0.4, 2.0), (78.0, 0.12, 0.25, 0.48, 0.66, 1.5), (45.0, 0.3, 0.45, 0.1, 0.25, 1.0), (25.0, 0.45, 0.7, 0.25, 0.42, 0.7), (140.0, 0.3, 0.5, 0.22, 0.4, 0.6) },
            };
            var dye = PickColour(rng, dyes);
            Set(values, "color.clothHue", dye.Item1 + rng.Gaussian(0, 6, 2.0));
            Set(values, "color.clothSaturation", rng.Range(dye.Item2, dye.Item3));
            Set(values, "color.clothValue", rng.Range(dye.Item4, dye.Item5));

            // hair: black, browns, auburn, ginger, blond, grey and white (hue, sat range, value range, weight)
            var hairs = arch switch
            {
                "dwarf" => new[] { (40.0, 0.05, 0.15, 0.08, 0.16, 1.0), (38.0, 0.25, 0.4, 0.16, 0.26, 1.2), (30.0, 0.38, 0.52, 0.26, 0.36, 1.2), (45.0, 0.5, 0.65, 0.42, 0.52, 1.0),
                    (85.0, 0.3, 0.45, 0.62, 0.74, 0.6), (70.0, 0.02, 0.06, 0.5, 0.62, 0.8), (90.0, 0.02, 0.05, 0.8, 0.9, 0.6) },
                "orc" or "troll" or "ogre" => new[] { (40.0, 0.05, 0.15, 0.06, 0.14, 2.0), (38.0, 0.3, 0.45, 0.14, 0.24, 1.0), (70.0, 0.02, 0.06, 0.45, 0.6, 0.6), (90.0, 0.02, 0.05, 0.78, 0.88, 0.3) },
                _ => new[] { (40.0, 0.05, 0.15, 0.06, 0.14, 1.5), (38.0, 0.3, 0.45, 0.14, 0.24, 1.0), (70.0, 0.02, 0.06, 0.5, 0.62, 0.5) },
            };
            var hair = PickColour(rng, hairs);
            Set(values, "color.hairHue", hair.Item1 + rng.Gaussian(0, 4, 2.0));
            Set(values, "color.hairSaturation", rng.Range(hair.Item2, hair.Item3));
            Set(values, "color.hairValue", rng.Range(hair.Item4, hair.Item5));

            string helm = Schema.Get("gear.helmet").Options[(int)values[Schema.IndexOf("gear.helmet")]].Id;
            string metal = arch switch
            {
                "dwarf" => PickWeighted(rng, ("steel", 2.0), ("bronze", 1.0), ("gold", 0.25)),
                "orc" => PickWeighted(rng, ("iron", 2.0), ("steel", 1.0), ("bronze", 0.5)),
                _ => PickWeighted(rng, ("iron", 1.5), ("bronze", 1.0), ("steel", 0.8)),
            };
            SetChoice(values, "color.metal", helm == "crown" ? "gold" : metal);

            // staff bearers are shamans: mostly robed and hooded
            string weapon = Schema.Get("gear.weapon").Options[(int)values[Schema.IndexOf("gear.weapon")]].Id;
            if (weapon == "staff" && arch != "golem" && arch != "yeti")
            {
                if (rng.Chance(0.6)) SetChoice(values, "gear.outfit", "robe");
                if (helm == "none" && rng.Chance(0.5)) SetChoice(values, "gear.helmet", "hood");
            }
        }

        private static (double, double, double, double, double, double) PickColour(Rng rng, (double, double, double, double, double, double)[] options)
        {
            var w = new List<double>();
            foreach (var o in options) w.Add(o.Item6);
            return options[rng.Weighted(w)];
        }

        // ------------------------------------------------------------------ anatomy

        public override CreatureAnatomy BuildAnatomy(CreatureGenome g, CreatureRegistry registry)
        {
            var b = new AnatomyBuilder(Id, g.AnatomySeed);
            string form = g.GetChoice("form");
            b.Trait(form);
            bool golem = form == "golem";

            double size = g.Get("size");
            double baseL = DMath.Lerp(12, 30, DMath.Pow(size, 1.05));
            double mass = g.Get("mass");
            double hunch = g.Get("posture");
            double torsoH = baseL * DMath.Lerp(0.75, 1.05, g.Get("torso.length"));
            double hipR = baseL * DMath.Lerp(0.2, 0.34, mass);
            double chestR = baseL * DMath.Lerp(0.24, 0.4, mass);
            double shoulderW = chestR * DMath.Lerp(0.9, 1.4, g.Get("torso.shoulders"));
            double belly = g.Get("torso.belly");
            double legLen = baseL * DMath.Lerp(0.6, 1.25, g.Get("legs.length"));
            string stance = g.GetChoice("legs.stance");
            bool digi = stance == "digitigrade";
            double legTh = g.Get("legs.thickness");
            double feet = g.Get("legs.feet");

            // ---- skeleton: pelvis and an upright spine leaning forward with the posture
            int root = b.Bone("root", -1, Vec3.Zero, BoneRole.Root);
            double hipH = legLen * (digi ? 0.9 : 0.94);
            double lean = DMath.Lerp(4, 46, hunch) * DMath.Deg2Rad;
            Vec3 spineDir = new Vec3(DMath.Sin(lean), DMath.Cos(lean), 0);
            Vec3 back = new Vec3(-1, 0, 0);
            Vec3 P = new Vec3(0, hipH, 0);
            Vec3 C = P + spineDir * torsoH;
            int pelvis = b.BoneAlong("pelvis", root, P, spineDir, back, BoneRole.Pelvis, 0, torsoH * 0.5);
            int spine = b.BoneAlong("spine", pelvis, P + spineDir * (torsoH * 0.5), spineDir, back, BoneRole.Spine, 0, torsoH * 0.5);
            int chest = b.BoneAlong("chest", spine, C, spineDir, back, BoneRole.Chest, 0, chestR);
            b.Rig.Root = root; b.Rig.Pelvis = pelvis; b.Rig.Chest = chest;
            b.Rig.Upright = true;
            b.Rig.Spine = new ChainRig { Name = "spine", Bones = new[] { pelvis, spine, chest }, Lengths = new[] { torsoH * 0.5, torsoH * 0.5 }, TotalLength = torsoH };

            // ---- torso (bone frames: +X along the spine (up), +Y backwards, +Z right)
            double blend = DMath.Clamp(chestR * 0.35, 1.5, 4.0);
            int body = b.Group("body", blend, 0, golem ? GroupFlags.CreaseShade : GroupFlags.None);
            double dom = torsoH + hipR + chestR;
            var hips = b.Ellipsoid(body, pelvis, new Vec3(hipR * 0.1, hipR * 0.05, 0), new Vec3(hipR * 0.95, hipR * 0.9, hipR * 1.15), MaterialSlot.Primary, PatternDomain.Body, 0, 0.3);
            hips.DomainLength = dom; hips.Tag = "hips";
            double bellyR = DMath.Lerp(hipR * 0.9, Math.Max(hipR, chestR) * 1.25, belly);
            var bel = b.Ellipsoid(body, spine, new Vec3(-torsoH * 0.05, -bellyR * DMath.Lerp(0.1, 0.4, belly), 0), new Vec3(torsoH * 0.5, bellyR, bellyR * 1.02),
                MaterialSlot.Primary, PatternDomain.Body, 0.25, 0.7);
            bel.DomainLength = dom; bel.Tag = "belly";
            var chestP = b.Ellipsoid(body, chest, new Vec3(-chestR * 0.3, chestR * 0.05, 0), new Vec3(chestR * 0.95, chestR * 0.82, shoulderW), MaterialSlot.Primary, PatternDomain.Body, 0.6, 1.0);
            chestP.DomainLength = dom; chestP.Tag = "chest";
            PrimitiveDef? hump = null;
            if (hunch > 0.45)
            {
                // hunched backs: a trapezius hump that the head juts forward from
                hump = b.Ellipsoid(body, chest, new Vec3(chestR * 0.1, chestR * 0.45, 0), new Vec3(chestR * 0.7, chestR * 0.55 * hunch * 1.3, shoulderW * 0.7),
                    MaterialSlot.Primary, PatternDomain.Body, 0.8, 1.0);
                hump.DomainLength = dom; hump.Tag = "hump";
            }
            if (golem) foreach (var prim in new[] { hips, bel, chestP }) { prim.Flags |= PrimFlags.Faceted; prim.FacetSeed = (uint)(g.AnatomySeed & 0xFFFFFF) + (uint)prim.Tag.Length; }

            // ---- neck and head
            double headSize = g.Get("head.size");
            double round = g.Get("head.round");
            // generous heads: faces carry the character at 30-60 px figure height
            double skullL = baseL * DMath.Lerp(0.52, 0.88, headSize) * DMath.Lerp(1.05, 0.92, round);
            double skullH = skullL * DMath.Lerp(0.8, 1.02, round);
            double skullW = skullL * DMath.Lerp(0.78, 0.98, round);
            Vec3 neckDir = Vec3.Lerp(Vec3.UnitY, Vec3.UnitX, DMath.Lerp(0.12, 0.72, hunch)).Normalized();
            // the skull sits on top of the chest (hunched creatures carry it forwards instead)
            double neckLen = Math.Max(1.5, (0.24 * chestR + 0.3 * skullH) * DMath.Lerp(1.0, 0.65, hunch));
            Vec3 N0 = C + spineDir * (chestR * 0.45) + new Vec3(chestR * 0.12, 0, 0);
            int neck = b.BoneAlong("neck", chest, N0, neckDir, back, BoneRole.Neck, 0, neckLen);
            Vec3 Hj = N0 + neckDir * neckLen;
            string teeth = g.GetChoice("head.teeth");
            string eye = g.GetChoice("head.eyeStyle");
            double snout = g.Get("head.snout");
            var hs = new HeadSpec
            {
                ParentBone = neck,
                Position = Hj,
                Forward = new Vec3(1, DMath.Lerp(0.05, -0.12, hunch), 0),
                SkullLength = skullL,
                SkullHeight = skullH,
                SkullWidth = skullW,
                SkullOffset = 0.22,
                SnoutLength = skullL * DMath.Lerp(0.08, 1.0, snout),
                SnoutRadius = skullH * DMath.Lerp(0.3, 0.34, snout),
                SnoutTaper = DMath.Lerp(0.95, 0.7, snout),
                SnoutDrop = 0.08,
                SnoutHeight = DMath.Lerp(-0.2, -0.1, snout),
                SnoutWidth = golem ? 1.2 : 1.0,
                NoseSize = golem ? 0.3 : DMath.Lerp(0.6, 2.1, g.Get("head.nose")) * DMath.Lerp(1.0, 0.7, snout),
                Nose = golem ? NoseKind.Pad : g.GetChoice("head.noseShape") switch { "round" => NoseKind.Round, "long" => NoseKind.Long, "flat" => NoseKind.Flat, _ => NoseKind.Pad },
                DarkNose = form == "yeti" || form == "kobold",
                JawLength = DMath.Lerp(0.8, 1.0, g.Get("head.jaw")),
                JawRadius = skullH * DMath.Lerp(0.2, 0.34, g.Get("head.jaw")),
                JawDepth = DMath.Lerp(0.7, 1.2, g.Get("head.jaw")),
                Brow = g.Get("head.brow"),
                Cheeks = form == "goblin" || form == "ogre" ? 0.35 : 0.1,
                EyeSize = DMath.Clamp(DMath.Lerp(1.6, 4.4, g.Get("head.eyeSize")) * Math.Sqrt(skullL / 9.0), 1.4, 5.5),
                EyeStyle = QuadrupedFamily.ParseEye(eye),
                EyeForward = 0.9,
                EyeHeight = DMath.Lerp(0.15, 0.35, round),
                EyeAspect = eye == "slit" ? 1.2 : 1.0,
                Teeth = teeth switch { "none" => TeethKind.None, "fangs" => TeethKind.Fangs, "tusks" => TeethKind.Tusks, _ => TeethKind.Small },
                TeethSize = DMath.Clamp(skullL / 8.0, 0.8, 1.7),
                // own group: a contour separates the face from the shoulders (readable silhouettes)
                Group = -1,
                BlendRadius = DMath.Clamp(skullL * 0.2, 1.2, 2.6),
                DomainLength = skullL * 1.8,
                JawOpenMax = 0.6,
                Nostrils = snout > 0.5,
            };
            var head = Heads.Build(b, hs);
            if (golem) MarkFaceted(b, head.Group, g.AnatomySeed);
            var nk = b.Cone(body, neck, Vec3.Zero, Math.Max(1.2, chestR * DMath.Lerp(0.45, 0.62, mass)), head.Head, head.SkullCenterLocal * 0.4,
                Math.Max(1.0, skullH * 0.38), MaterialSlot.Primary, PatternDomain.Neck, 0, 1);
            nk.DomainLength = neckLen + 2;
            b.Rig.Neck = new ChainRig { Name = "neck", Bones = new[] { neck, head.Head }, Lengths = new[] { neckLen }, TotalLength = neckLen };

            string ears = g.GetChoice("head.ears");
            Ears.Build(b, new EarSpec
            {
                Kind = ears == "elf" ? EarKind.Elf : QuadrupedFamily.ParseEar(ears),
                HeadBone = head.Head,
                // humanoid ears sit at the sides of the skull; only pointed/floppy ears start higher (round
                // ears on top of the head turned ogres into bears)
                Base = head.SkullCenterLocal + new Vec3(-skullL * 0.05, skullH * (ears switch { "elf" => 0.05, "round" => -0.04, "floppy" => 0.2, _ => 0.28 }), skullW * 0.45),
                // humanoid round ears stay small (large round ears turn ogres into bears)
                Size = skullL * DMath.Lerp(0.3, 0.9, g.Get("head.earSize")) * (ears == "round" ? 0.68 : 1.0),
                Width = ears == "round" ? 1.15 : 1.0,
            });
            string horns = g.GetChoice("head.horns");
            if (horns != "none")
            {
                Horns.Build(b, new HornSpec
                {
                    Kind = QuadrupedFamily.ParseHorn(horns),
                    HeadBone = head.Head,
                    Base = head.SkullCenterLocal + new Vec3(skullL * 0.1, skullH * 0.42, skullW * 0.25),
                    Size = skullL * DMath.Lerp(0.35, 1.1, g.Get("head.hornSize")),
                    Thickness = DMath.Lerp(0.8, 1.2, mass),
                });
                b.Trait("horns:" + horns);
            }

            // ---- legs
            double legTop = Math.Max(1.3, hipR * DMath.Lerp(0.45, 0.72, legTh));
            double footLen = Math.Max(2.0, baseL * DMath.Lerp(0.22, 0.42, feet));
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 hipJ = P + new Vec3(0, -hipR * 0.3, side * hipR * 0.62);
                Vec3 contact = new Vec3(hipJ.X + footLen * 0.1, 0, side * hipR * 0.7);
                var spec = new LegSpec
                {
                    Name = "leg" + (side < 0 ? "L" : "R"),
                    Side = side,
                    Pair = 0,
                    Front = false,
                    AnchorBone = pelvis,
                    Hip = hipJ,
                    Contact = contact,
                    Style = digi ? LegStyle.Digitigrade : LegStyle.Plantigrade,
                    RadiusTop = legTop,
                    RadiusKnee = Math.Max(0.9, legTop * 0.7),
                    RadiusAnkle = Math.Max(0.8, legTop * 0.5),
                    RadiusFoot = Math.Max(0.8, legTop * 0.55),
                    Haunch = 0.9,
                    PawRadius = Math.Max(0.9, legTop * DMath.Lerp(0.55, 0.8, feet)),
                    FootMaterial = MaterialSlot.Primary,
                    Claws = g.Get("arms.claws") > 0.45 ? 3 : 0,
                    ClawSize = DMath.Lerp(0.5, 1.2, g.Get("arms.claws")),
                    Phase = side < 0 ? 0.0 : 0.5,
                    LiftHeight = Math.Max(1.5, legLen * 0.16),
                    MetaAngle = 18,
                    BlendRadius = DMath.Clamp(legTop * 0.5, 0.8, 2.2),
                    DomainLength = legLen,
                };
                if (digi)
                {
                    spec.Length = hipJ.Y * 1.22;
                    spec.Upper = 0.38; spec.Lower = 0.38; spec.Distal = 0.24;
                    spec.PawLength = footLen * 0.8;
                }
                else
                {
                    double legPart = hipJ.Y * 1.03;
                    spec.Length = legPart + footLen;
                    spec.Upper = legPart * 0.5 / spec.Length;
                    spec.Lower = legPart * 0.5 / spec.Length;
                    spec.Distal = footLen / spec.Length;
                    spec.PawLength = footLen;
                }
                var leg = Limbs.Build(b, spec);
                if (golem) MarkFaceted(b, leg.Group, g.AnatomySeed);
                b.Rig.Legs.Add(leg);
            }

            // ---- arms (with hands) and the weapon in the right hand
            double armLen = baseL * DMath.Lerp(0.75, 1.6, g.Get("arms.length"));
            double armTh = g.Get("arms.thickness");
            double armTop = Math.Max(1.1, chestR * DMath.Lerp(0.3, 0.52, armTh));
            double handR = Math.Max(0.9, armTop * DMath.Lerp(0.6, 1.05, g.Get("arms.hands")));
            string weapon = g.GetChoice("gear.weapon");
            LegRig? rightArm = null, leftArm = null;
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 sh = C + new Vec3(-chestR * 0.1, -chestR * 0.18, side * shoulderW * 0.92);
                Vec3 wrist = sh + new Vec3(armLen * 0.12, -armLen * 0.8, side * armLen * 0.12);
                var spec = new LegSpec
                {
                    Name = "arm" + (side < 0 ? "L" : "R"),
                    Side = side,
                    Pair = 0,
                    Front = true,
                    IsArm = true,
                    AnchorBone = chest,
                    Hip = sh,
                    Contact = wrist,
                    Style = LegStyle.Plantigrade,
                    Length = armLen,
                    Upper = 0.44, Lower = 0.4, Distal = 0.16,
                    RadiusTop = armTop,
                    RadiusKnee = Math.Max(0.8, armTop * 0.72),
                    RadiusAnkle = Math.Max(0.7, armTop * 0.58),
                    RadiusFoot = handR,
                    Haunch = golem ? 1.3 : 1.0,
                    PawLength = Math.Max(1.6, handR * 2.0),
                    PawRadius = handR,
                    Hand = true,
                    Claws = g.Get("arms.claws") > 0.45 ? 3 : 0,
                    ClawSize = DMath.Lerp(0.5, 1.3, g.Get("arms.claws")),
                    BlendRadius = DMath.Clamp(armTop * 0.5, 0.8, 2.0),
                    DomainLength = armLen,
                };
                var arm = Limbs.Build(b, spec);
                arm.Phase = side < 0 ? 0.5 : 0.0;
                if (golem) MarkFaceted(b, arm.Group, g.AnatomySeed);
                b.Rig.Arms.Add(arm);
                if (side > 0) rightArm = arm;
                else leftArm = arm;
            }
            if (weapon != "none" && rightArm != null)
            {
                BuildWeapon(b, rightArm, weapon, baseL * DMath.Lerp(0.7, 1.3, g.Get("gear.size")), handR);
                b.Rig.Tuning["action.armSide"] = 1;
                b.Trait("weapon:" + weapon);
            }

            // ---- gear: clothing, head gear, beard, shield and pauldrons (own groups and colours)
            string outfit = g.GetChoice("gear.outfit");
            string helmet = g.GetChoice("gear.helmet");
            var torso = new GearTorso { Pelvis = pelvis, Chest = chest, BlendRadius = blend, HipRadius = hipR, ChestRadius = chestR, HipJointHeight = hipH - hipR * 0.3 };
            torso.Shell.Add(hips); torso.Shell.Add(bel); torso.Shell.Add(chestP);
            if (hump != null) torso.Shell.Add(hump);
            double waist = Gear.Waist(torso);
            double waistY = b.RestWorld(pelvis).Point(new Vec3(waist, 0, 0)).Y;
            double beltW = Math.Max(1.2, hipR * 0.3);
            switch (outfit)
            {
                case "loincloth":
                    Gear.Belt(b, torso, waist, beltW, MaterialSlot.Cloth, -1, false);
                    Gear.Loincloth(b, torso, waist, Math.Max(2.5, (waistY - torso.HipJointHeight) + legLen * 0.32), MaterialSlot.Cloth);
                    break;
                case "belt":
                    Gear.Belt(b, torso, waist, beltW, MaterialSlot.Cloth, 0, true);
                    Gear.Pouch(b, torso, waist - beltW, Math.Max(1.0, hipR * 0.3), -1, MaterialSlot.Cloth);
                    break;
                case "tunic":
                    Gear.Cover(b, torso, "gear.tunic", MaterialSlot.Cloth, 1.04, 0.45, p => true);
                    Gear.Skirt(b, torso, waist, Math.Max(2.5, waistY - torso.HipJointHeight * 0.6), MaterialSlot.Cloth);
                    Gear.Belt(b, torso, waist, beltW, MaterialSlot.Cloth, -2, true);
                    break;
                case "armor":
                    // cuirass over chest and belly, cloth skirt below
                    Gear.Cover(b, torso, "gear.undercoat", MaterialSlot.Cloth, 1.03, 0.4, p => p.Tag == "hips");
                    Gear.Skirt(b, torso, waist, Math.Max(2.5, waistY - torso.HipJointHeight * 0.62), MaterialSlot.Cloth);
                    Gear.Cover(b, torso, "gear.breastplate", MaterialSlot.Metal, 1.05, 0.5, p => p.Tag != "hips", Gear.Bias + 0.25);
                    Gear.Belt(b, torso, waist, beltW, MaterialSlot.Cloth, -2, true);
                    break;
                case "robe":
                    Gear.Cover(b, torso, "gear.robe", MaterialSlot.Cloth, 1.05, 0.45, p => true);
                    Gear.Skirt(b, torso, waist, Math.Max(3.0, waistY - torso.HipJointHeight * 0.16), MaterialSlot.Cloth, 1.12);
                    Gear.Belt(b, torso, waist, beltW, MaterialSlot.Cloth, -2, false);
                    break;
            }
            if (outfit != "none") b.Trait("outfit:" + outfit);
            var headGear = helmet switch
            {
                "cap" => HeadGearKind.Cap,
                "helmet" => HeadGearKind.Helmet,
                "horned" => HeadGearKind.Horned,
                "hood" => HeadGearKind.Hood,
                "crown" => HeadGearKind.Crown,
                _ => HeadGearKind.None,
            };
            Gear.HeadGear(b, head, headGear, neck, 1.0);
            if (headGear != HeadGearKind.None) b.Trait("headgear:" + helmet);
            double beard = g.Get("head.beard");
            if (beard > 0.05 && !golem)
            {
                Gear.Beard(b, head, beard);
                b.Trait("beard");
            }
            if (g.GetBool("gear.shield") && leftArm != null)
            {
                Gear.Shield(b, leftArm, Math.Max(2.4, chestR * 0.95) * DMath.Lerp(0.85, 1.2, g.Get("gear.size")), handR);
                b.Trait("shield");
            }
            if (g.GetBool("gear.pauldrons"))
                foreach (var arm in b.Rig.Arms) Gear.Pauldron(b, arm, armTop, form == "orc" || form == "ogre");

            // ---- tail
            string tail = g.GetChoice("tail.kind");
            double tailLen = baseL * DMath.Lerp(0.4, 1.4, g.Get("tail.length"));
            if (tail != "none")
            {
                double tr = Math.Max(0.8, hipR * 0.3);
                b.Rig.Tail = Chains.Build(b, new ChainSpec
                {
                    Name = "tail",
                    ParentBone = pelvis,
                    Base = P + new Vec3(-hipR * 0.75, -hipR * 0.2, 0),
                    Direction = new Vec3(-0.75, -0.55, 0),
                    Length = tailLen,
                    Segments = DMath.Clamp((int)Math.Round(tailLen / 3.0), 3, 9),
                    RadiusBase = tr,
                    RadiusTip = Math.Max(0.45, tr * 0.3),
                    Curl = form == "imp" ? 1.4 : 0.6,
                    Tip = tail switch { "spade" => TailTip.Spade, "tuft" => TailTip.Tuft, "club" => TailTip.Club, _ => TailTip.Plain },
                    TipSize = DMath.Clamp(tr * 0.9, 0.8, 2.0),
                    Group = body,
                    Stiffness = 0.45,
                    Droop = 0.6,
                });
            }

            // ---- back features
            string backF = g.GetChoice("back.feature");
            double bs = g.Get("back.size");
            switch (backF)
            {
                case "mane":
                {
                    // shaggy hair over the head, neck and shoulders
                    var ds = new DorsalSpec { Kind = DorsalKind.Mane, Size = baseL * DMath.Lerp(0.12, 0.24, bs), Material = MaterialSlot.Marking };
                    var hx = b.RestWorld(head.Head);
                    if (g.GetChoice("gear.helmet") == "none")
                        ds.Anchors.Add((head.Head, hx.Point(head.SkullCenterLocal + new Vec3(-skullL * 0.25, skullH * 0.45, 0)), Vec3.UnitY, 0.9));
                    ds.Anchors.Add((neck, N0 + new Vec3(-chestR * 0.35, chestR * 0.3, 0), new Vec3(-0.4, 1, 0).Normalized(), 1.1));
                    ds.Anchors.Add((chest, C + new Vec3(-chestR * 0.55, 0, 0), new Vec3(-1, 0.4, 0).Normalized(), 1.0));
                    Dorsal.Build(b, ds);
                    break;
                }
                case "spikes":
                case "crystals":
                {
                    var ds = new DorsalSpec
                    {
                        Kind = DorsalKind.Spikes,
                        Size = baseL * DMath.Lerp(0.1, 0.26, bs),
                        Material = backF == "crystals" ? MaterialSlot.Glow : MaterialSlot.Accent,
                    };
                    int n = backF == "crystals" ? 3 : 4;
                    for (int i = 0; i < n; i++)
                    {
                        double t = (i + 0.5) / n;
                        Vec3 pos = Vec3.Lerp(P, C, DMath.Lerp(0.35, 1.0, t)) + new Vec3(-DMath.Lerp(hipR, chestR, t) * 0.95, 0, (i % 2 == 0 ? 1 : -1) * chestR * 0.25 * (backF == "crystals" ? 1 : 0));
                        ds.Anchors.Add((t < 0.5 ? spine : chest, pos, new Vec3(-1, 0.35, 0).Normalized(), DMath.Lerp(0.8, 1.2, t)));
                    }
                    Dorsal.Build(b, ds);
                    break;
                }
                case "wings":
                {
                    double span = baseL * DMath.Lerp(0.9, 1.8, bs);
                    Wings.BuildPair(b, new WingSpec
                    {
                        Kind = WingKind.Membrane,
                        AnchorBone = chest,
                        Root = C + new Vec3(-chestR * 0.6, -chestR * 0.1, chestR * 0.35),
                        Span = span,
                        Chord = span * 0.4,
                        Raise = 0.55,
                        SweepBack = 0.55,
                        BoneRadius = Math.Max(0.6, armTop * 0.4),
                        Fingers = 3,
                        BodyBone = spine,
                        BodyAttach = P + spineDir * (torsoH * 0.35) + new Vec3(-hipR * 0.8, 0, hipR * 0.3),
                        ClawSize = 0.8,
                    });
                    break;
                }
            }

            // ---- rig semantics & metrics
            b.Rig.Locomotion = LocomotionKind.Legged;
            b.Rig.LocomotionModule = "legged";
            b.Rig.Action = form switch
            {
                "golem" or "troll" or "ogre" => weapon != "none" ? ActionStyle.Strike : ActionStyle.Slam,
                "yeti" => ActionStyle.Roar,
                "imp" => backF == "wings" ? ActionStyle.WingBuffet : ActionStyle.Strike,
                _ => weapon != "none" ? ActionStyle.Strike : ActionStyle.Bite,
            };
            b.Rig.Rest = RestStyle.SitSlump;
            b.Rig.Tuning["rest.bodyHeight"] = hipR * 0.95;
            b.Rig.Tuning["gait.walkDuty"] = 0.62;
            b.Rig.Tuning["gait.runDuty"] = 0.38;
            b.Metrics.LegLength = legLen;
            b.Metrics.HipHeight = hipH;
            b.Metrics.BackHeight = C.Y + chestR;
            b.Metrics.BodyLength = (C.Y + chestR + skullH) * 0.85;
            b.Metrics.Mass = DMath.Pow(baseL / 20.0, 3) * DMath.Lerp(0.6, 1.8, mass) * (golem ? 1.5 : 1.0);
            b.Metrics.WalkSpeed = Math.Sqrt(0.3 * 400 * legLen) * DMath.Lerp(0.8, 1.05, g.GetOr("motion.speed", 0.5)) * (golem ? 0.8 : 1.0);
            b.Metrics.RunSpeed = Math.Sqrt(2.0 * 400 * legLen) * DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)) * (golem ? 0.75 : 1.0);
            b.Metrics.PatternScale = DMath.Clamp(Math.Sqrt(baseL / 20.0), 0.75, 1.5);
            double reach = weapon != "none" ? baseL * 1.3 : 0;
            b.AnimationMargin = Math.Max(7, Math.Max(armLen * 0.55, baseL * 0.45) + reach * 0.35);
            b.ExtraHeadroom = Math.Max(4, armLen * 0.6 + reach * 0.5);
            return b.Build(g.AnatomyKey);
        }

        private static void MarkFaceted(AnatomyBuilder b, int group, ulong seed)
        {
            foreach (var p in b.Primitives)
            {
                if (p.Group != group || (p.Flags & PrimFlags.Thin) != 0) continue;
                p.Flags |= PrimFlags.Faceted;
                p.FacetSeed = (uint)((seed >> 8) & 0xFFFFF) + (uint)p.Tag.Length * 131u;
            }
        }

        /// <summary>Weapon held in the hand (hand frame: +X along the fingers, +Y forwards, +Z sideways).</summary>
        private static void BuildWeapon(AnatomyBuilder b, LegRig arm, string kind, double len, double handR)
        {
            int hand = arm.JointBones[2];
            int g = b.Group("weapon", 0.6, arm.Side, GroupFlags.None, 0.4);
            Vec3 grip = new Vec3(-handR * 0.6, 0, 0);
            Vec3 up = new Vec3(0.12, 1, 0).Normalized();
            Vec3 edge = new Vec3(1, -0.12, 0).Normalized();
            PrimitiveDef C(Vec3 a, double ra, Vec3 c, double rc, MaterialSlot m)
            {
                var p = b.Cone(g, hand, a, ra, hand, c, rc, m, PatternDomain.None, 0, 1, arm.Side);
                p.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                p.Tag = "weapon." + kind;
                return p;
            }
            PrimitiveDef E(Vec3 at, Vec3 radii, MaterialSlot m, Quat? rot = null)
            {
                var p = b.Ellipsoid(g, hand, at, radii, m, PatternDomain.None, 0, 1, rot, arm.Side);
                p.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
                p.Tag = "weapon." + kind;
                return p;
            }
            // shafts and hafts are horn/wood (accent, darkened), blades and heads forged metal
            switch (kind)
            {
                case "club":
                {
                    var shaft = C(grip - up * (len * 0.12), 0.7, grip + up * (len * 0.55), Math.Max(0.9, len * 0.1), MaterialSlot.Accent);
                    shaft.ShadeBias = -1;
                    var head = E(grip + up * (len * 0.72), new Vec3(len * 0.14, len * 0.22, len * 0.14), MaterialSlot.Accent);
                    head.ShadeBias = -1;
                    for (int i = 0; i < 3; i++)
                    {
                        double a = i * 2.1;
                        Vec3 dir = new Vec3(DMath.Cos(a), 0.3, DMath.Sin(a)).Normalized();
                        Vec3 at = grip + up * (len * (0.66 + 0.1 * i));
                        var spike = C(at + dir * (len * 0.1), 0.5, at + dir * (len * 0.2), 0.3, MaterialSlot.Metal);
                        spike.Flags |= PrimFlags.Thin | PrimFlags.Hard;
                        spike.MinQuality = 1;
                    }
                    break;
                }
                case "spear":
                {
                    C(grip - up * (len * 0.45), 0.5, grip + up * (len * 0.95), 0.5, MaterialSlot.Accent).ShadeBias = -1;
                    var tip = C(grip + up * (len * 0.92), 1.0, grip + up * (len * 1.18), 0.3, MaterialSlot.Metal);
                    tip.Flags |= PrimFlags.Hard;
                    break;
                }
                case "dagger":
                {
                    C(grip - up * (handR * 0.8), 0.5, grip + up * (handR * 0.6), 0.5, MaterialSlot.Accent).ShadeBias = -1;
                    var guard = C(grip + up * (handR * 0.65) - edge * 0.9, 0.45, grip + up * (handR * 0.65) + edge * 0.9, 0.45, MaterialSlot.Metal);
                    guard.Flags |= PrimFlags.Hard;
                    var blade = C(grip + up * (handR * 0.7), 0.85, grip + up * (handR * 0.7 + len * 0.42), 0.3, MaterialSlot.Metal);
                    blade.Flags |= PrimFlags.Hard;
                    break;
                }
                case "axe":
                {
                    C(grip - up * (len * 0.18), 0.6, grip + up * (len * 0.78), 0.55, MaterialSlot.Accent).ShadeBias = -1;
                    // bearded blade: a flat crescent of two ellipsoids on the striking side of the haft
                    var rot = AnatomyBuilder.QuatFromBasis(Mat3.LookAlong(edge, up));
                    Vec3 at = grip + up * (len * 0.66) + edge * (len * 0.13);
                    var blade = E(at, new Vec3(len * 0.15, len * 0.2, 0.5), MaterialSlot.Metal, rot);
                    blade.Flags |= PrimFlags.Hard;
                    var beard = E(at + edge * (len * 0.04) - up * (len * 0.09), new Vec3(len * 0.1, len * 0.12, 0.45), MaterialSlot.Metal, rot);
                    beard.Flags |= PrimFlags.Hard;
                    var back = C(grip + up * (len * 0.66) - edge * (len * 0.02), 0.7, grip + up * (len * 0.66) - edge * (len * 0.1), 0.45, MaterialSlot.Metal);
                    back.Flags |= PrimFlags.Hard;
                    break;
                }
                case "hammer":
                {
                    C(grip - up * (len * 0.18), 0.6, grip + up * (len * 0.7), 0.6, MaterialSlot.Accent).ShadeBias = -1;
                    var rot = AnatomyBuilder.QuatFromBasis(Mat3.LookAlong(edge, up));
                    var headP = E(grip + up * (len * 0.78), new Vec3(len * 0.22, len * 0.12, len * 0.12), MaterialSlot.Metal, rot);
                    headP.Flags |= PrimFlags.Hard | PrimFlags.Faceted;
                    headP.FacetSeed = 77;
                    break;
                }
                default: // staff with a glowing orb
                {
                    C(grip - up * (len * 0.5), 0.55, grip + up * (len * 0.85), 0.6, MaterialSlot.Accent).ShadeBias = -1;
                    var orb = b.Ellipsoid(g, hand, grip + up * (len * 0.98), new Vec3(1, 1, 1) * Math.Max(1.3, len * 0.11), MaterialSlot.Glow, PatternDomain.None, 0, 1, null, arm.Side);
                    orb.Flags |= PrimFlags.Emissive | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    orb.Tag = "weapon.orb";
                    break;
                }
            }
        }
    }
}
