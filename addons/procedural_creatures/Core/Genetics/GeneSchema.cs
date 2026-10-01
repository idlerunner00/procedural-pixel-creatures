// Procedural Pixel Creatures - gene schema.
//
// Every family describes its genome with a schema: an ordered list of typed gene definitions.
// The schema drives validation, serialization, mutation, crossover, locking and the automatically
// generated workshop controls. Values are stored as doubles; choice genes store the option index,
// bools 0/1, ints integral values. Float genes are quantized to a fixed grid so that a value that
// survives a JSON round trip is bit identical to the value used for generation.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace PixelCreatures.Core.Genetics
{
    /// <summary>Independent random/data streams. A gene only ever influences the stage of its stream.</summary>
    public enum GeneStream
    {
        Anatomy = 0,
        Color = 1,
        Pattern = 2,
        Motion = 3,
    }

    public enum GeneKind
    {
        Float,
        Int,
        Choice,
        Bool,
    }

    /// <summary>How a gene is combined when two parents are crossed.</summary>
    public enum InheritanceMode
    {
        /// <summary>Random interpolation between both parents (continuous traits).</summary>
        Blend,
        /// <summary>Take the value of one parent (discrete traits, also used for linked groups).</summary>
        Pick,
        /// <summary>Hue like values on a circle: interpolate along the shortest arc.</summary>
        Circular,
    }

    public enum DisplayUnit
    {
        Percent,
        Degrees,
        Plain,
        Count,
    }

    public sealed class GeneOption
    {
        public string Id { get; }
        public string Label { get; }
        public GeneOption(string id, string label) { Id = id; Label = label; }
        public override string ToString() => Id;
    }

    public sealed class GeneDef
    {
        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
        public string Group { get; }
        public GeneStream Stream { get; }
        public GeneKind Kind { get; }
        public double Min { get; }
        public double Max { get; }
        public double Default { get; }
        /// <summary>Quantization step for floats (value grid), 1 for discrete genes.</summary>
        public double Step { get; }
        /// <summary>Gaussian mutation sigma at mutation strength 1, as a fraction of the range.</summary>
        public double MutationSigma { get; }
        public InheritanceMode Inheritance { get; }
        /// <summary>Genes with the same linkage key are inherited together from one parent.</summary>
        public string Linkage { get; }
        public IReadOnlyList<GeneOption> Options { get; }
        public DisplayUnit Unit { get; }
        /// <summary>Genes flagged advanced are hidden in compact editor views.</summary>
        public bool Advanced { get; }
        public int Order { get; }

        internal GeneDef(string id, string label, string description, string group, GeneStream stream, GeneKind kind,
            double min, double max, double def, double step, double sigma, InheritanceMode inheritance, string linkage,
            IReadOnlyList<GeneOption> options, DisplayUnit unit, bool advanced, int order)
        {
            Id = id; Label = label; Description = description; Group = group; Stream = stream; Kind = kind;
            Min = min; Max = max; Step = step; MutationSigma = sigma; Inheritance = inheritance; Linkage = linkage;
            Options = options; Unit = unit; Advanced = advanced; Order = order;
            Default = Quantize(def);
        }

        public double Range => Max - Min;

        /// <summary>Clamps and snaps a value onto the gene grid. Pure and deterministic.</summary>
        public double Quantize(double v)
        {
            if (double.IsNaN(v)) v = Default;
            if (v < Min) v = Min;
            if (v > Max) v = Max;
            switch (Kind)
            {
                case GeneKind.Float:
                {
                    double k = Math.Round((v - Min) / Step, MidpointRounding.AwayFromZero);
                    double q = Min + k * Step;
                    if (q > Max) q = Max;
                    if (q < Min) q = Min;
                    return q;
                }
                case GeneKind.Bool:
                    return v >= 0.5 ? 1.0 : 0.0;
                default:
                    return Math.Round(v, MidpointRounding.AwayFromZero);
            }
        }

        public int OptionIndex(string id)
        {
            for (int i = 0; i < Options.Count; i++)
                if (string.Equals(Options[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        public string FormatValue(double v)
        {
            switch (Kind)
            {
                case GeneKind.Choice:
                {
                    int i = (int)v;
                    return i >= 0 && i < Options.Count ? Options[i].Label : "?";
                }
                case GeneKind.Bool:
                    return v >= 0.5 ? "ja" : "nein";
                case GeneKind.Int:
                    return ((int)v).ToString(CultureInfo.InvariantCulture);
                default:
                    switch (Unit)
                    {
                        case DisplayUnit.Percent: return (v * 100.0).ToString("0", CultureInfo.InvariantCulture) + " %";
                        case DisplayUnit.Degrees: return v.ToString("0", CultureInfo.InvariantCulture) + "°";
                        default: return v.ToString("0.00", CultureInfo.InvariantCulture);
                    }
            }
        }

        public override string ToString() => Id;
    }

    public sealed class GeneGroupDef
    {
        public string Id { get; }
        public string Label { get; }
        public GeneStream Stream { get; }
        public GeneGroupDef(string id, string label, GeneStream stream) { Id = id; Label = label; Stream = stream; }
    }

    public sealed class GeneSchema
    {
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>(StringComparer.Ordinal);

        public string FamilyId { get; }
        public IReadOnlyList<GeneDef> Genes { get; }
        public IReadOnlyList<GeneGroupDef> Groups { get; }

        internal GeneSchema(string familyId, List<GeneDef> genes, List<GeneGroupDef> groups)
        {
            FamilyId = familyId;
            Genes = genes;
            Groups = groups;
            for (int i = 0; i < genes.Count; i++)
            {
                if (_index.ContainsKey(genes[i].Id))
                    throw new InvalidOperationException($"Duplicate gene id '{genes[i].Id}' in schema '{familyId}'.");
                _index[genes[i].Id] = i;
            }
        }

        public int Count => Genes.Count;

        public int IndexOf(string id) => _index.TryGetValue(id, out int i) ? i : -1;

        public bool Contains(string id) => _index.ContainsKey(id);

        public GeneDef Get(string id)
        {
            if (!_index.TryGetValue(id, out int i)) throw new KeyNotFoundException($"Gene '{id}' is not part of schema '{FamilyId}'.");
            return Genes[i];
        }

        public GeneGroupDef? GetGroup(string id)
        {
            foreach (var g in Groups) if (g.Id == id) return g;
            return null;
        }

        public IEnumerable<GeneDef> GenesInGroup(string groupId)
        {
            foreach (var g in Genes) if (g.Group == groupId) yield return g;
        }

        /// <summary>Stable fingerprint of the schema layout (ids, kinds, ranges). Used in cache keys and tests.</summary>
        private ulong _fingerprint;
        private bool _hasFingerprint;

        /// <summary>Stable hash of the schema layout (cached: a schema never changes after Build).</summary>
        public ulong Fingerprint()
        {
            if (_hasFingerprint) return _fingerprint;
            _fingerprint = ComputeFingerprint();
            _hasFingerprint = true;
            return _fingerprint;
        }

        private ulong ComputeFingerprint()
        {
            ulong h = StableHash.Fnv1a64(FamilyId);
            foreach (var g in Genes)
            {
                h = StableHash.Combine(h, StableHash.Fnv1a64(g.Id));
                h = StableHash.Combine(h, (ulong)g.Kind);
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(g.Min));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(g.Max));
                h = StableHash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(g.Step));
                foreach (var o in g.Options) h = StableHash.Combine(h, StableHash.Fnv1a64(o.Id));
            }
            return h;
        }
    }

    /// <summary>Fluent builder used by families (and extensions) to declare their genes.</summary>
    public sealed class GeneSchemaBuilder
    {
        private readonly string _familyId;
        private readonly List<GeneDef> _genes = new List<GeneDef>();
        private readonly List<GeneGroupDef> _groups = new List<GeneGroupDef>();
        private string _group = "general";
        private GeneStream _stream = GeneStream.Anatomy;

        public GeneSchemaBuilder(string familyId) { _familyId = familyId; }

        public GeneSchemaBuilder Group(string id, string label, GeneStream stream)
        {
            _group = id;
            _stream = stream;
            bool exists = false;
            foreach (var g in _groups) if (g.Id == id) exists = true;
            if (!exists) _groups.Add(new GeneGroupDef(id, label, stream));
            return this;
        }

        public GeneSchemaBuilder Float(string id, string label, double min, double max, double def,
            string description = "", double sigma = 0.10, InheritanceMode inheritance = InheritanceMode.Blend,
            string? linkage = null, DisplayUnit unit = DisplayUnit.Percent, bool advanced = false, double? step = null)
        {
            double s = step ?? (max - min) / 1000.0;
            _genes.Add(new GeneDef(id, label, description, _group, _stream, GeneKind.Float, min, max, def, s, sigma,
                inheritance, linkage ?? _group, Array.Empty<GeneOption>(), unit, advanced, _genes.Count));
            return this;
        }

        public GeneSchemaBuilder Int(string id, string label, int min, int max, int def,
            string description = "", double sigma = 0.15, string? linkage = null, bool advanced = false)
        {
            _genes.Add(new GeneDef(id, label, description, _group, _stream, GeneKind.Int, min, max, def, 1.0, sigma,
                InheritanceMode.Pick, linkage ?? _group, Array.Empty<GeneOption>(), DisplayUnit.Count, advanced, _genes.Count));
            return this;
        }

        public GeneSchemaBuilder Choice(string id, string label, (string id, string label)[] options, string def,
            string description = "", double sigma = 0.12, string? linkage = null, bool advanced = false)
        {
            var opts = new List<GeneOption>();
            int defIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                opts.Add(new GeneOption(options[i].id, options[i].label));
                if (options[i].id == def) defIndex = i;
            }
            _genes.Add(new GeneDef(id, label, description, _group, _stream, GeneKind.Choice, 0, opts.Count - 1, defIndex, 1.0, sigma,
                InheritanceMode.Pick, linkage ?? _group, opts, DisplayUnit.Plain, advanced, _genes.Count));
            return this;
        }

        public GeneSchemaBuilder Bool(string id, string label, bool def, string description = "", double sigma = 0.10,
            string? linkage = null, bool advanced = false)
        {
            _genes.Add(new GeneDef(id, label, description, _group, _stream, GeneKind.Bool, 0, 1, def ? 1 : 0, 1.0, sigma,
                InheritanceMode.Pick, linkage ?? _group, Array.Empty<GeneOption>(), DisplayUnit.Plain, advanced, _genes.Count));
            return this;
        }

        public GeneSchemaBuilder Hue(string id, string label, double def, string description = "", double sigma = 0.06, string? linkage = null)
        {
            _genes.Add(new GeneDef(id, label, description, _group, _stream, GeneKind.Float, 0, 360, def, 0.5, sigma,
                InheritanceMode.Circular, linkage ?? _group, Array.Empty<GeneOption>(), DisplayUnit.Degrees, false, _genes.Count));
            return this;
        }

        public GeneSchema Build() => new GeneSchema(_familyId, new List<GeneDef>(_genes), new List<GeneGroupDef>(_groups));
    }
}
