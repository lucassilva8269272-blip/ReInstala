using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ReInstala
{
    internal static class BackupCli
    {
        public static int Run(string outputPath)
        {
            try
            {
                WingetResult version = Winget.RunAsync("--version", 30000, CancellationToken.None).Result;
                WingetResult list = Winget.RunAsync(Winget.ListArguments(), 300000, CancellationToken.None).Result;
                if (!list.Started) return 2;

                List<InstalledApp> apps = Winget.ParseList(list.Output);
                List<InstalledApp> selected = new List<InstalledApp>();
                foreach (InstalledApp app in apps)
                {
                    if (app.Restorable && !app.Framework) selected.Add(app);
                }
                if (selected.Count == 0) return 3;

                string versionText = string.Empty;
                string[] lines = Winget.CleanText(version.Output).Split('\n');
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    versionText = trimmed.TrimStart('v', 'V');
                    break;
                }

                BackupFile file = BackupStore.Create(selected, versionText);
                if (string.IsNullOrEmpty(outputPath))
                {
                    BackupStore.Save(file);
                }
                else
                {
                    string folder = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    File.WriteAllText(outputPath, BackupStore.ToJson(file), new System.Text.UTF8Encoding(false));
                }
                return 0;
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                return 1;
            }
        }
    }
}
