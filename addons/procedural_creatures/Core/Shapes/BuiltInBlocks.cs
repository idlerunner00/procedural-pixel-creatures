// Procedural Pixel Creatures - registry adapters for the built-in shape blocks.
//
// Families inside the framework use the typed specs directly. Extensions (and tools) can look the
// same blocks up by id in the CreatureRegistry and drive them with a loose ShapeParams bag, so a
// new body plan can reuse legs, tails, wings, fins, horns ... without referencing the spec types.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Shapes
{
    public static class BuiltInBlocks
    {
        public static void RegisterAll(CreatureRegistry r)
        {
            r.RegisterShapeBlock(new DelegateShapeBlock("core.chain", "Chains",
                "Bone chain with rounded cones (tails, necks, tentacles, antennae). Parameters: length, segments, radiusBase, radiusTip, curl, sweep, tip (TailTip-Index), tipSize, stiffness, droop, role (BoneRole-Index).",
                (b, p) =>
                {
                    var spec = new ChainSpec
                    {
                        Name = p.Name,
                        ParentBone = p.ParentBone,
                        Base = p.Position,
                        Direction = p.Direction,
                        Length = p.Get("length", 12),
                        Segments = (int)p.Get("segments", 5),
                        RadiusBase = p.Get("radiusBase", 2),
                        RadiusTip = p.Get("radiusTip", 0.6),
                        Curl = p.Get("curl", 0),
                        Sweep = p.Get("sweep", 0),
                        Tip = (TailTip)(int)p.Get("tip", 0),
                        TipSize = p.Get("tipSize", 1),
                        Material = p.Material,
                        Group = p.Group,
                        Side = p.Side,
                        Stiffness = p.Get("stiffness", 0.5),
                        Droop = p.Get("droop", 0.5),
                        Role = (BoneRole)(int)p.Get("role", (int)BoneRole.Tail),
                    };
                    var res = new ShapeResult { Chain = Chains.Build(b, spec) };
                    res.Bones.AddRange(res.Chain.Bones);
                    return res;
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.leg", "Legs",
                "Leg or arm with an IK rest pose. Parameters: contactX/Y/Z (ground contact or hand target), length, upper, lower, distal, radiusTop, style (LegStyle-Index), front (0/1), arm (0/1), pawRadius, pawLength, claws, phase.",
                (b, p) =>
                {
                    var spec = new LegSpec
                    {
                        Name = p.Name,
                        Side = p.Side,
                        AnchorBone = p.ParentBone,
                        Hip = p.Position,
                        Contact = new Vec3(p.Get("contactX", p.Position.X), p.Get("contactY", 0), p.Get("contactZ", p.Position.Z)),
                        Length = p.Get("length", 12),
                        Upper = p.Get("upper", 0.38), Lower = p.Get("lower", 0.36), Distal = p.Get("distal", 0.26),
                        RadiusTop = p.Get("radiusTop", 2),
                        RadiusKnee = p.Get("radiusTop", 2) * 0.7,
                        RadiusAnkle = p.Get("radiusTop", 2) * 0.5,
                        RadiusFoot = p.Get("radiusTop", 2) * 0.5,
                        Style = (LegStyle)(int)p.Get("style", (int)LegStyle.Digitigrade),
                        Front = p.Get("front", 0) > 0.5,
                        IsArm = p.Get("arm", 0) > 0.5,
                        Hand = p.Get("hand", 0) > 0.5,
                        NoFoot = p.Get("noFoot", 0) > 0.5,
                        PawRadius = p.Get("pawRadius", 1.2),
                        PawLength = p.Get("pawLength", 2.5),
                        Claws = (int)p.Get("claws", 0),
                        Phase = p.Get("phase", 0),
                        Material = p.Material,
                        FootMaterial = p.Material,
                        Group = p.Group,
                        DomainLength = p.Get("length", 12),
                    };
                    var leg = Limbs.Build(b, spec);
                    if (spec.IsArm) b.Rig.Arms.Add(leg); else b.Rig.Legs.Add(leg);
                    var res = new ShapeResult { Leg = leg };
                    res.Bones.Add(leg.RootBone);
                    res.Bones.AddRange(leg.JointBones);
                    res.Groups.Add(leg.Group);
                    return res;
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.ears", "Head",
                "Pair of ears on the head bone (ParentBone). Parameters: kind (EarKind-Index), size, width. Position = right ear attachment in head space.",
                (b, p) =>
                {
                    Ears.Build(b, new EarSpec { Kind = (EarKind)(int)p.Get("kind", (int)EarKind.Pointed), HeadBone = p.ParentBone, Base = p.Position, Size = p.Get("size", 4), Width = p.Get("width", 1), Material = p.Material, Name = p.Name });
                    return new ShapeResult();
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.horns", "Head",
                "Horns/antlers on the head bone. Parameters: kind (HornKind-Index), size, thickness; frontX/Y/Z for single horns.",
                (b, p) =>
                {
                    Horns.Build(b, new HornSpec
                    {
                        Kind = (HornKind)(int)p.Get("kind", (int)HornKind.Curved), HeadBone = p.ParentBone, Base = p.Position, Size = p.Get("size", 4), Thickness = p.Get("thickness", 1),
                        Front = new Vec3(p.Get("frontX", p.Position.X), p.Get("frontY", p.Position.Y), p.Get("frontZ", 0)), Material = p.Material, Name = p.Name,
                    });
                    return new ShapeResult();
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.wings", "Wings",
                "Pair of wings (built spread, folded/flapped by RigDriver). Parameters: kind (0 membrane, 1 feather, 2 insect), span, chord, raise, sweepBack, fingers, primaries, bodyBone, attachX/Y/Z.",
                (b, p) =>
                {
                    Wings.BuildPair(b, new WingSpec
                    {
                        Kind = (WingKind)(int)p.Get("kind", 0), Name = p.Name, AnchorBone = p.ParentBone, Root = p.Position,
                        Span = p.Get("span", 20), Chord = p.Get("chord", 8), Raise = p.Get("raise", 0.25), SweepBack = p.Get("sweepBack", 0.3),
                        Fingers = (int)p.Get("fingers", 3), Primaries = (int)p.Get("primaries", 3), BodyBone = (int)p.Get("bodyBone", p.ParentBone),
                        BodyAttach = new Vec3(p.Get("attachX", p.Position.X - 4), p.Get("attachY", p.Position.Y), p.Get("attachZ", p.Position.Z)),
                    });
                    return new ShapeResult();
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.fin", "Fins",
                "Fin (paired or unpaired, optionally animated). Parameters: shape (FinShape-Index), length, width, paired (0/1), animated (0/1), amplitude, kind (FinKind-Index).",
                (b, p) =>
                {
                    Fins.Build(b, new FinSpec
                    {
                        Name = p.Name, AnchorBone = p.ParentBone, Base = p.Position, Direction = p.Direction, Shape = (FinShape)(int)p.Get("shape", 0),
                        Length = p.Get("length", 5), Width = p.Get("width", 3), Paired = p.Get("paired", 1) > 0.5, Animated = p.Get("animated", 1) > 0.5,
                        Amplitude = p.Get("amplitude", 0.45), Kind = (FinKind)(int)p.Get("kind", 0), Material = p.Material, Axis = Vec3.UnitZ,
                    });
                    return new ShapeResult();
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.pincer", "Graspers",
                "Pincer or grasping blade on a hand bone (ParentBone). Parameters: size, bulk, blade (0/1).",
                (b, p) =>
                {
                    Pincers.Build(b, new PincerSpec { Name = p.Name, HandBone = p.ParentBone, Side = p.Side, Size = p.Get("size", 4), Bulk = p.Get("bulk", 1), Group = p.Group, Blade = p.Get("blade", 0) > 0.5 });
                    return new ShapeResult();
                }));

            r.RegisterShapeBlock(new DelegateShapeBlock("core.dorsal", "Back",
                "Dorsal feature along a line from Position to endX/Y/Z. Parameters: kind (DorsalKind-Index), size, count.",
                (b, p) =>
                {
                    var ds = new DorsalSpec { Kind = (DorsalKind)(int)p.Get("kind", (int)DorsalKind.Spikes), Size = p.Get("size", 3), Material = p.Material, Name = p.Name };
                    Vec3 end = new Vec3(p.Get("endX", p.Position.X - 10), p.Get("endY", p.Position.Y), p.Get("endZ", p.Position.Z));
                    int n = Math.Max(1, (int)p.Get("count", 5));
                    for (int i = 0; i < n; i++)
                        ds.Anchors.Add((p.ParentBone, Vec3.Lerp(p.Position, end, n == 1 ? 0.5 : i / (double)(n - 1)), p.Up, 1.0));
                    Dorsal.Build(b, ds);
                    return new ShapeResult();
                }));
        }
    }
}
