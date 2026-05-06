using System;
using System.Diagnostics;
using System.IO;

namespace ContextMenuCreateEditor.WPF.Services
{
    public class RegistryBackupService : IRegistryBackupService
    {
        public string BackupsFolder { get; }

        public RegistryBackupService()
        {
            BackupsFolder = Path.Combine(AppLogger.AppDataRoot, "Backups");
            Directory.CreateDirectory(BackupsFolder);
        }

        public string CreateBackup()
        {
            var fileName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.reg";
            var fullPath = Path.Combine(BackupsFolder, fileName);

            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            psi.ArgumentList.Add("export");
            psi.ArgumentList.Add(@"HKCU\Software\Classes");
            psi.ArgumentList.Add(fullPath);
            psi.ArgumentList.Add("/y");

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Не удалось запустить reg.exe");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var err = process.StandardError.ReadToEnd();
                throw new InvalidOperationException($"reg.exe export завершился с кодом {process.ExitCode}: {err}");
            }

            return fullPath;
        }
    }
}
