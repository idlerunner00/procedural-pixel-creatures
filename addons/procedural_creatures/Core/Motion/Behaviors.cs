// Procedural Pixel Creatures - idle life (breathing, blinking, fidgets), looking, actions, hits, rest.
// All schedules are derived from the motion seed and the simulation clock: deterministic.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public enum FidgetKind
    {
        None,
        LookAround,
        EarFlick,
        TailSwish,
        Sniff,
        WeightShift,
        Yawn,
        Shake,
    }

    /// <summary>Breathing, blinking, weight shifts and small scheduled gestures.</summary>
    public sealed class IdleLife
    {
        private double _nextBlink;
        private int _blinkIndex;
        private double _blinkStart = -10;
        private bool _doubleBlink;
        private double _nextFidget;
        private int _fidgetIndex;
        private double _fidgetStart = -10, _fidgetDur;
        private double _fidgetSign = 1;
        public FidgetKind Fidget { get; private set; }
        private readonly bool _hasEars, _hasTail, _hasHead, _hasJaw;

        public IdleLife(MotorContext ctx)
        {
            _hasTail = ctx.Rig.Tail != null;
            _hasHead = ctx.Rig.Head >= 0;
            _hasJaw = ctx.Rig.Jaw >= 0;
            foreach (var a in ctx.Rig.Appendages)
                if (a.Bones.Length > 0 && ctx.Anatomy.Skeleton[a.Bones[0]].Role == BoneRole.Ear) _hasEars = true;
            _nextBlink = ctx.Traits.BlinkInterval * (0.3 + ctx.Hash(1, 0));
            _nextFidget = ctx.Traits.FidgetInterval * (0.5 + ctx.Hash(2, 0));
        }

        public void Step(MotorContext ctx, double dt)
        {
            double t = ctx.Time;
            if (ctx.ExportMode)
            {
                // loopable: one blink per loop at a fixed loop-relative time, no random gestures
                double loop = Math.Max(0.5, ctx.ExportLoop);
                double k = Math.Floor(t / loop);
                _blinkStart = k * loop + loop * 0.62;
                _doubleBlink = false;
                Fidget = FidgetKind.None;
                return;
            }
            if (t >= _nextBlink)
            {
                _blinkStart = t;
                _blinkIndex++;
                _doubleBlink = ctx.Hash(3, _blinkIndex) < 0.18;
                _nextBlink = t + ctx.Traits.BlinkInterval * (0.55 + 0.9 * ctx.Hash(1, _blinkIndex));
            }
            if (t >= _nextFidget)
            {
                _fidgetIndex++;
                _fidgetStart = t;
                Fidget = PickFidget(ctx, _fidgetIndex);
                _fidgetSign = ctx.Hash(5, _fidgetIndex) < 0.5 ? -1 : 1;
                _fidgetDur = Fidget switch
                {
                    FidgetKind.LookAround => DMath.Lerp(1.2, 2.2, ctx.Hash(6, _fidgetIndex)),
                    FidgetKind.EarFlick => 0.35,
                    FidgetKind.TailSwish => 0.9,
                    FidgetKind.Sniff => 1.0,
                    FidgetKind.WeightShift => 2.2,
                    FidgetKind.Yawn => 1.8,
                    FidgetKind.Shake => 0.8,
                    _ => 0,
                } * ctx.Traits.TimeScale;
                _nextFidget = t + _fidgetDur + ctx.Traits.FidgetInterval * (0.5 + ctx.Hash(2, _fidgetIndex));
            }
        }

        private FidgetKind PickFidget(MotorContext ctx, int k)
        {
            double cur = ctx.Traits.Curiosity, en = ctx.Traits.Energy;
            Span<double> w = stackalloc double[8];
            w[(int)FidgetKind.LookAround] = _hasHead ? 1.0 + 1.5 * cur : 0;
            w[(int)FidgetKind.EarFlick] = _hasEars ? 1.0 + en : 0;
            w[(int)FidgetKind.TailSwish] = _hasTail ? 1.0 + en : 0;
            w[(int)FidgetKind.Sniff] = _hasHead ? 0.6 + cur : 0;
            w[(int)FidgetKind.WeightShift] = 0.8;
            w[(int)FidgetKind.Yawn] = _hasJaw ? 0.25 + (1 - en) * 0.5 : 0;
            w[(int)FidgetKind.Shake] = 0.15 + en * 0.25;
            double total = 0;
            for (int i = 1; i < w.Length; i++) total += w[i];
            double r = ctx.Hash(4, k) * total;
            for (int i = 1; i < w.Length; i++)
            {
                r -= w[i];
                if (r < 0) return (FidgetKind)i;
            }
            return FidgetKind.WeightShift;
        }

        /// <summary>Breathing and blink apply always; fidgets are weighted by 'idle' (1 = standing still).</summary>
        public void Evaluate(MotorContext ctx, ref PoseParams p, double idle)
        {
            double t = ctx.Time;
            double rest = ctx.RestWeight;
            double rate = ctx.Traits.BreathRate * DMath.Lerp(1.0, 0.55, rest) * DMath.Lerp(1.0, 1.8, ctx.RunBlend * ctx.MoveWeight);
            double swayRate = 0.7 / DMath.TwoPi;
            if (ctx.ExportMode)
            {
                // integer number of breaths per loop, sway once per loop -> seamless idle loops
                double loop = Math.Max(0.5, ctx.ExportLoop);
                double breaths = Math.Max(1, Math.Round(rate * loop));
                rate = breaths / loop;
                swayRate = 1.0 / loop;
            }
            double breath = DMath.Sin(DMath.TwoPi * t * rate);
            p.Breath += breath * DMath.Lerp(0.8, 1.2, rest);
            // slow sway of tentacles and antennae (modules may replace phase and amplitude)
            double swayHz = 0.3 / ctx.Traits.TimeScale;
            if (ctx.ExportMode)
            {
                double loop = Math.Max(0.5, ctx.ExportLoop);
                swayHz = Math.Max(1, Math.Round(swayHz * loop)) / loop;
            }
            p.TentaclePhase = DMath.TwoPi * swayHz * t + ctx.Hash(11, 0) * 6.0;
            p.TentacleAmp = DMath.Lerp(0.35, 0.15, rest);
            p.EarPitch += breath * 0.05;
            // slow weight sway while standing
            p.BodyRoll += DMath.Sin(DMath.TwoPi * t * swayRate + (ctx.ExportMode ? 0 : ctx.Hash(9, 0) * 6)) * 0.012 * idle;

            // resting mouth opening (maws, anglerfish) that closes while sleeping
            double idleJaw = ctx.Rig.Tune("idle.jaw", 0);
            if (idleJaw > 0) p.Jaw = Math.Max(p.Jaw, idleJaw * (1 - rest));

            // blink
            double bt = t - _blinkStart;
            double blink = BlinkCurve(bt);
            if (_doubleBlink) blink = Math.Min(blink, BlinkCurve(bt - 0.24));
            p.EyeOpen *= blink;

            if (idle <= 1e-3 || Fidget == FidgetKind.None) return;
            double ft = t - _fidgetStart;
            if (ft < 0 || ft > _fidgetDur) return;
            double u = ft / Math.Max(1e-6, _fidgetDur);
            double s = _fidgetSign;
            switch (Fidget)
            {
                case FidgetKind.LookAround:
                {
                    double e = DMath.Pulse(u, 0.0, 0.2, 0.75, 1.0);
                    p.HeadYaw += s * 0.55 * e * idle;
                    p.NeckYaw += s * 0.3 * e * idle;
                    p.LookX += s * e * idle;
                    p.EarPitch += 0.3 * e * idle;
                    break;
                }
                case FidgetKind.EarFlick:
                    p.EarPitch -= DMath.Pulse(u, 0.0, 0.15, 0.3, 0.6) * 1.2 * idle;
                    break;
                case FidgetKind.TailSwish:
                    p.TailYaw += DMath.Sin(u * DMath.TwoPi * 1.5) * (1 - u) * 0.9 * s * idle;
                    break;
                case FidgetKind.Sniff:
                {
                    double e = DMath.Pulse(u, 0.0, 0.25, 0.75, 1.0);
                    p.NeckPitch -= 0.35 * e * idle;
                    p.HeadPitch -= 0.25 * e * idle;
                    p.Jaw += (DMath.Sin(u * DMath.TwoPi * 7) * 0.5 + 0.5) * 0.08 * e * idle;
                    break;
                }
                case FidgetKind.WeightShift:
                {
                    double e = DMath.Pulse(u, 0.0, 0.3, 0.7, 1.0);
                    p.BodyRoll += s * 0.05 * e * idle;
                    p.BodyOffset = p.BodyOffset + new Vec3(0, 0, s * 0.6 * e * idle);
                    break;
                }
                case FidgetKind.Yawn:
                {
                    double e = DMath.Pulse(u, 0.0, 0.35, 0.65, 1.0);
                    p.Jaw += 0.95 * e * idle;
                    p.NeckPitch += 0.25 * e * idle;
                    p.HeadPitch += 0.3 * e * idle;
                    p.EyeOpen *= 1 - 0.85 * e;
                    p.EarPitch -= 0.5 * e * idle;
                    break;
                }
                case FidgetKind.Shake:
                {
                    double e = DMath.Pulse(u, 0.0, 0.1, 0.8, 1.0);
                    p.BodyRoll += DMath.Sin(u * DMath.TwoPi * 5) * 0.09 * e * idle;
                    p.HeadRoll += DMath.Sin(u * DMath.TwoPi * 5 + 0.8) * 0.18 * e * idle;
                    p.EarPitch += DMath.Sin(u * DMath.TwoPi * 5 + 1.6) * 0.6 * e * idle;
                    p.EyeOpen *= 1 - 0.6 * e;
                    break;
                }
            }
        }

        private static double BlinkCurve(double bt)
        {
            if (bt < 0 || bt > 0.2) return 1;
            if (bt < 0.06) return 1 - bt / 0.06;
            if (bt < 0.1) return 0;
            return (bt - 0.1) / 0.1;
        }
    }

    /// <summary>One-shot expressive actions built from anticipation, strike, hold and recovery.</summary>
    public sealed class ActionPlayer
    {
        public bool Active { get; private set; }
        public double Time { get; private set; }
        public double Duration { get; private set; }
        public ActionStyle Style { get; private set; }
        public double Envelope { get; private set; }
        private int _count;

        public void Trigger(MotorContext ctx, ActionStyle style)
        {
            Style = style;
            Active = true;
            Time = 0;
            _count++;
            double ts = ctx.Traits.TimeScale * DMath.Lerp(1.1, 0.85, ctx.Traits.Aggression);
            Duration = style switch
            {
                ActionStyle.Roar => 1.7,
                ActionStyle.Slam => 1.15,
                ActionStyle.Pounce => 1.25,
                ActionStyle.Strike => 1.0,
                ActionStyle.PincerSnap => 0.9,
                ActionStyle.Sting => 1.0,
                ActionStyle.WingBuffet => 1.1,
                ActionStyle.TentacleLash => 1.1,
                ActionStyle.TailSwipe => 1.0,
                ActionStyle.Spray => 1.3,
                ActionStyle.Rear => 1.25,
                _ => 0.9,
            } * ts;
        }

        public void Step(double dt)
        {
            if (!Active) { Envelope = 0; return; }
            Time += dt;
            double u = Time / Duration;
            Envelope = DMath.Pulse(u, 0.0, 0.12, 0.8, 1.0);
            if (Time >= Duration) { Active = false; Envelope = 0; }
        }

        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            if (!Active) return;
            double u = DMath.Saturate(Time / Duration);
            double L = Math.Max(4, ctx.Anatomy.Metrics.BodyLength);
            double leg = Math.Max(3, ctx.Anatomy.Metrics.LegLength);
            double agg = DMath.Lerp(0.75, 1.25, ctx.Traits.Aggression);
            Vec3 offsetBefore = p.BodyOffset;
            EvaluateStyle(ctx, ref p, u, L, leg, agg);
            // lunges scale with the body length, which includes long tails: keep the horizontal body
            // displacement of an action inside the animation margin of the canvas
            double margin = Math.Max(2.0, ctx.Anatomy.Canvas.Width * 0.5 - 2 - ctx.Anatomy.Metrics.HorizontalRadius);
            Vec3 d = p.BodyOffset - offsetBefore;
            double limit = margin * 0.45;
            if (Math.Abs(d.X) > limit || Math.Abs(d.Z) > limit)
                p.BodyOffset = offsetBefore + new Vec3(DMath.Clamp(d.X, -limit, limit), d.Y, DMath.Clamp(d.Z, -limit, limit));
        }

        private void EvaluateStyle(MotorContext ctx, ref PoseParams p, double u, double L, double leg, double agg)
        {
            switch (Style)
            {
                case ActionStyle.Bite:
                {
                    double k = Curves.Strike(u, 0.32, 0.44, 0.56, 1.0, 0.55);
                    if (ctx.Rig.Locomotion == LocomotionKind.Swim)
                    {
                        // fish strike: S-curve wind-up, then a burst forward driven by hard tail beats
                        double wind = Curves.Window(u, 0.0, 0.26, 0.32, 0.42);
                        double burst = Curves.Window(u, 0.34, 0.42, 0.55, 0.8);
                        p.SpineYaw += 0.5 * wind;
                        p.TailYaw += -1.1 * wind + Curves.Shake(u, 0.36, 0.78, 7) * 1.4 * burst;
                        p.FinAmp += 1.2 * burst;
                        p.FinPhase += u * 18.0;
                    }
                    p.BodyOffset = p.BodyOffset + new Vec3(k * 0.07 * L * agg, -Math.Max(0, -k) * 0.04 * leg, 0);
                    p.BodyPitch += -k * 0.07;
                    p.Crouch += 0.22 * Curves.Window(u, 0.02, 0.28, 0.36, 0.6);
                    p.NeckPitch += -k * 0.32 + 0.2 * Curves.Window(u, 0.0, 0.25, 0.3, 0.42);
                    p.HeadPitch += -k * 0.2;
                    double jaw = Curves.Window(u, 0.18, 0.36, 0.41, 0.46) + 0.25 * Curves.Window(u, 0.5, 0.56, 0.6, 0.7);
                    p.Jaw = Math.Max(p.Jaw, jaw);
                    p.EarPitch -= 0.9 * Curves.Window(u, 0.05, 0.2, 0.7, 0.95);
                    p.TailPitch += 0.25 * Curves.Window(u, 0.1, 0.35, 0.6, 0.95);
                    p.HeadRoll += Curves.Shake(u, 0.46, 0.66, 9) * 0.15;
                    p.FrontReach += 0.12 * Curves.Window(u, 0.36, 0.44, 0.55, 0.8);
                    if (u > 0.08 && u < 0.85) p.Mood = EyeMood.Angry;
                    p.ArmReach += k * 0.9;
                    p.ArmRaise += 0.4 * Curves.Window(u, 0.05, 0.3, 0.4, 0.55);
                    break;
                }
                case ActionStyle.Rear:
                {
                    // threat display: short crouch, rear up with lifted front legs and open fangs,
                    // hold, then snap forward and settle
                    double antic = Curves.Window(u, 0.0, 0.08, 0.12, 0.22);
                    double up = Curves.Window(u, 0.12, 0.3, 0.58, 0.72);
                    double snap = Curves.Window(u, 0.6, 0.67, 0.72, 0.9);
                    p.Crouch += 0.2 * antic + 0.1 * snap;
                    p.BodyPitch += 0.32 * up * agg - 0.1 * snap;
                    p.BodyOffset = p.BodyOffset + new Vec3(-0.05 * L * up + 0.08 * L * snap * agg, 0.1 * leg * up, 0);
                    p.FrontLift += 0.95 * up;
                    p.FrontReach += 0.18 * snap;
                    p.HeadPitch += -0.2 * up;
                    p.Jaw = Math.Max(p.Jaw, Curves.Window(u, 0.2, 0.34, 0.62, 0.74));
                    p.Grip = Math.Max(p.Grip, up);
                    p.HeadRoll += Curves.Shake(u, 0.34, 0.56, 8) * 0.06 * agg;
                    p.TailPitch += 0.2 * up;
                    if (u > 0.08 && u < 0.85) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.Roar:
                {
                    double antic = Curves.Window(u, 0.0, 0.16, 0.2, 0.3);
                    double roar = Curves.Window(u, 0.22, 0.34, 0.72, 0.9);
                    p.Crouch += 0.18 * antic;
                    p.NeckPitch += -0.3 * antic + 0.55 * roar;
                    p.HeadPitch += -0.2 * antic + 0.38 * roar;
                    p.Jaw = Math.Max(p.Jaw, 1.05 * roar);
                    p.BodyOffset = p.BodyOffset + new Vec3(-0.03 * L * roar, 0.07 * leg * roar, 0);
                    p.BodyPitch += 0.08 * roar;
                    p.HeadRoll += Curves.Shake(u, 0.3, 0.8, 11) * 0.07 * agg;
                    p.HeadYaw += Curves.Shake(u, 0.3, 0.8, 7) * 0.05 * agg;
                    p.EarPitch -= 1.0 * roar;
                    p.TailPitch += 0.45 * roar;
                    p.TailCurl += 0.3 * roar;
                    p.ArmRaise += 0.9 * roar;
                    p.ArmSide = 0;
                    p.WingFold -= 0.8 * roar;
                    p.Squash -= 0.06 * roar;
                    if (u > 0.2 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.Slam:
                {
                    // rear back with lowered head, then charge and butt
                    double k = Curves.Strike(u, 0.36, 0.5, 0.58, 1.0, 0.7);
                    p.BodyOffset = p.BodyOffset + new Vec3(k * 0.1 * L * agg, 0, 0);
                    p.BodyPitch += 0.12 * Curves.Window(u, 0.05, 0.3, 0.36, 0.45) - 0.1 * Curves.Window(u, 0.42, 0.5, 0.56, 0.75);
                    p.Crouch += 0.2 * Curves.Window(u, 0.0, 0.3, 0.4, 0.6);
                    p.NeckPitch += -0.45 * Curves.Window(u, 0.08, 0.32, 0.6, 0.9);
                    p.HeadPitch += -0.35 * Curves.Window(u, 0.1, 0.34, 0.6, 0.9);
                    p.Squash += 0.12 * Curves.Window(u, 0.5, 0.52, 0.56, 0.66);
                    p.HeadRoll += Curves.Shake(u, 0.5, 0.7, 10) * 0.1;
                    p.EarPitch -= 0.8 * Curves.Window(u, 0.1, 0.3, 0.7, 0.9);
                    p.TailPitch += 0.3 * Curves.Window(u, 0.2, 0.45, 0.6, 0.9);
                    p.FrontReach -= 0.1 * Curves.Window(u, 0.05, 0.3, 0.36, 0.45);
                    p.ArmRaise += 1.1 * Curves.Window(u, 0.08, 0.34, 0.44, 0.52);
                    p.ArmReach += 0.9 * Curves.Window(u, 0.44, 0.52, 0.6, 0.8);
                    if (u > 0.1 && u < 0.85) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.Pounce:
                {
                    double crouch = Curves.Window(u, 0.0, 0.3, 0.36, 0.42);
                    double air = Curves.Window(u, 0.4, 0.45, 0.62, 0.7);
                    double hop = u > 0.4 && u < 0.7 ? DMath.Sin(DMath.Pi * (u - 0.4) / 0.3) : 0;
                    p.Crouch += 0.45 * crouch + 0.25 * Curves.Window(u, 0.68, 0.72, 0.76, 0.95);
                    p.Altitude += hop * leg * 0.55;
                    p.BodyOffset = p.BodyOffset + new Vec3(hop * 0.12 * L, 0, 0);
                    p.BodyPitch += 0.1 * air - 0.08 * Curves.Window(u, 0.6, 0.66, 0.7, 0.8);
                    p.Squash += 0.22 * crouch - 0.2 * air + 0.25 * Curves.Window(u, 0.68, 0.71, 0.74, 0.86);
                    p.FrontReach += 0.25 * air;
                    p.TailYaw += DMath.Sin(u * 40) * 0.35 * crouch;
                    p.Jaw = Math.Max(p.Jaw, 0.6 * air);
                    if (u > 0.05 && u < 0.85) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.PincerSnap:
                {
                    double raise = Curves.Window(u, 0.0, 0.2, 0.75, 0.95);
                    p.ArmRaise += 0.95 * raise;
                    p.ArmSide = 0;
                    p.ArmReach += 0.55 * Curves.Window(u, 0.28, 0.36, 0.6, 0.82);
                    // open wide, snap shut twice
                    double grip = Curves.Window(u, 0.04, 0.2, 0.3, 0.36) + Curves.Window(u, 0.42, 0.49, 0.53, 0.58);
                    p.Grip = Math.Max(p.Grip, grip);
                    p.BodyPitch += 0.09 * raise;
                    p.Crouch += 0.18 * Curves.Window(u, 0.0, 0.15, 0.25, 0.4);
                    p.BodyOffset = p.BodyOffset + new Vec3(0.05 * L * Curves.Window(u, 0.3, 0.36, 0.6, 0.82) * agg, 0, 0);
                    p.HeadRoll += Curves.Shake(u, 0.3, 0.62, 12) * 0.05;
                    p.Jaw = Math.Max(p.Jaw, 0.6 * Curves.Window(u, 0.3, 0.38, 0.55, 0.7));
                    if (u > 0.05 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.Sting:
                {
                    double k = Curves.Strike(u, 0.36, 0.47, 0.55, 1.0, 0.6);
                    p.TailPitch += -k * 0.85;
                    p.TailCurl += -k * 0.5;
                    p.BodyPitch += -0.07 * k;
                    p.Crouch += 0.22 * Curves.Window(u, 0.02, 0.3, 0.4, 0.6);
                    p.ArmRaise += 0.55 * Curves.Window(u, 0.05, 0.25, 0.7, 0.9);
                    p.Grip = Math.Max(p.Grip, 0.7 * Curves.Window(u, 0.05, 0.2, 0.6, 0.8));
                    p.BodyOffset = p.BodyOffset + new Vec3(0.04 * L * k * agg, 0, 0);
                    if (u > 0.05 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.WingBuffet:
                {
                    double open = Curves.Window(u, 0.0, 0.18, 0.8, 1.0);
                    p.WingFold = Math.Min(p.WingFold, 1 - open);
                    double beatT = DMath.Saturate((u - 0.18) / 0.6);
                    double beats = u > 0.18 && u < 0.78 ? DMath.Sin(beatT * DMath.TwoPi * 2.5 - DMath.HalfPi) : -1;
                    p.WingFlap = DMath.Lerp(p.WingFlap, beats, open);
                    p.WingTwist += DMath.Cos(beatT * DMath.TwoPi * 2.5) * open;
                    p.BodyPitch += 0.2 * open;
                    p.BodyOffset = p.BodyOffset + new Vec3(-0.03 * L * open, 0.06 * leg * open, 0);
                    p.Altitude += Math.Max(0, beats) * 0.1 * leg * open;
                    p.Jaw = Math.Max(p.Jaw, 0.9 * Curves.Window(u, 0.22, 0.32, 0.65, 0.8));
                    p.NeckPitch += 0.3 * open;
                    p.HeadPitch += 0.2 * open;
                    p.TailPitch += 0.3 * open;
                    p.EarPitch -= 0.8 * open;
                    p.ArmRaise += 0.6 * open;
                    if (u > 0.1 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.TentacleLash:
                {
                    double k = Curves.Strike(u, 0.3, 0.42, 0.5, 1.0, 0.7);
                    // rise and spread the tentacles, then whip them forward
                    double rise = Curves.Window(u, 0.04, 0.28, 0.4, 0.62);
                    p.TentacleAmp += 2.4 * Curves.Window(u, 0.08, 0.3, 0.6, 0.95);
                    p.TentaclePhase += k * 3.2;
                    p.Altitude += 0.1 * L * rise;
                    p.BodyPitch += -0.2 * k + 0.12 * rise;
                    p.Squash += 0.16 * Math.Max(0, -k) - 0.1 * Math.Max(0, k);
                    p.BodyOffset = p.BodyOffset + new Vec3(0.06 * L * k * agg, 0, 0);
                    p.Jaw = Math.Max(p.Jaw, Curves.Window(u, 0.3, 0.4, 0.55, 0.7));
                    p.ArmRaise += 0.8 * Curves.Window(u, 0.05, 0.3, 0.45, 0.6);
                    p.ArmReach += k;
                    if (u > 0.05 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.TailSwipe:
                {
                    double k = Curves.Strike(u, 0.32, 0.46, 0.54, 1.0, 0.6);
                    p.BodyYaw += 0.22 * k;
                    p.SpineYaw += 0.45 * k;
                    p.TailYaw += -2.1 * k;
                    p.NeckYaw += -0.3 * k;
                    p.HeadYaw += -0.2 * k;
                    p.Crouch += 0.16 * Curves.Window(u, 0.0, 0.2, 0.4, 0.6);
                    p.Jaw = Math.Max(p.Jaw, 0.55 * Curves.Window(u, 0.35, 0.45, 0.6, 0.75));
                    p.WingFold = Math.Min(p.WingFold, 1 - 0.5 * Curves.Window(u, 0.3, 0.4, 0.6, 0.8));
                    if (u > 0.05 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                case ActionStyle.Spray:
                {
                    double charge = Curves.Window(u, 0.0, 0.3, 0.35, 0.42);
                    double spray = Curves.Window(u, 0.4, 0.46, 0.7, 0.85);
                    p.Squash += 0.26 * charge - 0.18 * spray + Curves.Shake(u, 0.45, 0.75, 14) * 0.06;
                    p.Crouch += 0.2 * charge;
                    p.NeckPitch += -0.2 * charge + 0.25 * spray;
                    p.HeadPitch += 0.15 * spray;
                    p.Jaw = Math.Max(p.Jaw, spray);
                    p.BodyOffset = p.BodyOffset + new Vec3(-0.03 * L * charge + 0.04 * L * spray, 0, 0);
                    p.TentacleAmp += spray;
                    p.ArmRaise += 0.7 * spray;
                    p.Grip = Math.Max(p.Grip, spray);
                    if (u > 0.05 && u < 0.9) p.Mood = EyeMood.Angry;
                    break;
                }
                default:
                {
                    // generic strike: lunge forward with the front of the body (armed creatures swing their weapon arm)
                    double k = Curves.Strike(u, 0.3, 0.45, 0.55, 1.0, 0.5);
                    p.ArmSide = ctx.Rig.Tune("action.armSide", 0);
                    p.BodyYaw += 0.18 * k * p.ArmSide;
                    p.BodyOffset = p.BodyOffset + new Vec3(k * 0.08 * L, 0, 0);
                    p.NeckPitch += -k * 0.3;
                    p.HeadPitch += -k * 0.2;
                    p.Jaw = Math.Max(p.Jaw, Curves.Window(u, 0.2, 0.38, 0.45, 0.55));
                    p.ArmRaise += 1.0 * Curves.Window(u, 0.05, 0.3, 0.35, 0.45);
                    p.ArmReach += k;
                    if (u > 0.1 && u < 0.85) p.Mood = EyeMood.Angry;
                    break;
                }
            }
        }
    }

    /// <summary>Additive, spring based hit reaction with flash and pain expression.</summary>
    public sealed class HitReaction
    {
        private Spring1 _push, _pitch, _roll, _head, _squash;
        private double _flash;
        private double _painUntil = -1;
        private Vec3 _dir = Vec3.UnitX;
        public double Flash => _flash;
        public bool Active => Math.Abs(_push.X) + Math.Abs(_push.V) + Math.Abs(_head.X) + Math.Abs(_head.V) > 1e-3 || _flash > 0;

        public void Trigger(MotorContext ctx, Vec3 fromWorld, double strength)
        {
            // direction the hit pushes the creature, in creature space
            Vec3 push = ctx.WorldDirToCreature(new Vec3(-fromWorld.X, 0, -fromWorld.Z)).NormalizedOr(new Vec3(-1, 0, 0));
            _dir = push;
            double m = 1.0 / Math.Sqrt(Math.Max(0.3, ctx.Anatomy.Metrics.Mass));
            double s = DMath.Clamp(strength, 0.1, 2.0) * DMath.Clamp(m, 0.5, 1.6);
            _push.V += 55 * s;
            _pitch.V += (push.X < 0 ? 2.8 : -2.8) * s;
            _roll.V += push.Z * 3.0 * s;
            _head.V += 5.5 * s;
            _squash.V += 2.6 * s;
            _flash = 1;
            _painUntil = ctx.Time + 0.42 * ctx.Traits.TimeScale;
        }

        public void Step(MotorContext ctx, double dt)
        {
            double f = 3.2 / ctx.Traits.TimeScale;
            _push.Step(0, f, 0.42, dt);
            _pitch.Step(0, f * 1.2, 0.38, dt);
            _roll.Step(0, f * 1.2, 0.38, dt);
            _head.Step(0, f * 1.4, 0.32, dt);
            _squash.Step(0, f * 1.8, 0.3, dt);
            _flash = Math.Max(0, _flash - dt / 0.11);
        }

        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            p.BodyOffset = p.BodyOffset + _dir * _push.X;
            p.BodyPitch += _pitch.X * 0.1;
            p.BodyRoll += _roll.X * 0.1;
            p.NeckPitch += _head.X * 0.12;
            p.HeadPitch += _head.X * 0.1;
            p.Squash += _squash.X * 0.08;
            p.EarPitch -= Math.Abs(_head.X) * 0.8;
            p.TailPitch += _head.X * 0.1;
            p.WingFlap += _head.X * 0.2;
            if (ctx.Time < _painUntil)
            {
                p.Mood = EyeMood.Pain;
                p.Jaw = Math.Max(p.Jaw, 0.35);
            }
        }

        public void Reset()
        {
            _push = default; _pitch = default; _roll = default; _head = default; _squash = default;
            _flash = 0; _painUntil = -1;
        }
    }

    /// <summary>Rest / sleep with staged enter and exit transitions.</summary>
    public sealed class RestPlayer
    {
        public bool Requested;
        public double Progress { get; private set; }
        public bool Exiting { get; private set; }
        public bool Sleeping => Progress >= 0.999 && Requested;

        public void Step(MotorContext ctx, double dt)
        {
            double ts = ctx.Traits.TimeScale;
            double enter = 1.35 * ts * DMath.Lerp(0.85, 1.25, ctx.Traits.Weight);
            double exit = 0.95 * ts;
            if (Requested)
            {
                Exiting = false;
                Progress = Math.Min(1, Progress + dt / enter);
            }
            else
            {
                if (Progress > 0) Exiting = true;
                Progress = Math.Max(0, Progress - dt / exit);
                if (Progress <= 0) Exiting = false;
            }
        }

        public void Snap(bool resting)
        {
            Requested = resting;
            Progress = resting ? 1 : 0;
            Exiting = false;
        }

        /// <summary>Blends the rest pose for the rig's rest style into p.</summary>
        public void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double w = Progress;
            if (w <= 0) return;
            var style = ctx.Rig.Rest;
            double eyes = 1 - DMath.SmoothStep(0.72, 1.0, w);
            double body = DMath.SmoothStep(0.05, 0.85, w);
            double head = Exiting ? DMath.SmoothStep(0.5, 1.0, w) : DMath.SmoothStep(0.55, 1.0, w);
            switch (style)
            {
                case RestStyle.LieDown:
                {
                    p.LegTuck = Math.Max(p.LegTuck, w);
                    p.Crouch = DMath.Lerp(p.Crouch, 1.0, body);
                    // rear drops first when lying down, the front first when getting up
                    double stage = Exiting ? DMath.Pulse(w, 0.1, 0.4, 0.5, 0.9) : DMath.Pulse(w, 0.05, 0.35, 0.45, 0.85);
                    p.BodyPitch += (Exiting ? 0.12 : 0.16) * stage;
                    p.NeckPitch += -0.35 * head;
                    p.HeadPitch += -0.3 * head;
                    p.HeadYaw += 0.22 * head;
                    p.TailPitch += -0.35 * body;
                    p.TailYaw += 1.1 * head;
                    p.TailCurl += 0.6 * head;
                    p.EarPitch -= 0.45 * head;
                    break;
                }
                case RestStyle.SitSlump:
                {
                    p.LegTuck = Math.Max(p.LegTuck, w);
                    p.Crouch = DMath.Lerp(p.Crouch, 1.0, body);
                    p.BodyPitch += -0.12 * body;
                    p.NeckPitch += -0.35 * head;
                    p.HeadPitch += -0.35 * head;
                    p.ArmRaise -= 0.2 * body;
                    break;
                }
                case RestStyle.Coil:
                {
                    p.Extra0 = Math.Max(p.Extra0, body);
                    p.NeckPitch += -0.2 * head;
                    p.HeadPitch += -0.15 * head;
                    break;
                }
                case RestStyle.Settle:
                {
                    p.LegTuck = Math.Max(p.LegTuck, w * 0.8);
                    p.Crouch = DMath.Lerp(p.Crouch, 0.85, body);
                    p.HeadPitch += -0.15 * head;
                    p.Altitude *= 1 - body * 0.7;
                    break;
                }
                case RestStyle.Perch:
                {
                    p.WingFold = Math.Max(p.WingFold, body);
                    p.LegTuck = Math.Max(p.LegTuck, w * 0.6);
                    p.Crouch = DMath.Lerp(p.Crouch, 0.6, body);
                    p.NeckPitch += -0.45 * head;
                    p.HeadPitch += -0.25 * head;
                    p.Altitude *= 1 - body;
                    break;
                }
                case RestStyle.Puddle:
                {
                    p.Squash += 0.32 * body;
                    p.Extra0 = Math.Max(p.Extra0, body);
                    break;
                }
                case RestStyle.Fold:
                {
                    p.Extra0 = Math.Max(p.Extra0, body);
                    p.LegTuck = Math.Max(p.LegTuck, w * 0.7);
                    p.Crouch = DMath.Lerp(p.Crouch, 0.7, body);
                    p.HeadPitch += -0.3 * head;
                    break;
                }
            }
            p.EyeOpen *= eyes;
            if (w > 0.97 && Requested) p.Mood = EyeMood.Sleep;
        }
    }
}
