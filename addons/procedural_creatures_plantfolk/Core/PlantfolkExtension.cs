// Procedural Pixel Creatures - example extension "Plantfolk" (plant creatures).
//
// This addon is deliberately written like third-party code: it lives in its own folder, references
// the framework only through public API and is discovered automatically through the
// [CreatureExtension] attribute. It contributes one item for every extension point:
//   * a body plan family      -> PlantfolkFamily ("plantfolk")
//   * a shape block           -> "plantfolk.leaf" (leaves, petals, foliage clusters)
//   * a palette rule          -> "plantfolk" (bark/stem, leaf green, blossom colours)
//   * a locomotion module     -> "rootwalk" (waddling root legs that sway like plants)
// Removing the folder removes the family; nothing in the core refers to it.

using System;
using PixelCreatures.Core;
using PixelCreatures.Core.Anatomy;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Core.Motion;
using PixelCreatures.Core.Palettes;
using PixelCreatures.Core.Shapes;

namespace PixelCreatures.Plantfolk
{
    [CreatureExtension]
    public sealed class PlantfolkExtension : ICreatureExtension
    {
        public string Id => "plantfolk";

        public void Register(CreatureRegistry registry)
        {
            registry.RegisterPaletteRule(new PlantPaletteRule());
            registry.RegisterShapeBlock(new DelegateShapeBlock(LeafBlock.Id, "Plants",
                "Leaf or petal on a bone: two triangles around a midrib, optionally a spring chain that flutters in the wind. Parameters: length, width, spring (0/1), curl.",
                LeafBlock.Build));
            registry.RegisterLocomotion(RootWalkLocomotion.ModuleId, () => new RootWalkLocomotion());
            registry.RegisterFamily(new PlantfolkFamily());
        }
    }

    /// <summary>Leaves and petals: a pointed oval made of two triangles around a midrib bone chain.</summary>
    public static class LeafBlock
    {
        public const string Id = "plantfolk.leaf";

        public static ShapeResult Build(AnatomyBuilder b, ShapeParams p)
        {
            double len = p.Get("length", 5);
            double width = p.Get("width", 2);
            bool spring = p.Get("spring", 1) > 0.5;
            double curl = p.Get("curl", 0.3);
            Vec3 dir = p.Direction.NormalizedOr(Vec3.UnitY);
            Vec3 up = Math.Abs(dir.Y) > 0.9 ? Vec3.UnitX : Vec3.UnitY;
            int g = p.Group >= 0 ? p.Group : b.Group(p.Name, 0.3, p.Side, GroupFlags.NoFarShade, 0.1);
            int root = b.BoneAlong(p.Name, p.ParentBone, p.Position, dir, up, BoneRole.Antenna, p.Side, len * 0.5);
            Vec3 mid = p.Position + dir * (len * 0.5);
            Vec3 dir2 = Chains.RotateAround(dir, Vec3.Cross(dir, up).NormalizedOr(Vec3.UnitZ), curl);
            int tipBone = b.BoneAlong(p.Name + ".tip", root, mid, dir2, up, BoneRole.Antenna, p.Side, len * 0.5);
            Vec3 tip = mid + dir2 * (len * 0.5);
            int tipEnd = b.BoneAt(p.Name + ".end", tipBone, tip, BoneRole.Antenna, p.Side);
            // the leaf blade is spanned by the midrib and the side axis
            Vec3 side = Vec3.Cross(dir, up).NormalizedOr(Vec3.UnitZ) * (width * 0.5);
            var xr = b.RestWorld(root);
            var xt = b.RestWorld(tipBone);
            var xe = b.RestWorld(tipEnd);
            PrimitiveDef Tri(int ba, Vec3 a, int bb, Vec3 c, int bc, Vec3 d)
            {
                var t = b.Triangle(g, ba, a, bb, c, bc, d, p.Material, PatternDomain.Fin, p.Side);
                t.Flags |= PrimFlags.TwoSided | PrimFlags.NoShadow;
                t.DomainLength = len;
                t.Tag = p.Name;
                return t;
            }
            Vec3 l = mid + side, r = mid - side;
            Tri(root, Vec3.Zero, tipBone, xt.InversePoint(l), tipBone, Vec3.Zero);
            Tri(root, Vec3.Zero, tipBone, Vec3.Zero, tipBone, xt.InversePoint(r));
            Tri(tipBone, xt.InversePoint(l), tipEnd, Vec3.Zero, tipBone, Vec3.Zero);
            Tri(tipBone, Vec3.Zero, tipEnd, Vec3.Zero, tipBone, xt.InversePoint(r));
            _ = xr; _ = xe;
            var res = new ShapeResult();
            res.Bones.Add(root);
            res.Bones.Add(tipBone);
            res.Bones.Add(tipEnd);
            res.Groups.Add(g);
            if (spring)
            {
                res.Chain = new ChainRig
                {
                    Name = p.Name,
                    Bones = new[] { root, tipBone, tipEnd },
                    Lengths = new[] { len * 0.5, len * 0.5 },
                    TotalLength = len,
                    Side = p.Side,
                    Radii = new[] { 0.5, 0.5, 0.5 },
                    Stiffness = p.Get("stiffness", 0.55),
                    RestPitch = new[] { 0.0, curl },
                    RestYaw = new[] { 0.0, 0.0 },
                    Droop = p.Get("droop", 0.15),
                };
                b.Rig.Appendages.Add(res.Chain);
            }
            return res;
        }
    }

    /// <summary>Bark or stem (primary), leaf green (secondary) and blossom colours (accent).</summary>
    public sealed class PlantPaletteRule : DefaultPaletteRule
    {
        public override string Id => "plantfolk";

