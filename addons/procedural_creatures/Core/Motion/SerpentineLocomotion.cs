// Procedural Pixel Creatures - serpentine locomotion (snakes, worms, legless burrowers).
//
// The creature's position is its head. The head weaves around its travel line (lateral undulation)
// and leaves a trail in world space; every body joint sits on that trail at its arc distance behind
// the head. The body therefore follows exactly the path the head took - the legless equivalent of
// planted feet: nothing slides sideways. Worms use a rectilinear style: a straight trail with
// travelling compression waves (peristalsis). The front of the body can be raised (cobras, curious
// worms), actions lunge the front forward along the head direction and resting coils the body into
// a flat spiral with the head on top. Standing still, the trail relaxes into a per-creature idle
// shape (an S or a loose curl, built by integrating the heading so arc lengths stay exact) and
// rearing species lift the front of the body into an upright column with the head bent forward.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class SerpentineLocomotion : LocomotionModuleBase
    {
        public override string Id => "serpentine";

        public const int Lateral = 0, Rectilinear = 1;
        private const double Spacing = 0.5;

        private ChainRig _body = null!;
        private Vec3[] _path = Array.Empty<Vec3>();
        private int _head;
        private int _count;
        private double[] _arc = Array.Empty<double>();
        private double[] _radius = Array.Empty<double>();
        private Vec3[] _joint = Array.Empty<Vec3>();
        private Vec3[] _coil = Array.Empty<Vec3>();
        private double _bodyLen, _lambda, _amp, _raise, _raiseLen, _travel, _phase, _freq;
        private int _style;
        private Smooth1 _wave;
        private double _tongueNext, _tongueStart = -10;
        private int _tongueIndex;
        private double _radiusMax, _settledYaw, _crawled;
        private bool _turning, _turnActive;
        private Vec3[] _idleShape = Array.Empty<Vec3>();
        private double _stillTime, _rearAmount;
        private Smooth1 _rear;
        private Vec3[] _rearPts = Array.Empty<Vec3>();

        public override void Initialize(MotorContext ctx)
        {
            var rig = ctx.Rig;
            _body = rig.Body ?? throw new InvalidOperationException("Serpentine locomotion needs MotionRig.Body.");
            int n = _body.Bones.Length;
            _arc = new double[n];
            _radius = new double[n];
            _joint = new Vec3[n];
            double acc = 0;
            for (int i = 0; i < n; i++)
            {
                _arc[i] = acc;
                _radius[i] = i < _body.Radii.Length ? _body.Radii[i] : 1;
                if (i < _body.Lengths.Length) acc += _body.Lengths[i];
            }
            _bodyLen = Math.Max(4, acc);
            _radiusMax = 0;
            foreach (var r in _radius) _radiusMax = Math.Max(_radiusMax, r);
            _turning = false;
            _turnActive = false;
            _crawled = 0;
            _settledYaw = ctx.Yaw;
            _lambda = rig.Tune("serpent.wavelength", _bodyLen * 0.55);
            _amp = rig.Tune("serpent.amplitude", _bodyLen * 0.085);
            _raise = rig.Tune("serpent.raise", 1.5);
            _raiseLen = Math.Max(2, rig.Tune("serpent.raiseLength", _bodyLen * 0.22));
            _style = (int)Math.Round(rig.Tune("serpent.style", Lateral));
            _rearAmount = DMath.Saturate(rig.Tune("serpent.rear", 0));
            int cap = (int)Math.Ceiling((_bodyLen * 1.5 + 32) / Spacing) + 8;
            _path = new Vec3[cap];
            _travel = 0;
            _phase = 0;
            _wave = default;
            _stillTime = 0;
            // creatures start settled: the trail is the idle shape and rearing species stand upright
            BuildIdleShape(ctx, cap);
            for (int k = 0; k < cap; k++) _path[k] = ctx.CreatureToWorld(_idleShape[k]).WithY(0);
            _rear = new Smooth1 { X = 1 };
            Vec3 lead = ctx.Position;
            _head = 0;
            _count = cap;
            BuildCoil();
            _tongueNext = ctx.Time + 0.8 + ctx.Hash(41, 0) * 1.5;
            _tongueStart = -10;
            _tongueIndex = 0;
            _ = lead;
        }

        private ref Vec3 PathAt(int k) => ref _path[(_head + k) % _path.Length];

        /// <summary>
        /// Idle body shape in creature space (head at the origin, facing +X): the heading is integrated
        /// along the arc, so every point lies exactly at its arc distance. Snakes lie in an S, often with
        /// a slow curl; worms stay nearly straight. Each creature gets its own variant (hash of its seed).
        /// </summary>
        private void BuildIdleShape(MotorContext ctx, int cap)
        {
            _idleShape = new Vec3[cap];
            double h1 = ctx.Hash(51, 0), h2 = ctx.Hash(52, 0), h3 = ctx.Hash(53, 0), h4 = ctx.Hash(54, 0);
            bool lateral = _style == Lateral;
            double sign = h3 < 0.5 ? -1 : 1;
            double bend = lateral ? DMath.Lerp(0.75, 1.3, h1) : DMath.Lerp(0.2, 0.45, h1);
            double lam = _bodyLen * (lateral ? DMath.Lerp(0.5, 0.78, h2) : DMath.Lerp(0.8, 1.2, h2));
            // a slow curl bends the whole body into a C (total turn up to ~130 degrees)
            double curl = lateral ? sign * DMath.Lerp(-0.25, 0.72, h4) * DMath.Pi / _bodyLen : 0;
            Vec3 p = Vec3.Zero;
            for (int k = 0; k < cap; k++)
            {
                double s = k * Spacing;
                _idleShape[k] = p;
                // straight neck behind the head, so the head keeps looking forward
                double env = DMath.SmoothStep(0, _bodyLen * 0.14, s);
                double th = sign * bend * env * Math.Sin(DMath.TwoPi * s / lam) + curl * Math.Min(s, _bodyLen);
                p += new Vec3(-Math.Cos(th), 0, Math.Sin(th)) * Spacing;
            }
        }

        /// <summary>Standing still the trail relaxes into the idle shape (head and facing stay put).</summary>
        private void SettleIdle(MotorContext ctx, double speed, double dt)
        {
            bool still = speed < 1e-3 && !_turnActive && ctx.RestWeight < 0.01 && ctx.MoveWeight < 0.05;
            _stillTime = still ? _stillTime + dt : 0;
            if (_stillTime < 0.6 || _count < 2) return;
            double a = 1.0 - Math.Exp(-1.3 * dt);
            int m = Math.Min(_count, _idleShape.Length);
            for (int k = 1; k < m; k++)
            {
                ref Vec3 q = ref PathAt(k);
                q = Vec3.Lerp(q, ctx.CreatureToWorld(_idleShape[k]).WithY(0), a);
            }
        }

        private void Push(Vec3 p)
        {
            _head = (_head - 1 + _path.Length) % _path.Length;
            _path[_head] = p;
            if (_count < _path.Length) _count++;
        }

        private void BuildCoil()
        {
            // flat Archimedean spiral in creature space: head near the centre, tail outside
            int n = _arc.Length;
            _coil = new Vec3[n];
            double rMax = 0;
            foreach (var r in _radius) rMax = Math.Max(rMax, r);
            double spacing = rMax * 2.05;
            double b = spacing / DMath.TwoPi;
            double r0 = rMax * 1.6;
            Vec3 centre = new Vec3(-r0 * 0.6, 0, 0);
            double theta = 0;
            double sPrev = 0;
            for (int i = 0; i < n; i++)
            {
                double target = _arc[i];
                // advance theta so that the spiral arc length matches the body arc length
                double s = sPrev;
                int guard = 0;
                while (s < target && guard++ < 10000)
                {
                    double r = r0 + b * theta;
                    double dth = Math.Min(0.05, Math.Max(1e-3, (target - s) / Math.Max(0.5, r)));
                    s += Math.Sqrt(r * r + b * b) * dth;
                    theta += dth;
                }
                sPrev = s;
                double rr = r0 + b * theta;
                // start pointing forward and wind counter-clockwise (seen from above)
                double a = theta;
                _coil[i] = centre + new Vec3(DMath.Cos(a) * rr, 0, DMath.Sin(a) * rr);
            }
        }

        public override void Step(MotorContext ctx, double dt)
        {
            double t = ctx.Time;
            _wave.Step(ctx.RestWeight > 0.01 ? 0 : ctx.MoveWeight, 0.28, dt);
            double speed = ctx.RestWeight > 0.01 ? 0 : ctx.Speed;
            _travel += speed * dt;
            double lam = _lambda;
            if (!double.IsNaN(ctx.CycleFrequencyOverride) && speed > 1e-3) lam = Math.Max(4, speed / ctx.CycleFrequencyOverride);
            _freq = speed / Math.Max(1e-6, lam);
            _phase = DMath.WrapAngle(_phase + DMath.TwoPi * (speed / Math.Max(1e-6, lam * 0.5)) * dt);

            TurnInPlace(ctx, speed, dt);
            SettleIdle(ctx, speed, dt);
            // rearing species stand upright while idle and lower the body to crawl
            _rear.Step(ctx.RestWeight > 0.01 || ctx.MoveWeight > 0.3 ? 0 : 1, ctx.MoveWeight > 0.3 ? 0.12 : 0.35, dt);

            Vec3 fwd = new Vec3(ctx.Velocity.X, 0, ctx.Velocity.Z).NormalizedOr(ctx.Forward);
            Vec3 side = new Vec3(-fwd.Z, 0, fwd.X);
            double lateral = _style == Lateral
                ? _amp * _wave.X * DMath.Sin(DMath.TwoPi * _travel / lam)
                : _amp * 0.2 * _wave.X * DMath.Sin(DMath.TwoPi * _travel / (lam * 1.6));
            Vec3 head = (ctx.Position + side * lateral).WithY(0);
            // PathAt(0) is the live head point, PathAt(1..) are committed trail points: commit a new
            // point once the head is a full spacing away from the last committed one (independent of
            // the speed and the simulation step)
            if ((head - PathAt(1)).Length >= Spacing) Push(head);
            else PathAt(0) = head;

            // idle tongue flicks
            if (t >= _tongueNext)
            {
                _tongueStart = t;
                _tongueIndex++;
                _tongueNext = t + DMath.Lerp(1.6, 4.0, ctx.Hash(42, _tongueIndex)) * ctx.Traits.TimeScale;
            }
        }

        /// <summary>
        /// A legless body cannot pivot: when the facing changes while the creature stands still, the
        /// head crawls along an arc towards the new direction and the body follows its trail until
        /// the whole body lies in the new direction. The trail is shifted back by the crawled distance
        /// so the head stays at the creature origin (the body flows around like on a treadmill;
        /// contacts are not reported as planted meanwhile).
        /// </summary>
        private void TurnInPlace(MotorContext ctx, double speed, double dt)
        {
            _turning = false;
            if (speed > ctx.Traits.WalkSpeed * 0.15 || ctx.RestWeight > 0.01 || _count < 2)
            {
                _turnActive = false;
                _crawled = 0;
                _settledYaw = ctx.TargetYaw;
                return;
            }
            if (!_turnActive)
            {
                if (Math.Abs(DMath.WrapAngle(ctx.TargetYaw - _settledYaw)) < 0.3) return;
                _turnActive = true;
                _crawled = 0;
            }
            Vec3 h0 = PathAt(0), h1 = PathAt(1);
            Vec3 cur = new Vec3(h0.X - h1.X, 0, h0.Z - h1.Z).NormalizedOr(ctx.Forward);
            // aim for the facing the motor is turning to (its yaw only catches up gradually)
            Vec3 want = PixelCamera.YawRotation(ctx.TargetYaw).Mul(Vec3.UnitX).WithY(0).NormalizedOr(ctx.Forward);
            double ang = Math.Atan2(Vec3.Cross(cur, want).Y, Vec3.Dot(cur, want));
            int k = 0;
            double acc = 0;
            // the turn is complete when the head and the body behind it (middle and tail) line up
            Vec3 mid = Sample(_bodyLen * 0.5, ref k, ref acc);
            Vec3 tail = Sample(_bodyLen * 0.85, ref k, ref acc);
            Vec3 midDir = new Vec3(h0.X - mid.X, 0, h0.Z - mid.Z).NormalizedOr(cur);
            Vec3 tailDir = new Vec3(h0.X - tail.X, 0, h0.Z - tail.Z).NormalizedOr(cur);
            double midAng = Math.Atan2(Vec3.Cross(midDir, want).Y, Vec3.Dot(midDir, want));
            double tailAng = Math.Atan2(Vec3.Cross(tailDir, want).Y, Vec3.Dot(tailDir, want));
            if ((Math.Abs(ang) < 0.06 && Math.Abs(midAng) < 0.4 && Math.Abs(tailAng) < 0.45) || _crawled > _bodyLen * 1.8)
            {
                _turnActive = false;
                _settledYaw = ctx.TargetYaw;
                return;
            }
            _turning = true;
            double d = Math.Max(6.0, ctx.Traits.WalkSpeed) * 1.15 * dt;
            _crawled += d;
            double radius = Math.Max(_radiusMax * 2.5, _bodyLen * 0.14);
            double maxTurn = d / radius;
            Vec3 dir = Quat.AxisAngle(Vec3.UnitY, DMath.Clamp(ang, -maxTurn, maxTurn)).Rotate(cur).WithY(0).NormalizedOr(want);
            Vec3 shift = dir * d;
            for (int i = 0; i < _count; i++)
            {
                ref Vec3 q = ref PathAt(i);
                q = q - shift;
            }
        }

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            if (ctx.Rig.Tongue >= 0 && !ctx.ExportMode)
            {
                double tt = ctx.Time - _tongueStart;
                double flick = DMath.Pulse(tt, 0, 0.05, 0.12, 0.17) + DMath.Pulse(tt, 0.25, 0.3, 0.37, 0.43);
                p.Tongue = Math.Max(p.Tongue, flick * (1 - ctx.RestWeight));
            }
            // the head leads with a slight counter-sway and looks around while idle
            p.HeadYaw += -DMath.Sin(DMath.TwoPi * _travel / Math.Max(1, _lambda)) * 0.18 * _wave.X;
        }

        /// <summary>Samples the trail at arc distance s behind the head (negative s extrapolates ahead).</summary>
        private Vec3 Sample(double s, ref int k, ref double acc)
        {
            if (s < acc) { k = 0; acc = 0; }
            if (s <= 0)
            {
                Vec3 a = PathAt(0), b = PathAt(Math.Min(1, _count - 1));
                Vec3 dir = (a - b).NormalizedOr(Vec3.UnitX);
                return a + dir * -s;
            }
            while (k + 1 < _count)
            {
                Vec3 a = PathAt(k), b = PathAt(k + 1);
                double seg = (b - a).Length;
                if (acc + seg >= s)
                {
                    double u = seg > 1e-9 ? (s - acc) / seg : 0;
                    return Vec3.Lerp(a, b, u);
                }
                acc += seg;
                k++;
            }
            // trail too short: continue straight
            Vec3 last = PathAt(_count - 1), prev = PathAt(Math.Max(0, _count - 2));
            return last + (last - prev).NormalizedOr(Vec3.UnitX * -1) * (s - acc);
        }

        public override void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output)
        {
            var bones = _body.Bones;
            int n = bones.Length;
            double lunge = p.BodyOffset.X;
            double lift = Math.Max(-_raise, p.BodyOffset.Y);
            double frontLen = Math.Max(4, _bodyLen * 0.45);
            int k = 0;
            double acc = 0;
            double peri = _style == Rectilinear ? 0.07 * _wave.X : 0;
            for (int i = 0; i < n; i++)
            {
                double s = _arc[i];
                if (peri > 0) s *= 1.0 - peri * (0.5 + 0.5 * DMath.Sin(_phase - i * 0.9));
                double front = DMath.Saturate(1.0 - _arc[i] / frontLen);
                s -= lunge * front * front;
                Vec3 w = Sample(s, ref k, ref acc);
                Vec3 c = ctx.WorldToCreature(w);
                double r = _radius[i];
                double raiseT = DMath.Saturate(1.0 - _arc[i] / _raiseLen);
                double y = r + (_raise + lift + p.Extra1) * raiseT * raiseT;
                _joint[i] = new Vec3(c.X, y, c.Z);
            }
            // rearing: the front rises into a column from a base point on the trail, the head bent
            // forward at the top (cobras, sandworms); blends out while crawling or resting
            double rear = _rearAmount * _rear.X;
            if (rear > 1e-3)
            {
                double lr = Math.Max(4, _raiseLen * 1.6);
                int kb = 0;
                double ab = 0;
                Vec3 bw = ctx.WorldToCreature(Sample(lr, ref kb, ref ab));
                Vec3 headC = ctx.WorldToCreature(Sample(0, ref kb, ref ab));
                Vec3 fwd = new Vec3(headC.X - bw.X, 0, headC.Z - bw.Z).NormalizedOr(Vec3.UnitX);
                // heading profile from the base (t = 0) to the head (t = 1): curve up, upright column,
                // short forward bend of the neck; integrated so that arc lengths stay exact
                const int steps = 40;
                if (_rearPts.Length != steps + 1) _rearPts = new Vec3[steps + 1];
                Vec3 q = new Vec3(bw.X, 0, bw.Z);
                for (int j = 0; j <= steps; j++)
                {
                    _rearPts[j] = q;
                    double t = (j + 0.5) / steps;
                    double phi = DMath.HalfPi * DMath.SmoothStep(0, 0.3, t) - 1.75 * DMath.SmoothStep(0.8, 1.0, t);
                    q += (fwd * Math.Cos(phi) + Vec3.UnitY * Math.Sin(phi)) * (lr / steps);
                }
                for (int i = 0; i < n && _arc[i] < lr; i++)
                {
                    double t = 1.0 - _arc[i] / lr;
                    double fj = t * steps;
                    int j0 = Math.Min(steps - 1, (int)fj);
                    Vec3 rp = Vec3.Lerp(_rearPts[j0], _rearPts[j0 + 1], fj - j0) + new Vec3(0, _radius[i], 0);
                    // strikes lunge from the column: the top whips forward and down
                    if (Math.Abs(lunge) > 1e-4) rp += fwd * (lunge * t * t) - Vec3.UnitY * (Math.Max(0, lunge) * 0.5 * t * t);
                    _joint[i] = Vec3.Lerp(_joint[i], rp, rear * DMath.SmoothStep(0, 0.3, t));
                }
            }
            // coil into a resting spiral (head first)
            double coil = p.Extra0;
            if (coil > 1e-4)
            {
                for (int i = 0; i < n; i++)
                {
                    double wi = DMath.SmoothStep(0, 1, coil * 1.6 - (i / (double)Math.Max(1, n - 1)) * 0.6);
                    double r = _radius[i];
                    double onTop = DMath.Saturate(1.0 - _arc[i] / (_bodyLen * 0.22));
                    Vec3 cp = _coil[i] + new Vec3(0, r + onTop * r * 1.8, 0);
                    _joint[i] = Vec3.Lerp(_joint[i], cp, wi);
                }
            }
            Vec3 prevDir = new Vec3(-1, 0, 0);
            for (int i = 0; i < n; i++)
            {
                Vec3 dir = i + 1 < n ? _joint[i + 1] - _joint[i] : prevDir;
                dir = dir.NormalizedOr(prevDir);
                prevDir = dir;
                // upright segments keep their back facing backwards (no flips in the column)
                double ay = dir.Y * dir.Y;
                Vec3 up = (Vec3.UnitY * (1 - ay) + new Vec3(-1, 0, 0) * ay).NormalizedOr(Vec3.UnitY);
                pose.SetOverride(bones[i], new Xform(Mat3.LookAlong(dir, up), _joint[i]));
                if ((i & 1) == 0 && _joint[i].Y <= _radius[i] + 0.25)
                    output.Contacts.Add(new ContactPoint { Position = new Vec3(_joint[i].X, 0, _joint[i].Z), Planted = !_turning, Leg = i });
            }
        }

        public override double CycleFrequency(MotorContext ctx) => ctx.MoveWeight > 0.5 ? _freq : 0;
    }
}
