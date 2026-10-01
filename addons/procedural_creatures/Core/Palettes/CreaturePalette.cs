// Procedural Pixel Creatures - creature palette with material ramps and shading styles.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Palettes
{
    /// <summary>Shade levels of a material ramp.</summary>
    public static class Shade
    {
        public const int Outline = 0;
        public const int Deep = 1;
        public const int Shadow = 2;
        public const int Base = 3;
        public const int Light = 4;
        public const int Highlight = 5;
        public const int Count = 6;
    }

    /// <summary>
    /// Final palette of a creature. Colors[0] is transparent; Ramp[slot, shade] maps a material slot and
    /// shade level to a palette index. Ramps share colors (outline, deep shadows, whites) to keep the
    /// number of visible colours per creature in the 10..16 range.
    /// </summary>
    public sealed class CreaturePalette
    {
        public const int ShadowIndex = 1;       // translucent ground shadow
        public const int WaterlineIndex = 2;    // light ripple where a body meets the water surface
        public const int FirstColorIndex = 3;

        private readonly List<Rgba> _colors = new List<Rgba>();
        private readonly List<string> _names = new List<string>();
        public readonly byte[,] Ramp = new byte[(int)MaterialSlot.Count, Shade.Count];

        public IReadOnlyList<Rgba> Colors => _colors;
        public IReadOnlyList<string> ColorNames => _names;
        public ulong ColorKey { get; internal set; }

        /// <summary>Colour used for the hit flash (near white, slightly tinted).</summary>
        public Rgba FlashColor { get; internal set; } = new Rgba(255, 250, 240);
        public Rgba WaterTint { get; internal set; } = new Rgba(52, 110, 150);

        public CreaturePalette()
        {
            _colors.Add(Rgba.Transparent); _names.Add("transparent");
            _colors.Add(new Rgba(18, 14, 28, 92)); _names.Add("ground shadow");
            _colors.Add(new Rgba(214, 238, 246, 220)); _names.Add("waterline");
        }

        public int Add(Rgba c, string name)
        {
            for (int i = FirstColorIndex; i < _colors.Count; i++)
                if (_colors[i].Equals(c)) return i;
            _colors.Add(c);
            _names.Add(name);
            if (_colors.Count > 255) throw new InvalidOperationException("Palette overflow.");
            return _colors.Count - 1;
        }

        public void SetRamp(MaterialSlot slot, int outline, int deep, int shadow, int baseColor, int light, int highlight)
        {
            int s = (int)slot;
            Ramp[s, 0] = (byte)outline;
            Ramp[s, 1] = (byte)deep;
            Ramp[s, 2] = (byte)shadow;
            Ramp[s, 3] = (byte)baseColor;
            Ramp[s, 4] = (byte)light;
            Ramp[s, 5] = (byte)highlight;
        }

        public byte Index(MaterialSlot slot, int shade) => Ramp[(int)slot, DMath.Clamp(shade, 0, Shade.Count - 1)];

        public Rgba Color(MaterialSlot slot, int shade) => _colors[Index(slot, shade)];

        /// <summary>RGBA lookup table (index to packed RGBA bytes), optionally flashed.</summary>
        public void BuildLut(byte[] lut, double flash)
        {
            for (int i = 0; i < 256; i++)
            {
                Rgba c = i < _colors.Count ? _colors[i] : Rgba.Transparent;
                if (flash > 0 && i >= FirstColorIndex)
                {
                    c = Rgba.Lerp(c, FlashColor.WithAlpha(c.A), flash);
                }
                lut[i * 4 + 0] = c.R;
                lut[i * 4 + 1] = c.G;
                lut[i * 4 + 2] = c.B;
                lut[i * 4 + 3] = c.A;
            }
        }
    }

    /// <summary>How a material reacts to light. Values are thresholds on wrapped diffuse lighting.</summary>
    public sealed class MaterialStyle
    {
        public string Id { get; }
        public string Label { get; }
        /// <summary>Wrap lighting amount (0 = hard terminator, 0.5 = soft).</summary>
        public double Wrap { get; }
        public double LightThreshold { get; }
        public double BaseThreshold { get; }
        public double ShadowThreshold { get; }
        /// <summary>Specular highlight threshold on n.h (above 1 disables).</summary>
        public double SpecularThreshold { get; }
        /// <summary>Surface micro texture drawn on top of the shading.</summary>
        public SurfaceTexture Texture { get; }
        public bool Faceted { get; }

        public MaterialStyle(string id, string label, double wrap, double light, double baseT, double shadow, double specular,
            SurfaceTexture texture = SurfaceTexture.None, bool faceted = false)
        {
            Id = id; Label = label; Wrap = wrap; LightThreshold = light; BaseThreshold = baseT; ShadowThreshold = shadow;
            SpecularThreshold = specular; Texture = texture; Faceted = faceted;
        }

        /// <summary>
        /// Global form-light remap applied before the material thresholds: shifts the terminator towards the
        /// light so that roughly a third of a body faces away from it (readable volume) instead of a quarter.
        /// </summary>
        private const double FormShift = -0.11, FormGain = 1.08;

        /// <summary>Maps lighting to a shade level (1 deep .. 4 light; 5 highlight only via specular).</summary>
        public int Level(double ndotl, double ndoth)
        {
            double w = ((ndotl + Wrap) / (1.0 + Wrap) + FormShift) * FormGain;
            if (ndoth > SpecularThreshold) return Shade.Highlight;
            if (w > LightThreshold) return Shade.Light;
            if (w > BaseThreshold) return Shade.Base;
            if (w > ShadowThreshold) return Shade.Shadow;
            return Shade.Deep;
        }
    }

    public enum SurfaceTexture : byte
    {
        None,
        Scales,
        Fur,
        Plates,
        Feathers,
        Bark,
        Spots,
    }

    /// <summary>Built-in material styles. Extensions can register more in the CreatureRegistry.</summary>
    public static class MaterialStyles
    {
        public static readonly MaterialStyle Fur = new MaterialStyle("fur", "Fur", 0.35, 0.58, 0.18, -0.12, 2.0, SurfaceTexture.Fur);
        public static readonly MaterialStyle Hide = new MaterialStyle("hide", "Hide", 0.25, 0.60, 0.22, -0.08, 0.985);
        public static readonly MaterialStyle Scales = new MaterialStyle("scales", "Scales", 0.20, 0.62, 0.24, -0.04, 0.975, SurfaceTexture.Scales);
        public static readonly MaterialStyle Chitin = new MaterialStyle("chitin", "Chitin", 0.10, 0.64, 0.28, 0.00, 0.955, SurfaceTexture.Plates);
        public static readonly MaterialStyle Stone = new MaterialStyle("stone", "Stone", 0.15, 0.60, 0.26, -0.02, 2.0, SurfaceTexture.None, faceted: true);
        public static readonly MaterialStyle Slime = new MaterialStyle("slime", "Slime", 0.45, 0.62, 0.20, -0.20, 0.94);
        public static readonly MaterialStyle Feather = new MaterialStyle("feather", "Feathers", 0.30, 0.60, 0.20, -0.10, 2.0, SurfaceTexture.Feathers);
        public static readonly MaterialStyle Horn = new MaterialStyle("horn", "Horn", 0.20, 0.55, 0.22, -0.05, 0.97);
        public static readonly MaterialStyle Membrane = new MaterialStyle("membrane", "Membrane", 0.40, 0.62, 0.20, -0.15, 2.0);
        public static readonly MaterialStyle Matte = new MaterialStyle("matte", "Matte", 0.30, 0.62, 0.22, -0.10, 2.0);
        public static readonly MaterialStyle Glossy = new MaterialStyle("glossy", "Glossy", 0.20, 0.62, 0.25, -0.05, 0.93);
        public static readonly MaterialStyle Emissive = new MaterialStyle("emissive", "Emissive", 1.0, 0.70, 0.30, -2.0, 0.90);
        public static readonly MaterialStyle Bark = new MaterialStyle("bark", "Bark", 0.20, 0.62, 0.24, -0.04, 2.0, SurfaceTexture.Bark);
        public static readonly MaterialStyle Leaf = new MaterialStyle("leaf", "Leaf", 0.35, 0.58, 0.18, -0.12, 0.985);
        /// <summary>Polished metal: hard terminator and a broad specular glint.</summary>
        public static readonly MaterialStyle Metal = new MaterialStyle("metal", "Metal", 0.05, 0.6, 0.3, 0.04, 0.9);

        public static IEnumerable<MaterialStyle> All => new[] { Fur, Hide, Scales, Chitin, Stone, Slime, Feather, Horn, Membrane, Matte, Glossy, Emissive, Bark, Leaf, Metal };
    }
}
