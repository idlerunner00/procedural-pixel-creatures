// Procedural Pixel Creatures - small double precision linear algebra used by the core.
// Engine independent on purpose: the core must run on worker threads and in tools without Godot.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace PixelCreatures.Core
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X, Y;

        public Vec2(double x, double y) { X = x; Y = y; }

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 One = new Vec2(1, 1);
        public static readonly Vec2 UnitX = new Vec2(1, 0);
        public static readonly Vec2 UnitY = new Vec2(0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSquared => X * X + Y * Y;

        public Vec2 Normalized()
        {
            double l = Length;
            return l > 1e-12 ? new Vec2(X / l, Y / l) : Zero;
        }

        public Vec2 Perp => new Vec2(-Y, X);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 FromAngle(double radians) { DMath.SinCos(radians, out double s, out double c); return new Vec2(c, s); }
        public double Angle => DMath.Atan2(Y, X);
        public Vec2 Rotated(double radians)
        {
            DMath.SinCos(radians, out double s, out double c);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, double s) => new Vec2(a.X / s, a.Y / s);

        public bool IsFinite => DMath.IsFinite(X) && DMath.IsFinite(Y);
        public bool Equals(Vec2 o) => X == o.X && Y == o.Y;
        public override bool Equals(object? obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }

    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly double X, Y, Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 One = new Vec3(1, 1, 1);
        public static readonly Vec3 UnitX = new Vec3(1, 0, 0);
        public static readonly Vec3 UnitY = new Vec3(0, 1, 0);
        public static readonly Vec3 UnitZ = new Vec3(0, 0, 1);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;

        public Vec3 Normalized()
        {
            double l = Length;
            return l > 1e-12 ? new Vec3(X / l, Y / l, Z / l) : Zero;
        }

        public Vec3 NormalizedOr(Vec3 fallback)
        {
            double l = Length;
            return l > 1e-12 ? new Vec3(X / l, Y / l, Z / l) : fallback;
        }

        public Vec2 XY => new Vec2(X, Y);
        public Vec2 XZ => new Vec2(X, Z);
        public Vec3 WithY(double y) => new Vec3(X, y, Z);
        public Vec3 WithX(double x) => new Vec3(x, Y, Z);
        public Vec3 WithZ(double z) => new Vec3(X, Y, z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => new Vec3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        public static double Distance(Vec3 a, Vec3 b) => (a - b).Length;
        public static Vec3 Min(Vec3 a, Vec3 b) => new Vec3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
        public static Vec3 Max(Vec3 a, Vec3 b) => new Vec3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        public static Vec3 Scale(Vec3 a, Vec3 b) => new Vec3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        /// <summary>Quadratic Bezier evaluation.</summary>
        public static Vec3 Bezier(Vec3 a, Vec3 b, Vec3 c, double t)
        {
            double u = 1 - t;
            return a * (u * u) + b * (2 * u * t) + c * (t * t);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(double s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, double s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public bool IsFinite => DMath.IsFinite(X) && DMath.IsFinite(Y) && DMath.IsFinite(Z);
        public bool Equals(Vec3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object? obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###})", X, Y, Z);
    }

    /// <summary>Unit quaternion rotation (x, y, z, w).</summary>
    public readonly struct Quat : IEquatable<Quat>
    {
        public readonly double X, Y, Z, W;

        public Quat(double x, double y, double z, double w) { X = x; Y = y; Z = z; W = w; }

        public static readonly Quat Identity = new Quat(0, 0, 0, 1);

        public static Quat AxisAngle(Vec3 axis, double angle)
        {
            Vec3 n = axis.Normalized();
            DMath.SinCos(angle * 0.5, out double s, out double c);
            return new Quat(n.X * s, n.Y * s, n.Z * s, c);
        }

        public static Quat RotX(double a) { DMath.SinCos(a * 0.5, out double s, out double c); return new Quat(s, 0, 0, c); }
        public static Quat RotY(double a) { DMath.SinCos(a * 0.5, out double s, out double c); return new Quat(0, s, 0, c); }
        public static Quat RotZ(double a) { DMath.SinCos(a * 0.5, out double s, out double c); return new Quat(0, 0, s, c); }

        /// <summary>Yaw (Y), then pitch (Z), then roll (X) in the creature convention (+X forward, +Y up, +Z right).</summary>
        public static Quat Euler(double yaw, double pitch, double roll) => RotY(yaw) * RotZ(pitch) * RotX(roll);

        public Quat Normalized()
        {
            double l = Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
            return l > 1e-12 ? new Quat(X / l, Y / l, Z / l, W / l) : Identity;
        }

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public static Quat operator *(Quat a, Quat b) => new Quat(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

        public Vec3 Rotate(Vec3 v)
        {
            // v' = v + 2w(q x v) + 2 q x (q x v)
            Vec3 q = new Vec3(X, Y, Z);
            Vec3 t = Vec3.Cross(q, v) * 2.0;
            return v + t * W + Vec3.Cross(q, t);
        }

        public static double Dot(Quat a, Quat b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

        /// <summary>Normalized linear interpolation along the shortest arc. Deterministic and stable for animation blending.</summary>
        public static Quat Nlerp(Quat a, Quat b, double t)
        {
            if (Dot(a, b) < 0) b = new Quat(-b.X, -b.Y, -b.Z, -b.W);
            return new Quat(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t).Normalized();
        }

        /// <summary>Shortest rotation that maps direction a onto direction b.</summary>
        public static Quat FromTo(Vec3 a, Vec3 b)
        {
            Vec3 na = a.Normalized(), nb = b.Normalized();
            double d = Vec3.Dot(na, nb);
            if (d > 0.999999) return Identity;
            if (d < -0.999999)
            {
                Vec3 axis = Vec3.Cross(Vec3.UnitX, na);
                if (axis.LengthSquared < 1e-8) axis = Vec3.Cross(Vec3.UnitY, na);
                return AxisAngle(axis, DMath.Pi);
            }
            Vec3 c = Vec3.Cross(na, nb);
            return new Quat(c.X, c.Y, c.Z, 1.0 + d).Normalized();
        }

        public bool IsFinite => DMath.IsFinite(X) && DMath.IsFinite(Y) && DMath.IsFinite(Z) && DMath.IsFinite(W);
        public bool Equals(Quat o) => X == o.X && Y == o.Y && Z == o.Z && W == o.W;
        public override bool Equals(object? obj) => obj is Quat q && Equals(q);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    }

    /// <summary>3x3 matrix stored as three column vectors (basis axes).</summary>
    public readonly struct Mat3
    {
        public readonly Vec3 C0, C1, C2;

        public Mat3(Vec3 c0, Vec3 c1, Vec3 c2) { C0 = c0; C1 = c1; C2 = c2; }

        public static readonly Mat3 Identity = new Mat3(Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);

        public static Mat3 FromQuat(Quat q)
        {
            double xx = q.X * q.X, yy = q.Y * q.Y, zz = q.Z * q.Z;
            double xy = q.X * q.Y, xz = q.X * q.Z, yz = q.Y * q.Z;
            double wx = q.W * q.X, wy = q.W * q.Y, wz = q.W * q.Z;
            return new Mat3(
                new Vec3(1 - 2 * (yy + zz), 2 * (xy + wz), 2 * (xz - wy)),
                new Vec3(2 * (xy - wz), 1 - 2 * (xx + zz), 2 * (yz + wx)),
                new Vec3(2 * (xz + wy), 2 * (yz - wx), 1 - 2 * (xx + yy)));
        }

        public static Mat3 Diagonal(double a, double b, double c) => new Mat3(new Vec3(a, 0, 0), new Vec3(0, b, 0), new Vec3(0, 0, c));

        /// <summary>Orthonormal basis whose X axis points along forward and whose Y axis is as close as possible to up.</summary>
        public static Mat3 LookAlong(Vec3 forward, Vec3 up)
        {
            Vec3 x = forward.NormalizedOr(Vec3.UnitX);
            Vec3 z = Vec3.Cross(x, up);
            if (z.LengthSquared < 1e-10) z = Vec3.Cross(x, Math.Abs(x.Y) < 0.9 ? Vec3.UnitY : Vec3.UnitX);
            z = z.Normalized();
            Vec3 y = Vec3.Cross(z, x).Normalized();
            return new Mat3(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 Mul(Vec3 v) => C0 * v.X + C1 * v.Y + C2 * v.Z;

        /// <summary>Transpose(M) * v. For rotations this is the inverse transform.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 MulTransposed(Vec3 v) => new Vec3(Vec3.Dot(C0, v), Vec3.Dot(C1, v), Vec3.Dot(C2, v));

        public static Mat3 operator *(Mat3 a, Mat3 b) => new Mat3(a.Mul(b.C0), a.Mul(b.C1), a.Mul(b.C2));

        public Mat3 Transposed => new Mat3(
            new Vec3(C0.X, C1.X, C2.X),
            new Vec3(C0.Y, C1.Y, C2.Y),
            new Vec3(C0.Z, C1.Z, C2.Z));

        public Mat3 ScaledColumns(double sx, double sy, double sz) => new Mat3(C0 * sx, C1 * sy, C2 * sz);

        public double Determinant => Vec3.Dot(C0, Vec3.Cross(C1, C2));

        public Mat3 Inverse()
        {
            Vec3 r0 = Vec3.Cross(C1, C2);
            Vec3 r1 = Vec3.Cross(C2, C0);
            Vec3 r2 = Vec3.Cross(C0, C1);
            double det = Vec3.Dot(C0, r0);
            if (Math.Abs(det) < 1e-18) return Identity;
            double inv = 1.0 / det;
            // rows of the inverse are r0, r1, r2 scaled
            return new Mat3(
                new Vec3(r0.X * inv, r1.X * inv, r2.X * inv),
                new Vec3(r0.Y * inv, r1.Y * inv, r2.Y * inv),
                new Vec3(r0.Z * inv, r1.Z * inv, r2.Z * inv));
        }

        /// <summary>Re-orthonormalizes a rotation basis (Gram-Schmidt, X axis dominant).</summary>
        public Mat3 Orthonormalized()
        {
            Vec3 x = C0.NormalizedOr(Vec3.UnitX);
            Vec3 y = (C1 - x * Vec3.Dot(x, C1)).NormalizedOr(Vec3.UnitY);
            Vec3 z = Vec3.Cross(x, y);
            return new Mat3(x, y, z);
        }
    }

    /// <summary>Rigid transform: rotation basis and translation.</summary>
    public readonly struct Xform
    {
        public readonly Mat3 Basis;
        public readonly Vec3 Origin;

        public Xform(Mat3 basis, Vec3 origin) { Basis = basis; Origin = origin; }

        public static readonly Xform Identity = new Xform(Mat3.Identity, Vec3.Zero);

        public static Xform FromQuat(Quat q, Vec3 origin) => new Xform(Mat3.FromQuat(q), origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 Point(Vec3 local) => Basis.Mul(local) + Origin;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 Dir(Vec3 local) => Basis.Mul(local);

        /// <summary>Inverse transform for rigid (orthonormal) bases.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 InversePoint(Vec3 world) => Basis.MulTransposed(world - Origin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vec3 InverseDir(Vec3 world) => Basis.MulTransposed(world);

        public static Xform operator *(Xform parent, Xform local) => new Xform(parent.Basis * local.Basis, parent.Point(local.Origin));

        public Vec3 AxisX => Basis.C0;
        public Vec3 AxisY => Basis.C1;
        public Vec3 AxisZ => Basis.C2;
    }
}
