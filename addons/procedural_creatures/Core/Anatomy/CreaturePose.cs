// Procedural Pixel Creatures - full creature pose: skeleton + facial expression + debug contacts.

using System.Collections.Generic;

namespace PixelCreatures.Core.Anatomy
{
    public enum EyeMood : byte
    {
        Neutral,
        Angry,
        Pain,
        Sleep,
        Happy,
        Alert,
    }

    public struct ExpressionState
    {
        /// <summary>0 = closed, 1 = fully open.</summary>
        public double EyeOpen;
        public EyeMood Mood;
        /// <summary>Pupil look offset in creature space (x forward, y up), each -1..1.</summary>
        public double LookX, LookY;
        /// <summary>0..1 white flash (hit feedback).</summary>
        public double Flash;

        public static ExpressionState Default => new ExpressionState { EyeOpen = 1, Mood = EyeMood.Neutral };
    }

    public struct ContactPoint
    {
        public Vec3 Position;
        public bool Planted;
        public int Leg;
    }

    public sealed class CreaturePose
    {
        public readonly SkeletonPose Skeleton;
        public ExpressionState Expression = ExpressionState.Default;
        public readonly List<ContactPoint> Contacts = new List<ContactPoint>();
        /// <summary>Height of the root above ground (hover, jumps, swimming). Informational for shadows/debug.</summary>
        public double Altitude;

        public CreaturePose(Skeleton skeleton)
        {
            Skeleton = new SkeletonPose(skeleton);
        }

        public void CopyFrom(CreaturePose other)
        {
            Skeleton.CopyFrom(other.Skeleton);
            Expression = other.Expression;
            Altitude = other.Altitude;
            Contacts.Clear();
            Contacts.AddRange(other.Contacts);
        }
    }
}
