// Procedural Pixel Creatures - spritesheet export from the live pipeline (same motor, same renderer).
//
// Each clip is simulated with a fresh motor in export mode and captured at an integer number of
// simulation steps per frame. Looping clips (idle, walk, run, sleep) get an exact period: the
// locomotion cycle frequency is locked to fps/N and idle breathing/blinking is quantized to the loop,
// so the last frame flows into the first. All frames share one frame size and one origin (the ground
// point below the root); root motion speed is written to the metadata so playback moves the sprite
// without foot sliding.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Rendering;

namespace PixelCreatures.Core.Export
{
    public enum ClipKind
    {
        Idle,
        Walk,
        Run,
        Action,
        Hit,
        RestEnter,
        Sleep,
        RestExit,
    }

    public sealed class SpritesheetOptions
    {
        public List<ClipKind> Clips = new List<ClipKind> { ClipKind.Idle, ClipKind.Walk, ClipKind.Run, ClipKind.Action, ClipKind.Hit, ClipKind.RestEnter, ClipKind.Sleep, ClipKind.RestExit };
        /// <summary>Directions as dir8 indices (0 = E, 2 = N, 4 = W, 6 = S).</summary>
        public List<int> Directions = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        /// <summary>Frames per second. Must divide 60 (6, 10, 12, 15, 20, 30, 60).</summary>
        public int Fps = 12;
        public int Padding = 1;
        public int Quality = 2;
        public bool DrawShadow = true;
        /// <summary>Largest allowed sheet page edge (Godot's default texture limit is 16384).</summary>
        public int MaxPageSize = 8192;
    }

    public sealed class ExportedFrame
    {
        public PixelFrame Frame = null!;
        public double Flash;
        public int Page, X, Y;
        public int DurationMs;
    }

    public sealed class ExportedClip
    {
        public string Name = string.Empty;
        public ClipKind Kind;
        public int Direction;
        public bool Loop;
        public int Fps;
        /// <summary>Root motion speed in ground pixels/second along the facing direction.</summary>
        public double RootSpeed;
        public readonly List<ExportedFrame> Frames = new List<ExportedFrame>();
    }

    public sealed class SpritesheetResult
    {
        public int FrameWidth, FrameHeight, OriginX, OriginY;
        public readonly List<ExportedClip> Clips = new List<ExportedClip>();
        public readonly List<(byte[] rgba, int width, int height)> Pages = new List<(byte[], int, int)>();
        public string MetadataJson = string.Empty;
        public int TotalFrames => Clips.Sum(c => c.Frames.Count);
    }

    public static class SpritesheetExporter
    {
        public static string ClipName(ClipKind k) => k switch
        {
            ClipKind.Idle => "idle",
            ClipKind.Walk => "walk",
            ClipKind.Run => "run",
            ClipKind.Action => "action",
            ClipKind.Hit => "hit",
            ClipKind.RestEnter => "rest_enter",
            ClipKind.Sleep => "sleep",
            _ => "rest_exit",
        };

