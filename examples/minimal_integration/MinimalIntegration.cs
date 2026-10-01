// Procedural Pixel Creatures - minimal integration example (no workshop code involved).
//
// Shows the three ways to get a creature into a game:
//   1. place CreatureActor.tscn in a scene and set FamilyId + SeedText or PresetPath in the inspector,
//   2. instantiate an actor from code and give it a sampled genome (family + seed),
//   3. derive new genomes in code (here: a mutated child of the player creature).
// Then drive it: MoveVelocity (ground pixels per second), FaceDirection, TriggerAction, TriggerHit, Resting.
//
// Controls: WASD / arrow keys move the player creature, Shift runs, Space acts, R rests, H hits.

using Godot;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;

namespace PixelCreatures.Examples
{
    public partial class MinimalIntegration : Node2D
    {
        private CreatureActor _player = null!;
        private CreatureActor _child = null!;

        public override void _Ready()
        {
            // 1) placed in the scene (see MinimalIntegration.tscn)
            _player = GetNode<CreatureActor>("Creatures/Player");

            // 2) created from code: family id + seed (deterministic: same seed, same creature)
            var snake = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn").Instantiate<CreatureActor>();
            snake.FamilyId = string.Empty; // the genome is set below
            snake.GroundPosition = new Vector2(70, 40);
            GetNode("Creatures").AddChild(snake);
            snake.SetGenome(CreatureRuntime.Sample("serpent", 1234), async: true);
            snake.MoveVelocity = new Vector2(-12, 4);

            // 3) genomes are plain data: mutate the player's genome into a child creature
            _child = GD.Load<PackedScene>("res://addons/procedural_creatures/Runtime/CreatureActor.tscn").Instantiate<CreatureActor>();
            _child.FamilyId = string.Empty;
            _child.GroundPosition = new Vector2(-40, 30);
            GetNode("Creatures").AddChild(_child);
            _player.ModelChanged += () =>
            {
                if (_child.Model != null || _player.Genome == null) return;
                var kid = GenomeOps.Mutate(_player.Genome, new MutationSettings { Strength = 0.3 }, seed: 42, locks: null).WithName("Kind");
                _child.SetGenome(kid, async: true);
            };
            if (_player.Model != null) _player.EmitSignal(CreatureActor.SignalName.ModelChanged);
        }

        public override void _Process(double delta)
        {
            if (_player.Model == null) return;
            var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            bool run = Input.IsKeyPressed(Key.Shift);
            double speed = run ? _player.Model.Motion.RunSpeed : _player.Model.Motion.WalkSpeed;
            _player.MoveVelocity = input * (float)speed;
            if (Input.IsActionJustPressed("creature_action")) _player.TriggerAction();
            if (Input.IsActionJustPressed("creature_rest")) _player.Resting = !_player.Resting;
            if (Input.IsActionJustPressed("creature_hit")) _player.TriggerHit(new Vector2(1, 0));
            // the child follows the player at a distance
            var toPlayer = _player.GroundPosition - _child.GroundPosition;
            _child.MoveVelocity = toPlayer.Length() > 40 && _child.Model != null ? toPlayer.Normalized() * (float)_child.Model.Motion.WalkSpeed : Vector2.Zero;
        }
    }
}
