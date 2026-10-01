// Procedural Pixel Creatures - palette rules (extension point) and the default rule.
//
// A palette rule turns the colour genes of a genome into a CreaturePalette. It only ever reads
// genes of the Color stream, so colour edits cannot change anatomy or patterns. Ramps are built in
// OKLCH with fixed lightness steps and classic pixel-art hue shifting (warm lights, cool shadows).
// Colours are shared between ramps (outline, deep shadow, whites) to keep the visible colour count
// of a creature in the 10..16 range.

using System;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;

namespace PixelCreatures.Core.Palettes
{
    public interface IPaletteRule
    {
        string Id { get; }
        CreaturePalette Build(CreatureGenome genome);
    }

    public sealed class ColorGenes
    {
        public double Hue, Saturation, Value, SecondaryShift, BellyLight, MarkingDark, EyeHue, Glow, HueShift, Outline;
        /// <summary>Clothing colour (families with gear only; defaults to brown leather).</summary>
        public double ClothHue = 55, ClothSaturation = 0.4, ClothValue = 0.3;
        /// <summary>Hair colour (families with hair genes only; otherwise hair uses the marking ramp).</summary>
        public double HairHue = 50, HairSaturation = 0.35, HairValue = 0.25;
        public bool HasHair;
        /// <summary>Foliage hue for plant families (negative: the family default).</summary>
        public double LeafHue = -1;
        /// <summary>Forged metal: steel, iron, bronze or gold.</summary>
        public string Metal = "steel";
        public string Scheme = "natural";
        public string Accent = "bone";

        public static ColorGenes Read(CreatureGenome g) => new ColorGenes
        {
            Hue = g.GetOr("color.hue", 30),
            Saturation = g.GetOr("color.saturation", 0.45),
            Value = g.GetOr("color.value", 0.5),
            SecondaryShift = g.GetOr("color.secondaryShift", 0),
            BellyLight = g.GetOr("color.bellyLight", 0.5),
            MarkingDark = g.GetOr("color.markingDark", 0.5),
            EyeHue = g.GetOr("color.eyeHue", 50),
            Glow = g.GetOr("color.glow", 0),
            HueShift = g.GetOr("color.hueShift", 0.6),
            Outline = g.GetOr("color.outline", 0.55),
            ClothHue = g.GetOr("color.clothHue", 55),
            ClothSaturation = g.GetOr("color.clothSaturation", 0.4),
            ClothValue = g.GetOr("color.clothValue", 0.3),
            HairHue = g.GetOr("color.hairHue", 50),
            HairSaturation = g.GetOr("color.hairSaturation", 0.35),
            HairValue = g.GetOr("color.hairValue", 0.25),
            HasHair = g.Schema.Contains("color.hairHue"),
            LeafHue = g.GetOr("color.leafHue", -1),
            Metal = g.Schema.Contains("color.metal") ? g.GetChoice("color.metal") : "steel",
            Scheme = g.Schema.Contains("color.scheme") ? g.GetChoice("color.scheme") : "natural",
            Accent = g.Schema.Contains("color.accent") ? g.GetChoice("color.accent") : "bone",
        };
    }

    /// <summary>Default rule used by all built-in families.</summary>
    public class DefaultPaletteRule : IPaletteRule
    {
        public virtual string Id => "default";

        private const double WarmHue = 92;   // light shift target (yellow)
        private const double CoolHue = 285;  // shadow shift target (blue violet)

        public virtual CreaturePalette Build(CreatureGenome genome)
        {
            var cg = ColorGenes.Read(genome);
            var pal = new CreaturePalette { ColorKey = genome.ColorKey };
            BuildInto(pal, cg);
            return pal;
        }

