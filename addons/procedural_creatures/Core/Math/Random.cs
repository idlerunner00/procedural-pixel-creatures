// Procedural Pixel Creatures - explicit, documented random number generation.
//
// Algorithms (fixed forever for a given generator version):
//   * Seed derivation: SplitMix64 finalizer over (parent seed XOR FNV-1a-64(label)).
//   * Streams: PCG32 (XSH-RR, 64-bit state, 64-bit increment derived from the seed).
//   * Bounded integers: Lemire's nearly divisionless method with rejection (unbiased).
//   * Doubles: 53 random bits.
// Never use System.Random, string.GetHashCode or time based seeds for reproducible content.

// PCG32 transition and output are adapted from pcg-c-basic.
// Copyright 2014 Melissa O'Neill <oneill@pcg-random.org>.
// Licensed under the Apache License, Version 2.0.
// The complete license and attribution are included in THIRD_PARTY_NOTICES.md.
// Changes: C# port, project-specific seeding and stream derivation.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace PixelCreatures.Core
{
    public static class StableHash
    {
        public const ulong FnvOffset = 14695981039346656037UL;
        public const ulong FnvPrime = 1099511628211UL;

        /// <summary>FNV-1a 64-bit over the UTF-8 bytes of the string.</summary>
        public static ulong Fnv1a64(string text)
        {
            ulong h = FnvOffset;
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= FnvPrime;
            }
            return h;
        }

        public static ulong Fnv1a64(ReadOnlySpan<byte> bytes, ulong h = FnvOffset)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                h ^= bytes[i];
                h *= FnvPrime;
            }
            return h;
        }

        /// <summary>SplitMix64 finalizer. Bijective 64-bit mixing function.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Mix64(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Combine(ulong a, ulong b) => Mix64(a ^ (Mix64(b) + 0x632BE59BD9B4E019UL + (a << 6) + (a >> 2)));

        /// <summary>Derives an independent child seed for a named purpose.</summary>
        public static ulong Derive(ulong seed, string label) => Mix64(seed ^ Fnv1a64(label));

        public static ulong Derive(ulong seed, string label, int index) => Mix64(Derive(seed, label) + (ulong)(uint)index * 0x9E3779B97F4A7C15UL);

        /// <summary>Integer lattice hash used by noise functions.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash3(ulong seed, int x, int y, int z)
        {
            ulong h = seed;
            h ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
            h ^= (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
            h ^= (ulong)(uint)z * 0x165667B19E3779F9UL;
            return (uint)(Mix64(h) >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Hash01(ulong seed, int x, int y = 0, int z = 0) => Hash3(seed, x, y, z) * (1.0 / 4294967296.0);

        /// <summary>Stable hash of a string suitable for user entered seed text.</summary>
        public static ulong SeedFromText(string text)
        {
            string t = (text ?? string.Empty).Trim();
            if (t.Length == 0) return 0;
            if (ulong.TryParse(t, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out ulong numeric))
                return numeric;
            return Mix64(Fnv1a64(t));
        }
    }

    /// <summary>PCG32 random stream (O'Neill, XSH-RR variant).</summary>
    public sealed class Rng
    {
        private ulong _state;
        private readonly ulong _inc;

        public ulong Seed { get; }

        public Rng(ulong seed)
        {
            Seed = seed;
            _inc = (StableHash.Mix64(seed ^ 0xDA3E39CB94B95BDBUL) << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state += StableHash.Mix64(seed);
            NextUInt();
        }

        public static Rng Derive(ulong seed, string label) => new Rng(StableHash.Derive(seed, label));
        public static Rng Derive(ulong seed, string label, int index) => new Rng(StableHash.Derive(seed, label, index));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public ulong NextULong() => ((ulong)NextUInt() << 32) | NextUInt();

        /// <summary>Uniform double in [0,1) with 53 bits of randomness.</summary>
        public double NextDouble()
        {
            ulong a = NextUInt() >> 5; // 27 bits
            ulong b = NextUInt() >> 6; // 26 bits
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }

        /// <summary>Unbiased integer in [0, n).</summary>
        public int NextInt(int n)
        {
            if (n <= 1) return 0;
            uint bound = (uint)n;
            ulong m = (ulong)NextUInt() * bound;
            uint l = (uint)m;
            if (l < bound)
            {
                uint t = (uint)(-(int)bound) % bound;
                while (l < t)
                {
                    m = (ulong)NextUInt() * bound;
                    l = (uint)m;
                }
            }
            return (int)(m >> 32);
        }

        /// <summary>Integer in [lo, hi] inclusive.</summary>
        public int Range(int lo, int hi) => hi <= lo ? lo : lo + NextInt(hi - lo + 1);

        public double Range(double lo, double hi) => lo + (hi - lo) * NextDouble();

        public bool Chance(double p) => NextDouble() < p;

        public double Sign() => (NextUInt() & 1) == 0 ? -1.0 : 1.0;

        /// <summary>Standard normal variate (Box-Muller with deterministic math).</summary>
        public double Gaussian()
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            return Math.Sqrt(-2.0 * DMath.Log(u1)) * DMath.Cos(DMath.TwoPi * u2);
        }

        /// <summary>Normal variate truncated to mean +- limit*sigma.</summary>
        public double Gaussian(double mean, double sigma, double limit = 2.5)
        {
            double g = Gaussian();
            if (g > limit) g = limit;
            if (g < -limit) g = -limit;
            return mean + g * sigma;
        }

        /// <summary>Picks an index according to non-negative weights.</summary>
        public int Weighted(IReadOnlyList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return 0;
            double r = NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0, weights[i]);
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }

        public T Pick<T>(IReadOnlyList<T> items) => items[NextInt(items.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Deterministic gradient noise for patterns (Perlin style, lattice hashed with StableHash).</summary>
    public static class PatternNoise
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Grad2(ulong seed, int ix, int iy, double fx, double fy)
        {
            uint h = StableHash.Hash3(seed, ix, iy, 0) & 7;
            switch (h)
            {
                case 0: return fx + fy;
                case 1: return -fx + fy;
                case 2: return fx - fy;
                case 3: return -fx - fy;
                case 4: return fx;
                case 5: return -fx;
                case 6: return fy;
                default: return -fy;
            }
        }

        /// <summary>2D gradient noise in roughly [-1, 1].</summary>
        public static double Perlin2(ulong seed, double x, double y)
        {
            int ix = DMath.FloorToInt(x), iy = DMath.FloorToInt(y);
            double fx = x - ix, fy = y - iy;
            double u = Fade(fx), v = Fade(fy);
            double n00 = Grad2(seed, ix, iy, fx, fy);
            double n10 = Grad2(seed, ix + 1, iy, fx - 1, fy);
            double n01 = Grad2(seed, ix, iy + 1, fx, fy - 1);
            double n11 = Grad2(seed, ix + 1, iy + 1, fx - 1, fy - 1);
            double nx0 = DMath.Lerp(n00, n10, u);
            double nx1 = DMath.Lerp(n01, n11, u);
            return DMath.Lerp(nx0, nx1, v) * 0.9;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Grad3(ulong seed, int ix, int iy, int iz, double fx, double fy, double fz)
        {
            uint h = StableHash.Hash3(seed, ix, iy, iz) % 12;
            switch (h)
            {
                case 0: return fx + fy;
                case 1: return -fx + fy;
                case 2: return fx - fy;
                case 3: return -fx - fy;
                case 4: return fx + fz;
                case 5: return -fx + fz;
                case 6: return fx - fz;
                case 7: return -fx - fz;
                case 8: return fy + fz;
                case 9: return -fy + fz;
                case 10: return fy - fz;
                default: return -fy - fz;
            }
        }

        public static double Perlin3(ulong seed, double x, double y, double z)
        {
            int ix = DMath.FloorToInt(x), iy = DMath.FloorToInt(y), iz = DMath.FloorToInt(z);
            double fx = x - ix, fy = y - iy, fz = z - iz;
            double u = Fade(fx), v = Fade(fy), w = Fade(fz);
            double a = DMath.Lerp(Grad3(seed, ix, iy, iz, fx, fy, fz), Grad3(seed, ix + 1, iy, iz, fx - 1, fy, fz), u);
            double b = DMath.Lerp(Grad3(seed, ix, iy + 1, iz, fx, fy - 1, fz), Grad3(seed, ix + 1, iy + 1, iz, fx - 1, fy - 1, fz), u);
            double c = DMath.Lerp(Grad3(seed, ix, iy, iz + 1, fx, fy, fz - 1), Grad3(seed, ix + 1, iy, iz + 1, fx - 1, fy, fz - 1), u);
            double d = DMath.Lerp(Grad3(seed, ix, iy + 1, iz + 1, fx, fy - 1, fz - 1), Grad3(seed, ix + 1, iy + 1, iz + 1, fx - 1, fy - 1, fz - 1), u);
            return DMath.Lerp(DMath.Lerp(a, b, v), DMath.Lerp(c, d, v), w) * 0.9;
        }

        /// <summary>Fractal sum of 2D noise.</summary>
        public static double Fbm2(ulong seed, double x, double y, int octaves, double lacunarity = 2.0, double gain = 0.5)
        {
            double sum = 0, amp = 1, norm = 0;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Perlin2(seed + (ulong)i * 0x9E3779B97F4A7C15UL, x, y);
                norm += amp;
                amp *= gain;
                x *= lacunarity;
                y *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Cellular (Worley) distance to the nearest jittered feature point in 2D. Returns F1 and the id of the cell.</summary>
        public static double Cellular2(ulong seed, double x, double y, double jitter, out uint cellId, out double f2)
        {
            int ix = DMath.FloorToInt(x), iy = DMath.FloorToInt(y);
            double best = double.MaxValue, second = double.MaxValue;
            cellId = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cy = iy + dy;
                    uint h = StableHash.Hash3(seed, cx, cy, 7);
                    double px = cx + 0.5 + (((h & 0xFFFF) / 65535.0) - 0.5) * jitter;
                    double py = cy + 0.5 + ((((h >> 16) & 0xFFFF) / 65535.0) - 0.5) * jitter;
                    double d = DMath.Sq(px - x) + DMath.Sq(py - y);
                    if (d < best)
                    {
                        second = best;
                        best = d;
                        cellId = h;
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }
            f2 = Math.Sqrt(second);
            return Math.Sqrt(best);
        }
    }
}
