// Procedural Pixel Creatures - simulated chains for follow-through (tails, ears, antennae, tentacles).
//
// Positions live in world space so the chain reacts to root motion and turning: it drags behind on
// acceleration, swings through on stops and settles with overshoot. Each joint is attracted to its
// animated goal (stiffer near the base), then segment lengths are restored front to back
// (follow-the-leader), and joints are kept above the ground. Fixed time step => deterministic.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class SpringChain
    {
        public readonly ChainRig Rig;
        public readonly Vec3[] P;
        public readonly Vec3[] Prev;
        public readonly Vec3[] Goal;
        private readonly double[] _len;
        private bool _initialized;

        /// <summary>Base stiffness (goal attraction per second, higher = stiffer).</summary>
        public double Stiffness = 18;
        /// <summary>Velocity damping per second (0 = none).</summary>
        public double Damping = 3.5;
        public double Gravity = 60;
        public double GroundY;

        public SpringChain(ChainRig rig)
        {
            Rig = rig;
            int n = rig.Bones.Length;
            P = new Vec3[n];
            Prev = new Vec3[n];
            Goal = new Vec3[n];
            _len = new double[Math.Max(0, n - 1)];
            for (int i = 0; i < _len.Length; i++) _len[i] = i < rig.Lengths.Length ? rig.Lengths[i] : 1;
        }

        public int Count => P.Length;
        public bool Initialized => _initialized;

        public void Reset()
        {
            for (int i = 0; i < P.Length; i++) { P[i] = Goal[i]; Prev[i] = Goal[i]; }
            _initialized = true;
        }

        /// <summary>Advances the chain. Goal[] must be filled (world space) before calling.</summary>
        public void Step(double dt)
        {
            if (!_initialized) { Reset(); return; }
            int n = P.Length;
            P[0] = Goal[0];
            Prev[0] = Goal[0];
            double damp = Math.Exp(-Damping * dt);
            for (int i = 1; i < n; i++)
            {
                double t = n > 1 ? i / (double)(n - 1) : 1;
                // inertia
                Vec3 vel = (P[i] - Prev[i]) * damp;
                Prev[i] = P[i];
                Vec3 p = P[i] + vel + new Vec3(0, -Gravity * dt * dt * (0.4 + 0.6 * t), 0);
                // goal attraction: stiff near the base, looser at the tip
                double k = Stiffness * DMath.Lerp(1.6, 0.55, t);
                double alpha = 1.0 - Math.Exp(-k * dt);
                p = Vec3.Lerp(p, Goal[i], alpha);
                P[i] = p;
            }
            // restore segment lengths (follow the leader)
            for (int i = 1; i < n; i++)
            {
                Vec3 d = P[i] - P[i - 1];
                double l = d.Length;
                if (l < 1e-9) d = Goal[i] - Goal[i - 1];
                l = d.Length;
                if (l < 1e-9) continue;
                P[i] = P[i - 1] + d * (_len[i - 1] / l);
                double r = Rig.Radii.Length > i ? Rig.Radii[i] : 0.5;
                if (P[i].Y < GroundY + r)
                {
                    P[i] = new Vec3(P[i].X, GroundY + r, P[i].Z);
                }
            }
        }

        /// <summary>Adds a velocity impulse to all joints (e.g. hit reactions), scaled towards the tip.</summary>
        public void Impulse(Vec3 dv, double dt)
        {
            int n = P.Length;
            for (int i = 1; i < n; i++)
            {
                double t = i / (double)Math.Max(1, n - 1);
                Prev[i] = Prev[i] - dv * (dt * t);
            }
        }
    }
}
