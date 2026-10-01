// Procedural Pixel Creature Workshop - UI theme and small control helpers (all UI is built in code).

using System;
using Godot;

namespace PixelCreatures.Workshop
{
    public static class UiKit
    {
        public static readonly Color Bg = Color.Color8(22, 24, 30);
        public static readonly Color Panel = Color.Color8(32, 35, 43);
        public static readonly Color PanelLight = Color.Color8(42, 46, 56);
        public static readonly Color Line = Color.Color8(58, 63, 76);
        public static readonly Color Text = Color.Color8(224, 222, 214);
        public static readonly Color TextDim = Color.Color8(150, 152, 160);
        public static readonly Color Accent = Color.Color8(226, 172, 76);
        public static readonly Color AccentDim = Color.Color8(150, 112, 52);
        public static readonly Color Good = Color.Color8(120, 196, 120);
        public static readonly Color Bad = Color.Color8(226, 104, 96);
        public static readonly Color Info = Color.Color8(120, 176, 226);

        public static Theme CreateTheme()
        {
            var t = new Theme();
            t.DefaultFontSize = 14;
            StyleBoxFlat Box(Color c, int border = 0, Color? bc = null, int radius = 3, int margin = 6)
            {
                var s = new StyleBoxFlat { BgColor = c, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius };
                s.ContentMarginLeft = margin; s.ContentMarginRight = margin; s.ContentMarginTop = margin * 0.6f; s.ContentMarginBottom = margin * 0.6f;
                if (border > 0) { s.BorderWidthBottom = border; s.BorderWidthTop = border; s.BorderWidthLeft = border; s.BorderWidthRight = border; s.BorderColor = bc ?? Line; }
                return s;
            }
            t.SetStylebox("panel", "PanelContainer", Box(Panel, 1, Line, 4, 8));
            t.SetStylebox("panel", "Panel", Box(Panel, 0));
            t.SetStylebox("normal", "Button", Box(PanelLight, 1, Line));
            t.SetStylebox("hover", "Button", Box(Color.Color8(54, 59, 72), 1, AccentDim));
            t.SetStylebox("pressed", "Button", Box(AccentDim, 1, Accent));
            t.SetStylebox("disabled", "Button", Box(Color.Color8(34, 36, 44), 1, Color.Color8(44, 47, 56)));
            t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), 1, Accent));
            t.SetColor("font_color", "Button", Text);
            t.SetColor("font_hover_color", "Button", Color.Color8(255, 240, 210));
            t.SetColor("font_pressed_color", "Button", Color.Color8(255, 246, 228));
            t.SetColor("font_disabled_color", "Button", Color.Color8(100, 102, 110));
            t.SetColor("font_color", "Label", Text);
            t.SetStylebox("normal", "LineEdit", Box(Color.Color8(18, 20, 25), 1, Line));
            t.SetStylebox("focus", "LineEdit", Box(Color.Color8(18, 20, 25), 1, Accent));
            t.SetColor("font_color", "LineEdit", Text);
            t.SetStylebox("normal", "OptionButton", Box(PanelLight, 1, Line));
            t.SetStylebox("hover", "OptionButton", Box(Color.Color8(54, 59, 72), 1, AccentDim));
            t.SetStylebox("pressed", "OptionButton", Box(AccentDim, 1, Accent));
            t.SetStylebox("focus", "OptionButton", Box(new Color(0, 0, 0, 0), 1, Accent));
            t.SetStylebox("panel", "PopupMenu", Box(Color.Color8(28, 30, 38), 1, Line));
            t.SetStylebox("hover", "PopupMenu", Box(AccentDim, 0));
            t.SetStylebox("slider", "HSlider", Box(Color.Color8(18, 20, 25), 0, null, 2, 2));
            t.SetStylebox("grabber_area", "HSlider", Box(AccentDim, 0, null, 2, 2));
            t.SetStylebox("grabber_area_highlight", "HSlider", Box(Accent, 0, null, 2, 2));
            t.SetStylebox("panel", "TabContainer", Box(Panel, 1, Line, 4, 8));
            t.SetStylebox("tab_selected", "TabContainer", Box(Color.Color8(52, 56, 68), 1, Accent, 3, 10));
            t.SetStylebox("tab_unselected", "TabContainer", Box(Color.Color8(30, 33, 40), 1, Line, 3, 10));
            t.SetStylebox("tab_hovered", "TabContainer", Box(Color.Color8(44, 48, 58), 1, AccentDim, 3, 10));
            t.SetColor("font_selected_color", "TabContainer", Accent);
            t.SetColor("font_unselected_color", "TabContainer", TextDim);
            t.SetStylebox("panel", "ScrollContainer", Box(new Color(0, 0, 0, 0), 0));
            t.SetStylebox("panel", "TooltipPanel", Box(Color.Color8(14, 15, 20), 1, AccentDim));
            t.SetColor("font_color", "TooltipLabel", Text);
            t.SetColor("font_color", "CheckBox", Text);
            t.SetColor("font_color", "CheckButton", Text);
            t.SetStylebox("normal", "TextEdit", Box(Color.Color8(18, 20, 25), 1, Line));
            t.SetColor("font_color", "TextEdit", Text);
            t.SetStylebox("panel", "ItemList", Box(Color.Color8(18, 20, 25), 1, Line));
            return t;
        }

        public static Label Label(string text, int size = 14, Color? color = null)
        {
            var l = new Label { Text = text };
            if (size != 14) l.AddThemeFontSizeOverride("font_size", size);
            if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
            return l;
        }

        public static Label Header(string text) => Label(text, 16, Accent);

        public static Button Button(string text, Action onPressed, string tooltip = "")
        {
            var b = new Button { Text = text, TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None };
            b.Pressed += onPressed;
            return b;
        }

        public static Button Toggle(string text, bool initial, Action<bool> onToggled, string tooltip = "")
        {
            var b = new Button { Text = text, ToggleMode = true, ButtonPressed = initial, TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None };
            b.Toggled += v => onToggled(v);
            return b;
        }

        public static HBoxContainer Row(int sep = 6, params Control[] children)
        {
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", sep);
            foreach (var c in children) h.AddChild(c);
            return h;
        }

        public static VBoxContainer Column(int sep = 6, params Control[] children)
        {
            var v = new VBoxContainer();
            v.AddThemeConstantOverride("separation", sep);
            foreach (var c in children) v.AddChild(c);
            return v;
        }

        public static Control Spacer(float w = 0, float h = 0, bool expand = false)
        {
            var c = new Control { CustomMinimumSize = new Vector2(w, h) };
            if (expand) c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            return c;
        }

        public static PanelContainer Card(Control content)
        {
            var p = new PanelContainer();
            p.AddChild(content);
            return p;
        }

        public static HSeparator Separator() => new HSeparator();
    }
}
