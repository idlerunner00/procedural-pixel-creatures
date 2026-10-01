// Procedural Pixel Creatures - versioned JSON format for genomes/presets with validation and migrations.
//
// File layout (formatVersion 1):
// {
//   "format": "ppc.creature",            // fixed identifier
//   "formatVersion": 1,                  // layout version of this file (migrations upgrade older files)
//   "generatorVersion": 1,               // version of the generation rules that produced the creature
//   "family": "quadruped",
//   "name": "Velyvall",
//   "seeds": { "anatomy": "…", "color": "…", "pattern": "…", "motion": "…" },   // unsigned 64-bit decimals
//   "genes": { "size": 0.512, "form": "canine", "tail.tip": "tuft", "pattern.dark": true, ... },
//   "lineage": { "origin": "mutation", "generation": 2, "parents": ["…"], "provenance": {"head": "A"}, "mutationStrength": 0.35 },
//   "meta": { "createdWith": "…", "notes": "", "tags": [] }
// }
// Errors make a file unusable (wrong format, newer version, unknown family, broken values). Warnings
// describe automatic repairs (missing genes -> defaults, out of range -> clamped, unknown genes ignored).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using PixelCreatures.Core.Genetics;

namespace PixelCreatures.Core.Serialization
{
    public sealed class PresetMeta
    {
        public string CreatedWith = "Procedural Pixel Creatures " + CreatureFramework.FrameworkVersion;
        public string Notes = string.Empty;
        public List<string> Tags = new List<string>();
    }

    public sealed class GenomeLoadResult
    {
        public CreatureGenome? Genome;
        public PresetMeta Meta = new PresetMeta();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Migrations = new List<string>();
        public int FileFormatVersion;
        public bool Ok => Errors.Count == 0 && Genome != null;

        public string Describe()
        {
            var parts = new List<string>();
            if (Errors.Count > 0) parts.Add("Errors: " + string.Join(" | ", Errors));
            if (Warnings.Count > 0) parts.Add("Warnings: " + string.Join(" | ", Warnings));
            if (Migrations.Count > 0) parts.Add("Migration: " + string.Join(" | ", Migrations));
            return parts.Count == 0 ? "OK" : string.Join("\n", parts);
        }
    }

    /// <summary>One step of the format migration chain (fromVersion -> fromVersion + 1).</summary>
    public interface IGenomeMigration
    {
        int FromVersion { get; }
        string Description { get; }
        void Apply(JsonObject root);
    }

    public sealed class GenomeSerializer
    {
        public const string FormatId = "ppc.creature";
        public const int CurrentFormatVersion = CreatureGenome.CurrentFormatVersion;

        private readonly CreatureRegistry _registry;
        private readonly Dictionary<int, IGenomeMigration> _migrations = new Dictionary<int, IGenomeMigration>();
        /// <summary>Renamed genes per family: old id -> new id (applied on load with a note).</summary>
        private readonly Dictionary<string, Dictionary<string, string>> _geneAliases = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public GenomeSerializer(CreatureRegistry registry)
        {
            _registry = registry;
        }

        public void RegisterMigration(IGenomeMigration migration) => _migrations[migration.FromVersion] = migration;

        public void RegisterGeneAlias(string familyId, string oldId, string newId)
        {
            if (!_geneAliases.TryGetValue(familyId, out var map)) _geneAliases[familyId] = map = new Dictionary<string, string>(StringComparer.Ordinal);
            map[oldId] = newId;
        }

        // ------------------------------------------------------------------ write