        public static SpritesheetResult Export(CreatureModel model, CreatureRegistry registry, SpritesheetOptions opt, string imageBaseName,
            CancellationToken cancel = default, IProgress<double>? progress = null)
        {
            if (60 % opt.Fps != 0) throw new ArgumentException("Fps muss 60 teilen (6, 10, 12, 15, 20, 30, 60).");
            var result = new SpritesheetResult();
            var renderer = new CreatureRenderer();
            const int extra = 24;
            var baseCanvas = model.Anatomy.Canvas;
            int cw = baseCanvas.Width + extra * 2, ch = baseCanvas.Height + extra * 2;
            int ox = baseCanvas.OriginX + extra, oy = baseCanvas.OriginY + extra;
            int total = opt.Clips.Count * opt.Directions.Count;
            int done = 0;
            int ux0 = int.MaxValue, uy0 = int.MaxValue, ux1 = 0, uy1 = 0;
            foreach (var kind in opt.Clips)
            {
                foreach (int dir in opt.Directions)
                {
                    cancel.ThrowIfCancellationRequested();
                    var clip = CaptureClip(model, registry, kind, dir, opt, renderer, cw, ch, ox, oy, cancel);
                    foreach (var f in clip.Frames)
                    {
                        if (f.Frame.ContentBounds(out int x0, out int y0, out int x1, out int y1))
                        {
                            ux0 = Math.Min(ux0, x0); uy0 = Math.Min(uy0, y0); ux1 = Math.Max(ux1, x1); uy1 = Math.Max(uy1, y1);
                        }
                    }
                    result.Clips.Add(clip);
                    done++;
                    progress?.Report(done / (double)total * 0.9);
                }
            }
            if (ux1 <= ux0) { ux0 = 0; uy0 = 0; ux1 = 1; uy1 = 1; }
            int pad = Math.Max(0, opt.Padding);
            int fw = ux1 - ux0 + pad * 2, fh = uy1 - uy0 + pad * 2;
            result.FrameWidth = fw;
            result.FrameHeight = fh;
            result.OriginX = ox - ux0 + pad;
            result.OriginY = oy - uy0 + pad;

            // layout: one row per clip, rows are packed into pages below the size limit
            int maxFrames = Math.Max(1, result.Clips.Max(c => c.Frames.Count));
            int pageW = maxFrames * fw;
            int rowsPerPage = Math.Max(1, opt.MaxPageSize / fh);
            int pages = (result.Clips.Count + rowsPerPage - 1) / rowsPerPage;
            var lut = new byte[1024];
            var tmp = new byte[cw * ch * 4];
            for (int p = 0; p < pages; p++)
            {
                int rows = Math.Min(rowsPerPage, result.Clips.Count - p * rowsPerPage);
                int pageH = rows * fh;
                var rgba = new byte[pageW * pageH * 4];
                for (int r = 0; r < rows; r++)
                {
                    var clip = result.Clips[p * rowsPerPage + r];
                    for (int i = 0; i < clip.Frames.Count; i++)
                    {
                        var f = clip.Frames[i];
                        f.Page = p;
                        f.X = i * fw;
                        f.Y = r * fh;
                        model.Palette.BuildLut(lut, f.Flash);
                        f.Frame.ToRgba(lut, tmp, model.Palette.WaterTint);
                        for (int y = 0; y < fh - pad * 2; y++)
                        {
                            int sy = uy0 + y;
                            if (sy < 0 || sy >= ch) continue;
                            Array.Copy(tmp, (sy * cw + ux0) * 4, rgba, ((f.Y + pad + y) * pageW + f.X + pad) * 4, (fw - pad * 2) * 4);
                        }
                    }
                }
                result.Pages.Add((rgba, pageW, pageH));
            }
            result.MetadataJson = Metadata(model, result, opt, imageBaseName);
            progress?.Report(1.0);
            return result;
        }

        private static ExportedClip CaptureClip(CreatureModel model, CreatureRegistry registry, ClipKind kind, int dir, SpritesheetOptions opt,
            CreatureRenderer renderer, int cw, int ch, int ox, int oy, CancellationToken cancel)
        {
            int fps = opt.Fps;
            int stepsPerFrame = 60 / fps;
            double dt = FixedClock.DefaultStep;
            double yaw = PixelCamera.DirectionYaw(dir);
            Vec3 fwd = new Vec3(DMath.Cos(yaw), 0, -DMath.Sin(yaw));
            var clip = new ExportedClip { Kind = kind, Direction = dir, Fps = fps, Name = $"{ClipName(kind)}_{PixelCamera.DirectionNames[dir]}" };

            // idle-like loops: 2 s at 12 fps = 24 frames (breathing and blink quantized to the loop)
            double idleLoop = kind == ClipKind.Sleep ? 3.0 : 2.0;
            var motor = new CreatureMotor(model, registry, new MotorOptions { Facing = FacingMode.Free, ExportMode = true, ExportLoop = idleLoop });
            motor.Teleport(Vec3.Zero, yaw);
            motor.FacingOverride = yaw;
            void Steps(int n)
            {
                for (int i = 0; i < n; i++) motor.Step(dt);
            }
            void Capture(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    cancel.ThrowIfCancellationRequested();
                    var frame = new PixelFrame(cw, ch) { OriginX = ox, OriginY = oy };
                    var view = ViewSpec.Default(motor.Yaw);
                    view.DrawShadow = opt.DrawShadow;
                    view.Quality = opt.Quality;
                    renderer.Render(model.Anatomy, model.Palette, model.Surface, motor.Pose, view, frame);
                    clip.Frames.Add(new ExportedFrame { Frame = frame, Flash = motor.HitFlash, DurationMs = (int)Math.Round(1000.0 / fps) });
                    Steps(stepsPerFrame);
                }
            }
            // start every clip from a settled stance
            Steps(30);
            switch (kind)
            {
                case ClipKind.Idle:
                {
                    // align to the loop start so that breathing phase starts at zero
                    int loopFrames = (int)Math.Round(idleLoop * fps);
                    AlignTime(motor, idleLoop, dt);
                    clip.Loop = true;
                    Capture(loopFrames);
                    break;
                }
                case ClipKind.Walk:
                case ClipKind.Run:
                {
                    double speed = kind == ClipKind.Walk ? model.Motion.WalkSpeed : model.Motion.RunSpeed;
                    motor.DesiredVelocity = fwd * speed;
                    Steps(90);
                    double f = motor.CycleFrequency;
                    if (f <= 1e-6) f = 1.2;
                    int n = Math.Max(4, (int)Math.Round(fps / f));
                    double fLock = fps / (double)n;
                    motor.Context.CycleFrequencyOverride = fLock;
                    // settle springs into the periodic steady state
                    Steps(stepsPerFrame * n * 3);
                    clip.Loop = true;
                    clip.RootSpeed = speed;
                    Capture(n);
                    break;
                }
                case ClipKind.Action:
                {
                    motor.TriggerAction();
                    int frames = 0;
                    int limit = fps * 4;
                    while (frames < limit)
                    {
                        Capture(1);
                        frames++;
                        if (!motor.ActionActive && frames > 2) break;
                    }
                    Capture(Math.Max(1, fps / 6));
                    break;
                }
                case ClipKind.Hit:
                {
                    motor.TriggerHit(fwd, 1.0);
                    Capture((int)Math.Round(0.9 * fps * model.Motion.TimeScale));
                    break;
                }
                case ClipKind.RestEnter:
                {
                    motor.Resting = true;
                    int frames = 0;
                    while (motor.RestProgress < 0.999 && frames < fps * 6) { Capture(1); frames++; }
                    Capture(Math.Max(1, fps / 4));
                    break;
                }
                case ClipKind.Sleep:
                {
                    motor.SnapRest(true);
                    motor.Resting = true;
                    Steps(60);
                    AlignTime(motor, idleLoop, dt);
                    clip.Loop = true;
                    Capture((int)Math.Round(idleLoop * fps));
                    break;
                }
                case ClipKind.RestExit:
                {
                    motor.SnapRest(true);
                    motor.Resting = true;
                    Steps(30);
                    motor.Resting = false;
                    int frames = 0;
                    while (motor.RestProgress > 0.0 && frames < fps * 6) { Capture(1); frames++; }
                    Capture(Math.Max(1, fps / 4));
                    break;
                }
            }
            return clip;
        }