        public override void BuildInto(CreaturePalette pal, ColorGenes cg)
        {
            base.BuildInto(pal, cg);
            var p = PrimaryRamp(cg);
            int outline = pal.Ramp[(int)MaterialSlot.Primary, 0];
            int highlight = pal.Ramp[(int)MaterialSlot.Primary, 5];
            // leaves: a green in the OKLCH 115..155 range, nudged by the secondary shift gene
            double leafHue = cg.LeafHue >= 0 ? ColorMath.WrapHue(cg.LeafHue + 8 * cg.SecondaryShift) : 132 + 22 * cg.SecondaryShift;
            // autumn and blossom foliage is more saturated than summer green
            bool green = leafHue > 105 && leafHue < 175;
            double lc = DMath.Lerp(0.09, 0.15, cg.Saturation) * (green ? 1.0 : 1.25);
            double ll = DMath.Lerp(0.5, 0.7, cg.BellyLight);
            int lDeep = pal.Add(ColorMath.ToRgba(new Oklch(ll - 0.2, lc * 0.9, ColorMath.HueToward(leafHue, 285, 20 * cg.HueShift))), "leaf deep");
            int lShadow = pal.Add(ColorMath.ToRgba(new Oklch(ll - 0.1, lc, ColorMath.HueToward(leafHue, 285, 10 * cg.HueShift))), "leaf shadow");
            int lBase = pal.Add(ColorMath.ToRgba(new Oklch(ll, lc, leafHue)), "leaf");
            int lLight = pal.Add(ColorMath.ToRgba(new Oklch(Math.Min(0.92, ll + 0.09), lc * 0.85, ColorMath.HueToward(leafHue, 95, 16 * cg.HueShift))), "leaf light");
            pal.SetRamp(MaterialSlot.Secondary, outline, lDeep, lShadow, lBase, lLight, highlight);
            // blossoms: vivid petals whose hue follows the scheme gene
            double petalHue = cg.Scheme switch
            {
                "complementary" => ColorMath.WrapHue(cg.Hue + 180),
                "triadic" => ColorMath.WrapHue(cg.Hue + 120),
                "analogous" => ColorMath.WrapHue(cg.Hue + 40),
                "monochrome" => cg.Hue,
                _ => ColorMath.WrapHue(350 + 90 * cg.SecondaryShift),
            };
            int aDeep = pal.Add(ColorMath.ToRgba(new Oklch(0.5, 0.14, ColorMath.HueToward(petalHue, 285, 18))), "petal deep");
            int aShadow = pal.Add(ColorMath.ToRgba(new Oklch(0.61, 0.16, ColorMath.HueToward(petalHue, 285, 9))), "petal shadow");
            int aBase = pal.Add(ColorMath.ToRgba(new Oklch(0.72, 0.16, petalHue)), "petal");
            int aLight = pal.Add(ColorMath.ToRgba(new Oklch(0.84, 0.1, ColorMath.HueToward(petalHue, 95, 12))), "petal light");
            pal.SetRamp(MaterialSlot.Accent, outline, aDeep, aShadow, aBase, aLight, aLight);
            _ = p;
        }
    }

    /// <summary>
    /// Root legs: the legged module does the stepping; this module adds the plant character - a
    /// waddle with a bigger side-to-side roll, leaves that bounce, and a slow sway in the wind while
    /// standing. Shows how a new motion module can build on an existing one by composition.
    /// </summary>
    public sealed class RootWalkLocomotion : LocomotionModuleBase
    {
        public const string ModuleId = "rootwalk";
        public override string Id => ModuleId;
        private readonly LeggedLocomotion _legs = new LeggedLocomotion();

        public override void Initialize(MotorContext ctx) => _legs.Initialize(ctx);
        public override void Step(MotorContext ctx, double dt) => _legs.Step(ctx, dt);
        public override void ApplyLimbs(MotorContext ctx, in PoseParams p, SkeletonPose pose, CreaturePose output) => _legs.ApplyLimbs(ctx, p, pose, output);
        public override double Altitude(MotorContext ctx) => _legs.Altitude(ctx);
        public override double CycleFrequency(MotorContext ctx) => _legs.CycleFrequency(ctx);

        public override void Evaluate(MotorContext ctx, ref PoseParams p)
        {
            _legs.Evaluate(ctx, ref p);
            double w = ctx.MoveWeight * (1 - ctx.RestWeight);
            double run = ctx.RunBlend;
            double ph = _legs.Phase * DMath.TwoPi;
            // waddle: the whole plant rocks over the stepping root; running leans forwards and flails
            p.BodyRoll += DMath.Sin(ph) * DMath.Lerp(0.14, 0.22, run) * w;
            p.SpineYaw += DMath.Sin(ph + 0.6) * DMath.Lerp(0.1, 0.16, run) * w;
            p.HeadRoll += -DMath.Sin(ph) * 0.1 * w;
            p.BodyPitch += -0.16 * run * w;
            p.SpinePitch += -0.1 * run * w;
            p.ArmSwing *= DMath.Lerp(1.0, 1.8, run);
            p.ArmRaise += 0.35 * run * w;
            // wind sway while standing (loopable in export mode)
            double idle = (1 - ctx.MoveWeight) * (1 - ctx.RestWeight);
            double hz = 0.35 / ctx.Traits.TimeScale;
            if (ctx.ExportMode) hz = 1.0 / Math.Max(0.5, ctx.ExportLoop);
            p.SpinePitch += DMath.Sin(ctx.Time * DMath.TwoPi * hz) * 0.05 * idle;
            p.SpineYaw += DMath.Sin(ctx.Time * DMath.TwoPi * hz * 0.7 + 1.3) * 0.06 * idle;
            p.TentacleAmp += 0.4 * w + 0.2 * idle;
        }
    }
}
