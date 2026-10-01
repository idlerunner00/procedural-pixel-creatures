// Procedural Pixel Creatures - registry of families, shape blocks, palette rules, material styles
// and motion modules. This is the single place where extensions plug in.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core
{
    public sealed class CreatureRegistry
    {
        private readonly Dictionary<string, ICreatureFamily> _families = new Dictionary<string, ICreatureFamily>(StringComparer.Ordinal);
        private readonly Dictionary<string, IPaletteRule> _paletteRules = new Dictionary<string, IPaletteRule>(StringComparer.Ordinal);
        private readonly Dictionary<string, MaterialStyle> _materials = new Dictionary<string, MaterialStyle>(StringComparer.Ordinal);
        private readonly Dictionary<string, IShapeBlock> _shapeBlocks = new Dictionary<string, IShapeBlock>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<ILocomotionModule>> _locomotion = new Dictionary<string, Func<ILocomotionModule>>(StringComparer.Ordinal);
        private readonly List<string> _extensions = new List<string>();
        private readonly object _lock = new object();

        public IReadOnlyList<string> Extensions => _extensions;

        public static CreatureRegistry CreateDefault()
        {
            var r = new CreatureRegistry();
            BuiltIns.RegisterAll(r);
            return r;
        }

        private static CreatureRegistry? _shared;
        private static readonly object SharedLock = new object();

        /// <summary>Process wide default registry with built-ins and all discovered extensions.</summary>
        public static CreatureRegistry Shared
        {
            get
            {
                lock (SharedLock)
                {
                    if (_shared == null)
                    {
                        var r = CreateDefault();
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal))
                            r.DiscoverExtensions(asm);
                        _shared = r;
                    }
                    return _shared;
                }
            }
        }

        // ------------------------------------------------------------------ families

        public void RegisterFamily(ICreatureFamily family)
        {
            lock (_lock)
            {
                if (_families.ContainsKey(family.Id)) throw new InvalidOperationException($"Family '{family.Id}' is already registered.");
                _families[family.Id] = family;
            }
        }

        public bool TryGetFamily(string id, out ICreatureFamily family)
        {
            lock (_lock) return _families.TryGetValue(id, out family!);
        }

        public ICreatureFamily GetFamily(string id)
        {
            if (!TryGetFamily(id, out var f)) throw new KeyNotFoundException($"Unknown creature family '{id}'.");
            return f;
        }

        public IReadOnlyList<ICreatureFamily> Families
        {
            get
            {
                lock (_lock) return _families.Values.OrderBy(f => f.Order).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
            }
        }

        // ------------------------------------------------------------------ palette rules & materials

        public void RegisterPaletteRule(IPaletteRule rule)
        {
            lock (_lock) _paletteRules[rule.Id] = rule;
        }

        public IPaletteRule GetPaletteRule(string id)
        {
            lock (_lock)
            {
                if (_paletteRules.TryGetValue(id, out var r)) return r;
                return _paletteRules["default"];
            }
        }

        public void RegisterMaterial(MaterialStyle style)
        {
            lock (_lock) _materials[style.Id] = style;
        }

        public MaterialStyle GetMaterial(string id)
        {
            lock (_lock) return _materials.TryGetValue(id, out var m) ? m : MaterialStyles.Hide;
        }

        public IReadOnlyList<MaterialStyle> Materials
        {
            get { lock (_lock) return _materials.Values.OrderBy(m => m.Id, StringComparer.Ordinal).ToList(); }
        }

        // ------------------------------------------------------------------ shape blocks

        public void RegisterShapeBlock(IShapeBlock block)
        {
            lock (_lock) _shapeBlocks[block.Id] = block;
        }

        public IShapeBlock GetShapeBlock(string id)
        {
            lock (_lock)
            {
                if (_shapeBlocks.TryGetValue(id, out var b)) return b;
            }
            throw new KeyNotFoundException($"Unknown shape block '{id}'.");
        }

        public bool HasShapeBlock(string id) { lock (_lock) return _shapeBlocks.ContainsKey(id); }

        public IReadOnlyList<IShapeBlock> ShapeBlocks
        {
            get { lock (_lock) return _shapeBlocks.Values.OrderBy(b => b.Id, StringComparer.Ordinal).ToList(); }
        }

        // ------------------------------------------------------------------ motion modules

        public void RegisterLocomotion(string id, Func<ILocomotionModule> factory)
        {
            lock (_lock) _locomotion[id] = factory;
        }

        public ILocomotionModule CreateLocomotion(string id)
        {
            lock (_lock)
            {
                if (_locomotion.TryGetValue(id, out var f)) return f();
            }
            throw new KeyNotFoundException($"Unknown locomotion module '{id}'.");
        }

        public bool HasLocomotion(string id) { lock (_lock) return _locomotion.ContainsKey(id); }

        public IReadOnlyList<string> LocomotionModules
        {
            get { lock (_lock) return _locomotion.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); }
        }

        // ------------------------------------------------------------------ extensions

        public void RegisterExtension(ICreatureExtension extension)
        {
            lock (_lock)
            {
                if (_extensions.Contains(extension.Id)) return;
                _extensions.Add(extension.Id);
            }
            extension.Register(this);
        }

        /// <summary>Registers every class marked [CreatureExtension] that implements ICreatureExtension.</summary>
        public int DiscoverExtensions(Assembly assembly)
        {
            int count = 0;
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
            foreach (var t in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (t.IsAbstract || !typeof(ICreatureExtension).IsAssignableFrom(t)) continue;
                if (t.GetCustomAttribute<CreatureExtensionAttribute>() == null) continue;
                if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                RegisterExtension((ICreatureExtension)Activator.CreateInstance(t)!);
                count++;
            }
            return count;
        }
    }
}
