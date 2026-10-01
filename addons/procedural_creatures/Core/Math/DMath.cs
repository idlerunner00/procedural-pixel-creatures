// Procedural Pixel Creatures - deterministic math.
//
// System.Math transcendental functions call into the platform C runtime. Their last-bit results
// differ between Windows (UCRT), Linux (glibc) and macOS. A generator that must reproduce the same
// creature from the same seed on every platform therefore must not use them. This file implements
// the needed functions with only IEEE-754 basic operations (+ - * / sqrt), which are bit exact on
// every conforming platform. Kernels follow the fdlibm algorithms; the original notice is preserved below.
//
// Rule for the whole Core: never call Math.Sin/Cos/Tan/Atan/Atan2/Exp/Log/Pow/Cbrt or MathF.* in
// code that influences generated data. Use DMath instead. Math.Sqrt/Abs/Floor/Ceiling/Round/Min/Max
// are exact and allowed.

// fdlibm kernels and constants:
// Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
// Developed at SunSoft, a Sun Microsystems, Inc. business.
// Permission to use, copy, modify, and distribute this
// software is freely granted, provided that this notice
// is preserved.
// Adapted to C# with bounded argument reduction; see THIRD_PARTY_NOTICES.md.

using System;
using System.Runtime.CompilerServices;

namespace PixelCreatures.Core
{
    public static class DMath
    {
        public const double Pi = 3.14159265358979311600e+00;
        public const double TwoPi = 6.28318530717958623200e+00;
        public const double HalfPi = 1.57079632679489655800e+00;
        public const double Deg2Rad = Pi / 180.0;
        public const double Rad2Deg = 180.0 / Pi;
        public const double Epsilon = 1e-12;

        // ---------------------------------------------------------------- sin / cos

        private const double S1 = -1.66666666666666324348e-01;
        private const double S2 = 8.33333333332248946124e-03;
        private const double S3 = -1.98412698298579493134e-04;
        private const double S4 = 2.75573137070700676789e-06;
        private const double S5 = -2.50507602534068634195e-08;
        private const double S6 = 1.58969099521155010221e-10;

        private const double C1 = 4.16666666666666019037e-02;
        private const double C2 = -1.38888888888741095749e-03;
        private const double C3 = 2.48015872894767294178e-05;
        private const double C4 = -2.75573143513906633035e-07;
        private const double C5 = 2.08757232129817482790e-09;
        private const double C6 = -1.13596475577881948265e-11;