        private static void AlignTime(CreatureMotor motor, double loop, double dt)
        {
            double t = motor.Time;
            double next = Math.Ceiling(t / loop - 1e-9) * loop;
            int steps = (int)Math.Round((next - t) / dt);
            for (int i = 0; i < steps; i++) motor.Step(dt);
        }

        private static string Metadata(CreatureModel model, SpritesheetResult r, SpritesheetOptions opt, string baseName)
        {
            double sinE = DMath.Sin(PixelCamera.DefaultElevation);
            var clips = new JsonArray();
            foreach (var c in r.Clips)
            {
                var frames = new JsonArray();
                foreach (var f in c.Frames)
                    frames.Add(new JsonObject { ["x"] = f.X, ["y"] = f.Y, ["page"] = f.Page, ["durationMs"] = f.DurationMs });
                double yaw = PixelCamera.DirectionYaw(c.Direction);
                var clip = new JsonObject
                {
                    ["name"] = c.Name,
                    ["state"] = ClipName(c.Kind),
                    ["direction"] = c.Direction,
                    ["directionName"] = PixelCamera.DirectionNames[c.Direction],
                    ["loop"] = c.Loop,
                    ["fps"] = c.Fps,
                    ["frameCount"] = c.Frames.Count,
                    ["rootMotion"] = new JsonObject
                    {
                        ["groundSpeed"] = Math.Round(c.RootSpeed, 4),
                        // screen space pixels per second (y down, south foreshortened by the camera)
                        ["screenVelocityX"] = Math.Round(DMath.Cos(yaw) * c.RootSpeed, 4),
                        ["screenVelocityY"] = Math.Round(-DMath.Sin(yaw) * c.RootSpeed * sinE, 4),
                    },
                    ["frames"] = frames,
                };
                clips.Add(clip);
            }
            var pages = new JsonArray();
            for (int i = 0; i < r.Pages.Count; i++)
                pages.Add(new JsonObject { ["file"] = r.Pages.Count == 1 ? baseName + ".png" : $"{baseName}_{i}.png", ["width"] = r.Pages[i].width, ["height"] = r.Pages[i].height });
            var palette = new JsonArray();
            for (int i = CreaturePalette.FirstColorIndex; i < model.Palette.Colors.Count; i++) palette.Add(JsonValue.Create(model.Palette.Colors[i].ToHex()));
            var root = new JsonObject
            {
                ["format"] = "ppc.spritesheet",
                ["formatVersion"] = 1,
                ["generatorVersion"] = CreatureFramework.GeneratorVersion,
                ["framework"] = "Procedural Pixel Creatures " + CreatureFramework.FrameworkVersion,
                ["creature"] = new JsonObject
                {
                    ["name"] = model.Genome.Name,
                    ["family"] = model.Genome.FamilyId,
                    ["contentId"] = model.Genome.ContentId,
                },
                ["frameWidth"] = r.FrameWidth,
                ["frameHeight"] = r.FrameHeight,
                ["originX"] = r.OriginX,
                ["originY"] = r.OriginY,
                ["originNote"] = "Pixel inside each frame that sits on the creature's ground point; place it at the entity position.",
                ["cameraElevationDeg"] = 30,
                ["pages"] = pages,
                ["palette"] = palette,
                ["clips"] = clips,
            };
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
        }
    }
}