        public static Oklch[] PrimaryRamp(ColorGenes cg)
        {
            double h = cg.Hue;
            // value spans charcoal (black coats) to off-white (white coats)
            double lb = DMath.Lerp(0.30, 0.86, cg.Value);
            double c = DMath.Lerp(0.012, 0.175, DMath.Pow(DMath.Saturate(cg.Saturation), 0.85));
            // very dark or very light colours cannot hold much chroma
            c *= DMath.Lerp(0.6, 1.0, DMath.Saturate(1.0 - Math.Abs(lb - 0.6) * 2.2));
            double shift = cg.HueShift;
            var baseC = new Oklch(lb, c, h);
            var shadow = new Oklch(lb - 0.13, c * 1.06 + 0.01, ColorMath.HueToward(h, CoolHue, 18 * shift));
            var deep = new Oklch(lb - 0.25, c * 0.94 + 0.014, ColorMath.HueToward(h, CoolHue, 32 * shift));
            double outL = DMath.Clamp(deep.L - 0.11 - 0.07 * cg.Outline, 0.08, 0.26);
            var outline = new Oklch(outL, Math.Min(0.075, deep.C * 0.65 + 0.012), deep.H);
            // dark coats get pushed up from the outline, light coats share the little room above the base
            var low = new[] { outline, deep, shadow, baseC };
            EnforceSteps(low, 0.085);
            double lb2 = low[3].L, room = 0.975 - lb2;
            var light = new Oklch(lb2 + Math.Min(0.11, room * 0.5), c * 0.9, ColorMath.HueToward(h, WarmHue, 16 * shift));
            var high = new Oklch(lb2 + Math.Min(0.215, room * 0.92), c * 0.6, ColorMath.HueToward(h, WarmHue, 30 * shift));
            return new[] { low[0], low[1], low[2], low[3], light, high };
        }

        /// <summary>Keeps successive ramp entries at least 'step' apart in lightness (pushes upwards).</summary>
        protected static void EnforceSteps(Oklch[] ramp, double step)
        {
            if (ramp[0].L < 0.08) ramp[0] = ramp[0].WithL(0.08);
            for (int i = 1; i < ramp.Length; i++)
            {
                if (ramp[i].L < ramp[i - 1].L + step) ramp[i] = ramp[i].WithL(Math.Min(0.985, ramp[i - 1].L + step));
            }
        }

        protected static double SecondaryHue(ColorGenes cg)
        {
            double h = cg.Hue;
            double s = cg.SecondaryShift;
            switch (cg.Scheme)
            {
                case "analogous": return ColorMath.WrapHue(h + 32 + 22 * s);
                case "complementary": return ColorMath.WrapHue(h + 180 + 25 * s);
                case "triadic": return ColorMath.WrapHue(h + 120 + 20 * s);
                case "split": return ColorMath.WrapHue(h + 150 + 20 * s);
                case "monochrome": return ColorMath.WrapHue(h + 6 * s);
                default: return ColorMath.HueToward(h, 78 + 20 * s, 42); // warm cream belly
            }
        }

        protected static double MarkingHue(ColorGenes cg)
        {
            double h = cg.Hue;
            switch (cg.Scheme)
            {
                case "analogous": return ColorMath.WrapHue(h - 28);
                case "complementary": return ColorMath.WrapHue(h + 8);
                case "triadic": return ColorMath.WrapHue(h + 240);
                case "split": return ColorMath.WrapHue(h + 210);
                case "monochrome": return h;
                default: return ColorMath.HueToward(h, 35, 22);
            }
        }

