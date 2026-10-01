// Procedural Pixel Creatures - batched rendering of creature frames on worker threads.
//
// Actors enqueue themselves during _Process. Right before Godot draws the frame (FramePreDraw on the
// main thread) all pending jobs are rasterized in parallel with thread-local renderers (pure C#,
// no engine calls), then textures are uploaded on the main thread. Results are identical to
// sequential rendering because every job is independent and deterministic.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core.Rendering;

namespace PixelCreatures.Runtime
{
    public enum RenderQueueMode
    {
        /// <summary>Jobs are flushed in parallel right before the frame is drawn (default).</summary>
        Deferred,
        /// <summary>Every job renders immediately on the main thread (tests, headless tools).</summary>
        Immediate,
    }

    public static class CreatureRenderQueue
    {
        private static readonly List<CreatureActor> Pending = new List<CreatureActor>();
        private static bool _connected;
        [ThreadStatic] private static CreatureRenderer? _threadRenderer;

        public static RenderQueueMode Mode = RenderQueueMode.Deferred;
        public static int MaxParallelism = Math.Max(1, System.Environment.ProcessorCount - 1);

        // statistics (main thread)
        public static int LastBatchSize { get; private set; }
        public static double LastRasterMs { get; private set; }
        public static double LastUploadMs { get; private set; }
        public static long TotalRenders { get; private set; }
        public static long TotalUploads { get; private set; }

        public static CreatureRenderer ThreadRenderer => _threadRenderer ??= new CreatureRenderer();

        internal static void Enqueue(CreatureActor actor)
        {
            if (Mode == RenderQueueMode.Immediate)
            {
                var sw = Stopwatch.StartNew();
                actor.RenderToBuffer(ThreadRenderer);
                LastRasterMs = sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                actor.UploadTexture();
                LastUploadMs = sw.Elapsed.TotalMilliseconds;
                LastBatchSize = 1;
                TotalRenders++;
                TotalUploads++;
                return;
            }
            if (!_connected)
            {
                RenderingServer.FramePreDraw += Flush;
                // fallback for servers that never draw (headless, --self-test, dedicated servers):
                // jobs still pending when the next frame starts are flushed then
                if (Engine.GetMainLoop() is SceneTree tree) tree.ProcessFrame += FlushStale;
                _connected = true;
            }
            if (actor.QueuedForRender) return;
            actor.QueuedForRender = true;
            Pending.Add(actor);
            _enqueueFrame = Engine.GetProcessFrames();
        }

        private static ulong _enqueueFrame;

        private static void FlushStale()
        {
            if (Pending.Count > 0 && Engine.GetProcessFrames() != _enqueueFrame) Flush();
        }

        internal static void Remove(CreatureActor actor)
        {
            if (!actor.QueuedForRender) return;
            Pending.Remove(actor);
            actor.QueuedForRender = false;
        }

        /// <summary>Renders all pending jobs. Called automatically before drawing; callable manually.</summary>
        public static void Flush()
        {
            int n = Pending.Count;
            if (n == 0) return;
            var jobs = Pending.ToArray();
            Pending.Clear();
            var sw = Stopwatch.StartNew();
            if (n == 1 || MaxParallelism <= 1)
            {
                foreach (var a in jobs) a.RenderToBuffer(ThreadRenderer);
            }
            else
            {
                Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism }, i => jobs[i].RenderToBuffer(ThreadRenderer));
            }
            LastRasterMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            foreach (var a in jobs)
            {
                a.QueuedForRender = false;
                if (GodotObject.IsInstanceValid(a) && a.IsInsideTree()) a.UploadTexture();
            }
            LastUploadMs = sw.Elapsed.TotalMilliseconds;
            LastBatchSize = n;
            TotalRenders += n;
            TotalUploads += n;
        }
    }
}
