using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ReInstala
{
    /// <summary>
    /// Instalador do ReInstala: copia o aplicativo para a pasta do usuário,
    /// cria os atalhos e registra a desinstalação em "Aplicativos e recursos".
    /// Não pede permissão de administrador.
    /// </summary>
    internal sealed class SetupForm : Form
    {
        private readonly CheckBox _desktop;
        private readonly CheckBox _startMenu;
        private readonly CheckBox _launch;
        private readonly Label _heading;
        private readonly Label _description;
        private readonly Label _status;
        private readonly InfoBox _pathBox;
        private readonly ThinProgress _progress;
        private readonly RoundedButton _install;
        private readonly RoundedButton _cancel;
        private readonly PictureBox _logo;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _version;
        private readonly Panel _header;
        private readonly Panel _footer;
        private readonly CardPanel _card;
        private bool _working;
        private bool _done;

        public SetupForm()
        {
            Text = "ReInstala — Instalação";
            BackColor = Theme.Background;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            Font = Theme.Font(9.75f);
            ClientSize = new Size(Theme.Px(620), Theme.Px(468));
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (Exception)
            {
            }

            _header = new Panel();
            _header.BackColor = Theme.Surface;
            _header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Theme.BorderSoft, 1f))
                {
                    e.Graphics.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
                }
            };
            Controls.Add(_header);

            _logo = new PictureBox();
            _logo.SizeMode = PictureBoxSizeMode.Zoom;
            _logo.BackColor = Theme.Surface;
            _logo.Image = LoadLogo();
            _header.Controls.Add(_logo);

            _title = NewLabel("ReInstala", Theme.Bold(17f), Theme.Text, Theme.Surface);
            _subtitle = NewLabel("Instalação para este computador", Theme.Font(9.5f), Theme.Muted, Theme.Surface);
            _version = NewLabel("v" + AppVersion, Theme.Font(8.75f), Theme.Muted, Theme.Surface);
            _version.TextAlign = ContentAlignment.MiddleRight;
            _header.Controls.Add(_title);
            _header.Controls.Add(_subtitle);
            _header.Controls.Add(_version);

            _card = new CardPanel();
            Controls.Add(_card);

            _heading = NewLabel("Instalar o ReInstala", Theme.Bold(12.5f), Theme.Text, Theme.Surface);
            _description = NewLabel(
                "Faça o backup dos seus programas e reinstale tudo com o Winget depois de formatar. " +
                "Não é preciso senha de administrador.",
                Theme.Font(9.25f), Theme.Muted, Theme.Surface);
            _card.Controls.Add(_heading);
            _card.Controls.Add(_description);

            _pathBox = new InfoBox();
            _pathBox.Caption = "INSTALAR EM";
            _pathBox.Value = InstallDirectory;
            _card.Controls.Add(_pathBox);

            _desktop = NewCheck("Criar atalho na área de trabalho", true);
            _startMenu = NewCheck("Criar atalho no menu Iniciar", true);
            _launch = NewCheck("Abrir o ReInstala ao concluir", true);
            _card.Controls.Add(_desktop);
            _card.Controls.Add(_startMenu);
            _card.Controls.Add(_launch);

            _footer = new Panel();
            _footer.BackColor = Theme.Background;
            Controls.Add(_footer);

            _progress = new ThinProgress();
            _progress.Indeterminate = false;
            _progress.Value = 0;
            _progress.Visible = false;
            _footer.Controls.Add(_progress);

            _status = NewLabel(string.Empty, Theme.Font(8.75f), Theme.Muted, Theme.Background);
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _footer.Controls.Add(_status);

            _cancel = new RoundedButton("Cancelar", RoundedButton.Kind.Secondary);
            _cancel.Click += delegate { Close(); };
            _footer.Controls.Add(_cancel);

            _install = new RoundedButton("Instalar", RoundedButton.Kind.Primary);
            _install.Click += OnInstallClick;
            _footer.Controls.Add(_install);

            LayoutWindow();
        }

        internal static string AppVersion
        {
            get
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version == null) return "1.0.0";
                return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }
        }

        internal static string InstallDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\ReInstala");
            }
        }

        internal static string InstalledExe
        {
            get { return Path.Combine(InstallDirectory, "ReInstala.exe"); }
        }

        internal static string UninstallerExe
        {
            get { return Path.Combine(InstallDirectory, "uninstall.exe"); }
        }

        private static Image LoadLogo()
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png");
                if (stream == null) return null;
                using (stream)
                {
                    return Image.FromStream(stream);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Label NewLabel(string text, Font font, Color color, Color back)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = back;
            label.AutoSize = false;
            label.UseMnemonic = false;
            return label;
        }

        private static CheckBox NewCheck(string text, bool value)
        {
            CheckBox check = new CheckBox();
            check.Text = text;
            check.Checked = value;
            check.Font = Theme.Font(9.5f);
            check.ForeColor = Theme.Text;
            check.BackColor = Theme.Surface;
            check.FlatStyle = FlatStyle.Standard;
            check.AutoSize = false;
            check.Cursor = Cursors.Hand;
            return check;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutWindow();
        }

        private void LayoutWindow()
        {
            if (_card == null) return;

            int width = ClientSize.Width;
            int height = ClientSize.Height;

            int pad = Theme.Px(24);
            int headerHeight = Theme.Px(96);
            int footerHeight = Theme.Px(96);

            _header.SetBounds(0, 0, width, headerHeight);
            _logo.SetBounds(pad, Theme.Px(24), Theme.Px(48), Theme.Px(48));
            _title.SetBounds(pad + Theme.Px(62), Theme.Px(23), Theme.Px(320), Theme.Px(30));
            _subtitle.SetBounds(pad + Theme.Px(62), Theme.Px(54), Theme.Px(360), Theme.Px(22));
            _version.SetBounds(width - pad - Theme.Px(160), Theme.Px(30), Theme.Px(160), Theme.Px(22));

            int cardTop = headerHeight + Theme.Px(18);
            _card.SetBounds(pad, cardTop, width - pad * 2, height - cardTop - footerHeight);

            int inner = Theme.Px(24);
            int innerWidth = _card.Width - inner * 2;
            _heading.SetBounds(inner, Theme.Px(20), innerWidth, Theme.Px(28));
            _description.SetBounds(inner, Theme.Px(52), innerWidth, Theme.Px(48));
            _pathBox.SetBounds(inner, Theme.Px(108), innerWidth, Theme.Px(50));
            _desktop.SetBounds(inner, Theme.Px(170), innerWidth, Theme.Px(24));
            _startMenu.SetBounds(inner, Theme.Px(198), innerWidth, Theme.Px(24));
            _launch.SetBounds(inner, Theme.Px(226), innerWidth, Theme.Px(24));

            _footer.SetBounds(0, height - footerHeight, width, footerHeight);
            _install.SetBounds(width - pad - Theme.Px(168), _footer.Height - Theme.Px(24) - Theme.Px(42),
                Theme.Px(168), Theme.Px(42));
            _cancel.SetBounds(_install.Left - Theme.Px(12) - Theme.Px(120),
                _install.Top, Theme.Px(120), Theme.Px(42));
            _status.SetBounds(pad, _install.Top, _cancel.Left - pad - Theme.Px(12), Theme.Px(42));
            _progress.SetBounds(pad, Theme.Px(12), width - pad * 2, Theme.Px(6));
        }

        private void OnInstallClick(object sender, EventArgs e)
        {
            if (_done)
            {
                Close();
                return;
            }
            if (_working) return;

            _working = true;
            SetOptionsEnabled(false);
            _progress.Visible = true;
            _progress.Indeterminate = true;
            _status.Text = "Instalando...";
            _install.Text = "Instalando...";
            _install.Enabled = false;
            _cancel.Enabled = false;

            bool desktop = _desktop.Checked;
            bool startMenu = _startMenu.Checked;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception error = null;
                try
                {
                    Installer.Install(desktop, startMenu);
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        _working = false;
                        _progress.Indeterminate = false;
                        _progress.Visible = false;

                        if (error != null)
                        {
                            _status.Text = string.Empty;
                            _install.Text = "Instalar";
                            _install.Enabled = true;
                            _cancel.Enabled = true;
                            SetOptionsEnabled(true);
                            MessageBox.Show(this,
                                "Não foi possível instalar o ReInstala.\n\n" + error.Message,
                                "ReInstala", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        ShowDone(desktop, startMenu);
                        if (_launch.Checked) Installer.Launch();
                    });
                }
                catch (Exception)
                {
                }
            });
        }

        private void ShowDone(bool desktop, bool startMenu)
        {
            _done = true;
            _heading.Text = "Instalação concluída";
            if (desktop && startMenu)
            {
                _description.Text = "O ReInstala está pronto. Os atalhos foram criados na sua área de trabalho e no menu Iniciar.";
            }
            else if (desktop)
            {
                _description.Text = "O ReInstala está pronto. O atalho foi criado na sua área de trabalho.";
            }
            else if (startMenu)
            {
                _description.Text = "O ReInstala está pronto. Você pode abri-lo pelo menu Iniciar.";
            }
            else
            {
                _description.Text = "O ReInstala está pronto para usar.";
            }
            _pathBox.Value = InstalledExe;
            _pathBox.Caption = "INSTALADO EM";
            _desktop.Visible = false;
            _startMenu.Visible = false;
            _launch.Visible = false;
            _status.Text = "Tudo pronto.";
            _install.Text = "Concluir";
            _install.Enabled = true;
            _cancel.Visible = false;
            _description.Invalidate();
            _card.Invalidate();
        }

        private void SetOptionsEnabled(bool enabled)
        {
            _desktop.Enabled = enabled;
            _startMenu.Enabled = enabled;
            _launch.Enabled = enabled;
        }
    }

    /// <summary>Caixa clara com um rótulo pequeno e um valor, usada para mostrar a pasta de instalação.</summary>
    internal sealed class InfoBox : Control
    {
        public string Caption = string.Empty;
        public string Value = string.Empty;

        public InfoBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.Font(9.25f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRounded(g, rect, Theme.Px(10), Theme.Background);
            Theme.StrokeRounded(g, rect, Theme.Px(10), Theme.BorderSoft, 1f);

            Theme.DrawText(g, Caption, Theme.Bold(7.75f), Theme.Muted,
                new Rectangle(Theme.Px(14), Theme.Px(7), Width - Theme.Px(28), Theme.Px(14)),
                TextFormatFlags.Left | TextFormatFlags.Top);
            Theme.DrawText(g, Value, Font, Theme.Text,
                new Rectangle(Theme.Px(14), Theme.Px(22), Width - Theme.Px(28), Theme.Px(20)),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
        }
    }

    internal static class SetupProgram
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "reinstala-setup.log"), ex.ToString());
                }
                catch (Exception)
                {
                }
                MessageBox.Show("Não foi possível abrir o instalador.\n\n" + ex.Message,
                    "ReInstala", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void Run(string[] args)
        {
            bool uninstall = HasFlag(args, "--uninstall");
            bool quiet = HasFlag(args, "--quiet") || HasFlag(args, "/S");

            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            float dpiScale;
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpiScale = g.DpiX / 96f;
            }
            Theme.Scale = Math.Max(0.9f, Math.Min(2f, dpiScale));
            Theme.FontScale = 1f;
            Theme.ApplyLight();

            if (uninstall)
            {
                Installer.Remove(quiet, !quiet);
                return;
            }

            if (quiet)
            {
                Installer.Install(true, true);
                return;
            }

            Application.Run(new SetupForm());
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    /// <summary>Operações de instalação e remoção, compartilhadas pela janela e pelos modos silenciosos.</summary>
    internal static class Installer
    {
        public static void Install(bool desktopShortcut, bool startMenuShortcut)
        {
            Directory.CreateDirectory(SetupForm.InstallDirectory);

            byte[] payload = ReadEmbeddedExe();
            string temp = SetupForm.InstalledExe + ".new";
            File.WriteAllBytes(temp, payload);
            if (File.Exists(SetupForm.InstalledExe)) File.Delete(SetupForm.InstalledExe);
            File.Move(temp, SetupForm.InstalledExe);

            string self = Application.ExecutablePath;
            if (!string.Equals(self, SetupForm.UninstallerExe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(self, SetupForm.UninstallerExe, true);
            }

            if (desktopShortcut) Shortcuts.CreateDesktop();
            if (startMenuShortcut) Shortcuts.CreateStartMenu();
            WriteUninstallEntry();
        }

        public static void Launch()
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(SetupForm.InstalledExe);
                info.WorkingDirectory = SetupForm.InstallDirectory;
                info.UseShellExecute = true;
                Process.Start(info);
            }
            catch (Exception)
            {
            }
        }

        public static void Remove(bool quiet, bool confirm)
        {
            if (confirm)
            {
                DialogResult answer = MessageBox.Show(
                    "Remover o ReInstala deste computador?\n\n" +
                    "Seus backups em JSON e as preferências do aplicativo serão mantidos.",
                    "Desinstalar ReInstala", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            Shortcuts.DeleteDesktop();
            Shortcuts.DeleteStartMenu();
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ReInstala", false);
            }
            catch (Exception)
            {
            }

            ScheduleSelfDelete();

            if (!quiet)
            {
                MessageBox.Show("O ReInstala foi removido deste computador.",
                    "ReInstala", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void ScheduleSelfDelete()
        {
            string dir = SetupForm.InstallDirectory;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe",
                    "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + dir + "\"");
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                info.WorkingDirectory = Path.GetTempPath();
                Process.Start(info);
            }
            catch (Exception)
            {
            }
            Application.Exit();
        }

        private static byte[] ReadEmbeddedExe()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ReInstala.exe");
            if (stream == null)
            {
                throw new InvalidOperationException("O aplicativo não foi encontrado dentro do instalador.");
            }
            using (stream)
            using (MemoryStream memory = new MemoryStream())
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memory.Write(buffer, 0, read);
                }
                return memory.ToArray();
            }
        }

        private static void WriteUninstallEntry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ReInstala"))
                {
                    if (key == null) return;
                    key.SetValue("DisplayName", "ReInstala");
                    key.SetValue("DisplayVersion", SetupForm.AppVersion);
                    key.SetValue("Publisher", "ReInstala contributors");
                    key.SetValue("DisplayIcon", SetupForm.InstalledExe + ",0");
                    key.SetValue("InstallLocation", SetupForm.InstallDirectory);
                    key.SetValue("UninstallString", "\"" + SetupForm.UninstallerExe + "\" --uninstall");
                    key.SetValue("QuietUninstallString", "\"" + SetupForm.UninstallerExe + "\" --uninstall --quiet");
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("EstimatedSize", 256, RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Criação e remoção dos atalhos, sem exigir referências COM no projeto.</summary>
    internal static class Shortcuts
    {
        private const string Description = "Backup e restauração de programas com Winget";

        private static string DesktopPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "ReInstala.lnk");
            }
        }

        private static string StartMenuPath
        {
            get
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                return Path.Combine(programs, "ReInstala.lnk");
            }
        }

        public static void CreateDesktop()
        {
            Create(DesktopPath);
        }

        public static void CreateStartMenu()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StartMenuPath));
            }
            catch (Exception)
            {
            }
            Create(StartMenuPath);
        }

        public static void DeleteDesktop()
        {
            TryDelete(DesktopPath);
        }

        public static void DeleteStartMenu()
        {
            TryDelete(StartMenuPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        private static void Create(string linkPath)
        {
            string target = SetupForm.InstalledExe;
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null,
                    shell, new object[] { linkPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut,
                    new object[] { SetupForm.InstallDirectory });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut,
                    new object[] { target + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut,
                    new object[] { Description });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch (Exception)
            {
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
