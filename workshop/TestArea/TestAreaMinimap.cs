// Procedural Pixel Creature Workshop - minimap of the test area: the whole map scaled down, a dot per
// creature (the controlled one highlighted) and the visible area as a frame. Click or drag to move the camera.

using Godot;

namespace PixelCreatures.Workshop
{
    public partial class TestAreaMinimap : Control
    {
        public TestArea? Area;
        private Texture2D? _texture;
        private bool _dragging;

        private static readonly Color Outline = new Color(0.06f, 0.07f, 0.09f, 0.9f);
        private static readonly Color CreatureDot = new Color(0.96f, 0.94f, 0.86f);
        private static readonly Color HiddenDot = new Color(0.96f, 0.94f, 0.86f, 0.55f);
        private static readonly Color ViewFrame = new Color(1f, 1f, 1f, 0.85f);

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            TooltipText = "Minimap: click or drag to move the camera (M hides it)";
        }

        public void SetMap(Texture2D? texture, int width, int height)
        {
            _texture = texture;
            CustomMinimumSize = new Vector2(width, height);
            Size = CustomMinimumSize;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var area = Area;
            if (area == null || _texture == null || area.Map == null) return;
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(r.Grow(3), UiKit.Line);
            DrawRect(r.Grow(2), UiKit.Panel);
            DrawTextureRect(_texture, r, false);
            var scale = new Vector2(Size.X / area.Map.Width, Size.Y / area.Map.Height);
            var player = area.Player;
            foreach (var a in area.AllActors())
            {
                if (a == player) continue;
                var p = (a.Position * scale).Floor();
                DrawRect(new Rect2(p - new Vector2(1, 1), new Vector2(3, 3)), Outline);
                DrawRect(new Rect2(p, new Vector2(1, 1)), a.IsCulled ? HiddenDot : CreatureDot);
            }
            if (player != null)
            {
                var p = (player.Position * scale).Floor();
                DrawRect(new Rect2(p - new Vector2(2, 2), new Vector2(5, 5)), Outline);
                DrawRect(new Rect2(p - new Vector2(1, 1), new Vector2(3, 3)), UiKit.Accent);
            }
            var view = area.ViewRect;
            var frame = new Rect2(view.Position * scale, view.Size * scale).Intersection(r);
            DrawRect(frame, ViewFrame, false, 1f);
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                _dragging = mb.Pressed;
                if (mb.Pressed) Jump(mb.Position);
                AcceptEvent();
            }
            else if (e is InputEventMouseMotion mm && _dragging)
            {
                Jump(mm.Position);
                AcceptEvent();
            }
        }

        private void Jump(Vector2 local)
        {
            var area = Area;
            if (area?.Map == null || Size.X <= 0 || Size.Y <= 0) return;
            area.LookAt(new Vector2(local.X / Size.X * area.Map.Width, local.Y / Size.Y * area.Map.Height));
        }
    }
}
