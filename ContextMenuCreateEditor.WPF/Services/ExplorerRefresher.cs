using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ContextMenuCreateEditor.WPF.Services
{
    public class ExplorerRefresher : IExplorerRefresher
    {
        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        public void RefreshAssociations()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"SHChangeNotify не сработал: {ex.Message}");
            }
        }

        public async Task RestartShellAsync()
        {
            try
            {
                // 1. Жёстко завершаем все explorer.exe процессы (стандартный способ Microsoft).
                //    PostMessage 0x5B4 ненадёжен на системах с AutoRestartShell=0.
                KillAllExplorerProcesses();

                // 2. Ждём, пока процессы фактически исчезнут (макс 5 сек).
                for (int i = 0; i < 50; i++)
                {
                    if (!AnyExplorerRunning())
                        break;
                    await Task.Delay(100);
                }

                await Task.Delay(300);

                // 3. Стартуем explorer.exe — без shell-специальных флагов он станет shell,
                //    т.к. ни одного explorer.exe сейчас не запущено.
                StartExplorerAsShell();

                // 4. Ждём появления Shell_TrayWnd (макс ~7 сек).
                bool ok = false;
                for (int i = 0; i < 70; i++)
                {
                    await Task.Delay(100);
                    if (FindWindow("Shell_TrayWnd", null) != IntPtr.Zero)
                    {
                        ok = true;
                        break;
                    }
                }

                if (!ok)
                {
                    // Резерв: повторный старт через ShellExecute (на случай странных конфигов).
                    AppLogger.Warn("Shell_TrayWnd не появился — пробуем повторный запуск.");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = true
                    });
                }
                else
                {
                    AppLogger.Info("Shell перезапущен.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при перезапуске shell.", ex);
                throw;
            }
        }

        private static void KillAllExplorerProcesses()
        {
            // Используем taskkill — самый надёжный способ снести все explorer.exe сразу.
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("/F");
                psi.ArgumentList.Add("/IM");
                psi.ArgumentList.Add("explorer.exe");

                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"taskkill не сработал, пробуем Process.Kill: {ex.Message}");

                // Fallback: ручное убийство
                foreach (var proc in Process.GetProcessesByName("explorer").ToArray())
                {
                    try { proc.Kill(true); proc.WaitForExit(2000); }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
        }

        private static bool AnyExplorerRunning()
        {
            var procs = Process.GetProcessesByName("explorer");
            try { return procs.Length > 0; }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        private static void StartExplorerAsShell()
        {
            var explorerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");

            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = explorerPath,
                UseShellExecute = false,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            });
        }
    }
}
