using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ReInstala
{
    internal class WingetResult
    {
        public int ExitCode;
        public string Output = string.Empty;
        public string Error = string.Empty;
        public bool TimedOut;
        public bool Started = true;

        public string AllText
        {
            get { return (Output ?? string.Empty) + "\n" + (Error ?? string.Empty); }
        }
    }

    internal class InstalledApp
    {
        public string Name = string.Empty;
        public string Id = string.Empty;
        public string Version = string.Empty;
        public string Source = string.Empty;
        public bool Restorable;
        public bool Framework;
    }

    internal static class Winget
    {
        private static readonly string[] FrameworkPrefixes = new string[]
        {
            "Microsoft.VCLibs",
            "Microsoft.UI.Xaml",
            "Microsoft.WindowsAppRuntime",
            "Microsoft.WinAppRuntime",
            "WinAppRuntime.",
            "Microsoft.DotNet.Native.Runtime",
            "Microsoft.AppInstaller"
        };

        private static string _cachedExe;
        private static DateTime _cachedAt = DateTime.MinValue;

        public static string FindExe()
        {
            if (_cachedExe != null && File.Exists(_cachedExe) &&
                (DateTime.UtcNow - _cachedAt).TotalSeconds < 30)
            {
                return _cachedExe;
            }

            string found = FindExeCore();
            _cachedExe = found;
            _cachedAt = DateTime.UtcNow;
            return found;
        }

        /// <summary>
        /// Caminho do Winget que funciona também em um processo elevado
        /// (o alias de %LOCALAPPDATA% pode não existir para outro administrador).
        /// </summary>
        public static string FindElevatedExe()
        {
            try
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string package = Path.Combine(pf, @"WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe");
                if (File.Exists(package)) return package;
            }
            catch (Exception)
            {
            }
            return FindExe();
        }

        private static string FindExeCore()
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string alias = Path.Combine(local, @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(alias)) return alias;

                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string package = Path.Combine(pf, @"WindowsApps\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\winget.exe");
                if (File.Exists(package)) return package;

                string path = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(path))
                {
                    string[] parts = path.Split(';');
                    foreach (string raw in parts)
                    {
                        string dir = raw.Trim().Trim('"');
                        if (dir.Length == 0) continue;
                        try
                        {
                            string candidate = Path.Combine(dir, "winget.exe");
                            if (File.Exists(candidate)) return candidate;
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        /// <summary>
        /// Argumentos da listagem. Os acordos de fonte só são aceitos sozinhos
        /// quando a opção "Aceitar automaticamente os termos" está ligada.
        /// </summary>
        public static string ListArguments()
        {
            return AppSettings.AcceptAgreements
                ? "list --accept-source-agreements --disable-interactivity"
                : "list --disable-interactivity";
        }

        /// <summary>Acordos de licença e de fonte usados na instalação.</summary>
        public static string AgreementArguments()
        {
            return AppSettings.AcceptAgreements
                ? " --accept-package-agreements --accept-source-agreements"
                : string.Empty;
        }

        public static Task<WingetResult> RunAsync(string arguments, int timeoutMs, CancellationToken token)        {
            string exe = FindExe();
            if (exe == null)
            {
                WingetResult missing = new WingetResult();
                missing.Started = false;
                missing.ExitCode = -1;
                missing.Error = "Winget não está instalado.";
                return Task.FromResult<WingetResult>(missing);
            }
            return Task.Run(delegate { return RunProcess(exe, arguments, timeoutMs, token); });
        }

        public static Task<WingetResult> InstallAsync(string id, string source, bool silent, CancellationToken token)
        {
            StringBuilder args = new StringBuilder();
            args.Append("install --id \"").Append(id).Append("\" --exact");
            args.Append(AgreementArguments());
            args.Append(" --disable-interactivity");
            if (!string.IsNullOrEmpty(source))
            {
                if (string.Equals(source, "winget", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(source, "msstore", StringComparison.OrdinalIgnoreCase))
                {
                    args.Append(" --source ").Append(source);
                }
            }
            if (silent) args.Append(" --silent");
            return RunAsync(args.ToString(), 45 * 60 * 1000, token);
        }

        public static async Task<WingetResult> InstallWingetAsync(IProgress<string> status, CancellationToken token)
        {
            WingetResult result = new WingetResult();
            string temp = Path.Combine(Path.GetTempPath(), "ReInstala-winget.msixbundle");

            try
            {
                if (status != null) status.Report("Baixando o Winget...");
                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "ReInstala/1.0");
                    await client.DownloadFileTaskAsync(new Uri("https://aka.ms/getwinget"), temp);
                }

                if (status != null) status.Report("Instalando o Winget...");
                string script =
                    "$ErrorActionPreference='Stop'; " +
                    "Add-AppxPackage -Path '" + temp.Replace("'", "''") + "' -ForceApplicationShutdown; " +
                    "Start-Sleep -Seconds 2";
                result = await RunPowerShellAsync(script, 10 * 60 * 1000, token);
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (Exception) { }
            }

            if (token.IsCancellationRequested) return result;
            if (result.ExitCode != 0) return result;

            if (status != null) status.Report("Finalizando a instalação...");
            for (int i = 0; i < 20; i++)
            {
                if (FindExe() != null)
                {
                    WingetResult version = await RunAsync("--version", 15000, token);
                    if (version.ExitCode == 0)
                    {
                        result.ExitCode = 0;
                        return result;
                    }
                }
                await Task.Delay(800);
            }

            result.ExitCode = 1;
            result.Error = "O Winget foi instalado, mas ainda não respondeu.";
            return result;
        }

        private static async Task<WingetResult> RunPowerShellAsync(string script, int timeoutMs, CancellationToken token)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string ps = Path.Combine(windows, @"WindowsPowerShell\v1.0\powershell.exe");
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string args = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded;
            return await Task.Run(delegate { return RunProcess(ps, args, timeoutMs, token); });
        }

        private static WingetResult RunProcess(string exe, string arguments, int timeoutMs, CancellationToken token)
        {
            WingetResult result = new WingetResult();
            StringBuilder stdout = new StringBuilder();
            StringBuilder stderr = new StringBuilder();

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = arguments;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WindowStyle = ProcessWindowStyle.Hidden;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (stdout) stdout.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (stderr) stderr.AppendLine(e.Data);
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    result.Started = false;
                    result.ExitCode = -1;
                    result.Error = ex.Message;
                    return result;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited;
                using (token.Register(delegate
                {
                    try
                    {
                        if (!process.HasExited) process.Kill();
                    }
                    catch (Exception)
                    {
                    }
                }))
                {
                    exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        result.TimedOut = true;
                        try { process.Kill(); }
                        catch (Exception) { }
                    }
                    try { process.WaitForExit(5000); }
                    catch (Exception) { }
                }

                try { result.ExitCode = process.ExitCode; }
                catch (Exception) { result.ExitCode = -1; }

                lock (stdout) result.Output = stdout.ToString();
                lock (stderr) result.Error = stderr.ToString();
            }

            return result;
        }

        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string clean = Regex.Replace(text, "\u001B\\[[0-9;?]*[A-Za-z]", string.Empty);
            clean = Regex.Replace(clean, "[\u2580-\u259F\u2800-\u28FF]", string.Empty);
            clean = clean.Replace('\r', '\n');
            return clean;
        }

        public static List<InstalledApp> ParseList(string raw)
        {
            List<InstalledApp> apps = new List<InstalledApp>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string text = CleanText(raw);
            string[] lines = text.Split('\n');

            int header = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Name", StringComparison.Ordinal) &&
                    line.IndexOf(" Id", StringComparison.Ordinal) > 0 &&
                    line.IndexOf("Version", StringComparison.Ordinal) > 0 &&
                    line.IndexOf("Source", StringComparison.Ordinal) > 0)
                {
                    header = i;
                    break;
                }
            }

            int idCol = -1, versionCol = -1, availableCol = -1, sourceCol = -1;
            if (header >= 0)
            {
                string h = lines[header];
                idCol = h.IndexOf("Id", StringComparison.Ordinal);
                versionCol = h.IndexOf("Version", StringComparison.Ordinal);
                availableCol = h.IndexOf("Available", StringComparison.Ordinal);
                sourceCol = h.IndexOf("Source", StringComparison.Ordinal);
                if (idCol < 0 || versionCol < 0 || sourceCol < 0)
                {
                    header = -1;
                }
            }

            int start = header >= 0 ? header + 2 : 0;
            for (int i = start; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) continue;

                string name, id, version, source;

                if (header >= 0)
                {
                    if (line.Length <= idCol) continue;
                    name = Slice(line, 0, idCol).Trim();
                    id = Slice(line, idCol, versionCol).Trim();
                    version = Slice(line, versionCol, availableCol).Trim();
                    source = Slice(line, sourceCol, int.MaxValue).Trim();
                }
                else
                {
                    Match m = Regex.Match(line,
                        @"^(?<n>.+?)\s{2,}(?<id>\S+)\s{2,}(?<v>\S*)\s{2,}(?<a>\S*)\s{2,}(?<s>\S+)\s*$");
                    if (!m.Success) continue;
                    name = m.Groups["n"].Value.Trim();
                    id = m.Groups["id"].Value;
                    version = m.Groups["v"].Value;
                    source = m.Groups["s"].Value;
                }

                if (name.Length == 0 || id.Length == 0) continue;
                if (name.StartsWith("---", StringComparison.Ordinal)) continue;
                if (name.StartsWith("Name", StringComparison.Ordinal)) continue;
                if (id.IndexOf(' ') >= 0 && !id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase) &&
                    !id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(id)) continue;

                InstalledApp app = new InstalledApp();
                app.Name = name;
                app.Id = id;
                app.Version = version;
                app.Source = source;
                app.Restorable = IsRestorable(id, source);
                app.Framework = IsFramework(id);
                apps.Add(app);
            }

            apps.Sort(delegate(InstalledApp a, InstalledApp b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });
            return apps;
        }

        private static string Slice(string text, int start, int end)
        {
            if (start >= text.Length) return string.Empty;
            if (end > text.Length) end = text.Length;
            if (end <= start) return string.Empty;
            return text.Substring(start, end - start);
        }

        private static bool IsRestorable(string id, string source)
        {
            if (id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.StartsWith("ARP\\", StringComparison.OrdinalIgnoreCase)) return false;
            if (id.IndexOf(' ') >= 0) return false;
            if (string.Equals(source, "winget", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(source, "msstore", StringComparison.OrdinalIgnoreCase)) return true;
            return id.IndexOf('.') > 0;
        }

        private static bool IsFramework(string id)
        {
            foreach (string prefix in FrameworkPrefixes)
            {
                if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static bool WasAlreadyInstalled(WingetResult result)
        {
            if (result == null) return false;
            string text = CleanText(result.AllText).ToLowerInvariant();
            return text.Contains("already installed") ||
                   text.Contains("já está instalad") ||
                   text.Contains("já instalad") ||
                   text.Contains("found an existing package already installed");
        }

        public static bool WasCancelled(WingetResult result)
        {
            if (result == null) return false;
            string text = CleanText(result.AllText).ToLowerInvariant();
            return text.Contains("0x8a15002e") || text.Contains("cancel");
        }

        public static bool NoPackageFound(WingetResult result)
        {
            if (result == null) return false;
            string text = CleanText(result.AllText).ToLowerInvariant();
            return text.Contains("no package found") ||
                   text.Contains("nenhum pacote encontrado") ||
                   text.Contains("não foi possível encontrar um pacote");
        }

        public static string FailureMessage(WingetResult result)
        {
            if (result == null) return "Falha desconhecida.";
            if (!result.Started) return result.Error;
            if (result.TimedOut) return "Tempo limite excedido.";
            if (NoPackageFound(result)) return "Pacote não encontrado no Winget.";

            string text = CleanText(result.AllText);
            string[] lines = text.Split('\n');
            List<string> useful = new List<string>();
            for (int i = lines.Length - 1; i >= 0 && useful.Count < 3; i--)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("Nome", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Name", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.IndexOf("██", StringComparison.Ordinal) >= 0) continue;
                if (line.IndexOf("---", StringComparison.Ordinal) >= 0) continue;
                if (line.StartsWith("Id ", StringComparison.Ordinal)) continue;
                if (line.Length > 220) line = line.Substring(0, 220) + "...";
                useful.Insert(0, line);
            }
            if (useful.Count == 0) return "Código de saída 0x" + result.ExitCode.ToString("X8") + ".";
            return string.Join(" ", useful.ToArray());
        }
    }
}
