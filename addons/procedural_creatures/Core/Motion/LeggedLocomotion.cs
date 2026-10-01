// Procedural Pixel Creatures - legged locomotion (bipeds, quadrupeds, hexapods, octopods).
//
// Feet are planted in WORLD space: a foot in stance never moves, so there is no foot sliding at any
// speed, while turning or while stopping. Swings are triggered by a gait clock while moving (walk,
// trot, gallop, tripod ... with duty factor and stride derived from leg length and speed) and by a
// balance controller while standing (a foot steps when it drifts too far from its neutral spot,
// e.g. after stopping or while turning in place). Landing spots are predicted from the root
// velocity so that mid-stance happens under the hip.

using System;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core.Motion
{
    public sealed class LeggedLocomotion : LocomotionModuleBase
    {
        public override string Id => "legged";

        private sealed class LegState
        {
            public LegRig Rig = null!;
            public int Index;
            public Vec3 Plant;
            public Vec3 Foot;
            public bool Swinging;
            public Vec3 From, To;
            public double T, Dur;
            public double Offset;
            public double Lift;
        }

        private LegState[] _legs = Array.Empty<LegState>();
        private LegState[] _arms = Array.Empty<LegState>();
        private readonly Vec3[] _joints = new Vec3[4];
        private double _phase;
        private bool _moving;
        private double _legLen = 10;
        private double _freq = 1;
        private double _duty = 0.6;
        private int _legCount;

        public double Phase => _phase;

        public override double CycleFrequency(MotorContext ctx) => _moving ? _freq : 0;

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            _legLen = Math.Max(3.0, ctx.Anatomy.Metrics.LegLength);
            _legs = new LegState[rig.Legs.Count];
            for (int i = 0; i < _legs.Length; i++)
            {
                var l = rig.Legs[i];
                var s = new LegState { Rig = l, Index = i, Offset = l.Phase };
                s.Plant = Ground(ctx.CreatureToWorld(l.RestFoot));
                s.Foot = s.Plant;
                _legs[i] = s;
            }
            _arms = new LegState[rig.Arms.Count];
            for (int i = 0; i < _arms.Length; i++) _arms[i] = new LegState { Rig = rig.Arms[i], Index = i, Offset = rig.Arms[i].Phase };
            _legCount = _legs.Length;
            _phase = 0;
            _moving = false;
        }

        private static Vec3 Ground(Vec3 v) => new Vec3(v.X, 0, v.Z);

        /// <summary>Puts every foot on its neutral spot under the current root (used while airborne).</summary>
        public void ResetPlants(MotorContext ctx)
        {
            foreach (var s in _legs)
            {
                s.Plant = Ground(ctx.CreatureToWorld(s.Rig.RestFoot));
                s.Foot = s.Plant;
                s.Swinging = false;
                s.T = 0;
            }
            _moving = false;
        }

        public int LegCount => _legCount;

        // ------------------------------------------------------------------ gait patterns

        private double RunOffset(LegState s)
        {
            var l = s.Rig;
            int n = _legCount;
            bool left = l.Side < 0;
            if (n == 2) return left ? 0.0 : 0.5;
            if (n == 4)
            {
                bool front = l.Pair == 0;
                // trot: diagonal pairs move together
                return (front, left) switch
                {
                    (false, true) => 0.0,
                    (true, false) => 0.0,
                    _ => 0.5,
                };
            }
            // hexapod tripod / octopod alternating tetrapod: neighbours in antiphase
            return ((l.Pair + (left ? 0 : 1)) & 1) == 0 ? 0.0 : 0.5;
        }

        private static double GallopOffset(LegState s)
        {
            bool front = s.Rig.Pair == 0, left = s.Rig.Side < 0;
            return (front, left) switch
            {
                (false, true) => 0.0,
                (false, false) => 0.1,
                (true, true) => 0.5,
                _ => 0.62,
            };
        }

        private static double CircLerp(double a, double b, double t)
        {
            double d = b - a;
            d -= Math.Round(d);
            return DMath.Frac(a + d * t);
        }

        // ------------------------------------------------------------------ step

        public override void Step(MotorContext ctx, double dt)
        {
            var traits = ctx.Traits;
            double speed = ctx.Speed;
            double ts = traits.TimeScale;
            double r = ctx.RunBlend;
            double walkDuty = ctx.Rig.Tune("gait.walkDuty", _legCount == 2 ? 0.62 : 0.66);
            double runDuty = ctx.Rig.Tune("gait.runDuty", _legCount == 2 ? 0.36 : 0.4);
            _duty = DMath.Lerp(walkDuty, runDuty, r);
            double strideLen = _legLen * DMath.Lerp(1.05, 2.3, r) * DMath.Lerp(0.95, 1.1, traits.Energy);
            if (_legCount > 4) strideLen *= 0.75;
            double fMin = 0.9 / ts, fMax = 3.6 / ts;
            _freq = DMath.Clamp(speed / Math.Max(1e-6, strideLen), fMin, fMax);
            if (!double.IsNaN(ctx.CycleFrequencyOverride)) _freq = ctx.CycleFrequencyOverride;

            bool tucking = ctx.RestWeight > 0.02;
            bool moving = speed > Math.Max(1.0, traits.WalkSpeed * 0.12) && !tucking && ctx.ActionWeight < 0.6;

            // gait offsets follow the speed (walk -> trot -> gallop)
            double trot = DMath.SmoothStep(0.15, 0.55, r);
            double gallop = _legCount == 4 ? DMath.SmoothStep(0.72, 0.98, r) * ctx.Rig.Tune("gait.gallop", 1.0) : 0;
            foreach (var s in _legs)
            {
                double target = CircLerp(s.Rig.Phase, RunOffset(s), trot);
                if (gallop > 0) target = CircLerp(target, GallopOffset(s), gallop);
                s.Offset = target;
            }

            if (moving && !_moving)
            {
                // start the cycle so that the leg that lags most behind swings first
                LegState? first = null;
                double worst = -1;
                Vec3 fwd = ctx.Velocity.NormalizedOr(ctx.Forward);
                foreach (var s in _legs)
                {
                    double behind = -Vec3.Dot(s.Plant - ctx.CreatureToWorld(s.Rig.RestFoot), fwd);
                    if (behind > worst) { worst = behind; first = s; }
                }
                if (first != null) _phase = DMath.Frac(_duty - first.Offset + 1e-3);
            }
            _moving = moving;

            if (moving)
            {
                _phase = DMath.Frac(_phase + _freq * dt);
                double swingWindow = 1.0 - _duty;
                foreach (var s in _legs)
                {
                    if (s.Swinging) continue;
                    double p = DMath.Frac(_phase + s.Offset);
                    if (p >= _duty && p < _duty + swingWindow * 0.55)
                    {
                        double remaining = (1.0 - p) / _freq;
                        StartSwing(ctx, s, DMath.Clamp(remaining, 0.07 * ts, 0.7 * ts), true);
                    }
                }
            }
            else if (!tucking)
            {
                Balance(ctx, dt);
            }

            // advance swings
            foreach (var s in _legs)
            {
                if (!s.Swinging) { s.Foot = s.Plant; continue; }
                s.T += dt / s.Dur;
                if (moving) s.To = PredictLanding(ctx, s, Math.Max(0, (1 - s.T) * s.Dur));
                else s.To = Ground(ctx.CreatureToWorld(s.Rig.RestFoot));
                double t = DMath.Saturate(s.T);
                double h = DMath.EaseInOutSine(t);
                Vec3 p = Vec3.Lerp(s.From, s.To, h);
                double liftShape = DMath.Sin(DMath.Pi * DMath.Pow(t, 0.85));
                s.Foot = new Vec3(p.X, s.Lift * liftShape, p.Z);
                if (s.T >= 1.0)
                {
                    s.Swinging = false;
                    s.Plant = Ground(s.To);
                    s.Foot = s.Plant;
                }
            }
            if (tucking)
            {
                // while lying down planted feet are re-anchored under the body so that getting up
                // starts from a sensible stance
                foreach (var s in _legs)
                {
                    if (s.Swinging) continue;
                    Vec3 neutral = Ground(ctx.CreatureToWorld(s.Rig.RestFoot));
                    s.Plant = Vec3.Lerp(s.Plant, neutral, DMath.DampFactor(0.25, dt));
                    s.Foot = s.Plant;
                }
            }
        }

        private void StartSwing(MotorContext ctx, LegState s, double duration, bool moving)
        {
            s.Swinging = true;
            s.From = s.Foot;
            s.T = 0;
            s.Dur = duration;
            double r = ctx.RunBlend;
            s.Lift = s.Rig.LiftHeight * DMath.Lerp(0.75, 1.25, r) * DMath.Lerp(0.85, 1.2, ctx.Traits.Bounce) * (moving ? 1.0 : 0.7);
            s.To = moving ? PredictLanding(ctx, s, duration) : Ground(ctx.CreatureToWorld(s.Rig.RestFoot));
        }

        private Vec3 PredictLanding(MotorContext ctx, LegState s, double tRemaining)
        {
            Vec3 futureRoot = ctx.Position + ctx.Velocity * tRemaining;
            double futureYaw = ctx.Yaw + DMath.Clamp(ctx.YawRate * tRemaining, -0.6, 0.6);
            Vec3 neutral = futureRoot + PixelCamera.YawRotation(futureYaw).Mul(s.Rig.RestFoot);
            double stanceTime = _duty / Math.Max(1e-6, _freq);
            double stanceLen = ctx.Speed * stanceTime;
            double maxReach = _legLen * 0.62;
            Vec3 dir = ctx.Velocity.NormalizedOr(ctx.Forward);
            Vec3 lead = dir * Math.Min(stanceLen * 0.5, maxReach);
            return Ground(neutral + lead);
        }

        private void Balance(MotorContext ctx, double dt)
        {
            double threshold = _legLen * (_legCount == 2 ? 0.16 : 0.2);
            int swinging = 0;
            foreach (var s in _legs) if (s.Swinging) swinging++;
            int maxSwing = _legCount <= 2 ? 1 : (_legCount <= 4 ? 2 : 3);
            if (swinging >= maxSwing) return;
            LegState? best = null;
            double bestErr = threshold;
            foreach (var s in _legs)
            {
                if (s.Swinging) continue;
                Vec3 neutral = Ground(ctx.CreatureToWorld(s.Rig.RestFoot));
                double err = new Vec2(s.Plant.X - neutral.X, s.Plant.Z - neutral.Z).Length;
                if (err <= bestErr) continue;
                if (!NeighboursPlanted(s)) continue;
                best = s;
                bestErr = err;
            }
            if (best != null)
                StartSwing(ctx, best, DMath.Lerp(0.2, 0.34, DMath.Saturate(_legLen / 30.0)) * ctx.Traits.TimeScale, false);
        }

        private bool NeighboursPlanted(LegState s)
        {
            foreach (var o in _legs)
            {
                if (o == s || !o.Swinging) continue;
                bool samePair = o.Rig.Pair == s.Rig.Pair;
                bool sameSideAdjacent = o.Rig.Side == s.Rig.Side && Math.Abs(o.Rig.Pair - s.Rig.Pair) == 1;
                if (samePair || sameSideAdjacent) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ body motion

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            double w = ctx.MoveWeight * (1 - ctx.RestWeight);
            if (w <= 1e-4) return;
            double r = ctx.RunBlend;
            double ph = _phase;
            double bounce = ctx.Traits.Bounce;
            double amp = _legLen * DMath.Lerp(0.022, 0.06, bounce) * DMath.Lerp(0.7, 1.25, r);
            double c2 = DMath.Cos(4 * DMath.Pi * ph);
            double gallop = _legCount == 4 ? DMath.SmoothStep(0.72, 0.98, r) * ctx.Rig.Tune("gait.gallop", 1.0) : 0;
            double bob;
            if (_legCount == 2)
            {
                // biped walk: highest at mid stance (inverted pendulum); run: lowest at mid stance
                bob = DMath.Lerp(-c2, c2, DMath.SmoothStep(0.35, 0.7, r)) * amp * 1.2;
                p.BodyRoll += DMath.Sin(DMath.TwoPi * ph) * DMath.Lerp(0.07, 0.03, r) * w;
                p.BodyYaw += DMath.Sin(DMath.TwoPi * ph) * 0.06 * w;
                p.ArmSwing = DMath.Sin(DMath.TwoPi * ph);
            }
            else
            {
                bob = DMath.Lerp(-c2 * 0.6, c2, DMath.SmoothStep(0.15, 0.5, r)) * amp;
                if (gallop > 0)
                {
                    double g = DMath.Cos(DMath.TwoPi * ph + 1.2);
                    bob = DMath.Lerp(bob, g * amp * 1.6, gallop);
                    p.BodyPitch += DMath.Sin(DMath.TwoPi * ph + 0.4) * 0.09 * gallop * w;
                    p.SpinePitch += DMath.Sin(DMath.TwoPi * ph) * 0.18 * gallop * w;
                }
                p.BodyRoll += DMath.Sin(DMath.TwoPi * ph) * 0.025 * w;
                if (_legs.Length > 0 && _legs[0].Rig.Style == LegStyle.Sprawling)
                    p.SpineYaw += DMath.Sin(DMath.TwoPi * ph) * 0.35 * w;
            }
            p.BodyOffset = p.BodyOffset + new Vec3(0, bob * w, 0);
            // head stabilisation and a small nod
            p.NeckPitch += DMath.Sin(4 * DMath.Pi * ph + 0.8) * 0.05 * w * (1 - r * 0.5);
            p.HeadPitch -= p.BodyPitch * 0.6;
            // tail counter swing
            p.TailYaw += DMath.Sin(DMath.TwoPi * ph + DMath.Pi) * DMath.Lerp(0.18, 0.08, r) * w;
            // lean with acceleration and into turns
            Vec3 accC = ctx.WorldDirToCreature(ctx.Acceleration);
            p.BodyPitch += DMath.Clamp(-accC.X * 0.0012, -0.12, 0.12) * w;
            p.BodyRoll += DMath.Clamp(-ctx.YawRate * ctx.Speed * 0.0022, -0.18, 0.18) * w;
            p.SpineYaw += DMath.Clamp(ctx.YawRate * 0.1, -0.35, 0.35) * w;
        }

        // ------------------------------------------------------------------ limbs

        public override void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output)
        {
            Vec3 bodyFwd = Vec3.UnitX;
            if (ctx.Rig.Pelvis >= 0)
            {
                Vec3 f = pose.World[ctx.Rig.Pelvis].Basis.C0;
                bodyFwd = new Vec3(f.X, 0, f.Z).NormalizedOr(Vec3.UnitX);
            }
            foreach (var s in _legs)
            {
                var leg = s.Rig;
                Vec3 hip = pose.World[leg.RootBone].Origin;
                Vec3 footC = ctx.WorldToCreature(s.Foot);
                footC = new Vec3(footC.X, Math.Max(0, footC.Y - ctx.Altitude), footC.Z);
                double tuck = TuckAmount(ctx, leg, p.LegTuck);
                Vec3 target = footC;
                if (ctx.GroundHeightAt != null && ctx.Altitude < 0.5)
                {
                    double offset = ctx.GroundHeightAt(s.Foot.X, s.Foot.Z) - ctx.GroundHeightAt(ctx.Position.X, ctx.Position.Z);
                    // Reach is limited by the anatomy; cliff collision is the world's responsibility.
                    target = target + new Vec3(0, DMath.Clamp(offset, -leg.TotalLength * 0.35, leg.TotalLength * 0.35), 0);
                }
                if (tuck > 0) target = Vec3.Lerp(footC, TuckTarget(leg, hip), tuck);
                if (p.FrontReach != 0 && leg.Pair == 0)
                    target = target + new Vec3(p.FrontReach * _legLen, Math.Max(0, p.FrontReach) * _legLen * 0.35, 0);
                double lift = leg.Pair == 0 ? DMath.Clamp(p.FrontLift, 0, 1.2) : 0;
                if (lift > 0)
                    target = target + new Vec3(lift * _legLen * 0.3, lift * _legLen * 0.8, 0);
                // grounded creatures in the air (jumps) let their legs hang
                if (ctx.Altitude > 0.5 && tuck < 0.5)
                    target = Vec3.Lerp(target, hip + new Vec3(0, -leg.TotalLength * 0.8, leg.Side * 0.5), DMath.Saturate(ctx.Altitude / (_legLen * 0.4)));
                double roll = 0;
                if (s.Swinging) roll = DMath.Sin(DMath.Pi * DMath.Saturate(s.T)) * 0.6;
                Vec3 outward = new Vec3(0, 0, leg.Side == 0 ? 1 : leg.Side);
                Limbs.Solve(leg, hip, target, bodyFwd, outward, roll, _joints);
                SetLegBones(pose, leg, bodyFwd, roll);
                output.Contacts.Add(new ContactPoint { Position = target, Planted = !s.Swinging && tuck < 0.5 && lift < 0.15, Leg = s.Index });
            }
            // arms swing opposite to the legs (bipeds); actions may raise/reach
            foreach (var a in _arms)
            {
                var arm = a.Rig;
                Vec3 shoulder = pose.World[arm.RootBone].Origin;
                double swing = p.ArmSwing * (arm.Side < 0 ? 1 : -1) * ctx.MoveWeight;
                double act = p.ArmSide == 0 || Math.Sign(p.ArmSide) == Math.Sign(arm.Side) ? 1.0 : 0.25;
                double len = arm.TotalLength;
                // rest hand position as built (hanging arms, claws held in front), then swing, raise and reach
                Vec3 restOff = arm.RestFoot - ctx.Anatomy.Skeleton.RestWorld(arm.RootBone).Origin;
                double raise = DMath.Clamp(p.ArmRaise * act, -0.3, 1.25);
                Vec3 target = shoulder + restOff + new Vec3(swing * len * 0.28 + p.ArmReach * act * len * 0.6 + raise * len * 0.15, raise * len * 1.05, 0);
                double tuck = p.LegTuck;
                if (tuck > 0) target = Vec3.Lerp(target, shoulder + new Vec3(len * 0.45, -len * 0.4, arm.Side * len * 0.25), tuck);
                Vec3 outward = new Vec3(0, 0, arm.Side == 0 ? 1 : arm.Side);
                Limbs.Solve(arm, shoulder, target, bodyFwd, outward, 0, _joints);
                SetLegBones(pose, arm, bodyFwd, 0);
            }
        }

        private void SetLegBones(SkeletonPose pose, LegRig leg, Vec3 fwd, double roll)
        {
            Vec3 up = fwd;
            pose.SetOverride(leg.RootBone, new Xform(Kinematics.SegmentBasis(_joints[0], _joints[1], up), _joints[0]));
            pose.SetOverride(leg.JointBones[0], new Xform(Kinematics.SegmentBasis(_joints[1], _joints[2], up), _joints[1]));
            pose.SetOverride(leg.JointBones[1], new Xform(Kinematics.SegmentBasis(_joints[2], _joints[3], up), _joints[2]));
            if (leg.IsArm)
            {
                // hands and pincers keep pointing along the hand segment
                pose.SetOverride(leg.JointBones[2], new Xform(Kinematics.SegmentBasis(_joints[2], _joints[3], up), _joints[3]));
                return;
            }
            Vec3 toeDir = (fwd * DMath.Cos(roll) - Vec3.UnitY * DMath.Sin(roll)).NormalizedOr(fwd);
            pose.SetOverride(leg.JointBones[2], new Xform(Mat3.LookAlong(toeDir, Vec3.UnitY), _joints[3]));
        }

        private static double TuckAmount(MotorContext ctx, LegRig leg, double tuck)
        {
            if (tuck <= 0) return 0;
            bool front = leg.Pair == 0;
            // lying down: hind legs fold first; getting up: front legs extend first
            if (!ctx.RestExiting) return front ? DMath.SmoothStep(0.3, 0.95, tuck) : DMath.SmoothStep(0.0, 0.7, tuck);
            return front ? DMath.SmoothStep(0.45, 1.0, tuck) : DMath.SmoothStep(0.05, 0.75, tuck);
        }

        private Vec3 TuckTarget(LegRig leg, Vec3 hip)
        {
            double len = leg.TotalLength;
            bool front = leg.Pair == 0 && _legCount > 2;
            double side = leg.Side == 0 ? 0 : leg.Side;
            // on the ground the tucked foot rests on the floor; in the air it folds up under the body
            double y = Math.Max(leg.SoleHeight, hip.Y - len * 0.42);
            if (leg.Style == LegStyle.Arthropod)
                return new Vec3(hip.X + (leg.Pair - 1.5) * len * 0.1, Math.Max(leg.SoleHeight, hip.Y - len * 0.2), hip.Z + side * len * 0.55);
            if (front)
                return new Vec3(hip.X + len * 0.42, y, hip.Z + side * len * 0.08);
            return new Vec3(hip.X + len * 0.3, y, hip.Z + side * len * 0.22);
        }
    }
}
