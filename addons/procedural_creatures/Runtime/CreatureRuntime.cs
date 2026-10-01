// Procedural Pixel Creatures - Godot runtime services shared by all actors.
//
// Owns the registry (built-ins + discovered extensions), the caching factory, the serializer and the
// asynchronous build service. Everything here is safe to call from the main thread; builds run on the
// .NET thread pool and results are published back to the main thread (Godot thread rules).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Serialization;

namespace PixelCreatures.Runtime
{
    public static class CreatureRuntime
    {
        private static CreatureFactory? _factory;
        private static GenomeSerializer? _serializer;
        private static readonly object Lock = new object();

        public static CreatureRegistry Registry => CreatureRegistry.Shared;

        public static CreatureFactory Factory
        {
            get
            {
                lock (Lock) return _factory ??= new CreatureFactory(Registry, 256);
            }
        }

        public static GenomeSerializer Serializer
        {
            get
            {
                lock (Lock) return _serializer ??= new GenomeSerializer(Registry);
            }
        }

        /// <summary>Samples a new genome of a family from a seed (deterministic).</summary>
        public static CreatureGenome Sample(string familyId, ulong seed) => GenomeFactory.Sample(Registry.GetFamily(familyId), seed);

        /// <summary>Builds a model synchronously (cached).</summary>
        public static CreatureModel Build(CreatureGenome genome) => Factory.Build(genome);

        /// <summary>Loads a preset (res:// or user://). Returns the load result with errors and warnings.</summary>
        public static GenomeLoadResult LoadPreset(string path)
        {
            if (!Godot.FileAccess.FileExists(path))
            {
                var r = new GenomeLoadResult();
                r.Errors.Add($"File not found: {path}");
                return r;
            }
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (f == null)
            {
                var r = new GenomeLoadResult();
                r.Errors.Add($"Could not open file: {path} ({Godot.FileAccess.GetOpenError()})");
                return r;
            }
            string text = f.GetAsText();
            return Serializer.Deserialize(text);
        }

        /// <summary>Paths of all shipped sample presets: res://addons/&lt;addon&gt;/Samples/&lt;family&gt;/*.json (sorted).</summary>
        public static List<string> SamplePaths()
        {
            var list = new List<string>();
            if (!DirAccess.DirExistsAbsolute("res://addons")) return list;
            var addons = new List<string>(DirAccess.GetDirectoriesAt("res://addons"));
            addons.Sort(StringComparer.Ordinal);
            foreach (var addon in addons)
            {
                string root = $"res://addons/{addon}/Samples";
                if (!DirAccess.DirExistsAbsolute(root)) continue;
                var families = new List<string>(DirAccess.GetDirectoriesAt(root));
                families.Sort(StringComparer.Ordinal);
                foreach (var fam in families)
                {
                    var files = new List<string>(DirAccess.GetFilesAt($"{root}/{fam}"));
                    files.Sort(StringComparer.Ordinal);
                    foreach (var file in files)
                        if (file.EndsWith(".json", StringComparison.Ordinal)) list.Add($"{root}/{fam}/{file}");
                }
            }
            return list;
        }

        /// <summary>Saves a preset. Creates the directory if needed. Returns an error message or null.</summary>
        public static string? SavePreset(string path, CreatureGenome genome, PresetMeta? meta = null)
        {
            string dir = path.GetBaseDir();
            if (!DirAccess.DirExistsAbsolute(dir))
            {
                var err = DirAccess.MakeDirRecursiveAbsolute(dir);
                if (err != Error.Ok) return $"Could not create directory {dir}: {err}";
            }
            string json = Serializer.Serialize(genome, meta);
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
            if (f == null) return $"Could not write file: {path} ({Godot.FileAccess.GetOpenError()})";
            f.StoreString(json);
            return null;
        }
    }

    /// <summary>
    /// Asynchronous model building with cancellation. Each requester gets a ticket; only the newest
    /// ticket of a requester is delivered, older results are discarded even if they finish later.
    /// Completion callbacks always run on the Godot main thread.
    /// </summary>
    public sealed class CreatureBuildRequester : IDisposable
    {
        private CancellationTokenSource? _cts;
        private long _ticket;
        private bool _disposed;
        public long Discarded { get; private set; }
        public long Delivered { get; private set; }

        public bool Busy { get; private set; }

        /// <summary>Starts building. 'onReady' runs on the main thread with the model, 'onError' with a message.</summary>
        public void Request(CreatureGenome genome, Action<CreatureModel> onReady, Action<string>? onError = null)
        {
            if (_disposed) return;
            _cts?.Cancel();
            _cts?.Dispose();
            var cts = new CancellationTokenSource();
            _cts = cts;
            long ticket = Interlocked.Increment(ref _ticket);
            Busy = true;
            var factory = CreatureRuntime.Factory;
            Task.Run(() => factory.Build(genome, cts.Token), cts.Token).ContinueWith(t =>
            {
                // hop to the main thread; Callable.CallDeferred is thread safe
                Callable.From(() =>
                {
                    if (_disposed) return;
                    if (ticket != Interlocked.Read(ref _ticket) || cts.IsCancellationRequested)
                    {
                        Discarded++;
                        return;
                    }
                    Busy = false;
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception?.GetBaseException();
                        onError?.Invoke(ex?.Message ?? "Unknown generation error.");
                        return;
                    }
                    if (t.IsCanceled) { Discarded++; return; }
                    Delivered++;
                    onReady(t.Result);
                }).CallDeferred();
            }, TaskScheduler.Default);
        }

        public void Cancel()
        {
            _cts?.Cancel();
            Interlocked.Increment(ref _ticket);
            Busy = false;
        }

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    public static class GodotConvert
    {
        public static Color ToColor(PixelCreatures.Core.Palettes.Rgba c) => Color.Color8(c.R, c.G, c.B, c.A);

        /// <summary>Ground position (x east, z south) to 2D screen position (y down) for the fixed camera.</summary>
        public static Vector2 GroundToScreen(double x, double z, double altitude = 0)
        {
            double e = PixelCamera.DefaultElevation;
            return new Vector2((float)x, (float)(z * DMath.Sin(e) - altitude * DMath.Cos(e)));
        }

        public static Vector2 ScreenToGround(Vector2 screen)
        {
            double e = PixelCamera.DefaultElevation;
            return new Vector2(screen.X, (float)(screen.Y / DMath.Sin(e)));
        }
    }
}
