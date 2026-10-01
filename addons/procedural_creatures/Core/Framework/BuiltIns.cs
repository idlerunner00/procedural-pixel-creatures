// Procedural Pixel Creatures - registration of all built-in content.

using PixelCreatures.Core.Families;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Core
{
    public static class BuiltIns
    {
        public static void RegisterAll(CreatureRegistry r)
        {
            r.RegisterPaletteRule(new DefaultPaletteRule());
            foreach (var m in MaterialStyles.All) r.RegisterMaterial(m);
            BuiltInBlocks.RegisterAll(r);
            r.RegisterLocomotion("legged", () => new LeggedLocomotion());
            r.RegisterLocomotion("flight", () => new FlightLocomotion());
            r.RegisterLocomotion("swim", () => new SwimLocomotion());
            r.RegisterLocomotion("serpentine", () => new SerpentineLocomotion());
            r.RegisterLocomotion("hop", () => new HopLocomotion());
            r.RegisterLocomotion("float", () => new FloatLocomotion());
            r.RegisterFamily(new QuadrupedFamily());
            r.RegisterFamily(new BipedFamily());
            r.RegisterFamily(new ReptileFamily());
            r.RegisterFamily(new ArthropodFamily());
            r.RegisterFamily(new WingedFamily());
            r.RegisterFamily(new SerpentFamily());
            r.RegisterFamily(new AquaticFamily());
            r.RegisterFamily(new AmorphousFamily());
        }
    }
}
