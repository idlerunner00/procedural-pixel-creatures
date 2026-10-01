// Procedural Pixel Creatures - camera projection shared by renderer, anatomy canvas sizing and tools.
//
// World space: +X east (screen right), +Y up, +Z south (towards the viewer). The camera looks north
// and down with a fixed elevation (oblique top-down game view). Projection is orthographic, so one
// world unit is one logical pixel horizontally; heights are foreshortened by cos(elevation) and
// ground depth by sin(elevation).
// View space: +x right, +y up on screen, +z towards the viewer (larger z = closer).

using System;

namespace PixelCreatures.Core
{
    public static class PixelCamera
    {
        /// <summary>Camera elevation above the horizon (30 degrees: classic 3/4 RPG view).</summary>
        public const double DefaultElevation = 30.0 * DMath.Deg2Rad;

        /// <summary>World space direction towards the key light: steep from the upper left, barely towards the viewer, so backs are lit, flanks mid-tone and bellies in shadow.</summary>
        public static readonly Vec3 WorldLight = new Vec3(-0.47, 0.87, 0.06).Normalized();

        /// <summary>Rotation that maps world coordinates into view coordinates.</summary>
        public static Mat3 WorldToView(double elevation)
        {
            DMath.SinCos(elevation, out double s, out double c);
            // rows: x' = x ; y' = y c - z s ; z' = y s + z c   -> stored as columns of the matrix
            return new Mat3(new Vec3(1, 0, 0), new Vec3(0, c, s), new Vec3(0, -s, c));
        }

        /// <summary>Rotation of the creature about the world up axis. Yaw 0 faces east (+X), 90 degrees faces north.</summary>
        public static Mat3 YawRotation(double yaw) => Mat3.FromQuat(Quat.RotY(yaw));

        /// <summary>Canonical yaw of one of eight directions (0 = E, 1 = NE, 2 = N, ..., 6 = S, 7 = SE).</summary>
        public static double DirectionYaw(int dir8) => ((dir8 % 8 + 8) % 8) * (DMath.Pi / 4.0);

        /// <summary>Nearest of the eight directions for a yaw angle.</summary>
        public static int YawToDirection8(double yaw)
        {
            double a = DMath.WrapAngle(yaw);
            int d = DMath.RoundToInt(a / (DMath.Pi / 4.0));
            return ((d % 8) + 8) % 8;
        }

        /// <summary>Nearest of the four cardinal directions (0 = E, 2 = N, 4 = W, 6 = S) for a yaw angle.</summary>
        public static int YawToDirection4(double yaw)
        {
            double a = DMath.WrapAngle(yaw);
            int d = DMath.RoundToInt(a / (DMath.Pi / 2.0));
            return (((d % 4) + 4) % 4) * 2;
        }

        public static readonly string[] DirectionNames = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };
        public static readonly string[] DirectionLabels = { "East", "Northeast", "North", "Northwest", "West", "Southwest", "South", "Southeast" };

        /// <summary>Projects a creature-space point to canvas pixels relative to the canvas origin (y down).</summary>
        public static Vec2 CreatureToScreen(Vec3 p, double yaw, double elevation, Vec3 rootOffset)
        {
            Vec3 v = WorldToView(elevation).Mul(YawRotation(yaw).Mul(p) + rootOffset);
            return new Vec2(v.X, -v.Y);
        }

        /// <summary>Screen-space movement direction (y down) to a world yaw.</summary>
        public static double ScreenDirectionToYaw(double sx, double sy) => DMath.Atan2(sy, sx);

        /// <summary>World ground velocity (x east, z south) to yaw (0 = east, 90 degrees = north).</summary>
        public static double GroundVelocityToYaw(double vx, double vz) => DMath.Atan2(-vz, vx);
    }

    /// <summary>Everything the renderer needs to place a posed creature into its canvas.</summary>
    public struct ViewSpec
    {
        /// <summary>Creature facing yaw in world space.</summary>
        public double Yaw;
        public double Elevation;
        /// <summary>World offset of the creature root relative to the canvas origin (sub-pixel motion, altitude).</summary>
        public Vec3 RootOffset;
        /// <summary>Water surface height in creature space (NaN = no water).</summary>
        public double WaterLevel;
        /// <summary>0..1 white hit flash.</summary>
        public double Flash;
        /// <summary>Render quality 0 (low) .. 2 (high). Primitives with MinQuality above are skipped.</summary>
        public int Quality;
        public bool DrawShadow;

        public static ViewSpec Default(double yaw) => new ViewSpec
        {
            Yaw = yaw,
            Elevation = PixelCamera.DefaultElevation,
            RootOffset = Vec3.Zero,
            WaterLevel = double.NaN,
            Flash = 0,
            Quality = 2,
            DrawShadow = true,
        };
    }
}
