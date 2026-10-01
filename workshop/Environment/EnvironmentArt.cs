// Procedural Pixel Creature Workshop - procedural environment art (ground, water, trees, rocks, bushes).
// Same logical pixel density as the creatures, lit from the upper left, hand-tuned colour ramps.
// Everything is generated from seeds; there are no image assets.

using System;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    public enum GroundKind
    {
        Grass,
        Meadow,
        Dirt,
        Sand,
        Water,
        Stone,
    }

    public static partial class EnvironmentArt
    {
        // ------------------------------------------------------------------ palette
        public static readonly Color Grass0 = Color.Color8(58, 100, 58);
        public static readonly Color Grass1 = Color.Color8(80, 132, 66);
        public static readonly Color Grass2 = Color.Color8(101, 154, 74);
        public static readonly Color Grass3 = Color.Color8(132, 180, 90);
        public static readonly Color Dirt0 = Color.Color8(104, 78, 60);
        public static readonly Color Dirt1 = Color.Color8(132, 102, 74);
        public static readonly Color Dirt2 = Color.Color8(158, 126, 90);
        public static readonly Color Sand0 = Color.Color8(178, 154, 106);
        public static readonly Color Sand1 = Color.Color8(206, 184, 130);
        public static readonly Color Sand2 = Color.Color8(226, 208, 156);
        public static readonly Color Water0 = Color.Color8(40, 76, 122);
        public static readonly Color Water1 = Color.Color8(52, 100, 150);
        public static readonly Color Water2 = Color.Color8(78, 138, 180);
        public static readonly Color Foam = Color.Color8(196, 228, 236);
        public static readonly Color Stone0 = Color.Color8(78, 80, 92);
        public static readonly Color Stone1 = Color.Color8(108, 110, 120);
        public static readonly Color Stone2 = Color.Color8(142, 142, 148);
        public static readonly Color Stone3 = Color.Color8(178, 176, 176);
        public static readonly Color[] Flowers = { Color.Color8(238, 206, 88), Color.Color8(238, 234, 222), Color.Color8(222, 128, 158), Color.Color8(126, 152, 224) };

        private static double Hash(ulong seed, int x, int y, int z = 0) => StableHash.Hash01(seed, x, y, z);

        /// <summary>Periodic value noise (tileable with the given period in cells).</summary>
        private static double PeriodicNoise(ulong seed, double x, double y, int period)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double fx = x - ix, fy = y - iy;
            double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            double a = Hash(seed, Wrap(ix), Wrap(iy)), b = Hash(seed, Wrap(ix + 1), Wrap(iy));
            double c = Hash(seed, Wrap(ix), Wrap(iy + 1)), d = Hash(seed, Wrap(ix + 1), Wrap(iy + 1));
            return DMath.Lerp(DMath.Lerp(a, b, sx), DMath.Lerp(c, d, sx), sy);
        }

        private static void Put(byte[] buf, int w, int h, int x, int y, Color c)
        {
            x = ((x % w) + w) % w;
            y = ((y % h) + h) % h;
            int i = (y * w + x) * 4;
            buf[i] = (byte)(c.R8); buf[i + 1] = (byte)(c.G8); buf[i + 2] = (byte)(c.B8); buf[i + 3] = (byte)(c.A8);
        }

        private static void PutClip(byte[] buf, int w, int h, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int i = (y * w + x) * 4;
            if (c.A8 < 255)
            {
                float a = c.A;
                buf[i] = (byte)(buf[i] * (1 - a) + c.R8 * a);
                buf[i + 1] = (byte)(buf[i + 1] * (1 - a) + c.G8 * a);
                buf[i + 2] = (byte)(buf[i + 2] * (1 - a) + c.B8 * a);
                buf[i + 3] = (byte)Math.Max(buf[i + 3], c.A8);
                return;
            }
            buf[i] = (byte)c.R8; buf[i + 1] = (byte)c.G8; buf[i + 2] = (byte)c.B8; buf[i + 3] = 255;
        }

        private static Image ToImage(byte[] buf, int w, int h) => Image.CreateFromData(w, h, false, Image.Format.Rgba8, buf);

        // ------------------------------------------------------------------ tileable ground

        /// <summary>Seamless ground tile (size must be a multiple of 16).</summary>
        public static Image GroundTile(GroundKind kind, int size, ulong seed)
        {
            var buf = new byte[size * size * 4];
            int period = size / 16;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double n = PeriodicNoise(seed, x / 16.0, y / 16.0, period) * 0.65 + PeriodicNoise(seed + 7, x / 8.0, y / 8.0, period * 2) * 0.35;
                    Color c;
                    switch (kind)
                    {
                        case GroundKind.Dirt: c = n < 0.38 ? Dirt0 : (n > 0.66 ? Dirt2 : Dirt1); break;
                        case GroundKind.Sand: c = n < 0.4 ? Sand0 : (n > 0.7 ? Sand2 : Sand1); break;
                        case GroundKind.Water:
                            c = Color.Color8(22, 65, 91).Lerp(Color.Color8(69, 139, 145), (float)(Math.Floor(n * 7) / 7));
                            double caustic = Math.Sin(x * Math.PI / 16 + Math.Sin(y * Math.PI / 16) * 1.5) + Math.Cos(y * Math.PI / 8);
                            if (caustic > 1.78) c = c.Lightened(0.08f);
                            break;
                        case GroundKind.Stone: c = n < 0.4 ? Stone1 : (n > 0.7 ? Stone3 : Stone2); break;
                        default: c = n < 0.36 ? Grass0 : (n > 0.64 ? Grass2 : Grass1); break;
                    }
                    Put(buf, size, size, x, y, c);
                }
            }
            if (kind == GroundKind.Grass || kind == GroundKind.Meadow) GrassTufts(buf, size, size, seed, kind == GroundKind.Meadow ? 0.05 : 0.012, true);
            if (kind == GroundKind.Dirt) Pebbles(buf, size, size, seed, Dirt0, Dirt2, 0.05, true);
            if (kind == GroundKind.Sand) Pebbles(buf, size, size, seed, Sand0, Sand2, 0.03, true);
            if (kind == GroundKind.Water) WaterGlints(buf, size, size, seed, 0, true);
            return ToImage(buf, size, size);
        }

        private static void GrassTufts(byte[] buf, int w, int h, ulong seed, double flowerRate, bool wrap)
        {
            int cell = 5;
            for (int cy = 0; cy < h / cell; cy++)
            {
                for (int cx = 0; cx < w / cell; cx++)
                {
                    double r = Hash(seed + 11, cx, cy);
                    int x = cx * cell + (int)(Hash(seed + 12, cx, cy) * (cell - 2)) + 1;
                    int y = cy * cell + (int)(Hash(seed + 13, cx, cy) * (cell - 2)) + 1;
                    if (r < 0.34)
                    {
                        // small tuft: dark base, light tip (lit from the upper left)
                        Set(buf, w, h, x, y + 1, Grass0, wrap);
                        Set(buf, w, h, x - 1, y + 1, Grass0, wrap);
                        Set(buf, w, h, x, y, Grass2, wrap);
                        if (r < 0.14) Set(buf, w, h, x - 1, y, Grass3, wrap);
                    }
                    else if (r < 0.34 + flowerRate)
                    {
                        var fc = Flowers[(int)(Hash(seed + 14, cx, cy) * Flowers.Length) % Flowers.Length];
                        Set(buf, w, h, x, y, fc, wrap);
                        Set(buf, w, h, x, y + 1, Grass0, wrap);
                        if (r < 0.34 + flowerRate * 0.4) Set(buf, w, h, x + 1, y, fc.Darkened(0.2f), wrap);
                    }
                }
            }
        }

        private static void Set(byte[] buf, int w, int h, int x, int y, Color c, bool wrap)
        {
            if (wrap) Put(buf, w, h, x, y, c);
            else PutClip(buf, w, h, x, y, c);
        }

        private static void Pebbles(byte[] buf, int w, int h, ulong seed, Color dark, Color light, double rate, bool wrap)
        {
            int cell = 4;
            for (int cy = 0; cy < h / cell; cy++)
                for (int cx = 0; cx < w / cell; cx++)
                {
                    if (Hash(seed + 21, cx, cy) > rate) continue;
                    int x = cx * cell + 1, y = cy * cell + 1;
                    Set(buf, w, h, x, y, light, wrap);
                    Set(buf, w, h, x + 1, y + 1, dark, wrap);
                }
        }

        private static void WaterGlints(byte[] buf, int w, int h, ulong seed, int phase, bool wrap)
        {
            int cell = 7;
            for (int cy = 0; cy < h / cell; cy++)
                for (int cx = 0; cx < w / cell; cx++)
                {
                    double r = Hash(seed + 31 + (ulong)phase, cx, cy);
                    if (r > 0.18) continue;
                    int x = cx * cell + (int)(Hash(seed + 32, cx, cy) * 4), y = cy * cell + (int)(Hash(seed + 33, cx, cy) * 5);
                    int len = r < 0.07 ? 3 : 2;
                    for (int i = 0; i < len; i++) Set(buf, w, h, x + i, y, Water2, wrap);
                    if (r < 0.03) Set(buf, w, h, x + 1, y, Foam, wrap);
                }
        }

        // ------------------------------------------------------------------ props

        /// <summary>Procedural tree. Origin is the trunk base (ground contact) in the returned image.</summary>
        public static Image Tree(ulong seed, out Vector2I origin, double scale = 1.0)
        {
            var rng = new Rng(seed);
            int w = (int)(44 * scale), h = (int)(64 * scale);
            var buf = new byte[w * h * 4];
            int baseX = w / 2, baseY = h - 5;
            origin = new Vector2I(baseX, baseY);
            // ground shadow
            Ellipse(buf, w, h, baseX + 3, baseY, (int)(15 * scale), (int)(5 * scale), new Color(0.08f, 0.1f, 0.12f, 0.35f));
            // trunk
            int trunkH = (int)(rng.Range(15, 20) * scale);
            int tw = Math.Max(3, (int)(4 * scale));
            Color bark0 = Color.Color8(62, 44, 38), bark1 = Color.Color8(96, 68, 50), bark2 = Color.Color8(124, 92, 64);
            for (int y = baseY - trunkH; y <= baseY; y++)
            {
                int flare = y > baseY - 3 ? (baseY - y == 0 ? 2 : 1) : 0;
                for (int x = baseX - tw / 2 - flare; x <= baseX + tw / 2 + flare; x++)
                {
                    Color c = x <= baseX - tw / 2 - flare || x >= baseX + tw / 2 + flare ? bark0 : (x < baseX ? bark2 : bark1);
                    if (((y * 7 + x * 3) % 11) == 0 && x > baseX - tw / 2 - flare && x < baseX + tw / 2 + flare) c = bark0;
                    PutClip(buf, w, h, x, y, c);
                }
            }
            // canopy: union of blobs, shaded by the nearest blob normal
            Color leaf0 = Color.Color8(36, 70, 48), leaf1 = Color.Color8(52, 98, 56), leaf2 = Color.Color8(74, 128, 62), leaf3 = Color.Color8(112, 162, 76), leafOut = Color.Color8(26, 48, 40);
            int n = rng.Range(6, 8);
            var cx = new double[n]; var cy = new double[n]; var cr = new double[n];
            double top = baseY - trunkH - 12 * scale;
            for (int i = 0; i < n; i++)
            {
                double a = i / (double)n * DMath.TwoPi + rng.Range(-0.3, 0.3);
                double dist = i == 0 ? 0 : rng.Range(6.0, 10.0) * scale;
                cx[i] = baseX + DMath.Cos(a) * dist;
                cy[i] = top + DMath.Sin(a) * dist * 0.75;
                cr[i] = rng.Range(7.0, 10.0) * scale * (i == 0 ? 1.2 : 1.0);
            }
            var mask = new bool[w * h];
            var shade = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int best = -1;
                    double bestD = 1e9;
                    for (int i = 0; i < n; i++)
                    {
                        double d = Math.Sqrt(DMath.Sq(x + 0.5 - cx[i]) + DMath.Sq((y + 0.5 - cy[i]) * 1.1)) / cr[i];
                        if (d < 1 && d < bestD) { bestD = d; best = i; }
                    }
                    if (best < 0) continue;
                    mask[y * w + x] = true;
                    double nx = (x + 0.5 - cx[best]) / cr[best], ny = (y + 0.5 - cy[best]) / cr[best];
                    double nz = Math.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
                    double l = -0.55 * nx - 0.6 * ny + 0.58 * nz;
                    double g = (y - top) / (baseY - top);
                    l -= g * 0.25;
                    shade[y * w + x] = l > 0.62 ? 3 : (l > 0.3 ? 2 : (l > -0.05 ? 1 : 0));
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!mask[i])
                    {
                        bool edge = (x > 0 && mask[i - 1]) || (x < w - 1 && mask[i + 1]) || (y > 0 && mask[i - w]) || (y < h - 1 && mask[i + w]);
                        if (edge) PutClip(buf, w, h, x, y, leafOut);
                        continue;
                    }
                    Color c = shade[i] switch { 3 => leaf3, 2 => leaf2, 1 => leaf1, _ => leaf0 };
                    // leaf cluster texture: small darker arcs anchored to the canopy
                    if (shade[i] >= 2 && Hash(seed, x / 3, y / 3) < 0.28 && (x + y) % 3 == 0) c = shade[i] == 3 ? leaf2 : leaf1;
                    PutClip(buf, w, h, x, y, c);
                }
            }
            // a few fruits or blossoms
            if (rng.Chance(0.5))
            {
                var fc = rng.Chance(0.5) ? Color.Color8(214, 84, 70) : Color.Color8(236, 222, 150);
                for (int k = 0; k < 5; k++)
                {
                    int x = (int)(baseX + rng.Range(-12.0, 12.0) * scale), y = (int)(top + rng.Range(-6.0, 8.0) * scale);
                    if (x > 0 && y > 0 && x < w && y < h && mask[y * w + x]) PutClip(buf, w, h, x, y, fc);
                }
            }
            return ToImage(buf, w, h);
        }

        public static Image Rock(ulong seed, out Vector2I origin, double scale = 1.0)
        {
            var rng = new Rng(seed);
            int w = (int)(20 * scale) + 4, h = (int)(15 * scale) + 4;
            var buf = new byte[w * h * 4];
            int bx = w / 2, by = h - 3;
            origin = new Vector2I(bx, by);
            Ellipse(buf, w, h, bx + 1, by, (int)(8 * scale), (int)(3 * scale), new Color(0.08f, 0.1f, 0.12f, 0.35f));
            double rx = rng.Range(6.0, 8.5) * scale, ry = rng.Range(4.5, 6.5) * scale;
            double cxr = bx, cyr = by - ry * 0.8;
            var mask = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = (x + 0.5 - cxr) / rx, dy = (y + 0.5 - cyr) / ry;
                    double wob = PatternNoise.Perlin2(seed, x * 0.35, y * 0.35) * 0.18;
                    if (dx * dx + dy * dy < 1 + wob && y <= by) mask[y * w + x] = true;
                }
            bool moss = rng.Chance(0.4);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!mask[i])
                    {
                        bool edge = (x > 0 && mask[i - 1]) || (x < w - 1 && mask[i + 1]) || (y > 0 && mask[i - w]) || (y < h - 1 && mask[i + w]);
                        if (edge) PutClip(buf, w, h, x, y, Color.Color8(46, 46, 58));
                        continue;
                    }
                    double nx = (x + 0.5 - cxr) / rx, ny = (y + 0.5 - cyr) / ry;
                    double l = -0.6 * nx - 0.7 * ny + PatternNoise.Perlin2(seed + 5, x * 0.5, y * 0.5) * 0.25;
                    Color c = l > 0.55 ? Stone3 : (l > 0.15 ? Stone2 : (l > -0.25 ? Stone1 : Stone0));
                    if (moss && ny < -0.35 && Hash(seed, x, y) < 0.7) c = l > 0.3 ? Grass2 : Grass1;
                    PutClip(buf, w, h, x, y, c);
                }
            return ToImage(buf, w, h);
        }

        public static Image Bush(ulong seed, out Vector2I origin, double scale = 1.0)
        {
            var rng = new Rng(seed);
            int w = (int)(24 * scale) + 4, h = (int)(18 * scale) + 4;
            var buf = new byte[w * h * 4];
            int bx = w / 2, by = h - 3;
            origin = new Vector2I(bx, by);
            Ellipse(buf, w, h, bx + 1, by, (int)(10 * scale), (int)(3 * scale), new Color(0.08f, 0.1f, 0.12f, 0.35f));
            Color l0 = Color.Color8(40, 76, 50), l1 = Color.Color8(58, 106, 58), l2 = Color.Color8(82, 136, 66), l3 = Color.Color8(120, 170, 80), outC = Color.Color8(28, 52, 42);
            int n = rng.Range(3, 5);
            var cx = new double[n]; var cy = new double[n]; var cr = new double[n];
            for (int i = 0; i < n; i++)
            {
                cx[i] = bx + rng.Range(-6.0, 6.0) * scale;
                cy[i] = by - rng.Range(4.0, 8.0) * scale;
                cr[i] = rng.Range(4.5, 7.0) * scale;
            }
            var mask = new bool[w * h];
            var sh = new int[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int best = -1; double bd = 1e9;
                    for (int i = 0; i < n; i++)
                    {
                        double d = Math.Sqrt(DMath.Sq(x + 0.5 - cx[i]) + DMath.Sq(y + 0.5 - cy[i])) / cr[i];
                        if (d < 1 && d < bd) { bd = d; best = i; }
                    }
                    if (best < 0 || y > by) continue;
                    mask[y * w + x] = true;
                    double nx = (x + 0.5 - cx[best]) / cr[best], ny = (y + 0.5 - cy[best]) / cr[best];
                    double l = -0.6 * nx - 0.7 * ny;
                    sh[y * w + x] = l > 0.5 ? 3 : (l > 0.1 ? 2 : (l > -0.35 ? 1 : 0));
                }
            bool berries = rng.Chance(0.5);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!mask[i])
                    {
                        bool edge = (x > 0 && mask[i - 1]) || (x < w - 1 && mask[i + 1]) || (y > 0 && mask[i - w]) || (y < h - 1 && mask[i + w]);
                        if (edge) PutClip(buf, w, h, x, y, outC);
                        continue;
                    }
                    Color c = sh[i] switch { 3 => l3, 2 => l2, 1 => l1, _ => l0 };
                    if (berries && sh[i] >= 1 && Hash(seed, x, y) < 0.05) c = Color.Color8(196, 70, 90);
                    PutClip(buf, w, h, x, y, c);
                }
            return ToImage(buf, w, h);
        }

        private static void Ellipse(byte[] buf, int w, int h, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    double dx = (x + 0.5 - cx) / Math.Max(1, rx), dy = (y + 0.5 - cy) / Math.Max(1, ry);
                    if (dx * dx + dy * dy <= 1) PutClip(buf, w, h, x, y, c);
                }
        }
    }
}
