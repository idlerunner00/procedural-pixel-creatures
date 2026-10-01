// Procedural Pixel Creature Workshop - pixel grid of up to 100 animated creatures in one SubViewport.
// Every creature stands in its own cell (swimmers over water), walks in place and is rasterized only
// while its cell is on screen (CreatureActor.CullOffscreen). Names, favourites and the selection are
// drawn as crisp UI text on top of the integer-scaled pixel image.

using System;
using System.Collections.Generic;
using Godot;
using PixelCreatures.Core;
using PixelCreatures.Core.Genetics;
using PixelCreatures.Runtime;

namespace PixelCreatures.Workshop
{
    /// <summary>One creature of the overview.</summary>
    public sealed class OverviewEntry
    {
        /// <summary>Position in generation order (stable, used for the "Order: generation" sorting).</summary>
        public int Index;
        public CreatureGenome Genome = null!;
        public CreatureModel Model = null!;
        public CreatureActor? Actor;
        public bool Favorite;
        public string Family = string.Empty;
        public string FamilyLabel = string.Empty;
        public string Archetype = string.Empty;
        public string ArchetypeLabel = string.Empty;
        /// <summary>Seed the genome was sampled from (random source) or the mutation seed (offspring).</summary>
        public ulong Seed;
        /// <summary>Content of the rest pose (direction SE) relative to the ground point, logical pixels.</summary>
        public Vector2 ContentCenter;
        public Vector2I ContentSize;
        public bool Aquatic;
        public double Size;
        public double Hue;
    }

    /// <summary>Draws the ground of every visible cell (water for swimmers) and thin cell borders.</summary>
    public partial class OverviewCells : Node2D
    {
        public OverviewGrid Grid = null!;

        public override void _Draw()
        {
            var g = Grid;
            if (g.Entries.Count == 0) return;
            var view = g.VisibleWorldRect.Grow(2);
            var line = Color.Color8(36, 52, 34);
            var lineWater = Color.Color8(30, 58, 84);
            for (int i = 0; i < g.Entries.Count; i++)
            {
                var r = g.CellRect(i);
                if (i < g.Columns && g.TopPad > 0) r = new Rect2(r.Position.X, r.Position.Y - g.TopPad, r.Size.X, r.Size.Y + g.TopPad);
                if (!view.Intersects(r)) continue;
                var e = g.Entries[i];
                var kind = e.Aquatic ? StageBackground.Water : g.Background;
                if (kind == StageBackground.Neutral || kind == StageBackground.Dark)
                    DrawRect(r, kind == StageBackground.Neutral ? Color.Color8(70, 76, 84) : Color.Color8(24, 26, 32));
                else
                    DrawTextureRectRegion(GroundLayer.TileFor(kind), r, new Rect2((i * 37) % 64, (i * 23) % 64, r.Size.X, r.Size.Y));
                var c = e.Aquatic ? lineWater : line;
                DrawRect(new Rect2(r.Position.X, r.End.Y - 1, r.Size.X, 1), c);
                DrawRect(new Rect2(r.End.X - 1, r.Position.Y, 1, r.Size.Y), c);
            }
        }
    }

    public partial class OverviewGrid : Control
    {
        public SubViewport Viewport { get; private set; } = null!;
        public TextureRect Display { get; private set; } = null!;
        public Node2D World { get; private set; } = null!;
        public Node2D Entities { get; private set; } = null!;
        public Camera2D Camera { get; private set; } = null!;
        public VScrollBar ScrollBar { get; private set; } = null!;

        public List<OverviewEntry> Entries { get; } = new List<OverviewEntry>();
        public StageBackground Background { get; set; } = StageBackground.Grass;
        /// <summary>Integer zoom (1..6); 0 = automatic (at least three columns).</summary>
        public int Zoom { get; private set; }
        public int EffectiveZoom { get; private set; } = 2;
        public Vector2I CellSize { get; private set; } = new Vector2I(112, 88);
        public int Columns { get; private set; } = 1;
        public int Rows { get; private set; }
        public float ScrollY { get; private set; }
        public float MaxScroll { get; private set; }
        public int Selected { get; set; } = -1;
        public int Hover { get; private set; } = -1;
        public bool ShowNames { get; set; } = true;
        public Rect2 VisibleWorldRect { get; private set; }
        /// <summary>Ground above the first row so that tall creatures there are not cut off.</summary>
        public int TopPad { get; private set; }

