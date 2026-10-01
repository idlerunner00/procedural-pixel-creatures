// Procedural Pixel Creature Workshop - scalable map of the test area: ponds with sandy shores, winding
// paths, grass with light meadows and darker patches, props (trees, bushes, rocks) and a minimap image.
// Deterministic for (size, seed). Generation only touches managed buffers - no engine calls - so it can
// run on a worker thread; the test area turns the buffers into images and textures.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    /// <summary>Map size presets of the test area (screen pixels; the ground is already foreshortened).</summary>
    public readonly struct MapSize
    {
        public readonly string Name;
        public readonly int Width, Height;

        public MapSize(string name, int width, int height)
        {
            Name = name;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"{Name} ({Width}×{Height})";

        public static readonly MapSize[] All =
        {
            new MapSize("Small", 640, 400),
            new MapSize("Medium", 960, 600),
            new MapSize("Large", 1280, 800),
            new MapSize("Very large", 1920, 1200),
            new MapSize("Huge", 2560, 1600),
        };
    }

    public enum PropKind : byte
    {
        Tree,
        Bush,
        Rock,
    }

    public struct PropSpot
    {
        public int X, Y;
        public PropKind Kind;
        public int Variant;
    }

    public sealed class PondSpec
    {
        public double Cx, Cy, Rx, Ry;
        /// <summary>Seed of the pond's outline (lobes and bays), so ponds of one map differ in shape.</summary>
        public ulong ShapeSeed;
        /// <summary>Area covered by the animated glint overlay (screen pixels).</summary>
        public Rect2I Bounds;
        /// <summary>Four RGBA frames of water glints over <see cref="Bounds"/>.</summary>
        public byte[][] GlintFrames = Array.Empty<byte[]>();
    }

    public sealed class TestMap
    {
        public const byte Grass = 0, Path = 1, Sand = 2, Water = 3;

        public int Width, Height;
        public ulong Seed;
        /// <summary>Terrain per screen pixel: <see cref="Grass"/>, <see cref="Path"/>, <see cref="Sand"/>, <see cref="Water"/>.</summary>
        public byte[] Terrain = Array.Empty<byte>();
        public byte[] GroundRgba = Array.Empty<byte>();
        public float[] Elevation = Array.Empty<float>();
        public float[] WaterDepth = Array.Empty<float>();
        public byte[] Clearance = Array.Empty<byte>();
        public byte[] WaterSurfaceRgba = Array.Empty<byte>();
        public readonly List<ReliefFace> Cliffs = new List<ReliefFace>();
        public readonly List<PondSpec> Ponds = new List<PondSpec>();
        public readonly List<PropSpot> Props = new List<PropSpot>();
        public int MiniWidth, MiniHeight;
        public byte[] MiniRgba = Array.Empty<byte>();
        public double GenerateMs;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool IsWater(int x, int y) => InBounds(x, y) && Terrain[y * Width + x] == Water;

        public float HeightAt(Vector2 p)
        {
            int x = Math.Clamp((int)p.X, 0, Width - 2), y = Math.Clamp((int)p.Y, 0, Height - 2);
            float fx = Math.Clamp(p.X - x, 0, 1), fy = Math.Clamp(p.Y - y, 0, 1);
            int i = y * Width + x;
            return Mathf.Lerp(Mathf.Lerp(Elevation[i], Elevation[i + 1], fx), Mathf.Lerp(Elevation[i + Width], Elevation[i + Width + 1], fx), fy);
        }

        public Vector2 Project(Vector2 p) => p - new Vector2(0, HeightAt(p));

        /// <summary>Checks the whole footprint, including banks, cliffs and solid prop bases.</summary>
        public bool CanOccupy(Vector2 p, bool aquatic, bool flying, float radius = 5)
        {
            if (p.X < radius + 2 || p.Y < radius + 2 || p.X >= Width - radius - 2 || p.Y >= Height - radius - 2) return false;
            if (flying) return true;
            int i = (int)p.Y * Width + (int)p.X;
            if (IsWater((int)p.X, (int)p.Y) != aquatic || Clearance[i] < radius) return false;
            return true;
        }

        public bool CanTravel(Vector2 from, Vector2 to, bool aquatic, bool flying, float radius = 5)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(from.DistanceTo(to) / 2));
            var previous = from;
            for (int k = 1; k <= steps; k++)
            {
                var p = from.Lerp(to, k / (float)steps);
                if (!CanOccupy(p, aquatic, flying, radius)) return false;
                if (!aquatic && !flying && Math.Abs(HeightAt(p) - HeightAt(previous)) > 1.6f) return false;
                previous = p;
            }
            return true;
        }

        /// <summary>True when the area around (x, y) is grass only (no water, sand, path or map edge).</summary>
        public bool IsGrassArea(int x, int y, int margin)
        {
            for (int dy = -margin / 2; dy <= margin / 2; dy += 2)
                for (int dx = -margin; dx <= margin; dx += 3)
                {
                    int xx = x + dx, yy = y + dy;
                    if (!InBounds(xx, yy) || Terrain[yy * Width + xx] != Grass) return false;
                }
            return true;
        }

        /// <summary>Share of the map covered by one terrain kind (0..1).</summary>
        public double Fraction(byte kind)
        {
            long n = 0;
            foreach (var t in Terrain) if (t == kind) n++;
            return Terrain.Length == 0 ? 0 : n / (double)Terrain.Length;
        }
    }

    public static partial class EnvironmentArt
    {
        public const int TreeVariants = 10, BushVariants = 8, RockVariants = 8;
        public const int MinimapMaxWidth = 240, MinimapMaxHeight = 150;
        /// <summary>Screen pixels² per prop: 46 props on the small 640×400 map, same density on larger maps.</summary>
        private const double PropArea = 5600;

        /// <summary>
        /// Generates a test area map of width x height screen pixels. Runs the per-pixel work in parallel
        /// (row by row, results identical to a sequential run) and can be cancelled.
        /// </summary>
        public static TestMap GenerateMap(int width, int height, ulong seed, CancellationToken cancel = default)
        {
            var sw = Stopwatch.StartNew();
            int w = Math.Max(64, width), h = Math.Max(64, height);
            var map = new TestMap { Width = w, Height = h, Seed = seed, Terrain = new byte[w * h], GroundRgba = new byte[w * h * 4] };
            PlacePonds(map, new Rng(seed ^ 0x504F4E44UL));
            var rowPaths = HorizontalPaths(w, h, seed);
            var columnPaths = VerticalPaths(w, h, seed);
            var options = new ParallelOptions { CancellationToken = cancel };
            Parallel.For(0, h, options, y => GroundRow(map, rowPaths, columnPaths, y));
            cancel.ThrowIfCancellationRequested();
            Tufts(map);
            Parallel.For(1, h - 1, options, y => ShoreRow(map, y));
            BuildRelief(map, cancel);
            foreach (var pond in map.Ponds) pond.GlintFrames = Glints(map, pond);
            cancel.ThrowIfCancellationRequested();
            PlaceProps(map, new Rng(seed ^ 0x50524F50UL));
            BuildClearance(map);
            BuildMinimap(map);
            map.GenerateMs = sw.Elapsed.TotalMilliseconds;
            return map;
        }

        // ------------------------------------------------------------------ layout

        private static void PlacePonds(TestMap map, Rng rng)
        {
            int w = map.Width, h = map.Height;
            int count = (int)Math.Round(w * (double)h / 300000.0);
            if (count <= 1)
            {
                // the small map keeps its familiar single pond in the lower right
                map.Ponds.Add(new PondSpec { Cx = w * 0.69, Cy = h * 0.61, Rx = w * 0.235, Ry = h * 0.285 });
            }
            else
            {
                for (int attempts = 0; map.Ponds.Count < count && attempts < count * 80; attempts++)
                {
                    // from three ponds on, the first one is a lake
                    double s = map.Ponds.Count == 0 ? (count >= 3 ? rng.Range(1.6, 1.9) : 1.55) : rng.Range(1.2, 1.4);
                    double rx = 108 * s, ry = 80 * s;
                    if (2 * rx + 60 >= w || 2 * ry + 70 >= h) continue;
                    double cx = rng.Range(rx + 30, w - rx - 30), cy = rng.Range(ry + 44, h - ry - 26);
                    bool free = true;
                    foreach (var p in map.Ponds)
                    {
                        // keep shores apart (ellipse metric with padding for wobble, sand and a walkway)
                        double dx = (cx - p.Cx) / (rx + p.Rx + 56), dy = (cy - p.Cy) / (ry + p.Ry + 44);
                        if (dx * dx + dy * dy < 1) { free = false; break; }
                    }
                    if (free) map.Ponds.Add(new PondSpec { Cx = cx, Cy = cy, Rx = rx, Ry = ry });
                }
            }
            for (int i = 0; i < map.Ponds.Count; i++)
            {
                var p = map.Ponds[i];
                p.ShapeSeed = map.Seed + 31 + (ulong)i * 977;
                // lobes reach ~1.27 radii, shore jitter and sand add up to 13.5 px: 1.6 radii cover both
                int x0 = Math.Max(0, (int)(p.Cx - p.Rx * 1.6)), x1 = Math.Min(w, (int)Math.Ceiling(p.Cx + p.Rx * 1.6));
                int y0 = Math.Max(0, (int)(p.Cy - p.Ry * 1.6)), y1 = Math.Min(h, (int)Math.Ceiling(p.Cy + p.Ry * 1.6));
                p.Bounds = new Rect2I(x0, y0, x1 - x0, y1 - y0);
            }
        }

        /// <summary>Centre lines (screen y per column) of the paths crossing the map from west to east.</summary>
        private static double[][] HorizontalPaths(int w, int h, ulong seed)
        {
            int n = Math.Max(1, (int)Math.Round(h / 400.0));
            double band = h / (double)n;
            var paths = new double[n][];
            for (int k = 0; k < n; k++)
            {
                // a single path sits at 0.42 h with the original curve of the small map
                double baseY = band * k + band * 0.42, amp = Math.Min(band * 0.14, 64), phase = 0.6 + k * 1.7;
                ulong s = seed + 3 + (ulong)k * 101;
                var c = new double[w];
                for (int x = 0; x < w; x++) c[x] = baseY + DMath.Sin(x * 0.018 + phase) * amp + PatternNoise.Perlin2(s, x * 0.03, 0.5) * 8;
                paths[k] = c;
            }
            return paths;
        }

        /// <summary>Centre lines (screen x per row) of the paths crossing from north to south (wider maps only).</summary>
        private static double[][] VerticalPaths(int w, int h, ulong seed)
        {
            int n = w / 900;
            double band = n > 0 ? w / (double)n : w;
            var paths = new double[n][];
            for (int k = 0; k < n; k++)
            {
                double baseX = band * k + band * 0.5, phase = 1.3 + k * 2.1;
                ulong s = seed + 7 + (ulong)k * 101;
                var c = new double[h];
                for (int y = 0; y < h; y++) c[y] = baseX + DMath.Sin(y * 0.02 + phase) * 48 + PatternNoise.Perlin2(s, y * 0.03, 0.5) * 10;
                paths[k] = c;
            }
            return paths;
        }

        /// <summary>Large-scale grass variation in [-1, 1]: positive = light meadow, negative = darker patch.</summary>
        private static double Meadow(ulong seed, int x, int y) => PatternNoise.Perlin2(seed + 60, x / 150.0, y / 95.0);

        private static double MeadowLift(double m) => Math.Clamp((m - 0.2) * 2.5, 0, 1);

        private static double MeadowShade(double m) => Math.Clamp((-m - 0.3) * 2.5, 0, 1);

        private static void GroundRow(TestMap map, double[][] rowPaths, double[][] columnPaths, int y)
        {
            int w = map.Width;
            ulong seed = map.Seed;
            var ponds = map.Ponds;
            for (int x = 0; x < w; x++)
            {
                // ponds: ellipses with lobes and bays relative to their size, a little shore jitter in
                // pixels and a sandy rim of about 10 px (thinner at the top and bottom, as seen from above)
                byte t = TestMap.Grass;
                double jitter = double.NaN;
                foreach (var p in ponds)
                {
                    var b = p.Bounds;
                    if (x < b.Position.X || y < b.Position.Y || x >= b.End.X || y >= b.End.Y) continue;
                    if (double.IsNaN(jitter)) jitter = PatternNoise.Perlin2(seed, x * 0.06, y * 0.09) * 3.5;
                    double dx = (x - p.Cx) / p.Rx, dy = (y - p.Cy) / p.Ry;
                    double v = Math.Sqrt(dx * dx + dy * dy) + PatternNoise.Perlin2(p.ShapeSeed, dx * 1.7, dy * 1.7) * 0.3 + jitter / p.Rx;
                    if (v < 1.0) { t = TestMap.Water; break; }
                    if (v < 1.0 + 10.0 / p.Rx) t = TestMap.Sand;
                }
                if (t == TestMap.Grass && OnPath(seed, rowPaths, columnPaths, x, y)) t = TestMap.Path;
                map.Terrain[y * w + x] = t;

                double n = PatternNoise.Fbm2(seed + 9, x / 14.0, y / 10.0, 2) * 0.5 + 0.5;
                Color c;
                switch (t)
                {
                    case TestMap.Water: c = n < 0.45 ? Water0 : Water1; break;
                    case TestMap.Sand: c = n < 0.4 ? Sand0 : (n > 0.72 ? Sand2 : Sand1); break;
                    case TestMap.Path: c = n < 0.4 ? Dirt0 : (n > 0.7 ? Dirt2 : Dirt1); break;
                    default:
                        double m = Meadow(seed, x, y), lift = MeadowLift(m), shade = MeadowShade(m);
                        double lo = 0.38 - lift * 0.14 + shade * 0.2, hi = 0.63 - lift * 0.2 + shade * 0.15;
                        c = n < lo ? Grass0 : (n > hi ? (lift > 0.6 && n > hi + 0.24 ? Grass3 : Grass2) : Grass1);
                        break;
                }
                int i = (y * w + x) * 4;
                map.GroundRgba[i] = (byte)c.R8;
                map.GroundRgba[i + 1] = (byte)c.G8;
                map.GroundRgba[i + 2] = (byte)c.B8;
                map.GroundRgba[i + 3] = 255;
            }
        }

        private static bool OnPath(ulong seed, double[][] rowPaths, double[][] columnPaths, int x, int y)
        {
            foreach (var c in rowPaths)
            {
                double d = Math.Abs(y - c[x]);
                if (d < 10 && d - 7 - PatternNoise.Perlin2(seed + 4, x * 0.1, y * 0.1) * 2.5 < 0) return true;
            }
            foreach (var c in columnPaths)
            {
                double d = Math.Abs(x - c[y]);
                if (d < 16 && d - 12 - PatternNoise.Perlin2(seed + 5, x * 0.1, y * 0.1) * 3 < 0) return true;
            }
            return false;
        }

        /// <summary>Grass tufts and flowers (more flowers on meadows). Sequential: neighbouring cells may touch the same pixel.</summary>
        private static void Tufts(TestMap map)
        {
            const int cell = 5;
            ulong seed = map.Seed;
            for (int cy = 0; cy < map.Height / cell; cy++)
            {
                for (int cx = 0; cx < map.Width / cell; cx++)
                {
                    double r = Hash(seed + 11, cx, cy);
                    int x = cx * cell + (int)(Hash(seed + 12, cx, cy) * (cell - 2)) + 1;
                    int y = cy * cell + (int)(Hash(seed + 13, cx, cy) * (cell - 2)) + 1;
                    double flowerRate = 0.012 + MeadowLift(Meadow(seed, x, y)) * 0.07;
                    if (r < 0.34)
                    {
                        // small tuft: dark base, light tip (lit from the upper left)
                        GrassPixel(map, x, y + 1, Grass0);
                        GrassPixel(map, x - 1, y + 1, Grass0);
                        GrassPixel(map, x, y, Grass2);
                        if (r < 0.14) GrassPixel(map, x - 1, y, Grass3);
                    }
                    else if (r < 0.34 + flowerRate)
                    {
                        var fc = Flowers[(int)(Hash(seed + 14, cx, cy) * Flowers.Length) % Flowers.Length];
                        GrassPixel(map, x, y, fc);
                        GrassPixel(map, x, y + 1, Grass0);
                        if (r < 0.34 + flowerRate * 0.4) GrassPixel(map, x + 1, y, fc.Darkened(0.2f));
                    }
                }
            }
        }

        private static void GrassPixel(TestMap map, int x, int y, Color c)
        {
            if (!map.InBounds(x, y) || map.Terrain[y * map.Width + x] != TestMap.Grass) return;
            int i = (y * map.Width + x) * 4;
            map.GroundRgba[i] = (byte)c.R8;
            map.GroundRgba[i + 1] = (byte)c.G8;
            map.GroundRgba[i + 2] = (byte)c.B8;
        }

        /// <summary>Foam on water next to land, a dark edge below the bank (depth cue). Reads terrain only.</summary>
        private static void ShoreRow(TestMap map, int y)
        {
            int w = map.Width;
            var t = map.Terrain;
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * w + x;
                if (t[i] != TestMap.Water) continue;
                if (t[i - 1] == TestMap.Water && t[i + 1] == TestMap.Water && t[i - w] == TestMap.Water && t[i + w] == TestMap.Water) continue;
                var c = t[i - w] != TestMap.Water ? Water0 : Foam;
                map.GroundRgba[i * 4] = (byte)c.R8;
                map.GroundRgba[i * 4 + 1] = (byte)c.G8;
                map.GroundRgba[i * 4 + 2] = (byte)c.B8;
            }
        }

        /// <summary>Four frames of animated glints for one pond (cells in map coordinates, so frames line up).</summary>
        private static byte[][] Glints(TestMap map, PondSpec pond)
        {
            const int cell = 9;
            var b = pond.Bounds;
            int bw = b.Size.X, bh = b.Size.Y;
            ulong seed = map.Seed;
            var frames = new byte[4][];
            for (int k = 0; k < 4; k++)
            {
                var ov = new byte[bw * bh * 4];
                for (int cy = b.Position.Y / cell; cy <= b.End.Y / cell; cy++)
                {
                    for (int cx = b.Position.X / cell; cx <= b.End.X / cell; cx++)
                    {
                        double r = Hash(seed + 40, cx, cy);
                        if (r > 0.16) continue;
                        int ph = (int)(Hash(seed + 41, cx, cy) * 4);
                        int age = (k - ph + 4) % 4;
                        if (age > 1) continue;
                        int x = cx * cell + (int)(Hash(seed + 42, cx, cy) * 5), y = cy * cell + (int)(Hash(seed + 43, cx, cy) * 6);
                        if (!map.IsWater(x, y) || !map.IsWater(x + 3, y) || !map.IsWater(x, y - 2) || !map.IsWater(x, y + 2)) continue;
                        int len = age == 0 ? 2 : 3;
                        for (int j = 0; j < len; j++) PutClip(ov, bw, bh, x + j + age - b.Position.X, y - b.Position.Y, age == 0 ? Foam : Water2);
                    }
                }
                frames[k] = ov;
            }
            return frames;
        }

        /// <summary>Trees, bushes and rocks on open grass at a constant density, never in water or on paths.</summary>
        private static void PlaceProps(TestMap map, Rng rng)
        {
            int w = map.Width, h = map.Height;
            int target = (int)Math.Round(w * (double)h / PropArea);
            const int grid = 12;
            int gw = w / grid + 1, gh = h / grid + 1;
            var occupied = new bool[gw * gh];
            for (int attempts = 0; map.Props.Count < target && attempts < target * 25; attempts++)
            {
                int x = rng.Range(8, w - 9), y = rng.Range(20, h - 5);
                if (!map.IsGrassArea(x, y, 10)) continue;
                int gx = x / grid, gy = y / grid;
                bool free = true;
                for (int oy = Math.Max(0, gy - 1); oy <= Math.Min(gh - 1, gy + 1) && free; oy++)
                    for (int ox = Math.Max(0, gx - 1); ox <= Math.Min(gw - 1, gx + 1); ox++)
                        if (occupied[oy * gw + ox]) { free = false; break; }
                if (!free) continue;
                occupied[gy * gw + gx] = true;
                double r = rng.NextDouble();
                var kind = r < 0.45 ? PropKind.Tree : (r < 0.75 ? PropKind.Bush : PropKind.Rock);
                int variants = kind == PropKind.Tree ? TreeVariants : (kind == PropKind.Bush ? BushVariants : RockVariants);
                map.Props.Add(new PropSpot { X = x, Y = y, Kind = kind, Variant = rng.NextInt(variants) });
            }
        }

        /// <summary>Downscaled overview (box filter on a 3×3 sample grid per minimap pixel), trees as dark dots.</summary>
        private static void BuildMinimap(TestMap map)
        {
            double scale = Math.Min(MinimapMaxWidth / (double)map.Width, MinimapMaxHeight / (double)map.Height);
            int mw = Math.Max(1, (int)Math.Round(map.Width * scale)), mh = Math.Max(1, (int)Math.Round(map.Height * scale));
            var buf = new byte[mw * mh * 4];
            for (int my = 0; my < mh; my++)
            {
                for (int mx = 0; mx < mw; mx++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                        {
                            int x = Math.Min(map.Width - 1, (int)((mx + (sx + 0.5) / 3.0) / scale));
                            int y = Math.Min(map.Height - 1, (int)((my + (sy + 0.5) / 3.0) / scale));
                            int i = (y * map.Width + x) * 4;
                            r += map.GroundRgba[i];
                            g += map.GroundRgba[i + 1];
                            b += map.GroundRgba[i + 2];
                        }
                    int o = (my * mw + mx) * 4;
                    buf[o] = (byte)(r / 9);
                    buf[o + 1] = (byte)(g / 9);
                    buf[o + 2] = (byte)(b / 9);
                    buf[o + 3] = 255;
                }
            }
            var treeDot = Color.Color8(36, 70, 48);
            foreach (var p in map.Props)
            {
                if (p.Kind != PropKind.Tree) continue;
                PutClip(buf, mw, mh, (int)(p.X * scale), (int)(p.Y * scale) - 1, treeDot);
            }
            map.MiniWidth = mw;
            map.MiniHeight = mh;
            map.MiniRgba = buf;
        }
    }
}
