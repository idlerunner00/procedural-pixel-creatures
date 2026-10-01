// Procedural Pixel Creatures - genes shared by every family (colour, pattern, motion) and their sampling.
//
// All families use the same colour/pattern/motion genes. Families only bias the sampling through
// FamilyStyleHints, which keeps palettes and motion personalities comparable across body plans.

using System;
using System.Collections.Generic;

namespace PixelCreatures.Core.Genetics
{
    /// <summary>
    /// A coat / skin colour family in gene units (OKLCH hue in degrees, saturation and value genes 0..1).
    /// Sampling picks a coat first and then the three colour genes inside its ranges, so natural archetypes
    /// get believable colours (black, cream, red, grey ...) instead of independent mid-range dice.
    /// </summary>
    public readonly struct CoatPrior
    {
        public readonly string Id;
        public readonly double Weight, Hue, HueSpread, SatMin, SatMax, ValueMin, ValueMax;

        public CoatPrior(string id, double weight, double hue, double hueSpread, double satMin, double satMax, double valueMin, double valueMax)
        {
            Id = id; Weight = weight; Hue = hue; HueSpread = hueSpread; SatMin = satMin; SatMax = satMax; ValueMin = valueMin; ValueMax = valueMax;
        }

        public CoatPrior WithWeight(double w) => new CoatPrior(Id, w, Hue, HueSpread, SatMin, SatMax, ValueMin, ValueMax);
    }

    /// <summary>Library of coat colours (natural fur, scales, feathers, chitin) and fantasy colours.</summary>
    public static class Coats
    {
        // mammal fur
        public static CoatPrior Black(double w) => new CoatPrior("black", w, 265, 20, 0.02, 0.12, 0.0, 0.08);
        public static CoatPrior Chocolate(double w) => new CoatPrior("chocolate", w, 50, 6, 0.25, 0.42, 0.08, 0.22);
        public static CoatPrior Brown(double w) => new CoatPrior("brown", w, 56, 7, 0.28, 0.5, 0.22, 0.42);
        public static CoatPrior Red(double w) => new CoatPrior("red", w, 44, 6, 0.6, 0.85, 0.36, 0.52);
        public static CoatPrior Ginger(double w) => new CoatPrior("ginger", w, 58, 6, 0.55, 0.8, 0.48, 0.64);
        public static CoatPrior Golden(double w) => new CoatPrior("golden", w, 76, 6, 0.38, 0.58, 0.55, 0.72);
        public static CoatPrior Tan(double w) => new CoatPrior("tan", w, 72, 8, 0.22, 0.4, 0.46, 0.66);
        public static CoatPrior Cream(double w) => new CoatPrior("cream", w, 84, 8, 0.06, 0.2, 0.8, 0.96);
        public static CoatPrior Grey(double w) => new CoatPrior("grey", w, 250, 25, 0.02, 0.1, 0.3, 0.62);
        public static CoatPrior Slate(double w) => new CoatPrior("slate", w, 245, 15, 0.08, 0.2, 0.2, 0.42);
        // scales, skin, chitin, feathers
        public static CoatPrior Olive(double w) => new CoatPrior("olive", w, 108, 10, 0.3, 0.55, 0.28, 0.5);
        public static CoatPrior Green(double w) => new CoatPrior("green", w, 140, 14, 0.45, 0.8, 0.35, 0.62);
        public static CoatPrior Sand(double w) => new CoatPrior("sand", w, 78, 8, 0.25, 0.45, 0.58, 0.78);
        public static CoatPrior Rust(double w) => new CoatPrior("rust", w, 38, 8, 0.5, 0.75, 0.3, 0.48);
        public static CoatPrior Yellow(double w) => new CoatPrior("yellow", w, 95, 6, 0.65, 0.9, 0.65, 0.82);
        public static CoatPrior Crimson(double w) => new CoatPrior("crimson", w, 25, 8, 0.65, 0.9, 0.38, 0.55);
        public static CoatPrior Blue(double w) => new CoatPrior("blue", w, 250, 15, 0.5, 0.85, 0.38, 0.62);
        public static CoatPrior Teal(double w) => new CoatPrior("teal", w, 195, 12, 0.45, 0.8, 0.4, 0.64);
        public static CoatPrior Violet(double w) => new CoatPrior("violet", w, 305, 15, 0.4, 0.75, 0.36, 0.6);
        public static CoatPrior Pink(double w) => new CoatPrior("pink", w, 5, 10, 0.3, 0.55, 0.62, 0.8);
        public static CoatPrior Silver(double w) => new CoatPrior("silver", w, 235, 20, 0.02, 0.1, 0.62, 0.82);
        public static CoatPrior Albino(double w) => new CoatPrior("albino", w, 20, 15, 0.04, 0.14, 0.85, 0.96);
        /// <summary>Saturated colour of any hue (fantasy creatures).</summary>
        public static CoatPrior Vivid(double w) => new CoatPrior("vivid", w, 0, 360, 0.55, 0.9, 0.4, 0.72);
        /// <summary>Muted colour of any hue (fantasy creatures).</summary>
        public static CoatPrior Muted(double w) => new CoatPrior("muted", w, 0, 360, 0.18, 0.42, 0.3, 0.62);
    }

