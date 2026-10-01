// Procedural Pixel Creatures - body anchored surface patterns and material styles.
//
// Patterns are evaluated in surface coordinates of the part a pixel belongs to: a coordinate along
// the part (U, pixels from the part's start) and an arc coordinate around it (V, pixels from the
// dorsal line, positive towards the creature's right). Because the coordinates are computed from the
// posed primitive's local frame, marks stay glued to the body during animation and never re-roll.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Palettes
{
    public enum PatternKind : byte
    {
        None,
        Stripes,
        Spots,
        Rosettes,
        Saddle,
        DorsalStripe,
        Mottled,
        Bands,
        Blotches,
    }

    /// <summary>Patterns on insect wings (moths, butterflies, dragonflies), in the wing's own 2D frame.</summary>
    public enum WingPatternKind : byte
    {
        None,
        Eyespot,
        Border,
        Bands,
        Veins,
    }

    public struct SurfacePoint
    {
        public PatternDomain Domain;
        /// <summary>Position along the part in pixels (domain coordinate).</summary>
        public double U;
        /// <summary>Normalised position along the part 0..1 (tip = 1).</summary>
        public double T;
        /// <summary>Angle around the part: 0 dorsal/top, +-pi ventral.</summary>
        public double Theta;
        /// <summary>Arc length around the part in pixels from the dorsal line.</summary>
        public double V;
        /// <summary>Unit local coordinates inside the primitive.</summary>
        public Vec3 Local;
        public int Side;
        /// <summary>True for flat plates (triangles): Local holds barycentric coordinates.</summary>
        public bool Plate;
    }

    public sealed class SurfaceSpec
    {
        public MaterialStyle BodyStyle { get; }
        public PatternKind Pattern { get; }
        /// <summary>Characteristic size of marks in pixels.</summary>
        public double MarkSize { get; }
        /// <summary>0..1 how much of the surface is covered by marks.</summary>
        public double Density { get; }
        /// <summary>True: marks use the dark Marking ramp. False: light Secondary ramp.</summary>
        public bool DarkMarks { get; }
        /// <summary>0..1 extent of the lighter belly (countershading).</summary>
        public double Countershade { get; }
        /// <summary>0..1 length of coloured tips (tail tip, ear tips, socks).</summary>
        public double Tips { get; }
        /// <summary>0..1 lighter muzzle / face mask.</summary>
        public double Muzzle { get; }
        /// <summary>0..1 strength of the surface micro texture (scales, fur strands).</summary>
        public double TextureAmount { get; }
        /// <summary>0..1 length of socks/stockings on the legs.</summary>
        public double Socks { get; init; }
        /// <summary>True: white socks, false: dark stockings.</summary>
        public bool SocksLight { get; init; } = true;
        /// <summary>0..1 dark face mask across the eyes.</summary>
        public double Mask { get; init; }
        /// <summary>0..1 share of large white (piebald) patches.</summary>
        public double Piebald { get; init; }
        /// <summary>Pattern on insect wings (eyespots, border, bands, veins).</summary>
        public WingPatternKind WingPattern { get; init; }
        public ulong Seed { get; }
        public ulong PatternKey { get; }

        private readonly MaterialStyle[] _slotStyles;

        public SurfaceSpec(MaterialStyle bodyStyle, PatternKind pattern, double markSize, double density, bool darkMarks,
            double countershade, double tips, double muzzle, double textureAmount, ulong seed, ulong key,
            MaterialStyle? accentStyle = null, MaterialStyle? membraneStyle = null)
        {
            BodyStyle = bodyStyle; Pattern = pattern; MarkSize = Math.Max(1.5, markSize); Density = DMath.Saturate(density);
            DarkMarks = darkMarks; Countershade = DMath.Saturate(countershade); Tips = DMath.Saturate(tips);
            Muzzle = DMath.Saturate(muzzle); TextureAmount = DMath.Saturate(textureAmount); Seed = seed; PatternKey = key;
            _slotStyles = new MaterialStyle[(int)MaterialSlot.Count];
            _slotStyles[(int)MaterialSlot.Primary] = bodyStyle;
            _slotStyles[(int)MaterialSlot.Secondary] = bodyStyle;
            _slotStyles[(int)MaterialSlot.Marking] = bodyStyle;
            _slotStyles[(int)MaterialSlot.Accent] = accentStyle ?? MaterialStyles.Horn;
            _slotStyles[(int)MaterialSlot.Membrane] = membraneStyle ?? MaterialStyles.Membrane;
            _slotStyles[(int)MaterialSlot.Mouth] = MaterialStyles.Matte;
            _slotStyles[(int)MaterialSlot.Teeth] = MaterialStyles.Glossy;
            _slotStyles[(int)MaterialSlot.Eye] = MaterialStyles.Glossy;
            _slotStyles[(int)MaterialSlot.Glow] = MaterialStyles.Emissive;
            _slotStyles[(int)MaterialSlot.White] = bodyStyle;
            _slotStyles[(int)MaterialSlot.Cloth] = MaterialStyles.Matte;
            _slotStyles[(int)MaterialSlot.Metal] = MaterialStyles.Metal;
            _slotStyles[(int)MaterialSlot.Hair] = MaterialStyles.Fur;
        }

        public MaterialStyle StyleOf(MaterialSlot slot) => _slotStyles[(int)slot];

        public static SurfaceSpec Plain(MaterialStyle style) =>
            new SurfaceSpec(style, PatternKind.None, 3, 0, true, 0.4, 0, 0, 0.3, 1, 1);

        /// <summary>
        /// Applies countershading, tips and marks. Returns the material to use; shadeBias may be changed
        /// for subtle darkening (e.g. mottling).
        /// </summary>
        public MaterialSlot Evaluate(in SurfacePoint spIn, MaterialSlot baseSlot, ref int shadeBias, double scale = 1.0)
        {
            // insect wings carry their own pattern instead of the body marks
            if (WingPattern != WingPatternKind.None && spIn.Domain == PatternDomain.Wing && !spIn.Plate
                && (baseSlot == MaterialSlot.Primary || baseSlot == MaterialSlot.Membrane))
                return WingMark(spIn) ?? baseSlot;
            if (baseSlot != MaterialSlot.Primary) return baseSlot;
            var sp = spIn;
            if (scale > 0 && Math.Abs(scale - 1.0) > 1e-9)
            {
                sp.U /= scale;
                sp.V /= scale;
            }
            var slot = baseSlot;
            double absTheta = Math.Abs(sp.Theta);

            // --- countershading: lighter underside
            if (Countershade > 0.01)
            {
                switch (sp.Domain)
                {
                    case PatternDomain.Body:
                    case PatternDomain.Neck:
                    case PatternDomain.Tail:
                    {
                        // measured from the dorsal line: 180 = belly. The camera looks down at 30 degrees,
                        // so the visible belly band in side views starts around 120-135 degrees.
                        double limit = DMath.Lerp(136.0, 84.0, Countershade);
                        // tails only carry the belly colour near their base
                        if (sp.Domain == PatternDomain.Tail) limit += sp.T * 50.0;
                        if (absTheta * DMath.Rad2Deg > limit) slot = MaterialSlot.Secondary;
                        break;
                    }
                    case PatternDomain.Head:
                    {
                        // throat and lower jaw
                        double limit = DMath.Lerp(150.0, 100.0, Countershade);
                        if (absTheta * DMath.Rad2Deg > limit) slot = MaterialSlot.Secondary;
                        break;
                    }
                }
            }

            // --- lighter muzzle
            if (Muzzle > 0.05 && sp.Domain == PatternDomain.Head && sp.T > DMath.Lerp(1.02, 0.62, Muzzle))
                slot = MaterialSlot.Secondary;

            // --- dark face mask across the eyes (raccoon, badger, husky)
            if (Mask > 0.05 && sp.Domain == PatternDomain.Head && sp.T > 0.26 && sp.T < DMath.Lerp(0.56, 0.82, Mask)
                && absTheta > 0.3 && absTheta < DMath.Lerp(1.25, 1.95, Mask))
                return MaterialSlot.Marking;

            // --- socks / stockings: the lower part of every leg
            if (Socks > 0.05 && sp.Domain == PatternDomain.Limb && sp.T > 1.0 - DMath.Lerp(0.18, 0.62, Socks)
                + PatternNoise.Perlin2(Seed + 77, sp.V * 0.25, 1.7) * 0.05)
                return SocksLight ? MaterialSlot.White : MaterialSlot.Marking;

            // --- coloured tips
            if (Tips > 0.05)
            {
                bool tip = false;
                switch (sp.Domain)
                {
                    case PatternDomain.Tail: tip = sp.T > 1.0 - 0.35 * Tips; break;
                    case PatternDomain.Appendage: tip = sp.T > 1.0 - 0.5 * Tips; break;
                    case PatternDomain.Limb: tip = sp.T > 1.0 - 0.45 * Tips; break;
                }
                if (tip) return DarkMarks ? MaterialSlot.Marking : MaterialSlot.Secondary;
            }

            // --- piebald: large white patches with clean edges, more white towards the legs and the face
            if (Piebald > 0.02 && sp.Domain != PatternDomain.Wing && sp.Domain != PatternDomain.Horn && sp.Domain != PatternDomain.Appendage
                && sp.Domain != PatternDomain.Fin)
            {
                ulong ps = StableHash.Combine(Seed ^ 0x9E3779B1UL, (ulong)sp.Domain);
                double n = PatternNoise.Fbm2(ps, sp.U / 11.0, sp.V / 8.0, 2);
                if (sp.Domain == PatternDomain.Limb) n += (sp.T - 0.35) * 0.5;
                if (sp.Domain == PatternDomain.Head) n += (sp.T - 0.6) * 0.4;
                if (n > DMath.Lerp(0.52, -0.3, Piebald)) return MaterialSlot.White;
            }

            if (slot == MaterialSlot.Secondary) return slot;
            if (Pattern == PatternKind.None || Density <= 0.01) return slot;

            bool mark = EvaluateMarks(sp, absTheta);
            if (mark) return DarkMarks ? MaterialSlot.Marking : MaterialSlot.Secondary;
            return slot;
        }

        /// <summary>
        /// Wing pattern in the wing ellipsoid's unit frame (x: root -1 .. tip +1, z: across the chord),
        /// mirrored between the wings by construction. Returns null for the plain wing colour.
        /// </summary>
        private MaterialSlot? WingMark(in SurfacePoint sp)
        {
            double x = sp.Local.X, z = sp.Local.Z;
            double r = Math.Sqrt(x * x + z * z);
            double j = (StableHash.Combine(Seed, 0x57494E47UL) & 0xFFFF) / 65535.0;
            switch (WingPattern)
            {
                case WingPatternKind.Eyespot:
                {
                    double cx = DMath.Lerp(0.18, 0.45, j);
                    double dx = (x - cx) / 0.34, dz = z / 0.48;
                    double d = Math.Sqrt(dx * dx + dz * dz);
                    // dark pupil with a glint, a ring in the eye colour, a dark outer ring
                    if (d < 0.3) return (dx + dz) < -0.12 && d < 0.2 ? MaterialSlot.White : MaterialSlot.Marking;
                    if (d < 0.7) return MaterialSlot.Eye;
                    if (d < 0.92) return MaterialSlot.Marking;
                    if (r > 0.86) return MaterialSlot.Marking;
                    return null;
                }
                case WingPatternKind.Border:
                {
                    if (r > 0.78)
                    {
                        double a = Math.Atan2(z, x) / DMath.TwoPi * 9 + j;
                        double f = a - Math.Floor(a);
                        return r < 0.93 && f > 0.3 && f < 0.7 ? MaterialSlot.White : MaterialSlot.Marking;
                    }
                    // discal spot in the middle of the wing
                    double dd = Math.Sqrt((x + 0.05) * (x + 0.05) * 9 + z * z * 16);
                    return dd < 1 ? MaterialSlot.Marking : null;
                }
                case WingPatternKind.Bands:
                {
                    double c = DMath.Lerp(-0.25, 0.1, j);
                    if (Math.Abs(x - c) < 0.14 + 0.04 * Math.Sin(z * 5)) return MaterialSlot.Marking;
                    if (x > 0.66) return MaterialSlot.White;
                    if (x > 0.56) return MaterialSlot.Marking;
                    return null;
                }
                case WingPatternKind.Veins:
                {
                    if (r > 0.93) return MaterialSlot.Marking;
                    // pterostigma: a dark cell near the tip
                    if (x > 0.7 && x < 0.84 && Math.Abs(z) > 0.45) return MaterialSlot.Marking;
                    double a = Math.Atan2(z, x + 1.15) / DMath.Pi * 7 + j * 0.5;
                    double f = a - Math.Floor(a);
                    return f < 0.13 ? MaterialSlot.Marking : null;
                }
            }
            return null;
        }

        private bool EvaluateMarks(in SurfacePoint sp, double absTheta)
        {
            // symmetric across the dorsal line
            double v = Math.Abs(sp.V);
            double size = MarkSize;
            switch (Pattern)
            {
                case PatternKind.Stripes:
                {
                    if (sp.Domain != PatternDomain.Body && sp.Domain != PatternDomain.Tail && sp.Domain != PatternDomain.Neck &&
                        !(sp.Domain == PatternDomain.Limb && sp.T < 0.45) && !(sp.Domain == PatternDomain.Head && sp.T < 0.5))
                        return false;
                    if (absTheta > DMath.Lerp(1.5, 2.3, Density)) return false;
                    double period = size * 2.2;
                    // stripes bend slightly and taper towards the belly
                    double warp = PatternNoise.Perlin2(Seed, sp.U * 0.08, v * 0.15) * size * 0.9 + v * 0.18;
                    double s = DMath.Frac((sp.U + warp) / period);
                    double taper = 1.0 - absTheta / 2.4;
                    double duty = DMath.Lerp(0.22, 0.46, Density) * DMath.Saturate(0.35 + taper);
                    return s < duty;
                }
                case PatternKind.Bands:
                {
                    if (sp.Domain == PatternDomain.Tail || sp.Domain == PatternDomain.Appendage)
                    {
                        double period = size * 2.4;
                        return DMath.Frac(sp.U / period) < DMath.Lerp(0.3, 0.5, Density);
                    }
                    if (sp.Domain == PatternDomain.Body && absTheta < 2.2)
                    {
                        double period = size * 3.2;
                        return DMath.Frac(sp.U / period) < DMath.Lerp(0.18, 0.34, Density);
                    }
                    return false;
                }
                case PatternKind.Spots:
                case PatternKind.Rosettes:
                case PatternKind.Blotches:
                {
                    if (sp.Domain != PatternDomain.Body && sp.Domain != PatternDomain.Neck && sp.Domain != PatternDomain.Tail &&
                        !(sp.Domain == PatternDomain.Limb && sp.T < 0.55) && sp.Domain != PatternDomain.Wing && sp.Domain != PatternDomain.Head)
                        return false;
                    if (absTheta > 2.35 && sp.Domain != PatternDomain.Wing) return false;
                    double cell = size * (Pattern == PatternKind.Blotches ? 3.4 : 2.6);
                    ulong seed = StableHash.Combine(Seed, (ulong)sp.Domain);
                    double f1 = PatternNoise.Cellular2(seed, sp.U / cell, v / cell, 0.85, out uint id, out double f2);
                    double present = (id & 0xFFFF) / 65535.0;
                    if (present > DMath.Lerp(0.35, 0.95, Density)) return false;
                    double radius = DMath.Lerp(0.26, 0.40, ((id >> 16) & 0xFF) / 255.0) * (Pattern == PatternKind.Blotches ? 1.25 : 1.0);
                    if (Pattern == PatternKind.Rosettes)
                        return f1 < radius && f1 > radius * 0.45;
                    return f1 < radius;
                }
                case PatternKind.Saddle:
                {
                    if (sp.Domain != PatternDomain.Body) return false;
                    double t = sp.T;
                    double reach = DMath.Lerp(0.9, 1.7, Density);
                    double edge = PatternNoise.Perlin2(Seed, sp.U * 0.12, 3.1) * 0.25;
                    return t > 0.18 && t < 0.86 && absTheta < reach + edge;
                }
                case PatternKind.DorsalStripe:
                {
                    if (sp.Domain != PatternDomain.Body && sp.Domain != PatternDomain.Neck && sp.Domain != PatternDomain.Tail && sp.Domain != PatternDomain.Head)
                        return false;
                    double width = DMath.Lerp(1.1, 2.6, Density) * Math.Max(1.0, size * 0.35);
                    return v < width && absTheta < 1.2;
                }
                case PatternKind.Mottled:
                {
                    if (sp.Domain == PatternDomain.Wing || sp.Domain == PatternDomain.Horn) return false;
                    if (absTheta > 2.4) return false;
                    double n = PatternNoise.Fbm2(StableHash.Combine(Seed, (ulong)sp.Domain), sp.U / (size * 2.0), sp.V / (size * 2.0), 2);
                    return n > DMath.Lerp(0.30, -0.05, Density);
                }
            }
            return false;
        }
    }
}