        public virtual void BuildInto(CreaturePalette pal, ColorGenes cg)
        {
            var p = PrimaryRamp(cg);
            int pOut = pal.Add(ColorMath.ToRgba(p[0]), "outline");
            int pDeep = pal.Add(ColorMath.ToRgba(p[1]), "primary deep");
            int pShadow = pal.Add(ColorMath.ToRgba(p[2]), "primary shadow");
            int pBase = pal.Add(ColorMath.ToRgba(p[3]), "primary");
            int pLight = pal.Add(ColorMath.ToRgba(p[4]), "primary light");
            int pHigh = pal.Add(ColorMath.ToRgba(p[5]), "highlight");
            pal.SetRamp(MaterialSlot.Primary, pOut, pDeep, pShadow, pBase, pLight, pHigh);

            // --- secondary (belly, light marks)
            double baseC = p[3].C;
            double h2 = SecondaryHue(cg);
            // bellies are large areas: contrasting schemes keep them soft so the body stays readable
            double c2 = cg.Scheme switch
            {
                "natural" => Math.Min(0.07, baseC * 0.55 + 0.01),
                "monochrome" => baseC * 0.6,
                "analogous" => Math.Max(0.045, baseC * 0.8),
                _ => Math.Max(0.035, baseC * 0.55),
            };
            double l2 = Math.Min(0.92, p[3].L + DMath.Lerp(0.05, 0.2, cg.BellyLight));
            var s = new[]
            {
                p[0],
                p[1],
                new Oklch(l2 - 0.105, c2 * 1.05 + 0.006, ColorMath.HueToward(h2, CoolHue, 12 * cg.HueShift)),
                new Oklch(l2, c2, h2),
                new Oklch(Math.Min(0.965, l2 + 0.07), c2 * 0.8, ColorMath.HueToward(h2, WarmHue, 14 * cg.HueShift)),
                p[5],
            };
            if (s[2].L < p[1].L + 0.06) s[2] = s[2].WithL(p[1].L + 0.06);
            int sShadow = pal.Add(ColorMath.ToRgba(s[2]), "secondary shadow");
            int sBase = pal.Add(ColorMath.ToRgba(s[3]), "secondary");
            int sLight = pal.Add(ColorMath.ToRgba(s[4]), "secondary light");
            pal.SetRamp(MaterialSlot.Secondary, pOut, pDeep, sShadow, sBase, sLight, pHigh);

            // --- marking (dark marks)
            double h3 = MarkingHue(cg);
            double l3 = Math.Max(p[1].L + 0.06, p[3].L - DMath.Lerp(0.1, 0.27, cg.MarkingDark));
            double c3 = Math.Min(0.16, baseC * 1.05 + 0.012);
            var m = new[]
            {
                new Oklch(l3 - 0.095, c3, ColorMath.HueToward(h3, CoolHue, 14 * cg.HueShift)),
                new Oklch(l3, c3, h3),
                new Oklch(l3 + 0.07, c3 * 0.9, ColorMath.HueToward(h3, WarmHue, 12 * cg.HueShift)),
            };
            if (m[0].L < p[0].L + 0.05) m[0] = m[0].WithL(p[0].L + 0.05);
            int mShadow = pal.Add(ColorMath.ToRgba(m[0]), "marking shadow");
            int mBase = pal.Add(ColorMath.ToRgba(m[1]), "marking");
            int mLight = pal.Add(ColorMath.ToRgba(m[2]), "marking light");
            pal.SetRamp(MaterialSlot.Marking, pOut, mShadow, mShadow, mBase, mLight, pLight);

            // --- accent (horns, claws, beaks, spikes)
            Oklch aDeep, aShadow, aBase, aLight;
            switch (cg.Accent)
            {
                case "dark":
                    aDeep = new Oklch(0.24, 0.02, ColorMath.HueToward(cg.Hue, 40, 30));
                    aShadow = new Oklch(0.31, 0.022, aDeep.H);
                    aBase = new Oklch(0.41, 0.025, aDeep.H);
                    aLight = new Oklch(0.54, 0.02, ColorMath.HueToward(aDeep.H, WarmHue, 10));
                    break;
                case "vivid":
                {
                    double ha = ColorMath.WrapHue(cg.Scheme == "complementary" ? cg.Hue + 170 : cg.Hue + 150);
                    aDeep = new Oklch(0.42, 0.12, ColorMath.HueToward(ha, CoolHue, 20));
                    aShadow = new Oklch(0.52, 0.14, ColorMath.HueToward(ha, CoolHue, 10));
                    aBase = new Oklch(0.64, 0.15, ha);
                    aLight = new Oklch(0.76, 0.12, ColorMath.HueToward(ha, WarmHue, 14));
                    break;
                }
                case "gold":
                    aDeep = new Oklch(0.46, 0.08, 55);
                    aShadow = new Oklch(0.58, 0.1, 65);
                    aBase = new Oklch(0.72, 0.12, 80);
                    aLight = new Oklch(0.86, 0.1, 95);
                    break;
                default:
                    aDeep = new Oklch(0.5, 0.04, 45);
                    aShadow = new Oklch(0.64, 0.045, 62);
                    aBase = new Oklch(0.79, 0.04, 80);
                    aLight = new Oklch(0.9, 0.028, 92);
                    break;
            }
            int aD = pal.Add(ColorMath.ToRgba(aDeep), "accent deep");
            int aS = pal.Add(ColorMath.ToRgba(aShadow), "accent shadow");
            int aB = pal.Add(ColorMath.ToRgba(aBase), "accent");
            int aL = pal.Add(ColorMath.ToRgba(aLight), "accent light");
            pal.SetRamp(MaterialSlot.Accent, pOut, aD, aS, aB, aL, aL);

            // --- membrane (wings, fins): a lighter, softer primary that shares most colours
            var memb = new Oklch(Math.Min(0.9, p[3].L + 0.05), p[3].C * 0.8, ColorMath.HueToward(p[3].H, h2, 25));
            int memB = pal.Add(ColorMath.ToRgba(memb), "membrane");
            pal.SetRamp(MaterialSlot.Membrane, pOut, pDeep, pShadow, memB, sBase, sLight);

            // --- white marks (piebald patches, light socks): ivory, faintly tinted towards the coat
            double wh = ColorMath.HueToward(85, cg.Hue, 25);
            int wDeep = pal.Add(ColorMath.ToRgba(new Oklch(0.62, 0.02, ColorMath.HueToward(wh, CoolHue, 40))), "white deep");
            int wShadow = pal.Add(ColorMath.ToRgba(new Oklch(0.78, 0.018, ColorMath.HueToward(wh, CoolHue, 25))), "white shadow");
            int wBase = pal.Add(ColorMath.ToRgba(new Oklch(0.9, 0.014, wh)), "white");
            int wLight = pal.Add(ColorMath.ToRgba(new Oklch(0.965, 0.01, ColorMath.HueToward(wh, WarmHue, 10))), "white light");
            pal.SetRamp(MaterialSlot.White, pOut, wDeep, wShadow, wBase, wLight, wLight);

            // --- cloth and leather gear: its own ramp with the creature's hue shifting and outline
            var cloth = PrimaryRamp(new ColorGenes
            {
                Hue = cg.ClothHue, Saturation = cg.ClothSaturation, Value = cg.ClothValue, HueShift = cg.HueShift, Outline = cg.Outline,
            });
            int cOut = pal.Add(ColorMath.ToRgba(cloth[0]), "cloth outline");
            int cDeep = pal.Add(ColorMath.ToRgba(cloth[1]), "cloth deep");
            int cShadow = pal.Add(ColorMath.ToRgba(cloth[2]), "cloth shadow");
            int cBase = pal.Add(ColorMath.ToRgba(cloth[3]), "cloth");
            int cLight = pal.Add(ColorMath.ToRgba(cloth[4]), "cloth light");
            pal.SetRamp(MaterialSlot.Cloth, cOut, cDeep, cShadow, cBase, cLight, cLight);

            // --- forged metal: low chroma ramps with a wide value range and a bright glint
            (double l, double c, double h)[] mt = cg.Metal switch
            {
                "iron" => new[] { (0.13, 0.012, 265.0), (0.21, 0.014, 262.0), (0.3, 0.016, 258.0), (0.42, 0.018, 252.0), (0.58, 0.016, 240.0), (0.8, 0.012, 95.0) },
                "bronze" => new[] { (0.2, 0.04, 40.0), (0.34, 0.07, 48.0), (0.46, 0.09, 58.0), (0.6, 0.11, 70.0), (0.74, 0.1, 82.0), (0.9, 0.06, 95.0) },
                "gold" => new[] { (0.24, 0.05, 45.0), (0.42, 0.09, 58.0), (0.56, 0.12, 70.0), (0.72, 0.14, 84.0), (0.84, 0.12, 95.0), (0.95, 0.06, 100.0) },
                _ => new[] { (0.16, 0.014, 262.0), (0.3, 0.018, 258.0), (0.43, 0.02, 252.0), (0.6, 0.018, 245.0), (0.76, 0.014, 235.0), (0.93, 0.01, 95.0) },
            };
            var mIdx = new int[6];
            for (int i = 0; i < 6; i++) mIdx[i] = pal.Add(ColorMath.ToRgba(new Oklch(mt[i].l, mt[i].c, mt[i].h)), i == 0 ? "metal outline" : "metal");
            pal.SetRamp(MaterialSlot.Metal, mIdx[0], mIdx[1], mIdx[2], mIdx[3], mIdx[4], mIdx[5]);

            // --- hair and beards: own genes where the family has them, otherwise the marking colour
            if (cg.HasHair)
            {
                var hair = PrimaryRamp(new ColorGenes
                {
                    Hue = cg.HairHue, Saturation = cg.HairSaturation, Value = cg.HairValue, HueShift = cg.HueShift, Outline = cg.Outline,
                });
                int hOut = pal.Add(ColorMath.ToRgba(hair[0]), "hair outline");
                int hDeep = pal.Add(ColorMath.ToRgba(hair[1]), "hair deep");
                int hShadow = pal.Add(ColorMath.ToRgba(hair[2]), "hair shadow");
                int hBase = pal.Add(ColorMath.ToRgba(hair[3]), "hair");
                int hLight = pal.Add(ColorMath.ToRgba(hair[4]), "hair light");
                pal.SetRamp(MaterialSlot.Hair, hOut, hDeep, hShadow, hBase, hLight, hLight);
            }
            else pal.SetRamp(MaterialSlot.Hair, pOut, mShadow, mShadow, mBase, mLight, pLight);

            // --- mouth interior
            int mouthDark = pal.Add(ColorMath.ToRgba(new Oklch(0.3, 0.09, 18)), "mouth dark");
            int mouthBase = pal.Add(ColorMath.ToRgba(new Oklch(0.45, 0.13, 20)), "mouth");
            pal.SetRamp(MaterialSlot.Mouth, pOut, mouthDark, mouthDark, mouthBase, mouthBase, mouthBase);

            // --- teeth and sclera (ivory)
            int ivoryShadow = pal.Add(ColorMath.ToRgba(new Oklch(0.76, 0.025, 75)), "ivory shadow");
            int ivory = pal.Add(ColorMath.ToRgba(new Oklch(0.93, 0.018, 90)), "ivory");
            pal.SetRamp(MaterialSlot.Teeth, pOut, ivoryShadow, ivoryShadow, ivory, ivory, ivory);

            // --- eye
            double eg = cg.Glow;
            var iris = new Oklch(DMath.Lerp(0.62, 0.8, eg), DMath.Lerp(0.13, 0.17, eg), cg.EyeHue);
            int irisD = pal.Add(ColorMath.ToRgba(new Oklch(iris.L - 0.17, iris.C, ColorMath.HueToward(iris.H, CoolHue, 12))), "iris dark");
            int irisB = pal.Add(ColorMath.ToRgba(iris), "iris");
            int irisL = pal.Add(ColorMath.ToRgba(new Oklch(Math.Min(0.95, iris.L + 0.12), iris.C * 0.8, ColorMath.HueToward(iris.H, WarmHue, 10))), "iris light");
            int shine = pal.Add(ColorMath.ToRgba(new Oklch(0.975, 0.012, 95)), "eye shine");
            pal.SetRamp(MaterialSlot.Eye, pOut, irisD, irisD, irisB, irisL, shine);

            // --- glow (emissive parts)
            double gh = cg.Glow > 0.01 ? cg.EyeHue : ColorMath.WrapHue(cg.Hue + 160);
            int glowD = pal.Add(ColorMath.ToRgba(new Oklch(DMath.Lerp(0.55, 0.68, cg.Glow), 0.14, gh)), "glow dark");
            int glowB = pal.Add(ColorMath.ToRgba(new Oklch(DMath.Lerp(0.72, 0.84, cg.Glow), 0.16, gh)), "glow");
            int glowL = pal.Add(ColorMath.ToRgba(new Oklch(DMath.Lerp(0.86, 0.96, cg.Glow), 0.09, ColorMath.HueToward(gh, WarmHue, 12))), "glow light");
            pal.SetRamp(MaterialSlot.Glow, pOut, glowD, glowD, glowB, glowL, glowL);

            pal.FlashColor = ColorMath.ToRgba(new Oklch(0.97, 0.02, 90));
        }
    }

