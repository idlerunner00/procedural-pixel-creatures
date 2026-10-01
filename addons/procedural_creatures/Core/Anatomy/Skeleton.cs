// Procedural Pixel Creatures - skeleton and pose buffers.
//
// Bones form a hierarchy with a rest pose (offset + rotation relative to the parent). A pose is
// computed per animation sample: motion modules set local rotations/offsets/scales or override
// whole world transforms (IK chains, simulated tails). Solve() runs forward kinematics once.
// World transforms are expressed in creature space: +X forward, +Y up, +Z to the creature's right,
// origin on the ground below the root.

using System;
using System.Collections.Generic;

namespace PixelCreatures.Core.Anatomy
{
    public sealed class Skeleton
    {
        private readonly BoneDef[] _bones;
        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Xform[] _restWorld;

        public IReadOnlyList<BoneDef> Bones => _bones;
        public int Count => _bones.Length;

        public Skeleton(IList<BoneDef> bones)
        {
            _bones = new BoneDef[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                var b = bones[i];
                if (b.Parent >= i) throw new ArgumentException($"Bone '{b.Name}' must come after its parent.");
                b.Index = i;
                _bones[i] = b;
                _byName[b.Name] = i;
            }
            _restWorld = new Xform[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                var b = _bones[i];
                var local = Xform.FromQuat(b.RestRotation, b.RestOffset);
                _restWorld[i] = b.Parent < 0 ? local : _restWorld[b.Parent] * local;
            }
        }

        public int Find(string name) => _byName.TryGetValue(name, out int i) ? i : -1;

        public int Require(string name)
        {
            int i = Find(name);
            if (i < 0) throw new KeyNotFoundException($"Bone '{name}' does not exist.");
            return i;
        }

        public Xform RestWorld(int bone) => _restWorld[bone];

        public BoneDef this[int i] => _bones[i];
    }

    /// <summary>Mutable pose buffer. One instance per creature instance (reused every sample).</summary>
    public sealed class SkeletonPose
    {
        public readonly Skeleton Skeleton;
        public readonly Xform[] World;
        public readonly Quat[] LocalRotation;
        public readonly Vec3[] LocalOffset;
        public readonly Vec3[] Scale;
        public readonly bool[] HasOverride;
        public readonly Xform[] OverrideWorld;

        public SkeletonPose(Skeleton skeleton)
        {
            Skeleton = skeleton;
            int n = skeleton.Count;
            World = new Xform[n];
            LocalRotation = new Quat[n];
            LocalOffset = new Vec3[n];
            Scale = new Vec3[n];
            HasOverride = new bool[n];
            OverrideWorld = new Xform[n];
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < World.Length; i++)
            {
                LocalRotation[i] = Quat.Identity;
                LocalOffset[i] = Vec3.Zero;
                Scale[i] = Vec3.One;
                HasOverride[i] = false;
                World[i] = Skeleton.RestWorld(i);
            }
        }

        public void SetOverride(int bone, Xform world)
        {
            if (bone < 0) return;
            HasOverride[bone] = true;
            OverrideWorld[bone] = world;
        }

        /// <summary>Forward kinematics. Overridden bones use their override, children follow.</summary>
        public void Solve()
        {
            var bones = Skeleton.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                if (HasOverride[i])
                {
                    World[i] = ApplyScale(OverrideWorld[i], Scale[i]);
                    continue;
                }
                var b = bones[i];
                var rot = Mat3.FromQuat(b.RestRotation * LocalRotation[i]);
                var s = Scale[i];
                if (s.X != 1 || s.Y != 1 || s.Z != 1) rot = rot.ScaledColumns(s.X, s.Y, s.Z);
                var local = new Xform(rot, b.RestOffset + LocalOffset[i]);
                World[i] = b.Parent < 0 ? local : World[b.Parent] * local;
            }
        }

        private static Xform ApplyScale(Xform xf, Vec3 s)
        {
            if (s.X == 1 && s.Y == 1 && s.Z == 1) return xf;
            return new Xform(xf.Basis.ScaledColumns(s.X, s.Y, s.Z), xf.Origin);
        }

        public void CopyFrom(SkeletonPose other)
        {
            Array.Copy(other.World, World, World.Length);
            Array.Copy(other.LocalRotation, LocalRotation, World.Length);
            Array.Copy(other.LocalOffset, LocalOffset, World.Length);
            Array.Copy(other.Scale, Scale, World.Length);
            Array.Copy(other.HasOverride, HasOverride, World.Length);
            Array.Copy(other.OverrideWorld, OverrideWorld, World.Length);
        }
    }
}