        private const double InvPio2 = 6.36619772367581382433e-01;
        private const double Pio2_1 = 1.57079632673412561417e+00;
        private const double Pio2_2 = 6.07710050630396597660e-11;
        private const double Pio2_3 = 2.02226624871116645580e-21;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double KernelSin(double x)
        {
            double z = x * x;
            double v = z * x;
            double r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
            return x + v * (S1 + z * r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double KernelCos(double x)
        {
            double z = x * x;
            double r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
            double hz = 0.5 * z;
            double w = 1.0 - hz;
            return w + (((1.0 - w) - hz) + z * r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Reduce(double x, out int quadrant)
        {
            double n = Math.Round(x * InvPio2, MidpointRounding.ToEven);
            double r = ((x - n * Pio2_1) - n * Pio2_2) - n * Pio2_3;
            quadrant = (int)((long)n & 3);
            return r;
        }

        public static double Sin(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
            if (Math.Abs(x) <= 0.7853981633974483) return KernelSin(x);
            double r = Reduce(x, out int q);
            switch (q)
            {
                case 0: return KernelSin(r);
                case 1: return KernelCos(r);
                case 2: return -KernelSin(r);
                default: return -KernelCos(r);
            }
        }

        public static double Cos(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return double.NaN;
            if (Math.Abs(x) <= 0.7853981633974483) return KernelCos(x);
            double r = Reduce(x, out int q);
            switch (q)
            {
                case 0: return KernelCos(r);
                case 1: return -KernelSin(r);
                case 2: return -KernelCos(r);
                default: return KernelSin(r);
            }
        }

        public static void SinCos(double x, out double sin, out double cos)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) { sin = double.NaN; cos = double.NaN; return; }
            if (Math.Abs(x) <= 0.7853981633974483) { sin = KernelSin(x); cos = KernelCos(x); return; }
            double r = Reduce(x, out int q);
            double s = KernelSin(r), c = KernelCos(r);
            switch (q)
            {
                case 0: sin = s; cos = c; break;
                case 1: sin = c; cos = -s; break;
                case 2: sin = -s; cos = -c; break;
                default: sin = -c; cos = s; break;
            }
        }

        public static double Tan(double x)
        {
            SinCos(x, out double s, out double c);
            return s / c;
        }

        // ---------------------------------------------------------------- atan / atan2

        private static readonly double[] AtanHi =
        {
            4.63647609000806093515e-01, 7.85398163397448278999e-01,
            9.82793723247329054082e-01, 1.57079632679489655800e+00,
        };

        private static readonly double[] AtanLo =
        {
            2.26987774529616870924e-17, 3.06161699786838301793e-17,
            1.39033110312309984516e-17, 6.12323399573676603587e-17,
        };

        private const double AT0 = 3.33333333333329318027e-01;
        private const double AT1 = -1.99999999998764832476e-01;
        private const double AT2 = 1.42857142725034663711e-01;
        private const double AT3 = -1.11111104054623557880e-01;
        private const double AT4 = 9.09088713343650656196e-02;
        private const double AT5 = -7.69187620504482999495e-02;
        private const double AT6 = 6.66107313738753120669e-02;
        private const double AT7 = -5.83357013379057348645e-02;
        private const double AT8 = 4.97687799461593236017e-02;
        private const double AT9 = -3.65315727442169155270e-02;
        private const double AT10 = 1.62858201153657823623e-02;

        public static double Atan(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            bool negative = x < 0;
            double ax = Math.Abs(x);
            if (ax >= 7.378697629483821e19) return negative ? -(AtanHi[3] + AtanLo[3]) : AtanHi[3] + AtanLo[3];
            int id;
            if (ax < 0.4375)
            {
                if (ax < 1e-29) return x;
                id = -1;
            }
            else if (ax < 1.1875)
            {
                if (ax < 0.6875) { id = 0; ax = (2.0 * ax - 1.0) / (2.0 + ax); }
                else { id = 1; ax = (ax - 1.0) / (ax + 1.0); }
            }
            else
            {
                if (ax < 2.4375) { id = 2; ax = (ax - 1.5) / (1.0 + 1.5 * ax); }
                else { id = 3; ax = -1.0 / ax; }
            }

            double xx = id < 0 ? x : ax;
            double z = xx * xx;
            double w = z * z;
            double s1 = z * (AT0 + w * (AT2 + w * (AT4 + w * (AT6 + w * (AT8 + w * AT10)))));
            double s2 = w * (AT1 + w * (AT3 + w * (AT5 + w * (AT7 + w * AT9))));
            if (id < 0) return xx - xx * (s1 + s2);
            double result = AtanHi[id] - ((xx * (s1 + s2) - AtanLo[id]) - xx);
            return negative ? -result : result;
        }

        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == 0.0)
            {
                if (y > 0) return HalfPi;
                if (y < 0) return -HalfPi;
                return 0.0;
            }
            if (y == 0.0) return x > 0 ? 0.0 : Pi;
            double ax = Math.Abs(x), ay = Math.Abs(y);
            double z = ay > ax ? HalfPi - Atan(ax / ay) : Atan(ay / ax);
            if (x > 0) return y > 0 ? z : -z;
            return y > 0 ? Pi - z : z - Pi;
        }