        /// <summary>Raised for a left click on a cell (display index).</summary>
        public event Action<int>? CellClicked;
        /// <summary>Raised for a double click on a cell.</summary>
        public event Action<int>? CellActivated;
        public event Action<int>? HoverChanged;

        private OverviewCells _cells = null!;
        private Control _overlay = null!;
        private bool _dragging;
        private float _gridWidth;

        public override void _Ready()
        {
            ClipContents = true;
            MouseFilter = MouseFilterEnum.Stop;
            // no keyboard focus: arrow keys would move the GUI focus instead of the selection
            FocusMode = FocusModeEnum.None;
            Viewport = new SubViewport
            {
                Name = "Viewport",
                Size = new Vector2I(320, 200),
                TransparentBg = false,
                CanvasItemDefaultTextureFilter = Godot.Viewport.DefaultCanvasItemTextureFilter.Nearest,
                Snap2DTransformsToPixel = true,
                Snap2DVerticesToPixel = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                Disable3D = true,
            };
            AddChild(Viewport);
            World = new Node2D { Name = "World" };
            Viewport.AddChild(World);
            // ground tiles are sampled with offsets per cell and wrap around
            _cells = new OverviewCells { Name = "Cells", Grid = this, TextureRepeat = TextureRepeatEnum.Enabled };
            World.AddChild(_cells);
            Entities = new Node2D { Name = "Entities", YSortEnabled = true };
            World.AddChild(Entities);
            Camera = new Camera2D { Name = "Camera", PositionSmoothingEnabled = false };
            World.AddChild(Camera);
            Camera.MakeCurrent();
            Display = new TextureRect
            {
                Name = "Display",
                Texture = Viewport.GetTexture(),
                TextureFilter = TextureFilterEnum.Nearest,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            AddChild(Display);
            _overlay = new OverlayDrawer { Grid = this, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(_overlay);
            ScrollBar = new VScrollBar { MouseFilter = MouseFilterEnum.Stop, Step = 1 };
            ScrollBar.ValueChanged += v => SetScroll((float)v);
            AddChild(ScrollBar);
            Resized += Relayout;
            Relayout();
        }

        // ------------------------------------------------------------------ layout

        /// <summary>Sets the creatures to show (display order); their actors must be children of Entities.</summary>
        public void SetEntries(IReadOnlyList<OverviewEntry> entries, Vector2I cellSize)
        {
            Entries.Clear();
            Entries.AddRange(entries);
            CellSize = cellSize;
            // tall creatures stick out of their cell; any of them may land in the first row
            float overflow = 0;
            foreach (var e in Entries) overflow = Math.Max(overflow, (e.ContentSize.Y - (cellSize.Y - 9)) * 0.5f + 2);
            TopPad = Math.Clamp((int)Math.Ceiling(overflow), 0, 48);
            Selected = Math.Min(Selected, Entries.Count - 1);
            Hover = -1;
            Relayout();
        }

        public void SetZoom(int zoom)
        {
            // keep the centre row in view while zooming
            float centre = ScrollY + VisibleWorldRect.Size.Y / 2;
            Zoom = Math.Clamp(zoom, 0, 6);
            Relayout();
            SetScroll(centre - VisibleWorldRect.Size.Y / 2);
        }

        public void Relayout()
        {
            if (Display == null) return;
            var s = Size;
            float barW = MaxScroll > 0 || Rows * CellSize.Y > s.Y / Math.Max(1, EffectiveZoom) ? 12 : 0;
            float availW = Math.Max(32, s.X - barW);
            int zoom = Zoom;
            if (zoom <= 0)
            {
                // largest zoom (2..4) that shows every creature without scrolling, otherwise 2x and scroll;
                // 1x only when 2x would leave fewer than three columns
                zoom = 2;
                for (int z = 4; z >= 2; z--)
                {
                    int cols = Math.Max(1, (int)(availW / z) / Math.Max(1, CellSize.X));
                    int rows = (int)(s.Y / z) / Math.Max(1, CellSize.Y);
                    if (cols >= Math.Min(3, Math.Max(1, Entries.Count)) && cols * Math.Max(1, rows) >= Entries.Count) { zoom = z; break; }
                }
                if (zoom == 2 && (int)(availW / 2) / Math.Max(1, CellSize.X) < 3) zoom = 1;
            }
            EffectiveZoom = Math.Clamp(zoom, 1, 6);
            int lw = Math.Max(16, (int)Math.Ceiling(availW / EffectiveZoom));
            int lh = Math.Max(16, (int)Math.Ceiling(s.Y / EffectiveZoom));
            if ((lw & 1) == 1) lw++;
            if ((lh & 1) == 1) lh++;
            if (Viewport.Size != new Vector2I(lw, lh)) Viewport.Size = new Vector2I(lw, lh);
            Display.Size = new Vector2(lw * EffectiveZoom, lh * EffectiveZoom);
            Display.Position = Vector2.Zero;
            _overlay.Position = Vector2.Zero;
            _overlay.Size = new Vector2(availW, s.Y);
            Columns = Math.Max(1, lw / Math.Max(1, CellSize.X));
            Rows = Entries.Count == 0 ? 0 : (Entries.Count + Columns - 1) / Columns;
            _gridWidth = Columns * CellSize.X;
            MaxScroll = Math.Max(0, Rows * CellSize.Y + TopPad - lh);
            ScrollBar.Visible = MaxScroll > 0;
            ScrollBar.Position = new Vector2(s.X - 12, 0);
            ScrollBar.Size = new Vector2(12, s.Y);
            ScrollBar.MaxValue = Rows * CellSize.Y + TopPad;
            ScrollBar.Page = lh;
            SetScroll(ScrollY);
            PlaceActors();
        }

        public void SetScroll(float y)
        {
            ScrollY = Mathf.Round(Math.Clamp(y, 0, MaxScroll));
            if (ScrollBar != null && Math.Abs(ScrollBar.Value - ScrollY) > 0.5) ScrollBar.SetValueNoSignal(ScrollY);
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (Camera == null || Viewport == null) return;
            var vs = new Vector2(Viewport.Size.X, Viewport.Size.Y);
            // narrow grids are centred, wide ones start at the left edge
            float cx = _gridWidth < vs.X ? _gridWidth / 2 : vs.X / 2;
            Camera.Position = new Vector2(Mathf.Round(cx), Mathf.Round(ScrollY + vs.Y / 2));
            VisibleWorldRect = new Rect2(Camera.Position - vs / 2, vs);
            _cells?.QueueRedraw();
        }

        /// <summary>Puts every actor into its cell: the content of its rest pose is centred, a little above the label.</summary>
        public void PlaceActors()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                var a = Entries[i].Actor;
                if (a == null || !IsInstanceValid(a)) continue;
                var r = CellRect(i);
                float labelSpace = ShowNames ? 9 : 0;
                var centre = r.Position + new Vector2(r.Size.X / 2, (r.Size.Y - labelSpace) / 2);
                var c = Entries[i].ContentCenter;
                a.Position = new Vector2(Mathf.Round(centre.X - c.X), Mathf.Round(centre.Y - c.Y));
            }
        }

