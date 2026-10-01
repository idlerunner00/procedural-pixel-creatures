// Procedural Pixel Creatures - tail / chain shape blocks (tails, necks of long creatures, tentacles, antennae).

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public enum TailTip : byte
    {
        Plain,
        Tuft,
        Fluffy,
        Club,
        Spade,
        Stinger,
        Fin,
        Fan,
        Rattle,
        Leaf,
    }

    public sealed class ChainSpec
    {
        public string Name = "tail";
        public int ParentBone;
        public Vec3 Base;
        /// <summary>Initial direction in creature space.</summary>
        public Vec3 Direction = new Vec3(-1, 0, 0);
        public double Length = 16;
        public int Segments = 6;
        public double RadiusBase = 2.5;
        public double RadiusTip = 0.6;
        /// <summary>Profile exponent (1 linear taper, &lt;1 keeps thickness longer).</summary>
        public double TaperPower = 1.0;
        /// <summary>Extra thickness in the middle (fluffy tails).</summary>
        public double Bulge;
        /// <summary>Total pitch bend in radians along the chain (positive = curls upwards relative to the direction).</summary>
        public double Curl;
        /// <summary>Total yaw bend along the chain.</summary>
        public double Sweep;
        public TailTip Tip;
        public double TipSize = 1;
        public MaterialSlot Material = MaterialSlot.Primary;
        public MaterialSlot TipMaterial = MaterialSlot.Accent;
        public int Group = -1;
        public double BlendRadius = 1.5;
        public int Side;
        public PatternDomain Domain = PatternDomain.Tail;
        public BoneRole Role = BoneRole.Tail;
        public double Stiffness = 0.5;
        public double Droop = 0.5;
        public PrimFlags Flags;
        /// <summary>Flatten the chain cross-section (fins, leaves): scales radius in Z.</summary>
        public bool Thin;
    }

    public static class Chains
    {
        /// <summary>Builds a bone chain with round-cone segments. Returns the chain rig (bones base..tip).</summary>
        public static ChainRig Build(AnatomyBuilder b, ChainSpec s, List<PrimitiveDef>? created = null)
        {
            int group = s.Group >= 0 ? s.Group : b.Group(s.Name, s.BlendRadius, s.Side);
            int n = Math.Max(1, s.Segments);
            double segLen = s.Length / n;
            var bones = new int[n + 1];
            var lengths = new double[n];
            var radii = new double[n + 1];
            var restPitch = new double[n];
            var restYaw = new double[n];
            Vec3 dir = s.Direction.NormalizedOr(Vec3.UnitX * -1);
            Vec3 pos = s.Base;
            int parent = s.ParentBone;
            // the curl axis is fixed at the base and only follows the sweep (yaw); recomputing it from
            // the current direction would flip the curl when the chain passes the vertical
            Vec3 curlAxis = Vec3.Cross(dir, Vec3.UnitY);
            if (curlAxis.LengthSquared < 1e-6) curlAxis = Vec3.UnitZ * -1;
            curlAxis = curlAxis.Normalized();
            // the chain turns in its own plane: pitch about the axis perpendicular to dir and up
            for (int i = 0; i <= n; i++)
            {
                double t = i / (double)n;
                double r = DMath.Lerp(s.RadiusBase, s.RadiusTip, DMath.Pow(t, s.TaperPower));
                if (s.Bulge > 0) r += s.Bulge * DMath.Sin(DMath.Pi * Math.Min(1.0, t * 1.15)) * s.RadiusBase;
                radii[i] = r;
                Vec3 up = Math.Abs(dir.Y) > 0.95 ? Vec3.UnitX : Vec3.UnitY;
                int bone = b.BoneAlong($"{s.Name}.{i}", parent, pos, dir, up, s.Role, s.Side, i < n ? segLen : 0);
                bones[i] = bone;
                parent = bone;
                if (i < n)
                {
                    lengths[i] = segLen;
                    double dp = s.Curl / n, dy = s.Sweep / n;
                    restPitch[i] = dp;
                    restYaw[i] = dy;
                    // bend the direction for the next segment
                    Vec3 nd = RotateAround(dir, curlAxis, dp);
                    nd = RotateAround(nd, Vec3.UnitY, dy);
                    curlAxis = RotateAround(curlAxis, Vec3.UnitY, dy);
                    pos = pos + dir * segLen;
                    dir = nd.Normalized();
                }
            }
            double total = s.Length;
            for (int i = 0; i < n; i++)
            {
                var prim = b.Segment(group, bones[i], radii[i], bones[i + 1], radii[i + 1], s.Material, s.Domain, i / (double)n, (i + 1) / (double)n, s.Side);
                prim.DomainLength = total;
                prim.Flags |= s.Flags;
                prim.Tag = $"{s.Name}.seg{i}";
                created?.Add(prim);
            }
            BuildTip(b, s, group, bones[n], bones[n - 1], radii[n], total);
            return new ChainRig
            {
                Name = s.Name,
                Bones = bones,
                Lengths = lengths,
                TotalLength = total,
                Side = s.Side,
                Radii = radii,
                Stiffness = s.Stiffness,
                RestPitch = restPitch,
                RestYaw = restYaw,
                Droop = s.Droop,
            };
        }

        public static Vec3 RotateAround(Vec3 v, Vec3 axis, double angle)
        {
            if (Math.Abs(angle) < 1e-12) return v;
            return Quat.AxisAngle(axis, angle).Rotate(v);
        }

        private static void BuildTip(AnatomyBuilder b, ChainSpec s, int group, int tipBone, int prevBone, double tipRadius, double total)
        {
            double k = s.TipSize;
            switch (s.Tip)
            {
                case TailTip.Tuft:
                {
                    var p = b.Ellipsoid(group, tipBone, new Vec3(-1.2 * k, 0, 0), new Vec3(2.6 * k, 1.5 * k, 1.4 * k), s.Material, s.Domain, 0.92, 1.0);
                    p.DomainLength = total;
                    p.Tag = s.Name + ".tuft";
                    break;
                }
                case TailTip.Club:
                {
                    var p = b.Ellipsoid(group, tipBone, new Vec3(-0.5 * k, 0, 0), new Vec3(2.4 * k, 2.0 * k, 2.0 * k), MaterialSlot.Accent, PatternDomain.None);
                    p.Tag = s.Name + ".club";
                    for (int i = 0; i < 3; i++)
                    {
                        double a = (i - 1) * 1.1;
                        Vec3 d = new Vec3(0, DMath.Cos(a), DMath.Sin(a));
                        var sp = b.Cone(group, tipBone, d * 1.4 * k, 0.9 * k, tipBone, d * 3.2 * k + new Vec3(-0.3 * k, 0, 0), 0.3, MaterialSlot.Accent);
                        sp.Flags |= PrimFlags.Hard | PrimFlags.NoPattern;
                    }
                    break;
                }
                case TailTip.Spade:
                {
                    int g2 = b.Group(s.Name + ".spade", 0.8, s.Side);
                    Vec3 c = new Vec3(1.6 * k, 0, 0);
                    b.Triangle(g2, tipBone, new Vec3(-0.6 * k, 0, 0), tipBone, c + new Vec3(0.8 * k, 1.9 * k, 0), tipBone, c + new Vec3(2.8 * k, 0, 0), MaterialSlot.Accent);
                    b.Triangle(g2, tipBone, new Vec3(-0.6 * k, 0, 0), tipBone, c + new Vec3(0.8 * k, -1.9 * k, 0), tipBone, c + new Vec3(2.8 * k, 0, 0), MaterialSlot.Accent);
                    break;
                }
                case TailTip.Stinger:
                {
                    var bulb = b.Ellipsoid(group, tipBone, new Vec3(0.6 * k, 0, 0), new Vec3(1.9 * k, 1.5 * k, 1.4 * k), s.Material, s.Domain, 0.95, 1.0);
                    bulb.DomainLength = total;
                    var sting = b.Cone(group, tipBone, new Vec3(1.8 * k, -0.3 * k, 0), 0.7 * k, tipBone, new Vec3(3.2 * k, -2.0 * k, 0), 0.3, MaterialSlot.Accent);
                    sting.Flags |= PrimFlags.Hard | PrimFlags.NoPattern;
                    sting.Tag = s.Name + ".stinger";
                    break;
                }
                case TailTip.Fin:
                case TailTip.Fan:
                {
                    int g2 = b.Group(s.Name + ".fin", 0.8, 0, GroupFlags.NoFarShade);
                    double h = 2.8 * k, l = 3.4 * k;
                    var slot = s.Tip == TailTip.Fan ? s.Material : MaterialSlot.Membrane;
                    var t1 = b.Triangle(g2, prevBone, new Vec3(0, 0, 0), tipBone, new Vec3(l, h, 0), tipBone, new Vec3(l * 0.55, 0, 0), slot, PatternDomain.Fin);
                    var t2 = b.Triangle(g2, prevBone, new Vec3(0, 0, 0), tipBone, new Vec3(l, -h, 0), tipBone, new Vec3(l * 0.55, 0, 0), slot, PatternDomain.Fin);
                    t1.Flags |= PrimFlags.Translucent; t2.Flags |= PrimFlags.Translucent;
                    t1.DomainLength = t2.DomainLength = l;
                    break;
                }
                case TailTip.Rattle:
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var r = b.Ellipsoid(group, tipBone, new Vec3(0.4 + i * 1.5 * k, 0, 0), new Vec3(0.9 * k, 1.2 * k * (1 - i * 0.15), 1.0 * k), MaterialSlot.Accent);
                        r.Flags |= PrimFlags.Hard | PrimFlags.NoPattern;
                    }
                    break;
                }
                case TailTip.Leaf:
                {
                    int g2 = b.Group(s.Name + ".leaf", 0.8, s.Side);
                    b.Triangle(g2, tipBone, new Vec3(0, 0, 0), tipBone, new Vec3(2.2 * k, 1.6 * k, 0), tipBone, new Vec3(4.5 * k, 0, 0), MaterialSlot.Secondary, PatternDomain.Fin);
                    b.Triangle(g2, tipBone, new Vec3(0, 0, 0), tipBone, new Vec3(2.2 * k, -1.6 * k, 0), tipBone, new Vec3(4.5 * k, 0, 0), MaterialSlot.Secondary, PatternDomain.Fin);
                    break;
                }
            }
        }
    }
}
