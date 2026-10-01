// Procedural Pixel Creatures - immutable anatomy and the builder used by families and shape blocks.

using System;
using System.Collections.Generic;

namespace PixelCreatures.Core.Anatomy
{
    public sealed class CreatureAnatomy
    {
        public string FamilyId { get; }
        public Skeleton Skeleton { get; }
        public IReadOnlyList<PartGroup> Groups { get; }
        public IReadOnlyList<PrimitiveDef> Primitives { get; }
        public IReadOnlyList<FeatureDef> Features { get; }
        public MotionRig Rig { get; }
        public AnatomyMetrics Metrics { get; }
        public CanvasSpec Canvas { get; }
        /// <summary>Constraint repairs applied while building (human readable, German).</summary>
        public IReadOnlyList<string> Repairs { get; }
        /// <summary>Free form descriptive tags (archetype, notable features) for UI and reports.</summary>
        public IReadOnlyList<string> Traits { get; }
        public ulong AnatomyKey { get; }

        internal CreatureAnatomy(string familyId, Skeleton skeleton, List<PartGroup> groups, List<PrimitiveDef> primitives,
            List<FeatureDef> features, MotionRig rig, AnatomyMetrics metrics, CanvasSpec canvas, List<string> repairs,
            List<string> traits, ulong key)
        {
            FamilyId = familyId; Skeleton = skeleton; Groups = groups; Primitives = primitives; Features = features;
            Rig = rig; Metrics = metrics; Canvas = canvas; Repairs = repairs; Traits = traits; AnatomyKey = key;
        }

        public int FindGroup(string name)
        {
            for (int i = 0; i < Groups.Count; i++) if (Groups[i].Name == name) return i;
            return -1;
        }
    }

    /// <summary>
    /// Mutable construction API for anatomies. Families and shape blocks add bones, groups,
    /// primitives and features; Build() validates the result and measures metrics and canvas.
    /// </summary>
    public sealed class AnatomyBuilder
    {
        private readonly List<BoneDef> _bones = new List<BoneDef>();
        private readonly List<PartGroup> _groups = new List<PartGroup>();
        private readonly List<PrimitiveDef> _prims = new List<PrimitiveDef>();
        private readonly List<FeatureDef> _features = new List<FeatureDef>();
        private readonly List<string> _repairs = new List<string>();
        private readonly List<string> _traits = new List<string>();
        private Skeleton? _skeletonCache;

        public string FamilyId { get; }
        public ulong Seed { get; }
        public MotionRig Rig { get; } = new MotionRig();
        public AnatomyMetrics Metrics { get; } = new AnatomyMetrics();
        /// <summary>Extra canvas margin in pixels for animation reach (added on all sides).</summary>
        public double AnimationMargin { get; set; } = 6;
        /// <summary>Extra headroom above the creature (jumps, raised heads, flight).</summary>
        public double ExtraHeadroom { get; set; } = 4;

        public AnatomyBuilder(string familyId, ulong seed)
        {
            FamilyId = familyId;
            Seed = seed;
        }

        public IReadOnlyList<BoneDef> Bones => _bones;
        public IReadOnlyList<PrimitiveDef> Primitives => _prims;
        public IReadOnlyList<PartGroup> Groups => _groups;
        public IReadOnlyList<FeatureDef> Features => _features;

        /// <summary>Deterministic sub-stream for one part of the anatomy (keeps parts independent).</summary>
        public Rng RngFor(string part) => Rng.Derive(Seed, "anatomy." + part);

        public void Repair(string message) => _repairs.Add(message);
        public void Trait(string trait) { if (!_traits.Contains(trait)) _traits.Add(trait); }

        // ------------------------------------------------------------------ bones

        public int Bone(string name, int parent, Vec3 offset, Quat rotation, double length, BoneRole role, int side = 0)
        {
            foreach (var b in _bones)
                if (b.Name == name) throw new InvalidOperationException($"Duplicate bone '{name}'.");
            _bones.Add(new BoneDef(name, parent, offset, rotation, length, role, side));
            _skeletonCache = null;
            return _bones.Count - 1;
        }

        public int Bone(string name, int parent, Vec3 offset, BoneRole role, int side = 0, double length = 0) =>
            Bone(name, parent, offset, Quat.Identity, length, role, side);

        public int FindBone(string name)
        {
            for (int i = 0; i < _bones.Count; i++) if (_bones[i].Name == name) return i;
            return -1;
        }

        /// <summary>Rest pose world transform of a bone (valid while building).</summary>
        public Xform RestWorld(int bone)
        {
            _skeletonCache ??= new Skeleton(_bones);
            return _skeletonCache.RestWorld(bone);
        }

        /// <summary>Adds a child bone whose origin sits at a given creature-space rest position.</summary>
        public int BoneAt(string name, int parent, Vec3 worldPos, BoneRole role, int side = 0, double length = 0)
        {
            Vec3 local = parent < 0 ? worldPos : RestWorld(parent).InversePoint(worldPos);
            return Bone(name, parent, local, Quat.Identity, length, role, side);
        }

