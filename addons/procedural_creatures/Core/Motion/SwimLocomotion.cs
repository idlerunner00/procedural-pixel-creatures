// Procedural Pixel Creatures - swimming (fish, sharks, rays, eels, aquatic beasts).
//
// Three body styles: carangiform (stiff front, the tail beats; most fish), anguilliform (the whole
// body undulates; eels) and rajiform (large pectoral wings ripple; rays). The beat frequency grows
// with speed relative to body length; while idle the creature hovers with sculling fins, a slow
// tail sway and a gentle bob. Resting lowers it to the bottom. The tail spring chain supplies the
// travelling-wave follow-through, so the module only drives the base of each wave.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class SwimLocomotion : LocomotionModuleBase
    {
        public override string Id => "swim";

        public const int Carangiform = 0, Rajiform = 1, Anguilliform = 2;

        private double _phase, _freq, _finPhase, _depth, _bob;
        private int _style;
        private Spring1 _alt;
        private Smooth1 _beatAmp;

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            var m = ctx.Anatomy.Metrics;
            _depth = rig.Altitude > 0 ? rig.Altitude : Math.Max(4, m.BackHeight * 0.8);
            _style = (int)Math.Round(rig.Tune("swim.style", Carangiform));
            _bob = rig.Tune("swim.bob", Math.Max(0.6, m.BodyLength * 0.02));
            _alt.Reset(_depth);
            _phase = ctx.Hash(31, 0) * DMath.TwoPi;
            _finPhase = ctx.Hash(32, 0) * DMath.TwoPi;
            _beatAmp = default;
        }

        public override void Step(MotorContext ctx, double dt)
        {
            var traits = ctx.Traits;
            double ts = traits.TimeScale;
            double len = Math.Max(8, ctx.Anatomy.Metrics.BodyLength);
            double moving = ctx.MoveWeight;
            double swimHz = DMath.Clamp(ctx.Speed / (len * 0.75), 0.8, 3.4) / ts;
            double idleHz = (_style == Rajiform ? 0.45 : 0.6) / ts;
            _freq = DMath.Lerp(idleHz, swimHz, moving);
            if (!double.IsNaN(ctx.CycleFrequencyOverride)) _freq = ctx.CycleFrequencyOverride;
            _phase = DMath.WrapAngle(_phase + DMath.TwoPi * _freq * dt);
            double finHz = DMath.Lerp(1.15, 0.75, moving) / ts;
            if (ctx.ExportMode)
            {
                double loop = Math.Max(0.5, ctx.ExportLoop);
                finHz = Math.Max(1, Math.Round(finHz * loop)) / loop;
                if (double.IsNaN(ctx.CycleFrequencyOverride) && moving < 0.5)
                {
                    _freq = Math.Max(1, Math.Round(_freq * loop)) / loop;
                }
            }
            _finPhase = DMath.WrapAngle(_finPhase + DMath.TwoPi * finHz * dt);
            _beatAmp.Step(DMath.Lerp(0.3, 1.0, moving), 0.2, dt);

            double bobHz = 0.45 / ts;
            if (ctx.ExportMode) bobHz = 1.0 / Math.Max(0.5, ctx.ExportLoop);
            double target = _depth + DMath.Sin(DMath.TwoPi * bobHz * ctx.Time) * _bob;
            _alt.Step(target, 0.8 / ts, 0.85, dt);
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double rest = ctx.RestWeight;
            double w = ctx.MoveWeight * (1 - rest);
            double idle = 1 - w;
            double stiff = ctx.Traits.Stiffness;
            double amp = _beatAmp.X * DMath.Lerp(0.34, 0.22, stiff) * (1 - rest * 0.75);
            double s = DMath.Sin(_phase);
            switch (_style)
            {
                case Rajiform:
                    p.FinPhase = _phase;
                    p.FinAmp = DMath.Lerp(0.45, 1.0, w) * (1 - rest * 0.8);
                    p.TailYaw += DMath.Sin(_phase - 1.0) * 0.25 * amp;
                    p.BodyOffset = p.BodyOffset + new Vec3(0, -DMath.Cos(_phase) * 0.5 * w, 0);
                    break;
                case Anguilliform:
                    p.SpineYaw += s * amp * 1.1;
                    p.TailYaw += DMath.Sin(_phase - 1.3) * amp * 2.6;
                    p.HeadYaw += -s * amp * 0.45;
                    p.FinPhase = _finPhase;
                    p.FinAmp = DMath.Lerp(0.9, 0.4, w);
                    break;
                default:
                    p.SpineYaw += s * amp * 0.42;
                    p.TailYaw += DMath.Sin(_phase - 1.15) * amp * 2.1;
                    p.HeadYaw += -s * amp * 0.28;
                    p.FinPhase = _finPhase;
                    p.FinAmp = DMath.Lerp(1.0, 0.4, w) * (1 - rest * 0.6);
                    break;
            }
            // bank into turns, nose slightly up while hovering
            p.BodyRoll += DMath.Clamp(-ctx.YawRate * ctx.Speed * 0.003, -0.4, 0.4);
            p.BodyPitch += 0.035 * idle - DMath.Clamp(ctx.WorldDirToCreature(ctx.Acceleration).X * 0.0008, -0.08, 0.08) * w;
            // gill breathing / gulping while hovering
            p.Jaw += (0.5 + 0.5 * DMath.Sin(ctx.Time * DMath.TwoPi * 0.55)) * 0.14 * idle * (1 - rest);
        }

        public override double Altitude(MotorContext ctx) => _alt.X;

        public override double CycleFrequency(MotorContext ctx) => ctx.MoveWeight > 0.5 ? _freq : 0;
    }
}