    public sealed class FamilyStyleHints
    {
        /// <summary>
        /// Coat colours with weights. When set, they replace Hues/Saturation/Value: a coat is picked first and
        /// the colour genes are sampled inside its ranges.
        /// </summary>
        public CoatPrior[] Coats = Array.Empty<CoatPrior>();
        /// <summary>Chances of the independent marks: socks/stockings, dark face mask, piebald patches.</summary>
        public double SocksChance = 0.1, MaskChance = 0.05, PiebaldChance = 0.05;
        /// <summary>Preferred hue centres (degrees) with weights. Empty = uniform.</summary>
        public (double hue, double spread, double weight)[] Hues = Array.Empty<(double, double, double)>();
        public double SaturationMin = 0.25, SaturationMax = 0.85;
        public double ValueMin = 0.25, ValueMax = 0.8;
        /// <summary>Weights per surface material option (see SharedGenes.Materials).</summary>
        public Dictionary<string, double> Materials = new Dictionary<string, double> { ["hide"] = 1 };
        /// <summary>Weights per pattern option (see SharedGenes.Patterns).</summary>
        public Dictionary<string, double> Patterns = new Dictionary<string, double> { ["none"] = 1, ["stripes"] = 1, ["spots"] = 1 };
        /// <summary>Weights per accent style.</summary>
        public Dictionary<string, double> Accents = new Dictionary<string, double> { ["bone"] = 2, ["dark"] = 1, ["vivid"] = 1 };
        /// <summary>Weights per colour scheme (see SharedGenes.Schemes). Natural bodies favour natural schemes.</summary>
        public Dictionary<string, double> Schemes = new Dictionary<string, double>
        {
            ["natural"] = 3.8, ["analogous"] = 2.0, ["complementary"] = 1.4, ["split"] = 1.0, ["triadic"] = 0.8, ["monochrome"] = 1.0,
        };
        public double GlowChance = 0.1;
        public double CountershadeMin = 0.2, CountershadeMax = 0.8;
        public double EnergyMin = 0.2, EnergyMax = 0.9;
        public double WeightMin = 0.1, WeightMax = 0.9;
    }

    public static class SharedGenes
    {
        public static readonly (string id, string label)[] Schemes =
        {
            ("natural", "Natural"), ("analogous", "Analogous"), ("complementary", "Complementary"),
            ("triadic", "Triadic"), ("split", "Split complementary"), ("monochrome", "Monochrome"),
        };

        public static readonly (string id, string label)[] Accents =
        {
            ("bone", "Bone / ivory"), ("dark", "Dark"), ("vivid", "Vivid"), ("gold", "Gold"),
        };

        public static readonly (string id, string label)[] Materials =
        {
            ("fur", "Fur"), ("hide", "Hide"), ("scales", "Scales"), ("chitin", "Chitin"), ("stone", "Stone"),
            ("slime", "Slime"), ("feather", "Feather"), ("bark", "Bark"), ("leaf", "Leaf"),
        };

        public static readonly (string id, string label)[] Patterns =
        {
            ("none", "None"), ("stripes", "Stripes"), ("spots", "Spots"), ("rosettes", "Rosettes"),
            ("saddle", "Saddle"), ("dorsal", "Dorsal"), ("mottled", "Mottled"), ("bands", "Bands"),
            ("blotches", "Blotches"),
        };

        public static void AddColorGenes(GeneSchemaBuilder b)
        {
            b.Group("palette", "Color", GeneStream.Color)
             .Hue("color.hue", "Base hue", 30, "Hue of the main color in OKLCH.")
             .Float("color.saturation", "Saturation", 0, 1, 0.45, "Chroma of the main color.", 0.14)
             .Float("color.value", "Brightness", 0, 1, 0.5, "Brightness of the main color.", 0.12)
             .Choice("color.scheme", "Color scheme", Schemes, "natural", "Relationship of belly, marking and accent colors to the main color.", 0.15)
             .Float("color.secondaryShift", "Secondary hue shift", -1, 1, 0, "Fine adjustment of the secondary hue within the color scheme.", 0.2, unit: DisplayUnit.Plain)
             .Float("color.bellyLight", "Belly brightness", 0, 1, 0.5, "Brightness of the belly and light markings.", 0.12)
             .Float("color.markingDark", "Marking contrast", 0, 1, 0.5, "Darkness and contrast of dark markings.", 0.12)
             .Choice("color.accent", "Horn & claw material", Accents, "bone", "Material of horns, claws, spines and beaks.", 0.12)
             .Hue("color.eyeHue", "Color eye hue", 50, "Iris hue.")
             .Float("color.glow", "Color glow", 0, 1, 0, "Intensity of luminous parts and glowing eyes.", 0.12)
             .Float("color.hueShift", "Shading hue shift", 0, 1, 0.6, "Warmer highlights and cooler shadows in classic pixel-art color ramps.", 0.1)
             .Float("color.outline", "Outline darkness", 0, 1, 0.55, "Darkness of the outer outline.", 0.1);
        }

