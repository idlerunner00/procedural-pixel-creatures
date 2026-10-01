// Procedural Pixel Creatures - hopping locomotion (slimes, blobs, jumping plants).
//
// A hop is crouch (squash, anticipation) -> launch (stretch) -> ballistic flight -> landing
// (squash, jiggle) -> recover. The visible body is anchored in world space while it touches the
// ground, so it never slides: all travel happens in the air. Jelly bodies get secondary motion from
// damped springs that are excited by landings, turns and idle "blorps".

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class HopLocomotion : LocomotionModuleBase
    {
        public override string Id => "hop";

        private enum HopState { Ground, Crouch, Air, Land }

        private HopState _state;
        private double _t, _crouchDur, _airDur, _landDur;
        private Vec3 _anchor, _from, _to;
        private double _height, _hopLen, _freq, _heightNow, _landImpact, _groundLead;
        private Spring1 _jx, _jz, _jy;
        private double _nextBlorp;
        private int _blorps;

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            var m = ctx.Anatomy.Metrics;
            _hopLen = rig.Tune("hop.length", Math.Max(4, m.BodyLength * 0.6));
            _height = rig.Tune("hop.height", Math.Max(3, m.BackHeight * 0.55 + 2));
            _anchor = ctx.Position;
            _state = HopState.Ground;
            _t = 0;
            _jx = default; _jz = default; _jy = default;
            _nextBlorp = ctx.Time + 1.5 + ctx.Hash(51, 0) * 2;
            _blorps = 0;
        }

        public override void Step(MotorContext ctx, double dt)
        {
            var traits = ctx.Traits;
            double ts = traits.TimeScale;
            double run = ctx.RunBlend;
            bool moving = ctx.Speed > traits.WalkSpeed * 0.1 && ctx.RestWeight < 0.01 && ctx.ActionWeight < 0.5;
            double far = new Vec2(ctx.Position.X - _anchor.X, ctx.Position.Z - _anchor.Z).Length;
            double heightScale = DMath.Lerp(0.8, 1.25, run) * DMath.Lerp(0.85, 1.2, traits.Bounce);
            switch (_state)
            {
                case HopState.Ground:
                    if (moving || far > _hopLen * 0.3)
                    {
                        _state = HopState.Crouch;
                        _t = 0;
                        _crouchDur = DMath.Lerp(0.2, 0.12, run) * ts * DMath.Lerp(1.15, 0.85, traits.Energy);
                    }
                    else
                    {
                        // tiny leftovers ooze instead of hopping
                        _anchor = Vec3.Lerp(_anchor, ctx.Position, DMath.DampFactor(0.5, dt));
                    }
                    break;
                case HopState.Crouch:
                    _t += dt;
                    if (_t >= _crouchDur)
                    {
                        _state = HopState.Air;
                        _t = 0;
                        _airDur = DMath.Lerp(0.36, 0.3, run) * Math.Sqrt(heightScale) * ts;
                        _heightNow = _height * heightScale;
                        _from = _anchor;
                        // land half a ground phase ahead of the logical position, so that the anchored body
                        // alternates between leading and trailing it instead of always lagging behind
                        _groundLead = (0.16 * ts + _crouchDur) * 0.5;
                        _to = ctx.Position + ctx.Velocity * (_airDur + _groundLead);
                        _jy.V -= 2.2; // launch stretch
                    }
                    break;
                case HopState.Air:
                {
                    _t += dt;
                    double remaining = Math.Max(0, _airDur - _t);
                    // steer the landing spot with the current velocity (keeps the ground track smooth)
                    Vec3 predicted = ctx.Position + ctx.Velocity * (remaining + _groundLead);
                    _to = Vec3.Lerp(_to, predicted, DMath.DampFactor(0.08, dt));
                    double u = DMath.Saturate(_t / _airDur);
                    _anchor = Vec3.Lerp(_from, _to, u);
                    if (_t >= _airDur)
                    {
                        _state = HopState.Land;
                        _t = 0;
                        _landDur = 0.16 * ts;
                        _anchor = _to;
                        _landImpact = DMath.Saturate(_heightNow / Math.Max(1, _height));
                        _jy.V += 3.2 * _landImpact;
                        Vec3 v = ctx.WorldDirToCreature(ctx.Velocity);
                        _jx.V += -v.X * 0.012;
                        _jz.V += v.Z * 0.012;
                    }
                    break;
                }
                case HopState.Land:
                    _t += dt;
                    if (_t >= _landDur) _state = HopState.Ground;
                    break;
            }
            double cycle = _crouchDur + _airDur + _landDur;
            _freq = moving && cycle > 1e-6 ? 1.0 / cycle : 0;

            // idle blorps: a small spontaneous wobble
            if (!ctx.ExportMode && ctx.Time >= _nextBlorp)
            {
                _blorps++;
                if (_state == HopState.Ground) _jy.V += DMath.Lerp(0.6, 1.4, ctx.Hash(52, _blorps));
                _nextBlorp = ctx.Time + DMath.Lerp(1.8, 4.5, ctx.Hash(53, _blorps)) * ts;
            }
            // turning shears the body
            _jz.V += -ctx.YawRate * 0.02 * dt * 60;
            double f = DMath.Lerp(2.6, 3.8, traits.Stiffness) / ts;
            _jx.Step(0, f, 0.22, dt);
            _jz.Step(0, f, 0.22, dt);
            _jy.Step(0, f * 1.1, 0.2, dt);
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            Vec3 rel = ctx.WorldDirToCreature(_anchor - ctx.Position);
            p.RootOffset = p.RootOffset + new Vec3(rel.X, 0, rel.Z);
            double squash = 0;
            switch (_state)
            {
                case HopState.Crouch:
                    squash = 0.3 * DMath.EaseOutQuad(DMath.Saturate(_t / Math.Max(1e-6, _crouchDur)));
                    p.EyeOpen *= 0.8;
                    break;
                case HopState.Air:
                {
                    double u = DMath.Saturate(_t / Math.Max(1e-6, _airDur));
                    // stretched at launch and before touch down, round at the apex
                    squash = -0.24 * DMath.Sq(1 - u) - 0.1 * DMath.Sq(u);
                    p.BodyPitch += DMath.Lerp(-0.12, 0.12, u);
                    break;
                }
                case HopState.Land:
                {
                    double u = DMath.Saturate(_t / Math.Max(1e-6, _landDur));
                    squash = 0.34 * _landImpact * (1 - DMath.EaseOutQuad(u));
                    p.EyeOpen *= DMath.Lerp(0.35, 1.0, u);
                    break;
                }
            }
            p.Squash += squash + _jy.X * 0.08;
            p.SpinePitch += _jx.X * 0.6;
            p.SpineYaw += _jz.X * 0.6;
            p.BodyRoll += _jz.X * 0.25;
        }

        public override double Altitude(MotorContext ctx)
        {
            if (_state != HopState.Air) return 0;
            double u = DMath.Saturate(_t / Math.Max(1e-6, _airDur));
            return 4 * _heightNow * u * (1 - u);
        }

        public override double CycleFrequency(MotorContext ctx) => _freq;
    }
}
