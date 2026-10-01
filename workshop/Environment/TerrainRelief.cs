using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;

namespace PixelCreatures.Workshop
{
    public sealed class ReliefFace
    {
        public int X, Y, Width, Height;
        public byte[] Rgba = Array.Empty<byte>();
    }

    public static partial class EnvironmentArt
    {
        // Terrain coordinates describe the ground plane. The texture, props and actors all use the
        // same projection y - elevation. Paths cut gently graded ramps through the terraced hills.
        private static void BuildRelief(TestMap map, CancellationToken cancel)
        {
            int w = map.Width, h = map.Height;
            map.Elevation = new float[w * h];
            map.WaterDepth = new float[w * h];
            map.WaterSurfaceRgba = new byte[w * h * 4];
            var pathDistance = new byte[w * h];
            for (int i = 0; i < pathDistance.Length; i++) pathDistance[i] = map.Terrain[i] == TestMap.Path ? (byte)0 : (byte)100;
            for (int y = 1; y < h; y++)
                for (int x = 1; x < w; x++)
                { int i = y * w + x; pathDistance[i] = (byte)Math.Min(pathDistance[i], 1 + Math.Min(pathDistance[i - 1], pathDistance[i - w])); }
            for (int y = h - 2; y >= 0; y--)
                for (int x = w - 2; x >= 0; x--)
                { int i = y * w + x; pathDistance[i] = (byte)Math.Min(pathDistance[i], 1 + Math.Min(pathDistance[i + 1], pathDistance[i + w])); }
            Parallel.For(0, h, new ParallelOptions { CancellationToken = cancel }, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double shore = 1e6;
                    foreach (var p in map.Ponds)
                    {
                        double dx = (x - p.Cx) / p.Rx, dy = (y - p.Cy) / p.Ry;
                        double v = Math.Sqrt(dx * dx + dy * dy) + PatternNoise.Perlin2(p.ShapeSeed, dx * 1.7, dy * 1.7) * 0.3;
                        shore = Math.Min(shore, (v - 1) * Math.Min(p.Rx, p.Ry));
                    }
                    if (map.Terrain[i] == TestMap.Water)
                    {
                        float depth = (float)Math.Clamp(-shore / 42, 0, 1);
                        map.WaterDepth[i] = depth;
                        double grain = Hash(map.Seed + 601, x / 2, y / 2);
                        var shallow = Color.Color8(75, 151, 145);
                        var deep = Color.Color8(21, 61, 89);
                        Color c = shallow.Lerp(deep, depth * 0.9f);
                        c = c.Lightened((float)(grain * 0.055));
                        // Sand bars and quiet caustic lines remain visible through the water.
                        double caustic = Math.Sin(x * 0.17 + Math.Sin(y * 0.13) * 2.4) + Math.Cos(y * 0.26 + x * 0.04);
                        if (caustic > 1.82) c = c.Lightened(0.14f * (1 - depth * 0.6f));
                        PutClip(map.GroundRgba, w, h, x, y, c);
                        int j = i * 4;
                        map.WaterSurfaceRgba[j] = 39;
                        map.WaterSurfaceRgba[j + 1] = 125;
                        map.WaterSurfaceRgba[j + 2] = 151;
                        map.WaterSurfaceRgba[j + 3] = (byte)(30 + depth * 22);
                        continue;
                    }
                    double n = PatternNoise.Perlin2(map.Seed + 702, x / 210.0, y / 155.0);
                    // A broad mesa guarantees a readable elevation change even on the smallest map.
                    double hx = (x - w * 0.27) / Math.Min(190, w * 0.25), hy = (y - h * 0.29) / Math.Min(130, h * 0.25);
                    double mound = Math.Max(0, 1 - Math.Sqrt(hx * hx + hy * hy));
                    double raw = Math.Max(mound * 45, Math.Max(0, n + 0.08) * 65);
                    double terrace = Math.Floor(raw / 12) * 12;
                    // A north/south access ramp at every hill band, wide enough for large creatures.
                    double rampCenter = w * 0.27 + Math.Sin(y * 0.012) * 12;
                    double ramp = Math.Clamp((Math.Abs(x - rampCenter) - 20) / 22, 0, 1);
                    ramp = Math.Min(ramp, Math.Clamp(pathDistance[i] / 24.0, 0, 1));
                    ramp = ramp * ramp * (3 - 2 * ramp);
                    double elevation = raw * (1 - ramp) + terrace * ramp;
                    double bank = Math.Clamp((shore - 16) / 40, 0, 1);
                    bank = bank * bank * (3 - 2 * bank);
                    double edge = Math.Clamp(Math.Min(Math.Min(x, w - x - 1), Math.Min(y, h - y - 1)) / 42.0, 0, 1);
                    map.Elevation[i] = (float)(elevation * bank * edge);
                }
            });
            // Sunlit crowns, darker slopes, mottled paths and stone fragments. No blurry resampling.
            var source = map.GroundRgba;
            var projected = new byte[source.Length];
            Array.Copy(source, projected, source.Length);
            for (int y = 1; y < h - 1; y++)
            {
                cancel.ThrowIfCancellationRequested();
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x, top = y - (int)Math.Round(map.Elevation[i]);
                    if (top < 0) continue;
                    float slope = (map.Elevation[i + 1] - map.Elevation[i - 1]) * 0.035f + (map.Elevation[i + w] - map.Elevation[i - w]) * 0.055f;
                    float light = Math.Clamp(1.0f + slope + map.Elevation[i] * 0.0017f, 0.77f, 1.16f);
                    int target = (top * w + x) * 4;
                    for (int c = 0; c < 3; c++) projected[target + c] = (byte)Math.Clamp(source[i * 4 + c] * light, 0, 255);
                    projected[target + 3] = 255;
                    int next = y + 1 - (int)Math.Round(map.Elevation[i + w]);
                    for (int fy = top + 1; fy < next && fy < h; fy++)
                    {
                        if (map.Elevation[i] - map.Elevation[i + w] < 2.5f)
                        {
                            Array.Copy(projected, target, projected, (fy * w + x) * 4, 4);
                            continue;
                        }
                        double grain = Hash(map.Seed + 710, x / 3, fy / 2);
                        var rock = grain > 0.6 ? Color.Color8(116, 102, 76) : Color.Color8(86, 84, 67);
                        if ((fy - top) % 5 == 0) rock = Color.Color8(67, 71, 59);
                        if (fy == top + 1) rock = Color.Color8(146, 139, 92);
                        PutClip(projected, w, h, x, fy, rock);
                    }
                }
            }
            // Small cliff strips participate in the same depth ordering as trees and creatures.
            for (int y = 1; y < h - 1; y += 2)
            {
                int start = -1;
                for (int x = 1; x < w; x++)
                {
                    bool cliff = x < w - 1 && map.Elevation[y * w + x] - map.Elevation[(y + 1) * w + x] > 4;
                    if (cliff && start < 0) start = x;
                    if ((!cliff || x == w - 1) && start >= 0)
                    {
                        int minTop = y, maxBottom = 0;
                        for (int xx = start; xx < x; xx++)
                        {
                            minTop = Math.Min(minTop, y - (int)map.Elevation[y * w + xx]);
                            maxBottom = Math.Max(maxBottom, y + 2 - (int)map.Elevation[Math.Min(h - 1, y + 2) * w + xx]);
                        }
                        int fh = maxBottom - minTop;
                        if (fh > 2 && minTop >= 0)
                        {
                            var face = new ReliefFace { X = start, Y = maxBottom, Width = x - start, Height = fh, Rgba = new byte[(x - start) * fh * 4] };
                            for (int xx = start; xx < x; xx++)
                            {
                                int t = y - (int)map.Elevation[y * w + xx];
                                int b = y + 2 - (int)map.Elevation[Math.Min(h - 1, y + 2) * w + xx];
                                for (int yy = t; yy < b; yy++)
                                    Array.Copy(projected, (yy * w + xx) * 4, face.Rgba, ((yy - minTop) * face.Width + xx - start) * 4, 4);
                            }
                            map.Cliffs.Add(face);
                        }
                        start = -1;
                    }
                }
            }
            map.GroundRgba = projected;
        }

        private static void BuildClearance(TestMap map)
        {
            int w = map.Width, h = map.Height;
            var d = new byte[w * h];
            Array.Fill(d, (byte)250);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    bool water = map.IsWater(x, y);
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                        map.IsWater(x - 1, y) != water || map.IsWater(x + 1, y) != water ||
                        map.IsWater(x, y - 1) != water || map.IsWater(x, y + 1) != water ||
                        (!water && (Math.Abs(map.Elevation[i] - map.Elevation[i - w]) > 2 || Math.Abs(map.Elevation[i] - map.Elevation[i - 1]) > 2))) d[i] = 0;
                }
            foreach (var p in map.Props)
            {
                int r = p.Kind == PropKind.Tree ? 3 : p.Kind == PropKind.Rock ? 4 : 2;
                for (int y = Math.Max(0, p.Y - r); y <= Math.Min(h - 1, p.Y + r); y++)
                    for (int x = Math.Max(0, p.X - r); x <= Math.Min(w - 1, p.X + r); x++) d[y * w + x] = 0;
            }
            // Chebyshev distance is conservative: a square footprint is fully clear of obstacles.
            for (int y = 1; y < h; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    d[i] = (byte)Math.Min(d[i], 1 + Math.Min(Math.Min(d[i - 1], d[i - w]), Math.Min(d[i - w - 1], d[i - w + 1])));
                }
            for (int y = h - 2; y >= 0; y--)
                for (int x = w - 2; x > 0; x--)
                {
                    int i = y * w + x;
                    d[i] = (byte)Math.Min(d[i], 1 + Math.Min(Math.Min(d[i + 1], d[i + w]), Math.Min(d[i + w - 1], d[i + w + 1])));
                }
            map.Clearance = d;
        }
    }
}
