using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security;
using System.Text;

namespace ReInstala
{
    [DataContract]
    public class BackupApp
    {
        [DataMember(Name = "nome", Order = 1)]
        public string Name = string.Empty;

        [DataMember(Name = "id", Order = 2)]
        public string Id = string.Empty;

        [DataMember(Name = "versao", Order = 3)]
        public string Version = string.Empty;

        [DataMember(Name = "fonte", Order = 4)]
        public string Source = string.Empty;
    }

    [DataContract]
    public class BackupFile
    {
        [DataMember(Name = "aplicativo", Order = 1)]
        public string App = string.Empty;

        [DataMember(Name = "formato", Order = 2)]
        public int Format;

        [DataMember(Name = "geradoEm", Order = 3)]
        public string CreatedAt = string.Empty;

        [DataMember(Name = "computador", Order = 4)]
        public string Computer = string.Empty;

        [DataMember(Name = "sistema", Order = 5)]
        public string System = string.Empty;

        [DataMember(Name = "winget", Order = 6)]
        public string WingetVersion = string.Empty;

        [DataMember(Name = "programas", Order = 7)]
        public List<BackupApp> Apps = new List<BackupApp>();
    }

    internal static class BackupStore
    {
        public const string ApplicationName = "ReInstala";
        public const int CurrentFormat = 1;

        public static string DownloadsFolder()
        {
            try
            {
                Guid folderGuid = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                IntPtr pointer;
                int hr = SHGetKnownFolderPath(ref folderGuid, 0, IntPtr.Zero, out pointer);
                if (hr == 0 && pointer != IntPtr.Zero)
                {
                    string path = Marshal.PtrToStringUni(pointer);
                    Marshal.FreeCoTaskMem(pointer);
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                }
            }
            catch (Exception)
            {
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string fallback = Path.Combine(profile, "Downloads");
            return fallback;
        }

        public static BackupFile Create(IList<InstalledApp> apps, string wingetVersion)
        {
            BackupFile file = new BackupFile();
            file.App = ApplicationName;
            file.Format = CurrentFormat;
            file.CreatedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            file.Computer = Environment.MachineName;
            file.System = Environment.OSVersion.VersionString;
            file.WingetVersion = wingetVersion ?? string.Empty;

            foreach (InstalledApp app in apps)
            {
                BackupApp item = new BackupApp();
                item.Name = app.Name;
                item.Id = app.Id;
                item.Version = app.Version;
                item.Source = string.IsNullOrEmpty(app.Source) ? "winget" : app.Source;
                file.Apps.Add(item);
            }

            file.Apps.Sort(delegate(BackupApp a, BackupApp b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return file;
        }

        public static string Save(BackupFile file)
        {
            string folder = DownloadsFolder();
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string baseName = "ReInstala-Backup-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            string path = Path.Combine(folder, baseName + ".json");
            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, baseName + "-" + suffix + ".json");
                suffix++;
            }

            File.WriteAllText(path, ToJson(file), new UTF8Encoding(false));
            return path;
        }

        public static BackupFile Load(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            BackupFile file = null;

            try
            {
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BackupFile));
                    file = serializer.ReadObject(stream) as BackupFile;
                }
            }
            catch (Exception)
            {
                throw new InvalidDataException("O arquivo selecionado não é um backup válido do ReInstala.");
            }

            if (file == null) throw new InvalidDataException("O arquivo selecionado está vazio ou não é um backup válido do ReInstala.");
            if (!string.Equals(file.App, ApplicationName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Este arquivo não foi gerado pelo ReInstala.");
            if (file.Format != CurrentFormat)
                throw new InvalidDataException("Este backup foi gerado por uma versão diferente do ReInstala.");
            if (file.Apps == null) file.Apps = new List<BackupApp>();

            List<BackupApp> valid = new List<BackupApp>();
            foreach (BackupApp app in file.Apps)
            {
                if (app == null) continue;
                if (string.IsNullOrEmpty(app.Id)) continue;
                app.Id = app.Id.Trim();
                if (!IsValidPackageId(app.Id))
                {
                    throw new InvalidDataException("O backup contém um identificador de programa inválido: " + app.Id);
                }
                if (string.IsNullOrEmpty(app.Name)) app.Name = app.Id;
                if (app.Source == null) app.Source = string.Empty;
                if (app.Version == null) app.Version = string.Empty;
                valid.Add(app);
            }
            file.Apps = valid;

            if (file.Apps.Count == 0)
                throw new InvalidDataException("O backup não contém nenhum programa para restaurar.");

            return file;
        }

        public static string ToJson(BackupFile file)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"aplicativo\": \"" + Escape(file.App) + "\",");
            sb.AppendLine("  \"formato\": " + file.Format + ",");
            sb.AppendLine("  \"geradoEm\": \"" + Escape(file.CreatedAt) + "\",");
            sb.AppendLine("  \"computador\": \"" + Escape(file.Computer) + "\",");
            sb.AppendLine("  \"sistema\": \"" + Escape(file.System) + "\",");
            sb.AppendLine("  \"winget\": \"" + Escape(file.WingetVersion) + "\",");
            sb.AppendLine("  \"programas\": [");

            for (int i = 0; i < file.Apps.Count; i++)
            {
                BackupApp app = file.Apps[i];
                sb.AppendLine("    {");
                sb.AppendLine("      \"nome\": \"" + Escape(app.Name) + "\",");
                sb.AppendLine("      \"id\": \"" + Escape(app.Id) + "\",");
                sb.AppendLine("      \"versao\": \"" + Escape(app.Version) + "\",");
                sb.AppendLine("      \"fonte\": \"" + Escape(app.Source) + "\"");
                sb.Append("    }");
                if (i < file.Apps.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static bool IsValidPackageId(string id)
        {
            if (id.Length == 0 || id.Length > 220) return false;
            foreach (char c in id)
            {
                bool ok = char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' || c == '+' || c == '~';
                if (!ok) return false;
            }
            return true;
        }

        private static string Escape(string value)        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            StringBuilder sb = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32 || c > 126)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        private static extern int SHGetKnownFolderPath(ref Guid rfid, uint flags, IntPtr token, out IntPtr path);
    }
}