    /// <summary>Builds the surface spec (materials, pattern) from the Pattern stream genes.</summary>
    public static class SurfaceBuilder
    {
        public static MaterialStyle MaterialById(string id) => id switch
        {
            "fur" => MaterialStyles.Fur,
            "scales" => MaterialStyles.Scales,
            "chitin" => MaterialStyles.Chitin,
            "stone" => MaterialStyles.Stone,
            "slime" => MaterialStyles.Slime,
            "feather" => MaterialStyles.Feather,
            "bark" => MaterialStyles.Bark,
            "leaf" => MaterialStyles.Leaf,
            _ => MaterialStyles.Hide,
        };

        public static PatternKind PatternById(string id) => id switch
        {
            "stripes" => PatternKind.Stripes,
            "spots" => PatternKind.Spots,
            "rosettes" => PatternKind.Rosettes,
            "saddle" => PatternKind.Saddle,
            "dorsal" => PatternKind.DorsalStripe,
            "mottled" => PatternKind.Mottled,
            "bands" => PatternKind.Bands,
            "blotches" => PatternKind.Blotches,
            _ => PatternKind.None,
        };

        public static SurfaceSpec Build(CreatureGenome g)
        {
            string mat = g.Schema.Contains("surface.material") ? g.GetChoice("surface.material") : "hide";
            string pat = g.Schema.Contains("pattern.type") ? g.GetChoice("pattern.type") : "none";
            var style = MaterialById(mat);
            var membrane = mat == "chitin" ? MaterialStyles.Glossy : MaterialStyles.Membrane;
            return new SurfaceSpec(
                style,
                PatternById(pat),
                DMath.Lerp(1.8, 5.5, g.GetOr("pattern.size", 0.5)),
                g.GetOr("pattern.density", 0.5),
                g.GetOr("pattern.dark", 1) >= 0.5,
                g.GetOr("pattern.countershade", 0.5),
                g.GetOr("pattern.tips", 0),
                g.GetOr("pattern.muzzle", 0),
                g.GetOr("surface.texture", 0.5),
                g.PatternSeed,
                g.PatternKey,
                null,
                membrane)
            {
                Socks = g.GetOr("pattern.socks", 0),
                SocksLight = g.GetOr("pattern.socksLight", 1) >= 0.5,
                Mask = g.GetOr("pattern.mask", 0),
                Piebald = g.GetOr("pattern.piebald", 0),
                WingPattern = !g.Schema.Contains("wings.pattern") ? WingPatternKind.None : g.GetChoice("wings.pattern") switch
                {
                    "eyespot" => WingPatternKind.Eyespot,
                    "border" => WingPatternKind.Border,
                    "bands" => WingPatternKind.Bands,
                    "veins" => WingPatternKind.Veins,
                    _ => WingPatternKind.None,
                },
            };
        }
    }
}
