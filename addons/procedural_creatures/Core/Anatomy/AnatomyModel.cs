// Procedural Pixel Creatures - anatomy data model.
//
// A CreatureAnatomy is built once per (anatomy genes + anatomy seed + generator version) and is
// immutable afterwards. It contains the skeleton (rest pose), the parametric shape primitives bound
// to bones, the part groups that control blending/contours, facial features, the semantic motion
// rig used by the motion modules, and measured metrics.

using System;
using System.Collections.Generic;

namespace PixelCreatures.Core.Anatomy
{
    public enum BoneRole : byte
    {
        Root, Pelvis, Spine, Chest, Neck, Head, Jaw, Tail, Hip, LegUpper, LegLower, LegMeta, Foot, Toe,
        Shoulder, ArmUpper, ArmLower, Hand, WingArm, WingHand, WingFinger, Ear, Antenna, Tentacle, Fin,
        Horn, Segment, Body, Other,
    }

    public sealed class BoneDef
    {
        public int Index { get; internal set; }
        public string Name { get; }
        public int Parent { get; }
        public Vec3 RestOffset { get; }
        public Quat RestRotation { get; }
        public double Length { get; }
        public BoneRole Role { get; }
        public int Side { get; }

        public BoneDef(string name, int parent, Vec3 restOffset, Quat restRotation, double length, BoneRole role, int side)
        {
            Name = name; Parent = parent; RestOffset = restOffset; RestRotation = restRotation; Length = length; Role = role; Side = side;
        }
    }

    public enum PrimKind : byte
    {
        Ellipsoid,
        RoundCone,
        Triangle,
    }

    [Flags]
    public enum PrimFlags : ushort
    {
        None = 0,
        /// <summary>Thin feature (antenna, whisker, claw). Protected from silhouette cleanup.</summary>
        Thin = 1,
        /// <summary>Does not take part in the soft union of its group (hard z-tested).</summary>
        Hard = 2,
        /// <summary>Flat faceted shading (stone, crystal, bark).</summary>
        Faceted = 4,
        /// <summary>Two sided plate (membranes, fins): back side uses a darker shade.</summary>
        TwoSided = 8,
        /// <summary>Casts no ground shadow.</summary>
        NoShadow = 16,
        /// <summary>Only drawn when its surface faces the viewer (e.g. inner mouth shapes).</summary>
        FrontOnly = 32,
        /// <summary>Emissive: ignores lighting (glow cores, eyes of spirits).</summary>
        Emissive = 64,
        /// <summary>Pattern marks are never applied to this primitive.</summary>
        NoPattern = 128,
        /// <summary>Surface rendered one shade lighter (translucent membranes).</summary>
        Translucent = 256,
        /// <summary>Casts a ground shadow even though its pattern domain is not a body/limb domain.</summary>
        ShadowCaster = 512,
        /// <summary>Shell plates (tortoise carapace): seeded scutes with dark seams on an ellipsoid (uses FacetSeed).</summary>
        Scutes = 1024,
    }

    /// <summary>Material slots of a creature. The palette maps each slot to a ramp of shades.</summary>
    public enum MaterialSlot : byte
    {
        Primary = 0,
        Secondary = 1,
        Accent = 2,
        Membrane = 3,
        Mouth = 4,
        Teeth = 5,
        Eye = 6,
        Marking = 7,
        Glow = 8,
        /// <summary>White marks: piebald patches and light socks (an ivory ramp tinted towards the body).</summary>
        White = 9,
        /// <summary>Clothing and leather gear (own colour genes; leather brown by default).</summary>
        Cloth = 10,
        /// <summary>Forged metal: blades, helmets, armour plates (steel, dark iron, bronze or gold).</summary>
        Metal = 11,
        /// <summary>Head hair and beards (own colour genes; falls back to the marking colour).</summary>
        Hair = 12,
        Count = 13,
    }

    /// <summary>Surface coordinate systems for anchored patterns.</summary>
    public enum PatternDomain : byte
    {
        None,
        Body,
        Neck,
        Head,
        Tail,
        Limb,
        Wing,
        Fin,
        Appendage,
        Horn,
    }

    public sealed class PrimitiveDef
    {
        public PrimKind Kind;
        public int Group;
        public MaterialSlot Material;
        public PrimFlags Flags;
        public int Side;
        public string Tag = string.Empty;

        // Attachment. Ellipsoid: center A in bone A frame. RoundCone: A in bone A, B in bone B.
        // Triangle: vertices A, B, C in bones A, B, C.
        public int BoneA = -1, BoneB = -1, BoneC = -1;
        public Vec3 LocalA, LocalB, LocalC;
        public double RadiusA, RadiusB;
        public Vec3 Radii;
        public Quat LocalRotation = Quat.Identity;