        public static void AddPatternGenes(GeneSchemaBuilder b, string defaultMaterial)
        {
            b.Group("pattern", "Pattern & material", GeneStream.Pattern)
             .Choice("surface.material", "Surface material", Materials, defaultMaterial, "Surface character: fur, scales, chitin, stone and more.", 0.08)
             .Float("surface.texture", "Surface texture", 0, 1, 0.5, "Microtexture strength: scale rows, fur strands and plates.", 0.12)
             .Choice("pattern.type", "Pattern type", Patterns, "none", "Type of body-anchored markings.", 0.12)
             .Float("pattern.size", "Pattern size", 0, 1, 0.5, "Size of the markings.", 0.12)
             .Float("pattern.density", "Pattern density", 0, 1, 0.5, "Area covered by markings.", 0.12)
             .Bool("pattern.dark", "Pattern dark", true, "Dark markings when enabled, light markings when disabled.", 0.08)
             .Float("pattern.countershade", "Countershading", 0, 1, 0.5, "Extent of the lighter underside.", 0.12)
             .Float("pattern.tips", "Pattern tips", 0, 1, 0, "Contrasting tail, ear and foot tips.", 0.12)
             .Float("pattern.muzzle", "Pattern muzzle", 0, 1, 0, "Lighter muzzle area.", 0.12)
             .Float("pattern.socks", "Pattern socks", 0, 1, 0, "Contrasting lower legs: socks or stockings.", 0.12)
             .Bool("pattern.socksLight", "Pattern socks light", true, "White socks when enabled, dark stockings when disabled.", 0.08)
             .Float("pattern.mask", "Pattern mask", 0, 1, 0, "Dark mask across the eyes.", 0.12)
             .Float("pattern.piebald", "Piebald patches", 0, 1, 0, "Large white patches with distinct edges.", 0.12);
        }

        public static void AddMotionGenes(GeneSchemaBuilder b)
        {
            b.Group("motion", "Motion", GeneStream.Motion)
             .Float("motion.energy", "Energy", 0, 1, 0.5, "Liveliness: idle movement speed and frequency of small gestures.", 0.12)
             .Float("motion.weight", "Weight", 0, 1, 0.5, "Perceived weight: compression, anticipation and follow-through.", 0.12)
             .Float("motion.stiffness", "Motion stiffness", 0, 1, 0.5, "Spring stiffness of tails, ears and soft appendages.", 0.12)
             .Float("motion.bounce", "Bounce", 0, 1, 0.5, "Vertical movement and squash-and-stretch strength.", 0.12)
             .Float("motion.aggression", "Motion aggression", 0, 1, 0.5, "Intensity of actions and facial expression.", 0.12)
             .Float("motion.curiosity", "Motion curiosity", 0, 1, 0.5, "Frequency of looking around, ear flicks and sniffing.", 0.12)
             .Float("motion.speed", "Motion speed", 0, 1, 0.5, "Relative maximum speed.", 0.1);
        }

        private static string PickWeighted(Rng rng, Dictionary<string, double> weights, (string id, string label)[] options)
        {
            var keys = new List<string>();
            var w = new List<double>();
            foreach (var o in options)
            {
                if (weights.TryGetValue(o.id, out double v) && v > 0)
                {
                    keys.Add(o.id);
                    w.Add(v);
                }
            }
            if (keys.Count == 0) return options[0].id;
            return keys[rng.Weighted(w)];
        }

        private static int IndexOf((string id, string label)[] options, string id)
        {
            for (int i = 0; i < options.Length; i++) if (options[i].id == id) return i;
            return 0;
        }

