// Procedural Pixel Creatures - immutable creature genome.
//
// Seed + genome + generator version fully determine a creature. The genome keeps one seed per
// stream (anatomy, color, pattern, motion); the builders for a stream only read the genes and the
// seed of that stream. Changing a color gene therefore can never re-roll the anatomy.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PixelCreatures.Core.Genetics
{
    public sealed class GenomeLineage
    {
        public string Origin { get; }            // sampled | mutation | crossover | edited | imported
        public int Generation { get; }
        public IReadOnlyList<string> ParentIds { get; }
        /// <summary>For crossover children: linkage group -> parent tag ("A" or "B").</summary>
        public IReadOnlyDictionary<string, string> Provenance { get; }
        public double MutationStrength { get; }

        public GenomeLineage(string origin, int generation, IReadOnlyList<string>? parents = null,
            IReadOnlyDictionary<string, string>? provenance = null, double mutationStrength = 0)
        {
            Origin = origin;
            Generation = generation;
            ParentIds = parents ?? Array.Empty<string>();
            Provenance = provenance ?? new Dictionary<string, string>();
            MutationStrength = mutationStrength;
        }

        public static readonly GenomeLineage Sampled = new GenomeLineage("sampled", 0);
    }

    public sealed class CreatureGenome
    {
        public const int CurrentFormatVersion = 1;

        private readonly double[] _values;
        private readonly ulong[] _seeds;

        public string FamilyId { get; }
        public GeneSchema Schema { get; }
        public int GeneratorVersion { get; }
        public string Name { get; }
        public GenomeLineage Lineage { get; }

        public CreatureGenome(GeneSchema schema, int generatorVersion, ulong[] seeds, double[] values, string name, GenomeLineage? lineage = null)
        {
            if (seeds.Length != 4) throw new ArgumentException("A genome needs exactly four stream seeds.", nameof(seeds));
            if (values.Length != schema.Count) throw new ArgumentException($"Expected {schema.Count} gene values, got {values.Length}.", nameof(values));
            Schema = schema;
            FamilyId = schema.FamilyId;
            GeneratorVersion = generatorVersion;
            _seeds = (ulong[])seeds.Clone();
            _values = new double[values.Length];
            for (int i = 0; i < values.Length; i++) _values[i] = schema.Genes[i].Quantize(values[i]);
            Name = name ?? string.Empty;
            Lineage = lineage ?? GenomeLineage.Sampled;
        }

        public static CreatureGenome CreateDefault(GeneSchema schema, int generatorVersion, ulong seed, string name = "")
        {
            var values = new double[schema.Count];
            for (int i = 0; i < values.Length; i++) values[i] = schema.Genes[i].Default;
            return new CreatureGenome(schema, generatorVersion, DeriveStreamSeeds(seed), values, name);
        }

        public static ulong[] DeriveStreamSeeds(ulong masterSeed) => new[]
        {
            StableHash.Derive(masterSeed, "stream.anatomy"),
            StableHash.Derive(masterSeed, "stream.color"),
            StableHash.Derive(masterSeed, "stream.pattern"),
            StableHash.Derive(masterSeed, "stream.motion"),
        };

        public ulong Seed(GeneStream stream) => _seeds[(int)stream];
        public ulong AnatomySeed => _seeds[0];
        public ulong ColorSeed => _seeds[1];
        public ulong PatternSeed => _seeds[2];
        public ulong MotionSeed => _seeds[3];
        public ulong[] CopySeeds() => (ulong[])_seeds.Clone();
        public double[] CopyValues() => (double[])_values.Clone();

        public int Count => _values.Length;
        public double this[int index] => _values[index];

        public double Get(string id)
        {
            int i = Schema.IndexOf(id);
            if (i < 0) throw new KeyNotFoundException($"Gene '{id}' is not part of family '{FamilyId}'.");
            return _values[i];
        }

        public double GetOr(string id, double fallback)
        {
            int i = Schema.IndexOf(id);
            return i < 0 ? fallback : _values[i];
        }

        public int GetInt(string id) => (int)Math.Round(Get(id));
        public bool GetBool(string id) => Get(id) >= 0.5;

        public string GetChoice(string id)
        {
            var def = Schema.Get(id);
            int i = (int)Get(id);
            return i >= 0 && i < def.Options.Count ? def.Options[i].Id : def.Options[0].Id;
        }

        public CreatureGenome With(string id, double value, GenomeLineage? lineage = null)
        {
            int i = Schema.IndexOf(id);
            if (i < 0) throw new KeyNotFoundException($"Gene '{id}' is not part of family '{FamilyId}'.");
            var v = CopyValues();
            v[i] = value;
            return new CreatureGenome(Schema, GeneratorVersion, _seeds, v, Name, lineage ?? Lineage);
        }

        public CreatureGenome WithChoice(string id, string optionId)
        {
            var def = Schema.Get(id);
            int idx = def.OptionIndex(optionId);
            if (idx < 0) throw new ArgumentException($"Option '{optionId}' is not valid for gene '{id}'.");
            return With(id, idx);
        }

        public CreatureGenome WithValues(double[] values, GenomeLineage? lineage = null) =>
            new CreatureGenome(Schema, GeneratorVersion, _seeds, values, Name, lineage ?? Lineage);

        public CreatureGenome WithSeeds(ulong[] seeds, GenomeLineage? lineage = null) =>
            new CreatureGenome(Schema, GeneratorVersion, seeds, _values, Name, lineage ?? Lineage);

        public CreatureGenome WithSeed(GeneStream stream, ulong seed)
        {
            var s = CopySeeds();
            s[(int)stream] = seed;
            return new CreatureGenome(Schema, GeneratorVersion, s, _values, Name, Lineage);
        }

        public CreatureGenome WithName(string name) => new CreatureGenome(Schema, GeneratorVersion, _seeds, _values, name, Lineage);

        public CreatureGenome WithLineage(GenomeLineage lineage) => new CreatureGenome(Schema, GeneratorVersion, _seeds, _values, Name, lineage);

        public CreatureGenome WithGeneratorVersion(int version) => new CreatureGenome(Schema, version, _seeds, _values, Name, Lineage);

        // ------------------------------------------------------------------ identity hashes

        private ulong[]? _streamKeys;

        /// <summary>Hash over everything that can influence one stream's output (cached; genomes are immutable).</summary>
        public ulong StreamKey(GeneStream stream)
        {
            var keys = _streamKeys;
            if (keys == null)
            {
                keys = new ulong[4];
                for (int s = 0; s < 4; s++) keys[s] = ComputeStreamKey((GeneStream)s);
                _streamKeys = keys;
            }
            return keys[(int)stream];
        }

        private ulong ComputeStreamKey(GeneStream stream)
        {
            ulong h = StableHash.Fnv1a64(FamilyId);
            h = StableHash.Combine(h, (ulong)GeneratorVersion);
            h = StableHash.Combine(h, Schema.Fingerprint());
            h = StableHash.Combine(h, (ulong)stream);
            h = StableHash.Combine(h, _seeds[(int)stream]);
            for (int i = 0; i < _values.Length; i++)
            {
                if (Schema.Genes[i].Stream != stream) continue;
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(_values[i]));
            }
            return h;
        }

        public ulong AnatomyKey => StreamKey(GeneStream.Anatomy);
        public ulong ColorKey => StreamKey(GeneStream.Color);
        public ulong PatternKey => StreamKey(GeneStream.Pattern);
        public ulong MotionKey => StreamKey(GeneStream.Motion);

        /// <summary>Identity of the whole genome (name and lineage excluded; they do not change the creature).</summary>
        public ulong ContentHash
        {
            get
            {
                ulong h = AnatomyKey;
                h = StableHash.Combine(h, ColorKey);
                h = StableHash.Combine(h, PatternKey);
                h = StableHash.Combine(h, MotionKey);
                return h;
            }
        }

        public string ContentId => ContentHash.ToString("x16", CultureInfo.InvariantCulture);

        public bool ContentEquals(CreatureGenome other)
        {
            if (other.FamilyId != FamilyId || other.GeneratorVersion != GeneratorVersion || other._values.Length != _values.Length) return false;
            for (int i = 0; i < _seeds.Length; i++) if (_seeds[i] != other._seeds[i]) return false;
            for (int i = 0; i < _values.Length; i++) if (_values[i] != other._values[i]) return false;
            return true;
        }

        public override string ToString() => $"{FamilyId}:{ContentId} '{Name}'";
    }
}
