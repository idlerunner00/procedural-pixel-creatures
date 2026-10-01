// Procedural Pixel Creatures - immutable creature model and the caching factory that builds it.

using System;
using System.Collections.Generic;
using System.Threading;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;

namespace PixelCreatures.Core
{
    /// <summary>Framework wide constants.</summary>
    public static class CreatureFramework
    {
        /// <summary>
        /// Version of the generation rules. Bump whenever a change alters the output for an existing
        /// genome (anatomy, palette, pattern or motion). Stored in genomes and part of every cache key.
        /// Version 2: quality pass (lighting, contact shadows, coats and marks, gear, idle shapes,
        /// folded wings, new archetypes and shapes).
        /// </summary>
        public const int GeneratorVersion = 2;
        public const string FrameworkVersion = "1.0.0";
    }

    /// <summary>
    /// Everything needed to animate and render one creature. Built off the main thread, immutable,
    /// shared between all actors showing the same genome.
    /// </summary>
    public sealed class CreatureModel
    {
        public CreatureGenome Genome { get; }
        public ICreatureFamily Family { get; }
        public CreatureAnatomy Anatomy { get; }
        public CreaturePalette Palette { get; }
        public SurfaceSpec Surface { get; }
        public MotionTraits Motion { get; }
        public IReadOnlyList<string> Warnings { get; }

        public CreatureModel(CreatureGenome genome, ICreatureFamily family, CreatureAnatomy anatomy, CreaturePalette palette,
            SurfaceSpec surface, MotionTraits motion, IReadOnlyList<string> warnings)
        {
            Genome = genome; Family = family; Anatomy = anatomy; Palette = palette; Surface = surface; Motion = motion; Warnings = warnings;
        }

        public ulong ModelKey => StableHash.Combine(StableHash.Combine(Anatomy.AnatomyKey, Palette.ColorKey), StableHash.Combine(Surface.PatternKey, Motion.MotionKey));
    }

