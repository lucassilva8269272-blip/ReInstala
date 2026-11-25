using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ReInstala
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--selftest", StringComparison.OrdinalIgnoreCase))
            {
                SelfTest.Run(args.Length > 1 ? args[1] : null);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "--backup", StringComparison.OrdinalIgnoreCase))
            {
                Environment.ExitCode = BackupCli.Run(args.Length > 1 ? args[1] : null);
                return;
            }

            AppSettings.Load();
            if (AppSettings.DarkTheme) Theme.ApplyDark();
            else Theme.ApplyLight();
            NativeTheme.SetDark(Theme.Dark);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                CrashLog.Write(e.Exception);
                CrashLog.ShowFatal(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                CrashLog.Write(e.ExceptionObject as Exception);
            };

            string initialFile = null;
            if (args.Length > 0 && args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(args[0]))
            {
                initialFile = args[0];
            }

            Application.Run(new MainForm(initialFile));
        }
    }

    internal static class CrashLog
    {
        public static string LogPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReInstala");
                try
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
                catch (Exception)
                {
                }
                return Path.Combine(dir, "error.log");
            }
        }

        public static void Write(Exception exception)
        {
            if (exception == null) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("======================================================");
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  ReInstala");
                sb.AppendLine(exception.ToString());
                File.AppendAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        public static void ShowFatal(Exception exception)
        {
            string details = exception == null ? "Erro desconhecido." : exception.Message;
            MessageBox.Show(
                "O ReInstala encontrou um erro inesperado.\n\n" + details +
                "\n\nDetalhes técnicos foram salvos em:\n" + LogPath,
                "ReInstala", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
