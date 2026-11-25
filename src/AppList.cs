using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ReInstala
{
    internal class ListRow
    {
        public string Key = string.Empty;
        public string Name = string.Empty;
        public string Version = string.Empty;
        public string Detail = string.Empty;
        public string ChipText = string.Empty;
        public Color ChipFore = Theme.Muted;
        public Color ChipBack = Theme.Background;
        public bool Checkable = true;
        public bool Checked;
        public bool Muted;
        public object Tag;
    }

    internal enum ListMode { Backup, Restore }

    internal class AppList : ListView
    {
        private readonly List<ListRow> _rows = new List<ListRow>();
        private int _hover = -1;
        private ListMode _mode = ListMode.Backup;

        private readonly Font _nameFont;
        private readonly Font _versionFont;
        private readonly Font _monoFont;
        private readonly Font _chipFont;
        private readonly Font _headerFont;

        public event EventHandler SelectionChanged;

        public bool Interactive = true;

        public AppList()
        {
            View = View.Details;
            OwnerDraw = true;
            FullRowSelect = true;
            MultiSelect = false;
            HeaderStyle = ColumnHeaderStyle.Clickable;
            BorderStyle = BorderStyle.None;
            Font = Theme.Font(9.5f);
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            HideSelection = true;
            ShowItemToolTips = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            _nameFont = Theme.Font(9.5f);
            _versionFont = Theme.Font(8.75f);
            _monoFont = Theme.Mono(8.25f);
            _chipFont = Theme.Bold(8.25f);
            _headerFont = Theme.Bold(8.25f);

            SmallImageList = new ImageList();
            SmallImageList.ImageSize = new Size(1, Theme.Px(44));

            ApplyMode(ListMode.Backup);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            ForeColor = Theme.Text;
            ApplyNativeTheme();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyNativeTheme();
        }

        private void ApplyNativeTheme()
        {
            if (!IsHandleCreated) return;
            NativeTheme.SetDark(Theme.Dark);
            ThemeNativeChildren();
        }

        private void ThemeNativeChildren()
        {
            if (!IsHandleCreated) return;
            try
            {
                bool dark = Theme.Dark;
                NativeTheme.ApplyToWindow(Handle, dark);
                IntPtr child = GetWindow(Handle, 5); // GW_CHILD
                while (child != IntPtr.Zero)
                {
                    NativeTheme.ApplyToWindow(child, dark);
                    child = GetWindow(child, 2); // GW_HWNDNEXT
                }

                IntPtr header = SendMessage(Handle, 0x101F, IntPtr.Zero, IntPtr.Zero); // LVM_GETHEADER
                if (header != IntPtr.Zero) NativeTheme.ApplyToWindow(header, dark);
            }
            catch (Exception)
            {
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_nameFont != null) _nameFont.Dispose();
                if (_versionFont != null) _versionFont.Dispose();
                if (_monoFont != null) _monoFont.Dispose();
                if (_chipFont != null) _chipFont.Dispose();
                if (_headerFont != null) _headerFont.Dispose();
            }
            base.Dispose(disposing);
        }

        public ListMode Mode
        {
            get { return _mode; }
        }

        public IList<ListRow> Rows
        {
            get { return _rows; }
        }

        public int CheckableCount
        {
            get
            {
                int count = 0;
                foreach (ListRow row in _rows) if (row.Checkable) count++;
                return count;
            }
        }

        public int CheckedCount
        {
            get
            {
                int count = 0;
                foreach (ListRow row in _rows) if (row.Checkable && row.Checked) count++;
                return count;
            }
        }

        public int RowCount
        {
            get { return _rows.Count; }
        }

        public int TopIndex
        {
            get
            {
                if (!IsHandleCreated || Items.Count == 0) return 0;
                return SendMessage(Handle, 0x1027, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LVM_GETTOPINDEX
            }
        }

        public int VisibleRowCount
        {
            get
            {
                if (!IsHandleCreated || Items.Count == 0) return 1;
                int perPage = SendMessage(Handle, 0x1028, IntPtr.Zero, IntPtr.Zero).ToInt32(); // LVM_GETCOUNTPERPAGE
                return Math.Max(1, perPage);
            }
        }

        private int RowHeight
        {
            get
            {
                if (Items.Count == 0) return 0;
                int height = Items[0].Bounds.Height;
                if (height <= 0) height = Theme.Px(44);
                return height;
            }
        }

        public void ScrollToRow(int row)
        {
            if (!IsHandleCreated || Items.Count == 0) return;

            int visible = VisibleRowCount;
            int maxTop = Math.Max(0, Items.Count - visible);
            if (row < 0) row = 0;
            if (row > maxTop) row = maxTop;

            int rowHeight = RowHeight;
            if (rowHeight <= 0) return;

            int current = TopIndex;
            if (row == current) return;

            // LVM_SCROLL usa pixels; valores positivos descem a lista.
            int delta = (row - current) * rowHeight;
            SendMessage(Handle, 0x1014, IntPtr.Zero, (IntPtr)delta); // LVM_SCROLL
        }

        public void ScrollByRowCount(int rows)
        {
            if (!IsHandleCreated || Items.Count == 0 || rows == 0) return;
            ScrollToRow(TopIndex + rows);
        }

        private int _wheelDelta;

        public void ScrollByWheel(int delta)
        {
            if (!IsHandleCreated || Items.Count == 0 || delta == 0) return;

            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines == -1) return; // rolagem por roda desativada no Windows

            if (lines == 0)
            {
                int page = VisibleRowCount;
                ScrollByRowCount(delta > 0 ? -page : page);
                return;
            }

            _wheelDelta += delta;
            int notches = _wheelDelta / 120;
            if (notches == 0) return;
            _wheelDelta -= notches * 120;
            ScrollByRowCount(-notches * lines);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Items.Count > 0)
            {
                ScrollByWheel(e.Delta);
                return;
            }
            base.OnMouseWheel(e);
        }

        public void ApplyMode(ListMode mode)
        {
            if (_mode == mode && Columns.Count > 0) return;

            _mode = mode;
            BeginUpdate();
            Columns.Clear();
            if (mode == ListMode.Backup)
            {
                Columns.Add("PROGRAMA", 400);
                Columns.Add("VERSÃO", Theme.Px(100));
                Columns.Add("ORIGEM", Theme.Px(112));
                Columns.Add("ID DO PACOTE", Theme.Px(260));
            }
            else
            {
                Columns.Add("PROGRAMA", 400);
                Columns.Add("VERSÃO SALVA", Theme.Px(112));
                Columns.Add("SITUAÇÃO", Theme.Px(132));
            }
            EndUpdate();
            RelayoutColumns();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RelayoutColumns();
            ThemeNativeChildren();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            // A barra de rolagem vertical aparece depois das linhas: o cliente encolhe
            // e as colunas precisam ser recalculadas, senão sobra uma rolagem horizontal.
            RelayoutColumns();
        }

        private void RelayoutColumns()
        {
            if (Columns.Count < 3) return;
            int fixedWidth;
            if (_mode == ListMode.Backup)
                fixedWidth = Columns[1].Width + Columns[2].Width + Columns[3].Width;
            else
                fixedWidth = Columns[1].Width + Columns[2].Width;

            // ClientSize já desconta a barra de rolagem: as colunas preenchem tudo
            // e não sobra faixa branca à direita do cabeçalho.
            int nameWidth = Math.Max(Theme.Px(190), ClientSize.Width - fixedWidth);
            if (Columns[0].Width != nameWidth) Columns[0].Width = nameWidth;
        }

        public void SetRows(List<ListRow> rows)
        {
            int top = TopIndex;
            bool restore = top > 0 && rows.Count == _rows.Count;
            BeginUpdate();
            try
            {
                Items.Clear();
                _rows.Clear();
                _hover = -1;
                int columns = Columns.Count;
                foreach (ListRow row in rows)
                {
                    string[] cells = new string[columns];
                    for (int i = 0; i < columns; i++) cells[i] = string.Empty;
                    if (columns > 0) cells[0] = row.Name;

                    ListViewItem item = new ListViewItem(cells);
                    item.Tag = row;
                    item.UseItemStyleForSubItems = false;
                    if (!row.Checkable) item.ToolTipText = "Este programa não pode ser reinstalado automaticamente pelo Winget.";
                    Items.Add(item);
                    _rows.Add(row);
                }
            }
            finally
            {
                EndUpdate();
            }
            RelayoutColumns();
            if (restore)
            {
                // Espera o ListView recalcular a área visível antes de voltar à posição.
                int target = top;
                BeginInvoke((MethodInvoker)delegate { ScrollToRow(target); });
            }
            Invalidate();
        }

        public void SetAllChecked(bool value)
        {
            if (!Interactive) return;
            foreach (ListRow row in _rows)
            {
                if (row.Checkable) row.Checked = value;
            }
            Invalidate();
            RaiseSelectionChanged();
        }

        public void ToggleRow(ListRow row)
        {
            if (!Interactive || row == null || !row.Checkable) return;
            row.Checked = !row.Checked;
            Invalidate();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        protected override void OnColumnClick(ColumnClickEventArgs e)
        {
            base.OnColumnClick(e);
            if (e.Column != 0) return;
            bool allChecked = true;
            bool hasCheckable = false;
            foreach (ListRow row in _rows)
            {
                if (!row.Checkable) continue;
                hasCheckable = true;
                if (!row.Checked) { allChecked = false; break; }
            }
            if (hasCheckable) SetAllChecked(!allChecked);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ListViewHitTestInfo hit = HitTest(e.Location);
            if (hit.Item == null) return;
            ListRow row = hit.Item.Tag as ListRow;
            if (row == null || !row.Checkable) return;

            Rectangle bounds = hit.Item.Bounds;
            if (e.X - bounds.Left <= Theme.Px(38))
            {
                ToggleRow(row);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            ListViewHitTestInfo hit = HitTest(e.Location);
            if (hit.Item == null) return;
            ListRow row = hit.Item.Tag as ListRow;
            if (row == null) return;
            ToggleRow(row);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ListViewHitTestInfo hit = HitTest(e.Location);
            int index = hit.Item != null ? hit.Item.Index : -1;
            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hover != -1)
            {
                _hover = -1;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && FocusedItem != null)
            {
                ToggleRow(FocusedItem.Tag as ListRow);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = false;
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(Theme.HeaderRow))
            {
                g.FillRectangle(bg, e.Bounds);
            }

            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            int left = e.Bounds.Left + Theme.Px(13);
            if (e.ColumnIndex == 0)
            {
                DrawSelectAllBox(g, e.Bounds, _headerFont);
                left = e.Bounds.Left + Theme.Px(40);
            }

            Rectangle textRect = new Rectangle(left, e.Bounds.Top, Math.Max(0, e.Bounds.Right - left - Theme.Px(8)), e.Bounds.Height);
            Theme.DrawText(g, e.Header.Text, _headerFont, Theme.Muted, textRect,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawSelectAllBox(Graphics g, Rectangle bounds, Font headerFont)
        {
            bool all = true;
            bool some = false;
            bool any = false;
            foreach (ListRow row in _rows)
            {
                if (!row.Checkable) continue;
                any = true;
                if (row.Checked) some = true;
                else all = false;
            }

            int size = Theme.Px(15);
            int x = bounds.Left + Theme.Px(13);
            int y = bounds.Top + (bounds.Height - size) / 2;
            Rectangle box = new Rectangle(x, y, size, size);

            if (any && all)
            {
                Theme.FillRounded(g, box, Theme.Px(4), Theme.Accent);
                DrawCheckMark(g, box, Color.White);
            }
            else if (some && !all)
            {
                Theme.FillRounded(g, box, Theme.Px(4), Theme.AccentSoft);
                using (Pen pen = new Pen(Theme.Accent, Math.Max(1.5f, Theme.Scale * 2f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, box.Left + Theme.Px(4), box.Top + box.Height / 2f, box.Right - Theme.Px(4), box.Top + box.Height / 2f);
                }
            }
            else
            {
                Theme.FillRounded(g, box, Theme.Px(4), Theme.Surface);
                Theme.StrokeRounded(g, box, Theme.Px(4), Theme.CheckBorder, 1f);
            }
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;
            ListRow row = e.Item.Tag as ListRow;
            if (row == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bounds = e.Bounds;
            bool hover = e.ItemIndex == _hover && Interactive && row.Checkable;

            Color background = Theme.Surface;
            if ((e.ItemIndex % 2) == 1) background = Theme.RowAlt;
            if (row.Checked) background = Theme.RowChecked;
            if (hover) background = Theme.Hover;

            using (SolidBrush brush = new SolidBrush(background))
            {
                g.FillRectangle(brush, bounds);
            }

            int left = bounds.Left + Theme.Px(13);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            if (_mode == ListMode.Backup)
            {
                if (e.ColumnIndex == 0)
                {
                    DrawCheckBox(g, bounds, row);
                    Rectangle nameRect = new Rectangle(left + Theme.Px(27), bounds.Top, Math.Max(0, bounds.Width - Theme.Px(40)), bounds.Height);
                    Theme.DrawText(g, row.Name, _nameFont, row.Muted ? Theme.Muted : Theme.Text, nameRect, flags);
                }
                else if (e.ColumnIndex == 1)
                {
                    Theme.DrawText(g, row.Version, _versionFont, Theme.Muted, new Rectangle(left, bounds.Top, bounds.Width - Theme.Px(20), bounds.Height), flags);
                }
                else if (e.ColumnIndex == 2)
                {
                    DrawChip(g, row.ChipText, row.ChipFore, row.ChipBack, bounds);
                }
                else if (e.ColumnIndex == 3)
                {
                    Theme.DrawText(g, row.Detail, _monoFont, row.Muted ? Theme.Muted : Theme.CodeText,
                        new Rectangle(left, bounds.Top, Math.Max(0, bounds.Width - Theme.Px(20)), bounds.Height), flags);
                }
            }
            else
            {
                if (e.ColumnIndex == 0)
                {
                    DrawCheckBox(g, bounds, row);
                    Rectangle nameRect = new Rectangle(left + Theme.Px(27), bounds.Top, Math.Max(0, bounds.Width - Theme.Px(40)), bounds.Height);
                    Theme.DrawText(g, row.Name, _nameFont, Theme.Text, nameRect, flags);
                }
                else if (e.ColumnIndex == 1)
                {
                    Theme.DrawText(g, row.Version, _versionFont, Theme.Muted, new Rectangle(left, bounds.Top, bounds.Width - Theme.Px(20), bounds.Height), flags);
                }
                else if (e.ColumnIndex == 2)
                {
                    DrawChip(g, row.ChipText, row.ChipFore, row.ChipBack, bounds);
                }
            }
        }

        private static void DrawCheckMark(Graphics g, Rectangle box, Color color)
        {
            using (Pen pen = new Pen(color, Math.Max(1.6f, Theme.Scale * 1.9f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float x = box.Left;
                float y = box.Top;
                float s = box.Width;
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(x + s * 0.25f, y + s * 0.52f, x + s * 0.43f, y + s * 0.70f);
                    path.AddLine(x + s * 0.43f, y + s * 0.70f, x + s * 0.76f, y + s * 0.31f);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawCheckBox(Graphics g, Rectangle bounds, ListRow row)
        {
            int size = Theme.Px(18);
            int x = bounds.Left + Theme.Px(14);
            int y = bounds.Top + (bounds.Height - size) / 2;
            Rectangle box = new Rectangle(x, y, size, size);

            if (!row.Checkable)
            {
                Theme.FillRounded(g, box, Theme.Px(5), Theme.Background);
                Theme.StrokeRounded(g, box, Theme.Px(5), Theme.BorderSoft, 1f);
                return;
            }

            if (row.Checked)
            {
                Theme.FillRounded(g, box, Theme.Px(5), Theme.Accent);
                DrawCheckMark(g, box, Color.White);
            }
            else
            {
                Theme.FillRounded(g, box, Theme.Px(5), Theme.Surface);
                Theme.StrokeRounded(g, box, Theme.Px(5), Theme.CheckBorder, 1f);
            }
        }

        private void DrawChip(Graphics g, string text, Color fore, Color back, Rectangle bounds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Size size = TextRenderer.MeasureText(text, _chipFont);
            int padding = Theme.Px(9);
            int height = Theme.Px(22);
            int width = Math.Min(size.Width + padding * 2, bounds.Width - Theme.Px(16));
            if (width <= padding) return;
            Rectangle chip = new Rectangle(bounds.Left + Theme.Px(12), bounds.Top + (bounds.Height - height) / 2, width, height);
            Theme.FillRounded(g, chip, Theme.Px(7), back);
            Theme.DrawText(g, text, _chipFont, fore, new Rectangle(chip.Left + padding / 2, chip.Top, chip.Width - padding, chip.Height),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
