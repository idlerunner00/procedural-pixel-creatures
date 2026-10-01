// Procedural Pixel Creatures - leg shape block and the shared leg solver.
//
// Legs are built in their standing rest pose. The same solver is used by the motion system at
// runtime, so the rest pose is exactly the pose the gait starts from.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public sealed class LegSpec
    {
        public string Name = "leg";
        public int Side;
        public int Pair;
        /// <summary>Front legs bend the elbow backwards; hind legs bend the knee forwards.</summary>
        public bool Front;
        public int AnchorBone;
        public Vec3 Hip;
        /// <summary>Rest contact point on the ground (y = 0).</summary>
        public Vec3 Contact;
        public LegStyle Style = LegStyle.Digitigrade;
        public double Length = 16;
        /// <summary>Segment fractions: upper, lower, distal (meta or foot).</summary>
        public double Upper = 0.38, Lower = 0.36, Distal = 0.26;
        public double RadiusTop = 3, RadiusKnee = 2, RadiusAnkle = 1.5, RadiusFoot = 1.6;
        /// <summary>Haunch/shoulder muscle size relative to RadiusTop (0 = none).</summary>
        public double Haunch = 1.0;
        public double PawLength = 3;
        public double PawRadius = 1.6;
        public MaterialSlot Material = MaterialSlot.Primary;
        public MaterialSlot FootMaterial = MaterialSlot.Primary;
        public int Claws;
        public double ClawSize = 1;
        public double Phase;
        public double LiftHeight = 3;
        /// <summary>Angle of the distal segment from vertical in degrees (digitigrade hock, carpus).</summary>
        public double MetaAngle = 25;
        public double BlendRadius = 1.5;
        public double DomainLength = 16;
        public bool IsArm;
        /// <summary>Group index to use; -1 creates a new group.</summary>
        public int Group = -1;
        /// <summary>Hands/paws: draw fingers instead of a paw (humanoids).</summary>
        public bool Hand;
        /// <summary>Optional elbow/knee pole hint (forward, up, outward weights).</summary>
        public Vec3 Pole;
        /// <summary>Skip the paw/foot/hoof shape (the caller attaches its own end piece, e.g. a pincer).</summary>
        public bool NoFoot;
    }

    public static class Limbs
    {
        /// <summary>
        /// Solves joint positions of a leg in creature space.
        /// joints[0] hip, [1] knee/elbow, [2] ankle/wrist, [3] foot tip (contact end).
        /// </summary>
        public static void Solve(LegRig leg, Vec3 hip, Vec3 contact, Vec3 forward, Vec3 outward, double toeRoll, Vec3[] joints)
        {
            Vec3 up = Vec3.UnitY;
            double l1 = leg.SegmentLengths[0], l2 = leg.SegmentLengths[1], l3 = leg.SegmentLengths[2];
            double kneeSign = leg.BendSigns.Length > 0 ? leg.BendSigns[0] : 1;
            joints[0] = hip;
            if (leg.IsArm)
            {
                // shoulder -> elbow -> wrist reaching the target; the hand continues the forearm
                Vec3 armPole = leg.Pole.LengthSquared > 1e-9
                    ? forward * leg.Pole.X + up * leg.Pole.Y + outward * leg.Pole.Z
                    : forward * (kneeSign * 0.8) + outward * 0.45 - up * 0.2;
                Vec3 elbow = Kinematics.TwoBone(hip, contact, l1, l2, armPole, out Vec3 wrist);
                Vec3 fore = (wrist - elbow).NormalizedOr(up * -1);
                Vec3 handDir = (fore * 0.85 - up * 0.3 + forward * (0.25 + toeRoll)).NormalizedOr(fore);
                joints[1] = elbow;
                joints[2] = wrist;
                joints[3] = wrist + handDir * l3;
                return;
            }
            switch (leg.Style)
            {
                case LegStyle.Plantigrade:
                case LegStyle.Stump:
                {
                    // heel above the rear of the contact patch, toes forward
                    Vec3 ankle = contact - forward * (l3 * 0.35) + up * leg.SoleHeight;
                    Vec3 pole = forward * kneeSign + outward * 0.15;
                    Vec3 knee = Kinematics.TwoBone(hip, ankle, l1, l2, pole, out Vec3 ankleReached);
                    Vec3 toeDir = (forward * DMath.Cos(toeRoll) - up * DMath.Sin(toeRoll)).Normalized();
                    joints[1] = knee;
                    joints[2] = ankleReached;
                    joints[3] = ankleReached + toeDir * l3 - up * leg.SoleHeight * 0.5;
                    break;
                }
                case LegStyle.Sprawling:
                {
                    Vec3 ankle = contact + up * leg.SoleHeight;
                    Vec3 pole = outward * 1.0 + up * 0.8 + forward * kneeSign * 0.25;
                    Vec3 knee = Kinematics.TwoBone(hip, ankle, l1, l2, pole, out Vec3 ankleReached);
                    joints[1] = knee;
                    joints[2] = ankleReached;
                    joints[3] = ankleReached + (forward * 0.6 + outward * 0.8).Normalized() * l3 - up * leg.SoleHeight * 0.6;
                    break;
                }
                case LegStyle.Arthropod:
                {
                    Vec3 foot = contact + up * leg.SoleHeight;
                    Vec3 pole = up * 1.2 + outward * 0.45;
                    // distal segment points steeply down to the foot
                    Vec3 metaDir = (up * 0.92 + outward * 0.35).Normalized();
                    Kinematics.ThreeBone(hip, foot, l1, l2, l3, metaDir, pole, out Vec3 knee, out Vec3 ankle, out Vec3 footOut);
                    joints[1] = knee;
                    joints[2] = ankle;
                    joints[3] = footOut;
                    break;
                }
                default:
                {
                    // digitigrade / unguligrade: hock (hind) or carpus (front) behind and above the paw
                    Vec3 foot = contact + up * leg.SoleHeight;
                    double a = leg.MetaAngle * DMath.Deg2Rad;
                    Vec3 metaDir = (up * DMath.Cos(a) - forward * DMath.Sin(a)).Normalized();
                    Vec3 pole = forward * kneeSign + outward * 0.12;
                    Kinematics.ThreeBone(hip, foot, l1, l2, l3, metaDir, pole, out Vec3 knee, out Vec3 ankle, out Vec3 footOut);
                    joints[1] = knee;
                    joints[2] = ankle;
                    joints[3] = footOut;
                    break;
                }
            }
        }

        /// <summary>Builds bones, primitives and the rig entry of one leg in its rest pose.</summary>
        public static LegRig Build(AnatomyBuilder b, LegSpec s)
        {
            int group = s.Group >= 0 ? s.Group : b.Group(s.Name, s.BlendRadius, s.Side);
            double l1 = s.Length * s.Upper, l2 = s.Length * s.Lower, l3 = s.Length * s.Distal;
            var leg = new LegRig
            {
                Name = s.Name,
                Side = s.Side,
                Pair = s.Pair,
                AnchorBone = s.AnchorBone,
                SegmentLengths = new[] { l1, l2, l3 },
                TotalLength = l1 + l2 + l3,
                Style = s.Style,
                Phase = s.Phase,
                LiftHeight = s.LiftHeight,
                IsArm = s.IsArm,
                FootRadius = s.PawRadius,
                SoleHeight = s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Stump ? Math.Max(0.6, s.PawRadius * 0.55) : Math.Max(0.6, s.PawRadius * 0.7),
                BendSigns = new[] { s.Front ? -1.0 : 1.0 },
                MetaAngle = s.MetaAngle,
                Group = group,
                Pole = s.Pole,
            };
            Vec3 outward = new Vec3(0, 0, s.Side == 0 ? 1 : s.Side);
            var joints = new Vec3[4];
            Solve(leg, s.Hip, s.Contact, Vec3.UnitX, outward, 0, joints);
            leg.RestFoot = s.Contact;

            Vec3 up = Vec3.UnitX; // bone Y axis leans to the creature front -> theta 0 = front of the leg
            int hip = b.BoneAlong(s.Name + ".hip", s.AnchorBone, joints[0], joints[1] - joints[0], up, BoneRole.LegUpper, s.Side, l1);
            int knee = b.BoneAlong(s.Name + ".knee", hip, joints[1], joints[2] - joints[1], up, BoneRole.LegLower, s.Side, l2);
            int ankle = b.BoneAlong(s.Name + ".ankle", knee, joints[2], joints[3] - joints[2], up, BoneRole.LegMeta, s.Side, l3);
            // feet point forwards; hands (and pincers) point along the hand segment
            int foot = s.IsArm
                ? b.BoneAlong(s.Name + ".foot", ankle, joints[3], joints[3] - joints[2], up, BoneRole.Hand, s.Side)
                : b.BoneAlong(s.Name + ".foot", ankle, joints[3], Vec3.UnitX, Vec3.UnitY, BoneRole.Foot, s.Side);
            leg.RootBone = hip;
            leg.JointBones = new[] { knee, ankle, foot };
            leg.ToeBones = new[] { foot };

            double len = s.DomainLength > 0 ? s.DomainLength : leg.TotalLength;
            double u1 = l1 / leg.TotalLength, u2 = (l1 + l2) / leg.TotalLength;
            var mat = s.Material;
            // haunch / shoulder muscle gives near legs a readable contour against the body
            if (s.Haunch > 0.05)
            {
                double hl = l1 * 0.62;
                var h = b.Ellipsoid(group, hip, new Vec3(l1 * 0.26, 0, 0), new Vec3(hl * 0.6, s.RadiusTop * 1.18 * s.Haunch, s.RadiusTop * 0.95 * s.Haunch),
                    mat, PatternDomain.Limb, 0, u1 * 0.6, null, s.Side);
                h.DomainLength = len;
                h.Tag = s.Name + ".haunch";
            }
            var p1 = b.Segment(group, hip, s.RadiusTop, knee, s.RadiusKnee, mat, PatternDomain.Limb, 0, u1, s.Side);
            p1.DomainLength = len; p1.Tag = s.Name + ".upper";
            var p2 = b.Segment(group, knee, s.RadiusKnee, ankle, s.RadiusAnkle, mat, PatternDomain.Limb, u1, u2, s.Side);
            p2.DomainLength = len; p2.Tag = s.Name + ".lower";
            if (s.NoFoot)
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, Math.Max(0.45, s.RadiusFoot), mat, PatternDomain.Limb, u2, 1, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".distal";
            }
            else if (s.Style == LegStyle.Plantigrade || s.Style == LegStyle.Stump)
            {
                if (s.Hand)
                {
                    BuildHand(b, group, ankle, foot, s, len);
                }
                else
                {
                    // flat foot: ellipsoid from heel to toes
                    var f = b.Ellipsoid(group, foot, new Vec3(-l3 * 0.42, s.PawRadius * 0.1, 0), new Vec3(l3 * 0.62, s.PawRadius * 0.62, s.PawRadius * 0.95),
                        s.FootMaterial, PatternDomain.Limb, 0.9, 1.0, null, s.Side);
                    f.DomainLength = len; f.Tag = s.Name + ".foot";
                    var c = b.Segment(group, ankle, s.RadiusAnkle, foot, s.PawRadius * 0.7, s.FootMaterial, PatternDomain.Limb, u2, 1, s.Side);
                    c.DomainLength = len;
                }
            }
            else if (s.Style == LegStyle.Arthropod)
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, Math.Max(0.45, s.RadiusFoot * 0.6), mat, PatternDomain.Limb, u2, 1, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".distal";
            }
            else
            {
                var p3 = b.Segment(group, ankle, s.RadiusAnkle, foot, s.RadiusFoot, mat, PatternDomain.Limb, u2, 0.95, s.Side);
                p3.DomainLength = len; p3.Tag = s.Name + ".meta";
                if (s.Style == LegStyle.Unguligrade)
                {
                    var hoof = b.Cone(group, foot, new Vec3(0.2, s.PawRadius * 0.3, 0), s.PawRadius * 0.95, foot, new Vec3(s.PawLength * 0.35, s.PawRadius * 0.15, 0), s.PawRadius * 0.85,
                        MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    hoof.ShadeBias = -1;
                    hoof.Tag = s.Name + ".hoof";
                    hoof.Flags |= PrimFlags.NoPattern;
                }
                else
                {
                    var paw = b.Ellipsoid(group, foot, new Vec3(s.PawLength * 0.28, s.PawRadius * 0.12, 0),
                        new Vec3(s.PawLength * 0.55, s.PawRadius * 0.7, s.PawRadius * 0.95), s.FootMaterial, PatternDomain.Limb, 0.95, 1.0, null, s.Side);
                    paw.DomainLength = len; paw.Tag = s.Name + ".paw";
                }
            }
            if (s.Claws > 0 && s.ClawSize > 0.2 && !s.Hand && !s.NoFoot)
            {
                for (int i = 0; i < s.Claws; i++)
                {
                    double zo = s.Claws == 1 ? 0 : (i / (double)(s.Claws - 1) - 0.5) * s.PawRadius * 1.1;
                    Vec3 a = new Vec3(s.PawLength * 0.62, s.PawRadius * 0.25, zo);
                    Vec3 tip = a + new Vec3(s.ClawSize * 1.1, -s.PawRadius * 0.35 - 0.4, zo * 0.2);
                    var claw = b.Cone(group, foot, a, Math.Max(0.45, s.ClawSize * 0.45), foot, tip, 0.3, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    claw.MinQuality = 1;
                    claw.Tag = s.Name + ".claw";
                }
            }
            return leg;
        }

        private static void BuildHand(AnatomyBuilder b, int group, int wrist, int hand, LegSpec s, double len)
        {
            // mitt shaped hand with a thumb; fingers are implied by the silhouette at pixel scale
            var palm = b.Ellipsoid(group, hand, new Vec3(-s.PawLength * 0.25, 0, 0), new Vec3(s.PawLength * 0.55, s.PawRadius * 0.9, s.PawRadius * 0.75),
                s.FootMaterial, PatternDomain.Limb, 0.92, 1.0, null, s.Side);
            palm.DomainLength = len; palm.Tag = s.Name + ".palm";
            var wristC = b.Segment(group, wrist, s.RadiusAnkle, hand, s.PawRadius * 0.75, s.FootMaterial, PatternDomain.Limb, 0.8, 0.95, s.Side);
            wristC.DomainLength = len;
            if (s.Claws > 0 && s.ClawSize > 0.2)
            {
                for (int i = 0; i < Math.Min(3, s.Claws); i++)
                {
                    double zo = (i - 1) * s.PawRadius * 0.55;
                    var claw = b.Cone(group, hand, new Vec3(s.PawLength * 0.2, -s.PawRadius * 0.2, zo), 0.5, hand,
                        new Vec3(s.PawLength * 0.2 + s.ClawSize, -s.PawRadius * 0.7, zo * 1.1), 0.3, MaterialSlot.Accent, PatternDomain.None, 0, 1, s.Side);
                    claw.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard;
                    claw.MinQuality = 1;
                }
            }
        }
    }
}
