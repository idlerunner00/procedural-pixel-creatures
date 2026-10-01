// Procedural Pixel Creatures - indexed output frame of the pixel renderer.

using System;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core.Rendering
{
    /// <summary>
    /// One rendered frame: palette indices per pixel (0 = transparent) plus the canvas origin (ground
    /// point under the creature root). Converting to RGBA is a lookup, so palette changes and hit
    /// flashes do not require re-rasterizing.
    /// </summary>
    public sealed class PixelFrame
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int OriginX { get; set; }
        public int OriginY { get; set; }
        public byte[] Indices { get; private set; } = Array.Empty<byte>();
        /// <summary>Per pixel: 1 when the pixel is below the water surface (tinted during RGBA conversion).</summary>
        public byte[] Submerged { get; private set; } = Array.Empty<byte>();
        public bool HasSubmerged { get; set; }

        public PixelFrame() { }

        public PixelFrame(int width, int height) { Resize(width, height); }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Frame size must be positive.");
            if (width > 1024 || height > 1024) throw new ArgumentOutOfRangeException(nameof(width), $"Frame size {width}x{height} exceeds the 1024 px safety limit.");
            Width = width;
            Height = height;
            int n = width * height;
            if (Indices.Length != n)
            {
                Indices = new byte[n];
                Submerged = new byte[n];
            }
        }

        public void Clear()
        {
            Array.Clear(Indices, 0, Indices.Length);
            if (HasSubmerged) Array.Clear(Submerged, 0, Submerged.Length);
            HasSubmerged = false;
        }

        public byte this[int x, int y] => Indices[y * Width + x];

        /// <summary>Writes RGBA8 pixels into 'rgba' (length Width*Height*4) using a palette LUT from CreaturePalette.BuildLut.</summary>
        public void ToRgba(byte[] lut, byte[] rgba, Rgba waterTint)
        {
            int n = Width * Height;
            if (rgba.Length < n * 4) throw new ArgumentException("RGBA buffer too small.", nameof(rgba));
            for (int i = 0; i < n; i++)
            {
                int idx = Indices[i] * 4;
                int o = i * 4;
                rgba[o] = lut[idx];
                rgba[o + 1] = lut[idx + 1];
                rgba[o + 2] = lut[idx + 2];
                rgba[o + 3] = lut[idx + 3];
            }
            if (HasSubmerged)
            {
                for (int i = 0; i < n; i++)
                {
                    if (Submerged[i] == 0 || Indices[i] < CreaturePalette.FirstColorIndex) continue;
                    int o = i * 4;
                    rgba[o] = (byte)((rgba[o] * 5 + waterTint.R * 5) / 10);
                    rgba[o + 1] = (byte)((rgba[o + 1] * 5 + waterTint.G * 5) / 10);
                    rgba[o + 2] = (byte)((rgba[o + 2] * 5 + waterTint.B * 5) / 10);
                    rgba[o + 3] = 185;
                }
            }
        }

        /// <summary>Bounding box of non transparent pixels (x0,y0 inclusive, x1,y1 exclusive). Empty frame returns false.</summary>
        public bool ContentBounds(out int x0, out int y0, out int x1, out int y1)
        {
            x0 = Width; y0 = Height; x1 = 0; y1 = 0;
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (Indices[row + x] == 0) continue;
                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x + 1 > x1) x1 = x + 1;
                    if (y + 1 > y1) y1 = y + 1;
                }
            }
            return x1 > x0 && y1 > y0;
        }

        public ulong ContentHash()
        {
            ulong h = StableHash.Combine((ulong)Width, (ulong)Height);
            h = StableHash.Combine(h, (ulong)(OriginX * 4099 + OriginY));
            h = StableHash.Fnv1a64(Indices, h);
            if (HasSubmerged) h = StableHash.Fnv1a64(Submerged, h);
            return h;
        }

        public void CopyFrom(PixelFrame other)
        {
            Resize(other.Width, other.Height);
            Array.Copy(other.Indices, Indices, Indices.Length);
            Array.Copy(other.Submerged, Submerged, Submerged.Length);
            HasSubmerged = other.HasSubmerged;
            OriginX = other.OriginX;
            OriginY = other.OriginY;
        }
    }

    /// <summary>Statistics of one render call (for profiling, tests and the debug overlay).</summary>
    public struct RenderStats
    {
        public int PrimitivesDrawn;
        public int PixelsTested;
        public int PixelsCovered;
        /// <summary>True when some part of the creature fell outside the canvas (would be clipped).</summary>
        public bool Overflow;
        public int OverflowLeft, OverflowRight, OverflowTop, OverflowBottom;
        public int NonFiniteRejected;
        public int VisibleColors;
        /// <summary>Pixels darkened by contact shadows.</summary>
        public int ContactShadowPixels;
        /// <summary>Pixels per shade level (only with RenderOptions.CollectShadeStats).</summary>
        public int HighlightPixels, LightPixels, BasePixels, ShadowPixels, DeepPixels;
    }
}
