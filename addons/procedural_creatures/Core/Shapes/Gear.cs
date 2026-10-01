// Procedural Pixel Creatures - gear block: belts, loincloths, tunics, robes, armour, helmets, hoods,
// crowns, beards, shields and pauldrons. Every piece is its own group drawn a little in front of the
// body (depth bias) and fitted to the primitives it covers, so it sits cleanly on any build and in
// every pose. Hanging cloth and beards are short spring chains: they lag behind, swing and settle.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    /// <summary>
    /// Torso that clothing is fitted to. All torso bones share one orientation (+X along the spine,
    /// +Y backwards, +Z right), the shell ellipsoids use identity rotations.
    /// </summary>
    public sealed class GearTorso
    {
        public int Pelvis = -1, Chest = -1;
        public readonly List<PrimitiveDef> Shell = new List<PrimitiveDef>();
        public double BlendRadius = 2, HipRadius = 4, ChestRadius = 5;
        /// <summary>Height of the hip joints above the ground (hem lengths).</summary>
        public double HipJointHeight = 10;
    }

    public enum HeadGearKind : byte
    {
        None,
        Cap,
        Helmet,
        Horned,
        Hood,
        Crown,
    }

    public static class Gear
    {
        /// <summary>Depth bias of gear: wins against the surface it lies on, not against limbs in front of it.</summary>
        public const double Bias = 0.55;

        // ------------------------------------------------------------------ torso

        /// <summary>Front and back (local Y) and half width (local Z) of the torso shell at pelvis height x.</summary>
        public static bool Section(AnatomyBuilder b, GearTorso t, double x, out double front, out double back, out double half)
        {
            var pel = b.RestWorld(t.Pelvis);
            front = double.MaxValue; back = double.MinValue; half = 0;
            bool any = false;
            foreach (var p in t.Shell)
            {
                if (p.Kind != PrimKind.Ellipsoid) continue;
                Vec3 c = pel.InversePoint(b.RestWorld(p.BoneA).Point(p.LocalA));
                double u = (x - c.X) / p.Radii.X;
                if (Math.Abs(u) >= 1) continue;
                double s = Math.Sqrt(1 - u * u);
                front = Math.Min(front, c.Y - p.Radii.Y * s);
                back = Math.Max(back, c.Y + p.Radii.Y * s);
                half = Math.Max(half, Math.Abs(c.Z) + p.Radii.Z * s);
                any = true;
            }
            return any;
        }

        /// <summary>Pelvis-local height of the waist line (top of the hips).</summary>
        public static double Waist(GearTorso t) => t.HipRadius * 0.55;

        /// <summary>Clothing that follows the body: grown copies of selected shell ellipsoids in one group.</summary>
        public static int Cover(AnatomyBuilder b, GearTorso t, string name, MaterialSlot mat, double grow, double pad, Func<PrimitiveDef, bool> which,
            double bias = Bias, int shadeBias = 0)
        {
            int g = b.Group(name, t.BlendRadius, 0, GroupFlags.None, bias);
            foreach (var p in t.Shell)
            {
                if (!which(p)) continue;
                var c = b.Ellipsoid(g, p.BoneA, p.LocalA, p.Radii * grow + new Vec3(pad, pad, pad), mat, PatternDomain.None, 0, 1, p.LocalRotation);
                c.Flags |= PrimFlags.NoPattern;
                c.ShadeBias = shadeBias;
                c.Tag = name;
            }
            return g;
        }

        /// <summary>Belt ring at pelvis height x; returns the pelvis-local centre of its front.</summary>
        public static Vec3 Belt(AnatomyBuilder b, GearTorso t, double x, double width, MaterialSlot mat, int shadeBias, bool buckle, double grow = 1.05)
        {
            if (!Section(b, t, x, out double f, out double bk, out double half)) return new Vec3(x, -t.HipRadius, 0);
            double pad = Math.Max(0.6, t.BlendRadius * 0.22);
            int g = b.Group("gear.belt", 0.5, 0, GroupFlags.None, Bias + 0.2);
            double ry = (bk - f) * 0.5 * grow + pad, rz = half * grow + pad, cy = (f + bk) * 0.5;
            var p = b.Ellipsoid(g, t.Pelvis, new Vec3(x, cy, 0), new Vec3(Math.Max(0.5, width * 0.5), ry, rz), mat);
            p.Flags |= PrimFlags.NoPattern;
            p.ShadeBias = shadeBias;
            p.Tag = "gear.belt";
            if (buckle)
            {
                var bu = b.Ellipsoid(g, t.Pelvis, new Vec3(x, cy - ry, 0), new Vec3(Math.Max(0.6, width * 0.62), 0.6, Math.Max(0.7, width * 0.6)), MaterialSlot.Metal);
                bu.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                bu.Tag = "gear.buckle";
            }
            return new Vec3(x, cy - ry, 0);
        }

        /// <summary>Small pouch hanging from the belt at the front of one hip.</summary>
        public static void Pouch(AnatomyBuilder b, GearTorso t, double x, double size, int side, MaterialSlot mat)
        {
            if (!Section(b, t, x, out double f, out double bk, out double half)) return;
            double cy = (f + bk) * 0.5;
            double a = 0.85;
            Vec3 at = new Vec3(x - size * 0.9, cy + (f - cy) * Math.Cos(a) * 1.08, side * half * Math.Sin(a) * 1.08);
            int g = b.Group("gear.pouch", 0.5, side, GroupFlags.None, Bias + 0.3);
            var p = b.Ellipsoid(g, t.Pelvis, at, new Vec3(size, size * 0.7, size * 0.85), mat);
            p.Flags |= PrimFlags.NoPattern;
            p.ShadeBias = -1;
            p.Tag = "gear.pouch";
        }

        /// <summary>
        /// Hanging cloth panel: a two bone spring chain from a pelvis-local point straight down, drawn as
        /// two flat ellipsoids (straight sides). The panel's thin axis faces 'normal' (creature space).
        /// </summary>
        public static int Panel(AnatomyBuilder b, GearTorso t, string name, Vec3 topLocal, Vec3 normal, double length, double width, double thick,
            MaterialSlot mat, int group = -1, double stiffness = 0.35, int shadeBias = 0)
        {
            var pel = b.RestWorld(t.Pelvis);
            Vec3 top = pel.Point(topLocal);
            Vec3 down = new Vec3(0, -1, 0);
            int side = normal.Z > 0.5 ? 1 : normal.Z < -0.5 ? -1 : 0;
            int root = b.BoneAlong(name, t.Pelvis, top, down, normal, BoneRole.Other, side, length);
            int tip = b.BoneAlong(name + ".tip", root, top + down * length, down, normal, BoneRole.Other, side, 0);
            int g = group >= 0 ? group : b.Group(name, 0.8, side, GroupFlags.None, Bias);
            double seg = length * 0.5;
            // upper half on the root bone, lower half spans to the tip bone so the panel bends with the chain
            var a = b.Ellipsoid(g, root, new Vec3(seg * 0.45, 0, 0), new Vec3(seg * 0.62, thick, width * 0.5), mat);
            a.Flags |= PrimFlags.NoPattern;
            a.ShadeBias = shadeBias;
            a.Tag = name;
            var c = b.Ellipsoid(g, tip, new Vec3(-seg * 0.55, 0, 0), new Vec3(seg * 0.62, thick, width * 0.47), mat);
            c.Flags |= PrimFlags.NoPattern;
            c.ShadeBias = shadeBias;
            c.Tag = name;
            b.Rig.Appendages.Add(new ChainRig
            {
                Name = name,
                Bones = new[] { root, tip },
                Lengths = new[] { length },
                TotalLength = length,
                Side = side,
                Radii = new[] { thick, Math.Max(0.6, thick) },
                Stiffness = stiffness,
                RestPitch = new[] { 0.0 },
                RestYaw = new[] { 0.0 },
                Droop = 0.55,
            });
            return g;
        }

        /// <summary>Loincloth flaps (front and back) under a belt at pelvis height x.</summary>
        public static void Loincloth(AnatomyBuilder b, GearTorso t, double x, double length, MaterialSlot mat)
        {
            if (!Section(b, t, x, out double f, out double bk, out double half)) return;
            double w = Math.Max(2.0, half * 0.95), th = Math.Max(0.55, t.HipRadius * 0.12);
            Panel(b, t, "gear.flapF", new Vec3(x + 0.4, f - th * 0.6, 0), new Vec3(1, 0, 0), length, w, th, mat);
            Panel(b, t, "gear.flapB", new Vec3(x + 0.4, bk + th * 0.6, 0), new Vec3(-1, 0, 0), length * 0.85, w * 0.95, th, mat, -1, 0.35, -1);
        }

        /// <summary>Skirt of four hanging panels around the waist (tunics, robes); merges into one soft shape.</summary>
        public static void Skirt(AnatomyBuilder b, GearTorso t, double x, double length, MaterialSlot mat, double flare = 1.0)
        {
            if (!Section(b, t, x, out double f, out double bk, out double half)) return;
            double th = Math.Max(0.6, t.HipRadius * 0.16);
            double cy = (f + bk) * 0.5, depth = (bk - f) * 0.5;
            int g = b.Group("gear.skirt", Math.Max(1.2, t.BlendRadius * 0.8), 0, GroupFlags.None, Bias);
            Panel(b, t, "gear.skirtF", new Vec3(x, f - th * 0.4, 0), new Vec3(1, 0, 0), length, half * 1.7 * flare, th, mat, g, 0.4);
            Panel(b, t, "gear.skirtB", new Vec3(x, bk + th * 0.4, 0), new Vec3(-1, 0, 0), length, half * 1.7 * flare, th, mat, g, 0.4);
            for (int side = -1; side <= 1; side += 2)
                Panel(b, t, side < 0 ? "gear.skirtL" : "gear.skirtR", new Vec3(x, cy, side * (half + th * 0.4)), new Vec3(0, 0, side), length * 0.97,
                    depth * 1.8 * flare, th, mat, g, 0.4);
        }

        // ------------------------------------------------------------------ head

        /// <summary>Hat, helmet, hood or crown fitted to the skull (head frame: +X forward, +Y up, +Z right).</summary>
        public static void HeadGear(AnatomyBuilder b, HeadResult head, HeadGearKind kind, int neckBone, double size, MaterialSlot hornMaterial = MaterialSlot.Accent)
        {
            if (kind == HeadGearKind.None) return;
            Vec3 c = head.SkullCenterLocal, r = head.SkullRadii;
            double pad = 0.45;
            int g = b.Group("gear.head", 0.8, 0, GroupFlags.None, Bias);
            PrimitiveDef E(Vec3 at, Vec3 radii, MaterialSlot m, string tag)
            {
                var p = b.Ellipsoid(g, head.Head, at, radii, m);
                p.Flags |= PrimFlags.NoPattern;
                p.Tag = tag;
                return p;
            }
            switch (kind)
            {
                case HeadGearKind.Cap:
                    E(c + new Vec3(-r.X * 0.08, r.Y * 0.56, 0), new Vec3(r.X * 1.05 + pad, r.Y * 0.62 + pad, r.Z * 1.08 + pad), MaterialSlot.Cloth, "gear.cap");
                    break;
                case HeadGearKind.Helmet:
                case HeadGearKind.Horned:
                {
                    E(c + new Vec3(-r.X * 0.06, r.Y * 0.6, 0), new Vec3(r.X * 1.07 + pad, r.Y * 0.68 + pad, r.Z * 1.09 + pad), MaterialSlot.Metal, "gear.helmet");
                    int rg = b.Group("gear.helmetRim", 0.5, 0, GroupFlags.None, Bias + 0.25);
                    var rim = b.Ellipsoid(rg, head.Head, c + new Vec3(-r.X * 0.06, r.Y * 0.4, 0), new Vec3(r.X * 1.1 + pad + 0.3, Math.Max(0.55, r.Y * 0.1), r.Z * 1.12 + pad + 0.3), MaterialSlot.Metal);
                    rim.Flags |= PrimFlags.NoPattern;
                    rim.ShadeBias = -1;
                    rim.Tag = "gear.helmetRim";
                    if (kind == HeadGearKind.Helmet && r.X > 2.6)
                    {
                        // nose guard
                        var ng = b.Cone(rg, head.Head, c + new Vec3(r.X * 1.0 + pad, r.Y * 0.42, 0), 0.55, head.Head, c + new Vec3(r.X * 1.06 + pad, -r.Y * 0.08, 0), 0.5, MaterialSlot.Metal);
                        ng.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                        ng.Tag = "gear.noseGuard";
                    }
                    if (kind == HeadGearKind.Horned)
                    {
                        Horns.Build(b, new HornSpec
                        {
                            Kind = HornKind.Curved,
                            HeadBone = head.Head,
                            Base = c + new Vec3(-r.X * 0.05, r.Y * 0.62, r.Z * 0.88),
                            Size = Math.Max(2.2, r.X * 1.15 * size),
                            Thickness = 1.1,
                            Material = hornMaterial,
                            Name = "gear.horn",
                        });
                    }
                    break;
                }
                case HeadGearKind.Hood:
                {
                    // shifted back so the face stays free; a drape falls to the shoulders
                    E(c + new Vec3(-r.X * 0.3, r.Y * 0.16, 0), new Vec3(r.X * 1.08 + pad, r.Y * 1.16 + pad, r.Z * 1.2 + pad), MaterialSlot.Cloth, "gear.hood");
                    var tipP = b.Cone(g, head.Head, c + new Vec3(-r.X * 0.7, r.Y * 0.7, 0), Math.Max(0.8, r.Y * 0.45), head.Head,
                        c + new Vec3(-r.X * 1.55, r.Y * 0.95, 0), 0.45, MaterialSlot.Cloth);
                    tipP.Flags |= PrimFlags.NoPattern;
                    tipP.Tag = "gear.hoodTip";
                    if (neckBone >= 0)
                    {
                        var hx = b.RestWorld(head.Head);
                        Vec3 back = hx.Point(c + new Vec3(-r.X * 0.55, -r.Y * 0.35, 0));
                        var nx = b.RestWorld(neckBone);
                        var drape = b.Cone(g, head.Head, c + new Vec3(-r.X * 0.5, -r.Y * 0.25, 0), r.Z * 0.6 + pad, neckBone, nx.InversePoint(back + new Vec3(-0.3, -r.Y * 0.5, 0)),
                            r.Z * 0.62 + pad, MaterialSlot.Cloth);
                        drape.Flags |= PrimFlags.NoPattern;
                        drape.Tag = "gear.hoodDrape";
                    }
                    break;
                }
                case HeadGearKind.Crown:
                {
                    double ringY = r.Y * 0.6, ringS = Math.Sqrt(1 - 0.6 * 0.6);
                    var ring = E(c + new Vec3(-r.X * 0.04, ringY, 0), new Vec3(r.X * ringS + 0.75, Math.Max(0.6, r.Y * 0.16), r.Z * ringS + 0.75), MaterialSlot.Metal, "gear.crown");
                    int n = 5;
                    for (int i = 0; i < n; i++)
                    {
                        double a = (i + 0.5) / n * DMath.TwoPi;
                        Vec3 at = c + new Vec3(-r.X * 0.04 + Math.Cos(a) * (r.X * ringS + 0.4), ringY, Math.Sin(a) * (r.Z * ringS + 0.4));
                        var sp = b.Cone(g, head.Head, at, 0.6, head.Head, at + new Vec3(0, Math.Max(1.2, r.Y * 0.42 * size), 0), 0.35, MaterialSlot.Metal);
                        sp.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.Thin;
                        sp.Tag = "gear.crownSpike";
                    }
                    _ = ring;
                    break;
                }
            }
        }

        /// <summary>
        /// Beard (amount 0..1): a hair mass over chin and cheeks that moves with the jaw, a moustache under the
        /// nose and, for longer beards, a hanging part on a spring chain that swings with the head.
        /// </summary>
        public static void Beard(AnatomyBuilder b, HeadResult head, double amount)
        {
            if (amount <= 0.05) return;
            Vec3 c = head.SkullCenterLocal, r = head.SkullRadii;
            var hx = b.RestWorld(head.Head);
            int g = b.Group("gear.beard", 1.2, 0, GroupFlags.None, Bias + 0.15);
            int carrier = head.Jaw >= 0 ? head.Jaw : head.Head;
            var cx = b.RestWorld(carrier);
            // head-aligned axes on the jaw bone
            var rot = AnatomyBuilder.QuatFromBasis(cx.Basis.Transposed * hx.Basis);
            double full = DMath.Saturate(amount * 1.6);
            Vec3 massC = hx.Point(c + new Vec3(r.X * 0.4, -r.Y * DMath.Lerp(0.55, 0.7, full), 0));
            var mass = b.Ellipsoid(g, carrier, cx.InversePoint(massC), new Vec3(r.X * 0.7 + 0.5, r.Y * DMath.Lerp(0.38, 0.55, full) + 0.4, r.Z * DMath.Lerp(0.85, 1.0, full) + 0.5),
                MaterialSlot.Hair, PatternDomain.Head, 0.5, 0.9, rot);
            mass.Flags |= PrimFlags.NoPattern;
            mass.DomainLength = r.X * 4;
            mass.Tag = "gear.beard";
            if (amount > 0.3)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var mo = b.Ellipsoid(g, head.Head, c + new Vec3(r.X * 0.92, -r.Y * 0.22, side * r.Z * 0.34), new Vec3(0.75, 0.55, r.Z * 0.36 + 0.3),
                        MaterialSlot.Hair, PatternDomain.Head, 0.85, 0.95, null, side);
                    mo.Flags |= PrimFlags.NoPattern;
                    mo.DomainLength = r.X * 4;
                    mo.Tag = "gear.moustache";
                }
            }
            if (amount < 0.3) return;
            double length = r.Y * 2 * DMath.Lerp(0.2, 0.95, (amount - 0.3) / 0.7);
            Vec3 chin = hx.Point(c + new Vec3(r.X * 0.55, -r.Y * 0.95, 0));
            Vec3 dir = new Vec3(0.22, -1, 0).Normalized();
            int root = b.BoneAlong("gear.beard", carrier, chin, dir, new Vec3(1, 0, 0), BoneRole.Other, 0, length);
            int tip = b.BoneAlong("gear.beard.tip", root, chin + dir * length, dir, new Vec3(1, 0, 0), BoneRole.Other, 0, 0);
            double r0 = Math.Max(1.0, r.Z * 0.8);
            // spade shaped: full and rounded below the chin, long beards taper to a point
            var hang = b.Ellipsoid(g, root, new Vec3(length * 0.42, 0, 0), new Vec3(length * 0.55 + 0.4, Math.Max(0.9, r.Z * 0.42), r0),
                MaterialSlot.Hair, PatternDomain.Head, 0.9, 1.0);
            hang.Flags |= PrimFlags.NoPattern;
            hang.DomainLength = r.X * 4;
            hang.Tag = "gear.beard";
            if (amount > 0.7)
            {
                var point = b.Cone(g, root, new Vec3(length * 0.5, 0, 0), r0 * 0.75, tip, new Vec3(length * 0.12, 0, 0), Math.Max(0.5, r0 * 0.3),
                    MaterialSlot.Hair, PatternDomain.Head, 0.9, 1.0);
                point.Flags |= PrimFlags.NoPattern;
                point.DomainLength = r.X * 4;
                point.Tag = "gear.beard";
            }
            b.Rig.Appendages.Add(new ChainRig
            {
                Name = "gear.beard",
                Bones = new[] { root, tip },
                Lengths = new[] { length },
                TotalLength = length,
                Radii = new[] { r0, r0 * 0.4 },
                Stiffness = 0.55,
                RestPitch = new[] { 0.0 },
                RestYaw = new[] { 0.0 },
                Droop = 0.35,
            });
        }

        // ------------------------------------------------------------------ arms

        /// <summary>Round shield on the outside of a hand (wood/leather face, metal rim and boss).</summary>
        public static void Shield(AnatomyBuilder b, LegRig arm, double radius, double handR)
        {
            int hand = arm.JointBones[2];
            var hx = b.RestWorld(hand);
            double s = arm.Side == 0 ? -1 : arm.Side;
            Vec3 n = new Vec3(0.5, 0.05, s * 0.86).Normalized();
            Vec3 center = hx.Origin + n * (handR + 0.9) + new Vec3(0, handR * 0.4, 0);
            var basis = Mat3.LookAlong(n, Vec3.UnitY);
            var rot = AnatomyBuilder.QuatFromBasis(hx.Basis.Transposed * basis);
            int g = b.Group("gear.shield", 0.5, arm.Side, GroupFlags.None, 0.6);
            var rim = b.Ellipsoid(g, hand, hx.InversePoint(center), new Vec3(0.75, radius, radius), MaterialSlot.Metal, PatternDomain.None, 0, 1, rot, arm.Side);
            rim.Flags |= PrimFlags.NoPattern | PrimFlags.ShadowCaster;
            rim.ShadeBias = -1;
            rim.Tag = "gear.shieldRim";
            int fg = b.Group("gear.shieldFace", 0.5, arm.Side, GroupFlags.None, 0.9);
            var face = b.Ellipsoid(fg, hand, hx.InversePoint(center + n * 0.45), new Vec3(0.6, radius * 0.8, radius * 0.8), MaterialSlot.Cloth, PatternDomain.None, 0, 1, rot, arm.Side);
            face.Flags |= PrimFlags.NoPattern;
            face.Tag = "gear.shieldFace";
            var boss = b.Ellipsoid(fg, hand, hx.InversePoint(center + n * 0.9), new Vec3(Math.Max(0.7, radius * 0.22), Math.Max(0.8, radius * 0.26), Math.Max(0.8, radius * 0.26)),
                MaterialSlot.Metal, PatternDomain.None, 0, 1, rot, arm.Side);
            boss.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
            boss.Tag = "gear.shieldBoss";
        }

        /// <summary>Metal shoulder guard on the upper arm (moves with the arm swing).</summary>
        public static void Pauldron(AnatomyBuilder b, LegRig arm, double armTop, bool spike)
        {
            int upper = arm.RootBone;
            var ux = b.RestWorld(upper);
            double s = arm.Side == 0 ? 1 : arm.Side;
            Vec3 at = ux.Origin + new Vec3(-armTop * 0.05, armTop * 0.55, s * armTop * 0.35);
            int g = b.Group("gear.pauldron", 0.6, arm.Side, GroupFlags.None, Bias + 0.3);
            var basis = Mat3.LookAlong(new Vec3(0, 0, s), Vec3.UnitY);
            var rot = AnatomyBuilder.QuatFromBasis(ux.Basis.Transposed * basis);
            var p = b.Ellipsoid(g, upper, ux.InversePoint(at), new Vec3(armTop * 0.95 + 0.4, armTop * 0.6 + 0.35, armTop * 1.25 + 0.45), MaterialSlot.Metal, PatternDomain.None, 0, 1, rot, arm.Side);
            p.Flags |= PrimFlags.NoPattern;
            p.Tag = "gear.pauldron";
            if (spike)
            {
                Vec3 sa = at + new Vec3(0, armTop * 0.5, s * armTop * 0.3);
                var sp = b.Cone(g, upper, ux.InversePoint(sa), Math.Max(0.6, armTop * 0.4), upper, ux.InversePoint(sa + new Vec3(-0.2, 1, s * 0.45).Normalized() * Math.Max(1.6, armTop * 1.3)),
                    0.35, MaterialSlot.Metal, PatternDomain.None, 0, 1, arm.Side);
                sp.Flags |= PrimFlags.NoPattern | PrimFlags.Hard | PrimFlags.Thin;
                sp.Tag = "gear.pauldronSpike";
            }
        }
    }
}
