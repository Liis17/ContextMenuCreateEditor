using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ContextMenuCreateEditor.WPF.UserControls
{
    public partial class PreviewItem : UserControl
    {
        public event EventHandler? ItemDeleted; // Событие для уведомления родителя

        private readonly string title;
        private readonly string format;

        public PreviewItem(string _title, string _format)
        {
            InitializeComponent();
            Loaded += PreviewItem_Loaded;
            title = _title ?? throw new ArgumentNullException(nameof(_title));
            format = _format ?? throw new ArgumentNullException(nameof(_format));
        }

        private void PreviewItem_Loaded(object sender, RoutedEventArgs e)
        {
            IconItem.Source = FileIconHelper.GetFileIcon(format);
            TitleTextBlock.Text = title;
            FormatTextBlock.Text = format;
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsRunningAsAdmin())
                {
                    // Права есть, удаляем напрямую
                    ContextMenuCreator.RemoveFromNewMenu(title, format);
                    ItemDeleted?.Invoke(this, EventArgs.Empty);
                    MainWindow.Instance.Update(); // Обновляем список в родительском окне
                }
                else
                {
                    // Запускаем elevated процесс
                    var process = RunElevatedProcessForDelete(title, format);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        if (process.ExitCode == 0)
                        {
                            ItemDeleted?.Invoke(this, EventArgs.Empty);
                            MainWindow.Instance.Update();
                        }
                        else
                        {
                            MessageBox.Show("Удаление элемента в elevated процессе завершилось с ошибкой.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении элемента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            DeleteButton.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            DeleteButton.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private void MainBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.2)
            };
            fadeOut.Completed += (s, _) => DeleteButton.Visibility = Visibility.Collapsed; // Исправлено: подписка на событие
            DeleteButton.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private static bool IsRunningAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static Process? RunElevatedProcessForDelete(string title, string format)
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                    throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");

                var args = $"--remove \"{title.Replace("\"", "\\\"")}\" \"{format.Replace("\"", "\\\"")}\"";
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
    }
}