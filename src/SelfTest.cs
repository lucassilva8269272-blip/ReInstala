using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ReInstala
{
    internal static class SelfTest
    {
        public static void Run(string outputPath)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                sb.AppendLine("wingetPath=" + (Winget.FindExe() ?? "(nao encontrado)"));

                WingetResult version = Winget.RunAsync("--version", 30000, CancellationToken.None).Result;
                sb.AppendLine("versionExit=" + version.ExitCode);
                sb.AppendLine("version=" + Winget.CleanText(version.Output).Trim());

                WingetResult list = Winget.RunAsync(Winget.ListArguments(), 300000, CancellationToken.None).Result;
                sb.AppendLine("listExit=" + list.ExitCode + " timedOut=" + list.TimedOut + " started=" + list.Started);

                List<InstalledApp> apps = Winget.ParseList(list.Output);
                sb.AppendLine("parsed=" + apps.Count);

                List<InstalledApp> restorable = new List<InstalledApp>();
                foreach (InstalledApp app in apps)
                {
                    if (app.Restorable) restorable.Add(app);
                }
                sb.AppendLine("restorable=" + restorable.Count);
                sb.AppendLine("--- primeiros programas ---");
                for (int i = 0; i < Math.Min(20, apps.Count); i++)
                {
                    InstalledApp app = apps[i];
                    sb.AppendLine(string.Format("{0} | {1} | {2} | {3} | restauravel={4}",
                        app.Name, app.Id, app.Version, app.Source, app.Restorable));
                }

                BackupFile file = BackupStore.Create(restorable, "selftest");
                string temp = Path.Combine(Path.GetTempPath(), "reinstala-selftest.json");
                File.WriteAllText(temp, BackupStore.ToJson(file), new UTF8Encoding(false));
                BackupFile loaded = BackupStore.Load(temp);
                sb.AppendLine("backupApps=" + file.Apps.Count);
                sb.AppendLine("roundtripApps=" + loaded.Apps.Count);
                sb.AppendLine("backupPath=" + BackupStore.DownloadsFolder());
                sb.AppendLine("jsonBytes=" + new FileInfo(temp).Length);
                File.Delete(temp);

                sb.AppendLine("RESULT=OK");
            }
            catch (Exception ex)
            {
                sb.AppendLine("RESULT=FAIL");
                sb.AppendLine(ex.ToString());
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(Path.GetTempPath(), "reinstala-selftest.txt");
            }
            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
