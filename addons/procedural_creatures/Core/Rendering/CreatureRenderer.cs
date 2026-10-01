// Procedural Pixel Creatures - CPU pixel renderer.
//
// Pipeline per frame (all on one logical pixel grid, no post scaling):
//   1. Pose -> view space: every bone transform is combined with the creature yaw and the oblique camera.
//   2. Primitives (ellipsoids, round cones, plates) are prepared analytically in view space.
//   3. Groups are rasterized. Inside a group primitives form a soft union: silhouettes get fillets
//      from a clamped distance field, depth and normals are blended with a smooth maximum. Across
//      groups a z-buffer decides visibility (near/far limbs, occlusion in every direction).
//   4. Stylization: body anchored patterns, quantized toon lighting with hue shifted ramps, far side
//      darkening, micro textures, cluster cleanup, interior contours and selective exterior outline.
//   5. Facial features are stamped as designed pixel clusters (eyes react to expression and look).
//   6. Ground shadow and waterline.
// The renderer is deterministic and allocation free after warm up. One instance per thread.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core.Rendering
{
    public sealed class RenderOptions
    {
        /// <summary>Use lighter outline pixels on the lit side of the silhouette ("sel-out").</summary>
        public bool SelectiveOutline = true;
        /// <summary>Draw contour lines where parts overlap other parts.</summary>
        public bool InteriorContours = true;
        /// <summary>Depth gap (pixels) between parts that produces a contour line.</summary>
        public double ContourDepth = 1.6;
        /// <summary>Depth gap inside one smooth group that still produces a contour.</summary>
        public double SelfContourDepth = 5.0;
        /// <summary>Remove isolated pixels and single pixel shade noise.</summary>
        public bool Cleanup = true;
        public bool MicroTexture = true;
        public bool Features = true;
        public bool CastShadow = true;
        /// <summary>Screen-space cast shadows between parts (belly over legs, head over neck, ears, wings).</summary>
        public bool ContactShadows = true;
        /// <summary>Length of the contact shadow ray in pixels (scaled with the creature size).</summary>
        public double ContactShadowLength = 6.0;
        /// <summary>Counts pixels per shade level into RenderStats (diagnostics; costs a little time).</summary>
        public bool CollectShadeStats;
        /// <summary>Collect per stage timings in CreatureRenderer.Profile (small overhead).</summary>
        public bool Profile;

        public static readonly RenderOptions Default = new RenderOptions();
    }

    public sealed class CreatureRenderer
    {
        // ------------------------------------------------------------------ prepared primitive

        private struct PPrim
        {
            public PrimKind Kind;
            public int Source;
            public int Group;
            public MaterialSlot Material;
            public PrimFlags Flags;
            public int Side;
            public int ShadeBias;
            public PatternDomain Domain;
            public double U0, U1, DomainLength;
            public uint FacetSeed;
            public bool Hard;
            // bbox (pixel coords, inclusive)
            public int X0, Y0, X1, Y1;
            // ellipsoid
            public double Cx, Cy, Cz;
            public double Qxx, Qxy, Qxz, Qyy, Qyz, Qzz;
            public double Mxx, Mxy, Myy;
            public double RMin;
            public Mat3 AInv;
            public double REq;
            // round cone
            public Vec3 A, B;
            public double Ra, Rb;
            public double Dx, Dy, H2, CoefA, CoefB;
            public bool Degenerate;
            public double ZTop;
            public Vec3 Up;
            // triangle
            public Vec3 P0, P1, P2;
            public Vec3 N;
            public bool BackFace;
            public double Area2;
        }

        private PPrim[] _pp = new PPrim[64];
        private int _ppCount;
        private int[] _groupList = new int[64];
        private int[] _groupStart = new int[16];
        private int[] _groupCount = new int[16];
        private Xform[] _boneView = new Xform[64];
        private double[] _boneScale = new double[64];

        // G-buffer
        private int _w, _h;
        private float[] _z = Array.Empty<float>();
        private float[] _nx = Array.Empty<float>();
        private float[] _ny = Array.Empty<float>();
        private float[] _nz = Array.Empty<float>();
        private short[] _prim = Array.Empty<short>();
        private byte[] _group = Array.Empty<byte>();
        private byte[] _pflags = Array.Empty<byte>();
        private byte[] _mat = Array.Empty<byte>();
        private sbyte[] _level = Array.Empty<sbyte>();
        private sbyte[] _levelTmp = Array.Empty<sbyte>();
        private byte[] _matTmp = Array.Empty<byte>();
        private byte[] _shadowMask = Array.Empty<byte>();

        private const byte FlagCovered = 1;
        private const byte FlagFillet = 2;
        private const byte FlagBack = 4;
        private const byte FlagThin = 8;
        private const byte FlagContour = 16;
        private const byte FlagFeature = 32;
        private const byte FlagNoOutline = 64;

        private Vec3 _lightView;
        private Vec3 _halfView;
        private Vec3 _lateralView;
        private Vec3 _forwardView;
        private Mat3 _creatureToView;
        private Vec3 _offsetView;
        private Vec3 _groundOffsetView;
        private double _patternScale = 1;
        private int _ox, _oy;

        public RenderOptions Options { get; set; } = RenderOptions.Default;

        /// <summary>Accumulated stage timings in milliseconds (only with Options.Profile).</summary>
        public readonly double[] Profile = new double[9];
        public static readonly string[] ProfileStages = { "clear", "prepare", "raster", "cleanup", "stylize", "shadecleanup", "compose", "features", "shadow" };
        private readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();

        private void Mark(int stage)
        {
            if (!Options.Profile) return;
            Profile[stage] += _sw.Elapsed.TotalMilliseconds;
            _sw.Restart();
        }

        // ------------------------------------------------------------------ entry point

        public RenderStats Render(CreatureAnatomy anatomy, CreaturePalette palette, SurfaceSpec surface, CreaturePose pose,
            in ViewSpec view, PixelFrame frame)
        {
            var stats = new RenderStats();
            if (Options.Profile) _sw.Restart();
            _w = frame.Width;
            _h = frame.Height;
            _ox = frame.OriginX;
            _oy = frame.OriginY;
            EnsureBuffers(_w * _h, anatomy);
            frame.Clear();
            Mark(0);

            var wv = PixelCamera.WorldToView(view.Elevation);
            _creatureToView = wv * PixelCamera.YawRotation(view.Yaw);
            _offsetView = wv.Mul(view.RootOffset);
            _groundOffsetView = wv.Mul(new Vec3(view.RootOffset.X, 0, view.RootOffset.Z));
            _lightView = wv.Mul(PixelCamera.WorldLight).Normalized();
            _halfView = (_lightView + Vec3.UnitZ).Normalized();
            _lateralView = _creatureToView.Mul(Vec3.UnitZ);
            _forwardView = _creatureToView.Mul(Vec3.UnitX);

            _patternScale = anatomy.Metrics.PatternScale;
            PrepareBones(pose.Skeleton);
            PreparePrimitives(anatomy, view.Quality, ref stats);
            Mark(1);
            RasterGroups(anatomy, ref stats);
            WorkRegion(out _dx0, out _dy0, out _dx1, out _dy1);
            Mark(2);
            if (Options.Cleanup) CleanupSilhouette();
            CleanupInterior(anatomy);
            Mark(3);
            Stylize(anatomy, surface);
            if (Options.ContactShadows) ContactShadows(anatomy, ref stats);
            Mark(4);
            if (Options.Cleanup) CleanupShading();
            Mark(5);
            if (Options.CollectShadeStats) CountShades(ref stats);
            Compose(anatomy, palette, frame);
            Mark(6);
            if (Options.Features) DrawFeatures(anatomy, palette, pose, frame);
            Mark(7);
            if (view.DrawShadow && Options.CastShadow) DrawShadow(anatomy, pose, frame);
            Mark(8);
            if (DMath.IsFinite(view.WaterLevel)) ApplyWater(view, frame, palette);

            int covered = 0;
            WorkRegion(out int cx0, out int cy0, out int cx1, out int cy1);
            for (int y = cy0; y <= cy1; y++)
                for (int x = cx0; x <= cx1; x++)
                    if ((_pflags[y * _w + x] & FlagCovered) != 0) covered++;
            stats.PixelsCovered = covered;
            return stats;
        }

        private int _lastW = -1, _lastH = -1;
        private int _dx0, _dy0, _dx1 = -1, _dy1 = -1; // region written by the previous frame

        private void EnsureBuffers(int n, CreatureAnatomy anatomy)
        {
            bool full = false;
            if (_z.Length < n)
            {
                _z = new float[n]; _nx = new float[n]; _ny = new float[n]; _nz = new float[n];
                _prim = new short[n]; _group = new byte[n]; _pflags = new byte[n]; _mat = new byte[n];
                _level = new sbyte[n]; _levelTmp = new sbyte[n]; _matTmp = new byte[n]; _shadowMask = new byte[n];
                full = true;
            }
            if (_w != _lastW || _h != _lastH) full = true;
            _lastW = _w; _lastH = _h;
            if (full)
            {
                for (int i = 0; i < n; i++) ClearPixel(i);
            }
            else if (_dx1 >= _dx0 && _dy1 >= _dy0)
            {
                // only the region touched by the previous frame can be dirty
                for (int y = _dy0; y <= _dy1; y++)
                {
                    int row = y * _w;
                    for (int x = _dx0; x <= _dx1; x++) ClearPixel(row + x);
                }
            }
            _dx0 = _w; _dy0 = _h; _dx1 = -1; _dy1 = -1;
            int bones = anatomy.Skeleton.Count;
            if (_boneView.Length < bones) { _boneView = new Xform[bones]; _boneScale = new double[bones]; }
            int prims = anatomy.Primitives.Count;
            if (_pp.Length < prims) _pp = new PPrim[prims];
            if (_groupList.Length < prims) _groupList = new int[prims];
            int groups = anatomy.Groups.Count;
            if (_groupStart.Length < groups) { _groupStart = new int[groups]; _groupCount = new int[groups]; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearPixel(int i)
        {
            _z[i] = float.NegativeInfinity;
            _prim[i] = -1;
            _group[i] = 255;
            _pflags[i] = 0;
            _level[i] = 0;
            _mat[i] = 0;
        }

        /// <summary>Working region: content bounds grown by one pixel (outlines) and clamped to the canvas.</summary>
        private void WorkRegion(out int x0, out int y0, out int x1, out int y1)
        {
            if (_cx1 < _cx0) { x0 = 0; y0 = 0; x1 = -1; y1 = -1; return; }
            x0 = Math.Max(0, _cx0 - 1); y0 = Math.Max(0, _cy0 - 1);
            x1 = Math.Min(_w - 1, _cx1 + 1); y1 = Math.Min(_h - 1, _cy1 + 1);
        }

        private void PrepareBones(SkeletonPose pose)
        {
            int n = pose.Skeleton.Count;
            for (int i = 0; i < n; i++)
            {
                var w = pose.World[i];
                var basis = _creatureToView * w.Basis;
                _boneView[i] = new Xform(basis, _creatureToView.Mul(w.Origin) + _offsetView);
                double det = Math.Abs(w.Basis.Determinant);
                _boneScale[i] = det > 1e-9 ? DMath.Cbrt(det) : 1.0;
            }
        }

        // ------------------------------------------------------------------ primitive preparation

        private void PreparePrimitives(CreatureAnatomy anatomy, int quality, ref RenderStats stats)
        {
            var prims = anatomy.Primitives;
            _ppCount = 0;
            for (int i = 0; i < prims.Count; i++)
            {
                var d = prims[i];
                if (d.MinQuality > quality) continue;
                ref var p = ref _pp[_ppCount];
                p = default;
                p.Kind = d.Kind;
                p.Source = i;
                p.Group = d.Group;
                p.Material = d.Material;
                p.Flags = d.Flags;
                p.Side = d.Side;
                p.ShadeBias = d.ShadeBias;
                p.Domain = d.Domain;
                p.U0 = d.U0;
                p.U1 = d.U1;
                p.DomainLength = d.DomainLength;
                p.FacetSeed = d.FacetSeed;
                p.Hard = (d.Flags & PrimFlags.Hard) != 0;
                bool ok;
                switch (d.Kind)
                {
                    case PrimKind.Ellipsoid: ok = PrepareEllipsoid(ref p, d); break;
                    case PrimKind.RoundCone: ok = PrepareCone(ref p, d); break;
                    default: ok = PrepareTriangle(ref p, d); break;
                }
                if (!ok)
                {
                    stats.NonFiniteRejected++;
                    continue;
                }
                // the prepared bbox already carries one pixel of margin for the outline
                if (p.X0 < 0) { stats.Overflow = true; stats.OverflowLeft = Math.Max(stats.OverflowLeft, -p.X0); }
                if (p.Y0 < 0) { stats.Overflow = true; stats.OverflowTop = Math.Max(stats.OverflowTop, -p.Y0); }
                if (p.X1 >= _w) { stats.Overflow = true; stats.OverflowRight = Math.Max(stats.OverflowRight, p.X1 + 1 - _w); }
                if (p.Y1 >= _h) { stats.Overflow = true; stats.OverflowBottom = Math.Max(stats.OverflowBottom, p.Y1 + 1 - _h); }
                _ppCount++;
            }
            stats.PrimitivesDrawn = _ppCount;

            // bucket by group (stable order)
            int groups = anatomy.Groups.Count;
            for (int g = 0; g < groups; g++) _groupCount[g] = 0;
            for (int i = 0; i < _ppCount; i++) _groupCount[_pp[i].Group]++;
            int acc = 0;
            for (int g = 0; g < groups; g++) { _groupStart[g] = acc; acc += _groupCount[g]; _groupCount[g] = 0; }
            for (int i = 0; i < _ppCount; i++)
            {
                int g = _pp[i].Group;
                _groupList[_groupStart[g] + _groupCount[g]++] = i;
            }
        }

        private bool PrepareEllipsoid(ref PPrim p, PrimitiveDef d)
        {
            var bv = _boneView[d.BoneA];
            Vec3 c = bv.Point(d.LocalA);
            Mat3 rot = bv.Basis * Mat3.FromQuat(d.LocalRotation);
            Mat3 a = rot.ScaledColumns(d.Radii.X, d.Radii.Y, d.Radii.Z);
            if (Math.Abs(a.Determinant) < 1e-9 || !c.IsFinite) return false;
            Mat3 ainv = a.Inverse();
            // Q = Ainv^T Ainv  -> entries are dot products of Ainv rows. Ainv rows = columns of its transpose.
            Mat3 t = ainv.Transposed;
            Vec3 r0 = t.C0, r1 = t.C1, r2 = t.C2; // columns of transpose = rows of ainv
            // Q_ij = sum_k Ainv_ki Ainv_kj = dot(col_i(Ainv), col_j(Ainv))
            Vec3 c0 = ainv.C0, c1 = ainv.C1, c2 = ainv.C2;
            p.Qxx = Vec3.Dot(c0, c0); p.Qxy = Vec3.Dot(c0, c1); p.Qxz = Vec3.Dot(c0, c2);
            p.Qyy = Vec3.Dot(c1, c1); p.Qyz = Vec3.Dot(c1, c2); p.Qzz = Vec3.Dot(c2, c2);
            _ = r0; _ = r1; _ = r2;
            if (p.Qzz < 1e-12) return false;
            p.Mxx = p.Qxx - p.Qxz * p.Qxz / p.Qzz;
            p.Mxy = p.Qxy - p.Qxz * p.Qyz / p.Qzz;
            p.Myy = p.Qyy - p.Qyz * p.Qyz / p.Qzz;
            p.Cx = c.X; p.Cy = c.Y; p.Cz = c.Z;
            p.AInv = ainv;
            double ex = Math.Sqrt(a.C0.X * a.C0.X + a.C1.X * a.C1.X + a.C2.X * a.C2.X);
            double ey = Math.Sqrt(a.C0.Y * a.C0.Y + a.C1.Y * a.C1.Y + a.C2.Y * a.C2.Y);
            double s = _boneScale[d.BoneA];
            p.RMin = Math.Min(d.Radii.X, Math.Min(d.Radii.Y, d.Radii.Z)) * s;
            p.REq = 0.5 * (d.Radii.Y + d.Radii.Z) * s;
            SetBox(ref p, c.X - ex, c.Y - ey, c.X + ex, c.Y + ey);
            return true;
        }

        private bool PrepareCone(ref PPrim p, PrimitiveDef d)
        {
            var ba = _boneView[d.BoneA];
            var bb = _boneView[d.BoneB];
            Vec3 a = ba.Point(d.LocalA);
            Vec3 b = bb.Point(d.LocalB);
            if (!a.IsFinite || !b.IsFinite) return false;
            double ra = d.RadiusA * _boneScale[d.BoneA];
            double rb = d.RadiusB * _boneScale[d.BoneB];
            p.A = a; p.B = b; p.Ra = ra; p.Rb = rb;
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double h = Math.Sqrt(dx * dx + dy * dy);
            p.H2 = h;
            if (h <= Math.Abs(ra - rb) + 1e-6)
            {
                p.Degenerate = true;
                p.Dx = 1; p.Dy = 0;
            }
            else
            {
                p.Dx = dx / h; p.Dy = dy / h;
                p.CoefB = (ra - rb) / h;
                p.CoefA = Math.Sqrt(Math.Max(0, 1 - p.CoefB * p.CoefB));
            }
            p.ZTop = Math.Max(a.Z + ra, b.Z + rb) + 1.0;
            p.Up = ba.Basis.C1.NormalizedOr(Vec3.UnitY);
            SetBox(ref p, Math.Min(a.X - ra, b.X - rb), Math.Min(a.Y - ra, b.Y - rb), Math.Max(a.X + ra, b.X + rb), Math.Max(a.Y + ra, b.Y + rb));
            return true;
        }

        private const double PlateHalfThickness = 0.42;

        private bool PrepareTriangle(ref PPrim p, PrimitiveDef d)
        {
            Vec3 p0 = _boneView[d.BoneA].Point(d.LocalA);
            Vec3 p1 = _boneView[d.BoneB].Point(d.LocalB);
            Vec3 p2 = _boneView[d.BoneC].Point(d.LocalC);
            if (!p0.IsFinite || !p1.IsFinite || !p2.IsFinite) return false;
            p.P0 = p0; p.P1 = p1; p.P2 = p2;
            Vec3 n = Vec3.Cross(p1 - p0, p2 - p0);
            double len = n.Length;
            n = len > 1e-9 ? n / len : Vec3.UnitZ;
            p.BackFace = n.Z < 0;
            if (p.BackFace) n = -n;
            p.N = n;
            p.Area2 = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
            double t = PlateHalfThickness;
            SetBox(ref p, Math.Min(p0.X, Math.Min(p1.X, p2.X)) - t, Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)) - t,
                Math.Max(p0.X, Math.Max(p1.X, p2.X)) + t, Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)) + t);
            return true;
        }

        private void SetBox(ref PPrim p, double vx0, double vy0, double vx1, double vy1)
        {
            // view (x right, y up) -> pixel (x right, y down)
            p.X0 = (int)Math.Floor(_ox + vx0) - 1;
            p.X1 = (int)Math.Ceiling(_ox + vx1) + 1;
            p.Y0 = (int)Math.Floor(_oy - vy1) - 1;
            p.Y1 = (int)Math.Ceiling(_oy - vy0) + 1;
        }

        // ------------------------------------------------------------------ analytic evaluation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Sd2D(ref PPrim p, double x, double y)
        {
            switch (p.Kind)
            {
                case PrimKind.Ellipsoid:
                {
                    double dx = x - p.Cx, dy = y - p.Cy;
                    double q = p.Mxx * dx * dx + 2 * p.Mxy * dx * dy + p.Myy * dy * dy;
                    if (q < 1e-12) return -p.RMin;
                    double rho = Math.Sqrt(q);
                    double gx = p.Mxx * dx + p.Mxy * dy, gy = p.Mxy * dx + p.Myy * dy;
                    double gl = Math.Sqrt(gx * gx + gy * gy);
                    if (gl < 1e-12) return -p.RMin;
                    return (rho - 1.0) * rho / gl;
                }
                case PrimKind.RoundCone:
                {
                    double px = x - p.A.X, py = y - p.A.Y;
                    if (p.Degenerate)
                    {
                        double d1 = Math.Sqrt(px * px + py * py) - p.Ra;
                        double qx = x - p.B.X, qy = y - p.B.Y;
                        double d2 = Math.Sqrt(qx * qx + qy * qy) - p.Rb;
                        return Math.Min(d1, d2);
                    }
                    double ly = px * p.Dx + py * p.Dy;
                    double lx = Math.Abs(px * p.Dy - py * p.Dx);
                    double k = -p.CoefB * lx + p.CoefA * ly;
                    if (k < 0) return Math.Sqrt(lx * lx + ly * ly) - p.Ra;
                    if (k > p.CoefA * p.H2) return Math.Sqrt(lx * lx + (ly - p.H2) * (ly - p.H2)) - p.Rb;
                    return lx * p.CoefA + ly * p.CoefB - p.Ra;
                }
                default:
                    return SdTriangle(ref p, x, y) - PlateHalfThickness;
            }
        }

        private static double SdTriangle(ref PPrim p, double px, double py)
        {
            double e0x = p.P1.X - p.P0.X, e0y = p.P1.Y - p.P0.Y;
            double e1x = p.P2.X - p.P1.X, e1y = p.P2.Y - p.P1.Y;
            double e2x = p.P0.X - p.P2.X, e2y = p.P0.Y - p.P2.Y;
            double v0x = px - p.P0.X, v0y = py - p.P0.Y;
            double v1x = px - p.P1.X, v1y = py - p.P1.Y;
            double v2x = px - p.P2.X, v2y = py - p.P2.Y;
            double t0 = DMath.Saturate((v0x * e0x + v0y * e0y) / Math.Max(1e-12, e0x * e0x + e0y * e0y));
            double t1 = DMath.Saturate((v1x * e1x + v1y * e1y) / Math.Max(1e-12, e1x * e1x + e1y * e1y));
            double t2 = DMath.Saturate((v2x * e2x + v2y * e2y) / Math.Max(1e-12, e2x * e2x + e2y * e2y));
            double q0x = v0x - e0x * t0, q0y = v0y - e0y * t0;
            double q1x = v1x - e1x * t1, q1y = v1y - e1y * t1;
            double q2x = v2x - e2x * t2, q2y = v2y - e2y * t2;
            double s = Math.Sign(e0x * e2y - e0y * e2x);
            if (s == 0) s = 1;
            double d0 = q0x * q0x + q0y * q0y, s0 = s * (v0x * e0y - v0y * e0x);
            double d1 = q1x * q1x + q1y * q1y, s1 = s * (v1x * e1y - v1y * e1x);
            double d2 = q2x * q2x + q2y * q2y, s2 = s * (v2x * e2y - v2y * e2x);
            double dmin = Math.Min(d0, Math.Min(d1, d2));
            double smin = Math.Min(s0, Math.Min(s1, s2));
            return -Math.Sqrt(dmin) * Math.Sign(smin == 0 ? -1 : smin);
        }

        /// <summary>Front surface depth and normal for a pixel inside the primitive's projection.</summary>
        private static bool Front(ref PPrim p, double x, double y, out double z, out Vec3 n)
        {
            switch (p.Kind)
            {
                case PrimKind.Ellipsoid:
                {
                    double dx = x - p.Cx, dy = y - p.Cy;
                    double bq = p.Qxz * dx + p.Qyz * dy;
                    double cq = p.Qxx * dx * dx + 2 * p.Qxy * dx * dy + p.Qyy * dy * dy - 1.0;
                    double disc = bq * bq - p.Qzz * cq;
                    if (disc < 0) disc = 0;
                    double s = (-bq + Math.Sqrt(disc)) / p.Qzz;
                    z = p.Cz + s;
                    n = new Vec3(p.Qxx * dx + p.Qxy * dy + p.Qxz * s, p.Qxy * dx + p.Qyy * dy + p.Qyz * s, p.Qxz * dx + p.Qyz * dy + p.Qzz * s).NormalizedOr(Vec3.UnitZ);
                    return true;
                }
                case PrimKind.RoundCone:
                    return ConeFront(ref p, x, y, out z, out n);
                default:
                    return PlateFront(ref p, x, y, out z, out n);
            }
        }

        private static bool ConeFront(ref PPrim p, double x, double y, out double z, out Vec3 n)
        {
            // Exact ray/round-cone intersection (after Inigo Quilez), ray from the viewer along -z.
            Vec3 ro = new Vec3(x, y, p.ZTop);
            Vec3 rd = new Vec3(0, 0, -1);
            Vec3 ba = p.B - p.A;
            Vec3 oa = ro - p.A;
            Vec3 ob = ro - p.B;
            double ra = p.Ra, rb = p.Rb;
            double rr = ra - rb;
            double m0 = Vec3.Dot(ba, ba);
            double m1 = Vec3.Dot(ba, oa);
            double m2 = -ba.Z;
            double m3 = -oa.Z;
            double m5 = Vec3.Dot(oa, oa);
            double m6 = -ob.Z;
            double m7 = Vec3.Dot(ob, ob);
            double d2 = m0 - rr * rr;
            if (d2 > 1e-9)
            {
                double k2 = d2 - m2 * m2;
                double k1 = d2 * m3 - m1 * m2 + m2 * rr * ra;
                double k0 = d2 * m5 - m1 * m1 + m1 * rr * ra * 2.0 - m0 * ra * ra;
                double h = k1 * k1 - k0 * k2;
                if (h >= 0 && Math.Abs(k2) > 1e-9)
                {
                    double t = (-Math.Sqrt(h) - k1) / k2;
                    double yy = m1 - ra * rr + t * m2;
                    if (yy > 0 && yy < d2 && t >= 0)
                    {
                        z = p.ZTop - t;
                        n = (d2 * (oa + t * rd) - ba * yy).NormalizedOr(Vec3.UnitZ);
                        return true;
                    }
                }
            }
            double best = double.MaxValue;
            n = Vec3.UnitZ;
            double h1 = m3 * m3 - m5 + ra * ra;
            if (h1 > 0)
            {
                double t = -m3 - Math.Sqrt(h1);
                if (t < best) { best = t; n = ((oa + t * rd) / ra).NormalizedOr(Vec3.UnitZ); }
            }
            double h2 = m6 * m6 - m7 + rb * rb;
            if (h2 > 0)
            {
                double t = -m6 - Math.Sqrt(h2);
                if (t < best) { best = t; n = ((ob + t * rd) / rb).NormalizedOr(Vec3.UnitZ); }
            }
            if (best == double.MaxValue)
            {
                // grazing pixel on the 2D silhouette edge: use the rim
                RimCone(ref p, x, y, out z, out n);
                return true;
            }
            z = p.ZTop - best;
            return true;
        }

        private static bool PlateFront(ref PPrim p, double x, double y, out double z, out Vec3 n)
        {
            n = p.N;
            double area = p.Area2;
            if (Math.Abs(area) > 0.25)
            {
                double l1 = ((x - p.P0.X) * (p.P2.Y - p.P0.Y) - (y - p.P0.Y) * (p.P2.X - p.P0.X)) / area;
                double l2 = ((p.P1.X - p.P0.X) * (y - p.P0.Y) - (p.P1.Y - p.P0.Y) * (x - p.P0.X)) / area;
                double l0 = 1 - l1 - l2;
                if (l0 < 0) l0 = 0;
                if (l1 < 0) l1 = 0;
                if (l2 < 0) l2 = 0;
                double s = l0 + l1 + l2;
                if (s < 1e-9) s = 1;
                z = (l0 * p.P0.Z + l1 * p.P1.Z + l2 * p.P2.Z) / s;
                return true;
            }
            // edge-on plate: depth of the closest point on the longest edge
            z = ClosestEdgeDepth(p.P0, p.P1, x, y, out double d01);
            double z12 = ClosestEdgeDepth(p.P1, p.P2, x, y, out double d12);
            double z20 = ClosestEdgeDepth(p.P2, p.P0, x, y, out double d20);
            if (d12 < d01) { z = z12; d01 = d12; }
            if (d20 < d01) z = z20;
            return true;
        }

        private static double ClosestEdgeDepth(Vec3 a, Vec3 b, double x, double y, out double dist)
        {
            double ex = b.X - a.X, ey = b.Y - a.Y;
            double t = DMath.Saturate(((x - a.X) * ex + (y - a.Y) * ey) / Math.Max(1e-12, ex * ex + ey * ey));
            double cx = a.X + ex * t, cy = a.Y + ey * t;
            dist = DMath.Sq(cx - x) + DMath.Sq(cy - y);
            return a.Z + (b.Z - a.Z) * t;
        }

        /// <summary>Depth and normal of the silhouette rim nearest to a pixel outside the primitive (for fillets).</summary>
        private static void Rim(ref PPrim p, double x, double y, out double z, out Vec3 n)
        {
            switch (p.Kind)
            {
                case PrimKind.Ellipsoid:
                {
                    double dx = x - p.Cx, dy = y - p.Cy;
                    z = p.Cz - (p.Qxz * dx + p.Qyz * dy) / p.Qzz;
                    double gx = p.Mxx * dx + p.Mxy * dy, gy = p.Mxy * dx + p.Myy * dy;
                    n = new Vec3(gx, gy, 0).NormalizedOr(Vec3.UnitZ);
                    return;
                }
                case PrimKind.RoundCone:
                    RimCone(ref p, x, y, out z, out n);
                    return;
                default:
                    PlateFront(ref p, x, y, out z, out n);
                    return;
            }
        }

        private static void RimCone(ref PPrim p, double x, double y, out double z, out Vec3 n)
        {
            double px = x - p.A.X, py = y - p.A.Y;
            double t = p.H2 > 1e-9 ? DMath.Saturate((px * p.Dx + py * p.Dy) / p.H2) : 0;
            z = p.A.Z + (p.B.Z - p.A.Z) * t;
            double cx = p.A.X + (p.B.X - p.A.X) * t, cy = p.A.Y + (p.B.Y - p.A.Y) * t;
            n = new Vec3(x - cx, y - cy, 0).NormalizedOr(Vec3.UnitZ);
        }

        // ------------------------------------------------------------------ group rasterization

        // per group accumulation buffers (sized to the group's bounding box)
        private byte[] _aInside = Array.Empty<byte>();
        private byte[] _aFlags = Array.Empty<byte>();
        private double[] _aZ = Array.Empty<double>();
        private double[] _aNx = Array.Empty<double>(), _aNy = Array.Empty<double>(), _aNz = Array.Empty<double>();
        private int[] _aDom = Array.Empty<int>();
        private double[] _aField = Array.Empty<double>();
        private double[] _aFz = Array.Empty<double>(), _aFw = Array.Empty<double>();
        private double[] _aFnx = Array.Empty<double>(), _aFny = Array.Empty<double>(), _aFnz = Array.Empty<double>();
        private double[] _aFdomW = Array.Empty<double>();
        private int[] _aFdom = Array.Empty<int>();
        // content bounds of covered pixels (inclusive)
        private int _cx0, _cy0, _cx1, _cy1;

        private void EnsureAccum(int n)
        {
            if (_aInside.Length >= n) return;
            int m = Math.Max(n, _aInside.Length * 2);
            _aInside = new byte[m]; _aFlags = new byte[m]; _aZ = new double[m];
            _aNx = new double[m]; _aNy = new double[m]; _aNz = new double[m]; _aDom = new int[m];
            _aField = new double[m]; _aFz = new double[m]; _aFw = new double[m];
            _aFnx = new double[m]; _aFny = new double[m]; _aFnz = new double[m]; _aFdomW = new double[m]; _aFdom = new int[m];
        }

        /// <summary>
        /// Rasterizes every group: primitives are visited one by one over their own bounding boxes and
        /// accumulate into per-pixel group buffers (soft union field, smooth max depth, blended normal).
        /// A resolve pass then z-tests the group against the frame. The per-pixel update order equals the
        /// primitive order, so the result is independent of how pixels are traversed.
        /// </summary>
        private void RasterGroups(CreatureAnatomy anatomy, ref RenderStats stats)
        {
            var groups = anatomy.Groups;
            _cx0 = _w; _cy0 = _h; _cx1 = -1; _cy1 = -1;
            const byte AccBack = 1, AccThin = 2;
            for (int g = 0; g < groups.Count; g++)
            {
                int count = _groupCount[g];
                if (count == 0) continue;
                int start = _groupStart[g];
                var grp = groups[g];
                double k = grp.BlendRadius;
                double invTwoK = k > 0 ? 1.0 / (2.0 * k) : 0;
                int pad = (int)Math.Ceiling(k);
                int gx0 = int.MaxValue, gy0 = int.MaxValue, gx1 = int.MinValue, gy1 = int.MinValue;
                for (int j = 0; j < count; j++)
                {
                    ref var p = ref _pp[_groupList[start + j]];
                    gx0 = Math.Min(gx0, p.X0 - pad); gy0 = Math.Min(gy0, p.Y0 - pad);
                    gx1 = Math.Max(gx1, p.X1 + pad); gy1 = Math.Max(gy1, p.Y1 + pad);
                }
                gx0 = Math.Max(0, gx0); gy0 = Math.Max(0, gy0);
                gx1 = Math.Min(_w - 1, gx1); gy1 = Math.Min(_h - 1, gy1);
                if (gx1 < gx0 || gy1 < gy0) continue;
                int gw = gx1 - gx0 + 1, gh = gy1 - gy0 + 1;
                int gn = gw * gh;
                EnsureAccum(gn);
                Array.Clear(_aInside, 0, gn);
                Array.Clear(_aField, 0, gn);
                Array.Clear(_aFw, 0, gn);
                Array.Clear(_aFz, 0, gn);
                Array.Clear(_aFnx, 0, gn);
                Array.Clear(_aFny, 0, gn);
                Array.Clear(_aFnz, 0, gn);
                Array.Clear(_aFdomW, 0, gn);
                for (int i = 0; i < gn; i++) { _aFdom[i] = -1; _aDom[i] = -1; }

                for (int j = 0; j < count; j++)
                {
                    int pi = _groupList[start + j];
                    ref var p = ref _pp[pi];
                    bool blend = k > 0 && !p.Hard;
                    int ppad = blend ? pad : 0;
                    int x0 = Math.Max(gx0, p.X0 - ppad), x1 = Math.Min(gx1, p.X1 + ppad);
                    int y0 = Math.Max(gy0, p.Y0 - ppad), y1 = Math.Min(gy1, p.Y1 + ppad);
                    byte pflag = (byte)((p.Kind == PrimKind.Triangle && p.BackFace ? AccBack : 0) | ((p.Flags & PrimFlags.Thin) != 0 ? AccThin : 0));
                    for (int py = y0; py <= y1; py++)
                    {
                        double vy = _oy - (py + 0.5);
                        int row = (py - gy0) * gw - gx0;
                        for (int px = x0; px <= x1; px++)
                        {
                            double vx = px + 0.5 - _ox;
                            int a = row + px;
                            stats.PixelsTested++;
                            double sd = Sd2D(ref p, vx, vy);
                            if (sd <= 0)
                            {
                                Front(ref p, vx, vy, out double z, out Vec3 n);
                                if (_aInside[a] == 0)
                                {
                                    _aInside[a] = 1;
                                    _aZ[a] = z; _aNx[a] = n.X; _aNy[a] = n.Y; _aNz[a] = n.Z; _aDom[a] = pi;
                                    _aFlags[a] = pflag;
                                }
                                else if (k <= 0 || p.Hard || _pp[_aDom[a]].Hard)
                                {
                                    if (z > _aZ[a])
                                    {
                                        _aZ[a] = z; _aNx[a] = n.X; _aNy[a] = n.Y; _aNz[a] = n.Z; _aDom[a] = pi;
                                        _aFlags[a] = pflag;
                                    }
                                }
                                else
                                {
                                    double zAcc = _aZ[a];
                                    double h = DMath.Saturate(0.5 + 0.5 * (z - zAcc) / k);
                                    _aZ[a] = zAcc + (z - zAcc) * h + k * h * (1 - h);
                                    _aNx[a] += (n.X - _aNx[a]) * h; _aNy[a] += (n.Y - _aNy[a]) * h; _aNz[a] += (n.Z - _aNz[a]) * h;
                                    if (h > 0.5)
                                    {
                                        _aDom[a] = pi;
                                        _aFlags[a] = pflag;
                                    }
                                }
                            }
                            if (blend && sd < k)
                            {
                                double gi = 0.5 - sd * invTwoK;
                                if (gi > 1) gi = 1;
                                if (gi > 0)
                                {
                                    _aField[a] += gi;
                                    if (sd > 0)
                                    {
                                        Rim(ref p, vx, vy, out double zr, out Vec3 nr);
                                        _aFz[a] += gi * zr; _aFw[a] += gi;
                                        _aFnx[a] += gi * nr.X; _aFny[a] += gi * nr.Y; _aFnz[a] += gi * nr.Z;
                                        if (gi > _aFdomW[a]) { _aFdomW[a] = gi; _aFdom[a] = pi; }
                                    }
                                }
                            }
                        }
                    }
                }

                // resolve: z-test the group against what is already in the frame
                double bias = grp.DepthBias;
                byte noOutline = (grp.Flags & GroupFlags.NoOutline) != 0 ? FlagNoOutline : (byte)0;
                for (int py = gy0; py <= gy1; py++)
                {
                    int row = (py - gy0) * gw;
                    for (int px = gx0; px <= gx1; px++)
                    {
                        int a = row + (px - gx0);
                        double zAcc, nX, nY, nZ;
                        int dom;
                        byte extra = 0, af;
                        if (_aInside[a] != 0)
                        {
                            zAcc = _aZ[a]; nX = _aNx[a]; nY = _aNy[a]; nZ = _aNz[a]; dom = _aDom[a];
                            af = _aFlags[a];
                        }
                        else
                        {
                            double fw = _aFw[a];
                            if (_aField[a] < 0.5 || fw <= 0 || _aFdom[a] < 0) continue;
                            zAcc = _aFz[a] / fw;
                            nX = _aFnx[a]; nY = _aFny[a]; nZ = _aFnz[a] + 0.35 * fw;
                            dom = _aFdom[a];
                            extra = FlagFillet;
                            af = 0;
                        }
                        double zt = zAcc + bias;
                        int idx = py * _w + px;
                        if (zt <= _z[idx]) continue;
                        double nl = Math.Sqrt(nX * nX + nY * nY + nZ * nZ);
                        if (nl < 1e-9) { nX = 0; nY = 0; nZ = 1; nl = 1; }
                        _z[idx] = (float)zt;
                        _nx[idx] = (float)(nX / nl);
                        _ny[idx] = (float)(nY / nl);
                        _nz[idx] = (float)(nZ / nl);
                        _prim[idx] = (short)dom;
                        _group[idx] = (byte)g;
                        byte f = (byte)(FlagCovered | extra | noOutline);
                        if ((af & AccBack) != 0) f |= FlagBack;
                        if ((af & AccThin) != 0) f |= FlagThin;
                        _pflags[idx] = f;
                        if (px < _cx0) _cx0 = px;
                        if (px > _cx1) _cx1 = px;
                        if (py < _cy0) _cy0 = py;
                        if (py > _cy1) _cy1 = py;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ silhouette cleanup

        private void CleanupSilhouette()
        {
            // 1. remove isolated specks (fewer than one covered 4-neighbour), keep thin features
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
            {
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0 || (_pflags[i] & FlagThin) != 0) continue;
                    int n = CoveredNeighbours4(x, y);
                    if (n == 0) Uncover(i);
                }
            }
            // 2. fill single pixel holes and notches surrounded on three sides
            for (int y = Math.Max(1, ry0); y <= Math.Min(_h - 2, ry1); y++)
            {
                for (int x = Math.Max(1, rx0); x <= Math.Min(_w - 2, rx1); x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) != 0) continue;
                    int n = CoveredNeighbours4(x, y);
                    if (n < 3) continue;
                    // only fill when all covering neighbours belong to the same group (never bridge two parts)
                    int src = -1, grp = -1;
                    bool sameGroup = true;
                    int[] ox = { 1, -1, 0, 0 };
                    int[] oy = { 0, 0, 1, -1 };
                    float bestZ = float.NegativeInfinity;
                    for (int k = 0; k < 4; k++)
                    {
                        int j = (y + oy[k]) * _w + x + ox[k];
                        if ((_pflags[j] & FlagCovered) == 0) continue;
                        if (grp < 0) grp = _group[j];
                        else if (_group[j] != grp) sameGroup = false;
                        if (_z[j] > bestZ) { bestZ = _z[j]; src = j; }
                    }
                    if (!sameGroup || src < 0) continue;
                    CopyPixel(src, i);
                }
            }
        }

        /// <summary>
        /// Interior parts (mouth insides) may only show inside the creature: pixels of interior groups
        /// that touch the background are peeled away (two passes), so an open mouth shows a recessed
        /// dark interior framed by the jaws, and a closed mouth never leaks below the head.
        /// </summary>
        private void CleanupInterior(CreatureAnatomy anatomy)
        {
            var groups = anatomy.Groups;
            bool any = false;
            for (int g = 0; g < groups.Count; g++) if ((groups[g].Flags & GroupFlags.Interior) != 0) { any = true; break; }
            if (!any) return;
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = ry0; y <= ry1; y++)
                    for (int x = rx0; x <= rx1; x++) _matTmp[y * _w + x] = 0;
                for (int y = ry0; y <= ry1; y++)
                {
                    for (int x = rx0; x <= rx1; x++)
                    {
                        int i = y * _w + x;
                        if ((_pflags[i] & FlagCovered) == 0) continue;
                        if ((groups[_group[i]].Flags & GroupFlags.Interior) == 0) continue;
                        if (CoveredNeighbours4(x, y) < 4) _matTmp[i] = 1;
                    }
                }
                for (int y = ry0; y <= ry1; y++)
                    for (int x = rx0; x <= rx1; x++)
                        if (_matTmp[y * _w + x] != 0) Uncover(y * _w + x);
            }
        }

        private int CoveredNeighbours4(int x, int y)
        {
            int n = 0;
            if (x > 0 && (_pflags[y * _w + x - 1] & FlagCovered) != 0) n++;
            if (x < _w - 1 && (_pflags[y * _w + x + 1] & FlagCovered) != 0) n++;
            if (y > 0 && (_pflags[(y - 1) * _w + x] & FlagCovered) != 0) n++;
            if (y < _h - 1 && (_pflags[(y + 1) * _w + x] & FlagCovered) != 0) n++;
            return n;
        }

        private void Uncover(int i)
        {
            _pflags[i] = 0;
            _z[i] = float.NegativeInfinity;
            _prim[i] = -1;
            _group[i] = 255;
        }

        private void CopyPixel(int src, int dst)
        {
            _z[dst] = _z[src];
            _nx[dst] = _nx[src]; _ny[dst] = _ny[src]; _nz[dst] = _nz[src];
            _prim[dst] = _prim[src];
            _group[dst] = _group[src];
            _pflags[dst] = (byte)(_pflags[src] | FlagFillet);
        }

        // ------------------------------------------------------------------ stylization

        private void Stylize(CreatureAnatomy anatomy, SurfaceSpec surface)
        {
            var groups = anatomy.Groups;
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
            {
                double vy = _oy - (y + 0.5);
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0) continue;
                    ref var p = ref _pp[_prim[i]];
                    double vx = x + 0.5 - _ox;
                    Vec3 n = new Vec3(_nx[i], _ny[i], _nz[i]);
                    var grp = groups[_group[i]];
                    MaterialSlot slot = p.Material;
                    int bias = p.ShadeBias;

                    SurfacePoint sp = default;
                    bool hasSurface = p.Domain != PatternDomain.None;
                    if (hasSurface)
                    {
                        sp = SurfaceAt(ref p, vx, vy, _z[i] - grp.DepthBias);
                        if ((p.Flags & PrimFlags.NoPattern) == 0) slot = surface.Evaluate(sp, slot, ref bias, _patternScale);
                    }

                    var style = surface.StyleOf(slot);
                    if ((p.Flags & PrimFlags.Faceted) != 0 || style.Faceted) n = FacetNormal(ref p, n);
                    int level;
                    if ((p.Flags & PrimFlags.Emissive) != 0 || slot == MaterialSlot.Glow)
                    {
                        level = n.Z > 0.55 ? Shade.Light : Shade.Base;
                    }
                    else
                    {
                        double ndl = Vec3.Dot(n, _lightView);
                        double ndh = Vec3.Dot(n, _halfView);
                        if (Options.MicroTexture && hasSurface && slot == MaterialSlot.Hair)
                        {
                            // hair and beards always show their tufts
                            ndl += TextureLightOffset(style.Texture, surface, sp, _patternScale, Math.Max(0.85, surface.TextureAmount));
                        }
                        else if (Options.MicroTexture && hasSurface && surface.TextureAmount > 0.05 &&
                            (slot == MaterialSlot.Primary || slot == MaterialSlot.Secondary || slot == MaterialSlot.Marking || slot == MaterialSlot.White))
                        {
                            // structured modulation of the lighting term: band edges become tufts,
                            // scales or plates instead of random speckles
                            ndl += TextureLightOffset(style.Texture, surface, sp, _patternScale);
                        }
                        bool seam = false, scutes = (p.Flags & PrimFlags.Scutes) != 0 && p.Kind == PrimKind.Ellipsoid && hasSurface;
                        if (scutes) ndl += ScuteOffset(ref p, sp, out seam);
                        level = style.Level(ndl, ndh);
                        if (level == Shade.Highlight && ((_pflags[i] & FlagFillet) != 0 || scutes)) level = Shade.Light;
                        if (seam) level = Math.Min(level, Shade.Shadow) - 1;
                    }
                    level += bias;
                    if ((_pflags[i] & FlagBack) != 0) level -= 1;
                    if ((p.Flags & PrimFlags.Translucent) != 0 && (_pflags[i] & FlagBack) != 0) level += 1;
                    if (grp.Side != 0 && (grp.Flags & GroupFlags.NoFarShade) == 0)
                    {
                        double near = _lateralView.Z * grp.Side;
                        if (near < -0.3) level -= 1;
                    }
                    if ((grp.Flags & GroupFlags.CreaseShade) != 0 && (_pflags[i] & FlagFillet) != 0 && level > Shade.Shadow) level -= 1;


                    if (level < Shade.Deep) level = Shade.Deep;
                    if (level > Shade.Highlight) level = Shade.Highlight;
                    _mat[i] = (byte)slot;
                    _level[i] = (sbyte)level;
                }
            }
        }

        /// <summary>
        /// Screen-space contact shadows: a short ray from every lit pixel towards the key light is marched
        /// through the depth buffer. When it passes behind another visible part (a different primitive,
        /// within a plausible thickness), the pixel lies in that part's shadow and drops one shade level
        /// (never below Shadow). Bellies shade the tops of the legs, heads the neck, ears the skull.
        /// </summary>
        private void ContactShadows(CreatureAnatomy anatomy, ref RenderStats stats)
        {
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            Vec3 l = _lightView;
            double size = DMath.Clamp(_patternScale, 0.6, 1.8);
            double maxT = Options.ContactShadowLength * size;
            double thickness = 18.0 * size;
            int shaded = 0;
            for (int y = ry0; y <= ry1; y++)
            {
                double vy = _oy - (y + 0.5);
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0 || _level[i] < Shade.Base) continue;
                    var slot = (MaterialSlot)_mat[i];
                    if (slot == MaterialSlot.Glow || slot == MaterialSlot.Eye) continue;
                    ref var p = ref _pp[_prim[i]];
                    if ((p.Flags & PrimFlags.Emissive) != 0) continue;
                    double vx = x + 0.5 - _ox, z = _z[i];
                    int prim = _prim[i];
                    bool hit = false;
                    for (double t = 1.5; t <= maxT && !hit; t += 1.0)
                    {
                        int sx = (int)Math.Floor(vx + l.X * t + _ox), sy = (int)Math.Floor(_oy - (vy + l.Y * t));
                        if (sx < 0 || sy < 0 || sx >= _w || sy >= _h) break;
                        int j = sy * _w + sx;
                        if ((_pflags[j] & FlagCovered) == 0 || _prim[j] == prim) continue;
                        double gap = _z[j] - (z + l.Z * t);
                        hit = gap > 1.0 && gap < thickness;
                    }
                    if (!hit) continue;
                    _level[i] = (sbyte)Math.Max(Shade.Shadow, _level[i] - 1);
                    shaded++;
                }
            }
            stats.ContactShadowPixels = shaded;
        }

        private void CountShades(ref RenderStats stats)
        {
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0) continue;
                    switch (_level[i])
                    {
                        case Shade.Highlight: stats.HighlightPixels++; break;
                        case Shade.Light: stats.LightPixels++; break;
                        case Shade.Base: stats.BasePixels++; break;
                        case Shade.Shadow: stats.ShadowPixels++; break;
                        default: stats.DeepPixels++; break;
                    }
                }
        }

        private SurfacePoint SurfaceAt(ref PPrim p, double vx, double vy, double z)
        {
            var sp = new SurfacePoint { Domain = p.Domain };
            Vec3 pt = new Vec3(vx, vy, z);
            double len = Math.Max(1.0, p.DomainLength);
            switch (p.Kind)
            {
                case PrimKind.Ellipsoid:
                {
                    Vec3 q = p.AInv.Mul(pt - new Vec3(p.Cx, p.Cy, p.Cz));
                    double t = DMath.Saturate(q.X * 0.5 + 0.5);
                    double tn = DMath.Lerp(p.U0, p.U1, t);
                    sp.T = tn;
                    sp.U = tn * len;
                    sp.Theta = DMath.Atan2(q.Z, q.Y);
                    double ring = Math.Sqrt(Math.Max(0, 1 - q.X * q.X));
                    sp.V = sp.Theta * p.REq * Math.Max(0.35, ring);
                    sp.Local = q;
                    sp.Side = q.Z >= 0 ? 1 : -1;
                    break;
                }
                case PrimKind.RoundCone:
                {
                    Vec3 axis = p.B - p.A;
                    double al = axis.Length;
                    Vec3 dir = al > 1e-9 ? axis / al : Vec3.UnitX;
                    Vec3 rel = pt - p.A;
                    double along = Vec3.Dot(rel, dir);
                    double t = al > 1e-9 ? DMath.Saturate(along / al) : 0;
                    double tn = DMath.Lerp(p.U0, p.U1, t);
                    sp.T = tn;
                    sp.U = tn * len;
                    Vec3 perp = rel - dir * along;
                    Vec3 up = (p.Up - dir * Vec3.Dot(p.Up, dir)).NormalizedOr(Vec3.UnitY);
                    Vec3 side = Vec3.Cross(dir, up);
                    double cu = Vec3.Dot(perp, up), cs = Vec3.Dot(perp, side);
                    sp.Theta = DMath.Atan2(cs, cu);
                    double r = DMath.Lerp(p.Ra, p.Rb, t);
                    sp.V = sp.Theta * r;
                    sp.Local = new Vec3(t * 2 - 1, cu / Math.Max(0.5, r), cs / Math.Max(0.5, r));
                    sp.Side = cs >= 0 ? 1 : -1;
                    break;
                }
                default:
                {
                    double area = p.Area2;
                    double l1 = 0, l2 = 0;
                    if (Math.Abs(area) > 0.25)
                    {
                        l1 = ((vx - p.P0.X) * (p.P2.Y - p.P0.Y) - (vy - p.P0.Y) * (p.P2.X - p.P0.X)) / area;
                        l2 = ((p.P1.X - p.P0.X) * (vy - p.P0.Y) - (p.P1.Y - p.P0.Y) * (vx - p.P0.X)) / area;
                    }
                    double t = DMath.Saturate(l1 + l2);
                    double tn = DMath.Lerp(p.U0, p.U1, t);
                    sp.T = tn;
                    sp.U = tn * len;
                    sp.V = (l2 - l1) * len * 0.5;
                    sp.Theta = 0;
                    sp.Local = new Vec3(l1, l2, 0);
                    sp.Side = 1;
                    sp.Plate = true;
                    break;
                }
            }
            return sp;
        }

        /// <summary>
        /// Shell plates: a jittered hexagonal Voronoi tiling over the top view of the ellipsoid (unit sphere
        /// coordinates, so the plates stay glued to the shell in every pose and direction). Plate centres
        /// are lit a little more (each scute is convex), seams between plates become dark lines about one
        /// pixel wide.
        /// </summary>
        private static double ScuteOffset(ref PPrim p, in SurfacePoint sp, out bool seam)
        {
            seam = false;
            Vec3 q = sp.Local;
            if (q.Y < -0.25) return 0;               // underside / rim: no plates
            const double cells = 2.3;                // plates per unit radius
            uint seed = p.FacetSeed == 0 ? (uint)(p.Source * 2654435761u) : p.FacetSeed;
            double gx = q.X * cells, gz = q.Z * cells * 1.15;
            int cx = (int)Math.Floor(gx), cz = (int)Math.Floor(gz);
            double f1 = 1e9, f2 = 1e9;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int ix = cx + dx, iz = cz + dz;
                    // hexagonal rows with a little seeded jitter
                    double ox = ix + ((iz & 1) != 0 ? 0.5 : 0.0) + (StableHash.Hash01(seed, ix, iz) - 0.5) * 0.35;
                    double oz = iz + 0.5 + (StableHash.Hash01(seed, ix, iz + 7919) - 0.5) * 0.35;
                    double ddx = gx - ox, ddz = gz - oz;
                    double d = Math.Sqrt(ddx * ddx + ddz * ddz);
                    if (d < f1) { f2 = f1; f1 = d; }
                    else if (d < f2) f2 = d;
                }
            }
            // seam width: about one pixel on screen, measured in cell units
            double pxToCell = cells / Math.Max(2.0, p.REq);
            if (f2 - f1 < 1.15 * pxToCell) { seam = true; return 0; }
            return 0.16 * (1.0 - DMath.Saturate(f1 / 0.62)) - 0.04;
        }

        private Vec3 FacetNormal(ref PPrim p, Vec3 n)
        {
            // snap the normal to one of a few seeded facet directions per primitive -> chiselled planes
            uint seed = p.FacetSeed == 0 ? (uint)(p.Source * 2654435761u) : p.FacetSeed;
            Vec3 best = n;
            double bestDot = -2;
            for (int k = 0; k < 7; k++)
            {
                double a = StableHash.Hash01(seed, k, 1) * DMath.TwoPi;
                double e = StableHash.Hash01(seed, k, 2) * 1.2 - 0.2;
                DMath.SinCos(a, out double sa, out double ca);
                DMath.SinCos(e, out double se, out double ce);
                Vec3 f = new Vec3(ca * ce, se, sa * ce);
                // facet directions live in creature space so they rotate with the body
                f = _creatureToView.Mul(f);
                double d = Vec3.Dot(f, n);
                if (d > bestDot) { bestDot = d; best = f; }
            }
            if (Vec3.Dot(best, n) < 0.55) return n;
            Vec3 m = (best * 0.8 + n * 0.2).Normalized();
            return m.Z < 0.05 ? n : m;
        }

        /// <summary>Lighting offset of the surface micro texture, anchored in surface coordinates.</summary>
        private static double TextureLightOffset(SurfaceTexture texture, SurfaceSpec surface, in SurfacePoint sp, double scale, double amount = -1)
        {
            double amt = amount >= 0 ? amount : surface.TextureAmount;
            double u = sp.U / scale, v = Math.Abs(sp.V) / scale;
            switch (texture)
            {
                case SurfaceTexture.Fur:
                {
                    // saw-tooth band edges: fur tufts that point down the body
                    double period = 3.0;
                    double t = DMath.Frac(u / period + 0.37 * Math.Floor(v / 3.0));
                    double tri = Math.Abs(t * 2 - 1);
                    return (tri - 0.5) * 0.30 * amt;
                }
                case SurfaceTexture.Scales:
                {
                    double cell = 3.2;
                    double row = Math.Floor(v / cell);
                    double off = (((long)row & 1) == 0) ? 0 : 0.5;
                    double fu = DMath.Frac(u / cell + off) - 0.5;
                    double fv = DMath.Frac(v / cell) - 0.5;
                    double d = Math.Sqrt(fu * fu * 1.2 + fv * fv);
                    return (0.28 - d) * 0.55 * amt;
                }
                case SurfaceTexture.Plates:
                {
                    double period = 4.6;
                    double f = DMath.Frac(u / period);
                    return (f < 0.22 ? -0.32 : 0.06) * amt;
                }
                case SurfaceTexture.Feathers:
                {
                    double cell = 3.8;
                    double row = Math.Floor(u / cell);
                    double off = (((long)row & 1) == 0) ? 0 : 0.5;
                    double fv = DMath.Frac(v / cell + off) - 0.5;
                    double fu = DMath.Frac(u / cell);
                    double scallop = fu - (0.55 - 1.6 * fv * fv);
                    return (scallop > 0 ? -0.18 : 0.05) * amt;
                }
                case SurfaceTexture.Bark:
                {
                    double n = PatternNoise.Perlin2(surface.Seed, v * 0.55, u * 0.1);
                    return n * 0.45 * amt;
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ shading cleanup

        private void CleanupShading()
        {
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
            {
                int row = y * _w;
                Array.Copy(_level, row + rx0, _levelTmp, row + rx0, rx1 - rx0 + 1);
                Array.Copy(_mat, row + rx0, _matTmp, row + rx0, rx1 - rx0 + 1);
            }
            Span<int> counts = stackalloc int[8];
            Span<int> matCounts = stackalloc int[(int)MaterialSlot.Count];
            for (int y = Math.Max(1, ry0); y <= Math.Min(_h - 2, ry1); y++)
            {
                for (int x = Math.Max(1, rx0); x <= Math.Min(_w - 2, rx1); x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0 || (_pflags[i] & FlagThin) != 0) continue;
                    // --- isolated material pixel (pattern speck) inside one group
                    matCounts.Clear();
                    int sameGroup = 0;
                    int g = _group[i];
                    int differentMat = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int j = Neighbour(i, k);
                        if ((_pflags[j] & FlagCovered) == 0 || _group[j] != g) continue;
                        sameGroup++;
                        matCounts[_matTmp[j]]++;
                        if (_matTmp[j] != _matTmp[i]) differentMat++;
                    }
                    if (sameGroup >= 3 && differentMat == sameGroup)
                    {
                        int bestM = _matTmp[i], bestC = 0;
                        for (int m = 0; m < matCounts.Length; m++) if (matCounts[m] > bestC) { bestC = matCounts[m]; bestM = m; }
                        if (bestC >= 3) _mat[i] = (byte)bestM;
                    }

                    // --- isolated shade pixel
                    counts.Clear();
                    int same = 0, total = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int j = Neighbour(i, k);
                        if ((_pflags[j] & FlagCovered) == 0 || _group[j] != g || _mat[j] != _mat[i]) continue;
                        total++;
                        int lv = _levelTmp[j];
                        counts[lv]++;
                        if (lv == _levelTmp[i]) same++;
                    }
                    if (total >= 3 && same == 0)
                    {
                        int best = _levelTmp[i], bestC = 0;
                        for (int lv = 0; lv < 8; lv++) if (counts[lv] > bestC) { bestC = counts[lv]; best = lv; }
                        // highlights are allowed as single accent pixels
                        if (_levelTmp[i] != Shade.Highlight || bestC >= 4) _level[i] = (sbyte)best;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int Neighbour(int i, int k)
        {
            switch (k)
            {
                case 0: return i - 1;
                case 1: return i + 1;
                case 2: return i - _w;
                default: return i + _w;
            }
        }

        // ------------------------------------------------------------------ composition: colours, contours, outline

        private void Compose(CreatureAnatomy anatomy, CreaturePalette palette, PixelFrame frame)
        {
            var output = frame.Indices;
            var groups = anatomy.Groups;
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
            {
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0) { output[i] = 0; continue; }
                    output[i] = palette.Index((MaterialSlot)_mat[i], _level[i]);
                }
            }

            // interior contours: a pixel directly behind a nearer part (other group) becomes a line
            if (Options.InteriorContours)
            {
                double tau = Options.ContourDepth;
                double tauSelf = Options.SelfContourDepth;
                for (int y = ry0; y <= ry1; y++)
                {
                    for (int x = rx0; x <= rx1; x++)
                    {
                        int i = y * _w + x;
                        if ((_pflags[i] & FlagCovered) == 0) continue;
                        int g = _group[i];
                        float zi = _z[i];
                        int frontNeighbour = -1;
                        float frontZ = float.NegativeInfinity;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0);
                            int ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                            if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                            int j = ny * _w + nx;
                            if ((_pflags[j] & FlagCovered) == 0) continue;
                            int gj = _group[j];
                            var grpJ = groups[gj];
                            if ((grpJ.Flags & GroupFlags.NoContour) != 0) continue;
                            double gap = _z[j] - zi;
                            bool edge = gj != g ? gap > tau : gap > tauSelf + groups[g].BlendRadius;
                            if (!edge) continue;
                            if (_z[j] > frontZ) { frontZ = _z[j]; frontNeighbour = j; }
                        }
                        if (frontNeighbour < 0) continue;
                        double depthGap = frontZ - zi;
                        var backMat = (MaterialSlot)_mat[i];
                        // soft contour for touching parts, full outline for clear separations
                        byte c = depthGap > 6.0 || _level[i] <= Shade.Deep
                            ? palette.Index((MaterialSlot)_mat[frontNeighbour], Shade.Outline)
                            : palette.Index(backMat, Shade.Deep);
                        output[i] = c;
                        _pflags[i] |= FlagContour;
                    }
                }
            }

            // exterior outline (4-neighbourhood -> clean diagonals)
            bool selOut = Options.SelectiveOutline;
            for (int y = ry0; y <= ry1; y++)
            {
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) != 0) continue;
                    int best = -1;
                    float bestZ = float.NegativeInfinity;
                    int litNeighbours = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0);
                        int ny = y + (k == 2 ? -1 : k == 3 ? 1 : 0);
                        if (nx < 0 || ny < 0 || nx >= _w || ny >= _h) continue;
                        int j = ny * _w + nx;
                        if ((_pflags[j] & FlagCovered) == 0 || (_pflags[j] & FlagNoOutline) != 0) continue;
                        if (_z[j] > bestZ) { bestZ = _z[j]; best = j; }
                        if (_level[j] >= Shade.Light) litNeighbours++;
                    }
                    if (best < 0) continue;
                    var mat = (MaterialSlot)_mat[best];
                    bool lit = selOut && litNeighbours > 0 && (k0Up(x, y) || k0Left(x, y));
                    output[i] = palette.Index(mat, lit ? Shade.Deep : Shade.Outline);
                }
            }
        }

        // outline pixel lies above or left of the shape (towards the light)
        private bool k0Up(int x, int y) => y + 1 < _h && (_pflags[(y + 1) * _w + x] & FlagCovered) != 0 && (y == 0 || (_pflags[(y - 1) * _w + x] & FlagCovered) == 0);
        private bool k0Left(int x, int y) => x + 1 < _w && (_pflags[y * _w + x + 1] & FlagCovered) != 0 && (x == 0 || (_pflags[y * _w + x - 1] & FlagCovered) == 0);

        // ------------------------------------------------------------------ features (eyes, nostrils)

        private void DrawFeatures(CreatureAnatomy anatomy, CreaturePalette palette, CreaturePose pose, PixelFrame frame)
        {
            var feats = anatomy.Features;
            var output = frame.Indices;
            var expr = pose.Expression;
            for (int fi = 0; fi < feats.Count; fi++)
            {
                var f = feats[fi];
                var bv = _boneView[f.Bone];
                Vec3 pos = bv.Point(f.LocalPosition);
                Vec3 nrm = bv.Dir(f.LocalNormal).NormalizedOr(Vec3.UnitZ);
                if (!pos.IsFinite) continue;
                double sx = _ox + pos.X, sy = _oy - pos.Y;
                int cx = (int)Math.Floor(sx), cy = (int)Math.Floor(sy);
                if (cx < 0 || cy < 0 || cx >= _w || cy >= _h) continue;
                if (nrm.Z < -0.12) continue;
                // occlusion: the feature must sit on (or just below) the visible surface
                int ci = cy * _w + cx;
                if ((_pflags[ci] & FlagCovered) == 0) continue;
                if (_z[ci] > pos.Z + 1.6 + f.Size * 0.3) continue;
                double scale = _boneScale[f.Bone];

                switch (f.Kind)
                {
                    case FeatureKind.Eye:
                        DrawEye(f, palette, output, sx, sy, nrm, expr, scale);
                        break;
                    case FeatureKind.Nostril:
                        if (nrm.Z > 0.15 && (_pflags[ci] & FlagContour) == 0)
                            output[ci] = palette.Index(MaterialSlot.Primary, Shade.Deep);
                        break;
                    case FeatureKind.Spot:
                        output[ci] = palette.Index(f.Material, Shade.Highlight);
                        break;
                }
            }
        }

        private void DrawEye(FeatureDef f, CreaturePalette palette, byte[] output, double sx, double sy, Vec3 nrm, ExpressionState expr, double scale)
        {
            double facing = DMath.Saturate(nrm.Z * 1.25 + 0.15);
            // round up generously: a 2x2 eye with a shine pixel reads far better than a single dot
            int h = Math.Max(1, (int)Math.Floor(f.Size * scale + 0.7));
            int wFull = Math.Max(1, (int)Math.Floor(f.Size * f.Aspect * scale + 0.7));
            int w = Math.Max(1, (int)Math.Round(wFull * DMath.Lerp(0.45, 1.0, facing)));
            if (h >= 3 && facing < 0.35) w = Math.Max(1, Math.Min(w, h - 1));
            // a 1-pixel-wide eye in three-quarter view reads as a dash: keep two columns while it faces us
            if (h >= 2 && wFull >= 2 && facing > 0.3) w = Math.Max(2, w);

            // screen direction the eye looks towards: the creature's forward projected on screen
            double fwdX = _forwardView.X;
            int lookDir = fwdX > 0.3 ? 1 : (fwdX < -0.3 ? -1 : 0);
            // front view: pupils drift inward towards the snout line
            if (lookDir == 0) lookDir = nrm.X > 0.15 ? -1 : (nrm.X < -0.15 ? 1 : 0);
            lookDir += expr.LookX > 0.5 ? 1 : (expr.LookX < -0.5 ? -1 : 0);
            lookDir = Math.Clamp(lookDir, -1, 1);

            int x0 = (int)Math.Floor(sx - w * 0.5 + 0.5);
            int y0 = (int)Math.Floor(sy - h * 0.5 + 0.5);
            byte outline = palette.Index(MaterialSlot.Primary, Shade.Outline);
            byte pupil = outline;
            byte iris = palette.Index(MaterialSlot.Eye, Shade.Base);
            byte irisDark = palette.Index(MaterialSlot.Eye, Shade.Shadow);
            byte irisLight = palette.Index(MaterialSlot.Eye, Shade.Light);
            byte sclera = palette.Index(MaterialSlot.Teeth, Shade.Base);
            byte scleraShadow = palette.Index(MaterialSlot.Teeth, Shade.Shadow);
            byte shine = palette.Index(MaterialSlot.Eye, Shade.Highlight);
            byte glow = palette.Index(MaterialSlot.Glow, Shade.Light);
            byte glowDark = palette.Index(MaterialSlot.Glow, Shade.Base);
            // glowing eyes on a glowing body (wisps) need a dark socket to stay readable
            bool onGlow = false;
            if (f.Style == EyeStyle.Glow)
            {
                int ux = (int)Math.Floor(sx), uy = (int)Math.Floor(sy);
                if (ux >= 0 && uy >= 0 && ux < _w && uy < _h)
                {
                    byte under = output[uy * _w + ux];
                    for (int sh = Shade.Deep; sh <= Shade.Highlight && !onGlow; sh++) onGlow = under == palette.Index(MaterialSlot.Glow, sh);
                }
                if (onGlow) glowDark = outline;
            }

            double open = DMath.Saturate(expr.EyeOpen);
            var mood = expr.Mood;
            if (mood == EyeMood.Sleep) open = 0;

            // closed eye: a short lid line (sleep: gentle downward curve)
            if (open < 0.2 || (mood == EyeMood.Pain && h >= 2))
            {
                int lw = Math.Max(1, Math.Min(w + (h >= 3 ? 1 : 0), 5));
                int lx0 = (int)Math.Floor(sx - lw * 0.5 + 0.5);
                int ly = (int)Math.Floor(sy + (h >= 3 ? 0.5 : 0));
                for (int i = 0; i < lw; i++)
                {
                    int yy = ly;
                    if (mood == EyeMood.Pain && lw >= 3 && (i == 0 || i == lw - 1)) yy = ly - 1;
                    if (mood == EyeMood.Sleep && lw >= 3 && (i == 0 || i == lw - 1)) yy = ly - 1;
                    Put(output, lx0 + i, yy, outline);
                }
                return;
            }

            int lidRows = (int)Math.Round((1.0 - open) * h);
            // angry: lid slants down towards the snout; alert: fully open
            for (int yy = 0; yy < h; yy++)
            {
                for (int xx = 0; xx < w; xx++)
                {
                    if (!EyeMask(xx, yy, w, h)) continue;
                    int row = yy;
                    int lidCut = lidRows;
                    if (mood == EyeMood.Angry && h >= 2)
                    {
                        int towardsSnout = lookDir >= 0 ? xx : (w - 1 - xx);
                        lidCut += (int)Math.Floor((double)towardsSnout * (h >= 3 ? 1.0 : 0.6) / Math.Max(1, w - 1) + 0.25);
                        if (w == 1) lidCut += 0;
                    }
                    if (row < lidCut) continue;
                    byte c = w >= 4 && h >= 4
                        ? BigEyePixel(f.Style, xx, yy, w, h, lookDir, pupil, iris, irisDark, irisLight, sclera, scleraShadow, shine, glow, glowDark, outline)
                        : EyePixel(f.Style, xx, yy, w, h, lookDir, pupil, iris, irisDark, irisLight, sclera, shine, glow, glowDark, outline);
                    if (onGlow && w * h <= 2) c = outline;
                    // lid line on the first visible row for bigger eyes
                    if (h >= 4 && row == lidCut && (lidCut > 0 || f.Brow > 0.4 || mood == EyeMood.Angry)) c = outline;
                    Put(output, x0 + xx, y0 + yy, c);
                }
            }
            // brow line above big eyes when the creature is angry or has a heavy brow
            if ((mood == EyeMood.Angry || f.Brow > 0.6) && h >= 2 && w >= 2)
            {
                int by = y0 - 1;
                for (int xx = 0; xx < w; xx++)
                {
                    int towardsSnout = lookDir >= 0 ? xx : (w - 1 - xx);
                    int dy = mood == EyeMood.Angry && towardsSnout >= w - 1 ? 1 : 0;
                    Put(output, x0 + xx, by + dy, outline);
                }
            }
        }

        private static bool EyeMask(int x, int y, int w, int h)
        {
            if (w <= 3 || h <= 3) return true;
            double cx = (x + 0.5) / w * 2 - 1, cy = (y + 0.5) / h * 2 - 1;
            // 4x4 loses its corners, 5x5 and up become proper discs
            return cx * cx + cy * cy <= (w <= 4 || h <= 4 ? 1.05 : 0.98);
        }

        /// <summary>
        /// Eyes of 4x4 pixels and more: lid line on the rim (5+), iris shifted towards the gaze with a shaded
        /// upper part, pupil (round or slit), a shine towards the key light and a shaded lower sclera.
        /// </summary>
        private static byte BigEyePixel(EyeStyle style, int x, int y, int w, int h, int lookDir, byte pupil, byte iris, byte irisDark,
            byte irisLight, byte sclera, byte scleraShadow, byte shine, byte glow, byte glowDark, byte outline)
        {
            if (w >= 5 && h >= 5)
            {
                bool rim = !EyeMask(x - 1, y, w, h) || !EyeMask(x + 1, y, w, h) || !EyeMask(x, y - 1, w, h) || !EyeMask(x, y + 1, w, h)
                           || x == 0 || y == 0 || x == w - 1 || y == h - 1;
                if (rim && style != EyeStyle.Glow) return outline;
            }
            double cx = (w - 1) * 0.5, cy = (h - 1) * 0.5;
            double ix = cx + lookDir * w * 0.13, iy = cy + h * 0.04;
            double m = Math.Min(w, h);
            double dx = x - ix, dy = y - iy;
            double d = Math.Sqrt(dx * dx + dy * dy);
            // shine: towards the key light (upper left of the iris)
            int shx = (int)Math.Round(ix - m * 0.18), shy = (int)Math.Round(iy - m * 0.2);
            bool isShine = x == shx && y == shy || (m >= 8 && x == shx + 1 && y == shy);
            switch (style)
            {
                case EyeStyle.Glow:
                {
                    double r = Math.Sqrt(((x - cx) / (w * 0.5)) * ((x - cx) / (w * 0.5)) + ((y - cy) / (h * 0.5)) * ((y - cy) / (h * 0.5)));
                    return r < 0.45 ? glow : glowDark;
                }
                case EyeStyle.Compound:
                    if (isShine) return shine;
                    return y < cy - m * 0.1 ? irisDark : pupil;
                case EyeStyle.Slit:
                {
                    // the whole eye is iris; a vertical slit pupil, darker under the upper lid
                    double halfSlit = m >= 7 ? 0.9 : 0.5;
                    if (Math.Abs(x - ix) <= halfSlit && Math.Abs(y - cy) <= h * 0.38) return pupil;
                    if (isShine) return shine;
                    if (y <= cy - h * 0.25) return irisDark;
                    return y >= cy + h * 0.28 ? irisLight : iris;
                }
                case EyeStyle.Bead:
                {
                    if (isShine) return shine;
                    double irisR = m * 0.5;
                    return d > irisR - 0.6 && m >= 6 ? irisDark : pupil;
                }
                default:
                {
                    // round / button: sclera, iris ring, pupil
                    double irisR = style == EyeStyle.Button ? m * 0.42 : m * 0.34 + 0.35;
                    double pupilR = irisR * (style == EyeStyle.Button ? 0.75 : 0.52);
                    if (isShine && d <= irisR + 0.2) return shine;
                    if (d <= pupilR) return pupil;
                    if (d <= irisR) return dy < -irisR * 0.35 ? irisDark : (dy > irisR * 0.45 ? irisLight : iris);
                    return y > cy + h * 0.2 || (x - cx) * lookDir > w * 0.3 ? scleraShadow : sclera;
                }
            }
        }

        private static byte EyePixel(EyeStyle style, int x, int y, int w, int h, int lookDir, byte pupil, byte iris, byte irisDark,
            byte irisLight, byte sclera, byte shine, byte glow, byte glowDark, byte outline)
        {
            // column that holds the pupil: towards the look direction
            int pupilCol = lookDir > 0 ? w - 1 - (w >= 4 ? 1 : 0) : (lookDir < 0 ? (w >= 4 ? 1 : 0) : (w - 1) / 2);
            bool isShine = x == (lookDir > 0 ? Math.Max(0, pupilCol - 1) : Math.Min(w - 1, pupilCol + (w >= 3 ? 0 : 1))) && y == 0 && (w >= 2 || h >= 3);
            switch (style)
            {
                case EyeStyle.Bead:
                case EyeStyle.Button:
                {
                    if (w == 1 && h == 1) return pupil;
                    if (style == EyeStyle.Button && w >= 3 && h >= 3 && x <= 1 && y <= 1 && !(x == 1 && y == 1)) return shine;
                    if (isShine || (w >= 2 && h >= 2 && x == (lookDir > 0 ? w - 1 : 0) && y == 0 && w == 2)) return shine;
                    return pupil;
                }
                case EyeStyle.Round:
                {
                    if (w == 1) return h >= 2 && y == 0 ? sclera : pupil;
                    if (h == 1) return x == pupilCol ? pupil : sclera;
                    if (x == pupilCol || (w >= 5 && Math.Abs(x - pupilCol) == 1 && y > 0 && y < h - 1))
                        return y == 0 && h >= 3 ? (w >= 4 ? shine : iris) : pupil;
                    if (w >= 4 && Math.Abs(x - pupilCol) == 1) return iris;
                    return sclera;
                }
                case EyeStyle.Slit:
                {
                    if (w == 1 && h == 1) return iris;
                    int mid = lookDir > 0 ? w / 2 : (w - 1) / 2;
                    if (x == mid && y > 0) return pupil;
                    if (x == mid && y == 0) return h >= 3 ? pupil : iris;
                    if (isShine && w >= 3) return irisLight;
                    return y >= h - 1 && h >= 3 ? irisDark : iris;
                }
                case EyeStyle.Compound:
                {
                    if (isShine) return shine;
                    if (w >= 3 && h >= 3 && x == 1 && y == 1) return irisLight;
                    return y >= h / 2 ? pupil : irisDark;
                }
                case EyeStyle.Glow:
                {
                    if (w == 1 && h == 1) return glow;
                    if (x == pupilCol && y >= (h >= 3 ? 1 : 0)) return glow;
                    return w >= 3 || h >= 3 ? glowDark : glow;
                }
            }
            return pupil;
        }

        private void Put(byte[] output, int x, int y, byte c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            int i = y * _w + x;
            if ((_pflags[i] & FlagCovered) == 0) return;
            output[i] = c;
            _pflags[i] |= FlagFeature;
        }

        // ------------------------------------------------------------------ ground shadow & water

        // Ground shadow as a thresholded metaball field of the mass carrying parts. Summing smooth
        // falloffs (instead of stamping ellipses) gives one calm, rounded shadow blob that joins the
        // body and the planted feet, and thin parts (tails, horns, claws) never produce stray dashes.
        private readonly List<(double x, double z, double r, double w)> _shadowBlobs = new List<(double, double, double, double)>();
        private float[] _shadowField = Array.Empty<float>();

        private void DrawShadow(CreatureAnatomy anatomy, CreaturePose pose, PixelFrame frame)
        {
            var output = frame.Indices;
            int n = _w * _h;
            if (_shadowField.Length < n) _shadowField = new float[n];
            Array.Clear(_shadowField, 0, n);
            double sinE = DMath.Sin(PixelCamera.DefaultElevation);
            // airborne creatures (flight, hops, swimming depth) keep a readable but smaller, lighter shadow
            double alt = Math.Max(0, pose.Altitude);
            double altScale = DMath.Lerp(1.0, 0.72, DMath.Saturate(alt / 70.0));
            double altWeight = DMath.Lerp(1.0, 0.62, DMath.Saturate(alt / 60.0));
            _shadowBlobs.Clear();
            for (int i = 0; i < _ppCount; i++)
            {
                ref var p = ref _pp[i];
                if ((p.Flags & (PrimFlags.NoShadow | PrimFlags.Thin)) != 0 || p.Kind == PrimKind.Triangle) continue;
                // only mass carrying parts cast the ground blob (explicit casters may opt in)
                bool caster = (p.Flags & PrimFlags.ShadowCaster) != 0 || p.Domain == PatternDomain.Body || p.Domain == PatternDomain.Limb ||
                              p.Domain == PatternDomain.Neck || p.Domain == PatternDomain.Head;
                if (!caster) continue;
                var def = anatomy.Primitives[p.Source];
                Vec3 cw;
                double r;
                if (p.Kind == PrimKind.Ellipsoid)
                {
                    cw = pose.Skeleton.World[def.BoneA].Point(def.LocalA);
                    r = Math.Sqrt(def.Radii.X * def.Radii.Z) * _boneScale[def.BoneA];
                }
                else
                {
                    Vec3 a = pose.Skeleton.World[def.BoneA].Point(def.LocalA);
                    Vec3 b = pose.Skeleton.World[def.BoneB].Point(def.LocalB);
                    // legs: only the lowest point matters (feet), bodies: the centre
                    cw = a.Y < b.Y ? a : b;
                    r = Math.Max(def.RadiusA, def.RadiusB) * 1.25;
                }
                if (r < 1.2) continue;
                double height = Math.Max(0, cw.Y - r - alt);
                double fade = DMath.Clamp(1.0 - height / (r * 4.0 + 20.0), 0.0, 1.0);
                if (fade <= 0.05) continue;
                _shadowBlobs.Add((cw.X, cw.Z, r * DMath.Lerp(0.55, 1.05, fade) * altScale, fade * altWeight));
            }
            if (alt > 0.5 && _shadowBlobs.Count > 1)
            {
                // airborne: the distant, softer shadow reads as one calm shape instead of separate
                // "footprints" - pull the blobs towards their mass centre and widen them
                double t = DMath.Saturate(alt / 45.0);
                double sw = 0, sx = 0, sz = 0;
                foreach (var (bx, bz, br, bw) in _shadowBlobs)
                {
                    double m = br * br * bw;
                    sw += m; sx += bx * m; sz += bz * m;
                }
                if (sw > 1e-9)
                {
                    double cx = sx / sw, cz = sz / sw;
                    double pull = 0.55 * t, grow = DMath.Lerp(1.0, 1.35, t);
                    for (int i = 0; i < _shadowBlobs.Count; i++)
                    {
                        var (bx, bz, br, bw) = _shadowBlobs[i];
                        _shadowBlobs[i] = (DMath.Lerp(bx, cx, pull), DMath.Lerp(bz, cz, pull), br * grow, bw);
                    }
                }
            }
            foreach (var (bx, bz, br, bw) in _shadowBlobs)
            {
                Vec3 c = _creatureToView.Mul(new Vec3(bx, 0, bz)) + _groundOffsetView;
                double rx = br * 1.35, ry = Math.Max(1.0, br * 1.35 * sinE);
                double scx = _ox + c.X, scy = _oy - c.Y;
                int x0 = Math.Max(0, (int)Math.Floor(scx - rx)), x1 = Math.Min(_w - 1, (int)Math.Ceiling(scx + rx));
                int y0 = Math.Max(0, (int)Math.Floor(scy - ry)), y1 = Math.Min(_h - 1, (int)Math.Ceiling(scy + ry));
                for (int y = y0; y <= y1; y++)
                {
                    double dy = (y + 0.5 - scy) / ry;
                    for (int x = x0; x <= x1; x++)
                    {
                        double dx = (x + 0.5 - scx) / rx;
                        double d2 = dx * dx + dy * dy;
                        if (d2 >= 1.0) continue;
                        double f = 1.0 - d2;
                        _shadowField[y * _w + x] += (float)(f * f * bw);
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (_shadowField[i] > 0.33f && output[i] == 0) output[i] = CreaturePalette.ShadowIndex;
            }
        }

        private void ApplyWater(in ViewSpec view, PixelFrame frame, CreaturePalette palette)
        {
            var output = frame.Indices;
            var sub = frame.Submerged;
            var inv = _creatureToView.Transposed;
            bool any = false;
            WorkRegion(out int rx0, out int ry0, out int rx1, out int ry1);
            for (int y = ry0; y <= ry1; y++)
            {
                double vy = _oy - (y + 0.5);
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if ((_pflags[i] & FlagCovered) == 0) continue;
                    double vx = x + 0.5 - _ox;
                    Vec3 v = new Vec3(vx, vy, _z[i]) - _offsetView;
                    Vec3 c = inv.Mul(v);
                    if (c.Y < view.WaterLevel)
                    {
                        sub[i] = 1;
                        any = true;
                    }
                }
            }
            if (!any) return;
            frame.HasSubmerged = true;
            // waterline: submerged pixel with a dry pixel above -> light ripple
            for (int y = Math.Max(1, ry0); y <= ry1; y++)
            {
                for (int x = rx0; x <= rx1; x++)
                {
                    int i = y * _w + x;
                    if (sub[i] == 0) continue;
                    int up = i - _w;
                    if ((_pflags[up] & FlagCovered) != 0 && sub[up] == 0)
                    {
                        output[i] = CreaturePalette.WaterlineIndex;
                        sub[i] = 0;
                    }
                }
            }
        }
    }
}
