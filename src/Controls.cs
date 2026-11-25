using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ReInstala
{
    internal class RoundedButton : Control
    {
        public enum Kind { Primary, Secondary, Ghost, Danger }

        private bool _hover;
        private bool _pressed;
        private Kind _kind = Kind.Primary;
        private int _corner = 10;

        public RoundedButton(string text, Kind kind)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Text = text;
            _kind = kind;
            Font = Theme.Bold(9.75f);
            Cursor = Cursors.Hand;
            TabStop = true;
            Height = Theme.Px(42);
        }

        public Kind Style
        {
            get { return _kind; }
            set { _kind = value; Invalidate(); }
        }

        public int Corner
        {
            get { return _corner; }
            set { _corner = value; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _pressed = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            _hover = false;
            _pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                _pressed = true;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (_pressed && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                _pressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill;
            Color border;
            Color text;

            if (_kind == Kind.Primary)
            {
                fill = _pressed ? Theme.AccentDark : (_hover ? Theme.Mix(Theme.Accent, Color.White, 0.1f) : Theme.Accent);
                border = Theme.AccentDark;
                text = Color.White;
            }
            else if (_kind == Kind.Danger)
            {
                fill = _pressed || _hover ? Theme.DangerSoft : Theme.Surface;
                border = Theme.Danger;
                text = Theme.Danger;
            }
            else if (_kind == Kind.Ghost)
            {
                fill = _pressed ? Theme.AccentSoft : (_hover ? Theme.AccentSoft : Color.Transparent);
                border = Color.Transparent;
                text = Theme.Accent;
            }
            else
            {
                fill = _pressed ? Theme.Background : (_hover ? Theme.RowAlt : Theme.Surface);
                border = Theme.Border;
                text = Theme.Text;
            }

            if (!Enabled)
            {
                fill = _kind == Kind.Primary ? Theme.AccentDim : Theme.Background;
                border = _kind == Kind.Primary ? Theme.AccentDim : Theme.BorderSoft;
                text = _kind == Kind.Primary ? Color.White : Theme.Muted;
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            if (fill != Color.Transparent)
            {
                using (GraphicsPath path = Theme.RoundedRect(rect, _corner * Theme.Scale))
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
            }

            if (border != Color.Transparent)
            {
                using (GraphicsPath path = Theme.RoundedRect(rect, _corner * Theme.Scale))
                using (Pen pen = new Pen(border, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            Theme.DrawText(g, Text, Font, text, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                Rectangle inner = new Rectangle(Theme.Px(4), Theme.Px(4), Width - Theme.Px(8) - 1, Height - Theme.Px(8) - 1);
                Color ring = _kind == Kind.Primary ? Color.FromArgb(150, Color.White) : Color.FromArgb(120, Theme.Accent);
                using (GraphicsPath path = Theme.RoundedRect(inner, Math.Max(2f, _corner * Theme.Scale - 3f)))
                using (Pen pen = new Pen(ring, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    internal class StatusChip : Control
    {
        private Color _dot = Theme.Muted;
        private Color _bg = Theme.Background;
        private Color _fg = Theme.Muted;

        public StatusChip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Bold(8.5f);
            Height = Theme.Px(28);
            Width = Theme.Px(140);
        }

        public void Set(string text, Color dot, Color bg, Color fg)
        {
            Text = text;
            _dot = dot;
            _bg = bg;
            _fg = fg;
            Width = TextRenderer.MeasureText(text, Font).Width + Theme.Px(42);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, rect, Height / 2f, _bg);

            int dotSize = Theme.Px(7);
            int cy = Height / 2;
            using (SolidBrush brush = new SolidBrush(_dot))
            {
                g.FillEllipse(brush, Theme.Px(13), cy - dotSize / 2, dotSize, dotSize);
            }
            Theme.DrawText(g, Text, Font, _fg,
                new Rectangle(Theme.Px(25), 0, Width - Theme.Px(33), Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class ThinProgress : Control
    {
        private int _value;
        private bool _indeterminate;
        private readonly Timer _timer;
        private int _offset;

        public ThinProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.Px(6);
            BackColor = Theme.Background;
            _timer = new Timer();
            _timer.Interval = 25;
            _timer.Tick += delegate
            {
                _offset += 6;
                if (_offset > Width + Theme.Px(90)) _offset = -Theme.Px(90);
                Invalidate();
            };
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(0, Math.Min(100, value));
                if (v != _value)
                {
                    _value = v;
                    Invalidate();
                }
            }
        }

        public bool Indeterminate
        {
            get { return _indeterminate; }
            set
            {
                if (_indeterminate == value) return;
                _indeterminate = value;
                _offset = -Theme.Px(90);
                if (value)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, Height / 2 - Theme.Px(2), Width, Theme.Px(5));
            Theme.FillRounded(g, track, track.Height / 2f, Theme.ProgressTrack);

            if (_indeterminate)
            {
                int barWidth = Theme.Px(110);
                Rectangle fill = new Rectangle(_offset, track.Y, barWidth, track.Height);
                using (GraphicsPath clip = Theme.RoundedRect(track, track.Height / 2f))
                {
                    GraphicsState state = g.Save();
                    g.SetClip(clip);
                    Theme.FillRounded(g, fill, fill.Height / 2f, Theme.Accent);
                    g.Restore(state);
                }
            }
            else if (_value > 0)
            {
                int w = (int)Math.Round(track.Width * (_value / 100f));
                Rectangle fill = new Rectangle(0, track.Y, Math.Max(Theme.Px(5), w), track.Height);
                Theme.FillRounded(g, fill, fill.Height / 2f, Theme.Accent);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal class SearchBox : Control
    {
        private readonly TextBox _box;
        private bool _focused;

        public event EventHandler QueryChanged;

        public SearchBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Surface;
            Height = Theme.Px(38);

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.Font = Theme.Font(9.5f);
            _box.BackColor = Theme.Surface;
            _box.ForeColor = Theme.Text;
            _box.TextChanged += delegate
            {
                Invalidate();
                if (QueryChanged != null) QueryChanged(this, EventArgs.Empty);
            };
            _box.GotFocus += delegate { _focused = true; Invalidate(); };
            _box.LostFocus += delegate { _focused = false; Invalidate(); };
            Controls.Add(_box);
        }

        public string Placeholder = "Buscar programa...";

        public string Query
        {
            get { return _box.Text; }
            set { _box.Text = value; }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            _box.BackColor = Theme.Surface;
            _box.ForeColor = Theme.Text;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_box != null)
            {
                _box.SetBounds(Theme.Px(36), (Height - _box.Height) / 2, Math.Max(Theme.Px(10), Width - Theme.Px(48)), _box.Height);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                SendMessage(_box.Handle, 0x1501, new IntPtr(1), Placeholder);
            }
            catch (Exception)
            {
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, rect, Theme.Px(10), Theme.Surface);
            Theme.StrokeRounded(g, rect, Theme.Px(10), _focused ? Theme.Accent : Theme.Border, 1f);

            using (Pen pen = new Pen(_focused ? Theme.Accent : Theme.Muted, 1.6f))
            {
                int cx = Theme.Px(19);
                int cy = Height / 2;
                int r = Theme.Px(5);
                g.DrawEllipse(pen, cx - r, cy - r - Theme.Px(1), r * 2, r * 2);
                g.DrawLine(pen, cx + Theme.Px(4), cy + Theme.Px(3), cx + Theme.Px(8), cy + Theme.Px(7));
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }

    internal class SegmentedControl : Control
    {
        private readonly string[] _items;
        private int _selected;
        private int _hoverIndex = -1;

        public event EventHandler SelectedIndexChanged;

        public SegmentedControl(string[] items)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            _items = items;
            Font = Theme.Bold(9.5f);
            Height = Theme.Px(38);
            Cursor = Cursors.Hand;
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = Math.Max(0, Math.Min(_items.Length - 1, value));
                if (v != _selected)
                {
                    _selected = v;
                    Invalidate();
                    if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitIndex(e.X);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int idx = HitIndex(e.X);
                if (idx >= 0) SelectedIndex = idx;
            }
            base.OnMouseDown(e);
        }

        private int HitIndex(int x)
        {
            int w = Width / _items.Length;
            int idx = x / Math.Max(1, w);
            if (idx < 0 || idx >= _items.Length) return -1;
            return idx;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, rect, Theme.Px(11), Theme.SegmentedBack);

            int w = Width / _items.Length;
            Rectangle thumb = new Rectangle(_selected * w + Theme.Px(3), Theme.Px(3), w - Theme.Px(6), Height - Theme.Px(6));
            using (GraphicsPath path = Theme.RoundedRect(thumb, Theme.Px(8)))
            {
                using (SolidBrush shadow = new SolidBrush(Theme.ThumbShadow))
                {
                    GraphicsPath shadowPath = (GraphicsPath)path.Clone();
                    using (Matrix m = new Matrix())
                    {
                        m.Translate(0, 1.4f);
                        shadowPath.Transform(m);
                    }
                    g.FillPath(shadow, shadowPath);
                    shadowPath.Dispose();
                }
                using (SolidBrush thumbBrush = new SolidBrush(Theme.Surface))
                {
                    g.FillPath(thumbBrush, path);
                }
                using (Pen pen = new Pen(Theme.ThumbBorder, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            using (Font selectedFont = Theme.Bold(9.5f))
            using (Font regularFont = Theme.Font(9.5f))
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    Rectangle cell = new Rectangle(i * w, 0, w, Height);
                    Color color = i == _selected ? Theme.Text : (i == _hoverIndex ? Theme.Text : Theme.Muted);
                    Theme.DrawText(g, _items[i], i == _selected ? selectedFont : regularFont, color, cell,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    internal class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, rect, Theme.Px(14), Theme.Surface);
            Theme.StrokeRounded(g, rect, Theme.Px(14), Theme.Border, 1f);
            base.OnPaint(e);
        }
    }

    internal enum StateIcon { Search, Download, Alert, Check }

    internal class StateView : Panel
    {
        public StateIcon Icon = StateIcon.Search;
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        private readonly List<RoundedButton> _actions = new List<RoundedButton>();
        private Size _subSize = Size.Empty;
        private int _blockTop;

        public StateView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
        }

        public RoundedButton AddAction(string text, RoundedButton.Kind kind, EventHandler onClick)
        {
            RoundedButton button = new RoundedButton(text, kind);
            button.Size = new Size(Theme.Px(172), Theme.Px(42));
            button.Click += onClick;
            _actions.Add(button);
            Controls.Add(button);
            PerformLayout();
            Invalidate();
            return button;
        }

        public void ClearActions()
        {
            foreach (RoundedButton b in _actions)
            {
                Controls.Remove(b);
                b.Dispose();
            }
            _actions.Clear();
        }

        private void MeasureBlock()
        {
            int maxTextWidth = Math.Min(Theme.Px(560), Math.Max(Theme.Px(200), Width - Theme.Px(80)));
            using (Font subFont = Theme.Font(9.5f))
            {
                _subSize = TextRenderer.MeasureText(Subtitle, subFont, new Size(maxTextWidth, Theme.Px(400)),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }

            int actionsHeight = _actions.Count > 0 ? Theme.Px(42) : 0;
            int blockHeight = Theme.Px(64) + Theme.Px(16) + Theme.Px(30) + Theme.Px(4) + _subSize.Height +
                              (_actions.Count > 0 ? Theme.Px(22) + actionsHeight : 0);
            _blockTop = Math.Max(Theme.Px(16), (Height - blockHeight) / 2 - Theme.Px(8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            MeasureBlock();

            using (Font titleFont = Theme.Bold(14f))
            using (Font subFont = Theme.Font(9.5f))
            {
                int circle = Theme.Px(64);
                Rectangle iconRect = new Rectangle((Width - circle) / 2, _blockTop, circle, circle);
                using (SolidBrush bg = new SolidBrush(Theme.AccentSoft))
                {
                    g.FillEllipse(bg, iconRect);
                }
                DrawGlyph(g, iconRect);

                Rectangle titleRect = new Rectangle(Theme.Px(20), iconRect.Bottom + Theme.Px(16), Width - Theme.Px(40), Theme.Px(30));
                Theme.DrawText(g, Title, titleFont, Theme.Text, titleRect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                Rectangle subRect = new Rectangle((Width - _subSize.Width) / 2, titleRect.Bottom + Theme.Px(4), _subSize.Width, _subSize.Height + Theme.Px(4));
                Theme.DrawText(g, Subtitle, subFont, Theme.Muted, subRect,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            MeasureBlock();
            LayoutActions();
        }

        private void LayoutActions()
        {
            if (_actions.Count == 0) return;
            int gap = Theme.Px(10);
            int totalWidth = 0;
            for (int i = 0; i < _actions.Count; i++)
            {
                totalWidth += _actions[i].Width;
                if (i > 0) totalWidth += gap;
            }
            int x = (Width - totalWidth) / 2;
            int y = _blockTop + Theme.Px(64) + Theme.Px(16) + Theme.Px(30) + Theme.Px(4) + _subSize.Height + Theme.Px(22);
            if (y + Theme.Px(46) > Height) y = Math.Max(Theme.Px(10), Height - Theme.Px(56));
            for (int i = 0; i < _actions.Count; i++)
            {
                _actions[i].SetBounds(x, y, _actions[i].Width, _actions[i].Height);
                x += _actions[i].Width + gap;
            }
        }

        private void DrawGlyph(Graphics g, Rectangle r)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            int s = Theme.Px(24);
            Rectangle box = new Rectangle(cx - s / 2, cy - s / 2, s, s);

            using (Pen pen = new Pen(Theme.Accent, Theme.Scale > 1.4f ? 2.4f : 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (Icon == StateIcon.Download)
                {
                    g.DrawLine(pen, cx, box.Top + Theme.Px(2), cx, box.Bottom - Theme.Px(6));
                    using (GraphicsPath head = new GraphicsPath())
                    {
                        head.AddLine(cx - Theme.Px(5), box.Bottom - Theme.Px(12), cx, box.Bottom - Theme.Px(6));
                        head.AddLine(cx + Theme.Px(5), box.Bottom - Theme.Px(12), cx, box.Bottom - Theme.Px(6));
                        g.DrawPath(pen, head);
                    }
                    g.DrawLine(pen, box.Left + Theme.Px(2), box.Bottom - Theme.Px(1), box.Right - Theme.Px(2), box.Bottom - Theme.Px(1));
                }
                else if (Icon == StateIcon.Alert)
                {
                    g.DrawEllipse(pen, box);
                    Theme.DrawText(g, "!", Theme.Bold(13f), Theme.Accent, box, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else if (Icon == StateIcon.Check)
                {
                    g.DrawEllipse(pen, box);
                    using (GraphicsPath check = new GraphicsPath())
                    {
                        check.AddLine(cx - Theme.Px(6), cy, cx - Theme.Px(1), cy + Theme.Px(5));
                        check.AddLine(cx - Theme.Px(1), cy + Theme.Px(5), cx + Theme.Px(7), cy - Theme.Px(5));
                        g.DrawPath(pen, check);
                    }
                }
                else
                {
                    int rr = Theme.Px(8);
                    g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2);
                    g.DrawLine(pen, cx + Theme.Px(6), cy + Theme.Px(6), cx + Theme.Px(11), cy + Theme.Px(11));
                }
            }
        }
    }
}
