using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ReInstala
{
    public class MainForm : Form
    {
        private enum ScreenMode { Backup, Restore, Settings }

        private enum RestoreState { Pending, Installing, Installed, AlreadyInstalled, Failed, Cancelled }

        private class RestoreEntry
        {
            public BackupApp App;
            public bool AlreadyInstalled;
            public RestoreState State = RestoreState.Pending;
            public string Error = string.Empty;
            public ListRow Row;
        }

        private Panel _header;
        private Panel _toolbar;
        private Panel _bottom;
        private CardPanel _card;
        private Label _title;
        private Label _subtitle;
        private StatusChip _chip;
        private SegmentedControl _modes;
        private SearchBox _search;
        private CheckBox _showAll;
        private RoundedButton _refresh;
        private AppList _list;
        private StateView _state;
        private ThinProgress _progress;
        private Label _status;
        private Label _subStatus;
        private RoundedButton _primary;
        private RoundedButton _secondary;
        private RoundedButton _themeToggle;
        private ListScrollBar _scroll;
        private WheelRouter _wheelRouter;
        private SettingsView _settingsView;

        private ScreenMode _mode = ScreenMode.Backup;
        private bool _wingetReady;
        private string _wingetVersion = string.Empty;
        private bool _busy;
        private bool _installing;
        private bool _refreshing;
        private bool _started;
        private List<InstalledApp> _installed = new List<InstalledApp>();
        private readonly HashSet<string> _knownBackupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _backupChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _restoreChecked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<RestoreEntry> _restore = new List<RestoreEntry>();
        private BackupFile _backup;
        private string _backupPath = string.Empty;
        private string _initialBackup;
        private CancellationTokenSource _cts;

        public MainForm() : this(null)
        {
        }

        public MainForm(string initialBackup)
        {
            _initialBackup = initialBackup;

            AppSettings.Load();
            if (AppSettings.DarkTheme) Theme.ApplyDark();
            else Theme.ApplyLight();

            Text = "ReInstala — Backup de programas";
            BackColor = Theme.Background;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.Manual;

            float dpiScale;
            using (Graphics g = CreateGraphics())
            {
                dpiScale = g.DpiX / 96f;
            }

            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            float designWidth = 980f;
            float designHeight = 660f;
            float fitScale = Math.Min((workArea.Width - 48f) / designWidth, (workArea.Height - 48f) / designHeight);
            Theme.Scale = Math.Max(0.8f, Math.Min(2f, Math.Min(dpiScale, fitScale)));
            Theme.FontScale = Theme.Scale / dpiScale;

            Font = Theme.Font(9.75f);
            ClientSize = new Size(Theme.Px(980), Theme.Px(660));
            MinimumSize = new Size(Theme.Px(820), Theme.Px(560));

            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                workingArea.Left + Math.Max(0, (workingArea.Width - Width) / 2),
                workingArea.Top + Math.Max(0, (workingArea.Height - Height) / 2));
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
            }

            BuildUi();
            _wheelRouter = new WheelRouter(_list, _scroll);
            Application.AddMessageFilter(_wheelRouter);
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += OnShownHandler;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_wheelRouter != null)
            {
                Application.RemoveMessageFilter(_wheelRouter);
                _wheelRouter = null;
            }
            base.OnFormClosed(e);
        }

        private void BuildUi()
        {
            _header = new Panel();
            _header.BackColor = Theme.Surface;
            _header.Paint += PaintHeader;
            Controls.Add(_header);

            _title = new Label();
            _title.AutoSize = true;
            _title.Text = "ReInstala";
            _title.Font = Theme.Bold(17f);
            _title.ForeColor = Theme.Text;
            _title.BackColor = Color.Transparent;
            _header.Controls.Add(_title);

            _subtitle = new Label();
            _subtitle.AutoSize = true;
            _subtitle.Text = "Backup e restauração de programas com Winget";
            _subtitle.Font = Theme.Font(9.25f);
            _subtitle.ForeColor = Theme.Muted;
            _subtitle.BackColor = Color.Transparent;
            _header.Controls.Add(_subtitle);

            _chip = new StatusChip();
            _chip.Set("Verificando Winget...", Theme.Muted, Theme.Background, Theme.Muted);
            _header.Controls.Add(_chip);

            _themeToggle = new RoundedButton(Theme.Dark ? "Tema claro" : "Tema escuro", RoundedButton.Kind.Secondary);
            _themeToggle.Font = Theme.Bold(8.75f);
            _themeToggle.Corner = 9;
            _themeToggle.Size = new Size(Theme.Px(116), Theme.Px(32));
            _themeToggle.Click += OnThemeToggleClick;
            _header.Controls.Add(_themeToggle);

            _toolbar = new Panel();
            _toolbar.BackColor = Theme.Background;
            Controls.Add(_toolbar);

            _modes = new SegmentedControl(new string[] { "Fazer backup", "Restaurar", "Configurações" });
            _modes.SelectedIndexChanged += OnModeChanged;
            _toolbar.Controls.Add(_modes);

            _search = new SearchBox();
            _search.Size = new Size(Theme.Px(250), Theme.Px(38));
            _search.QueryChanged += delegate
            {
                if (_busy) return;
                RefreshListForMode();
            };
            _toolbar.Controls.Add(_search);

            _showAll = new CheckBox();
            _showAll.Text = "Mostrar itens de sistema";
            _showAll.AutoSize = true;
            _showAll.Font = Theme.Font(9f);
            _showAll.ForeColor = Theme.Muted;
            _showAll.BackColor = Theme.Background;
            _showAll.Cursor = Cursors.Hand;
            _showAll.CheckedChanged += delegate
            {
                if (_busy) return;
                PopulateBackupList();
            };

            _refresh = new RoundedButton("Atualizar", RoundedButton.Kind.Secondary);
            _refresh.Font = Theme.Bold(9f);
            _refresh.Size = new Size(Theme.Px(96), Theme.Px(38));
            _refresh.Corner = 10;
            _refresh.Click += OnRefreshClick;
            _toolbar.Controls.Add(_refresh);

            _card = new CardPanel();
            _card.Padding = new Padding(Theme.Px(10), Theme.Px(4), Theme.Px(10), Theme.Px(10));
            Controls.Add(_card);

            _list = new AppList();
            _list.Dock = DockStyle.Fill;
            _list.SelectionChanged += delegate { UpdateActionButtons(); UpdateStatusLine(); };
            _card.Controls.Add(_list);

            _scroll = new ListScrollBar(_list);
            _card.Controls.Add(_scroll);

            _state = new StateView();
            _state.Dock = DockStyle.Fill;
            _state.Visible = false;
            _card.Controls.Add(_state);
            _state.BringToFront();

            _settingsView = new SettingsView();
            _settingsView.Dock = DockStyle.Fill;
            _settingsView.Visible = false;
            _settingsView.Changed += delegate { UpdateStatusLine(); };
            _card.Controls.Add(_settingsView);

            _bottom = new Panel();
            _bottom.BackColor = Theme.Background;
            Controls.Add(_bottom);
            _bottom.Controls.Add(_showAll);

            _status = new Label();
            _status.AutoSize = true;
            _status.Font = Theme.Bold(9.75f);
            _status.ForeColor = Theme.Text;
            _status.BackColor = Color.Transparent;
            _status.Text = "Preparando...";
            _bottom.Controls.Add(_status);

            _subStatus = new Label();
            _subStatus.AutoSize = false;
            _subStatus.Font = Theme.Font(8.75f);
            _subStatus.ForeColor = Theme.Muted;
            _subStatus.BackColor = Color.Transparent;
            _subStatus.Text = string.Empty;
            _subStatus.AutoEllipsis = true;
            _bottom.Controls.Add(_subStatus);

            _progress = new ThinProgress();
            _progress.Visible = false;
            _bottom.Controls.Add(_progress);

            _primary = new RoundedButton("Gerar backup", RoundedButton.Kind.Primary);
            _primary.Size = new Size(Theme.Px(200), Theme.Px(44));
            _primary.Click += OnPrimaryClick;
            _bottom.Controls.Add(_primary);

            _secondary = new RoundedButton("Escolher arquivo...", RoundedButton.Kind.Secondary);
            _secondary.Size = new Size(Theme.Px(180), Theme.Px(44));
            _secondary.Visible = false;
            _secondary.Click += OnSecondaryClick;
            _bottom.Controls.Add(_secondary);

            Resize += delegate { LayoutChrome(); };
            LayoutChrome();
        }

        private void LayoutChrome()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            int headerHeight = Theme.Px(84);
            int toolbarHeight = Theme.Px(64);
            int bottomHeight = Theme.Px(112);

            _header.SetBounds(0, 0, width, headerHeight);
            _toolbar.SetBounds(0, headerHeight, width, toolbarHeight);
            _bottom.SetBounds(0, height - bottomHeight, width, bottomHeight);
            _card.SetBounds(Theme.Px(24), headerHeight + toolbarHeight, width - Theme.Px(48),
                Math.Max(Theme.Px(100), height - bottomHeight - headerHeight - toolbarHeight - Theme.Px(12)));

            if (_scroll != null)
            {
                // Fica exatamente sobre a barra nativa do ListView, cobrindo-a.
                int barWidth = SystemInformation.VerticalScrollBarWidth;
                _scroll.SetBounds(_list.Right - barWidth, _list.Top, barWidth, _list.Height);
                _scroll.BringToFront();
            }

            LayoutHeader();
            LayoutToolbar();
            LayoutBottom();
        }

        private void LayoutHeader()
        {
            _title.Location = new Point(Theme.Px(84), Theme.Px(14));
            _subtitle.Location = new Point(Theme.Px(86), Theme.Px(49));

            int right = _header.Width - Theme.Px(24);
            _chip.Location = new Point(right - _chip.Width, Theme.Px(28));
            if (_themeToggle != null)
            {
                _themeToggle.Location = new Point(_chip.Left - Theme.Px(10) - _themeToggle.Width,
                    Theme.Px(26) + (Theme.Px(28) - _themeToggle.Height) / 2);
            }
        }

        private void LayoutToolbar()
        {
            _modes.SetBounds(Theme.Px(24), Theme.Px(13), Theme.Px(420), Theme.Px(38));

            int right = _toolbar.Width - Theme.Px(24);
            _refresh.Location = new Point(right - _refresh.Width, Theme.Px(13));
            _search.Location = new Point(_refresh.Left - Theme.Px(10) - _search.Width, Theme.Px(13));
        }

        private void LayoutBottom()
        {
            int width = _bottom.Width;

            _primary.Location = new Point(width - Theme.Px(24) - _primary.Width, Theme.Px(22));
            _secondary.Location = new Point(_primary.Left - Theme.Px(10) - _secondary.Width, Theme.Px(22));

            _status.Location = new Point(Theme.Px(26), Theme.Px(12));
            _subStatus.Location = new Point(Theme.Px(26), Theme.Px(36));
            _subStatus.Width = Math.Max(Theme.Px(120), _secondary.Left - Theme.Px(52));

            _showAll.Location = new Point(Theme.Px(26), Theme.Px(60));

            int progressWidth = Math.Max(Theme.Px(120), _secondary.Left - Theme.Px(52));
            _progress.SetBounds(Theme.Px(26), Theme.Px(90), progressWidth, Theme.Px(6));
        }

        private void PaintHeader(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(Theme.Surface))
            {
                g.FillRectangle(brush, _header.ClientRectangle);
            }
            using (Pen pen = new Pen(Theme.Border, 1f))
            {
                g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            }

            Theme.DrawLogo(g, new Rectangle(Theme.Px(24), Theme.Px(20), Theme.Px(44), Theme.Px(44)));
        }

        private async void OnShownHandler(object sender, EventArgs e)
        {
            if (_started) return;
            _started = true;
            ClampToScreen();
            ApplyWindowFrameTheme();
            await InitializeAsync();

            if (!string.IsNullOrEmpty(_initialBackup) && _wingetReady)
            {
                string file = _initialBackup;
                _initialBackup = null;
                if (File.Exists(file))
                {
                    _modes.SelectedIndex = 1;
                    LoadBackupFile(file);
                }
            }
        }

        private void ClampToScreen()
        {
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int maxWidth = Math.Max(Theme.Px(600), work.Width - Theme.Px(16));
            int maxHeight = Math.Max(Theme.Px(400), work.Height - Theme.Px(16));
            if (Width > maxWidth || Height > maxHeight)
            {
                Size = new Size(Math.Min(Width, maxWidth), Math.Min(Height, maxHeight));
            }

            int x = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
            int y = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
            Location = new Point(x, y);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (_busy || !_wingetReady) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            if (files[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            if (_busy || !_wingetReady) return;
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) return;
            if (!files[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return;
            _modes.SelectedIndex = 1;
            LoadBackupFile(files[0]);
        }

        private async Task InitializeAsync()
        {
            bool hasExe = Winget.FindExe() != null;
            _wingetReady = hasExe;

            if (!hasExe)
            {
                SetBusy(true, "Verificando o Winget...", true);
                WingetResult probe = await Winget.RunAsync("--version", 25000, CancellationToken.None);
                _wingetReady = probe.ExitCode == 0 && Winget.FindExe() != null;
                _wingetVersion = string.Empty;
                UpdateWingetChip();
                SetBusy(false, null);
                if (!_wingetReady)
                {
                    ShowWingetMissingState(null);
                    UpdateActionButtons();
                    return;
                }
            }

            // Abre na hora com a última lista salva; a atualização roda depois,
            // em segundo plano, sem travar a janela.
            if (_installed.Count == 0)
            {
                List<InstalledApp> cached = WingetCache.Load();
                if (cached.Count > 0)
                {
                    _installed = cached;
                    SeedBackupChecks();
                    if (_mode == ScreenMode.Backup) PopulateBackupList();
                    else if (_mode == ScreenMode.Restore)
                    {
                        if (_backup != null) PopulateRestoreList();
                        else ShowRestoreEmptyState();
                    }
                }
            }

            if (_wingetVersion.Length == 0)
            {
                _wingetVersion = WingetCache.LoadVersion();
                if (_wingetVersion.Length > 0) UpdateWingetChip();
            }

            if (_installed.Count > 0)
            {
                UpdateActionButtons();
                UpdateStatusLine();
                if (_wingetVersion.Length == 0) StartVersionLookup();
                if (!WingetCache.IsFresh(2)) RefreshInBackground();
                return;
            }

            // Primeira execução, sem lista salva: detecta agora.
            // A versão do Winget é consultada depois da listagem para não
            // disputar o mesmo processo do Winget (ele serializa as chamadas).
            Task<WingetResult> listTask = Winget.RunAsync(Winget.ListArguments(), 300000, CancellationToken.None);
            await LoadInstalledAsync(listTask);
            UpdateWingetChip();
            StartVersionLookup();
        }

        private async void RefreshInBackground()
        {
            if (_refreshing) return;
            try
            {
                await LoadInstalledAsync(null);
            }
            catch (Exception)
            {
                EndBackgroundRefresh();
            }
        }

        private async void StartVersionLookup()
        {
            UpdateWingetChip();
            try
            {
                WingetResult version = await Winget.RunAsync("--version", 25000, CancellationToken.None);
                if (version.ExitCode != 0)
                {
                    _wingetVersion = string.Empty;
                    UpdateWingetChip();
                    return;
                }

                string clean = CleanVersion(version.Output);
                if (clean.Length == 0)
                {
                    _wingetVersion = string.Empty;
                    UpdateWingetChip();
                    return;
                }
                _wingetVersion = clean;
                WingetCache.SaveVersion(clean);
                UpdateWingetChip();
            }
            catch (Exception)
            {
                UpdateWingetChip();
            }
        }

        private void BeginBackgroundRefresh()
        {
            _refreshing = true;
            _progress.Visible = true;
            _progress.Indeterminate = true;
            _refresh.Enabled = false;
            UpdateStatusLine();
            UpdateActionButtons();
        }

        private void EndBackgroundRefresh()
        {
            _refreshing = false;
            _progress.Visible = false;
            _progress.Value = 0;
            _refresh.Enabled = !_busy;
            UpdateStatusLine();
            UpdateActionButtons();
        }

        private void UpdateWingetChip()
        {
            if (_chip == null) return;
            if (!_wingetReady)
            {
                _chip.Set("Winget ausente", Theme.Danger, Theme.DangerSoft, Theme.Danger);
            }
            else
            {
                _chip.Set("Winget " + (_wingetVersion.Length > 0 ? _wingetVersion : "pronto"),
                    Theme.Success, Theme.SuccessSoft, Theme.Success);
            }
            LayoutHeader();
        }

        private static string CleanVersion(string output)
        {
            if (string.IsNullOrEmpty(output)) return string.Empty;
            string[] lines = Winget.CleanText(output).Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("v", StringComparison.OrdinalIgnoreCase)) line = line.Substring(1);
                if (line.Length > 24) line = line.Substring(0, 24);
                return line;
            }
            return string.Empty;
        }

        private async Task LoadInstalledAsync()
        {
            await LoadInstalledAsync(null);
        }

        private async Task LoadInstalledAsync(Task<WingetResult> pending)
        {
            bool hasList = _installed.Count > 0;
            if (hasList)
            {
                BeginBackgroundRefresh();
            }
            else
            {
                SetBusy(true, "Detectando programas instalados...", true);
                ShowState(StateIcon.Download, "Detectando programas instalados",
                    "O ReInstala está consultando o Winget. Isso leva alguns segundos.");
            }

            bool failed = false;
            bool empty = false;
            try
            {
                WingetResult result = pending != null
                    ? await pending
                    : await Winget.RunAsync(Winget.ListArguments(), 300000, CancellationToken.None);

                failed = result == null || !result.Started || result.TimedOut;
                if (!failed)
                {
                    List<InstalledApp> parsed = Winget.ParseList(result.Output);
                    if (parsed.Count == 0)
                    {
                        failed = true;
                        empty = true;
                    }
                    else
                    {
                        _installed = parsed;
                        WingetCache.Save(_installed);
                        if (_wingetVersion.Length > 0) WingetCache.SaveVersion(_wingetVersion);
                        SeedBackupChecks();

                        if (_mode == ScreenMode.Backup)
                        {
                            PopulateBackupList();
                        }
                        else if (_mode == ScreenMode.Restore)
                        {
                            if (_backup != null)
                            {
                                RefreshAlreadyInstalled();
                                PopulateRestoreList();
                            }
                            else
                            {
                                ShowRestoreEmptyState();
                            }
                        }
                    }
                }

                if (failed)
                {
                    if (!hasList)
                    {
                        if (empty)
                        {
                            ShowErrorState("Nenhum programa encontrado",
                                "O Winget não retornou nenhum programa instalado. Verifique se ele está funcionando e tente novamente.");
                        }
                        else
                        {
                            ShowErrorState("Não foi possível detectar os programas", Winget.FailureMessage(result));
                        }
                    }
                }
            }
            finally
            {
                if (hasList) EndBackgroundRefresh();
                else SetBusy(false, null);

                if (failed && hasList && _subStatus != null)
                {
                    _subStatus.Text = "Não foi possível atualizar agora. A lista salva continua disponível.";
                }
                UpdateActionButtons();
            }
        }

        private void SeedBackupChecks()
        {
            foreach (InstalledApp app in _installed)
            {
                if (app.Restorable && !app.Framework)
                {
                    if (!_knownBackupIds.Contains(app.Id))
                    {
                        _knownBackupIds.Add(app.Id);
                        _backupChecked.Add(app.Id);
                    }
                }
            }
        }

        private void RefreshListForMode()
        {
            if (_mode == ScreenMode.Settings) return;

            if (_mode == ScreenMode.Backup)
            {
                if (_wingetReady) PopulateBackupList();
            }
            else if (_backup != null)
            {
                PopulateRestoreList();
            }
        }

        private bool MatchesQuery(string name, string id)
        {
            string query = _search.Query.Trim();
            if (query.Length == 0) return true;
            if (name != null && name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            if (id != null && id.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0) return true;
            return false;
        }

        private void PopulateBackupList()
        {
            if (!_wingetReady) return;

            _list.ApplyMode(ListMode.Backup);
            List<ListRow> rows = new List<ListRow>();

            foreach (InstalledApp app in _installed)
            {
                bool systemItem = !app.Restorable || app.Framework;
                if (systemItem && !_showAll.Checked) continue;
                if (!MatchesQuery(app.Name, app.Id)) continue;

                ListRow row = new ListRow();
                row.Key = app.Id;
                row.Name = app.Name;
                row.Version = app.Version;
                row.Detail = app.Id;
                row.Checkable = app.Restorable && !app.Framework;
                row.Muted = !app.Restorable || app.Framework;
                row.Checked = row.Checkable && _backupChecked.Contains(app.Id);
                row.Tag = app;

                if (app.Restorable && !app.Framework)
                {
                    if (string.Equals(app.Source, "msstore", StringComparison.OrdinalIgnoreCase))
                    {
                        row.ChipText = "Microsoft Store";
                        row.ChipFore = Theme.Info;
                        row.ChipBack = Theme.InfoSoft;
                    }
                    else
                    {
                        row.ChipText = "Winget";
                        row.ChipFore = Theme.Success;
                        row.ChipBack = Theme.SuccessSoft;
                    }
                }
                else if (app.Framework)
                {
                    row.ChipText = "Biblioteca";
                    row.ChipFore = Theme.Muted;
                    row.ChipBack = Theme.Background;
                }
                else
                {
                    row.ChipText = "Sem suporte";
                    row.ChipFore = Theme.Muted;
                    row.ChipBack = Theme.Background;
                }

                rows.Add(row);
            }

            _list.SetRows(rows);
            HideState();
            UpdateStatusLine();
            UpdateActionButtons();
        }

        private void PopulateRestoreList()
        {
            if (_backup == null) return;

            _list.ApplyMode(ListMode.Restore);
            List<ListRow> rows = new List<ListRow>();

            foreach (RestoreEntry entry in _restore)
            {
                if (!MatchesQuery(entry.App.Name, entry.App.Id)) continue;

                ListRow row = new ListRow();
                row.Key = entry.App.Id;
                row.Name = entry.App.Name;
                row.Version = entry.App.Version;
                row.Detail = entry.App.Id;
                row.Checkable = !entry.AlreadyInstalled;
                row.Checked = row.Checkable && _restoreChecked.Contains(entry.App.Id);
                row.Tag = entry;
                entry.Row = row;
                ApplyEntryChip(entry);
                rows.Add(row);
            }

            _list.SetRows(rows);
            HideState();
            UpdateStatusLine();
            UpdateActionButtons();
        }

        private void ApplyEntryChip(RestoreEntry entry)
        {
            if (entry.Row == null) return;
            switch (entry.State)
            {
                case RestoreState.Installing:
                    entry.Row.ChipText = "Instalando...";
                    entry.Row.ChipFore = Theme.Accent;
                    entry.Row.ChipBack = Theme.AccentSoft;
                    break;
                case RestoreState.Installed:
                    entry.Row.ChipText = "Instalado";
                    entry.Row.ChipFore = Theme.Success;
                    entry.Row.ChipBack = Theme.SuccessSoft;
                    break;
                case RestoreState.AlreadyInstalled:
                    entry.Row.ChipText = "Já instalado";
                    entry.Row.ChipFore = Theme.Muted;
                    entry.Row.ChipBack = Theme.Background;
                    break;
                case RestoreState.Failed:
                    entry.Row.ChipText = "Falhou";
                    entry.Row.ChipFore = Theme.Danger;
                    entry.Row.ChipBack = Theme.DangerSoft;
                    break;
                case RestoreState.Cancelled:
                    entry.Row.ChipText = "Cancelado";
                    entry.Row.ChipFore = Theme.Warn;
                    entry.Row.ChipBack = Theme.WarnSoft;
                    break;
                default:
                    entry.Row.ChipText = "Pendente";
                    entry.Row.ChipFore = Theme.Muted;
                    entry.Row.ChipBack = Theme.Background;
                    break;
            }
        }

        private void RefreshEntryRow(RestoreEntry entry)
        {
            ApplyEntryChip(entry);
            if (entry.Row != null && (entry.State == RestoreState.Installed || entry.State == RestoreState.AlreadyInstalled))
            {
                entry.Row.Checkable = false;
                entry.Row.Checked = false;
            }
            _list.Invalidate();
        }

        private int SelectableBackupCount()
        {
            int count = 0;
            foreach (InstalledApp app in _installed)
            {
                if (app.Restorable && !app.Framework) count++;
            }
            return count;
        }

        private int SelectedBackupCount()
        {
            int count = 0;
            foreach (InstalledApp app in _installed)
            {
                if (app.Restorable && !app.Framework && _backupChecked.Contains(app.Id)) count++;
            }
            return count;
        }

        private int SelectedRestoreCount()
        {
            int count = 0;
            foreach (RestoreEntry entry in _restore)
            {
                if (!entry.AlreadyInstalled && entry.State != RestoreState.Installed && _restoreChecked.Contains(entry.App.Id)) count++;
            }
            return count;
        }

        private void UpdateStatusLine()
        {
            if (_busy) return;
            if (_showAll != null) _showAll.Visible = _wingetReady && _mode == ScreenMode.Backup;

            if (_mode == ScreenMode.Settings)
            {
                _status.Text = "Configurações";
                _subStatus.Text = "As opções padrão já deixam tudo pronto.";
                return;
            }

            if (_mode == ScreenMode.Backup)
            {
                if (!_wingetReady)
                {
                    _status.Text = "Winget não encontrado";
                    _subStatus.Text = "Instale o Winget para detectar os programas do computador.";
                    return;
                }

                int selected = SelectedBackupCount();
                int selectable = SelectableBackupCount();
                int systemItems = _installed.Count - selectable;

                if (_installed.Count == 0)
                {
                    _status.Text = "Nenhum programa detectado";
                    _subStatus.Text = "Clique em Atualizar para consultar o Winget novamente.";
                    return;
                }

                _status.Text = string.Format("{0} de {1} programas selecionados", selected, selectable);
                string text = string.Format("{0} detectados · {1} reinstaláveis", _installed.Count, selectable);
                if (systemItems > 0)
                {
                    text += _showAll.Checked
                        ? string.Format(" · {0} de sistema visíveis", systemItems)
                        : string.Format(" · {0} de sistema ocultos", systemItems);
                }
                if (!string.IsNullOrEmpty(_search.Query))
                {
                    text += string.Format(" · {0} na busca", _list.Items.Count);
                }
                if (_refreshing) text += " · atualizando...";
                _subStatus.Text = text;
            }
            else
            {
                if (_backup == null)
                {
                    _status.Text = "Nenhum backup carregado";
                    _subStatus.Text = "Escolha um arquivo gerado pelo ReInstala para restaurar os programas.";
                    return;
                }

                int selected = SelectedRestoreCount();
                int total = _restore.Count;
                int installed = 0;
                foreach (RestoreEntry entry in _restore) if (entry.AlreadyInstalled) installed++;

                _status.Text = string.Format("{0} de {1} programas marcados", selected, total);
                string text = "Backup de " + FormatDate(_backup.CreatedAt) + " · " + _backup.Computer;
                if (installed > 0) text += string.Format(" · {0} já instalados", installed);
                if (_refreshing) text += " · atualizando...";
                _subStatus.Text = text;
            }
        }

        private static string FormatDate(string iso)
        {
            DateTime date;
            if (DateTime.TryParse(iso, out date))
            {
                return date.ToString("dd/MM/yyyy 'às' HH:mm");
            }
            return iso;
        }

        private bool HasSelection()
        {
            if (_mode == ScreenMode.Backup) return SelectedBackupCount() > 0;
            return SelectedRestoreCount() > 0;
        }

        private void UpdateActionButtons()
        {
            if (_installing) return;

            if (_mode == ScreenMode.Settings)
            {
                _primary.Visible = false;
                _secondary.Visible = false;
                return;
            }

            if (_mode == ScreenMode.Backup)
            {
                _primary.Text = "Gerar backup";
                _primary.Style = RoundedButton.Kind.Primary;
                _primary.Visible = true;
                _secondary.Visible = false;
            }
            else
            {
                _primary.Text = "Instalar selecionados";
                _primary.Style = RoundedButton.Kind.Primary;
                _primary.Visible = true;
                _secondary.Visible = true;
                _secondary.Style = RoundedButton.Kind.Secondary;
                _secondary.Text = _backup == null ? "Escolher arquivo..." : "Trocar arquivo";
            }

            _primary.Enabled = !_busy && !(_refreshing && _mode == ScreenMode.Restore) && HasSelection() && _wingetReady;
            _secondary.Enabled = !_busy || _installing;
        }

        private void SetBusy(bool busy, string message)
        {
            SetBusy(busy, message, false);
        }

        private void SetBusy(bool busy, string message, bool indeterminate)
        {
            _busy = busy;
            _progress.Visible = busy;
            _progress.Indeterminate = busy && indeterminate;
            if (!busy) _progress.Value = 0;

            _modes.Enabled = !busy;
            _search.Enabled = !busy;
            _showAll.Enabled = !busy;
            _refresh.Enabled = !busy && !_refreshing;
            if (_themeToggle != null) _themeToggle.Enabled = !busy;
            _list.Interactive = !busy && !_installing;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;

            if (busy && message != null)
            {
                _status.Text = message;
                _subStatus.Text = string.Empty;
            }
            else if (!busy)
            {
                UpdateStatusLine();
            }

            UpdateActionButtons();
        }

        private static int ModeToIndex(ScreenMode mode)
        {
            if (mode == ScreenMode.Backup) return 0;
            if (mode == ScreenMode.Restore) return 1;
            return 2;
        }

        private void ShowSettings()
        {
            if (_settingsView == null) return;
            _settingsView.SyncFromSettings();
            _settingsView.Visible = true;
            _settingsView.BringToFront();
            _list.Visible = false;
            if (_scroll != null) _scroll.Visible = false;
            if (_state != null) _state.Visible = false;
            _settingsView.Invalidate();
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            if (_busy)
            {
                _modes.SelectedIndex = ModeToIndex(_mode);
                return;
            }

            _mode = _modes.SelectedIndex == 0 ? ScreenMode.Backup
                : (_modes.SelectedIndex == 1 ? ScreenMode.Restore : ScreenMode.Settings);

            bool listMode = _mode != ScreenMode.Settings;
            _search.Visible = listMode;
            _refresh.Visible = listMode;
            _secondary.Visible = _mode == ScreenMode.Restore;
            _showAll.Visible = listMode && _mode == ScreenMode.Backup;

            if (_mode == ScreenMode.Settings)
            {
                ShowSettings();
                UpdateStatusLine();
                UpdateActionButtons();
                return;
            }

            if (_settingsView != null) _settingsView.Visible = false;

            if (_mode == ScreenMode.Backup)
            {
                if (!_wingetReady)
                {
                    ShowWingetMissingState(null);
                    UpdateStatusLine();
                    UpdateActionButtons();
                    return;
                }
                PopulateBackupList();
            }
            else
            {
                if (!_wingetReady)
                {
                    ShowWingetMissingState(null);
                    UpdateStatusLine();
                    UpdateActionButtons();
                    return;
                }
                if (_backup == null) ShowRestoreEmptyState();
                else PopulateRestoreList();
            }

            UpdateStatusLine();
            UpdateActionButtons();
        }

        private async void OnRefreshClick(object sender, EventArgs e)
        {
            if (_busy || _refreshing) return;
            if (!_wingetReady)
            {
                await InitializeAsync();
                return;
            }
            await LoadInstalledAsync();
        }

        private void OnThemeToggleClick(object sender, EventArgs e)
        {
            if (_busy || _installing) return;

            AppSettings.DarkTheme = !AppSettings.DarkTheme;
            if (AppSettings.DarkTheme) Theme.ApplyDark();
            else Theme.ApplyLight();
            AppSettings.Save();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            BackColor = Theme.Background;
            _header.BackColor = Theme.Surface;
            _toolbar.BackColor = Theme.Background;
            _bottom.BackColor = Theme.Background;
            _card.BackColor = Theme.Background;

            _title.ForeColor = Theme.Text;
            _subtitle.ForeColor = Theme.Muted;
            _status.ForeColor = Theme.Text;
            _subStatus.ForeColor = Theme.Muted;

            _showAll.ForeColor = Theme.Muted;
            _showAll.BackColor = Theme.Background;

            _search.ApplyTheme();
            _list.ApplyTheme();
            if (_scroll != null) _scroll.ApplyTheme();
            if (_settingsView != null) _settingsView.ApplyTheme();
            _progress.BackColor = Theme.Background;
            _state.BackColor = Theme.Surface;

            _themeToggle.Text = AppSettings.DarkTheme ? "Tema claro" : "Tema escuro";
            UpdateWingetChip();

            if (_wingetReady)
            {
                if (_mode == ScreenMode.Backup)
                {
                    PopulateBackupList();
                }
                else if (_mode == ScreenMode.Restore)
                {
                    if (_backup != null) PopulateRestoreList();
                    else _state.Invalidate();
                }
                else
                {
                    _settingsView.Invalidate();
                }
            }
            else
            {
                _state.Invalidate();
            }

            _header.Invalidate();
            _toolbar.Invalidate();
            _bottom.Invalidate();
            _card.Invalidate();
            Invalidate(true);
            ApplyWindowFrameTheme();
            Update();
        }

        private void ApplyWindowFrameTheme()
        {
            if (!IsHandleCreated) return;
            try
            {
                NativeTheme.SetDark(Theme.Dark);
                NativeTheme.ApplyToWindow(Handle, Theme.Dark);
                int value = Theme.Dark ? 1 : 0;
                DwmSetWindowAttribute(Handle, 20, ref value, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
                DwmSetWindowAttribute(Handle, 19, ref value, 4); // versões anteriores do Windows 10
            }
            catch (Exception)
            {
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private async void OnPrimaryClick(object sender, EventArgs e)
        {
            if (_busy) return;
            if (_mode == ScreenMode.Restore && _refreshing) return;
            if (_mode == ScreenMode.Backup) GenerateBackup();
            else if (_mode == ScreenMode.Restore) await InstallSelectedAsync();
        }

        private void OnSecondaryClick(object sender, EventArgs e)
        {
            if (_installing)
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    _status.Text = "Cancelando...";
                    _secondary.Enabled = false;
                    _secondary.Text = "Cancelando...";
                }
                return;
            }
            if (_mode == ScreenMode.Restore) OpenBackupFile();
        }

        private void GenerateBackup()
        {
            List<InstalledApp> selected = new List<InstalledApp>();
            foreach (InstalledApp app in _installed)
            {
                if (app.Restorable && _backupChecked.Contains(app.Id)) selected.Add(app);
            }

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Selecione pelo menos um programa para salvar no backup.", "ReInstala",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                BackupFile file = BackupStore.Create(selected, _wingetVersion);
                string path = BackupStore.Save(file);

                string message = "Backup gerado com sucesso!\n\n" + selected.Count + " programa(s) salvo(s) em:\n" + path +
                                 "\n\nDeseja abrir a pasta onde o arquivo foi salvo?";
                DialogResult answer = MessageBox.Show(this, message, "Backup concluído",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (answer == DialogResult.Yes) OpenInExplorer(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível salvar o backup.\n\n" + ex.Message, "ReInstala",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenInExplorer(string path)
        {
            try
            {
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception)
            {
                try { Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\""); }
                catch (Exception) { }
            }
        }

        private void OpenBackupFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Selecione o arquivo de backup do ReInstala";
                dialog.Filter = "Backup do ReInstala (*.json)|*.json|Todos os arquivos (*.*)|*.*";
                dialog.InitialDirectory = BackupStore.DownloadsFolder();
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                LoadBackupFile(dialog.FileName);
            }
        }

        private void LoadBackupFile(string path)
        {
            try
            {
                BackupFile file = BackupStore.Load(path);
                _backup = file;
                _backupPath = path;
                _restore.Clear();
                _restoreChecked.Clear();

                HashSet<string> installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (InstalledApp app in _installed) installedIds.Add(app.Id);

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (BackupApp app in file.Apps)
                {
                    if (!seen.Add(app.Id)) continue;
                    RestoreEntry entry = new RestoreEntry();
                    entry.App = app;
                    entry.AlreadyInstalled = installedIds.Contains(app.Id);
                    entry.State = entry.AlreadyInstalled ? RestoreState.AlreadyInstalled : RestoreState.Pending;
                    if (!entry.AlreadyInstalled) _restoreChecked.Add(app.Id);
                    _restore.Add(entry);
                }

                _search.Query = string.Empty;
                PopulateRestoreList();
            }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(this, ex.Message, "Backup inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível abrir o backup.\n\n" + ex.Message, "ReInstala",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshAlreadyInstalled()
        {
            HashSet<string> installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (InstalledApp app in _installed) installedIds.Add(app.Id);

            foreach (RestoreEntry entry in _restore)
            {
                if (entry.State == RestoreState.Installed) continue;
                entry.AlreadyInstalled = installedIds.Contains(entry.App.Id);
                if (entry.AlreadyInstalled)
                {
                    entry.State = RestoreState.AlreadyInstalled;
                    _restoreChecked.Remove(entry.App.Id);
                }
            }
        }

        private async Task InstallSelectedAsync()
        {
            List<RestoreEntry> targets = new List<RestoreEntry>();
            foreach (RestoreEntry entry in _restore)
            {
                if (entry.AlreadyInstalled) continue;
                if (entry.State == RestoreState.Installed) continue;
                if (_restoreChecked.Contains(entry.App.Id)) targets.Add(entry);
            }

            if (targets.Count == 0)
            {
                MessageBox.Show(this, "Marque pelo menos um programa para instalar.", "ReInstala",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool batchElevate = !BatchInstaller.IsElevated() &&
                                (targets.Count > 1 || AppSettings.SingleElevation);

            if (!AppSettings.SkipInstallConfirm)
            {
                string confirmation = string.Format(
                    "Instalar {0} programa(s) agora?\n\nO Winget fará o download e a instalação em segundo plano.", targets.Count);
                if (batchElevate)
                {
                    confirmation += "\n\nPara evitar vários pedidos de permissão, o Windows vai pedir permissão de administrador " +
                                    "uma única vez e instalará todos os programas em seguida.";
                }
                else
                {
                    confirmation += "\n\nO Windows pode pedir permissão de administrador em alguns casos.";
                }

                if (MessageBox.Show(this, confirmation, "Confirmar instalação",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            _installing = true;
            _list.Interactive = false;
            SetBusy(true, "Iniciando instalação...", false);
            _primary.Text = "Instalando...";
            _primary.Enabled = false;
            _secondary.Text = "Cancelar";
            _secondary.Style = RoundedButton.Kind.Danger;
            _secondary.Enabled = true;
            _secondary.Visible = true;

            try
            {
                if (batchElevate)
                {
                    bool started = await RunElevatedBatchAsync(targets);
                    if (!started)
                    {
                        foreach (RestoreEntry entry in targets)
                        {
                            if (entry.State == RestoreState.Installing)
                            {
                                entry.State = RestoreState.Pending;
                                RefreshEntryRow(entry);
                            }
                        }
                        return;
                    }
                }
                else
                {
                    await RunSequentialInstallAsync(targets);
                }

                foreach (RestoreEntry entry in targets)
                {
                    if (entry.State == RestoreState.Pending || entry.State == RestoreState.Installing)
                    {
                        entry.State = RestoreState.Cancelled;
                        RefreshEntryRow(entry);
                    }
                }
            }
            finally
            {
                _installing = false;
                _list.Interactive = true;
                _secondary.Style = RoundedButton.Kind.Secondary;
                SetBusy(false, null);
                _progress.Value = 0;
                _progress.Visible = false;
                UpdateStatusLine();
                UpdateActionButtons();
            }

            int success = 0;
            int failed = 0;
            int cancelled = 0;
            List<string> failures = new List<string>();
            foreach (RestoreEntry entry in targets)
            {
                if (entry.State == RestoreState.Installed || entry.State == RestoreState.AlreadyInstalled)
                {
                    success++;
                }
                else if (entry.State == RestoreState.Failed)
                {
                    failed++;
                    failures.Add(entry.App.Name + ": " + entry.Error);
                }
                else if (entry.State == RestoreState.Cancelled)
                {
                    cancelled++;
                }
            }

            ShowInstallSummary(success, failed, cancelled, failures, targets.Count);
        }

        private async Task RunSequentialInstallAsync(List<RestoreEntry> targets)
        {
            int total = targets.Count;
            int done = 0;

            foreach (RestoreEntry entry in targets)
            {
                if (_cts.IsCancellationRequested) return;

                entry.State = RestoreState.Installing;
                entry.Error = string.Empty;
                RefreshEntryRow(entry);

                _status.Text = string.Format("Instalando {0} de {1}: {2}", done + 1, total, entry.App.Name);
                _subStatus.Text = entry.App.Id;
                _progress.Value = (int)Math.Round(done * 100.0 / total);

                WingetResult result = await Winget.InstallAsync(entry.App.Id, entry.App.Source, true, _cts.Token);

                if (result.ExitCode == 0)
                {
                    entry.State = RestoreState.Installed;
                }
                else if (Winget.WasAlreadyInstalled(result))
                {
                    entry.State = RestoreState.AlreadyInstalled;
                    entry.AlreadyInstalled = true;
                }
                else if (_cts.IsCancellationRequested)
                {
                    entry.State = RestoreState.Cancelled;
                    RefreshEntryRow(entry);
                    return;
                }
                else
                {
                    _subStatus.Text = "Tentando novamente sem o modo silencioso...";
                    result = await Winget.InstallAsync(entry.App.Id, entry.App.Source, false, _cts.Token);

                    if (result.ExitCode == 0)
                    {
                        entry.State = RestoreState.Installed;
                    }
                    else if (Winget.WasAlreadyInstalled(result))
                    {
                        entry.State = RestoreState.AlreadyInstalled;
                        entry.AlreadyInstalled = true;
                    }
                    else if (_cts.IsCancellationRequested)
                    {
                        entry.State = RestoreState.Cancelled;
                        RefreshEntryRow(entry);
                        return;
                    }
                    else
                    {
                        entry.State = RestoreState.Failed;
                        entry.Error = Winget.FailureMessage(result);
                    }
                }

                if (entry.State == RestoreState.Installed || entry.State == RestoreState.AlreadyInstalled)
                {
                    _restoreChecked.Remove(entry.App.Id);
                }

                done++;
                RefreshEntryRow(entry);
                _progress.Value = (int)Math.Round(done * 100.0 / total);
            }
        }

        private async Task<bool> RunElevatedBatchAsync(List<RestoreEntry> targets)
        {
            List<BatchJob> jobs = new List<BatchJob>();
            foreach (RestoreEntry entry in targets)
            {
                BatchJob job = new BatchJob();
                job.Id = entry.App.Id;
                job.Source = entry.App.Source;
                jobs.Add(job);
            }

            Progress<BatchEvent> progress = new Progress<BatchEvent>(delegate(BatchEvent ev)
            {
                ApplyBatchEvent(targets, ev);
            });

            BatchOutcome outcome = await BatchInstaller.RunAsync(jobs, progress, _cts.Token);

            if (outcome.UacCancelled)
            {
                MessageBox.Show(this,
                    "A instalação foi cancelada porque a permissão de administrador não foi concedida.\n\n" +
                    "Clique em \"Instalar selecionados\" novamente e aprove o pedido do Windows.",
                    "ReInstala", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!outcome.Started)
            {
                foreach (RestoreEntry entry in targets)
                {
                    if (entry.State == RestoreState.Pending || entry.State == RestoreState.Installing)
                    {
                        entry.State = RestoreState.Failed;
                        entry.Error = outcome.Error.Length > 0 ? outcome.Error : "Não foi possível iniciar a instalação.";
                        RefreshEntryRow(entry);
                    }
                }
                return true;
            }

            if (outcome.Error.Length > 0 && !outcome.Finished)
            {
                _subStatus.Text = outcome.Error;
            }

            bool unresolved = false;
            foreach (RestoreEntry entry in targets)
            {
                if (entry.State == RestoreState.Pending || entry.State == RestoreState.Installing) unresolved = true;
            }

            if (unresolved && (_cts == null || !_cts.IsCancellationRequested))
            {
                await ReconcileAfterBatchAsync(targets);
            }

            return true;
        }

        private void ApplyBatchEvent(List<RestoreEntry> targets, BatchEvent ev)
        {
            if (ev == null) return;

            if (ev.Kind == "I" && ev.Index >= 0 && ev.Index < targets.Count)
            {
                RestoreEntry entry = targets[ev.Index];
                entry.State = RestoreState.Installing;
                entry.Error = string.Empty;
                RefreshEntryRow(entry);
                _status.Text = string.Format("Instalando {0} de {1}: {2}", ev.Index + 1, targets.Count, entry.App.Name);
                _subStatus.Text = entry.App.Id;
                return;
            }

            if (ev.Kind == "R" && ev.Index >= 0 && ev.Index < targets.Count)
            {
                RestoreEntry entry = targets[ev.Index];
                if (ev.ExitCode == 0)
                {
                    entry.State = RestoreState.Installed;
                }
                else if (ev.AlreadyInstalled)
                {
                    entry.State = RestoreState.AlreadyInstalled;
                    entry.AlreadyInstalled = true;
                }
                else if (_cts != null && _cts.IsCancellationRequested)
                {
                    entry.State = RestoreState.Cancelled;
                }
                else
                {
                    entry.State = RestoreState.Failed;
                    entry.Error = string.IsNullOrEmpty(ev.Message) ? "Falha ao instalar." : ev.Message;
                }

                if (entry.State == RestoreState.Installed || entry.State == RestoreState.AlreadyInstalled)
                {
                    _restoreChecked.Remove(entry.App.Id);
                }

                RefreshEntryRow(entry);
                _progress.Value = (int)Math.Round((ev.Index + 1) * 100.0 / targets.Count);
                return;
            }

            if (ev.Kind == "C")
            {
                foreach (RestoreEntry entry in targets)
                {
                    if (entry.State == RestoreState.Pending || entry.State == RestoreState.Installing)
                    {
                        entry.State = RestoreState.Cancelled;
                        RefreshEntryRow(entry);
                    }
                }
            }
        }

        private async Task ReconcileAfterBatchAsync(List<RestoreEntry> targets)
        {
            List<InstalledApp> now = await RefreshInstalledSnapshotAsync();

            HashSet<string> installedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (now != null)
            {
                foreach (InstalledApp app in now) installedIds.Add(app.Id);
            }

            foreach (RestoreEntry entry in targets)
            {
                if (entry.State != RestoreState.Pending && entry.State != RestoreState.Installing) continue;

                if (now != null && installedIds.Contains(entry.App.Id))
                {
                    entry.State = RestoreState.Installed;
                    _restoreChecked.Remove(entry.App.Id);
                }
                else
                {
                    entry.State = RestoreState.Failed;
                    if (string.IsNullOrEmpty(entry.Error))
                    {
                        entry.Error = now == null
                            ? "Não foi possível confirmar a instalação."
                            : "A instalação não foi confirmada.";
                    }
                }
                RefreshEntryRow(entry);
            }
        }

        private async Task<List<InstalledApp>> RefreshInstalledSnapshotAsync()
        {
            WingetResult result = await Winget.RunAsync(Winget.ListArguments(), 300000, CancellationToken.None);
            if (!result.Started || result.TimedOut) return null;

            List<InstalledApp> now = Winget.ParseList(result.Output);
            if (now.Count == 0) return null;

            _installed = now;
            WingetCache.Save(now);
            SeedBackupChecks();
            return now;
        }

        private void ShowInstallSummary(int success, int failed, int cancelled, List<string> failures, int total)
        {
            string message;
            MessageBoxIcon icon;

            if (failed == 0 && cancelled == 0)
            {
                message = string.Format("Instalação concluída!\n\n{0} de {1} programa(s) instalado(s) com sucesso.", success, total);
                icon = MessageBoxIcon.Information;
            }
            else if (failed == 0)
            {
                message = string.Format("Instalação interrompida.\n\n{0} instalado(s) e {1} cancelado(s).", success, cancelled);
                icon = MessageBoxIcon.Information;
            }
            else
            {
                message = string.Format("Instalação finalizada.\n\n{0} instalado(s), {1} com falha.", success, failed);
                if (cancelled > 0) message += string.Format(", {0} cancelado(s)", cancelled);
                message += ".";
                if (failures.Count > 0)
                {
                    message += "\n\nOcorrências:";
                    int limit = Math.Min(failures.Count, 6);
                    for (int i = 0; i < limit; i++) message += "\n• " + failures[i];
                    if (failures.Count > limit) message += "\n• ...";
                }
                icon = MessageBoxIcon.Warning;
            }

            MessageBox.Show(this, message, "ReInstala", MessageBoxButtons.OK, icon);
        }

        private async void OnBootstrapClick(object sender, EventArgs e)
        {
            if (_busy) return;
            SetBusy(true, "Baixando o Winget...", true);
            ShowState(StateIcon.Download, "Instalando o Winget", "O ReInstala está baixando e instalando o gerenciador de pacotes do Windows. Aguarde...");

            Progress<string> progress = new Progress<string>(delegate(string message)
            {
                if (IsDisposed) return;
                _status.Text = message;
            });

            bool ok = false;
            try
            {
                WingetResult result = await Winget.InstallWingetAsync(progress, CancellationToken.None);
                ok = result.ExitCode == 0 && Winget.FindExe() != null;
                if (!ok)
                {
                    ShowState(StateIcon.Alert, "Instalação automática não concluída",
                        "Não foi possível instalar o Winget automaticamente. Use a Microsoft Store para instalar o 'Instalador de Aplicativo'.");
                }
            }
            catch (Exception)
            {
                ok = false;
            }
            finally
            {
                SetBusy(false, null);
            }

            if (ok)
            {
                await InitializeAsync();
                return;
            }

            ShowWingetMissingState("Não foi possível instalar o Winget automaticamente. Instale o \"Instalador de Aplicativo\" pela Microsoft Store e clique em verificar novamente.");

            DialogResult answer = MessageBox.Show(this,
                "Não foi possível instalar o Winget automaticamente.\n\nDeseja abrir a página do \"Instalador de Aplicativo\" na Microsoft Store para instalar manualmente?",
                "Instalar Winget", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes)
            {
                try
                {
                    Process.Start("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1");
                }
                catch (Exception)
                {
                    try { Process.Start("https://aka.ms/getwinget"); }
                    catch (Exception) { }
                }
            }
        }

        private async void OnStateRetryClick(object sender, EventArgs e)
        {
            if (_busy) return;
            await InitializeAsync();
        }

        private void ShowWingetMissingState(string customMessage)
        {
            string subtitle = customMessage;
            if (subtitle == null)
            {
                subtitle = "O Winget é o gerenciador de pacotes do Windows e é necessário para detectar e reinstalar seus programas. " +
                           "O ReInstala pode instalá-lo para você.";
            }

            ShowState(StateIcon.Alert, "Winget não encontrado", subtitle);
            if (!_busy)
            {
                _state.AddAction("Instalar Winget", RoundedButton.Kind.Primary, OnBootstrapClick);
                _state.AddAction("Verificar novamente", RoundedButton.Kind.Secondary, OnStateRetryClick);
            }
            _state.Visible = true;
        }

        private void ShowRestoreEmptyState()
        {
            ShowState(StateIcon.Download, "Nenhum backup carregado",
                "Escolha o arquivo .json que o ReInstala gerou antes de formatar o computador. Você verá a lista de programas e poderá revisar tudo antes de instalar.");

            if (_wingetReady)
            {
                RoundedButton choose = _state.AddAction("Escolher arquivo...", RoundedButton.Kind.Primary, delegate { OpenBackupFile(); });
                choose.Size = new Size(Theme.Px(180), Theme.Px(42));
                RoundedButton backup = _state.AddAction("Fazer backup agora", RoundedButton.Kind.Ghost, delegate { _modes.SelectedIndex = 0; });
                backup.Size = new Size(Theme.Px(180), Theme.Px(42));
            }
            else
            {
                _state.AddAction("Instalar Winget", RoundedButton.Kind.Primary, OnBootstrapClick);
            }
        }

        private void ShowErrorState(string title, string message)
        {
            ShowState(StateIcon.Alert, title, message);
            _state.AddAction("Tentar novamente", RoundedButton.Kind.Primary, OnStateRetryClick);
        }

        private void ShowState(StateIcon icon, string title, string subtitle)
        {
            if (_state == null) return;
            _state.ClearActions();
            _state.Icon = icon;
            _state.Title = title;
            _state.Subtitle = subtitle;
            _state.Visible = true;
            _state.BringToFront();
            _list.Visible = false;
            if (_scroll != null) _scroll.Visible = false;
            _state.Invalidate();
            _state.PerformLayout();
            if (_settingsView != null && _settingsView.Visible) _settingsView.BringToFront();
        }

        private void HideState()
        {
            if (_state == null) return;
            _state.Visible = false;
            _list.Visible = true;
        }
    }
}
