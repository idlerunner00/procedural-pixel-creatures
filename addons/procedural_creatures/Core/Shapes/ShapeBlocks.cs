// Procedural Pixel Creatures - shape block extension point.
//
// Shape blocks are reusable parametric building blocks (legs, tails, heads, horns, ears, wings, fins,
// spikes, ...). Families compose them; extensions register new ones in the CreatureRegistry and can
// use every built-in block. A block only talks to the AnatomyBuilder: it adds bones, groups,
// primitives and features and reports the rig pieces it created.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    /// <summary>Loose parameter bag for registry based block use (typed specs are used internally).</summary>
    public sealed class ShapeParams
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);
        public int ParentBone = -1;
        public Vec3 Position;
        public Vec3 Direction = Vec3.UnitX;
        public Vec3 Up = Vec3.UnitY;
        public int Side;
        public int Group = -1;
        public MaterialSlot Material = MaterialSlot.Primary;
        public string Name = "block";

        public ShapeParams Set(string key, double v) { _values[key] = v; return this; }
        public double Get(string key, double fallback) => _values.TryGetValue(key, out double v) ? v : fallback;
    }

    public sealed class ShapeResult
    {
        public readonly List<int> Bones = new List<int>();
        public readonly List<int> Groups = new List<int>();
        public ChainRig? Chain;
        public LegRig? Leg;
    }

    public interface IShapeBlock
    {
        string Id { get; }
        string Category { get; }
        string Description { get; }
        ShapeResult Build(AnatomyBuilder builder, ShapeParams p);
    }

    /// <summary>Adapter that exposes a delegate as a registry shape block.</summary>
    public sealed class DelegateShapeBlock : IShapeBlock
    {
        private readonly Func<AnatomyBuilder, ShapeParams, ShapeResult> _build;
        public string Id { get; }
        public string Category { get; }
        public string Description { get; }

        public DelegateShapeBlock(string id, string category, string description, Func<AnatomyBuilder, ShapeParams, ShapeResult> build)
        {
            Id = id; Category = category; Description = description; _build = build;
        }

        public ShapeResult Build(AnatomyBuilder builder, ShapeParams p) => _build(builder, p);
    }

    /// <summary>Small geometry helpers used by blocks.</summary>
    public static class ShapeMath
    {
        /// <summary>Point on an ellipsoid surface (creature space) in direction 'dir' from its centre (radii in its own frame).</summary>
        public static Vec3 EllipsoidSurface(Vec3 center, Mat3 basis, Vec3 radii, Vec3 localDir)
        {
            Vec3 d = localDir.NormalizedOr(Vec3.UnitX);
            // scale direction so it lands on the surface
            double k = 1.0 / Math.Sqrt(DMath.Sq(d.X / radii.X) + DMath.Sq(d.Y / radii.Y) + DMath.Sq(d.Z / radii.Z));
            return center + basis.Mul(d * k);
        }

        public static Vec3 EllipsoidNormal(Mat3 basis, Vec3 radii, Vec3 localPoint)
        {
            Vec3 g = new Vec3(localPoint.X / (radii.X * radii.X), localPoint.Y / (radii.Y * radii.Y), localPoint.Z / (radii.Z * radii.Z));
            return basis.Mul(g).NormalizedOr(Vec3.UnitY);
        }

        /// <summary>Rotates v around the Z axis (pitch in the creature's sagittal plane).</summary>
        public static Vec3 Pitch(Vec3 v, double angle)
        {
            DMath.SinCos(angle, out double s, out double c);
            return new Vec3(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
        }

        public static Vec3 Yaw(Vec3 v, double angle)
        {
            DMath.SinCos(angle, out double s, out double c);
            return new Vec3(v.X * c + v.Z * s, v.Y, -v.X * s + v.Z * c);
        }

        public static Vec3 FromAngles(double pitch, double yaw)
        {
            DMath.SinCos(pitch, out double sp, out double cp);
            DMath.SinCos(yaw, out double sy, out double cy);
            return new Vec3(cp * cy, sp, -cp * sy);
        }
    }
}
