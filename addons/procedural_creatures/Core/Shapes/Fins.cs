// Procedural Pixel Creatures - fins (pectoral, pelvic, dorsal, anal, ray wings) and pincers.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public enum FinShape : byte
    {
        /// <summary>Pointed triangular fin.</summary>
        Pointed,
        /// <summary>Rounded paddle (flattened ellipsoid).</summary>
        Paddle,
        /// <summary>Fan of rays with a scalloped edge.</summary>
        Fan,
        /// <summary>Tall, swept back blade (shark dorsal fin).</summary>
        Blade,
    }

    public sealed class FinSpec
    {
        public string Name = "fin";
        public FinKind Kind = FinKind.Pectoral;
        public FinShape Shape = FinShape.Pointed;
        public int AnchorBone;
        /// <summary>Fin base in creature space (right side for paired fins; mirrored for the left).</summary>
        public Vec3 Base;
        /// <summary>Direction the fin points to (right side), creature space.</summary>
        public Vec3 Direction = new Vec3(-0.4, -0.3, 1);
        public double Length = 5;
        /// <summary>Width of the fin base (along the body).</summary>
        public double Width = 3;
        public bool Paired = true;
        public MaterialSlot Material = MaterialSlot.Membrane;
        /// <summary>Oscillation: local axis, amplitude (radians) and phase offset between left and right.</summary>
        public Vec3 Axis = Vec3.UnitY;
        public double Amplitude = 0.45;
        public double Phase;
        public double SidePhase = DMath.Pi;
        public double RestAngle;
        /// <summary>Oscillating fins get a FinRig; fixed fins (dorsal) only geometry.</summary>
        public bool Animated = true;
    }

    public static class Fins
    {
        public static void Build(AnatomyBuilder b, FinSpec s)
        {
            if (!s.Paired)
            {
                BuildOne(b, s, 0);
                return;
            }
            for (int side = -1; side <= 1; side += 2) BuildOne(b, s, side);
        }

        private static void BuildOne(AnatomyBuilder b, FinSpec s, int side)
        {
            int m = side == 0 ? 1 : side;
            Vec3 basePos = new Vec3(s.Base.X, s.Base.Y, s.Base.Z * m);
            Vec3 dir = new Vec3(s.Direction.X, s.Direction.Y, s.Direction.Z * m).NormalizedOr(Vec3.UnitY);
            string name = side == 0 ? s.Name : $"{s.Name}.{(side < 0 ? "L" : "R")}";
            Vec3 up = Math.Abs(dir.Y) > 0.9 ? Vec3.UnitX : Vec3.UnitY;
            int bone = b.BoneAlong(name, s.AnchorBone, basePos, dir, up, BoneRole.Fin, side, s.Length);
            int g = b.Group(name, 0.4, side, GroupFlags.NoFarShade | GroupFlags.NoContour, -0.2);
            var xf = b.RestWorld(bone);
            // fin plane: spanned by the fin direction and the body axis (+X creature)
            Vec3 along = xf.InverseDir(Vec3.UnitX).NormalizedOr(Vec3.UnitY);
            // remove the component along the fin axis so the base runs perpendicular to it
            along = (along - Vec3.UnitX * along.X).NormalizedOr(Vec3.UnitZ);
            double L = s.Length, W = s.Width;
            PrimitiveDef Tri(Vec3 a, Vec3 c, Vec3 d)
            {
                var t = b.Triangle(g, bone, a, bone, c, bone, d, s.Material, PatternDomain.Fin, side);
                t.Flags |= PrimFlags.Translucent | PrimFlags.TwoSided | PrimFlags.NoShadow;
                t.DomainLength = L;
                t.Tag = name;
                return t;
            }
            Vec3 front = along * (W * 0.5), rear = along * (-W * 0.5);
            switch (s.Shape)
            {
                case FinShape.Paddle:
                {
                    var e = b.Ellipsoid(g, bone, new Vec3(L * 0.5, 0, 0), new Vec3(L * 0.55, 0.45, W * 0.55), s.Material, PatternDomain.Fin, 0, 1,
                        QuatAlongPlane(along), side);
                    e.Flags |= PrimFlags.Translucent | PrimFlags.NoShadow;
                    e.DomainLength = L;
                    e.Tag = name;
                    break;
                }
                case FinShape.Fan:
                {
                    int rays = 3;
                    Vec3 prev = front * 0.8 + new Vec3(L * 0.55, 0, 0);
                    Tri(front * 0.6, rear * 0.6, prev);
                    for (int k = 1; k <= rays; k++)
                    {
                        double t = k / (double)rays;
                        Vec3 tip = Vec3.Lerp(front * 1.3, rear * 1.6, t) + new Vec3(L * DMath.Lerp(1.0, 0.8, t), 0, 0);
                        Tri(Vec3.Zero, prev, tip);
                        prev = tip;
                    }
                    Tri(Vec3.Zero, prev, rear * 0.7);
                    break;
                }
                case FinShape.Blade:
                    Tri(front, rear, new Vec3(L, 0, 0) + rear * 1.1);
                    Tri(front, new Vec3(L, 0, 0) + rear * 1.1, new Vec3(L * 0.55, 0, 0) + front * 0.1);
                    break;
                default:
                    Tri(front, rear, new Vec3(L, 0, 0) + rear * 0.35);
                    break;
            }
            if (s.Animated)
            {
                b.Rig.Fins.Add(new FinRig
                {
                    Name = name,
                    Bone = bone,
                    Side = side,
                    Kind = s.Kind,
                    Axis = s.Axis,
                    Amplitude = s.Amplitude,
                    Phase = s.Phase + (side < 0 ? s.SidePhase : 0),
                    RestAngle = s.RestAngle * (side < 0 ? -1 : 1),
                });
            }
        }

        /// <summary>Rotation that maps the ellipsoid's local Z (width) onto the given in-plane direction.</summary>
        private static Quat QuatAlongPlane(Vec3 along)
        {
            // along lies in the bone's YZ plane; rotate about X so that +Z aligns with it
            double a = DMath.Atan2(-along.Y, along.Z);
            return Quat.RotX(a);
        }
    }

    public sealed class PincerSpec
    {
        public string Name = "pincer";
        /// <summary>Bone the claw hangs from (the arm's hand/foot bone, +X along the hand).</summary>
        public int HandBone;
        public int Side;
        public double Size = 4;
        /// <summary>Palm bulk relative to size.</summary>
        public double Bulk = 1;
        public MaterialSlot Material = MaterialSlot.Primary;
        public MaterialSlot TipMaterial = MaterialSlot.Accent;
        public int Group = -1;
        /// <summary>Mantis style: a single folding blade instead of a two-fingered claw.</summary>
        public bool Blade;
    }

    public static class Pincers
    {
        public static void Build(AnatomyBuilder b, PincerSpec s)
        {
            int g = s.Group >= 0 ? s.Group : b.Group(s.Name, 0.8, s.Side);
            double k = s.Size;
            if (s.Blade)
            {
                // raptorial foreleg: a spiked blade that folds against the forearm
                int blade = b.Bone(s.Name + ".blade", s.HandBone, Vec3.Zero, Quat.Identity, k, BoneRole.Hand, s.Side);
                var bl = b.Cone(g, blade, Vec3.Zero, 0.7 * s.Bulk, blade, new Vec3(k * 1.1, -k * 0.25, 0), 0.35, s.Material, PatternDomain.Limb, 0.8, 1, s.Side);
                bl.Tag = s.Name + ".blade";
                for (int i = 0; i < 2; i++)
                {
                    Vec3 a = new Vec3(k * (0.35 + 0.35 * i), -0.4, 0);
                    var sp = b.Cone(g, blade, a, 0.45, blade, a + new Vec3(0.2, -0.9, 0), 0.3, s.TipMaterial);
                    sp.Flags |= PrimFlags.Thin | PrimFlags.Hard | PrimFlags.NoPattern | PrimFlags.NoShadow;
                    sp.MinQuality = 1;
                }
                b.Rig.Grippers.Add(new GripperRig { Bone = blade, Axis = Vec3.UnitZ, OpenAngle = 0.9, Side = s.Side });
                return;
            }
            // palm (chela), fixed finger on top, movable finger below
            var palm = b.Ellipsoid(g, s.HandBone, new Vec3(k * 0.1, 0, 0), new Vec3(k * 0.55, k * 0.42 * s.Bulk, k * 0.34 * s.Bulk), s.Material, PatternDomain.Limb, 0.8, 0.95, null, s.Side);
            palm.Tag = s.Name + ".palm";
            palm.DomainLength = k * 3;
            var fixedF = b.Cone(g, s.HandBone, new Vec3(k * 0.45, k * 0.12, 0), k * 0.24 * s.Bulk, s.HandBone, new Vec3(k * 1.25, k * 0.02, 0), 0.4, s.Material, PatternDomain.Limb, 0.95, 1, s.Side);
            fixedF.Tag = s.Name + ".finger";
            fixedF.DomainLength = k * 3;
            var tipF = b.Cone(g, s.HandBone, new Vec3(k * 1.05, k * 0.05, 0), 0.5, s.HandBone, new Vec3(k * 1.35, -k * 0.08, 0), 0.3, s.TipMaterial);
            tipF.Flags |= PrimFlags.NoPattern | PrimFlags.Thin;
            int mov = b.Bone(s.Name + ".dactyl", s.HandBone, new Vec3(k * 0.4, -k * 0.2, 0), Quat.Identity, k, BoneRole.Hand, s.Side);
            var mf = b.Cone(g, mov, Vec3.Zero, k * 0.22 * s.Bulk, mov, new Vec3(k * 0.85, -k * 0.02, 0), 0.4, s.Material, PatternDomain.Limb, 0.95, 1, s.Side);
            mf.Tag = s.Name + ".dactyl";
            mf.DomainLength = k * 3;
            var mt = b.Cone(g, mov, new Vec3(k * 0.7, 0, 0), 0.45, mov, new Vec3(k * 0.98, k * 0.1, 0), 0.3, s.TipMaterial);
            mt.Flags |= PrimFlags.NoPattern | PrimFlags.Thin;
            b.Rig.Grippers.Add(new GripperRig { Bone = mov, Axis = Vec3.UnitZ, OpenAngle = -0.75, Side = s.Side });
        }
    }
}
