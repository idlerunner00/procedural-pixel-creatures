// Procedural Pixel Creatures - semantic motion rig.
//
// The motion rig is the contract between anatomy builders and motion modules. It names the bones
// and chains a module drives (legs, spine, neck, tail, wings, ...) and carries measured rest data
// (lengths, rest foot positions). Motion modules never look at primitives; anatomy builders never
// compute animation. This keeps both sides replaceable.

using System.Collections.Generic;

namespace PixelCreatures.Core.Anatomy
{
    public enum LegStyle : byte
    {
        /// <summary>Walks on the whole sole (bears, humanoids).</summary>
        Plantigrade,
        /// <summary>Walks on the toes with a raised heel (dogs, cats, birds).</summary>
        Digitigrade,
        /// <summary>Walks on hoof tips (deer, horses).</summary>
        Unguligrade,
        /// <summary>Legs splay sideways from the body (lizards, salamanders).</summary>
        Sprawling,
        /// <summary>Knee raised above the body, foot far out (spiders, insects, crabs).</summary>
        Arthropod,
        /// <summary>Short stump or root leg (plants, golems).</summary>
        Stump,
    }

    public sealed class LegRig
    {
        public string Name = string.Empty;
        public int Side;
        /// <summary>Order from front (0) to back.</summary>
        public int Pair;
        /// <summary>Body bone the leg hangs from.</summary>
        public int AnchorBone = -1;
        /// <summary>Leg root joint (hip/shoulder) bone.</summary>
        public int RootBone = -1;
        /// <summary>Bones whose origins are the joints after the root: knee, ankle, (meta), foot tip.</summary>
        public int[] JointBones = System.Array.Empty<int>();
        public double[] SegmentLengths = System.Array.Empty<double>();
        public double TotalLength;
        /// <summary>Neutral foot contact point in creature space (rest pose, on the ground).</summary>
        public Vec3 RestFoot;
        /// <summary>Joint bend direction: +1 knee bends forward (+X), -1 bends backward. Per joint.</summary>
        public double[] BendSigns = System.Array.Empty<double>();
        public LegStyle Style;
        /// <summary>Gait phase offset in [0,1).</summary>
        public double Phase;
        public double LiftHeight = 3;
        /// <summary>Arms do not carry weight; they swing or reach.</summary>
        public bool IsArm;
        /// <summary>Radius of the foot (used for ground contact height).</summary>
        public double FootRadius = 1;
        /// <summary>Optional toe/claw bones rotated with the foot.</summary>
        public int[] ToeBones = System.Array.Empty<int>();
        /// <summary>Height of the foot-contact joint above ground in the rest pose (sole thickness).</summary>
        public double SoleHeight = 1;
        /// <summary>Angle of the distal segment from vertical in degrees (digitigrade hock / carpus).</summary>
        public double MetaAngle = 25;
        /// <summary>Part group of the leg (for debug views).</summary>
        public int Group = -1;
        /// <summary>Optional elbow/knee pole hint as (forward, up, outward) weights; zero = style default.</summary>
        public Vec3 Pole;
    }

    public sealed class ChainRig
    {
        public string Name = string.Empty;
        public int[] Bones = System.Array.Empty<int>();
        public double[] Lengths = System.Array.Empty<double>();
        public double TotalLength;
        public int Side;
        /// <summary>Radius per chain joint (used for ground collision of dragging tails).</summary>
        public double[] Radii = System.Array.Empty<double>();
        /// <summary>Stiffness 0..1 of the simulated chain (1 = rigid).</summary>
        public double Stiffness = 0.5;
        /// <summary>Rest bend per joint (pitch) in radians, e.g. tail carriage or curl.</summary>
        public double[] RestPitch = System.Array.Empty<double>();
        public double[] RestYaw = System.Array.Empty<double>();
        /// <summary>Mass per joint for dynamics (heavier tips drag more).</summary>
        public double Droop;
    }

    public enum WingKind : byte
    {
        Membrane,
        Feathered,
        Insect,
    }

