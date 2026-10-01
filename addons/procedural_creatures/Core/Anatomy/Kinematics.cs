// Procedural Pixel Creatures - inverse kinematics helpers shared by anatomy builders (rest pose)
// and motion modules (runtime). All solvers are analytic and deterministic.

using System;

namespace PixelCreatures.Core.Anatomy
{
    public static class Kinematics
    {
        /// <summary>
        /// Two bone IK. Returns the middle joint for a chain root -> joint -> end reaching 'target'.
        /// 'pole' is a direction hint for where the middle joint should bend. The effective end
        /// position (clamped to the reachable range) is returned in 'end'.
        /// </summary>
        public static Vec3 TwoBone(Vec3 root, Vec3 target, double l1, double l2, Vec3 pole, out Vec3 end, double minBend = 0.02)
        {
            Vec3 d = target - root;
            double dist = d.Length;
            Vec3 dir = dist > 1e-9 ? d / dist : new Vec3(0, -1, 0);
            double maxReach = (l1 + l2) * (1.0 - minBend * 0.02);
            double minReach = Math.Abs(l1 - l2) + 1e-3;
            double clamped = DMath.Clamp(dist, minReach, maxReach);
            end = root + dir * clamped;
            double a = (l1 * l1 - l2 * l2 + clamped * clamped) / (2 * clamped);
            double h = Math.Sqrt(Math.Max(0, l1 * l1 - a * a));
            Vec3 bend = pole - dir * Vec3.Dot(pole, dir);
            if (bend.LengthSquared < 1e-10)
            {
                bend = Vec3.Cross(dir, Math.Abs(dir.Y) < 0.9 ? Vec3.UnitY : Vec3.UnitX);
            }
            bend = bend.Normalized();
            return root + dir * a + bend * h;
        }

        /// <summary>
        /// Three bone leg IK for digitigrade/unguligrade legs: the distal segment keeps a preferred
        /// angle (metaDir, pointing from the foot to the ankle) and the remaining two bones are solved
        /// analytically. If the ankle is out of reach the distal segment straightens gradually.
        /// </summary>
        public static void ThreeBone(Vec3 root, Vec3 foot, double l1, double l2, double l3, Vec3 metaDir, Vec3 pole,
            out Vec3 knee, out Vec3 ankle, out Vec3 footOut)
        {
            Vec3 md = metaDir.NormalizedOr(Vec3.UnitY);
            ankle = foot + md * l3;
            double reach = (l1 + l2) * 0.995;
            double da = (ankle - root).Length;
            if (da > reach)
            {
                // rotate the distal segment towards the root until the ankle becomes reachable
                Vec3 toRoot = (root - foot).NormalizedOr(Vec3.UnitY);
                for (int i = 1; i <= 8; i++)
                {
                    double t = i / 8.0;
                    Vec3 m = Vec3.Lerp(md, toRoot, t).NormalizedOr(toRoot);
                    Vec3 a = foot + m * l3;
                    if ((a - root).Length <= reach || i == 8)
                    {
                        ankle = a;
                        break;
                    }
                }
            }
            knee = TwoBone(root, ankle, l1, l2, pole, out Vec3 ankleReached);
            footOut = foot + (ankleReached - ankle);
            ankle = ankleReached;
        }

        /// <summary>Basis whose X axis points from a to b; Y axis leans towards 'up'.</summary>
        public static Mat3 SegmentBasis(Vec3 a, Vec3 b, Vec3 up)
        {
            Vec3 x = (b - a).NormalizedOr(Vec3.UnitX);
            return Mat3.LookAlong(x, up);
        }

        /// <summary>Distance from a point to a segment.</summary>
        public static double SegmentDistance(Vec3 p, Vec3 a, Vec3 b)
        {
            Vec3 ab = b - a;
            double t = DMath.Saturate(Vec3.Dot(p - a, ab) / Math.Max(1e-12, ab.LengthSquared));
            return (a + ab * t - p).Length;
        }
    }
}
