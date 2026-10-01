// Procedural Pixel Creatures - simple RGBA canvas with nearest-neighbour blits and a built-in 3x5
// pixel font. Used for labelled contact sheets, spritesheets and reports without engine dependency.

using System;
using System.Collections.Generic;
using System.Text;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;

namespace PixelCreatures.Core.Export
{
    public sealed class ImageCanvas
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Rgba { get; }

        public ImageCanvas(int width, int height, Rgba background)
        {
            Width = width;
            Height = height;
            Rgba = new byte[width * height * 4];
            Fill(0, 0, width, height, background);
        }

        public void Fill(int x, int y, int w, int h, Rgba c)
        {
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y), x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
            for (int yy = y0; yy < y1; yy++)
            {
                for (int xx = x0; xx < x1; xx++)
                {
                    int i = (yy * Width + xx) * 4;
                    Rgba[i] = c.R; Rgba[i + 1] = c.G; Rgba[i + 2] = c.B; Rgba[i + 3] = c.A;
                }
            }
        }

        public void Checker(int x, int y, int w, int h, Rgba a, Rgba b, int cell)
        {
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                    SetPixel(x + xx, y + yy, ((xx / cell + yy / cell) & 1) == 0 ? a : b);
        }

        public void SetPixel(int x, int y, Rgba c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 4;
            if (c.A == 255)
            {
                Rgba[i] = c.R; Rgba[i + 1] = c.G; Rgba[i + 2] = c.B; Rgba[i + 3] = 255;
                return;
            }
            if (c.A == 0) return;
            int a = c.A, ia = 255 - a;
            Rgba[i] = (byte)((c.R * a + Rgba[i] * ia) / 255);
            Rgba[i + 1] = (byte)((c.G * a + Rgba[i + 1] * ia) / 255);
            Rgba[i + 2] = (byte)((c.B * a + Rgba[i + 2] * ia) / 255);
            Rgba[i + 3] = (byte)Math.Min(255, Rgba[i + 3] + a);
        }

        /// <summary>Alpha-over blit of an RGBA buffer with integer scale.</summary>
        public void Blit(byte[] src, int sw, int sh, int dx, int dy, int scale = 1)
        {
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int si = (y * sw + x) * 4;
                    byte a = src[si + 3];
                    if (a == 0) continue;
                    var c = new Rgba(src[si], src[si + 1], src[si + 2], a);
                    for (int oy = 0; oy < scale; oy++)
                        for (int ox = 0; ox < scale; ox++)
                            SetPixel(dx + x * scale + ox, dy + y * scale + oy, c);
                }
            }
        }

        public void BlitFrame(PixelFrame frame, CreaturePalette palette, int dx, int dy, int scale = 1, double flash = 0)
        {
            var lut = new byte[256 * 4];
            palette.BuildLut(lut, flash);
            var rgba = new byte[frame.Width * frame.Height * 4];
            frame.ToRgba(lut, rgba, palette.WaterTint);
            Blit(rgba, frame.Width, frame.Height, dx, dy, scale);
        }

        public void Rect(int x, int y, int w, int h, Rgba c)
        {
            for (int i = 0; i < w; i++) { SetPixel(x + i, y, c); SetPixel(x + i, y + h - 1, c); }
            for (int i = 0; i < h; i++) { SetPixel(x, y + i, c); SetPixel(x + w - 1, y + i, c); }
        }

        public void Line(int x0, int y0, int x1, int y1, Rgba c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                SetPixel(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public byte[] ToPng(params (string, string)[] text) => PngEncoder.Encode(Rgba, Width, Height, text);

        // ------------------------------------------------------------------ 3x5 pixel font

        private static readonly Dictionary<char, string> Glyphs = BuildGlyphs();

        private static Dictionary<char, string> BuildGlyphs()
        {
            var g = new Dictionary<char, string>
            {
                ['A'] = ".#.#.#####.##.#",
                ['B'] = "##.#.###.#.###.",
                ['C'] = ".###..#..#...##",
                ['D'] = "##.#.##.##.###.",
                ['E'] = "####..##.#..###",
                ['F'] = "####..##.#..#..",
                ['G'] = ".###..#.##.#.##",
                ['H'] = "#.##.#####.##.#",
                ['I'] = "###.#..#..#.###",
                ['J'] = "..#..#..##.#.#.",
                ['K'] = "#.##.###.#.##.#",
                ['L'] = "#..#..#..#..###",
                ['M'] = "#.########.##.#",
                ['N'] = "##.#.##.##.##.#",
                ['O'] = ".#.#.##.##.#.#.",
                ['P'] = "##.#.###.#..#..",
                ['Q'] = ".#.#.##.###..##",
                ['R'] = "##.#.###.#.##.#",
                ['S'] = ".###...#...###.",
                ['T'] = "###.#..#..#..#.",
                ['U'] = "#.##.##.##.####",
                ['V'] = "#.##.##.##.#.#.",
                ['W'] = "#.##.########.#",
                ['X'] = "#.##.#.#.#.##.#",
                ['Y'] = "#.##.#.#..#..#.",
                ['Z'] = "###..#.#.#..###",
                ['0'] = "####.##.##.####",
                ['1'] = ".#.##..#..#.###",
                ['2'] = "##...#.#.#..###",
                ['3'] = "##...#.#...###.",
                ['4'] = "#.##.####..#..#",
                ['5'] = "####..##...###.",
                ['6'] = ".###..####.####",
                ['7'] = "###..#.#..#..#.",
                ['8'] = "####.#####.####",
                ['9'] = "####.####..###.",
                ['.'] = ".............#.",
                [','] = "..........#.#..",
                [':'] = "....#.....#....",
                ['-'] = "......###......",
                ['_'] = "............###",
                ['/'] = "..#..#.#.#..#..",
                ['('] = ".#.#..#..#...#.",
                [')'] = ".#...#..#..#.#.",
                ['#'] = "#.#####.#####.#",
                ['%'] = "#....#.#.#....#",
                ['+'] = "....#.###.#....",
                ['='] = "...###...###...",
                ['\''] = ".#..#..........",
                ['!'] = ".#..#..#.....#.",
                ['?'] = "##...#.#.....#.",
                ['<'] = "..#.#.#...#...#",
                ['>'] = "#...#...#.#.#..",
                ['['] = "##.#..#..#..##.",
                [']'] = ".##..#..#..#.##",
                ['*'] = "...#.#.#.#.#...",
                [' '] = "...............",
                ['x'] = "...#.#.#.#.#...",
            };
            return g;
        }

        public static string Normalize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case 'ä': case 'Ä': sb.Append("AE"); break;
                    case 'ö': case 'Ö': sb.Append("OE"); break;
                    case 'ü': case 'Ü': sb.Append("UE"); break;
                    case 'ß': sb.Append("SS"); break;
                    case '°': sb.Append('*'); break;
                    default: sb.Append(ch == 'x' ? 'x' : char.ToUpperInvariant(ch)); break;
                }
            }
            return sb.ToString();
        }

        public static int TextWidth(string s, int scale = 1) => Normalize(s).Length * 4 * scale;

        public void Text(int x, int y, string s, Rgba c, int scale = 1)
        {
            string t = Normalize(s);
            int cx = x;
            foreach (char ch in t)
            {
                if (!Glyphs.TryGetValue(ch, out var glyph)) glyph = Glyphs['?'];
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 3; col++)
                        if (glyph[row * 3 + col] == '#')
                            for (int oy = 0; oy < scale; oy++)
                                for (int ox = 0; ox < scale; ox++)
                                    SetPixel(cx + col * scale + ox, y + row * scale + oy, c);
                cx += 4 * scale;
            }
        }
    }
}
