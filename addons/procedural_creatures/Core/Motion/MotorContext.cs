// Procedural Pixel Creatures - shared motor state visible to all motion modules.

using System;
using PixelCreatures.Core.Anatomy;

namespace PixelCreatures.Core.Motion
{
    public enum FacingMode
    {
        /// <summary>Four cardinal directions (E, N, W, S).</summary>
        Four,
        /// <summary>Eight directions (default pixel-art look).</summary>
        Eight,
        /// <summary>Continuous yaw.</summary>
        Free,
    }

    public enum MotionStateKind
    {
        Idle,
        Walk,
        Run,
        Action,
        Hit,
        Rest,
        Transition,
    }

    public sealed class MotorContext
    {
        public readonly CreatureModel Model;
        public readonly CreatureAnatomy Anatomy;
        public readonly MotionRig Rig;
        public readonly MotionTraits Traits;

        /// <summary>Simulation time in seconds.</summary>
        public double Time;
        /// <summary>World position of the root on the ground plane (Y = ground height, usually 0).</summary>
        public Vec3 Position;
        public double Yaw;
        public double TargetYaw;
        public double YawRate;
        public Vec3 Velocity;
        public Vec3 Acceleration;
        public double Speed;
        /// <summary>Speed component along the facing direction.</summary>
        public double ForwardSpeed;
        /// <summary>0 = idle stance, 1 = fully in locomotion (smoothed).</summary>
        public double MoveWeight;
        /// <summary>0 = walk, 1 = run (smoothed by speed).</summary>
        public double RunBlend;
        /// <summary>0..1 how far the creature is into its rest pose.</summary>
        public double RestWeight;
        public bool RestExiting;
        /// <summary>Action envelope 0..1 (locomotion is damped while acting).</summary>
        public double ActionWeight;
        /// <summary>Root altitude (flight, jumps, swimming offset).</summary>
        public double Altitude;
        /// <summary>Surface level for swimming creatures (NaN = land).</summary>
        public double WaterLevel = double.NaN;
        /// <summary>Optional world-space terrain height for foot placement; null preserves flat-ground poses.</summary>
        public Func<double, double, double>? GroundHeightAt;
        /// <summary>Forces the locomotion cycle frequency (exporters use it for seamless loops). NaN = natural.</summary>
        public double CycleFrequencyOverride = double.NaN;
        /// <summary>Export mode: no random gestures, periodic blink and breathing with an exact loop length.</summary>
        public bool ExportMode;
        /// <summary>Loop length in seconds that idle cycles (breathing, blink) must divide in export mode.</summary>
        public double ExportLoop = 2.0;

        private Mat3 _yawMat = Mat3.Identity;
        private Mat3 _yawInv = Mat3.Identity;
        private double _yawCached = double.NaN;

        public MotorContext(CreatureModel model)
        {
            Model = model;
            Anatomy = model.Anatomy;
            Rig = model.Anatomy.Rig;
            Traits = model.Motion;
        }

        public Mat3 YawMatrix
        {
            get
            {
                if (_yawCached != Yaw)
                {
                    _yawMat = PixelCamera.YawRotation(Yaw);
                    _yawInv = _yawMat.Transposed;
                    _yawCached = Yaw;
                }
                return _yawMat;
            }
        }

        public Vec3 CreatureToWorld(Vec3 c) => Position + YawMatrix.Mul(c);

        public Vec3 WorldToCreature(Vec3 w)
        {
            _ = YawMatrix;
            return _yawInv.Mul(w - Position);
        }

        public Vec3 WorldDirToCreature(Vec3 d)
        {
            _ = YawMatrix;
            return _yawInv.Mul(d);
        }

        public Vec3 Forward => YawMatrix.Mul(Vec3.UnitX);

        /// <summary>Deterministic hash in [0,1) for scheduling events.</summary>
        public double Hash(int a, int b = 0, int c = 0) => StableHash.Hash01(Traits.Seed, a, b, c);
    }
}
