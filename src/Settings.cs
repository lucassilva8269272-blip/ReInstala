using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ReInstala
{
    internal static class AppSettings
    {
        public static bool DarkTheme;

        /// <summary>Começa a instalar sem a pergunta de confirmação do ReInstala.</summary>
        public static bool SkipInstallConfirm = true;

        /// <summary>Pede a permissão de administrador uma única vez, mesmo com um só programa.</summary>
        public static bool SingleElevation = true;

        /// <summary>Aceita sozinho os acordos de licença e de fonte do Winget.</summary>
        public static bool AcceptAgreements = true;

        private static string Folder
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReInstala");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "settings.json"); }
        }

        public static string InstalledCachePath
        {
            get { return Path.Combine(Folder, "cache-instalados.txt"); }
        }

        public static string WingetVersionPath
        {
            get { return Path.Combine(Folder, "winget-versao.txt"); }
        }

        public static void Load()
        {
            DarkTheme = false;
            SkipInstallConfirm = true;
            SingleElevation = true;
            AcceptAgreements = true;

            try
            {
                if (!File.Exists(FilePath)) return;
                string text = File.ReadAllText(FilePath, Encoding.UTF8);

                Match match = Regex.Match(text, "\"tema\"\\s*:\\s*\"(?<v>[^\"]*)\"");
                if (match.Success)
                {
                    DarkTheme = string.Equals(match.Groups["v"].Value, "escuro", StringComparison.OrdinalIgnoreCase);
                }

                SkipInstallConfirm = ReadBool(text, "instalarSemConfirmar", true);
                SingleElevation = ReadBool(text, "permissaoUnica", true);
                AcceptAgreements = ReadBool(text, "aceitarTermos", true);
            }
            catch (Exception)
            {
            }
        }

        private static bool ReadBool(string text, string key, bool fallback)
        {
            Match match = Regex.Match(text, "\"" + key + "\"\\s*:\\s*(?<v>true|false)", RegexOptions.IgnoreCase);
            if (!match.Success) return fallback;
            return string.Equals(match.Groups["v"].Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static string Json(bool value)
        {
            return value ? "true" : "false";
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
                string text = "{\n" +
                              "  \"tema\": \"" + (DarkTheme ? "escuro" : "claro") + "\",\n" +
                              "  \"instalarSemConfirmar\": " + Json(SkipInstallConfirm) + ",\n" +
                              "  \"permissaoUnica\": " + Json(SingleElevation) + ",\n" +
                              "  \"aceitarTermos\": " + Json(AcceptAgreements) + "\n" +
                              "}\n";
                File.WriteAllText(FilePath, text, new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }
    }
}