        /// <summary>Adds a bone oriented so that its local +X axis points along 'forward' in creature space.</summary>
        public int BoneAlong(string name, int parent, Vec3 worldPos, Vec3 forward, Vec3 up, BoneRole role, int side = 0, double length = 0)
        {
            var parentXf = parent < 0 ? Xform.Identity : RestWorld(parent);
            Vec3 local = parentXf.InversePoint(worldPos);
            var worldBasis = Mat3.LookAlong(forward, up);
            var localBasis = parentXf.Basis.Transposed * worldBasis;
            return Bone(name, parent, local, QuatFromBasis(localBasis), length, role, side);
        }

        public static Quat QuatFromBasis(Mat3 m)
        {
            double m00 = m.C0.X, m11 = m.C1.Y, m22 = m.C2.Z;
            double trace = m00 + m11 + m22;
            double x, y, z, w;
            if (trace > 0)
            {
                double s = Math.Sqrt(trace + 1.0) * 2;
                w = 0.25 * s;
                x = (m.C1.Z - m.C2.Y) / s;
                y = (m.C2.X - m.C0.Z) / s;
                z = (m.C0.Y - m.C1.X) / s;
            }
            else if (m00 > m11 && m00 > m22)
            {
                double s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2;
                w = (m.C1.Z - m.C2.Y) / s;
                x = 0.25 * s;
                y = (m.C1.X + m.C0.Y) / s;
                z = (m.C2.X + m.C0.Z) / s;
            }
            else if (m11 > m22)
            {
                double s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2;
                w = (m.C2.X - m.C0.Z) / s;
                x = (m.C1.X + m.C0.Y) / s;
                y = 0.25 * s;
                z = (m.C2.Y + m.C1.Z) / s;
            }
            else
            {
                double s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2;
                w = (m.C0.Y - m.C1.X) / s;
                x = (m.C2.X + m.C0.Z) / s;
                y = (m.C2.Y + m.C1.Z) / s;
                z = 0.25 * s;
            }
            return new Quat(x, y, z, w).Normalized();
        }

        // ------------------------------------------------------------------ groups & primitives

        public int Group(string name, double blendRadius, int side = 0, GroupFlags flags = GroupFlags.None, double depthBias = 0)
        {
            _groups.Add(new PartGroup(name, blendRadius, side, flags, depthBias));
            _groups[_groups.Count - 1].Index = _groups.Count - 1;
            return _groups.Count - 1;
        }

        public PrimitiveDef Ellipsoid(int group, int bone, Vec3 center, Vec3 radii, MaterialSlot material,
            PatternDomain domain = PatternDomain.None, double u0 = 0, double u1 = 1, Quat? rotation = null, int side = 0)
        {
            var p = new PrimitiveDef
            {
                Kind = PrimKind.Ellipsoid, Group = group, Material = material, BoneA = bone, LocalA = center,
                Radii = new Vec3(Math.Max(0.35, radii.X), Math.Max(0.35, radii.Y), Math.Max(0.35, radii.Z)),
                LocalRotation = rotation ?? Quat.Identity, Domain = domain, U0 = u0, U1 = u1, Side = side,
            };
            _prims.Add(p);
            return p;
        }

        public PrimitiveDef Cone(int group, int boneA, Vec3 a, double ra, int boneB, Vec3 b, double rb, MaterialSlot material,
            PatternDomain domain = PatternDomain.None, double u0 = 0, double u1 = 1, int side = 0)
        {
            var p = new PrimitiveDef
            {
                Kind = PrimKind.RoundCone, Group = group, Material = material, BoneA = boneA, LocalA = a, BoneB = boneB, LocalB = b,
                RadiusA = Math.Max(0.3, ra), RadiusB = Math.Max(0.3, rb), Domain = domain, U0 = u0, U1 = u1, Side = side,
            };
            _prims.Add(p);
            return p;
        }

        /// <summary>Round cone between the origins of two bones.</summary>
        public PrimitiveDef Segment(int group, int boneA, double ra, int boneB, double rb, MaterialSlot material,
            PatternDomain domain = PatternDomain.None, double u0 = 0, double u1 = 1, int side = 0) =>
            Cone(group, boneA, Vec3.Zero, ra, boneB, Vec3.Zero, rb, material, domain, u0, u1, side);

        public PrimitiveDef Triangle(int group, int boneA, Vec3 a, int boneB, Vec3 b, int boneC, Vec3 c, MaterialSlot material,
            PatternDomain domain = PatternDomain.None, int side = 0, PrimFlags flags = PrimFlags.TwoSided)
        {
            var p = new PrimitiveDef
            {
                Kind = PrimKind.Triangle, Group = group, Material = material, BoneA = boneA, LocalA = a, BoneB = boneB, LocalB = b,
                BoneC = boneC, LocalC = c, Domain = domain, Side = side, Flags = flags,
            };
            _prims.Add(p);
            return p;
        }

        public FeatureDef Feature(FeatureDef f)
        {
            _features.Add(f);
            return f;
        }

        // ------------------------------------------------------------------ build

