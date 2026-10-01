// Procedural Pixel Creatures - floating locomotion (jellyfish, floating eyes, wisps, crawling
// tentacle beings that hover just above the ground).
//
// The body hovers at the rig's altitude with a slow bob, leans into its travel direction and banks
// in turns. Tentacles are spring chains: the module drives a travelling sway at their base, the
// chains add drag, gravity and ground contact. Pulsing floaters (jellyfish) propel themselves with
// rhythmic squash-and-stretch contractions whose rate follows the speed.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class FloatLocomotion : LocomotionModuleBase
    {
        public override string Id => "float";

        private Spring1 _alt;
        private double _hover, _bob, _pulsePhase, _pulseFreq, _tentPhase;
        private bool _pulse;
        private Smooth1 _lean;

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            _hover = rig.Altitude > 0 ? rig.Altitude : 6;
            _bob = rig.Tune("float.bob", Math.Max(0.8, ctx.Anatomy.Metrics.BodyLength * 0.03));
            _pulse = rig.Tune("float.pulse", 0) > 0.5;
            _alt.Reset(_hover);
            _pulsePhase = ctx.Hash(61, 0) * DMath.TwoPi;
            _tentPhase = ctx.Hash(62, 0) * DMath.TwoPi;
            _lean = default;
        }

        public override void Step(MotorContext ctx, double dt)
        {
            double ts = ctx.Traits.TimeScale;
            double move = ctx.MoveWeight;
            double bobHz = 0.38 / ts, tentHz = DMath.Lerp(0.3, 0.75, move) / ts;
            _pulseFreq = DMath.Lerp(0.55, 1.5, move) / ts;
            if (ctx.ExportMode)
            {
                double loop = Math.Max(0.5, ctx.ExportLoop);
                bobHz = 1.0 / loop;
                tentHz = Math.Max(1, Math.Round(tentHz * loop)) / loop;
                _pulseFreq = Math.Max(1, Math.Round(_pulseFreq * loop)) / loop;
            }
            if (!double.IsNaN(ctx.CycleFrequencyOverride)) _pulseFreq = ctx.CycleFrequencyOverride;
            _pulsePhase = DMath.WrapAngle(_pulsePhase + DMath.TwoPi * _pulseFreq * dt);
            _tentPhase = DMath.WrapAngle(_tentPhase + DMath.TwoPi * tentHz * dt);
            double target = _hover + DMath.Sin(DMath.TwoPi * bobHz * ctx.Time) * _bob;
            if (_pulse) target += DMath.Sin(_pulsePhase - 0.8) * _bob * 0.6;
            _alt.Step(target, 0.9 / ts, 0.75, dt);
            _lean.Step(DMath.Saturate(ctx.Speed / Math.Max(1.0, ctx.Traits.RunSpeed)), 0.25, dt);
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double move = ctx.MoveWeight;
            p.BodyPitch += -0.22 * _lean.X;
            p.BodyRoll += DMath.Clamp(-ctx.YawRate * ctx.Speed * 0.003, -0.35, 0.35);
            p.TentaclePhase = _tentPhase;
            p.TentacleAmp = DMath.Lerp(0.55, 0.95, move) * (1 - ctx.RestWeight * 0.6);
            if (_pulse)
            {
                double s = DMath.Sin(_pulsePhase);
                // quick contraction, slow relaxation
                double c = s > 0 ? s * s : s * 0.35;
                p.Squash += -0.16 * c * DMath.Lerp(0.6, 1.0, move);
                p.TentacleAmp += 0.3 * Math.Max(0, s);
            }
            p.Breath += DMath.Sin(_tentPhase * 0.5) * 0.3;
        }

        public override double Altitude(MotorContext ctx) => _alt.X;

        public override double CycleFrequency(MotorContext ctx) => _pulse && ctx.MoveWeight > 0.5 ? _pulseFreq : 0;
    }
}