    /// <summary>
    /// A wing built in its spread rest pose. Bones are oriented with local +X along the wing (outwards),
    /// +Y up. Folding and flapping are expressed as rotations about the local axes (see RigDriver).
    /// </summary>
    public sealed class WingRig
    {
        public int Side;
        public WingKind Kind;
        /// <summary>Upper arm (humerus) bone; rotates for flapping. Insects: the whole wing.</summary>
        public int ArmBone = -1;
        public int ForearmBone = -1;
        public int HandBone = -1;
        public int[] FingerBones = System.Array.Empty<int>();
        public double Span;
        public double ArmLength, ForearmLength, HandLength;
        /// <summary>Insect wings: pair index (0 fore, 1 hind).</summary>
        public int Pair;
        /// <summary>Flap amplitude up / down in radians.</summary>
        public double FlapUp = 1.05, FlapDown = 0.75;
        /// <summary>Folded pose: shoulder drop, sweep back, elbow and wrist fold (radians).</summary>
        public double FoldDrop = 0.55, FoldSweep = 1.1, FoldElbow = 2.5, FoldWrist = 2.7;
        /// <summary>Sign of the elbow fold (+1: forearm folds forwards as in birds, -1: backwards).</summary>
        public double ElbowSign = 1;
        /// <summary>
        /// Explicit folded pose: corrections applied on top of the fold angles above (eased in towards the
        /// end of the fold) so that the fully folded wing reaches a designed pose, while the motion in
        /// between keeps the natural sweep-elbow-wrist folding.
        /// </summary>
        public bool UseFoldTargets;
        public Quat ArmFix = Quat.Identity, ForeFix = Quat.Identity, HandFix = Quat.Identity;
    }

    public enum FinKind : byte
    {
        Pectoral,
        Pelvic,
        Dorsal,
        Anal,
        /// <summary>Large flapping pectoral wing of rays.</summary>
        RayWing,
    }

    /// <summary>A fin or fin-like flap driven by PoseParams.FinPhase/FinAmp (oscillates about a local axis).</summary>
    public sealed class FinRig
    {
        public string Name = string.Empty;
        public int Bone = -1;
        public int Side;
        public FinKind Kind;
        /// <summary>Local rotation axis of the fin bone.</summary>
        public Vec3 Axis = Vec3.UnitX;
        public double Amplitude = 0.5;
        /// <summary>Phase offset in radians (left/right fins alternate, rays ripple).</summary>
        public double Phase;
        public double RestAngle;
    }

    /// <summary>Movable finger of a pincer or gripping mouth part, opened by PoseParams.Grip.</summary>
    public sealed class GripperRig
    {
        public int Bone = -1;
        public Vec3 Axis = Vec3.UnitZ;
        public double OpenAngle = 0.7;
        public int Side;
    }

    public enum LocomotionKind : byte
    {
        Legged,
        Serpentine,
        Swim,
        Fly,
        Hop,
        Float,
    }

    public enum ActionStyle : byte
    {
        Bite,
        Roar,
        Slam,
        Pounce,
        Strike,
        PincerSnap,
        Sting,
        WingBuffet,
        TentacleLash,
        TailSwipe,
        Spray,
        /// <summary>Threat display: rear up, lift the front legs, bare the fangs, snap down.</summary>
        Rear,
    }

    public enum RestStyle : byte
    {
        LieDown,
        SitSlump,
        Coil,
        Settle,
        Perch,
        Puddle,
        Fold,
    }

    public sealed class MotionRig
    {
        public int Root = -1;
        public int Pelvis = -1;
        public int Chest = -1;
        public int Head = -1;
        public int Jaw = -1;
        public ChainRig? Spine;
        public ChainRig? Neck;
        public ChainRig? Tail;
        public readonly List<LegRig> Legs = new List<LegRig>();
        public readonly List<LegRig> Arms = new List<LegRig>();
        public readonly List<WingRig> Wings = new List<WingRig>();
        /// <summary>Soft appendages simulated with spring chains (ears, antennae, tentacles, whiskers, manes, fins).</summary>
        public readonly List<ChainRig> Appendages = new List<ChainRig>();
        /// <summary>Body segments for serpentine and segmented bodies (head first).</summary>
        public ChainRig? Body;
        public readonly List<ChainRig> Tentacles = new List<ChainRig>();
        public readonly List<FinRig> Fins = new List<FinRig>();
        public readonly List<GripperRig> Grippers = new List<GripperRig>();
        /// <summary>Tongue bone (built retracted inside the head, extended along its parent's +X).</summary>
        public int Tongue = -1;
        public double TongueLength;
        public LocomotionKind Locomotion;
        public ActionStyle Action;
        public RestStyle Rest;
        /// <summary>Id of the locomotion module (registry key). Empty = default for Locomotion.</summary>
        public string LocomotionModule = string.Empty;
        /// <summary>Jaw open angle limit in radians.</summary>
        public double JawOpenMax = 0.6;
        /// <summary>Upright torso (bipeds): spine points up instead of forward.</summary>
        public bool Upright;
        /// <summary>Default hover/swim altitude of the root (0 for ground creatures).</summary>
        public double Altitude;
        /// <summary>Extra per family tuning values for motion modules.</summary>
        public readonly Dictionary<string, double> Tuning = new Dictionary<string, double>();

        public double Tune(string key, double fallback) => Tuning.TryGetValue(key, out double v) ? v : fallback;
    }
}
