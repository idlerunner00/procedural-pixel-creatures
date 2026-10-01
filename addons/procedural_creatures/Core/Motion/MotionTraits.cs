// Procedural Pixel Creatures - motion personality derived from the Motion stream and body metrics.

using System;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;

namespace PixelCreatures.Core.Motion
{
    public sealed class MotionTraits
    {
        public double Energy { get; private set; }
        public double Weight { get; private set; }
        public double Stiffness { get; private set; }
        public double Bounce { get; private set; }
        public double Aggression { get; private set; }
        public double Curiosity { get; private set; }
        public double SpeedFactor { get; private set; }
        public ulong Seed { get; private set; }
        public ulong MotionKey { get; private set; }

        /// <summary>Walk and run speeds in pixels per second.</summary>
        public double WalkSpeed { get; private set; }
        public double RunSpeed { get; private set; }
        /// <summary>Time scale for timing (heavier and bigger creatures move slower).</summary>
        public double TimeScale { get; private set; }
        public double BreathRate { get; private set; }
        public double BlinkInterval { get; private set; }
        public double FidgetInterval { get; private set; }

        public static MotionTraits FromGenome(CreatureGenome g, CreatureAnatomy anatomy)
        {
            var t = new MotionTraits
            {
                Energy = g.GetOr("motion.energy", 0.5),
                Weight = g.GetOr("motion.weight", 0.5),
                Stiffness = g.GetOr("motion.stiffness", 0.5),
                Bounce = g.GetOr("motion.bounce", 0.5),
                Aggression = g.GetOr("motion.aggression", 0.5),
                Curiosity = g.GetOr("motion.curiosity", 0.5),
                SpeedFactor = DMath.Lerp(0.8, 1.2, g.GetOr("motion.speed", 0.5)),
                Seed = g.MotionSeed,
                MotionKey = g.MotionKey,
            };
            var m = anatomy.Metrics;
            double sizeRef = Math.Max(6.0, m.LegLength > 0 ? m.LegLength : m.BackHeight * 0.6);
            t.WalkSpeed = m.WalkSpeed > 0 ? m.WalkSpeed : Math.Sqrt(0.3 * 400 * sizeRef);
            t.RunSpeed = m.RunSpeed > 0 ? m.RunSpeed : Math.Sqrt(2.2 * 400 * sizeRef);
            // bigger creatures: slower timing (sqrt scaling like pendulums), energy speeds up
            double sizeTime = Math.Sqrt(Math.Max(0.35, anatomy.Metrics.BodyLength / 48.0));
            t.TimeScale = DMath.Clamp(sizeTime * DMath.Lerp(1.12, 0.88, t.Energy) * DMath.Lerp(0.92, 1.12, t.Weight), 0.6, 1.8);
            t.BreathRate = DMath.Lerp(0.55, 1.05, t.Energy) / t.TimeScale;
            t.BlinkInterval = DMath.Lerp(4.2, 2.4, t.Energy);
            t.FidgetInterval = DMath.Lerp(6.0, 2.2, t.Curiosity);
            return t;
        }
    }
}
