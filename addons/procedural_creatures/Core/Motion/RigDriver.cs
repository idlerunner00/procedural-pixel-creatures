// Procedural Pixel Creatures - turns PoseParams into bone transforms (generic for all body plans).

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public sealed class RigDriver
    {
        private readonly MotionRig _rig;
        private readonly Skeleton _skeleton;
        private readonly Vec3 _pivot;
        private readonly double _bodyHeight;
        private readonly double _lieHeight;
        private readonly SpringChain? _tail;
        private readonly List<SpringChain> _appendages = new List<SpringChain>();
        private readonly Vec3[] _tailUp;
        private readonly Dictionary<SpringChain, Vec3[]> _chainUp = new Dictionary<SpringChain, Vec3[]>();

        public SpringChain? Tail => _tail;
        public IReadOnlyList<SpringChain> Appendages => _appendages;

        public RigDriver(CreatureAnatomy anatomy, MotionTraits traits)
        {
            _rig = anatomy.Rig;
            _skeleton = anatomy.Skeleton;
            if (_rig.Pelvis >= 0 && _rig.Chest >= 0)
            {
                Vec3 p = _skeleton.RestWorld(_rig.Pelvis).Origin, c = _skeleton.RestWorld(_rig.Chest).Origin;
                _pivot = _rig.Upright ? p : (p + c) * 0.5;
                // upright bodies sit down on the pelvis; horizontal bodies lower their middle
                _bodyHeight = _rig.Upright ? p.Y : (p.Y + c.Y) * 0.5;
            }
            else if (_rig.Pelvis >= 0)
            {
                _pivot = _skeleton.RestWorld(_rig.Pelvis).Origin;
                _bodyHeight = _pivot.Y;
            }
            _lieHeight = _rig.Tune("rest.bodyHeight", _bodyHeight * 0.45);

            double stiff = DMath.Lerp(9, 30, traits.Stiffness);
            if (_rig.Tail != null && _rig.Tail.Bones.Length >= 2)
            {
                _tail = new SpringChain(_rig.Tail)
                {
                    Stiffness = stiff * DMath.Lerp(0.7, 1.3, _rig.Tail.Stiffness),
                    Damping = DMath.Lerp(2.5, 5.0, traits.Weight),
                    // swimmers and fliers override the gravity of their tail (buoyancy / airflow)
                    Gravity = _rig.Tune("tail.gravity", DMath.Lerp(20, 90, _rig.Tail.Droop)),
                };
                _tailUp = new Vec3[_rig.Tail.Bones.Length];
            }
            else _tailUp = Array.Empty<Vec3>();
            foreach (var a in _rig.Appendages)
            {
                if (a.Bones.Length < 2) continue;
                var ch = new SpringChain(a)
                {
                    Stiffness = stiff * DMath.Lerp(0.6, 1.8, a.Stiffness),
                    Damping = DMath.Lerp(3.0, 6.0, traits.Weight),
                    Gravity = DMath.Lerp(0, 120, a.Droop),
                };
                _appendages.Add(ch);
                _chainUp[ch] = new Vec3[a.Bones.Length];
            }
        }

        /// <summary>Applies body, spine, neck, head, jaw and appendage base rotations (locals only).</summary>
        public void ApplyBody(in PoseParams p, SkeletonPose pose)
        {
            // root: altitude and squash & stretch
            int root = _rig.Root;
            if (root >= 0)
            {
                pose.LocalOffset[root] = p.RootOffset + new Vec3(0, p.Altitude, 0);
                double sq = DMath.Clamp(p.Squash, -0.45, 0.45);
                if (Math.Abs(sq) > 1e-4)
                {
                    double ys = 1.0 - sq, xs = 1.0 / Math.Sqrt(ys);
                    pose.Scale[root] = new Vec3(xs, ys, xs);
                }
            }

            // pelvis: rotate the torso about the body pivot, then offset
            int pelvis = _rig.Pelvis;
            if (pelvis >= 0)
            {
                var b = _skeleton[pelvis];
                Vec3 restPos = _skeleton.RestWorld(pelvis).Origin;
                Quat r = Quat.Euler(p.BodyYaw, p.BodyPitch, p.BodyRoll);
                double crouchDrop = DMath.Saturate(p.Crouch) * Math.Max(0, _bodyHeight - _lieHeight);
                Vec3 offset = p.BodyOffset + new Vec3(0, -crouchDrop + p.Breath * 0.35, 0);
                Vec3 desired = _pivot + r.Rotate(restPos - _pivot) + offset;
                // parent of the pelvis is the root (identity rest frame)
                Vec3 parentOrigin = b.Parent >= 0 ? _skeleton.RestWorld(b.Parent).Origin : Vec3.Zero;
                pose.LocalOffset[pelvis] = desired - parentOrigin - b.RestOffset;
                pose.LocalRotation[pelvis] = b.RestRotation.Conjugate * r * b.RestRotation;
            }

            // spine bend distributed over the remaining spine bones
            if (_rig.Spine != null && _rig.Spine.Bones.Length > 1)
            {
                var bones = _rig.Spine.Bones;
                int n = bones.Length - 1;
                for (int i = 1; i < bones.Length; i++)
                {
                    pose.LocalRotation[bones[i]] = pose.LocalRotation[bones[i]] * Quat.Euler(p.SpineYaw / n, p.SpinePitch / n, 0);
                }
            }

            // neck bend (all neck bones except the head)
            if (_rig.Neck != null && _rig.Neck.Bones.Length > 1)
            {
                var bones = _rig.Neck.Bones;
                int n = bones.Length - 1;
                for (int i = 0; i < n; i++)
                {
                    pose.LocalRotation[bones[i]] = pose.LocalRotation[bones[i]] * Quat.Euler(p.NeckYaw / n, p.NeckPitch / n, 0);
                }
            }

            if (_rig.Head >= 0)
                pose.LocalRotation[_rig.Head] = pose.LocalRotation[_rig.Head] * Quat.Euler(p.HeadYaw, p.HeadPitch, p.HeadRoll);
            if (_rig.Jaw >= 0)
                pose.LocalRotation[_rig.Jaw] = Quat.RotZ(-DMath.Clamp(p.Jaw, 0, 1.2) * _rig.JawOpenMax);

            // ears: base rotation from EarPitch; tentacles and antennae: travelling sway waves
            // (the spring chains add lag and gravity on top of these goals)
            int k = 0;
            foreach (var ch in _appendages)
            {
                var bones = ch.Rig.Bones;
                var role = _skeleton[bones[0]].Role;
                if (role == BoneRole.Ear)
                {
                    pose.LocalRotation[bones[0]] = Quat.RotZ(p.EarPitch * 0.55);
                    continue;
                }
                if (role != BoneRole.Tentacle && role != BoneRole.Antenna) continue;
                double amp = (role == BoneRole.Antenna ? 0.12 : 0.34) * p.TentacleAmp;
                if (Math.Abs(amp) < 1e-6) { k++; continue; }
                double ph = p.TentaclePhase + k * 2.13 + ch.Rig.Side * 0.7;
                for (int j = 0; j + 1 < bones.Length; j++)
                {
                    double a = amp * (0.55 + 0.45 * j / Math.Max(1, bones.Length - 2));
                    double yaw = DMath.Sin(ph - j * 0.85) * a;
                    double pitch = DMath.Cos(ph * 0.9 - j * 0.7 + k) * a * 0.6;
                    pose.LocalRotation[bones[j]] = pose.LocalRotation[bones[j]] * Quat.Euler(yaw, pitch, 0);
                }
                k++;
            }

            foreach (var w in _rig.Wings) ApplyWing(w, p, pose);
            foreach (var f in _rig.Fins)
            {
                if (f.Bone < 0) continue;
                double a = f.RestAngle + f.Amplitude * p.FinAmp * DMath.Sin(p.FinPhase + f.Phase);
                pose.LocalRotation[f.Bone] = pose.LocalRotation[f.Bone] * Quat.AxisAngle(f.Axis, a);
            }
            foreach (var g in _rig.Grippers)
            {
                if (g.Bone < 0) continue;
                pose.LocalRotation[g.Bone] = Quat.AxisAngle(g.Axis, DMath.Clamp(p.Grip, -0.15, 1.2) * g.OpenAngle);
            }
            if (_rig.Tongue >= 0)
                pose.LocalOffset[_rig.Tongue] = new Vec3(_rig.TongueLength * DMath.Clamp(p.Tongue, 0, 1), 0, 0);
        }

        /// <summary>
        /// Folding and flapping of a wing built in its spread pose (local +X outwards, +Y up).
        /// Raise = rotation about local Z (towards +Y) for both sides; sweep = rotation about local Y
        /// whose sign depends on the side (local Z points backwards on the right wing, forwards on the left).
        /// </summary>
        private static void ApplyWing(WingRig w, in PoseParams p, SkeletonPose pose)
        {
            double fold = DMath.Clamp(p.WingFold, 0, 1);
            double open = 1 - fold;
            double flap = DMath.Clamp(p.WingFlap, -1.4, 1.4);
            double s = w.Side < 0 ? -1 : 1;
            double raise = (flap < 0 ? -flap * w.FlapUp : -flap * w.FlapDown) * DMath.Lerp(0.2, 1.0, open);
            if (w.UseFoldTargets)
            {
                // natural fold angles plus a correction that eases in towards the designed folded pose
                double e = fold * fold * (3 - 2 * fold);
                if (w.ArmBone >= 0)
                    pose.LocalRotation[w.ArmBone] = pose.LocalRotation[w.ArmBone] * Quat.RotY(-s * w.FoldSweep * fold) * Quat.RotZ(-w.FoldDrop * fold)
                        * Quat.Nlerp(Quat.Identity, w.ArmFix, e) * Quat.RotY(s * p.WingSweep) * Quat.RotZ(raise);
                if (w.ForearmBone >= 0)
                    pose.LocalRotation[w.ForearmBone] = pose.LocalRotation[w.ForearmBone] * Quat.RotY(s * w.ElbowSign * w.FoldElbow * fold)
                        * Quat.Nlerp(Quat.Identity, w.ForeFix, e) * Quat.RotZ(-raise * 0.12);
                if (w.HandBone >= 0)
                    pose.LocalRotation[w.HandBone] = pose.LocalRotation[w.HandBone] * Quat.RotY(-s * w.ElbowSign * w.FoldWrist * fold)
                        * Quat.Nlerp(Quat.Identity, w.HandFix, e) * Quat.RotZ(p.WingTwist * 0.4 * open);
                for (int i = 0; i < w.FingerBones.Length; i++)
                {
                    double bunch = (i - (w.FingerBones.Length - 1) * 0.5) * 0.28 * fold;
                    pose.LocalRotation[w.FingerBones[i]] = pose.LocalRotation[w.FingerBones[i]] * Quat.RotY(s * bunch);
                }
                return;
            }
            double drop = w.FoldDrop * fold;
            double sweepBack = w.FoldSweep * fold - p.WingSweep;
            if (w.ArmBone >= 0)
                pose.LocalRotation[w.ArmBone] = pose.LocalRotation[w.ArmBone] * Quat.RotY(-s * sweepBack) * Quat.RotZ(raise - drop);
            if (w.ForearmBone >= 0)
                pose.LocalRotation[w.ForearmBone] = pose.LocalRotation[w.ForearmBone] * Quat.RotY(s * w.ElbowSign * w.FoldElbow * fold) * Quat.RotZ(-raise * 0.12);
            if (w.HandBone >= 0)
                pose.LocalRotation[w.HandBone] = pose.LocalRotation[w.HandBone] * Quat.RotY(-s * w.ElbowSign * w.FoldWrist * fold) * Quat.RotZ(p.WingTwist * 0.4 * open);
            for (int i = 0; i < w.FingerBones.Length; i++)
            {
                // fingers bunch together when folded
                double bunch = (i - (w.FingerBones.Length - 1) * 0.5) * 0.28 * fold;
                pose.LocalRotation[w.FingerBones[i]] = pose.LocalRotation[w.FingerBones[i]] * Quat.RotY(s * bunch);
            }
        }

        // ------------------------------------------------------------------ chains

        /// <summary>Computes chain goals from the solved pose and steps the chain simulations.</summary>
        public void StepChains(MotorContext ctx, in PoseParams p, SkeletonPose pose, double dt)
        {
            if (_tail != null) { TailGoals(ctx, p, pose); _tail.Step(dt); }
            foreach (var ch in _appendages)
            {
                var up = _chainUp[ch];
                for (int i = 0; i < ch.Rig.Bones.Length; i++)
                {
                    var w = pose.World[ch.Rig.Bones[i]];
                    ch.Goal[i] = ctx.CreatureToWorld(w.Origin);
                    up[i] = w.Basis.C1.NormalizedOr(Vec3.UnitY);
                }
                ch.GroundY = 0;
                ch.Step(dt);
            }
        }

        public void ResetChains(MotorContext ctx, in PoseParams p, SkeletonPose pose)
        {
            if (_tail != null) { TailGoals(ctx, p, pose); _tail.Reset(); }
            foreach (var ch in _appendages)
            {
                for (int i = 0; i < ch.Rig.Bones.Length; i++) ch.Goal[i] = ctx.CreatureToWorld(pose.World[ch.Rig.Bones[i]].Origin);
                ch.Reset();
            }
        }

        private void TailGoals(MotorContext ctx, in PoseParams p, SkeletonPose pose)
        {
            var tail = _tail!;
            var rig = tail.Rig;
            var bones = rig.Bones;
            var baseXf = pose.World[bones[0]];
            Vec3 pos = baseXf.Origin;
            Vec3 dir = baseXf.Basis.C0.NormalizedOr(new Vec3(-1, 0, 0));
            Vec3 up = baseXf.Basis.C1.NormalizedOr(Vec3.UnitY);
            int n = bones.Length - 1;
            // base carriage
            Vec3 side = Vec3.Cross(dir, up).NormalizedOr(Vec3.UnitZ);
            dir = Quat.AxisAngle(side, p.TailPitch).Rotate(dir);
            dir = Quat.AxisAngle(Vec3.UnitY, p.TailYaw * 0.6).Rotate(dir);
            tail.Goal[0] = ctx.CreatureToWorld(pos);
            _tailUp[0] = up;
            for (int i = 0; i < n; i++)
            {
                double len = rig.Lengths[i];
                pos = pos + dir * len;
                tail.Goal[i + 1] = ctx.CreatureToWorld(pos);
                // bend for the next segment: rest shape + curl + distributed yaw (swish)
                side = Vec3.Cross(dir, up).NormalizedOr(Vec3.UnitZ);
                double pitchStep = (i + 1 < rig.RestPitch.Length ? rig.RestPitch[i + 1] : 0) + p.TailCurl / n;
                double yawStep = (i + 1 < rig.RestYaw.Length ? rig.RestYaw[i + 1] : 0) + p.TailYaw * 0.4 / n;
                dir = Quat.AxisAngle(side, pitchStep).Rotate(dir);
                dir = Quat.AxisAngle(Vec3.UnitY, yawStep).Rotate(dir).Normalized();
                up = Vec3.Cross(side, dir).NormalizedOr(Vec3.UnitY);
                _tailUp[i + 1] = up;
            }
            tail.GroundY = 0;
        }

        /// <summary>Writes simulated chain positions into the pose as world overrides.</summary>
        public void ApplyChains(MotorContext ctx, SkeletonPose pose)
        {
            if (_tail != null && _tail.Initialized) Apply(ctx, _tail, _tailUp, pose);
            foreach (var ch in _appendages)
                if (ch.Initialized) Apply(ctx, ch, _chainUp[ch], pose);
        }

        private static void Apply(MotorContext ctx, SpringChain ch, Vec3[] ups, SkeletonPose pose)
        {
            var bones = ch.Rig.Bones;
            int n = bones.Length;
            Vec3 prevDir = Vec3.UnitX;
            for (int i = 0; i < n; i++)
            {
                Vec3 pc = ctx.WorldToCreature(ch.P[i]);
                Vec3 dir;
                if (i + 1 < n) dir = ctx.WorldToCreature(ch.P[i + 1]) - pc;
                else dir = prevDir;
                dir = dir.NormalizedOr(prevDir);
                prevDir = dir;
                Vec3 up = ups[i].LengthSquared > 0.5 ? ups[i] : Vec3.UnitY;
                var basis = Mat3.LookAlong(dir, up);
                if (i == 0)
                {
                    // the base keeps its animated position; only orientation follows the chain
                    pc = pose.World[bones[0]].Origin;
                }
                pose.SetOverride(bones[i], new Xform(basis, pc));
            }
        }
    }
}
