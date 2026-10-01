using System;
using Godot;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    /// <summary>World anchored surface currents and bubbles above submerged creatures.</summary>
    public partial class HabitatEffects : Node2D
    {
        public TestArea Area = null!;
        private double _time;
        public override void _Process(double delta) { _time += delta; QueueRedraw(); }

        public override void _Draw()
        {
            if (Area.Map == null) return;
            var map = Area.Map;
            var view = Area.ViewRect.Grow(40);
            foreach (var pond in map.Ponds)
            {
                var bounds = pond.Bounds;
                if (!view.Intersects(new Rect2(bounds.Position, bounds.Size))) continue;
                for (int k = 0; k < 18; k++)
                {
                    float x = (float)(pond.Cx + Math.Sin(k * 9.31) * pond.Rx * 0.85 + Math.Sin(_time * 0.32 + k) * 4);
                    float y = (float)(pond.Cy + Math.Cos(k * 4.13) * pond.Ry * 0.82);
                    if (!map.IsWater((int)x - 7, (int)y) || !map.IsWater((int)x + 7, (int)y)) continue;
                    float alpha = (float)(0.10 + 0.10 * Math.Sin(_time * 1.1 + k));
                    DrawLine(new Vector2(x - 5, y), new Vector2(x + 5, y), new Color(0.63f, 0.90f, 0.83f, alpha));
                    DrawLine(new Vector2(x - 2, y + 1), new Vector2(x + 8, y + 1), new Color(0.45f, 0.75f, 0.76f, alpha * 0.5f));
                }
            }
            foreach (var a in Area.AllActors())
            {
                if (a.Model?.Anatomy.Rig.Locomotion != LocomotionKind.Swim || a.IsCulled) continue;
                var p = GodotConvert.GroundToScreen(a.GroundPosition.X, a.GroundPosition.Y);
                var direction = a.MoveVelocity.Normalized();
                for (int k = 0; k < 3; k++)
                {
                    double phase = (_time * 0.48 + k * 0.31 + a.Genome!.ContentHash % 7 * 0.13) % 1;
                    var b = p - new Vector2(direction.X, direction.Y * 0.5f) * (float)(5 + phase * 14) + new Vector2(k * 3 - 3, (float)(-phase * 7));
                    if (!map.IsWater((int)b.X, (int)b.Y)) continue;
                    DrawCircle(b.Round(), phase > 0.5 ? 1 : 0.65f, new Color(0.73f, 0.95f, 0.9f, (float)(0.48 * (1 - phase))), false);
                }
            }
        }
    }
}
