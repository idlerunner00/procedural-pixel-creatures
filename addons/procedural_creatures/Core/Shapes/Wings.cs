// Procedural Pixel Creatures - wing shape block: membrane wings (bats, dragons), feathered wings
// (birds, griffins) and insect wings. Wings are built spread; RigDriver folds and flaps them.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public sealed class WingSpec
    {
        public WingKind Kind = WingKind.Membrane;
        public string Name = "wing";
        /// <summary>Body bone carrying the wing (chest / thorax).</summary>
        public int AnchorBone;
        /// <summary>Right wing root in creature space (mirrored for the left wing).</summary>
        public Vec3 Root;
        /// <summary>Tip distance from the root in the spread pose (pixels).</summary>
        public double Span = 20;
        /// <summary>Depth of the wing (front to back) near the body.</summary>
        public double Chord = 8;
        /// <summary>Rest elevation and backward sweep of the spread wing (radians).</summary>
        public double Raise = 0.2, SweepBack = 0.3;
        public double BoneRadius = 1.0;
        /// <summary>Membrane wings: number of fingers spanning the membrane.</summary>
        public int Fingers = 3;
        /// <summary>Body bone and right side creature-space point where the membrane's trailing edge attaches.</summary>
        public int BodyBone = -1;
        public Vec3 BodyAttach;
        public bool ThumbClaw = true;
        public double ClawSize = 1;
        public MaterialSlot BoneMaterial = MaterialSlot.Primary;
        public MaterialSlot MembraneMaterial = MaterialSlot.Membrane;
        /// <summary>Feathered: darker flight feather tips.</summary>
        public MaterialSlot TipMaterial = MaterialSlot.Marking;
        /// <summary>Insects: pair index (0 fore, 1 hind) and length of this pair relative to Span.</summary>
        public int Pair;
        /// <summary>Scalloped trailing edge between membrane fingers (0..1).</summary>
        public double Scallop = 0.18;
        /// <summary>Feathered: number of separated primary feathers at the tip.</summary>
        public int Primaries = 3;
    }

    public static class Wings
    {
        /// <summary>Builds both wings and adds them to the rig.</summary>
        public static void BuildPair(AnatomyBuilder b, WingSpec s)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var rig = s.Kind switch
                {
                    WingKind.Feathered => BuildFeathered(b, s, side),
                    WingKind.Insect => BuildInsect(b, s, side),
                    _ => BuildMembrane(b, s, side),
                };
                b.Rig.Wings.Add(rig);
            }
        }

        private static Vec3 Mirror(Vec3 v, int side) => new Vec3(v.X, v.Y, v.Z * side);

        /// <summary>Direction pointing outwards (to the given side), raised and swept back.</summary>
        private static Vec3 Out(int side, double raise, double sweepBack)
        {
            DMath.SinCos(raise, out double sr, out double cr);
            DMath.SinCos(sweepBack, out double ss, out double cs);
            return new Vec3(-ss * cr, sr, cs * cr * side).Normalized();
        }

        private static string N(WingSpec s, int side, string part) => $"{s.Name}.{(side < 0 ? "L" : "R")}.{part}";

        private static WingRig BuildMembrane(AnatomyBuilder b, WingSpec s, int side)
        {
            double arm = s.Span * 0.3, fore = s.Span * 0.36, hand = s.Span * 0.08;
            Vec3 root = Mirror(s.Root, side);
            Vec3 d1 = Out(side, s.Raise, s.SweepBack + 0.35);
            Vec3 elbow = root + d1 * arm;
            Vec3 d2 = Out(side, s.Raise * 0.6, s.SweepBack - 0.55);
            Vec3 wrist = elbow + d2 * fore;
            Vec3 d3 = Out(side, s.Raise * 0.3, s.SweepBack - 0.2);

            int gBone = b.Group(N(s, side, "bones"), 0.6, side, GroupFlags.NoFarShade, 0.35);
            int gMem = b.Group(N(s, side, "membrane"), 0.4, side, GroupFlags.NoFarShade | GroupFlags.NoContour, 0);
            int armB = b.BoneAlong(N(s, side, "arm"), s.AnchorBone, root, d1, Vec3.UnitY, BoneRole.WingArm, side, arm);
            int foreB = b.BoneAlong(N(s, side, "forearm"), armB, elbow, d2, Vec3.UnitY, BoneRole.WingArm, side, fore);
            int handB = b.BoneAlong(N(s, side, "hand"), foreB, wrist, d3, Vec3.UnitY, BoneRole.WingHand, side, hand);

            double r = Math.Max(0.6, s.BoneRadius);
            var pa = b.Segment(gBone, armB, r * 1.25, foreB, r, s.BoneMaterial, PatternDomain.Limb, 0, 0.5, side);
            pa.DomainLength = s.Span; pa.Tag = N(s, side, "arm");
            var pf = b.Segment(gBone, foreB, r, handB, r * 0.8, s.BoneMaterial, PatternDomain.Limb, 0.5, 0.9, side);
            pf.DomainLength = s.Span; pf.Tag = N(s, side, "forearm");

            // fingers radiate from the wrist, from the leading edge backwards
            int n = Math.Max(2, s.Fingers);
            var fingers = new int[n];
            var tips = new Vec3[n];
            for (int k = 0; k < n; k++)
            {
                double t = k / (double)(n - 1);
                double back = DMath.Lerp(-0.25, 1.35, t) + s.SweepBack;
                double len = s.Span * DMath.Lerp(0.55, 0.36, t);
                Vec3 dir = Out(side, DMath.Lerp(s.Raise * 0.2, -0.05, t), back);
                int fb = b.BoneAlong(N(s, side, "finger" + k), handB, wrist, dir, Vec3.UnitY, BoneRole.WingFinger, side, len);
                fingers[k] = fb;
                tips[k] = wrist + dir * len;
                var fp = b.Cone(gBone, fb, Vec3.Zero, r * 0.75, fb, new Vec3(len, 0, 0), 0.4, s.BoneMaterial, PatternDomain.None, 0, 1, side);
                fp.Flags |= PrimFlags.NoPattern | PrimFlags.NoShadow;
                fp.Tag = N(s, side, "finger");
            }

            // membrane panels (two sided, lighter where the light shines through)
            PrimitiveDef Tri(int ba, Vec3 wa, int bb, Vec3 wb, int bc, Vec3 wc)
            {
                var t = b.Triangle(gMem, ba, b.RestWorld(ba).InversePoint(wa), bb, b.RestWorld(bb).InversePoint(wb), bc, b.RestWorld(bc).InversePoint(wc),
                    s.MembraneMaterial, PatternDomain.Wing, side);
                t.Flags |= PrimFlags.Translucent | PrimFlags.TwoSided | PrimFlags.NoShadow;
                t.DomainLength = s.Span;
                t.Tag = N(s, side, "membrane");
                return t;
            }
            for (int k = 0; k + 1 < n; k++)
            {
                Vec3 mid = Vec3.Lerp(tips[k], tips[k + 1], 0.5);
                Vec3 scal = Vec3.Lerp(mid, wrist, s.Scallop);
                Tri(handB, wrist, fingers[k], tips[k], fingers[k], scal);
                Tri(handB, wrist, fingers[k], scal, fingers[k + 1], tips[k + 1]);
            }
            // arm membrane: wrist, last finger tip, elbow and the body attachment
            int bodyBone = s.BodyBone >= 0 ? s.BodyBone : s.AnchorBone;
            Vec3 attach = Mirror(s.BodyAttach, side);
            Vec3 lastTip = tips[n - 1];
            Vec3 edgeMid = Vec3.Lerp(Vec3.Lerp(lastTip, attach, 0.5), elbow, s.Scallop * 1.2);
            Tri(handB, wrist, fingers[n - 1], lastTip, foreB, edgeMid);
            Tri(handB, wrist, foreB, edgeMid, foreB, elbow);
            Tri(foreB, elbow, foreB, edgeMid, bodyBone, attach);
            Tri(armB, root, foreB, elbow, bodyBone, attach);
            // leading edge membrane between shoulder, elbow and wrist
            Tri(armB, root, foreB, elbow, handB, wrist);

            if (s.ThumbClaw && s.ClawSize > 0.3)
            {
                Vec3 dir = Out(side, 0.3, -0.9);
                var c = b.Cone(gBone, handB, Vec3.Zero, 0.6 * s.ClawSize, handB, b.RestWorld(handB).InverseDir(dir) * (1.6 * s.ClawSize), 0.3, MaterialSlot.Accent,
                    PatternDomain.None, 0, 1, side);
                c.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.NoShadow;
                c.MinQuality = 1;
            }
            var rig = new WingRig
            {
                Side = side, Kind = WingKind.Membrane, ArmBone = armB, ForearmBone = foreB, HandBone = handB, FingerBones = fingers,
                Span = s.Span, ArmLength = arm, ForearmLength = fore, HandLength = hand,
                FlapUp = 1.1, FlapDown = 0.8, FoldDrop = 0.55, FoldSweep = 1.15, FoldElbow = 2.55, FoldWrist = 2.75, ElbowSign = 1,
            };
            // folded: the wrist (thumb claw) stands high above the shoulder, the fingers run back along
            // the flank and the membrane drapes between them (dragons, bats at rest, imps)
            BodyAxes(b, s, out Vec3 axisBack, out Vec3 belly);
            Vec3 outV = new Vec3(0, 0, side), up = -belly;
            Vec3 trailDir = (belly + axisBack * 0.45).Normalized();
            Vec3 trail = side > 0 ? trailDir : -trailDir;
            var armF = FoldBasis(axisBack * 0.9 + up * 0.38 + outV * 0.24, trail);
            var foreF = FoldBasis(-axisBack * 0.85 + up * 0.5 + outV * 0.1, trail);
            var handF = FoldBasis(axisBack * 0.9 - up * 0.35 + outV * 0.12, trail);
            SetFoldTargets(b, rig, s.AnchorBone, armF, foreF, handF);
            return rig;
        }

        /// <summary>Body axis from the wing anchor towards the hips and the belly direction perpendicular to it.</summary>
        private static void BodyAxes(AnatomyBuilder b, WingSpec s, out Vec3 axisBack, out Vec3 belly)
        {
            axisBack = (b.RestWorld(s.BodyBone >= 0 ? s.BodyBone : s.AnchorBone).Origin - b.RestWorld(s.AnchorBone).Origin).NormalizedOr(new Vec3(-1, 0, 0));
            if (axisBack.X > -0.2 && axisBack.Y > -0.5) axisBack = new Vec3(-1, axisBack.Y, 0).NormalizedOr(new Vec3(-1, 0, 0));
            belly = (-Vec3.Cross(axisBack, new Vec3(0, 0, 1))).NormalizedOr(new Vec3(0, -1, 0));
        }

        private static WingRig BuildFeathered(AnatomyBuilder b, WingSpec s, int side)
        {
            double arm = s.Span * 0.28, fore = s.Span * 0.3, hand = s.Span * 0.42;
            Vec3 root = Mirror(s.Root, side);
            Vec3 d1 = Out(side, s.Raise, s.SweepBack + 0.25);
            Vec3 elbow = root + d1 * arm;
            Vec3 d2 = Out(side, s.Raise * 0.6, s.SweepBack - 0.35);
            Vec3 wrist = elbow + d2 * fore;
            Vec3 d3 = Out(side, s.Raise * 0.3, s.SweepBack + 0.15);
            Vec3 tip = wrist + d3 * hand;
            Vec3 back = Out(side, 0, DMath.HalfPi + s.SweepBack * 0.3); // points backwards (and slightly out)

            int gWing = b.Group(N(s, side, "wing"), 0.9, side, GroupFlags.NoFarShade, 0.1);
            int armB = b.BoneAlong(N(s, side, "arm"), s.AnchorBone, root, d1, Vec3.UnitY, BoneRole.WingArm, side, arm);
            int foreB = b.BoneAlong(N(s, side, "forearm"), armB, elbow, d2, Vec3.UnitY, BoneRole.WingArm, side, fore);
            int handB = b.BoneAlong(N(s, side, "hand"), foreB, wrist, d3, Vec3.UnitY, BoneRole.WingHand, side, hand);

            double chord = s.Chord;
            Vec3 rootT = root + back * chord * 0.9;
            Vec3 elbowT = elbow + back * chord;
            Vec3 wristT = wrist + back * chord * 0.95;

            PrimitiveDef Tri(int ba, Vec3 wa, int bb, Vec3 wb, int bc, Vec3 wc, MaterialSlot mat, double u0, double u1)
            {
                var t = b.Triangle(gWing, ba, b.RestWorld(ba).InversePoint(wa), bb, b.RestWorld(bb).InversePoint(wb), bc, b.RestWorld(bc).InversePoint(wc),
                    mat, PatternDomain.Wing, side);
                t.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow;
                t.U0 = u0; t.U1 = u1;
                t.DomainLength = s.Span;
                t.Tag = N(s, side, "feathers");
                return t;
            }
            // covert and secondary feathers (arm and forearm)
            Tri(armB, root, foreB, elbow, foreB, elbowT, MaterialSlot.Primary, 0, 0.3);
            Tri(armB, root, foreB, elbowT, armB, rootT, MaterialSlot.Primary, 0, 0.3);
            Tri(foreB, elbow, handB, wrist, handB, wristT, MaterialSlot.Primary, 0.3, 0.6);
            Tri(foreB, elbow, handB, wristT, foreB, elbowT, MaterialSlot.Primary, 0.3, 0.6);
            // primary feathers: a fan from the wrist to the tip with separated feather ends
            int np = Math.Max(2, s.Primaries);
            Vec3 prev = tip;
            for (int k = 1; k <= np; k++)
            {
                double t = k / (double)np;
                Vec3 edge = Vec3.Lerp(tip, wristT, t);
                // feather ends stick out a little beyond the straight edge
                Vec3 bulge = (edge - wrist) * (0.12 * DMath.Sin(DMath.Pi * t));
                Vec3 end = edge + bulge;
                Tri(handB, wrist, handB, prev, handB, end, k == 1 ? s.TipMaterial : MaterialSlot.Primary, 0.6, 1.0);
                prev = end;
            }
            // leading edge volume: slim rounded coverts over arm and forearm
            double r = Math.Max(0.7, s.BoneRadius);
            var c1 = b.Segment(gWing, armB, r * 1.5, foreB, r * 1.2, MaterialSlot.Primary, PatternDomain.Wing, 0, 0.3, side);
            c1.DomainLength = s.Span; c1.Flags |= PrimFlags.NoShadow; c1.Tag = N(s, side, "coverts");
            var c2 = b.Segment(gWing, foreB, r * 1.2, handB, r * 0.9, MaterialSlot.Primary, PatternDomain.Wing, 0.3, 0.6, side);
            c2.DomainLength = s.Span; c2.Flags |= PrimFlags.NoShadow; c2.Tag = N(s, side, "coverts");
            var rig = new WingRig
            {
                Side = side, Kind = WingKind.Feathered, ArmBone = armB, ForearmBone = foreB, HandBone = handB,
                Span = s.Span, ArmLength = arm, ForearmLength = fore, HandLength = hand,
                FlapUp = 1.0, FlapDown = 0.75, FoldDrop = 0.3, FoldSweep = 1.3, FoldElbow = 2.65, FoldWrist = 2.95, ElbowSign = 1,
            };
            // folded: a Z along the flank - arm back and down, forearm forward, hand back over the rump;
            // the feathers hang towards the belly so the wing lies flat against the side
            BodyAxes(b, s, out Vec3 axisBack, out Vec3 belly);
            Vec3 outV = new Vec3(0, 0, side);
            // feathers slant back towards the tail (overlapping like roof tiles) instead of hanging straight down
            Vec3 trailDir = (belly * 0.55 + axisBack * 0.85).Normalized();
            Vec3 trail = side > 0 ? trailDir : -trailDir;
            var armF = FoldBasis(axisBack * 0.9 + belly * 0.3 + outV * 0.2, trail);
            var foreF = FoldBasis(-axisBack - belly * 0.12 + outV * 0.08, trail);
            var handF = FoldBasis(axisBack + belly * 0.05 + outV * 0.03, trail);
            SetFoldTargets(b, rig, s.AnchorBone, armF, foreF, handF);
            return rig;
        }

        /// <summary>Right-handed basis with X along x and Z as close as possible to z.</summary>
        private static Mat3 FoldBasis(Vec3 x, Vec3 z)
        {
            Vec3 bx = x.NormalizedOr(Vec3.UnitX);
            Vec3 bz = (z - bx * Vec3.Dot(bx, z)).NormalizedOr(Vec3.UnitY);
            return new Mat3(bx, Vec3.Cross(bz, bx), bz);
        }

        /// <summary>Local post-rotations taking arm, forearm and hand from the spread rest pose into the folded bases.</summary>
        private static void SetFoldTargets(AnatomyBuilder b, WingRig rig, int anchor, Mat3 armF, Mat3 foreF, Mat3 handF)
        {
            var parent = b.RestWorld(anchor).Basis;
            var armR = b.RestWorld(rig.ArmBone).Basis;
            var foreR = b.RestWorld(rig.ForearmBone).Basis;
            var handR = b.RestWorld(rig.HandBone).Basis;
            // corrections on top of the angle based fold (see RigDriver.ApplyWing): fix = old^-1 * target
            double s = rig.Side < 0 ? -1 : 1;
            var oldArm = Quat.RotY(-s * rig.FoldSweep) * Quat.RotZ(-rig.FoldDrop);
            var oldFore = Quat.RotY(s * rig.ElbowSign * rig.FoldElbow);
            var oldHand = Quat.RotY(-s * rig.ElbowSign * rig.FoldWrist);
            rig.ArmFix = Positive((oldArm.Conjugate * Delta(parent, armR, parent, armF)).Normalized());
            rig.ForeFix = Positive((oldFore.Conjugate * Delta(armR, foreR, armF, foreF)).Normalized());
            rig.HandFix = Positive((oldHand.Conjugate * Delta(foreR, handR, foreF, handF)).Normalized());
            rig.UseFoldTargets = true;
        }

        private static Quat Positive(Quat q) => q.W < 0 ? new Quat(-q.X, -q.Y, -q.Z, -q.W) : q;

        // rest local rotation r = P^T R, folded local f = P'^T F; post-rotation d with r d = f
        private static Quat Delta(Mat3 parentRest, Mat3 rest, Mat3 parentFold, Mat3 fold)
        {
            var r = AnatomyBuilder.QuatFromBasis(parentRest.Transposed * rest);
            var f = AnatomyBuilder.QuatFromBasis(parentFold.Transposed * fold);
            var d = (r.Conjugate * f).Normalized();
            return d.W < 0 ? new Quat(-d.X, -d.Y, -d.Z, -d.W) : d;
        }

        private static WingRig BuildInsect(AnatomyBuilder b, WingSpec s, int side)
        {
            Vec3 root = Mirror(s.Root, side);
            Vec3 d = Out(side, s.Raise, s.SweepBack + s.Pair * 0.45);
            double len = s.Span;
            int g = b.Group(N(s, side, "wing" + s.Pair), 0.3, side, GroupFlags.NoFarShade | GroupFlags.NoContour, s.Pair == 0 ? 0.2 : 0);
            int wb = b.BoneAlong(N(s, side, "wing" + s.Pair), s.AnchorBone, root, d, Vec3.UnitY, BoneRole.WingArm, side, len);
            var e = b.Ellipsoid(g, wb, new Vec3(len * 0.52, 0, 0), new Vec3(len * 0.52, 0.45, s.Chord * 0.5), s.MembraneMaterial, PatternDomain.Wing, 0, 1, null, side);
            e.Flags |= PrimFlags.Translucent | PrimFlags.NoShadow;
            e.DomainLength = len;
            e.Tag = N(s, side, "insectwing");
            // leading edge vein
            var v = b.Cone(g, wb, new Vec3(0, 0.1, -s.Chord * 0.2), 0.45, wb, new Vec3(len * 0.95, 0.1, -s.Chord * 0.12), 0.3, s.BoneMaterial, PatternDomain.None, 0, 1, side);
            v.Flags |= PrimFlags.Thin | PrimFlags.NoPattern | PrimFlags.NoShadow | PrimFlags.Hard;
            v.MinQuality = 1;
            return new WingRig
            {
                Side = side, Kind = WingKind.Insect, ArmBone = wb, Pair = s.Pair, Span = len, ArmLength = len,
                FlapUp = 0.95, FlapDown = 0.7, FoldDrop = 0.12, FoldSweep = 1.3 - s.Pair * 0.15, FoldElbow = 0, FoldWrist = 0,
            };
        }
    }
}