        public string Serialize(CreatureGenome g, PresetMeta? meta = null)
        {
            var root = new JsonObject
            {
                ["format"] = FormatId,
                ["formatVersion"] = CurrentFormatVersion,
                ["generatorVersion"] = g.GeneratorVersion,
                ["family"] = g.FamilyId,
                ["name"] = g.Name,
                ["seeds"] = new JsonObject
                {
                    ["anatomy"] = g.AnatomySeed.ToString(CultureInfo.InvariantCulture),
                    ["color"] = g.ColorSeed.ToString(CultureInfo.InvariantCulture),
                    ["pattern"] = g.PatternSeed.ToString(CultureInfo.InvariantCulture),
                    ["motion"] = g.MotionSeed.ToString(CultureInfo.InvariantCulture),
                },
            };
            var genes = new JsonObject();
            foreach (var def in g.Schema.Genes)
            {
                double v = g[g.Schema.IndexOf(def.Id)];
                switch (def.Kind)
                {
                    case GeneKind.Choice: genes[def.Id] = def.Options[(int)v].Id; break;
                    case GeneKind.Bool: genes[def.Id] = v >= 0.5; break;
                    case GeneKind.Int: genes[def.Id] = (int)v; break;
                    default: genes[def.Id] = JsonValue.Create(Math.Round(v, 6)); break;
                }
            }
            root["genes"] = genes;
            var lin = g.Lineage;
            var lineage = new JsonObject
            {
                ["origin"] = lin.Origin,
                ["generation"] = lin.Generation,
                ["parents"] = new JsonArray(lin.ParentIds.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
                ["mutationStrength"] = JsonValue.Create(Math.Round(lin.MutationStrength, 4)),
            };
            if (lin.Provenance.Count > 0)
            {
                var prov = new JsonObject();
                foreach (var kv in lin.Provenance.OrderBy(k => k.Key, StringComparer.Ordinal)) prov[kv.Key] = kv.Value;
                lineage["provenance"] = prov;
            }
            root["lineage"] = lineage;
            meta ??= new PresetMeta();
            root["meta"] = new JsonObject
            {
                ["createdWith"] = meta.CreatedWith,
                ["notes"] = meta.Notes,
                ["tags"] = new JsonArray(meta.Tags.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray()),
            };
            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() }) + "\n";
        }

        // ------------------------------------------------------------------ read

