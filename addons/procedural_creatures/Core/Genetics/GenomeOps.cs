// Procedural Pixel Creatures - genetic operators: locks, mutation, crossover, regeneration.
// All operators are pure functions of their inputs and an explicit seed.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PixelCreatures.Core.Genetics
{
    /// <summary>Set of locked genes and gene groups. Locked genes survive regeneration and mutation.</summary>
    public sealed class GeneLocks
    {
        private readonly HashSet<string> _genes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _groups = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> LockedGenes => _genes;
        public IReadOnlyCollection<string> LockedGroups => _groups;

        public GeneLocks() { }

        public GeneLocks(IEnumerable<string> genes, IEnumerable<string> groups)
        {
            foreach (var g in genes) _genes.Add(g);
            foreach (var g in groups) _groups.Add(g);
        }

        public GeneLocks Clone() => new GeneLocks(_genes, _groups);

        public bool IsLocked(GeneDef gene) => _genes.Contains(gene.Id) || _groups.Contains(gene.Group);
        public bool IsGroupLocked(string group) => _groups.Contains(group);
        public bool IsGeneLocked(string id) => _genes.Contains(id);

        public void SetGene(string id, bool locked) { if (locked) _genes.Add(id); else _genes.Remove(id); }
        public void SetGroup(string id, bool locked) { if (locked) _groups.Add(id); else _groups.Remove(id); }
        public void Clear() { _genes.Clear(); _groups.Clear(); }
        public bool IsEmpty => _genes.Count == 0 && _groups.Count == 0;

        /// <summary>A stream seed is kept when any gene of that stream is locked.</summary>
        public bool IsStreamTouched(GeneSchema schema, GeneStream stream)
        {
            foreach (var g in schema.Genes)
                if (g.Stream == stream && IsLocked(g)) return true;
            return false;
        }
    }

    /// <summary>Which streams a mutation may change and how strongly.</summary>
    public sealed class MutationSettings
    {
        public double Strength { get; set; } = 0.35;
        public bool Anatomy { get; set; } = true;
        public bool Color { get; set; } = true;
        public bool Pattern { get; set; } = true;
        public bool Motion { get; set; } = true;
        /// <summary>Colors of offspring drift noticeably more than anatomy (see reference sheet).</summary>
        public double ColorBoost { get; set; } = 1.6;

        public bool Allows(GeneStream s) => s switch
        {
            GeneStream.Anatomy => Anatomy,
            GeneStream.Color => Color,
            GeneStream.Pattern => Pattern,
            _ => Motion,
        };

        public MutationSettings Clone() => (MutationSettings)MemberwiseClone();
    }

    public static class GenomeOps
    {
        /// <summary>Resamples every unlocked gene from a fresh sample of the same family.</summary>
        public static CreatureGenome Regenerate(CreatureGenome current, GeneLocks locks, CreatureGenome freshSample)
        {
            if (freshSample.FamilyId != current.FamilyId) throw new ArgumentException("Fresh sample must be of the same family.");
            var schema = current.Schema;
            var values = current.CopyValues();
            for (int i = 0; i < schema.Count; i++)
            {
                if (!locks.IsLocked(schema.Genes[i])) values[i] = freshSample[i];
            }
            var seeds = current.CopySeeds();
            var fresh = freshSample.CopySeeds();
            for (int s = 0; s < 4; s++)
            {
                if (!locks.IsStreamTouched(schema, (GeneStream)s)) seeds[s] = fresh[s];
            }
            var lineage = new GenomeLineage("regenerated", current.Lineage.Generation, new[] { current.ContentId });
            return new CreatureGenome(schema, current.GeneratorVersion, seeds, values, freshSample.Name, lineage);
        }

        /// <summary>Mutates a genome. Deterministic for (parent, settings, seed).</summary>
        public static CreatureGenome Mutate(CreatureGenome parent, MutationSettings settings, ulong seed, GeneLocks? locks = null)
        {
            var rng = new Rng(StableHash.Combine(seed, parent.ContentHash));
            var schema = parent.Schema;
            var values = parent.CopyValues();
            double k = DMath.Clamp(settings.Strength, 0, 1);
            for (int i = 0; i < schema.Count; i++)
            {
                var g = schema.Genes[i];
                // Always consume the same number of random draws per gene so that toggling a stream
                // or a lock does not shift the random sequence of the other genes.
                double r1 = rng.NextDouble();
                double r2 = rng.Gaussian();
                double r3 = rng.NextDouble();
                if (!settings.Allows(g.Stream) || (locks != null && locks.IsLocked(g))) continue;
                double boost = g.Stream == GeneStream.Color ? settings.ColorBoost : 1.0;
                values[i] = MutateValue(g, values[i], k * boost, r1, r2, r3);
            }

            var seeds = parent.CopySeeds();
            // Seeds carry fine details (jitter, spot placement). They re-roll rarely so that
            // offspring keep their family resemblance.
            double[] rerollChance = { 0.10 * k, 0.0, 0.35 * k, 0.5 * k };
            for (int s = 0; s < 4; s++)
            {
                double r = rng.NextDouble();
                ulong fresh = rng.NextULong();
                bool allowed = settings.Allows((GeneStream)s) && (locks == null || !locks.IsStreamTouched(schema, (GeneStream)s));
                if (allowed && r < rerollChance[s]) seeds[s] = fresh;
            }

            var lineage = new GenomeLineage("mutation", parent.Lineage.Generation + 1, new[] { parent.ContentId }, null, k);
            return new CreatureGenome(schema, parent.GeneratorVersion, seeds, values, parent.Name, lineage);
        }

        private static double MutateValue(GeneDef g, double v, double strength, double r1, double r2, double r3)
        {
            if (strength <= 0) return v;
            switch (g.Kind)
            {
                case GeneKind.Float:
                {
                    double sigma = g.MutationSigma * strength * g.Range;
                    if (g.Inheritance == InheritanceMode.Circular)
                    {
                        // occasional larger hue jumps give the colourful sibling variety of the reference
                        double jump = r3 < 0.18 * strength ? (r1 - 0.5) * g.Range * 0.9 * strength : 0.0;
                        double nv = v + r2 * sigma + jump;
                        nv = g.Min + DMath.Frac((nv - g.Min) / g.Range) * g.Range;
                        return g.Quantize(nv);
                    }
                    return g.Quantize(v + r2 * sigma);
                }
                case GeneKind.Int:
                {
                    if (r1 < g.MutationSigma * strength * 2.0) return g.Quantize(v + (r3 < 0.5 ? -1 : 1));
                    return v;
                }
                case GeneKind.Choice:
                {
                    if (r1 < g.MutationSigma * strength * 1.5 && g.Options.Count > 1)
                    {
                        int n = g.Options.Count;
                        int cur = (int)v;
                        int pick = (int)(r3 * (n - 1));
                        if (pick >= cur) pick++;
                        return pick;
                    }
                    return v;
                }
                case GeneKind.Bool:
                    return r1 < g.MutationSigma * strength ? 1.0 - v : v;
            }
            return v;
        }

        public sealed class CrossResult
        {
            public CreatureGenome Child { get; }
            public IReadOnlyDictionary<string, string> Provenance { get; }
            public CrossResult(CreatureGenome child, IReadOnlyDictionary<string, string> provenance) { Child = child; Provenance = provenance; }
        }

        public static bool CanCross(CreatureGenome a, CreatureGenome b, out string reason)
        {
            if (a.FamilyId != b.FamilyId)
            {
                reason = $"Incompatible body plans: '{a.FamilyId}' and '{b.FamilyId}' cannot be crossed.";
                return false;
            }
            if (a.Schema.Fingerprint() != b.Schema.Fingerprint())
            {
                reason = "The genomes use different schema versions.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary>Crosses two compatible parents. Linked gene groups are inherited together.</summary>
        public static CrossResult Cross(CreatureGenome a, CreatureGenome b, ulong seed, MutationSettings mutation)
        {
            if (!CanCross(a, b, out string reason)) throw new InvalidOperationException(reason);
            var rng = new Rng(StableHash.Combine(StableHash.Combine(seed, a.ContentHash), b.ContentHash));
            var schema = a.Schema;
            var linkages = schema.Genes.Select(g => g.Linkage).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
            var mode = new Dictionary<string, (int mode, double t)>(StringComparer.Ordinal);
            var provenance = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var link in linkages)
            {
                double r = rng.NextDouble();
                double t = rng.Range(0.25, 0.75);
                int m = r < 0.4 ? 0 : (r < 0.8 ? 1 : 2);
                mode[link] = (m, t);
                provenance[link] = m == 0 ? "A" : (m == 1 ? "B" : "A+B");
            }

            var values = new double[schema.Count];
            for (int i = 0; i < schema.Count; i++)
            {
                var g = schema.Genes[i];
                var (m, t) = mode[g.Linkage];
                double va = a[i], vb = b[i];
                if (m == 0) values[i] = va;
                else if (m == 1) values[i] = vb;
                else
                {
                    switch (g.Inheritance)
                    {
                        case InheritanceMode.Blend:
                            values[i] = g.Kind == GeneKind.Float ? DMath.Lerp(va, vb, t) : (t < 0.5 ? va : vb);
                            break;
                        case InheritanceMode.Circular:
                        {
                            double d = vb - va;
                            if (d > g.Range / 2) d -= g.Range;
                            if (d < -g.Range / 2) d += g.Range;
                            double nv = va + d * t;
                            values[i] = g.Min + DMath.Frac((nv - g.Min) / g.Range) * g.Range;
                            break;
                        }
                        default:
                            values[i] = t < 0.5 ? va : vb;
                            break;
                    }
                }
            }

            var seedsA = a.CopySeeds();
            var seedsB = b.CopySeeds();
            var seeds = new ulong[4];
            for (int s = 0; s < 4; s++)
            {
                // the seed of a stream follows the parent that contributed most of that stream's groups
                int fromA = 0, fromB = 0;
                foreach (var g in schema.Genes)
                {
                    if ((int)g.Stream != s) continue;
                    var m = mode[g.Linkage].mode;
                    if (m == 0) fromA++; else if (m == 1) fromB++;
                }
                seeds[s] = fromA >= fromB ? seedsA[s] : seedsB[s];
            }

            var lineage = new GenomeLineage("crossover", Math.Max(a.Lineage.Generation, b.Lineage.Generation) + 1,
                new[] { a.ContentId, b.ContentId }, provenance, mutation.Strength);
            var child = new CreatureGenome(schema, a.GeneratorVersion, seeds, values, a.Name, lineage);
            if (mutation.Strength > 0)
            {
                var mutated = Mutate(child, mutation, rng.NextULong());
                child = mutated.WithLineage(lineage);
            }
            return new CrossResult(child, provenance);
        }
    }
}