    /// <summary>Bounded least-recently-used cache (thread safe).</summary>
    public sealed class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey key, TValue value)>> _map = new Dictionary<TKey, LinkedListNode<(TKey, TValue)>>();
        private readonly LinkedList<(TKey key, TValue value)> _order = new LinkedList<(TKey, TValue)>();
        private readonly object _lock = new object();
        public long Hits, Misses, Evictions;

        public LruCache(int capacity) { _capacity = Math.Max(1, capacity); }

        public int Count { get { lock (_lock) return _map.Count; } }
        public int Capacity => _capacity;

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var node))
                {
                    _order.Remove(node);
                    _order.AddFirst(node);
                    value = node.Value.value;
                    Hits++;
                    return true;
                }
                Misses++;
                value = default!;
                return false;
            }
        }

        public void Put(TKey key, TValue value)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(key, out var existing))
                {
                    _order.Remove(existing);
                    _map.Remove(key);
                }
                var node = new LinkedListNode<(TKey, TValue)>((key, value));
                _order.AddFirst(node);
                _map[key] = node;
                while (_map.Count > _capacity)
                {
                    var last = _order.Last!;
                    _order.RemoveLast();
                    _map.Remove(last.Value.key);
                    Evictions++;
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _map.Clear();
                _order.Clear();
            }
        }
    }

    /// <summary>
    /// Builds creature models. Each stream (anatomy, palette, surface, motion) is cached separately
    /// under its own stream key, so a colour change reuses the anatomy and vice versa.
    /// Thread safe; safe to call from worker threads.
    /// </summary>
    public sealed class CreatureFactory
    {
        public CreatureRegistry Registry { get; }
        private readonly LruCache<ulong, CreatureAnatomy> _anatomy;
        private readonly LruCache<ulong, CreaturePalette> _palette;
        private readonly LruCache<ulong, SurfaceSpec> _surface;
        private readonly LruCache<ulong, MotionTraits> _motion;
        private readonly LruCache<ulong, CreatureModel> _models;

        public CreatureFactory(CreatureRegistry registry, int capacity = 192)
        {
            Registry = registry;
            _anatomy = new LruCache<ulong, CreatureAnatomy>(capacity);
            _palette = new LruCache<ulong, CreaturePalette>(capacity * 2);
            _surface = new LruCache<ulong, SurfaceSpec>(capacity * 2);
            _motion = new LruCache<ulong, MotionTraits>(capacity * 2);
            _models = new LruCache<ulong, CreatureModel>(capacity);
        }

        public (long hits, long misses, int count) AnatomyCacheStats => (_anatomy.Hits, _anatomy.Misses, _anatomy.Count);
        public (long hits, long misses, int count) ModelCacheStats => (_models.Hits, _models.Misses, _models.Count);

        public void ClearCaches()
        {
            _anatomy.Clear(); _palette.Clear(); _surface.Clear(); _motion.Clear(); _models.Clear();
        }

        public CreatureModel Build(CreatureGenome genome, CancellationToken cancel = default)
        {
            if (!Registry.TryGetFamily(genome.FamilyId, out var family))
                throw new CreatureBuildException($"Unknown body-plan family '{genome.FamilyId}'.");
            if (family.Schema.Fingerprint() != genome.Schema.Fingerprint())
                throw new CreatureBuildException($"The genome does not match the current schema of family '{genome.FamilyId}'.");
            var warnings = new List<string>();
            if (genome.GeneratorVersion != CreatureFramework.GeneratorVersion)
                warnings.Add($"Genome was created with generator version {genome.GeneratorVersion}; current version is {CreatureFramework.GeneratorVersion}. Appearance may differ.");

            ulong modelKey = StableHash.Combine(genome.ContentHash, (ulong)CreatureFramework.GeneratorVersion);
            if (_models.TryGet(modelKey, out var cachedModel) && ReferenceEquals(cachedModel.Family, family))
                return cachedModel.Genome.Name == genome.Name ? cachedModel : WithGenome(cachedModel, genome);

            cancel.ThrowIfCancellationRequested();
            ulong aKey = StableHash.Combine(genome.AnatomyKey, (ulong)CreatureFramework.GeneratorVersion);
            if (!_anatomy.TryGet(aKey, out var anatomy))
            {
                anatomy = family.BuildAnatomy(genome, Registry);
                _anatomy.Put(aKey, anatomy);
            }
            cancel.ThrowIfCancellationRequested();
            ulong pKey = StableHash.Combine(StableHash.Combine(genome.ColorKey, StableHash.Fnv1a64(family.PaletteRuleId)), (ulong)CreatureFramework.GeneratorVersion);
            if (!_palette.TryGet(pKey, out var palette))
            {
                palette = Registry.GetPaletteRule(family.PaletteRuleId).Build(genome);
                _palette.Put(pKey, palette);
            }
            ulong sKey = StableHash.Combine(genome.PatternKey, (ulong)CreatureFramework.GeneratorVersion);
            if (!_surface.TryGet(sKey, out var surface))
            {
                surface = SurfaceBuilder.Build(genome);
                _surface.Put(sKey, surface);
            }
            ulong mKey = StableHash.Combine(StableHash.Combine(genome.MotionKey, genome.AnatomyKey), (ulong)CreatureFramework.GeneratorVersion);
            if (!_motion.TryGet(mKey, out var motion))
            {
                motion = MotionTraits.FromGenome(genome, anatomy);
                _motion.Put(mKey, motion);
            }
            cancel.ThrowIfCancellationRequested();
            foreach (var r in anatomy.Repairs) warnings.Add(r);
            var model = new CreatureModel(genome, family, anatomy, palette, surface, motion, warnings);
            _models.Put(modelKey, model);
            return model;
        }

        private static CreatureModel WithGenome(CreatureModel m, CreatureGenome g) =>
            new CreatureModel(g, m.Family, m.Anatomy, m.Palette, m.Surface, m.Motion, m.Warnings);
    }

    public sealed class CreatureBuildException : Exception
    {
        public CreatureBuildException(string message) : base(message) { }
    }

    /// <summary>Samples new genomes and names.</summary>
    public static class GenomeFactory
    {
        /// <summary>
        /// Fixed, uncurated seed sample used for quality reviews (gallery, contact sheets, reports).
        /// Never edited to hide bad results.
        /// </summary>
        public static readonly ulong[] UncuratedSeeds = { 101, 202, 303, 404, 505, 606, 707, 808 };

        /// <summary>A brand new creature of a family from a seed. Deterministic.</summary>
        public static CreatureGenome Sample(ICreatureFamily family, ulong seed)
        {
            var schema = family.Schema;
            var values = new double[schema.Count];
            for (int i = 0; i < values.Length; i++) values[i] = schema.Genes[i].Default;
            var archRng = Rng.Derive(seed, "archetype." + family.Id);
            var weights = new List<double>();
            foreach (var a in family.Archetypes) weights.Add(a.Weight);
            string arch = family.Archetypes[archRng.Weighted(weights)].Id;
            family.SampleAnatomy(values, Rng.Derive(seed, "genes.anatomy." + family.Id), arch);
            var hints = family.StyleHints(arch);
            SharedGenes.SampleColor(schema, values, Rng.Derive(seed, "genes.color"), hints);
            SharedGenes.SamplePattern(schema, values, Rng.Derive(seed, "genes.pattern"), hints);
            SharedGenes.SampleMotion(schema, values, Rng.Derive(seed, "genes.motion"), hints);
            family.AdjustSample(values, Rng.Derive(seed, "genes.adjust." + family.Id), arch);
            var seeds = CreatureGenome.DeriveStreamSeeds(seed);
            string name = NameGenerator.Generate(Rng.Derive(seed, "name"));
            return new CreatureGenome(schema, CreatureFramework.GeneratorVersion, seeds, values, name, GenomeLineage.Sampled);
        }

        /// <summary>Regenerates all unlocked genes from a new seed (locks keep their values).</summary>
        public static CreatureGenome RegenerateUnlocked(ICreatureFamily family, CreatureGenome current, GeneLocks locks, ulong seed)
        {
            var fresh = Sample(family, seed);
            var result = GenomeOps.Regenerate(current, locks, fresh);
            return result.WithName(locks.IsEmpty ? fresh.Name : current.Name);
        }
    }

    /// <summary>Syllable based creature names (deterministic, for galleries and presets).</summary>
    public static class NameGenerator
    {
        private static readonly string[] Starts = { "Kr", "Mor", "Th", "Sk", "Gr", "Vel", "Bra", "Ul", "Zar", "Fen", "Kol", "Rha", "Ny", "Tor", "Ish", "Dru", "Gal", "Or", "Ser", "Wyn", "Ba", "Ki", "Lu", "Pe" };
        private static readonly string[] Mids = { "a", "o", "e", "i", "u", "ae", "ou", "y", "ai" };
        private static readonly string[] Ends = { "k", "x", "th", "rn", "ll", "sh", "gar", "mir", "dor", "wen", "ssa", "rik", "lo", "vek", "nox", "bel", "ra", "zul", "pip", "bo" };

        public static string Generate(Rng rng)
        {
            string s = rng.Pick(Starts) + rng.Pick(Mids);
            if (rng.Chance(0.45)) s += rng.Pick(new[] { "r", "l", "n", "m", "v", "d" }) + rng.Pick(Mids);
            s += rng.Pick(Ends);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