        public CreatureAnatomy Build(ulong anatomyKey)
        {
            var skeleton = new Skeleton(_bones);
            ValidateAndRepair(skeleton);
            var canvas = ComputeCanvas(skeleton);
            Rig.Root = Rig.Root < 0 ? 0 : Rig.Root;
            return new CreatureAnatomy(FamilyId, skeleton, new List<PartGroup>(_groups), new List<PrimitiveDef>(_prims),
                new List<FeatureDef>(_features), Rig, Metrics, canvas, new List<string>(_repairs), new List<string>(_traits), anatomyKey);
        }

        private void ValidateAndRepair(Skeleton skeleton)
        {
            foreach (var p in _prims)
            {
                if (p.BoneA < 0 || p.BoneA >= skeleton.Count) throw new InvalidOperationException($"Primitive '{p.Tag}' references an invalid bone.");
                if (p.Kind != PrimKind.Ellipsoid && (p.BoneB < 0 || p.BoneB >= skeleton.Count))
                    throw new InvalidOperationException($"Primitive '{p.Tag}' references an invalid second bone.");
                if (p.Kind == PrimKind.Triangle && (p.BoneC < 0 || p.BoneC >= skeleton.Count))
                    throw new InvalidOperationException($"Primitive '{p.Tag}' references an invalid third bone.");
                if (p.Group < 0 || p.Group >= _groups.Count) throw new InvalidOperationException($"Primitive '{p.Tag}' references an invalid group.");
                if (!p.LocalA.IsFinite || !p.LocalB.IsFinite || !p.LocalC.IsFinite || !p.Radii.IsFinite ||
                    !DMath.IsFinite(p.RadiusA) || !DMath.IsFinite(p.RadiusB))
                    throw new InvalidOperationException($"Primitive '{p.Tag}' has non finite parameters.");
            }
            foreach (var b in skeleton.Bones)
            {
                if (!b.RestOffset.IsFinite || !b.RestRotation.IsFinite)
                    throw new InvalidOperationException($"Bone '{b.Name}' has non finite rest data.");
            }
        }

        /// <summary>Rest pose bounds of all primitives in creature space.</summary>
        public static void MeasureBounds(Skeleton skeleton, IReadOnlyList<PrimitiveDef> prims, out double horizontalRadius,
            out double minY, out double maxY, out double minX, out double maxX)
        {
            horizontalRadius = 1; minY = 0; maxY = 1; minX = 0; maxX = 0;
            foreach (var p in prims)
            {
                var xa = skeleton.RestWorld(p.BoneA);
                switch (p.Kind)
                {
                    case PrimKind.Ellipsoid:
                    {
                        Vec3 c = xa.Point(p.LocalA);
                        double r = Math.Max(p.Radii.X, Math.Max(p.Radii.Y, p.Radii.Z));
                        Accumulate(c, r, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        break;
                    }
                    case PrimKind.RoundCone:
                    {
                        Accumulate(xa.Point(p.LocalA), p.RadiusA, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        Accumulate(skeleton.RestWorld(p.BoneB).Point(p.LocalB), p.RadiusB, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        break;
                    }
                    default:
                    {
                        Accumulate(xa.Point(p.LocalA), 0.5, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        Accumulate(skeleton.RestWorld(p.BoneB).Point(p.LocalB), 0.5, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        Accumulate(skeleton.RestWorld(p.BoneC).Point(p.LocalC), 0.5, ref horizontalRadius, ref minY, ref maxY, ref minX, ref maxX);
                        break;
                    }
                }
            }
        }

        private static void Accumulate(Vec3 c, double r, ref double hr, ref double minY, ref double maxY, ref double minX, ref double maxX)
        {
            double h = Math.Sqrt(c.X * c.X + c.Z * c.Z) + r;
            if (h > hr) hr = h;
            if (c.Y - r < minY) minY = c.Y - r;
            if (c.Y + r > maxY) maxY = c.Y + r;
            if (c.X - r < minX) minX = c.X - r;
            if (c.X + r > maxX) maxX = c.X + r;
        }

        private CanvasSpec ComputeCanvas(Skeleton skeleton)
        {
            MeasureBounds(skeleton, _prims, out double hr, out double minY, out double maxY, out double minX, out double maxX);
            Metrics.HorizontalRadius = hr;
            Metrics.TopHeight = maxY;
            if (Metrics.BodyLength <= 0) Metrics.BodyLength = maxX - minX;
            double e = PixelCamera.DefaultElevation;
            DMath.SinCos(e, out double se, out double ce);
            double margin = AnimationMargin;
            double r = hr + margin;
            double top = (maxY + ExtraHeadroom + margin) * ce + r * se;
            double bottom = -Math.Min(0, minY) * ce + r * se + 2;
            int width = 2 * (int)Math.Ceiling(r) + 4;
            int height = (int)Math.Ceiling(top + bottom) + 4;
            // even sizes keep the origin centred
            if ((width & 1) == 1) width++;
            if ((height & 1) == 1) height++;
            int ox = width / 2;
            int oy = (int)Math.Ceiling(top) + 2;
            return new CanvasSpec(width, height, ox, oy);
        }
    }
}
