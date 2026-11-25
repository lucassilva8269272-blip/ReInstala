using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReInstala
{
    /// <summary>
    /// Guarda a última lista de programas detectados para que a janela apareça
    /// instantaneamente na próxima abertura, enquanto o Winget é consultado de novo.
    /// </summary>
    internal static class WingetCache
    {
        private const string Header = "1";

        public static void Save(List<InstalledApp> apps)
        {
            if (apps == null || apps.Count == 0) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Header);
                sb.AppendLine(DateTime.Now.Ticks.ToString());
                foreach (InstalledApp app in apps)
                {
                    sb.Append(Clean(app.Name)).Append('\t');
                    sb.Append(Clean(app.Id)).Append('\t');
                    sb.Append(Clean(app.Version)).Append('\t');
                    sb.Append(Clean(app.Source)).Append('\t');
                    sb.Append(app.Restorable ? "1" : "0").Append('\t');
                    sb.Append(app.Framework ? "1" : "0");
                    sb.AppendLine();
                }

                string path = AppSettings.InstalledCachePath;
                string folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        public static List<InstalledApp> Load()
        {
            List<InstalledApp> apps = new List<InstalledApp>();
            try
            {
                string path = AppSettings.InstalledCachePath;
                if (!File.Exists(path)) return apps;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length < 3) return apps;
                if (!string.Equals(lines[0], Header, StringComparison.Ordinal)) return apps;

                long ticks;
                if (!long.TryParse(lines[1], out ticks)) return apps;
                TimeSpan age = TimeSpan.FromTicks(DateTime.Now.Ticks - ticks);
                if (age < TimeSpan.Zero || age > TimeSpan.FromHours(24)) return apps;

                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 2; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Length == 0) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 6) continue;
                    if (parts[1].Length == 0) continue;
                    if (!seen.Add(parts[1])) continue;

                    InstalledApp app = new InstalledApp();
                    app.Name = parts[0];
                    app.Id = parts[1];
                    app.Version = parts[2];
                    app.Source = parts[3];
                    app.Restorable = parts[4] == "1";
                    app.Framework = parts[5] == "1";
                    apps.Add(app);
                }

                apps.Sort(delegate(InstalledApp a, InstalledApp b)
                {
                    return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                });
            }
            catch (Exception)
            {
                apps.Clear();
            }
            return apps;
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        /// <summary>
        /// Guarda a versão do Winget já detectada para não iniciar outro processo
        /// a cada abertura do app.
        /// </summary>
        public static void SaveVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return;
            try
            {
                string path = AppSettings.WingetVersionPath;
                string folder = Path.GetDirectoryName(path);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(path, version.Trim(), new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        public static string LoadVersion()
        {
            try
            {
                string path = AppSettings.WingetVersionPath;
                if (!File.Exists(path)) return string.Empty;
                string text = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (text.Length > 24) text = text.Substring(0, 24);
                return text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Diz se a lista salva foi atualizada há menos de X minutos.
        /// </summary>
        public static bool IsFresh(int minutes)
        {
            try
            {
                string path = AppSettings.InstalledCachePath;
                if (!File.Exists(path)) return false;

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length < 3) return false;
                if (!string.Equals(lines[0], Header, StringComparison.Ordinal)) return false;

                long ticks;
                if (!long.TryParse(lines[1], out ticks)) return false;
                TimeSpan age = TimeSpan.FromTicks(DateTime.Now.Ticks - ticks);
                return age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(minutes);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