        // Pattern mapping: normalised coordinate along the part (0 = start, 1 = tip) covered by this
        // primitive ([U0,U1]) and the total length of the domain in pixels (for round marks).
        public PatternDomain Domain = PatternDomain.None;
        public double U0, U1 = 1;
        public double DomainLength = 10;
        /// <summary>Extra shading bias in shade levels (negative = darker), e.g. -1 for inner mouth.</summary>
        public int ShadeBias;
        /// <summary>Faceted shading / scute layout seed (PrimFlags.Faceted, PrimFlags.Scutes).</summary>
        public uint FacetSeed;
        /// <summary>Level of detail: primitive is skipped at render quality below this value.</summary>
        public int MinQuality;

        public PrimitiveDef Clone() => (PrimitiveDef)MemberwiseClone();
    }

    [Flags]
    public enum GroupFlags : ushort
    {
        None = 0,
        /// <summary>No darkening when the group is on the far side of the body.</summary>
        NoFarShade = 1,
        /// <summary>No contour line drawn where this group overlaps others.</summary>
        NoContour = 2,
        /// <summary>No outline at all (e.g. mouth interior).</summary>
        NoOutline = 4,
        /// <summary>Darken concave fillet pixels (joints, creases).</summary>
        CreaseShade = 8,
        /// <summary>Hidden unless the jaw is open (mouth interior).</summary>
        Interior = 16,
    }

    public sealed class PartGroup
    {
        public int Index { get; internal set; }
        public string Name { get; }
        /// <summary>Soft union radius in pixels.</summary>
        public double BlendRadius { get; }
        public int Side { get; }
        public GroupFlags Flags { get; }
        /// <summary>Depth bias in pixels applied in z tests (positive = drawn in front on ties).</summary>
        public double DepthBias { get; }

        public PartGroup(string name, double blendRadius, int side, GroupFlags flags, double depthBias)
        {
            Name = name; BlendRadius = blendRadius; Side = side; Flags = flags; DepthBias = depthBias;
        }
    }

    public enum FeatureKind : byte
    {
        Eye,
        Nostril,
        /// <summary>Small bright spot (gem, glow freckle, highlight on a horn).</summary>
        Spot,
    }

    public enum EyeStyle : byte
    {
        /// <summary>Solid dark bead with a highlight pixel.</summary>
        Bead,
        /// <summary>Light sclera, coloured iris, dark pupil.</summary>
        Round,
        /// <summary>Coloured iris with a vertical slit pupil (reptiles, predators).</summary>
        Slit,
        /// <summary>Large glossy compound eye (insects).</summary>
        Compound,
        /// <summary>Glowing eye without pupil (monsters, spirits).</summary>
        Glow,
        /// <summary>Big dark eye with a big highlight (cute).</summary>
        Button,
    }

    public sealed class FeatureDef
    {
        public FeatureKind Kind;
        public int Bone;
        public Vec3 LocalPosition;
        public Vec3 LocalNormal = Vec3.UnitX;
        /// <summary>Nominal size in pixels (eyes: height of the eye stamp).</summary>
        public double Size = 2;
        public int Side;
        public EyeStyle Style;
        /// <summary>Horizontal stretch of the eye (1 = round).</summary>
        public double Aspect = 1.0;
        /// <summary>Brow heaviness 0..1 (adds a lid line / angry slant).</summary>
        public double Brow;
        public MaterialSlot Material = MaterialSlot.Eye;
        public int Group = -1;
    }

    /// <summary>Canvas large enough for every pose and direction of the creature.</summary>
    public sealed class CanvasSpec
    {
        public int Width { get; }
        public int Height { get; }
        /// <summary>Pixel position of the ground point below the creature root.</summary>
        public int OriginX { get; }
        public int OriginY { get; }

        public CanvasSpec(int width, int height, int originX, int originY)
        {
            Width = width; Height = height; OriginX = originX; OriginY = originY;
        }

        public override string ToString() => $"{Width}x{Height} origin ({OriginX},{OriginY})";
    }

    public sealed class AnatomyMetrics
    {
        /// <summary>Overall body length (nose to tail tip) in pixels.</summary>
        public double BodyLength;
        /// <summary>Height of the back above ground in the rest pose.</summary>
        public double BackHeight;
        /// <summary>Height of the hip/shoulder joints above ground.</summary>
        public double HipHeight;
        /// <summary>Maximum horizontal reach of any part from the root.</summary>
        public double HorizontalRadius;
        /// <summary>Highest point in the rest pose.</summary>
        public double TopHeight;
        /// <summary>Approximate mass relative to a 48px creature (used for timing).</summary>
        public double Mass = 1;
        /// <summary>Nominal leg length (used for stride and speeds).</summary>
        public double LegLength;
        public double WalkSpeed;
        public double RunSpeed;
        /// <summary>Hover/swim altitude of the root above ground in the rest pose.</summary>
        public double Altitude;
        /// <summary>Scale of pattern marks relative to a medium (48 px) creature. Keeps marks readable on big and small bodies.</summary>
        public double PatternScale = 1;
    }
}
