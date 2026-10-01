// Procedural Pixel Creatures - flight (birds, bats, winged drakes, flying insects).
//
// Ground movement is delegated to the legged module. Fast movement makes the creature take off
// (anticipation crouch with opening wings, strong climbing beats), cruise with a skewed wing beat
// (fast downstroke, slower upstroke with partly folded wings) and occasional glides, and land again
// with a flare when it slows down. Hovering creatures (insects, creatures without legs) stay
// airborne and only land to rest. Everything runs on the fixed simulation clock.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class FlightLocomotion : LocomotionModuleBase
    {
        public override string Id => "flight";

        private enum FlightState { Ground, Anticipate, Air }

        private readonly LeggedLocomotion _ground = new LeggedLocomotion();
        private FlightState _state;
        private bool _hasLegs, _hover, _wantAir;
        private double _air, _timer, _flapPhase, _flapFreq, _cruise, _baseFreq;
        private Smooth1 _glide;
        private Spring1 _alt;
        private double _bobAmp = 1;
        private double _tailRestCurl;

        /// <summary>0 = on the ground, 1 = fully airborne.</summary>
        public double AirWeight => _air;
        public bool Hovering => _hover;

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            _hasLegs = rig.Legs.Count > 0;
            if (_hasLegs) _ground.Initialize(ctx);
            _hover = rig.Tune("flight.hover", 0) > 0.5 || !_hasLegs;
            var m = ctx.Anatomy.Metrics;
            _cruise = rig.Altitude > 0 ? rig.Altitude : Math.Max(8, m.BackHeight * 1.2 + 8);
            _baseFreq = rig.Tune("flight.flapHz", 3.2);
            _bobAmp = Math.Max(0.6, m.BodyLength * 0.025);
            // total rest curl of the tail; airborne tails stream straight behind the body
            _tailRestCurl = 0;
            if (rig.Tail != null)
                for (int i = 1; i < rig.Tail.RestPitch.Length; i++) _tailRestCurl += rig.Tail.RestPitch[i];
            _state = _hover ? FlightState.Air : FlightState.Ground;
            _wantAir = _hover;
            _air = _hover ? 1 : 0;
            _alt.Reset(_hover ? _cruise : 0);
            _glide = default;
            _flapPhase = 0;
            _timer = 0;
        }

        private double AnticipationTime(MotorContext ctx) => 0.24 * ctx.Traits.TimeScale;

        public override void Step(MotorContext ctx, double dt)
        {
            var traits = ctx.Traits;
            double ts = traits.TimeScale;
            double walk = traits.WalkSpeed;
            bool resting = ctx.RestWeight > 0.001 && !ctx.RestExiting;

            // ---- decide between ground and air (with hysteresis)
            if (_hover) _wantAir = !resting && ctx.RestWeight < 0.5;
            else if (resting) _wantAir = false;
            else if (!_wantAir && ctx.Speed > walk * 1.4 && ctx.RunBlend > 0.3) _wantAir = true;
            else if (_wantAir && ctx.Speed < walk * 0.75) _wantAir = false;

            switch (_state)
            {
                case FlightState.Ground:
                    if (_wantAir) { _state = FlightState.Anticipate; _timer = 0; }
                    break;
                case FlightState.Anticipate:
                    _timer += dt;
                    if (!_wantAir) _state = FlightState.Ground;
                    else if (_timer >= AnticipationTime(ctx)) _state = FlightState.Air;
                    break;
                case FlightState.Air:
                    if (!_wantAir) _state = FlightState.Ground;
                    break;
            }

            double airTarget = _state == FlightState.Air ? 1 : 0;
            double rate = dt / ((airTarget > _air ? 0.42 : 0.62) * ts);
            _air = airTarget > _air ? Math.Min(airTarget, _air + rate) : Math.Max(airTarget, _air - rate);

            // ---- altitude: critically damped towards the cruise height
            double altTarget = _cruise * DMath.SmoothStep(0, 1, _air);
            if (_hover && _air > 0.5 && !ctx.ExportMode) altTarget += DMath.Sin(ctx.Time * 1.7 / ts) * _bobAmp * 0.8;
            _alt.Step(altTarget, 1.05 / ts, 0.95, dt);
            if (_alt.X < 0) { _alt.X = 0; _alt.V = Math.Max(0, _alt.V); }

            // ---- wing beat
            double climb = DMath.Saturate(_alt.V / 25.0);
            _flapFreq = _baseFreq / ts * DMath.Lerp(1.0, 1.3, climb) * (_hover ? 1.1 : 1.0);
            double glideTarget = 0;
            if (!_hover && !ctx.ExportMode && _air > 0.95 && ctx.Speed > traits.RunSpeed * 0.6)
            {
                const double window = 3.4;
                double k = Math.Floor(ctx.Time / window);
                double u = ctx.Time / window - k;
                if (ctx.Hash(21, (int)k) < 0.55 && u > 0.45 && u < 0.85) glideTarget = 1;
            }
            _glide.Step(glideTarget, 0.18, dt);
            double beat = _flapFreq * DMath.Lerp(1.0, 0.2, _glide.X);
            if (!double.IsNaN(ctx.CycleFrequencyOverride) && _air > 0.5) beat = ctx.CycleFrequencyOverride;
            if (_air > 0.001 || _state == FlightState.Anticipate) _flapPhase = DMath.WrapAngle(_flapPhase + DMath.TwoPi * beat * dt);

            // ---- legs: walk on the ground, keep the feet under the body while airborne
            if (_hasLegs)
            {
                if (_air > 0.5) _ground.ResetPlants(ctx);
                else _ground.Step(ctx, dt);
            }
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double air = _air;
            double airS = DMath.SmoothStep(0, 1, air);
            if (_hasLegs && airS < 0.999)
            {
                var q = p;
                _ground.Evaluate(ctx, ref q);
                p = PoseParams.Lerp(q, p, airS);
            }
            double open = DMath.SmoothStep(0.0, 0.55, air);
            double antic = _state == FlightState.Anticipate ? DMath.Saturate(_timer / AnticipationTime(ctx)) : 0;

            // skewed beat: fast powerful downstroke, slower upstroke
            double ph = _flapPhase;
            double flap = DMath.Sin(ph + 0.45 * DMath.Sin(ph));
            double amp = DMath.Lerp(1.0, 0.18, _glide.X) * open;
            p.WingFold = DMath.Lerp(p.WingFold, 0.0, open) + Math.Max(0, -flap) * 0.24 * amp;
            p.WingFlap += flap * amp;
            p.WingTwist += DMath.Cos(ph) * amp;

            if (antic > 0)
            {
                // crouch, wings open and lift before the jump
                double c = DMath.Sin(DMath.Pi * DMath.Saturate(antic * 1.05));
                p.Crouch += 0.4 * c;
                p.Squash += 0.12 * c;
                p.WingFold = Math.Min(p.WingFold, 1.0 - antic * 0.85);
                p.WingFlap += -0.9 * antic;
                p.NeckPitch += -0.15 * c;
            }

            double spd = DMath.Saturate(ctx.Speed / Math.Max(1.0, ctx.Traits.RunSpeed));
            p.BodyPitch += -DMath.Lerp(0.04, 0.28, spd) * airS * (_hover ? 0.5 : 1.0);
            p.HeadPitch -= p.BodyPitch * 0.5;
            // the body rises on the downstroke
            p.BodyOffset = p.BodyOffset + new Vec3(0, flap * amp * _bobAmp, 0);
            p.LegTuck = Math.Max(p.LegTuck, DMath.SmoothStep(0.25, 0.85, air));
            // the tail trails behind: compensate the forward body pitch and straighten most of the
            // rest curl, so a curled drake tail does not stick up while it flies
            p.TailPitch += (p.BodyPitch * 0.85 - 0.08) * airS;
            p.TailCurl -= _tailRestCurl * 0.7 * airS;
            // bank into turns
            p.BodyRoll += DMath.Clamp(-ctx.YawRate * ctx.Speed * 0.004, -0.5, 0.5) * airS;

            // landing flare: pitch up with spread wings while descending
            if (_state == FlightState.Ground && air > 0.02)
            {
                double flare = DMath.Pulse(air, 0.0, 0.3, 0.7, 1.0);
                p.BodyPitch += 0.35 * flare;
                p.WingFold = Math.Min(p.WingFold, 1.0 - flare);
            }
        }

        public override void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output)
        {
            if (_hasLegs) _ground.ApplyLimbs(ctx, p, pose, output);
        }

        public override double Altitude(MotorContext ctx) => _alt.X;

        public override double CycleFrequency(MotorContext ctx)
        {
            if (_air > 0.5) return _flapFreq * DMath.Lerp(1.0, 0.2, _glide.X);
            return _hasLegs ? _ground.CycleFrequency(ctx) : 0;
        }
    }
}
