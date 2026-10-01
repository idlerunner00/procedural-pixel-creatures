// Procedural Pixel Creature Workshop - editing state: current genome, locks, undo/redo, saved state.
// Pure C# (no nodes) so it is testable in the self-test.

using System;
using System.Collections.Generic;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;

namespace PixelCreatures.Workshop
{
    public sealed class UndoStack
    {
        private readonly List<(CreatureGenome genome, string label, string coalesce)> _items = new List<(CreatureGenome, string, string)>();
        private int _index = -1;
        public int Capacity { get; }

        public UndoStack(int capacity = 250) { Capacity = capacity; }

        public int Count => _items.Count;
        public int Index => _index;
        public bool CanUndo => _index > 0;
        public bool CanRedo => _index < _items.Count - 1;
        public string UndoLabel => CanUndo ? _items[_index].label : string.Empty;
        public string RedoLabel => CanRedo ? _items[_index + 1].label : string.Empty;

        public void Reset(CreatureGenome genome, string label)
        {
            _items.Clear();
            _items.Add((genome, label, string.Empty));
            _index = 0;
        }

        /// <summary>Pushes a new state. Consecutive pushes with the same non-empty coalesce key merge into one step.</summary>
        public void Push(CreatureGenome genome, string label, string coalesceKey = "")
        {
            if (_index >= 0 && _index < _items.Count - 1) _items.RemoveRange(_index + 1, _items.Count - _index - 1);
            if (coalesceKey.Length > 0 && _index > 0 && _items[_index].coalesce == coalesceKey)
            {
                _items[_index] = (genome, label, coalesceKey);
                return;
            }
            _items.Add((genome, label, coalesceKey));
            _index = _items.Count - 1;
            if (_items.Count > Capacity)
            {
                _items.RemoveAt(0);
                _index--;
            }
        }

        /// <summary>Ends a coalescing sequence (e.g. slider released).</summary>
        public void Seal()
        {
            if (_index >= 0) _items[_index] = (_items[_index].genome, _items[_index].label, string.Empty);
        }

        public CreatureGenome? Undo()
        {
            if (!CanUndo) return null;
            _index--;
            return _items[_index].genome;
        }

        public CreatureGenome? Redo()
        {
            if (!CanRedo) return null;
            _index++;
            return _items[_index].genome;
        }
    }

    public sealed class WorkshopState
    {
        public CreatureGenome Genome { get; private set; }
        public CreatureGenome SavedGenome { get; private set; }
        public string? SavedPath { get; private set; }
        public GeneLocks Locks { get; } = new GeneLocks();
        public UndoStack History { get; } = new UndoStack();
        public CreatureRegistry Registry { get; }

        /// <summary>Raised after the genome changed (argument: human readable reason).</summary>
        public event Action<string>? GenomeChanged;
        public event Action? LocksChanged;

        public WorkshopState(CreatureRegistry registry, CreatureGenome initial)
        {
            Registry = registry;
            Genome = initial;
            SavedGenome = initial;
            History.Reset(initial, "Start");
        }

        public ICreatureFamily Family => Registry.GetFamily(Genome.FamilyId);
        public bool IsDirty => !Genome.ContentEquals(SavedGenome) || Genome.Name != SavedGenome.Name;

        public void Set(CreatureGenome genome, string reason, string coalesceKey = "", bool record = true)
        {
            Genome = genome;
            if (record) History.Push(genome, reason, coalesceKey);
            GenomeChanged?.Invoke(reason);
        }

        public void SetGene(string id, double value, bool dragging)
        {
            var def = Genome.Schema.Get(id);
            double q = def.Quantize(value);
            if (q == Genome.Get(id)) return;
            var g = Genome.With(id, q, new GenomeLineage("edited", Genome.Lineage.Generation, new[] { Genome.ContentId }));
            Set(g, $"{def.Label} = {def.FormatValue(q)}", dragging ? "gene:" + id : string.Empty);
        }

        public void EndDrag() => History.Seal();

        public void NewCreature(string familyId, ulong seed)
        {
            var g = GenomeFactory.Sample(Registry.GetFamily(familyId), seed);
            // locks only make sense within one family
            if (familyId != Genome.FamilyId) Locks.Clear();
            else if (!Locks.IsEmpty) g = GenomeFactory.RegenerateUnlocked(Family, Genome, Locks, seed);
            Set(g, $"New creature (seed {seed})");
        }

        public void RegenerateUnlocked(ulong seed)
        {
            var g = GenomeFactory.RegenerateUnlocked(Family, Genome, Locks, seed);
            Set(g, $"Rerolled unlocked traits (seed {seed})");
        }

        public bool Undo()
        {
            var g = History.Undo();
            if (g == null) return false;
            Genome = g;
            GenomeChanged?.Invoke("Undo");
            return true;
        }

        public bool Redo()
        {
            var g = History.Redo();
            if (g == null) return false;
            Genome = g;
            GenomeChanged?.Invoke("Redo");
            return true;
        }

        public void MarkSaved(string? path)
        {
            SavedGenome = Genome;
            SavedPath = path;
            GenomeChanged?.Invoke("Saved");
        }

        /// <summary>Loads a genome as the new saved state (e.g. from a preset or gallery).</summary>
        public void Load(CreatureGenome genome, string? path, string reason)
        {
            if (genome.FamilyId != Genome.FamilyId) Locks.Clear();
            SavedGenome = genome;
            SavedPath = path;
            Set(genome, reason);
        }

        public void ResetToSaved()
        {
            Set(SavedGenome, "Reset to the saved state");
        }

        public void SetLock(string? gene, string? group, bool locked)
        {
            if (gene != null) Locks.SetGene(gene, locked);
            if (group != null) Locks.SetGroup(group, locked);
            LocksChanged?.Invoke();
        }
    }
}
