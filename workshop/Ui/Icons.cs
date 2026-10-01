// Procedural Pixel Creature Workshop - tiny pixel icons generated in code (no image assets, no emoji
// font dependencies). Each icon is a string pattern: '#' = ink, '+' = accent, '.' = transparent.

using System.Collections.Generic;
using Godot;

namespace PixelCreatures.Workshop
{
    public static class Icons
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static readonly Dictionary<string, string[]> Patterns = new Dictionary<string, string[]>
        {
            ["lock"] = new[]
            {
                "...####...", "..#....#..", "..#....#..", "..#....#..", ".########.",
                ".#++++++#.", ".#++##++#.", ".#++##++#.", ".#++++++#.", ".########.",
            },
            ["unlock"] = new[]
            {
                "...####...", "..#....#..", "..#.......", "..#.......", ".########.",
                ".#......#.", ".#..##..#.", ".#..##..#.", ".#......#.", ".########.",
            },
            ["fold_open"] = new[] { "..........", "..........", ".########.", "..######..", "...####...", "....##....", "..........", ".........." },
            ["fold_closed"] = new[] { "..#.....", "..##....", "..###...", "..####..", "..###...", "..##....", "..#....." },
            ["play"] = new[] { ".#......", ".##.....", ".###....", ".####...", ".#####..", ".####...", ".###....", ".##.....", ".#......" },
            ["pause"] = new[] { ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##.", ".##..##." },
            ["step"] = new[] { ".#....#.", ".##...#.", ".###..#.", ".####.#.", ".###..#.", ".##...#.", ".#....#." },
            ["undo"] = new[] { "...#......", "..##......", ".#######..", "..##....#.", "...#.....#", ".........#", "........#.", "..#####..." },
            ["redo"] = new[] { "......#...", "......##..", "..#######.", ".#....##..", "#.....#...", "#.........", ".#........", "...#####.." },
            ["dice"] = new[] { ".########.", "#........#", "#.##..##.#", "#.##..##.#", "#........#", "#...##...#", "#........#", "#.##..##.#", "#.##..##.#", ".########." },
            ["new"] = new[] { "....##....", "....##....", "....##....", "##########", "##########", "....##....", "....##....", "....##...." },
            ["save"] = new[] { "########..", "#+#..#+##.", "#+#..#+#+#", "#+####+#+#", "#++++++#+#", "#+#####+#+", "#+#+++#+#.", "#+#+++#+#.", "#########." },
            ["load"] = new[] { "..........", "####......", "#..#......", "#..######.", "#........#", "#.########", "#.#......#", "##......#.", "#########." },
            ["export"] = new[] { "....#.....", "...###....", "..#####...", "....#.....", "....#.....", "#...#...#.", "#.......#.", "#########." },
            ["reset"] = new[] { "..####....", ".#....#...", "#......#..", "#.....###.", "#......#..", "#.........", ".#....#...", "..####...." },
            ["arrow"] = new[] { "....#....", "...###...", "..#####..", ".#######.", "....#....", "....#....", "....#....", "....#...." },
            ["eye"] = new[] { "..........", "...####...", ".##....##.", "#...##...#", "#..####..#", "#...##...#", ".##....##.", "...####..." },
        };

        public static Texture2D Get(string name, int scale = 2, Color? ink = null, Color? accent = null)
        {
            string key = $"{name}:{scale}:{ink}:{accent}";
            if (Cache.TryGetValue(key, out var t)) return t;
            var p = Patterns[name];
            int h = p.Length, w = p[0].Length;
            var img = Image.CreateEmpty(w * scale, h * scale, false, Image.Format.Rgba8);
            var inkC = ink ?? UiKit.Text;
            var accC = accent ?? UiKit.Accent;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w && x < p[y].Length; x++)
                {
                    char ch = p[y][x];
                    if (ch == '.') continue;
                    var c = ch == '+' ? accC : inkC;
                    for (int oy = 0; oy < scale; oy++)
                        for (int ox = 0; ox < scale; ox++)
                            img.SetPixel(x * scale + ox, y * scale + oy, c);
                }
            t = ImageTexture.CreateFromImage(img);
            Cache[key] = t;
            return t;
        }

        /// <summary>Arrow icon rotated to one of eight directions (0 = east ... 6 = south).</summary>
        public static Texture2D Direction(int dir8, int scale = 2)
        {
            string key = $"dir:{dir8}:{scale}";
            if (Cache.TryGetValue(key, out var t)) return t;
            var basePattern = Patterns["arrow"];
            int n = 9;
            var img = Image.CreateEmpty(n * scale, n * scale, false, Image.Format.Rgba8);
            // arrow points up (north) in the pattern; rotate around the centre
            double yaw = dir8 * Mathf.Pi / 4.0; // 0 = east
            double rot = -(yaw - Mathf.Pi / 2.0);
            double cs = Mathf.Cos((float)rot), sn = Mathf.Sin((float)rot);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    double dx = x - 4, dy = y - 4;
                    int sx = (int)Mathf.Round((float)(dx * cs + dy * sn + 4));
                    int sy = (int)Mathf.Round((float)(-dx * sn + dy * cs + 4));
                    if (sy < 0 || sy >= basePattern.Length || sx < 0 || sx >= basePattern[sy].Length) continue;
                    if (basePattern[sy][sx] == '.') continue;
                    for (int oy = 0; oy < scale; oy++)
                        for (int ox = 0; ox < scale; ox++)
                            img.SetPixel(x * scale + ox, y * scale + oy, UiKit.Text);
                }
            t = ImageTexture.CreateFromImage(img);
            Cache[key] = t;
            return t;
        }
    }
}
