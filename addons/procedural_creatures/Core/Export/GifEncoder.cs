// Procedural Pixel Creatures - animated GIF encoder (lossless for pixel art: exact colour table).

using System;
using System.Collections.Generic;
using System.IO;

namespace PixelCreatures.Core.Export
{
    public sealed class GifEncoder
    {
        private readonly int _width, _height;
        private readonly List<byte[]> _frames = new List<byte[]>();
        private readonly List<int> _delays = new List<int>();

        public GifEncoder(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public int FrameCount => _frames.Count;

        /// <summary>Adds an opaque RGBA frame (alpha ignored) with a delay in milliseconds.</summary>
        public void AddFrame(byte[] rgba, int delayMs)
        {
            if (rgba.Length < _width * _height * 4) throw new ArgumentException("Frame too small.");
            _frames.Add((byte[])rgba.Clone());
            _delays.Add(Math.Max(2, (int)Math.Round(delayMs / 10.0)));
        }

        public byte[] Encode()
        {
            // exact global palette (pixel art uses few colours); falls back to colour snapping if > 256
            var map = new Dictionary<uint, byte>();
            var palette = new List<uint>();
            var indexed = new List<byte[]>();
            foreach (var f in _frames)
            {
                var idx = new byte[_width * _height];
                for (int i = 0; i < idx.Length; i++)
                {
                    uint c = (uint)(f[i * 4] | (f[i * 4 + 1] << 8) | (f[i * 4 + 2] << 16));
                    if (!map.TryGetValue(c, out byte pi))
                    {
                        if (palette.Count < 256)
                        {
                            pi = (byte)palette.Count;
                            palette.Add(c);
                            map[c] = pi;
                        }
                        else pi = Nearest(palette, c);
                    }
                    idx[i] = pi;
                }
                indexed.Add(idx);
            }
            int bits = 1;
            while ((1 << bits) < Math.Max(2, palette.Count)) bits++;
            int tableSize = 1 << bits;

            using var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(new[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });
            w.Write((ushort)_width);
            w.Write((ushort)_height);
            w.Write((byte)(0x80 | ((bits - 1) << 4) | (bits - 1)));
            w.Write((byte)0);
            w.Write((byte)0);
            for (int i = 0; i < tableSize; i++)
            {
                uint c = i < palette.Count ? palette[i] : 0;
                w.Write((byte)(c & 0xFF));
                w.Write((byte)((c >> 8) & 0xFF));
                w.Write((byte)((c >> 16) & 0xFF));
            }
            // loop forever
            w.Write(new byte[] { 0x21, 0xFF, 0x0B });
            w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            w.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
            for (int f = 0; f < indexed.Count; f++)
            {
                w.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });
                w.Write((ushort)_delays[f]);
                w.Write((byte)0);
                w.Write((byte)0);
                w.Write((byte)0x2C);
                w.Write((ushort)0); w.Write((ushort)0);
                w.Write((ushort)_width); w.Write((ushort)_height);
                w.Write((byte)0);
                int minCode = Math.Max(2, bits);
                w.Write((byte)minCode);
                byte[] data = Lzw(indexed[f], minCode);
                int pos = 0;
                while (pos < data.Length)
                {
                    int n = Math.Min(255, data.Length - pos);
                    w.Write((byte)n);
                    w.Write(data, pos, n);
                    pos += n;
                }
                w.Write((byte)0);
            }
            w.Write((byte)0x3B);
            w.Flush();
            return ms.ToArray();
        }

        private static byte Nearest(List<uint> palette, uint c)
        {
            int r = (int)(c & 0xFF), g = (int)((c >> 8) & 0xFF), b = (int)((c >> 16) & 0xFF);
            int best = 0, bestD = int.MaxValue;
            for (int i = 0; i < palette.Count; i++)
            {
                uint p = palette[i];
                int dr = r - (int)(p & 0xFF), dg = g - (int)((p >> 8) & 0xFF), db = b - (int)((p >> 16) & 0xFF);
                int d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return (byte)best;
        }

        private static byte[] Lzw(byte[] pixels, int minCode)
        {
            int clear = 1 << minCode, eoi = clear + 1;
            var output = new List<byte>();
            int bitBuf = 0, bitCount = 0;
            int codeSize = minCode + 1;
            var dict = new Dictionary<int, int>();
            int next = eoi + 1;

            void Emit(int code)
            {
                bitBuf |= code << bitCount;
                bitCount += codeSize;
                while (bitCount >= 8)
                {
                    output.Add((byte)(bitBuf & 0xFF));
                    bitBuf >>= 8;
                    bitCount -= 8;
                }
            }

            Emit(clear);
            int prefix = pixels.Length > 0 ? pixels[0] : 0;
            for (int i = 1; i < pixels.Length; i++)
            {
                int k = pixels[i];
                int key = (prefix << 8) | k;
                if (dict.TryGetValue(key, out int code))
                {
                    prefix = code;
                    continue;
                }
                Emit(prefix);
                if (next < 4096)
                {
                    dict[key] = next++;
                    if (next > (1 << codeSize) && codeSize < 12) codeSize++;
                }
                else
                {
                    Emit(clear);
                    dict.Clear();
                    next = eoi + 1;
                    codeSize = minCode + 1;
                }
                prefix = k;
            }
            Emit(prefix);
            Emit(eoi);
            if (bitCount > 0) output.Add((byte)(bitBuf & 0xFF));
            return output.ToArray();
        }
    }
}
