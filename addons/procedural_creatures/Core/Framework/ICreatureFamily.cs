// Procedural Pixel Creatures - body plan families (extension point).
//
// A family owns a gene schema, samples genomes from seeds (with archetypes as priors) and builds
// anatomies. It never touches colours or animation directly: colours come from palette rules,
// animation from motion modules selected through the anatomy's MotionRig.

using System;
using System.Collections.Generic;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;

namespace PixelCreatures.Core
{
    public sealed class ArchetypeInfo
    {
        public string Id { get; }
        public string Label { get; }
        public double Weight { get; }
        public ArchetypeInfo(string id, string label, double weight = 1) { Id = id; Label = label; Weight = weight; }
    }

    public interface ICreatureFamily
    {
        /// <summary>Stable id stored in genomes (never rename; add a migration instead).</summary>
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        /// <summary>Sort order in user interfaces.</summary>
        int Order { get; }
        GeneSchema Schema { get; }
        IReadOnlyList<ArchetypeInfo> Archetypes { get; }
        /// <summary>Id of the palette rule used for this family (see CreatureRegistry).</summary>
        string PaletteRuleId { get; }
        /// <summary>Style hints that bias colour, pattern and motion sampling for an archetype.</summary>
        FamilyStyleHints StyleHints(string archetype);
        /// <summary>Samples the family specific (anatomy) genes for an archetype.</summary>
        void SampleAnatomy(double[] values, Rng rng, string archetype);
        /// <summary>Last chance to adjust shared genes after sampling (e.g. golems prefer stone).</summary>
        void AdjustSample(double[] values, Rng rng, string archetype);
        /// <summary>Builds the anatomy from the Anatomy stream only.</summary>
        CreatureAnatomy BuildAnatomy(CreatureGenome genome, CreatureRegistry registry);
    }

    /// <summary>Convenience base class for families.</summary>
    public abstract class CreatureFamilyBase : ICreatureFamily
    {
        private GeneSchema? _schema;

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public virtual int Order => 100;
        public virtual string PaletteRuleId => "default";
        public abstract IReadOnlyList<ArchetypeInfo> Archetypes { get; }

        public GeneSchema Schema => _schema ??= CreateSchema();

        /// <summary>Declares genes. Implementations call SharedGenes.Add* for colour, pattern and motion.</summary>
        protected abstract GeneSchema CreateSchema();

        public abstract FamilyStyleHints StyleHints(string archetype);
        public abstract void SampleAnatomy(double[] values, Rng rng, string archetype);
        public virtual void AdjustSample(double[] values, Rng rng, string archetype) { }
        public abstract CreatureAnatomy BuildAnatomy(CreatureGenome genome, CreatureRegistry registry);

        protected void Set(double[] values, string id, double v) => SharedGenes.Set(Schema, values, id, v);

        protected void SetChoice(double[] values, string id, string option)
        {
            var def = Schema.Get(id);
            int i = def.OptionIndex(option);
            if (i < 0) throw new ArgumentException($"Unknown option '{option}' for '{id}'.");
            values[Schema.IndexOf(id)] = i;
        }

        /// <summary>Normal sample around a centre, clamped to the gene range.</summary>
        protected void Around(double[] values, Rng rng, string id, double centre, double spread)
        {
            var def = Schema.Get(id);
            double v = rng.Gaussian(centre, spread, 2.2);
            values[Schema.IndexOf(id)] = def.Quantize(v);
        }

        protected static string PickWeighted(Rng rng, params (string id, double w)[] options)
        {
            var weights = new List<double>();
            foreach (var o in options) weights.Add(o.w);
            return options[rng.Weighted(weights)].id;
        }
    }

    /// <summary>Everything an extension can contribute to a registry.</summary>
    public interface ICreatureExtension
    {
        string Id { get; }
        void Register(CreatureRegistry registry);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CreatureExtensionAttribute : Attribute { }
}
