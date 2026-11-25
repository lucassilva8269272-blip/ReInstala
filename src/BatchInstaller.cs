using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReInstala
{
    internal sealed class BatchJob
    {
        public string Id = string.Empty;
        public string Source = string.Empty;
    }

    /// <summary>Evento enviado pelo instalador em lote. Kind: S, I, R, C ou D.</summary>
    internal sealed class BatchEvent
    {
        public string Kind = string.Empty;
        public int Index = -1;
        public int ExitCode;
        public bool AlreadyInstalled;
        public string Message = string.Empty;
    }

    internal sealed class BatchOutcome
    {
        public bool UacCancelled;
        public bool Started;
        public bool Finished;
        public string Error = string.Empty;
        public readonly List<BatchEvent> Events = new List<BatchEvent>();
    }

    /// <summary>
    /// Executa várias instalações do Winget dentro de um único processo elevado,
    /// para que o Windows peça permissão de administrador uma única vez.
    /// O progresso é comunicado por um arquivo de log temporário.
    /// </summary>
    internal static class BatchInstaller
    {
        public static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static Task<BatchOutcome> RunAsync(IList<BatchJob> jobs, IProgress<BatchEvent> progress, CancellationToken token)
        {
            return RunAsync(jobs, progress, token, true, null);
        }

        internal static Task<BatchOutcome> RunAsync(IList<BatchJob> jobs, IProgress<BatchEvent> progress,
            CancellationToken token, bool elevate, string wingetOverride)
        {
            return Task.Run(delegate
            {
                return RunCoreAsync(jobs, progress, token, elevate, wingetOverride);
            });
        }

        private static async Task<BatchOutcome> RunCoreAsync(IList<BatchJob> jobs, IProgress<BatchEvent> progress,
            CancellationToken token, bool elevate, string wingetOverride)
        {
            BatchOutcome outcome = new BatchOutcome();
            if (jobs == null || jobs.Count == 0)
            {
                outcome.Error = "Nenhum programa para instalar.";
                return outcome;
            }

            string winget = string.IsNullOrEmpty(wingetOverride) ? Winget.FindElevatedExe() : wingetOverride;
            if (string.IsNullOrEmpty(winget) || !File.Exists(winget))
            {
                outcome.Error = "Winget não está instalado.";
                return outcome;
            }

            string folder = Path.Combine(Path.GetTempPath(), "ReInstala");
            string stamp = Guid.NewGuid().ToString("N");
            string logPath = Path.Combine(folder, "batch-" + stamp + ".log");
            string cancelPath = Path.Combine(folder, "batch-" + stamp + ".cancel");
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            }
            catch (Exception)
            {
            }

            try
            {
                string script = BuildScript(jobs, winget, logPath, cancelPath);
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string ps = Path.Combine(windows, @"WindowsPowerShell\v1.0\powershell.exe");

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ps;
                psi.Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded;
                psi.UseShellExecute = elevate;
                if (elevate) psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                if (!elevate)
                {
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    psi.StandardErrorEncoding = Encoding.UTF8;
                }

                Process process;
                try
                {
                    process = Process.Start(psi);
                    outcome.Started = true;
                }
                catch (Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223) outcome.UacCancelled = true;
                    else outcome.Error = ex.Message;
                    return outcome;
                }
                catch (Exception ex)
                {
                    outcome.Error = ex.Message;
                    return outcome;
                }

                if (process == null)
                {
                    outcome.Error = "Não foi possível iniciar o instalador.";
                    return outcome;
                }

                using (process)
                {
                    if (!elevate)
                    {
                        try { process.BeginOutputReadLine(); }
                        catch (Exception) { }
                        try { process.BeginErrorReadLine(); }
                        catch (Exception) { }
                    }

                    int processed = 0;
                    DateTime start = DateTime.UtcNow;
                    DateTime lastActivity = DateTime.UtcNow;
                    int lastCount = 0;

                    while (true)
                    {
                        if (token.IsCancellationRequested) TryCreate(cancelPath);

                        List<string> lines = ReadCompleteLines(logPath);
                        if (lines.Count > lastCount)
                        {
                            lastCount = lines.Count;
                            lastActivity = DateTime.UtcNow;
                        }

                        for (; processed < lines.Count; processed++)
                        {
                            BatchEvent ev = ParseLine(lines[processed]);
                            if (ev == null) continue;
                            outcome.Events.Add(ev);
                            if (ev.Kind == "D" || ev.Kind == "C") outcome.Finished = true;
                            if (progress != null) progress.Report(ev);
                        }

                        if (outcome.Finished) break;

                        bool exited = false;
                        try { exited = process.HasExited; }
                        catch (Exception) { }

                        if (exited)
                        {
                            // Dá um instante para o arquivo de log ser liberado e lê o restante.
                            await Task.Delay(400).ConfigureAwait(false);
                            lines = ReadCompleteLines(logPath);
                            for (; processed < lines.Count; processed++)
                            {
                                BatchEvent ev = ParseLine(lines[processed]);
                                if (ev == null) continue;
                                outcome.Events.Add(ev);
                                if (progress != null) progress.Report(ev);
                            }
                            break;
                        }

                        if ((DateTime.UtcNow - lastActivity).TotalMinutes > 15 ||
                            (DateTime.UtcNow - start).TotalHours > 4)
                        {
                            outcome.Error = "A instalação demorou mais que o esperado.";
                            TryCreate(cancelPath);
                            break;
                        }

                        await Task.Delay(350).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                try { if (File.Exists(logPath)) File.Delete(logPath); }
                catch (Exception) { }
                try { if (File.Exists(cancelPath)) File.Delete(cancelPath); }
                catch (Exception) { }
            }

            return outcome;
        }

        internal static string BuildScript(IList<BatchJob> jobs, string winget, string logPath, string cancelPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            sb.AppendLine("$log = '" + Ps(logPath) + "'");
            sb.AppendLine("$cancel = '" + Ps(cancelPath) + "'");
            sb.AppendLine("$winget = '" + Ps(winget) + "'");
            sb.AppendLine("$items = @(");
            for (int i = 0; i < jobs.Count; i++)
            {
                sb.Append("  @{ id = '").Append(Ps(jobs[i].Id));
                sb.Append("'; src = '").Append(Ps(jobs[i].Source));
                sb.Append("' }");
                if (i < jobs.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine(")");
            sb.AppendLine("function WriteLog([string]$text) {");
            sb.AppendLine("  try { [System.IO.File]::AppendAllText($log, $text + [Environment]::NewLine, (New-Object System.Text.UTF8Encoding($false))) } catch { }");
            sb.AppendLine("}");
            sb.AppendLine("WriteLog ('S|' + $items.Count)");
            sb.AppendLine("$i = 0");
            sb.AppendLine("foreach ($item in $items) {");
            sb.AppendLine("  if (Test-Path -LiteralPath $cancel) { WriteLog 'C|'; break }");
            sb.AppendLine("  WriteLog ('I|' + $i)");
            string agreements = AppSettings.AcceptAgreements
                ? ",'--accept-package-agreements','--accept-source-agreements'"
                : string.Empty;
            sb.AppendLine("  $base = @('install','--id',$item.id,'--exact','--disable-interactivity'" + agreements + ")");
            sb.AppendLine("  if ($item.src -eq 'winget' -or $item.src -eq 'msstore') { $base += @('--source',$item.src) }");
            sb.AppendLine("  $out = (& $winget @base '--silent' 2>&1 | Out-String)");
            sb.AppendLine("  $code = $LASTEXITCODE");
            sb.AppendLine("  if ($code -ne 0 -and -not (Test-Path -LiteralPath $cancel)) {");
            sb.AppendLine("    $out = (& $winget @base 2>&1 | Out-String)");
            sb.AppendLine("    $code = $LASTEXITCODE");
            sb.AppendLine("  }");
            sb.AppendLine("  $already = 0");
            sb.AppendLine("  if ($out -match '(?i)already installed|já está instalad|já instalad|found an existing package') { $already = 1 }");
            sb.AppendLine("  $msg = ''");
            sb.AppendLine("  $lines = @($out -split \"`r?`n\" | Where-Object { $_.Trim().Length -gt 0 })");
            sb.AppendLine("  if ($lines.Count -gt 0) { $msg = ($lines[$lines.Count - 1]).Trim() }");
            sb.AppendLine("  $msg = $msg -replace '\\|','/'");
            sb.AppendLine("  if ($msg.Length -gt 200) { $msg = $msg.Substring(0,200) }");
            sb.AppendLine("  WriteLog ('R|' + $i + '|' + $code + '|' + $already + '|' + $msg)");
            sb.AppendLine("  $i = $i + 1");
            sb.AppendLine("}");
            sb.AppendLine("if (-not (Test-Path -LiteralPath $cancel)) { WriteLog 'D|' }");
            return sb.ToString();
        }

        private static string Ps(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private static void TryCreate(string path)
        {
            try
            {
                if (!File.Exists(path)) File.WriteAllText(path, "1", new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        private static List<string> ReadCompleteLines(string path)
        {
            List<string> lines = new List<string>();
            try
            {
                if (!File.Exists(path)) return lines;
                string text;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }

                if (text.Length == 0) return lines;
                string[] parts = text.Split('\n');
                int count = parts.Length;
                if (text[text.Length - 1] != '\n') count--; // última linha pode estar incompleta
                for (int i = 0; i < count; i++)
                {
                    string line = parts[i].TrimEnd('\r');
                    if (line.Length > 0) lines.Add(line);
                }
            }
            catch (Exception)
            {
            }
            return lines;
        }

        private static BatchEvent ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] parts = line.Split('|');
            if (parts.Length < 2) return null;

            BatchEvent ev = new BatchEvent();
            ev.Kind = parts[0];

            if (ev.Kind == "I" || ev.Kind == "R")
            {
                int index;
                if (!int.TryParse(parts[1], out index) || index < 0) return null;
                ev.Index = index;
            }

            if (ev.Kind == "R")
            {
                if (parts.Length >= 3)
                {
                    int code;
                    if (int.TryParse(parts[2], out code)) ev.ExitCode = code;
                }
                if (parts.Length >= 4) ev.AlreadyInstalled = parts[3] == "1";
                if (parts.Length >= 5) ev.Message = parts[4];
            }

            return ev;
        }
    }
}
