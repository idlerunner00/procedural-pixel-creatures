// Procedural Pixel Creatures - blendable high level pose parameters and small dynamics helpers.
//
// Every behaviour (idle, locomotion, rest, action, hit) expresses itself through the same set of
// parameters. Transitions therefore blend parameters (not pixels), which keeps state changes smooth
// and anatomically consistent. The RigDriver turns parameters into bone transforms.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public struct PoseParams
    {
        // body (creature space, relative to rest pose)
        public Vec3 BodyOffset;
        public double BodyPitch, BodyRoll, BodyYaw;
        /// <summary>Lateral (yaw) and sagittal (pitch) bend distributed along the spine.</summary>
        public double SpineYaw, SpinePitch;
        /// <summary>0..1 lowers the body towards the ground; legs fold accordingly.</summary>
        public double Crouch;
        /// <summary>&gt;0 squash (flatter, wider), &lt;0 stretch. Volume preserving.</summary>
        public double Squash;
        /// <summary>Breathing expansion -1..1 (chest).</summary>
        public double Breath;
        // neck & head
        public double NeckPitch, NeckYaw;
        public double HeadPitch, HeadYaw, HeadRoll;
        /// <summary>0 closed .. 1 fully open (scaled by the rig's jaw limit).</summary>
        public double Jaw;
        // tail
        public double TailPitch, TailYaw, TailCurl;
        // ears: -1 flattened back .. +1 perked forward
        public double EarPitch;
        // wings: 1 folded .. 0 spread; flap -1 (up) .. +1 (down); twist: hand lag -1..1
        public double WingFold, WingFlap, WingSweep, WingTwist;
        // fins and tentacles: oscillator phase (radians) and amplitude (0..1+)
        public double FinPhase, FinAmp, TentaclePhase, TentacleAmp;
        /// <summary>Pincers/grippers: 0 closed .. 1 open.</summary>
        public double Grip;
        /// <summary>Tongue: 0 retracted .. 1 extended.</summary>
        public double Tongue;
        /// <summary>Offset of the whole creature (root) in creature space (hops, lunges of body-less plans).</summary>
        public Vec3 RootOffset;
        // arms (bipeds, pincers): swing phase offset, raise, reach
        public double ArmSwing, ArmRaise, ArmReach;
        /// <summary>Which arm performs actions: -1 left, +1 right, 0 both.</summary>
        public double ArmSide;
        /// <summary>0..1 legs fold into the resting pose.</summary>
        public double LegTuck;
        /// <summary>Front legs reach forward (pounces, bracing) in body lengths.</summary>
        public double FrontReach;
        /// <summary>Front leg pair lifts off the ground (threat displays, rearing) 0..1.</summary>
        public double FrontLift;
        // face
        public double EyeOpen;
        public double LookX, LookY;
        /// <summary>Root height above ground (hops, flight, swimming depth offsets).</summary>
        public double Altitude;
        /// <summary>Generic channel for family specific motion (e.g. slime wobble, fin beat).</summary>
        public double Extra0, Extra1;
        public EyeMood Mood;

        public static PoseParams Neutral => new PoseParams { EyeOpen = 1, WingFold = 1 };

        public static PoseParams Lerp(in PoseParams a, in PoseParams b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            var r = new PoseParams
            {
                BodyOffset = Vec3.Lerp(a.BodyOffset, b.BodyOffset, t),
                BodyPitch = DMath.Lerp(a.BodyPitch, b.BodyPitch, t),
                BodyRoll = DMath.Lerp(a.BodyRoll, b.BodyRoll, t),
                BodyYaw = DMath.Lerp(a.BodyYaw, b.BodyYaw, t),
                SpineYaw = DMath.Lerp(a.SpineYaw, b.SpineYaw, t),
                SpinePitch = DMath.Lerp(a.SpinePitch, b.SpinePitch, t),
                Crouch = DMath.Lerp(a.Crouch, b.Crouch, t),
                Squash = DMath.Lerp(a.Squash, b.Squash, t),
                Breath = DMath.Lerp(a.Breath, b.Breath, t),
                NeckPitch = DMath.Lerp(a.NeckPitch, b.NeckPitch, t),
                NeckYaw = DMath.Lerp(a.NeckYaw, b.NeckYaw, t),
                HeadPitch = DMath.Lerp(a.HeadPitch, b.HeadPitch, t),
                HeadYaw = DMath.Lerp(a.HeadYaw, b.HeadYaw, t),
                HeadRoll = DMath.Lerp(a.HeadRoll, b.HeadRoll, t),
                Jaw = DMath.Lerp(a.Jaw, b.Jaw, t),
                TailPitch = DMath.Lerp(a.TailPitch, b.TailPitch, t),
                TailYaw = DMath.Lerp(a.TailYaw, b.TailYaw, t),
                TailCurl = DMath.Lerp(a.TailCurl, b.TailCurl, t),
                EarPitch = DMath.Lerp(a.EarPitch, b.EarPitch, t),
                WingFold = DMath.Lerp(a.WingFold, b.WingFold, t),
                WingFlap = DMath.Lerp(a.WingFlap, b.WingFlap, t),
                WingSweep = DMath.Lerp(a.WingSweep, b.WingSweep, t),
                WingTwist = DMath.Lerp(a.WingTwist, b.WingTwist, t),
                FinPhase = DMath.Lerp(a.FinPhase, b.FinPhase, t),
                FinAmp = DMath.Lerp(a.FinAmp, b.FinAmp, t),
                TentaclePhase = DMath.Lerp(a.TentaclePhase, b.TentaclePhase, t),
                TentacleAmp = DMath.Lerp(a.TentacleAmp, b.TentacleAmp, t),
                Grip = DMath.Lerp(a.Grip, b.Grip, t),
                Tongue = DMath.Lerp(a.Tongue, b.Tongue, t),
                RootOffset = Vec3.Lerp(a.RootOffset, b.RootOffset, t),
                ArmSwing = DMath.Lerp(a.ArmSwing, b.ArmSwing, t),
                ArmRaise = DMath.Lerp(a.ArmRaise, b.ArmRaise, t),
                ArmReach = DMath.Lerp(a.ArmReach, b.ArmReach, t),
                ArmSide = DMath.Lerp(a.ArmSide, b.ArmSide, t),
                LegTuck = DMath.Lerp(a.LegTuck, b.LegTuck, t),
                FrontReach = DMath.Lerp(a.FrontReach, b.FrontReach, t),
                FrontLift = DMath.Lerp(a.FrontLift, b.FrontLift, t),
                EyeOpen = DMath.Lerp(a.EyeOpen, b.EyeOpen, t),
                LookX = DMath.Lerp(a.LookX, b.LookX, t),
                LookY = DMath.Lerp(a.LookY, b.LookY, t),
                Altitude = DMath.Lerp(a.Altitude, b.Altitude, t),
                Extra0 = DMath.Lerp(a.Extra0, b.Extra0, t),
                Extra1 = DMath.Lerp(a.Extra1, b.Extra1, t),
                Mood = t < 0.5 ? a.Mood : b.Mood,
            };
            return r;
        }

        /// <summary>Adds the transform-like channels of 'd' scaled by w (used by additive layers).</summary>
        public void AddScaled(in PoseParams d, double w)
        {
            BodyOffset = BodyOffset + d.BodyOffset * w;
            BodyPitch += d.BodyPitch * w; BodyRoll += d.BodyRoll * w; BodyYaw += d.BodyYaw * w;
            SpineYaw += d.SpineYaw * w; SpinePitch += d.SpinePitch * w;
            Crouch += d.Crouch * w; Squash += d.Squash * w;
            NeckPitch += d.NeckPitch * w; NeckYaw += d.NeckYaw * w;
            HeadPitch += d.HeadPitch * w; HeadYaw += d.HeadYaw * w; HeadRoll += d.HeadRoll * w;
            Jaw += d.Jaw * w;
            TailPitch += d.TailPitch * w; TailYaw += d.TailYaw * w; TailCurl += d.TailCurl * w;
            EarPitch += d.EarPitch * w;
            WingFlap += d.WingFlap * w; WingSweep += d.WingSweep * w; WingTwist += d.WingTwist * w;
            FinAmp += d.FinAmp * w; TentacleAmp += d.TentacleAmp * w; Grip += d.Grip * w; Tongue += d.Tongue * w;
            RootOffset = RootOffset + d.RootOffset * w;
            ArmRaise += d.ArmRaise * w; ArmReach += d.ArmReach * w;
            FrontReach += d.FrontReach * w; FrontLift += d.FrontLift * w;
            Altitude += d.Altitude * w;
        }
    }

    /// <summary>Damped harmonic oscillator (semi-implicit Euler, stable for the fixed sim step).</summary>
    public struct Spring1
    {
        public double X, V;

        public void Step(double target, double frequency, double damping, double dt)
        {
            double w = DMath.TwoPi * frequency;
            double a = w * w * (target - X) - 2.0 * damping * w * V;
            V += a * dt;
            X += V * dt;
        }

        public void Reset(double x) { X = x; V = 0; }
    }

    /// <summary>Critically damped smoothing towards a target with a half-life.</summary>
    public struct Smooth1
    {
        public double X;
        public void Step(double target, double halfLife, double dt) { X += (target - X) * DMath.DampFactor(halfLife, dt); }
    }

    /// <summary>Piecewise timing helpers for procedural keyframed actions.</summary>
    public static class Curves
    {
        /// <summary>Anticipation-strike-recover envelope: 0 at t=0, -anticipation depth at a, +1 at s, holds to h, back to 0 at r.</summary>
        public static double Strike(double t, double a, double s, double h, double r, double antic)
        {
            if (t <= 0 || t >= r) return 0;
            if (t < a) return -antic * DMath.EaseInOutSine(t / a);
            if (t < s) return DMath.Lerp(-antic, 1.0, DMath.EaseInCubic((t - a) / (s - a)));
            if (t < h) return 1.0;
            return 1.0 - DMath.EaseInOutCubic((t - h) / (r - h));
        }

        /// <summary>Smooth window: ramps up in [a,b], holds, ramps down in [c,d].</summary>
        public static double Window(double t, double a, double b, double c, double d) => DMath.Pulse(t, a, b, c, d);

        /// <summary>Decaying shake (sin) for roars and impacts.</summary>
        public static double Shake(double t, double start, double end, double freq)
        {
            if (t < start || t > end) return 0;
            double k = (t - start) / (end - start);
            return DMath.Sin((t - start) * DMath.TwoPi * freq) * (1 - k) * DMath.Saturate(k * 6);
        }
    }
}