        public static double Asin(double x)
        {
            x = Clamp(x, -1.0, 1.0);
            return Atan2(x, Math.Sqrt(Math.Max(0.0, 1.0 - x * x)));
        }

        public static double Acos(double x)
        {
            x = Clamp(x, -1.0, 1.0);
            return Atan2(Math.Sqrt(Math.Max(0.0, 1.0 - x * x)), x);
        }

        // ---------------------------------------------------------------- exp / log / pow

        private const double Ln2Hi = 6.93147180369123816490e-01;
        private const double Ln2Lo = 1.90821492927058770002e-10;
        private const double InvLn2 = 1.44269504088896338700e+00;
        private const double P1 = 1.66666666666666019037e-01;
        private const double P2 = -2.77777777770155933842e-03;
        private const double P3 = 6.61375632143793436117e-05;
        private const double P4 = -1.65339022054652515390e-06;
        private const double P5 = 4.13813679705723846039e-08;

        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 709.0) return double.PositiveInfinity;
            if (x < -745.0) return 0.0;
            if (Math.Abs(x) < 1e-300) return 1.0;
            double kd = Math.Round(x * InvLn2, MidpointRounding.ToEven);
            int k = (int)kd;
            double hi = x - kd * Ln2Hi;
            double lo = kd * Ln2Lo;
            double r = hi - lo;
            double t = r * r;
            double c = r - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
            double y = 1.0 - ((lo - (r * c) / (2.0 - c)) - hi);
            return Math.ScaleB(y, k);
        }

        private const double Lg1 = 6.666666666666735130e-01;
        private const double Lg2 = 3.999999999940941908e-01;
        private const double Lg3 = 2.857142874366239149e-01;
        private const double Lg4 = 2.222219843214978396e-01;
        private const double Lg5 = 1.818357216161805012e-01;
        private const double Lg6 = 1.531383769920937332e-01;
        private const double Lg7 = 1.479819860511658591e-01;
        private const double Sqrt2 = 1.41421356237309514547e+00;

        public static double Log(double x)
        {
            if (double.IsNaN(x) || x < 0) return double.NaN;
            if (x == 0) return double.NegativeInfinity;
            if (double.IsPositiveInfinity(x)) return x;
            int e = 0;
            if (x < 2.2250738585072014e-308)
            {
                x *= 1.80143985094819840000e+16;
                e -= 54;
            }
            long bits = BitConverter.DoubleToInt64Bits(x);
            e += (int)((bits >> 52) & 0x7FF) - 1023;
            bits = (bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L;
            double m = BitConverter.Int64BitsToDouble(bits);
            if (m > Sqrt2)
            {
                m *= 0.5;
                e += 1;
            }
            double f = m - 1.0;
            double s = f / (2.0 + f);
            double z = s * s;
            double w = z * z;
            double t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
            double t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            double rr = t2 + t1;
            double hfsq = 0.5 * f * f;
            double de = e;
            return de * Ln2Hi - ((hfsq - (s * (hfsq + rr) + de * Ln2Lo)) - f);
        }

        public static double Log2(double x) => Log(x) * 1.44269504088896338700e+00;

        /// <summary>x^y for x &gt;= 0. Negative bases are only defined for integral exponents.</summary>
        public static double Pow(double x, double y)
        {
            if (y == 0.0) return 1.0;
            if (x == 0.0) return y > 0 ? 0.0 : double.PositiveInfinity;
            if (x == 1.0) return 1.0;
            if (x < 0)
            {
                double yi = Math.Floor(y);
                if (yi != y) return double.NaN;
                double mag = Exp(y * Log(-x));
                return ((long)yi & 1) == 0 ? mag : -mag;
            }
            if (y == 1.0) return x;
            if (y == 2.0) return x * x;
            if (y == 0.5) return Math.Sqrt(x);
            return Exp(y * Log(x));
        }

        public static double Cbrt(double x)
        {
            if (x == 0.0 || double.IsNaN(x) || double.IsInfinity(x)) return x;
            double a = Math.Abs(x);
            double y = Exp(Log(a) / 3.0);
            y -= (y * y * y - a) / (3.0 * y * y);
            return x < 0 ? -y : y;
        }

        public static double Tanh(double x)
        {
            if (x > 20) return 1.0;
            if (x < -20) return -1.0;
            double e2 = Exp(2.0 * x);
            return (e2 - 1.0) / (e2 + 1.0);
        }

        // ---------------------------------------------------------------- helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Saturate(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double InverseLerp(double a, double b, double v) => Math.Abs(b - a) < Epsilon ? 0.0 : (v - a) / (b - a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Remap(double v, double a0, double a1, double b0, double b1) => Lerp(b0, b1, Saturate(InverseLerp(a0, a1, v)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmoothStep(double e0, double e1, double x)
        {
            double t = Saturate((x - e0) / (e1 - e0));
            return t * t * (3.0 - 2.0 * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SmootherStep(double e0, double e1, double x)
        {
            double t = Saturate((x - e0) / (e1 - e0));
            return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        }

        /// <summary>Fractional part in [0,1).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Frac(double x) => x - Math.Floor(x);

        /// <summary>Wraps an angle into (-pi, pi].</summary>
        public static double WrapAngle(double a)
        {
            a = a - TwoPi * Math.Floor((a + Pi) / TwoPi);
            if (a <= -Pi) a += TwoPi;
            return a;
        }

        public static double AngleLerp(double a, double b, double t) => a + WrapAngle(b - a) * t;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Sq(double x) => x * x;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FloorToInt(double x) => (int)Math.Floor(x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundToInt(double x) => (int)Math.Floor(x + 0.5);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        /// <summary>Polynomial smooth minimum (k in the same units as a and b).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SMin(double a, double b, double k)
        {
            if (k <= 0) return Math.Min(a, b);
            double h = Saturate(0.5 + 0.5 * (b - a) / k);
            return Lerp(b, a, h) - k * h * (1.0 - h);
        }

        /// <summary>Critically damped exponential approach factor for a time step.</summary>
        public static double DampFactor(double halfLife, double dt)
        {
            if (halfLife <= 1e-6) return 1.0;
            return 1.0 - Exp(-0.69314718055994530942 * dt / halfLife);
        }

        /// <summary>Ease functions used by animation curves.</summary>
        public static double EaseInOutSine(double t) => 0.5 - 0.5 * Cos(Pi * Saturate(t));
        public static double EaseOutCubic(double t) { t = 1 - Saturate(t); return 1 - t * t * t; }
        public static double EaseOutQuad(double t) { t = 1 - Saturate(t); return 1 - t * t; }
        public static double EaseInCubic(double t) { t = Saturate(t); return t * t * t; }
        public static double EaseInOutCubic(double t)
        {
            t = Saturate(t);
            return t < 0.5 ? 4 * t * t * t : 1 - DMath.Sq(-2 * t + 2) * (-2 * t + 2) / 2;
        }
        public static double EaseOutBack(double t, double overshoot = 1.70158)
        {
            t = Saturate(t) - 1.0;
            return 1.0 + t * t * ((overshoot + 1.0) * t + overshoot);
        }
        public static double EaseInBack(double t, double overshoot = 1.70158)
        {
            t = Saturate(t);
            return t * t * ((overshoot + 1.0) * t - overshoot);
        }

        /// <summary>Smooth pulse: 0 outside [a,d], ramps up in [a,b], holds, ramps down in [c,d].</summary>
        public static double Pulse(double x, double a, double b, double c, double d)
        {
            if (x <= a || x >= d) return 0.0;
            if (x < b) return SmoothStep(a, b, x);
            if (x <= c) return 1.0;
            return 1.0 - SmoothStep(c, d, x);
        }
    }
}
