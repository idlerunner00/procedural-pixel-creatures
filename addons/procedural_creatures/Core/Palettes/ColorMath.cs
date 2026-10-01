// Procedural Pixel Creatures - deterministic colour math (sRGB <-> linear <-> OKLab/OKLCH).
// OKLab (Björn Ottosson) gives perceptually even lightness steps, which is what shading ramps need.

using System;
using System.Globalization;

namespace PixelCreatures.Core.Palettes
{
    public readonly struct Rgba : IEquatable<Rgba>
    {
        public readonly byte R, G, B, A;

        public Rgba(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

        public static readonly Rgba Transparent = new Rgba(0, 0, 0, 0);

        public static Rgba FromHex(string hex)
        {
            string h = hex.TrimStart('#');
            byte r = byte.Parse(h.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(h.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(h.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte a = h.Length >= 8 ? byte.Parse(h.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)255;
            return new Rgba(r, g, b, a);
        }

        public string ToHex() => A == 255 ? $"#{R:x2}{G:x2}{B:x2}" : $"#{R:x2}{G:x2}{B:x2}{A:x2}";

        public Rgba WithAlpha(byte a) => new Rgba(R, G, B, a);

        public static Rgba Lerp(Rgba a, Rgba b, double t)
        {
            t = DMath.Saturate(t);
            return new Rgba(
                (byte)DMath.Clamp(Math.Round(a.R + (b.R - a.R) * t), 0, 255),
                (byte)DMath.Clamp(Math.Round(a.G + (b.G - a.G) * t), 0, 255),
                (byte)DMath.Clamp(Math.Round(a.B + (b.B - a.B) * t), 0, 255),
                (byte)DMath.Clamp(Math.Round(a.A + (b.A - a.A) * t), 0, 255));
        }

        public uint Packed => (uint)R | ((uint)G << 8) | ((uint)B << 16) | ((uint)A << 24);

        public bool Equals(Rgba o) => R == o.R && G == o.G && B == o.B && A == o.A;
        public override bool Equals(object? obj) => obj is Rgba c && Equals(c);
        public override int GetHashCode() => (int)Packed;
        public override string ToString() => ToHex();
    }

    /// <summary>OKLCH colour: L 0..1, C 0..~0.37, H degrees.</summary>
    public readonly struct Oklch
    {
        public readonly double L, C, H;
        public Oklch(double l, double c, double h) { L = l; C = c; H = h; }
        public Oklch WithL(double l) => new Oklch(l, C, H);
        public Oklch WithC(double c) => new Oklch(L, c, H);
        public Oklch WithH(double h) => new Oklch(L, C, h);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "oklch({0:0.000} {1:0.000} {2:0.0})", L, C, H);
    }

    public static class ColorMath
    {
        public static double SrgbToLinear(double c) => c <= 0.04045 ? c / 12.92 : DMath.Pow((c + 0.055) / 1.055, 2.4);
        public static double LinearToSrgb(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * DMath.Pow(c, 1.0 / 2.4) - 0.055;

        public static void LinearToOklab(double r, double g, double b, out double L, out double A, out double B)
        {
            double l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
            double m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
            double s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;
            double l_ = DMath.Cbrt(l), m_ = DMath.Cbrt(m), s_ = DMath.Cbrt(s);
            L = 0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_;
            A = 1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_;
            B = 0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_;
        }

        public static void OklabToLinear(double L, double A, double B, out double r, out double g, out double b)
        {
            double l_ = L + 0.3963377774 * A + 0.2158037573 * B;
            double m_ = L - 0.1055613458 * A - 0.0638541728 * B;
            double s_ = L - 0.0894841775 * A - 1.2914855480 * B;
            double l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
            r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        }

        public static Oklch ToOklch(Rgba c)
        {
            double r = SrgbToLinear(c.R / 255.0), g = SrgbToLinear(c.G / 255.0), b = SrgbToLinear(c.B / 255.0);
            LinearToOklab(r, g, b, out double L, out double A, out double B);
            double C = Math.Sqrt(A * A + B * B);
            double H = DMath.Atan2(B, A) * DMath.Rad2Deg;
            if (H < 0) H += 360;
            return new Oklch(L, C, H);
        }

        private static bool InGamut(double L, double C, double H, out double r, out double g, out double b)
        {
            DMath.SinCos(H * DMath.Deg2Rad, out double sh, out double ch);
            OklabToLinear(L, C * ch, C * sh, out r, out g, out b);
            const double eps = 1e-6;
            return r >= -eps && r <= 1 + eps && g >= -eps && g <= 1 + eps && b >= -eps && b <= 1 + eps;
        }

        /// <summary>Converts OKLCH to sRGB. Out of gamut colours keep lightness and hue and lose chroma (binary search).</summary>
        public static Rgba ToRgba(Oklch c, byte alpha = 255)
        {
            double L = DMath.Clamp(c.L, 0, 1);
            double C = Math.Max(0, c.C);
            double H = c.H;
            double r, g, b;
            if (!InGamut(L, C, H, out r, out g, out b))
            {
                double lo = 0, hi = C;
                for (int i = 0; i < 24; i++)
                {
                    double mid = 0.5 * (lo + hi);
                    if (InGamut(L, mid, H, out _, out _, out _)) lo = mid; else hi = mid;
                }
                InGamut(L, lo, H, out r, out g, out b);
            }
            return new Rgba(ToByte(LinearToSrgb(DMath.Saturate(r))), ToByte(LinearToSrgb(DMath.Saturate(g))), ToByte(LinearToSrgb(DMath.Saturate(b))), alpha);
        }

        private static byte ToByte(double v) => (byte)DMath.Clamp(Math.Round(v * 255.0, MidpointRounding.AwayFromZero), 0, 255);

        /// <summary>Hue difference in degrees in (-180, 180].</summary>
        public static double HueDelta(double from, double to)
        {
            double d = to - from;
            while (d > 180) d -= 360;
            while (d <= -180) d += 360;
            return d;
        }

        public static double WrapHue(double h)
        {
            h %= 360.0;
            if (h < 0) h += 360.0;
            return h;
        }

        /// <summary>Moves hue a towards hue b by at most 'amount' degrees along the shortest arc.</summary>
        public static double HueToward(double a, double b, double amount)
        {
            double d = HueDelta(a, b);
            if (Math.Abs(d) <= amount) return WrapHue(b);
            return WrapHue(a + Math.Sign(d) * amount);
        }

        /// <summary>Perceptual distance between two colours (Euclidean in OKLab).</summary>
        public static double Distance(Rgba x, Rgba y)
        {
            LinearToOklab(SrgbToLinear(x.R / 255.0), SrgbToLinear(x.G / 255.0), SrgbToLinear(x.B / 255.0), out double l1, out double a1, out double b1);
            LinearToOklab(SrgbToLinear(y.R / 255.0), SrgbToLinear(y.G / 255.0), SrgbToLinear(y.B / 255.0), out double l2, out double a2, out double b2);
            return Math.Sqrt(DMath.Sq(l1 - l2) + DMath.Sq(a1 - a2) + DMath.Sq(b1 - b2));
        }

        public static double Luminance(Rgba c)
        {
            return 0.2126 * SrgbToLinear(c.R / 255.0) + 0.7152 * SrgbToLinear(c.G / 255.0) + 0.0722 * SrgbToLinear(c.B / 255.0);
        }
    }
}
