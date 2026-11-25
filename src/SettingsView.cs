using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ReInstala
{
    /// <summary>Interruptor no estilo do tema, usado nas configurações.</summary>
    internal class ToggleSwitch : Control
    {
        private bool _checked;

        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Theme.Px(46), Theme.Px(26));
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void SetCheckedSilent(bool value)
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Enabled) Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (Enabled) Checked = !Checked;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            RectangleF track = new RectangleF(0.5f, 0.5f, Width - 2f, Height - 2f);
            float radius = track.Height / 2f;
            Theme.FillRounded(g, track, radius, _checked ? Theme.Accent : Theme.ProgressTrack);
            if (!_checked) Theme.StrokeRounded(g, track, radius, Theme.CheckBorder, 1f);

            float pad = Math.Max(2f, Theme.Px(3));
            float knob = track.Height - pad * 2;
            float x = _checked ? track.Width - knob - pad : pad;
            RectangleF circle = new RectangleF(x, pad + 0.5f, knob, knob);
            using (SolidBrush brush = new SolidBrush(Color.White))
            {
                g.FillEllipse(brush, circle);
            }
            using (Pen pen = new Pen(Color.FromArgb(46, 0, 0, 0), 1f))
            {
                g.DrawEllipse(pen, circle);
            }
        }
    }

    /// <summary>
    /// Aba de configurações: permissões que o ReInstala deve resolver sozinho.
    /// Tudo já vem ligado e é salvo na hora em que o usuário muda.
    /// </summary>
    internal sealed class SettingsView : Panel
    {
        private sealed class Row
        {
            public Panel Box;
            public Label Title;
            public Label Desc;
            public ToggleSwitch Toggle;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly Label _heading;
        private readonly Label _subtitle;
        private readonly Label _note;
        private readonly Font _headingFont;
        private readonly Font _titleFont;
        private readonly Font _descFont;
        private bool _syncing;
        private bool _layingOut;

        public event EventHandler Changed;

        public SettingsView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Surface;

            _headingFont = Theme.Bold(13f);
            _titleFont = Theme.Bold(9.75f);
            _descFont = Theme.Font(8.75f);

            _heading = MakeLabel("Configurações", _headingFont, Theme.Text);
            _subtitle = MakeLabel("Valem na hora e ficam salvas para as próximas vezes.", _descFont, Theme.Muted);
            _note = MakeLabel(
                "O aviso de administrador do Windows é obrigatório por segurança e não pode ser desativado " +
                "pelo ReInstala. Com a opção ligada, o app faz um único pedido para a instalação inteira.",
                _descFont, Theme.Muted);

            Controls.Add(_heading);
            Controls.Add(_subtitle);
            Controls.Add(_note);

            AddRow("Instalar sem pedir confirmação",
                "O botão \"Instalar selecionados\" começa na hora, sem a pergunta de confirmação do ReInstala.",
                AppSettings.SkipInstallConfirm,
                delegate(bool value) { AppSettings.SkipInstallConfirm = value; });

            AddRow("Uma única permissão de administrador",
                "O Windows pede permissão uma vez por instalação, mesmo com um só programa, e instala todos em seguida.",
                AppSettings.SingleElevation,
                delegate(bool value) { AppSettings.SingleElevation = value; });

            AddRow("Aceitar automaticamente os termos do Winget",
                "Mantém os acordos de licença e de fonte do Winget aceitos, para baixar os programas sem interrupções.",
                AppSettings.AcceptAgreements,
                delegate(bool value) { AppSettings.AcceptAgreements = value; });
        }

        private Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = false;
            label.UseMnemonic = false;
            return label;
        }

        private void AddRow(string title, string description, bool value, Action<bool> apply)
        {
            Row row = new Row();
            row.Box = new Panel();
            row.Box.BackColor = Theme.Surface;
            row.Box.Paint += PaintRowSeparator;

            row.Title = MakeLabel(title, _titleFont, Theme.Text);
            row.Desc = MakeLabel(description, _descFont, Theme.Muted);

            row.Toggle = new ToggleSwitch();
            row.Toggle.SetCheckedSilent(value);

            ToggleSwitch toggle = row.Toggle;
            row.Toggle.CheckedChanged += delegate
            {
                if (_syncing) return;
                apply(toggle.Checked);
                AppSettings.Save();
                if (Changed != null) Changed(this, EventArgs.Empty);
            };

            row.Box.Controls.Add(row.Title);
            row.Box.Controls.Add(row.Desc);
            row.Box.Controls.Add(row.Toggle);

            Controls.Add(row.Box);
            _rows.Add(row);
        }

        private static void PaintRowSeparator(object sender, PaintEventArgs e)
        {
            Panel box = sender as Panel;
            if (box == null) return;
            using (Pen pen = new Pen(Theme.BorderSoft, 1f))
            {
                e.Graphics.DrawLine(pen, 0, box.Height - 1, box.Width, box.Height - 1);
            }
        }

        public void SyncFromSettings()
        {
            _syncing = true;
            try
            {
                if (_rows.Count >= 3)
                {
                    _rows[0].Toggle.SetCheckedSilent(AppSettings.SkipInstallConfirm);
                    _rows[1].Toggle.SetCheckedSilent(AppSettings.SingleElevation);
                    _rows[2].Toggle.SetCheckedSilent(AppSettings.AcceptAgreements);
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Surface;
            _heading.ForeColor = Theme.Text;
            _subtitle.ForeColor = Theme.Muted;
            _note.ForeColor = Theme.Muted;

            foreach (Row row in _rows)
            {
                row.Box.BackColor = Theme.Surface;
                row.Title.ForeColor = Theme.Text;
                row.Desc.ForeColor = Theme.Muted;
                row.Toggle.Invalidate();
                row.Box.Invalidate();
            }

            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutRows();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutRows();
        }

        private void LayoutRows()
        {
            if (_layingOut) return;
            _layingOut = true;
            try
            {
                LayoutRowsCore();
            }
            finally
            {
                _layingOut = false;
            }
        }

        private void LayoutRowsCore()
        {
            int pad = Theme.Px(28);
            int width = Math.Max(Theme.Px(240), Width - pad * 2);

            _heading.SetBounds(pad, Theme.Px(14), width, Theme.Px(26));
            _subtitle.SetBounds(pad, Theme.Px(42), width, Theme.Px(20));

            int top = Theme.Px(70);
            int toggleWidth = Theme.Px(46);
            int toggleHeight = Theme.Px(26);
            int textWidth = Math.Max(Theme.Px(160), width - toggleWidth - Theme.Px(26));

            // As linhas encolhem um pouco para o aviso final caber mesmo com a janela no tamanho mínimo.
            int noteGap = Theme.Px(10);
            int bottomPad = Theme.Px(12);
            int desiredNote = Theme.Px(40);
            int available = Height - top - noteGap - desiredNote - bottomPad;
            int rowHeight = Math.Max(Theme.Px(50),
                Math.Min(Theme.Px(64), available / Math.Max(1, _rows.Count)));

            foreach (Row row in _rows)
            {
                int titleY = Math.Max(Theme.Px(4), (rowHeight - Theme.Px(46)) / 2);
                row.Box.SetBounds(pad, top, width, rowHeight);
                row.Title.SetBounds(0, titleY, textWidth, Theme.Px(20));
                row.Desc.SetBounds(0, titleY + Theme.Px(20), textWidth,
                    Math.Max(Theme.Px(20), rowHeight - titleY - Theme.Px(4)));
                row.Toggle.Location = new Point(width - toggleWidth, (rowHeight - toggleHeight) / 2);
                top += rowHeight;
            }

            int noteTop = top + noteGap;
            int noteHeight = Height - noteTop - bottomPad;
            _note.Visible = noteHeight >= Theme.Px(24);
            _note.SetBounds(pad, noteTop, width, Math.Max(Theme.Px(24), noteHeight));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _headingFont.Dispose();
                _titleFont.Dispose();
                _descFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
