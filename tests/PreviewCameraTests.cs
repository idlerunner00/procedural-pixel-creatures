using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Runtime;
using PixelCreatures.Workshop;

namespace PixelCreatures.Tests
{
    public partial class SelfTest
    {
        private async Task PreviewCameraStaysFixed(TestContext ctx)
        {
            Main.EmbeddedForTests = true;
            var host = new SubViewport { Size = new Vector2I(1600, 940), Disable3D = true };
            AddChild(host);
            var main = GD.Load<PackedScene>("res://workshop/Main.tscn").Instantiate<Main>();
            host.AddChild(main);
            try
            {
                await Frames(12);
                IEnumerable<Node> Descendants(Node node)
                {
                    foreach (var child in node.GetChildren())
                    {
                        yield return child;
                        foreach (var nested in Descendants(child)) yield return nested;
                    }
                }
                var stages = Descendants(main).OfType<PixelStage>().ToArray();
                ctx.True(stages.Length >= 7 && stages.All(s => s.FixedFraming), "editor, offspring, breeding and inspector use fixed framing");
                ctx.True(Descendants(main).OfType<CreatureActor>().All(a => !a.SyncNodePosition), "workshop actors animate in place");

                var stage = main.PreviewStage;
                var actor = main.PreviewActor;
                // Drive a fixed amount of simulated time, independent of headless frame throughput.
                stage.ProcessMode = ProcessModeEnum.Disabled;
                int checkedFrames = 0;
                foreach (var family in CreatureRuntime.Registry.Families)
                {
                    actor.Resting = false;
                    actor.SetGenome(CreatureRuntime.Sample(family.Id, 26), async: false);
                    stage.SetZoom(0);
                    stage._Process(0);
                    var camera = stage.Camera.Position;
                    var viewport = stage.Viewport.Size;
                    var position = actor.Position;
                    var start = actor.GroundPosition;
                    long renders = actor.RenderCount;
                    for (int frame = 0; frame < 180; frame++)
                    {
                        int direction = frame / 22 % 8;
                        actor.MoveVelocity = AnimationPanel.DirVector(direction) * (float)(frame < 60 ? actor.Model!.Motion.WalkSpeed : actor.Model!.Motion.RunSpeed);
                        if (frame == 70) actor.TriggerAction();
                        if (frame == 100) actor.TriggerHit(Vector2.Right, 1);
                        if (frame == 130) actor.Resting = true;
                        if (frame == 165) actor.Resting = false;
                        actor._Process(1.0 / 30);
                        stage._Process(1.0 / 30);
                        await NextFrame();
                        ctx.Equal(camera, stage.Camera.Position, $"{family.Id}: camera moved during animation");
                        ctx.Equal(viewport, stage.Viewport.Size, $"{family.Id}: automatic zoom changed during animation");
                        ctx.Equal(position, actor.Position, $"{family.Id}: preview root drifted");
                        checkedFrames++;
                    }
                    ctx.True(actor.GroundPosition.DistanceTo(start) > 1 && actor.RenderCount > renders + 10, $"{family.Id}: locomotion and rendering must continue with a fixed camera");
                    stage.SetZoom(1);
                    var zoomedOut = stage.Viewport.Size;
                    stage.SetZoom(4);
                    stage._Process(0);
                    ctx.True(stage.Viewport.Size.X < zoomedOut.X, "explicit zoom remains available");
                    ctx.Equal(camera, stage.Camera.Position, "manual zoom preserves the camera anchor");
                }
                ctx.Metric("stableAnimationFrames", checkedFrames);
                ctx.Metric("fixedPreviewStages", stages.Length);
            }
            finally
            {
                main.QueueFree();
                host.QueueFree();
                Main.EmbeddedForTests = false;
                await Frames(3);
            }
        }
    }
}