        public GenomeLoadResult Deserialize(string json)
        {
            var result = new GenomeLoadResult();
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 });
            }
            catch (JsonException e)
            {
                result.Errors.Add($"Invalid JSON: {e.Message}");
                return result;
            }
            if (node is not JsonObject root)
            {
                result.Errors.Add("The file does not contain a JSON object.");
                return result;
            }
            string? format = GetString(root, "format");
            if (format != FormatId)
            {
                result.Errors.Add(format == null ? "Missing 'format' field: not a creature file." : $"Unknown file format '{format}' (expected '{FormatId}').");
                return result;
            }
            if (!TryGetInt(root, "formatVersion", out int fileVersion))
            {
                result.Errors.Add("The 'formatVersion' field is missing or is not an integer.");
                return result;
            }
            result.FileFormatVersion = fileVersion;
            if (fileVersion > CurrentFormatVersion)
            {
                result.Errors.Add($"The file uses format version {fileVersion}; this installation supports up to {CurrentFormatVersion}. Please update the framework.");
                return result;
            }
            if (fileVersion < 0)
            {
                result.Errors.Add($"Invalid format version {fileVersion}.");
                return result;
            }
            // migrations: fileVersion -> current, one step at a time
            int v = fileVersion;
            while (v < CurrentFormatVersion)
            {
                if (!_migrations.TryGetValue(v, out var m))
                {
                    result.Errors.Add($"No migration from format version {v} to {v + 1} is available.");
                    return result;
                }
                try { m.Apply(root); }
                catch (Exception e)
                {
                    result.Errors.Add($"Migration {v}->{v + 1} failed: {e.Message}");
                    return result;
                }
                result.Migrations.Add($"{v}->{v + 1}: {m.Description}");
                v++;
                root["formatVersion"] = v;
            }

            if (!TryGetInt(root, "generatorVersion", out int genVersion) || genVersion < 1)
            {
                result.Errors.Add("The 'generatorVersion' field is missing or invalid.");
                return result;
            }
            if (genVersion > CreatureFramework.GeneratorVersion)
            {
                result.Errors.Add($"The creature was generated with generator version {genVersion}; this installation supports up to {CreatureFramework.GeneratorVersion}. It cannot be reproduced exactly.");
                return result;
            }
            if (genVersion < CreatureFramework.GeneratorVersion)
                result.Warnings.Add($"Generator version {genVersion} is older than the current version ({CreatureFramework.GeneratorVersion}); appearance may differ.");

            string? familyId = GetString(root, "family");
            if (string.IsNullOrWhiteSpace(familyId))
            {
                result.Errors.Add("Missing 'family' field.");
                return result;
            }
            if (!_registry.TryGetFamily(familyId, out var family))
            {
                result.Errors.Add($"Unknown body-plan family '{familyId}'. Is the corresponding extension installed?");
                return result;
            }

            // seeds
            var seeds = new ulong[4];
            if (root["seeds"] is not JsonObject seedObj)
            {
                result.Errors.Add("The 'seeds' field is missing or is not an object.");
                return result;
            }
            string[] seedNames = { "anatomy", "color", "pattern", "motion" };
            for (int i = 0; i < 4; i++)
            {
                if (!TryGetSeed(seedObj, seedNames[i], out seeds[i], out string? err))
                {
                    result.Errors.Add(err!);
                    return result;
                }
            }

            // genes
            if (root["genes"] is not JsonObject geneObj)
            {
                result.Errors.Add("The 'genes' field is missing or is not an object.");
                return result;
            }
            var schema = family.Schema;
            var values = new double[schema.Count];
            for (int i = 0; i < values.Length; i++) values[i] = schema.Genes[i].Default;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            _geneAliases.TryGetValue(familyId, out var aliases);
            foreach (var kv in geneObj)
            {
                string id = kv.Key;
                if (aliases != null && aliases.TryGetValue(id, out var renamed))
                {
                    result.Migrations.Add($"Gene '{id}' has been renamed to '{renamed}'.");
                    id = renamed;
                }
                int idx = schema.IndexOf(id);
                if (idx < 0)
                {
                    result.Warnings.Add($"Unknown gene '{kv.Key}' was ignored.");
                    continue;
                }
                seen.Add(id);
                var def = schema.Genes[idx];
                if (!TryReadGene(def, kv.Value, out double val, out string? problem, out bool fatal))
                {
                    if (fatal)
                    {
                        result.Errors.Add(problem!);
                        continue;
                    }
                    result.Warnings.Add(problem!);
                }
                values[idx] = def.Quantize(val);
            }
            foreach (var def in schema.Genes)
                if (!seen.Contains(def.Id)) result.Warnings.Add($"Gene '{def.Id}' was missing and has been set to its default.");
            if (result.Errors.Count > 0) return result;

            string name = GetString(root, "name") ?? string.Empty;
            if (name.Length > 64) { name = name.Substring(0, 64); result.Warnings.Add("Name was truncated to 64 characters."); }
            var lineage = ReadLineage(root["lineage"] as JsonObject);
            result.Genome = new CreatureGenome(schema, genVersion, seeds, values, name, lineage);
            if (root["meta"] is JsonObject metaObj)
            {
                result.Meta.CreatedWith = GetString(metaObj, "createdWith") ?? result.Meta.CreatedWith;
                result.Meta.Notes = GetString(metaObj, "notes") ?? string.Empty;
                if (metaObj["tags"] is JsonArray tags)
                    foreach (var t in tags) if (t is JsonValue tv && tv.TryGetValue(out string? ts) && ts != null) result.Meta.Tags.Add(ts);
            }
            return result;
        }

        private static bool TryReadGene(GeneDef def, JsonNode? node, out double value, out string? problem, out bool fatal)
        {
            value = def.Default;
            problem = null;
            fatal = false;
            if (node is not JsonValue jv)
            {
                problem = $"Gene '{def.Id}': Value is missing or has the wrong type.";
                fatal = true;
                return false;
            }
            switch (def.Kind)
            {
                case GeneKind.Choice:
                {
                    if (!jv.TryGetValue(out string? s) || s == null)
                    {
                        problem = $"Gene '{def.Id}': Expected a choice value as text.";
                        fatal = true;
                        return false;
                    }
                    int i = def.OptionIndex(s);
                    if (i < 0)
                    {
                        problem = $"Gene '{def.Id}': Unknown option '{s}'; using default '{def.Options[(int)def.Default].Id}'.";
                        return false;
                    }
                    value = i;
                    return true;
                }
                case GeneKind.Bool:
                {
                    if (jv.TryGetValue(out bool b)) { value = b ? 1 : 0; return true; }
                    problem = $"Gene '{def.Id}': Expected a Boolean (true/false).";
                    fatal = true;
                    return false;
                }
                default:
                {
                    double d;
                    if (!jv.TryGetValue(out d))
                    {
                        if (jv.TryGetValue(out int iv)) d = iv;
                        else
                        {
                            problem = $"Gene '{def.Id}': Expected a number.";
                            fatal = true;
                            return false;
                        }
                    }
                    if (!DMath.IsFinite(d))
                    {
                        problem = $"Gene '{def.Id}': Value is not a finite number.";
                        fatal = true;
                        return false;
                    }
                    if (def.Kind == GeneKind.Int && Math.Abs(d - Math.Round(d)) > 1e-9)
                    {
                        problem = $"Gene '{def.Id}': Expected an integer; rounded {d.ToString(CultureInfo.InvariantCulture)}.";
                        value = Math.Round(d);
                        return false;
                    }
                    value = d;
                    if (d < def.Min - 1e-9 || d > def.Max + 1e-9)
                    {
                        problem = $"Gene '{def.Id}': {d.ToString(CultureInfo.InvariantCulture)} is outside [{def.Min.ToString(CultureInfo.InvariantCulture)}, {def.Max.ToString(CultureInfo.InvariantCulture)}] and was clamped.";
                        return false;
                    }
                    return true;
                }
            }
        }

        private static GenomeLineage ReadLineage(JsonObject? o)
        {
            if (o == null) return new GenomeLineage("imported", 0);
            string origin = GetString(o, "origin") ?? "imported";
            TryGetInt(o, "generation", out int gen);
            var parents = new List<string>();
            if (o["parents"] is JsonArray arr)
                foreach (var p in arr) if (p is JsonValue pv && pv.TryGetValue(out string? ps) && ps != null) parents.Add(ps);
            var prov = new Dictionary<string, string>(StringComparer.Ordinal);
            if (o["provenance"] is JsonObject po)
                foreach (var kv in po) if (kv.Value is JsonValue v && v.TryGetValue(out string? s) && s != null) prov[kv.Key] = s;
            double strength = 0;
            if (o["mutationStrength"] is JsonValue mv) mv.TryGetValue(out strength);
            return new GenomeLineage(origin, Math.Max(0, gen), parents, prov, strength);
        }

        private static string? GetString(JsonObject o, string key)
        {
            if (o[key] is JsonValue v && v.TryGetValue(out string? s)) return s;
            return null;
        }

        private static bool TryGetInt(JsonObject o, string key, out int value)
        {
            value = 0;
            if (o[key] is not JsonValue v) return false;
            if (v.TryGetValue(out int i)) { value = i; return true; }
            if (v.TryGetValue(out double d) && Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < int.MaxValue) { value = (int)Math.Round(d); return true; }
            return false;
        }

        private static bool TryGetSeed(JsonObject o, string key, out ulong seed, out string? error)
        {
            seed = 0;
            error = null;
            var node = o[key];
            if (node is not JsonValue v)
            {
                error = $"Seed '{key}' is missing.";
                return false;
            }
            if (v.TryGetValue(out string? s) && s != null)
            {
                if (ulong.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out seed)) return true;
                error = $"Seed '{key}' is not an unsigned 64-bit integer: '{s}'.";
                return false;
            }
            if (v.TryGetValue(out long l) && l >= 0) { seed = (ulong)l; return true; }
            error = $"Seed '{key}' must be supplied as a decimal number in a string.";
            return false;
        }
    }
}
