using ContextMenuCreateEditor.WPF.UserControls;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace ContextMenuCreateEditor.WPF
{
    public partial class MainWindow : FluentWindow
    {
        public static MainWindow Instance { get; private set; }
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            ApplicationThemeManager.Apply(this);
            Loaded += MainWindow_Loaded;

            // Проверяем, запущено ли приложение с аргументами для реестра
            HandleCommandLineArgs();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleTb.Text?.Trim();
            var format = FormatTb.Text?.Trim();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(format))
            {
                MessageBox.Show("Пожалуйста, заполните поля 'Название' и 'Формат'.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!format.StartsWith("."))
            {
                format = "." + format;
            }

            try
            {
                if (IsRunningAsAdmin())
                {
                    // Права есть, выполняем напрямую
                    ContextMenuCreator.AddToNewMenu(title, format);
                    Update();
                }
                else
                {
                    // Запускаем отдельный процесс с правами администратора и ждем завершения
                    var process = RunElevatedProcess(title, format);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            Update(); // Обновляем UI после успешного изменения
                        }
                        else
                        {
                            MessageBox.Show("Изменение реестра в elevated процессе завершилось с ошибкой.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении элемента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Update()
        {
            try
            {
                var items = ContextMenuReader.GetRegisteredNewItems();
                PreviewPanel.Children.Clear();

                foreach (var item in items)
                {
                    var previewContainer = new PreviewItem(item.DisplayName, item.Extension);
                    PreviewPanel.Children.Add(previewContainer);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении списка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExplorerRestart(object sender, RoutedEventArgs e)
        {
            try
            {
                // Пробуем мягкий перезапуск через WinAPI (SHChangeNotify)
                try
                {
                    SHChangeNotify(0x08000000 /* SHCNE_ASSOCCHANGED */, 0x0000 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);
                }
                catch (Exception notifyEx)
                {
                    MessageBox.Show($"SHChangeNotify не сработал: {notifyEx.Message}. Выполняем полный перезапуск.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // Если SHChangeNotify не достаточно (для меню "Создать" часто нужен полный рестарт), завершаем shell
                var processes = Process.GetProcessesByName("explorer");
                var tasks = new List<Task>();

                foreach (var process in processes)
                {
                    // Проверяем, является ли процесс shell (Desktop)
                    if (IsShellProcess(process))
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            process.Kill();
                            process.WaitForExit();
                        }));
                    }
                }

                await Task.WhenAll(tasks);

                // Запускаем explorer без UI
                var processInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden // Пытаемся минимизировать UI
                };
                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при перезапуске проводника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Проверяет, является ли процесс shell (Desktop).
        /// </summary>
        private static bool IsShellProcess(Process process)
        {
            try
            {
                return !string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowHandle != IntPtr.Zero;
            }
            catch
            {
                return false; // Если не можем проверить, считаем не shell
            }
        }

        /// <summary>
        /// Проверяет, запущено ли приложение с правами администратора.
        /// </summary>
        private static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Запускает новый процесс с запросом прав администратора для добавления в реестр.
        /// Возвращает процесс для ожидания завершения.
        /// </summary>
        private static Process? RunElevatedProcess(string title, string format)
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

                var args = $"--add \"{title.Replace("\"", "\\\"")}\" \"{format.Replace("\"", "\\\"")}\"";
                var processInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    Verb = "runas",
                    UseShellExecute = true
                };

                return Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске процесса с правами администратора: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Обрабатывает аргументы командной строки, если приложение запущено для изменения реестра.
        /// </summary>
        private static void HandleCommandLineArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 3)
            {
                try
                {
                    if (args[1] == "--add")
                    {
                        string title = args[2];
                        string format = args[3];
                        ContextMenuCreator.AddToNewMenu(title, format);
                        Environment.Exit(0);
                    }
                    else if (args[1] == "--remove")
                    {
                        string title = args[2];
                        string format = args[3];
                        ContextMenuCreator.RemoveFromNewMenu(title, format);
                        Environment.Exit(0);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при выполнении команды: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            }
        }

        // WinAPI для SHChangeNotify
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
    }
}