        public static void SampleColor(GeneSchema schema, double[] values, Rng rng, FamilyStyleHints hints)
        {
            double hue, sat, val;
            if (hints.Coats.Length > 0)
            {
                var weights = new List<double>();
                foreach (var c in hints.Coats) weights.Add(c.Weight);
                var coat = hints.Coats[rng.Weighted(weights)];
                hue = coat.HueSpread >= 180 ? rng.Range(0.0, 360.0) : ColorWrap(coat.Hue + rng.Gaussian(0, coat.HueSpread, 2.0));
                sat = rng.Range(coat.SatMin, coat.SatMax);
                val = rng.Range(coat.ValueMin, coat.ValueMax);
            }
            else
            {
                if (hints.Hues.Length > 0)
                {
                    var weights = new List<double>();
                    foreach (var h in hints.Hues) weights.Add(h.weight);
                    var pick = hints.Hues[rng.Weighted(weights)];
                    hue = ColorWrap(pick.hue + rng.Gaussian(0, pick.spread, 2.0));
                }
                else hue = rng.Range(0.0, 360.0);
                sat = rng.Range(hints.SaturationMin, hints.SaturationMax);
                val = rng.Range(hints.ValueMin, hints.ValueMax);
            }
            Set(schema, values, "color.hue", hue);
            Set(schema, values, "color.saturation", sat);
            Set(schema, values, "color.value", val);
            string scheme = PickWeighted(rng, hints.Schemes, Schemes);
            Set(schema, values, "color.scheme", IndexOf(Schemes, scheme));
            Set(schema, values, "color.secondaryShift", rng.Range(-0.6, 0.6));
            Set(schema, values, "color.bellyLight", rng.Range(0.25, 0.85));
            Set(schema, values, "color.markingDark", rng.Range(0.3, 0.85));
            Set(schema, values, "color.accent", IndexOf(Accents, PickWeighted(rng, hints.Accents, Accents)));
            double eye = rng.NextDouble();
            // OKLCH hues: amber/gold 65-95, red-orange 30-50, green-teal 140-200, violet 285-320
            double eyeHue = eye < 0.45 ? rng.Range(65.0, 95.0) : eye < 0.65 ? rng.Range(30.0, 50.0) : eye < 0.85 ? rng.Range(140.0, 200.0) : rng.Range(285.0, 320.0);
            Set(schema, values, "color.eyeHue", eyeHue);
            Set(schema, values, "color.glow", rng.Chance(hints.GlowChance) ? rng.Range(0.5, 1.0) : 0.0);
            Set(schema, values, "color.hueShift", rng.Range(0.4, 0.9));
            Set(schema, values, "color.outline", rng.Range(0.35, 0.8));
        }

        public static void SamplePattern(GeneSchema schema, double[] values, Rng rng, FamilyStyleHints hints)
        {
            Set(schema, values, "surface.material", IndexOf(Materials, PickWeighted(rng, hints.Materials, Materials)));
            Set(schema, values, "surface.texture", rng.Range(0.2, 0.8));
            Set(schema, values, "pattern.type", IndexOf(Patterns, PickWeighted(rng, hints.Patterns, Patterns)));
            Set(schema, values, "pattern.size", rng.Range(0.2, 0.8));
            Set(schema, values, "pattern.density", rng.Range(0.3, 0.8));
            Set(schema, values, "pattern.dark", rng.Chance(0.72) ? 1 : 0);
            Set(schema, values, "pattern.countershade", rng.Range(hints.CountershadeMin, hints.CountershadeMax));
            Set(schema, values, "pattern.tips", rng.Chance(0.3) ? rng.Range(0.3, 0.9) : 0.0);
            Set(schema, values, "pattern.muzzle", rng.Chance(0.3) ? rng.Range(0.3, 0.9) : 0.0);
            Set(schema, values, "pattern.socks", rng.Chance(hints.SocksChance) ? rng.Range(0.3, 1.0) : 0.0);
            Set(schema, values, "pattern.socksLight", rng.Chance(0.7) ? 1 : 0);
            Set(schema, values, "pattern.mask", rng.Chance(hints.MaskChance) ? rng.Range(0.35, 1.0) : 0.0);
            Set(schema, values, "pattern.piebald", rng.Chance(hints.PiebaldChance) ? rng.Range(0.25, 0.85) : 0.0);
        }

        public static void SampleMotion(GeneSchema schema, double[] values, Rng rng, FamilyStyleHints hints)
        {
            Set(schema, values, "motion.energy", rng.Range(hints.EnergyMin, hints.EnergyMax));
            Set(schema, values, "motion.weight", rng.Range(hints.WeightMin, hints.WeightMax));
            Set(schema, values, "motion.stiffness", rng.Range(0.2, 0.8));
            Set(schema, values, "motion.bounce", rng.Range(0.2, 0.8));
            Set(schema, values, "motion.aggression", rng.Range(0.2, 0.9));
            Set(schema, values, "motion.curiosity", rng.Range(0.2, 0.9));
            Set(schema, values, "motion.speed", rng.Range(0.3, 0.8));
        }

        private static double ColorWrap(double h)
        {
            h %= 360.0;
            if (h < 0) h += 360.0;
            return h;
        }

        public static void Set(GeneSchema schema, double[] values, string id, double v)
        {
            int i = schema.IndexOf(id);
            if (i >= 0) values[i] = schema.Genes[i].Quantize(v);
        }
    }
}
