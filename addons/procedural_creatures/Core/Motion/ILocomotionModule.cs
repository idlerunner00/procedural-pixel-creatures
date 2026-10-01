// Procedural Pixel Creatures - locomotion module extension point.
//
// A locomotion module owns everything that depends on how a body plan moves over ground/water/air:
// gait clocks, foot plants, body undulation, flapping, hopping. It contributes pose parameters and,
// after the body has been posed, solves its limbs. Modules are registered by id in the
// CreatureRegistry; the anatomy's MotionRig selects one.

using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public interface ILocomotionModule
    {
        string Id { get; }

        /// <summary>Called once after the motor is created (and after teleports).</summary>
        void Initialize(MotorContext ctx);

        /// <summary>Advances internal state (gait phase, plants, swings) by one fixed step.</summary>
        void Step(MotorContext ctx, double dt);

        /// <summary>Adds the locomotion contribution to the base pose (weighted by ctx.MoveWeight inside).</summary>
        void Evaluate(MotorContext ctx, ref PoseParams p);

        /// <summary>Solves limbs after the body chain has been posed (FK already solved once).</summary>
        void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output);

        /// <summary>Recommended altitude of the root above ground for the current state.</summary>
        double Altitude(MotorContext ctx);

        /// <summary>Current cycle frequency in Hz (gait, undulation, flap). 0 = not periodic.</summary>
        double CycleFrequency(MotorContext ctx);
    }

    /// <summary>Base class with no-op defaults.</summary>
    public abstract class LocomotionModuleBase : ILocomotionModule
    {
        public abstract string Id { get; }
        public virtual void Initialize(MotorContext ctx) { }
        public virtual void Step(MotorContext ctx, double dt) { }
        public virtual void Evaluate(MotorContext ctx, ref PoseParams p) { }
        public virtual void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output) { }
        public virtual double Altitude(MotorContext ctx) => 0;
        public virtual double CycleFrequency(MotorContext ctx) => 0;
    }
}