        public Rect2 CellRect(int i)
        {
            int cols = Math.Max(1, Columns);
            return new Rect2((i % cols) * CellSize.X, TopPad + (i / cols) * CellSize.Y, CellSize.X, CellSize.Y);
        }

        /// <summary>Cell rectangle in control coordinates (for clicks and labels).</summary>
        public Rect2 CellScreenRect(int i)
        {
            var r = CellRect(i);
            var tl = Display.Position + (r.Position - VisibleWorldRect.Position) * EffectiveZoom;
            return new Rect2(tl, r.Size * EffectiveZoom);
        }

        /// <summary>Display index under a control position, or -1.</summary>
        public int CellAt(Vector2 local)
        {
            if (Entries.Count == 0 || local.X < 0 || local.Y < 0 || local.X >= _overlay.Size.X) return -1;
            var w = VisibleWorldRect.Position + (local - Display.Position) / EffectiveZoom;
            if (w.X < 0 || w.Y < TopPad || w.X >= Columns * CellSize.X) return -1;
            int i = (int)((w.Y - TopPad) / CellSize.Y) * Columns + (int)(w.X / CellSize.X);
            return i >= 0 && i < Entries.Count ? i : -1;
        }

        /// <summary>Scrolls so that a cell is completely visible.</summary>
        public void EnsureVisible(int i)
        {
            if (i < 0 || i >= Entries.Count) return;
            var r = CellRect(i);
            float h = VisibleWorldRect.Size.Y;
            if (r.Position.Y < ScrollY + (i < Columns ? TopPad : 0)) SetScroll(i < Columns ? 0 : r.Position.Y);
            else if (r.End.Y > ScrollY + h) SetScroll(r.End.Y - h);
        }

