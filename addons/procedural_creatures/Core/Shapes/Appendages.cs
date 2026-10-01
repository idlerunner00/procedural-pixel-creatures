// Procedural Pixel Creatures - ears, horns and dorsal features (spikes, plates, ridges, manes).

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public enum EarKind : byte
    {
        None,
        Pointed,
        Round,
        Floppy,
        Long,
        Tufted,
        Fin,
        /// <summary>Long pointed ears sticking out sideways (goblins, elves).</summary>
        Elf,
    }

    public enum HornKind : byte
    {
        None,
        Short,
        Curved,
        Ram,
        Antlers,
        Unicorn,
        Backswept,
        Nasal,
    }

    public sealed class EarSpec
    {
        public EarKind Kind = EarKind.Pointed;
        public int HeadBone;
        /// <summary>Ear base in the head bone frame for the right ear (+Z); mirrored for the left ear.</summary>
        public Vec3 Base;
        public double Size = 4;
        public double Width = 1;
        public MaterialSlot Material = MaterialSlot.Primary;
        public double Stiffness = 0.6;
        public string Name = "ear";
    }

    public sealed class HornSpec
    {
        public HornKind Kind = HornKind.Curved;
        public int HeadBone;
        /// <summary>Horn base in the head frame for the right horn (+Z); mirrored for the left one.</summary>
        public Vec3 Base;
        public double Size = 4;
        public double Thickness = 1;
        public MaterialSlot Material = MaterialSlot.Accent;
        public string Name = "horn";
        /// <summary>Head front point for single horns (unicorn, nasal) in the head frame.</summary>
        public Vec3 Front;
    }

    public static class Ears
    {
        public static void Build(AnatomyBuilder b, EarSpec s)
        {
            if (s.Kind == EarKind.None || s.Size < 0.8) return;
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 baseLocal = new Vec3(s.Base.X, s.Base.Y, s.Base.Z * side);
                Vec3 baseWorld = b.RestWorld(s.HeadBone).Point(baseLocal);
                // local ear direction in head frame
                Vec3 dirLocal;
                double len = s.Size, r0, r1;
                switch (s.Kind)
                {
                    case EarKind.Round: dirLocal = new Vec3(-0.15, 1, 0.45 * side); r0 = len * 0.42 * s.Width; r1 = len * 0.36 * s.Width; len *= 0.55; break;
                    case EarKind.Floppy: dirLocal = new Vec3(0.1, -0.7, 0.9 * side); r0 = len * 0.28 * s.Width; r1 = len * 0.3 * s.Width; break;
                    case EarKind.Long: dirLocal = new Vec3(-0.35, 1, 0.22 * side); r0 = len * 0.2 * s.Width; r1 = len * 0.16 * s.Width; len *= 1.5; break;
                    case EarKind.Fin: dirLocal = new Vec3(-0.9, 0.35, 0.55 * side); r0 = len * 0.22 * s.Width; r1 = 0.4; break;
                    case EarKind.Elf: dirLocal = new Vec3(-0.45, 0.32, 0.95 * side); r0 = len * 0.26 * s.Width; r1 = 0.4; len *= 1.25; break;
                    default: dirLocal = new Vec3(-0.12, 1, 0.38 * side); r0 = len * 0.3 * s.Width; r1 = Math.Max(0.35, len * 0.06); break;
                }
                Vec3 dirWorld = b.RestWorld(s.HeadBone).Dir(dirLocal).Normalized();
                int g = b.Group($"{s.Name}.{(side < 0 ? "L" : "R")}", 0.9, side);
                int ear = b.BoneAlong($"{s.Name}.{(side < 0 ? "L" : "R")}", s.HeadBone, baseWorld, dirWorld, Vec3.UnitY, BoneRole.Ear, side, len);
                Vec3 tipWorld = baseWorld + dirWorld * len;
                int tip = b.BoneAt($"{s.Name}.{(side < 0 ? "L" : "R")}.tip", ear, tipWorld, BoneRole.Ear, side);
                var outer = b.Segment(g, ear, Math.Max(0.6, r0), tip, Math.Max(0.35, r1), s.Material, PatternDomain.Appendage, 0, 1, side);
                outer.DomainLength = len;
                outer.Tag = s.Name + ".outer";
                if (s.Kind == EarKind.Pointed || s.Kind == EarKind.Tufted || s.Kind == EarKind.Long || s.Kind == EarKind.Round || s.Kind == EarKind.Elf)
                {
                    // inner ear, facing forward: readable in front views
                    var inner = b.Cone(g, ear, new Vec3(len * 0.12, 0, 0) + new Vec3(0, 0, 0), Math.Max(0.45, r0 * 0.55), ear, new Vec3(len * 0.7, 0, 0), Math.Max(0.3, r1 * 0.6),
                        MaterialSlot.Secondary, PatternDomain.None, 0, 1, side);
                    // shift the inner ear to the front face of the ear (towards head forward)
                    Vec3 frontLocal = b.RestWorld(ear).InverseDir(b.RestWorld(s.HeadBone).Dir(Vec3.UnitX)).Normalized() * (r0 * 0.45);
                    inner.LocalA = inner.LocalA + frontLocal;
                    inner.LocalB = inner.LocalB + frontLocal * 0.8;
                    inner.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                    inner.ShadeBias = -1;
                    inner.Tag = s.Name + ".inner";
                }
                if (s.Kind == EarKind.Tufted)
                {
                    var tuft = b.Cone(g, tip, Vec3.Zero, 0.5, tip, new Vec3(0, 0, 0) + b.RestWorld(tip).InverseDir(dirWorld) * (len * 0.35), 0.3, MaterialSlot.Marking);
                    tuft.Flags |= PrimFlags.Thin | PrimFlags.NoPattern;
                }
                b.Rig.Appendages.Add(new ChainRig
                {
                    Name = $"{s.Name}.{(side < 0 ? "L" : "R")}",
                    Bones = new[] { ear, tip },
                    Lengths = new[] { len },
                    TotalLength = len,
                    Side = side,
                    Radii = new[] { r0, r1 },
                    Stiffness = s.Kind == EarKind.Floppy ? 0.25 : s.Stiffness,
                    RestPitch = new[] { 0.0 },
                    RestYaw = new[] { 0.0 },
                    Droop = s.Kind == EarKind.Floppy ? 0.8 : 0.1,
                });
            }
        }
    }

    public static class Horns
    {
        public static void Build(AnatomyBuilder b, HornSpec s)
        {
            if (s.Kind == HornKind.None || s.Size < 0.8) return;
            if (s.Kind == HornKind.Unicorn || s.Kind == HornKind.Nasal)
            {
                BuildSingle(b, s);
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Vec3 baseLocal = new Vec3(s.Base.X, s.Base.Y, s.Base.Z * side);
                var pts = new List<Vec3>();
                var radii = new List<double>();
                double sz = s.Size, th = s.Thickness;
                switch (s.Kind)
                {
                    case HornKind.Short:
                        pts.Add(baseLocal);
                        pts.Add(baseLocal + new Vec3(0.2 * sz, 0.9 * sz, 0.25 * sz * side));
                        radii.Add(0.32 * sz * th); radii.Add(0.35);
                        break;
                    case HornKind.Curved:
                    {
                        // rises, then sweeps back and out
                        Vec3 p = baseLocal;
                        pts.Add(p);
                        Vec3 d = new Vec3(0.25, 1, 0.45 * side).Normalized();
                        int n = 4;
                        for (int i = 0; i < n; i++)
                        {
                            p = p + d * (sz / n);
                            pts.Add(p);
                            d = Chains.RotateAround(d, new Vec3(0, 0, 1), 0.38);
                        }
                        for (int i = 0; i <= n; i++) radii.Add(DMath.Lerp(0.3 * sz * th, 0.3, i / (double)n));
                        break;
                    }
                    case HornKind.Backswept:
                    {
                        Vec3 p = baseLocal;
                        pts.Add(p);
                        Vec3 d = new Vec3(-0.75, 0.55, 0.25 * side).Normalized();
                        int n = 3;
                        for (int i = 0; i < n; i++)
                        {
                            p = p + d * (sz * 1.2 / n);
                            pts.Add(p);
                            d = Chains.RotateAround(d, new Vec3(0, 0, 1), -0.18);
                        }
                        for (int i = 0; i <= n; i++) radii.Add(DMath.Lerp(0.28 * sz * th, 0.3, i / (double)n));
                        break;
                    }
                    case HornKind.Ram:
                    {
                        // spiral around the side of the head
                        double rad = sz * 0.55;
                        Vec3 c = baseLocal + new Vec3(-rad * 0.55, -rad * 0.35, side * rad * 0.35);
                        int n = 7;
                        for (int i = 0; i <= n; i++)
                        {
                            double t = i / (double)n;
                            double ang = DMath.HalfPi + t * DMath.Pi * 1.45;
                            double rr = rad * (1.0 - 0.35 * t);
                            Vec3 p = c + new Vec3(DMath.Cos(ang) * rr * -1, DMath.Sin(ang) * rr, side * t * rad * 0.8);
                            if (i == 0) p = baseLocal;
                            pts.Add(p);
                            radii.Add(DMath.Lerp(0.36 * sz * th, 0.45, t));
                        }
                        break;
                    }
                    case HornKind.Antlers:
                    {
                        Vec3 p = baseLocal;
                        pts.Add(p);
                        Vec3 d = new Vec3(-0.15, 1, 0.5 * side).Normalized();
                        int n = 3;
                        for (int i = 0; i < n; i++)
                        {
                            p = p + d * (sz * 1.25 / n);
                            pts.Add(p);
                            d = Chains.RotateAround(d, new Vec3(0, 0, 1), 0.28);
                        }
                        for (int i = 0; i <= n; i++) radii.Add(DMath.Lerp(0.22 * sz * th, 0.35, i / (double)n));
                        break;
                    }
                }
                BuildChain(b, s, side, pts, radii);
                if (s.Kind == HornKind.Antlers)
                {
                    // tines branching forward from the main beam
                    for (int t = 1; t <= 2; t++)
                    {
                        Vec3 a = pts[t];
                        Vec3 tip = a + new Vec3(0.55, 0.75, 0.1 * side).Normalized() * (s.Size * 0.5);
                        BuildChain(b, s, side, new List<Vec3> { a, tip }, new List<double> { 0.45, 0.3 }, "tine" + t);
                    }
                }
            }
        }

        private static void BuildSingle(AnatomyBuilder b, HornSpec s)
        {
            Vec3 baseLocal = s.Front;
            Vec3 d = s.Kind == HornKind.Unicorn ? new Vec3(0.8, 0.75, 0).Normalized() : new Vec3(0.35, 1, 0).Normalized();
            var pts = new List<Vec3> { baseLocal, baseLocal + d * (s.Size * 0.55), baseLocal + d * s.Size + new Vec3(s.Kind == HornKind.Nasal ? -0.4 * s.Size : 0, 0, 0) };
            var radii = new List<double> { 0.32 * s.Size * s.Thickness, 0.2 * s.Size * s.Thickness, 0.3 };
            BuildChain(b, s, 0, pts, radii);
        }

        private static void BuildChain(AnatomyBuilder b, HornSpec s, int side, List<Vec3> ptsLocal, List<double> radii, string suffix = "")
        {
            string name = $"{s.Name}{suffix}.{(side < 0 ? "L" : side > 0 ? "R" : "C")}";
            int g = b.Group(name, 0.6, side);
            var head = b.RestWorld(s.HeadBone);
            // horns are rigid: every segment hangs off the head bone
            double total = 0;
            for (int i = 1; i < ptsLocal.Count; i++) total += (ptsLocal[i] - ptsLocal[i - 1]).Length;
            double acc = 0;
            for (int i = 0; i + 1 < ptsLocal.Count; i++)
            {
                double segLen = (ptsLocal[i + 1] - ptsLocal[i]).Length;
                var prim = b.Cone(g, s.HeadBone, ptsLocal[i], Math.Max(0.35, radii[i]), s.HeadBone, ptsLocal[i + 1], Math.Max(0.3, radii[i + 1]),
                    s.Material, PatternDomain.Horn, acc / total, (acc + segLen) / total, side);
                prim.DomainLength = total;
                prim.Flags |= PrimFlags.NoPattern;
                prim.Tag = name;
                acc += segLen;
            }
            _ = head;
        }
    }

    public enum DorsalKind : byte
    {
        None,
        Spikes,
        Plates,
        Ridge,
        Mane,
        Sail,
        Quills,
    }

    public sealed class DorsalSpec
    {
        public DorsalKind Kind;
        /// <summary>Anchor points along the back (creature space surface points, bone to attach).</summary>
        public readonly List<(int bone, Vec3 surfaceWorld, Vec3 normalWorld, double scale)> Anchors = new List<(int, Vec3, Vec3, double)>();
        public double Size = 3;
        public MaterialSlot Material = MaterialSlot.Accent;
        public string Name = "dorsal";
        public int BodyGroup = -1;
    }

    public static class Dorsal
    {
        public static void Build(AnatomyBuilder b, DorsalSpec s)
        {
            if (s.Kind == DorsalKind.None || s.Anchors.Count == 0) return;
            switch (s.Kind)
            {
                case DorsalKind.Ridge:
                {
                    int g = s.BodyGroup >= 0 ? s.BodyGroup : b.Group(s.Name, 1.0);
                    foreach (var (bone, pos, nrm, scale) in s.Anchors)
                    {
                        var xf = b.RestWorld(bone);
                        Vec3 c = xf.InversePoint(pos + nrm * (s.Size * 0.15 * scale));
                        var e = b.Ellipsoid(g, bone, c, new Vec3(s.Size * 0.55 * scale, s.Size * 0.45 * scale, s.Size * 0.35 * scale), s.Material, PatternDomain.None);
                        e.Flags |= PrimFlags.NoPattern;
                        e.Tag = s.Name + ".ridge";
                    }
                    break;
                }
                case DorsalKind.Mane:
                {
                    int g = b.Group(s.Name, 1.6, 0, GroupFlags.CreaseShade);
                    foreach (var (bone, pos, nrm, scale) in s.Anchors)
                    {
                        var xf = b.RestWorld(bone);
                        Vec3 c = xf.InversePoint(pos + nrm * (s.Size * 0.25 * scale) - new Vec3(s.Size * 0.2, 0, 0));
                        var e = b.Ellipsoid(g, bone, c, new Vec3(s.Size * 0.75 * scale, s.Size * 0.62 * scale, s.Size * 0.75 * scale), s.Material, PatternDomain.None);
                        e.Flags |= PrimFlags.NoPattern;
                        e.Tag = s.Name + ".mane";
                    }
                    break;
                }
                case DorsalKind.Spikes:
                case DorsalKind.Quills:
                {
                    int g = b.Group(s.Name, 0.5, 0, GroupFlags.None, -0.2);
                    foreach (var (bone, pos, nrm, scale) in s.Anchors)
                    {
                        var xf = b.RestWorld(bone);
                        Vec3 back = new Vec3(-1, 0, 0);
                        Vec3 dir = (nrm * 1.0 + back * (s.Kind == DorsalKind.Quills ? 0.9 : 0.35)).Normalized();
                        double h = s.Size * scale;
                        Vec3 a = xf.InversePoint(pos - nrm * 0.6);
                        Vec3 tip = xf.InversePoint(pos + dir * h);
                        double r0 = Math.Max(0.55, h * (s.Kind == DorsalKind.Quills ? 0.16 : 0.32));
                        var c = b.Cone(g, bone, a, r0, bone, tip, 0.3, s.Material, PatternDomain.None);
                        c.Flags |= PrimFlags.NoPattern | PrimFlags.Hard;
                        c.Tag = s.Name + ".spike";
                    }
                    break;
                }
                case DorsalKind.Plates:
                {
                    int g = b.Group(s.Name, 0.3, 0, GroupFlags.None, -0.2);
                    int i = 0;
                    foreach (var (bone, pos, nrm, scale) in s.Anchors)
                    {
                        var xf = b.RestWorld(bone);
                        double h = s.Size * scale * 1.2;
                        double w = s.Size * scale * 0.9;
                        double zoff = (i % 2 == 0 ? 1 : -1) * 0.5;
                        Vec3 up = nrm.Normalized();
                        Vec3 a = pos - new Vec3(w * 0.55, 0, -zoff) - up * 0.5;
                        Vec3 c = pos + new Vec3(w * 0.5, 0, zoff) - up * 0.5;
                        Vec3 tip = pos + up * h + new Vec3(-w * 0.15, 0, zoff);
                        var t = b.Triangle(g, bone, xf.InversePoint(a), bone, xf.InversePoint(tip), bone, xf.InversePoint(c), s.Material, PatternDomain.None);
                        t.Flags |= PrimFlags.NoPattern | PrimFlags.TwoSided;
                        t.Tag = s.Name + ".plate";
                        i++;
                    }
                    break;
                }
                case DorsalKind.Sail:
                {
                    int g = b.Group(s.Name, 0.3, 0, GroupFlags.NoFarShade, -0.3);
                    for (int i = 0; i + 1 < s.Anchors.Count; i++)
                    {
                        var (boneA, pa, na, sa) = s.Anchors[i];
                        var (boneB, pb, nb, sb) = s.Anchors[i + 1];
                        var xa = b.RestWorld(boneA);
                        var xb = b.RestWorld(boneB);
                        Vec3 ta = pa + na * s.Size * sa;
                        Vec3 tb = pb + nb * s.Size * sb;
                        var t1 = b.Triangle(g, boneA, xa.InversePoint(pa - na * 0.4), boneA, xa.InversePoint(ta), boneB, xb.InversePoint(pb - nb * 0.4), MaterialSlot.Membrane, PatternDomain.Fin);
                        var t2 = b.Triangle(g, boneA, xa.InversePoint(ta), boneB, xb.InversePoint(tb), boneB, xb.InversePoint(pb - nb * 0.4), MaterialSlot.Membrane, PatternDomain.Fin);
                        t1.Flags |= PrimFlags.Translucent; t2.Flags |= PrimFlags.Translucent;
                        t1.DomainLength = t2.DomainLength = s.Size;
                    }
                    break;
                }
            }
        }
    }
}
