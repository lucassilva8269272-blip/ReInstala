using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ReInstala
{
    /// <summary>
    /// Barra de rolagem fina desenhada pelo tema. Fica por cima da barra nativa
    /// do ListView (que o Windows não escurece) sem desativar a rolagem nativa.
    /// </summary>
    internal class ListScrollBar : Control
    {
        private readonly AppList _list;
        private readonly Timer _timer;
        private int _lastTop = -1;
        private int _lastTotal = -1;
        private int _lastVisible = -1;
        private bool _hover;
        private bool _dragging;
        private int _grabOffset;

        public ListScrollBar(AppList list)
        {
            _list = list;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;
            Width = SystemInformation.VerticalScrollBarWidth;
            Cursor = Cursors.Hand;

            _timer = new Timer();
            _timer.Interval = 80;
            _timer.Tick += delegate { Sync(); };
            _timer.Start();
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            Invalidate();
        }

        private int Total
        {
            get { return _list.RowCount; }
        }

        private int VisibleRows
        {
            get { return _list.VisibleRowCount; }
        }

        private bool Needed
        {
            get { return _list.Visible && Total > VisibleRows; }
        }

        private void Sync()
        {
            if (IsDisposed) return;

            bool needed = Needed;
            if (Visible != needed)
            {
                Visible = needed;
                if (!needed) return;
            }
            if (!needed) return;

            int top = _list.TopIndex;
            int total = Total;
            int visible = VisibleRows;
            if (top != _lastTop || total != _lastTotal || visible != _lastVisible)
            {
                _lastTop = top;
                _lastTotal = total;
                _lastVisible = visible;
                Invalidate();
            }
        }

        private Rectangle ThumbRect()
        {
            int total = Math.Max(1, Total);
            int visible = Math.Max(1, Math.Min(VisibleRows, total));
            if (total <= visible || Height <= 0) return Rectangle.Empty;

            int thumbHeight = (int)Math.Round(Height * (double)visible / total);
            thumbHeight = Math.Max(Theme.Px(26), Math.Min(Height, thumbHeight));

            int maxTop = total - visible;
            int top = Math.Max(0, Math.Min(maxTop, _list.TopIndex));
            int y = maxTop == 0 ? 0 : (int)Math.Round((Height - thumbHeight) * (double)top / maxTop);
            return new Rectangle(0, y, Width, thumbHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Surface);

            Rectangle thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            float barWidth = Math.Max(Theme.Px(4), Theme.Px(5));
            float x = (Width - barWidth) / 2f;

            Theme.FillRounded(g, new RectangleF(x, 0, barWidth, Height), barWidth / 2f, Theme.BorderSoft);
            Color fill = _hover || _dragging ? Theme.Accent : Theme.Muted;
            Theme.FillRounded(g, new RectangleF(x, thumb.Y, barWidth, thumb.Height), barWidth / 2f, fill);
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
            if (!_dragging) Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _list.ScrollByWheel(e.Delta);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Rectangle thumb = ThumbRect();
                if (!thumb.IsEmpty)
                {
                    _dragging = true;
                    if (thumb.Contains(e.Location))
                    {
                        _grabOffset = e.Y - thumb.Y;
                    }
                    else
                    {
                        _grabOffset = thumb.Height / 2;
                        DragTo(e.Y);
                    }
                    Capture = true;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                DragTo(e.Y);
            }
            else
            {
                bool over = ThumbRect().Contains(e.Location);
                if (over != _hover)
                {
                    _hover = over;
                    Invalidate();
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        private void DragTo(int mouseY)
        {
            Rectangle thumb = ThumbRect();
            if (thumb.IsEmpty) return;

            int maxTop = Math.Max(1, Total - VisibleRows);
            int span = Math.Max(1, Height - thumb.Height);
            int thumbTop = mouseY - _grabOffset;
            int top = (int)Math.Round((double)thumbTop * maxTop / span);
            if (top < 0) top = 0;
            if (top > maxTop) top = maxTop;
            _list.ScrollToRow(top);
            Invalidate();
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
}