        public int VisibleCount()
        {
            int n = 0;
            foreach (var e in Entries) if (e.Actor != null && IsInstanceValid(e.Actor) && !e.Actor.IsCulled) n++;
            return n;
        }

        // ------------------------------------------------------------------ input

        public override void _GuiInput(InputEvent e)
        {
            switch (e)
            {
                case InputEventMouseButton mb when mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown):
                {
                    int dir = mb.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
                    if (mb.CtrlPressed) SetZoom(Math.Clamp(EffectiveZoom + dir, 1, 6));
                    else SetScroll(ScrollY - dir * CellSize.Y * 0.5f);
                    AcceptEvent();
                    break;
                }
                case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Right || mb.ButtonIndex == MouseButton.Middle:
                    _dragging = mb.Pressed;
                    AcceptEvent();
                    break;
                case InputEventMouseButton mb when mb.Pressed && mb.ButtonIndex == MouseButton.Left:
                {
                    GetViewport().GuiReleaseFocus(); // e.g. the seed field: keys belong to the grid now
                    int i = CellAt(mb.Position);
                    if (i >= 0)
                    {
                        if (mb.DoubleClick) CellActivated?.Invoke(i);
                        else CellClicked?.Invoke(i);
                    }
                    AcceptEvent();
                    break;
                }
                case InputEventMouseMotion mm:
                {
                    if (_dragging) SetScroll(ScrollY - mm.Relative.Y / EffectiveZoom);
                    int h = CellAt(mm.Position);
                    if (h != Hover)
                    {
                        Hover = h;
                        HoverChanged?.Invoke(h);
                    }
                    break;
                }
            }
        }

        public override void _Notification(int what)
        {
            if (what == NotificationMouseExit && Hover != -1)
            {
                Hover = -1;
                HoverChanged?.Invoke(-1);
            }
        }

        public override void _Process(double delta)
        {
            if (Viewport == null) return;
            UpdateCamera();
            _overlay.QueueRedraw();
        }

        /// <summary>Names, favourite stars, hover and selection frames in screen resolution.</summary>
        private partial class OverlayDrawer : Control
        {
            public OverviewGrid Grid = null!;

            public override void _Draw()
            {
                var g = Grid;
                if (g.Entries.Count == 0) return;
                var font = GetThemeDefaultFont();
                int fs = g.EffectiveZoom >= 3 ? 13 : 11;
                var view = new Rect2(Vector2.Zero, Size);
                for (int i = 0; i < g.Entries.Count; i++)
                {
                    var r = g.CellScreenRect(i);
                    if (!view.Intersects(r)) continue;
                    var e = g.Entries[i];
                    if (g.ShowNames && r.Size.X >= 60)
                    {
                        string name = string.IsNullOrEmpty(e.Genome.Name) ? "(unnamed)" : e.Genome.Name;
                        string text = r.Size.X >= 150 ? $"{name} · {e.ArchetypeLabel}" : name;
                        var pos = new Vector2(r.Position.X + 4, r.End.Y - 5);
                        DrawRect(new Rect2(r.Position.X, r.End.Y - fs - 6, r.Size.X - 1, fs + 5), new Color(0, 0, 0, 0.42f));
                        DrawString(font, pos, text, HorizontalAlignment.Left, r.Size.X - 8, fs, new Color(0.93f, 0.92f, 0.88f));
                    }
                    if (e.Favorite)
                        DrawString(font, new Vector2(r.End.X - fs - 4, r.Position.Y + fs + 3), "★", HorizontalAlignment.Left, -1, fs + 3, UiKit.Accent);
                    if (i == g.Selected)
                    {
                        DrawRect(r.Grow(-1), UiKit.Accent, false, 2);
                        DrawRect(r.Grow(-3), new Color(UiKit.Accent, 0.35f), false, 1);
                    }
                    else if (i == g.Hover) DrawRect(r.Grow(-1), new Color(1, 1, 1, 0.35f), false, 1);
                }
            }
        }
    }
}
