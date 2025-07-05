using ContextMenuCreateEditor.WPF.UserControls;

using System.Diagnostics;
using System.Windows;

using Wpf.Ui.Appearance;

namespace ContextMenuCreateEditor.WPF
{
    public partial class MainWindow
    {
        private List<(string title, string format)> items;
        public MainWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
            Loaded += MainWindow_Loaded;
            items = new List<(string title, string format)>();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Update(null, null);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var title = TitleTb.Text;
            var format = FormatTb.Text;
            if (!format.StartsWith("."))
            {
                format = "." + format;
            }
            ContextMenuCreator.AddToNewMenu(title, format);
        }

        private void Update(object sender, RoutedEventArgs e)
        {
            var items = ContextMenuReader.GetRegisteredNewItems();
            PreviewPanel.Children.Clear();
            foreach (var item in items)
            {
                var previewContainer = new PreviewItem(item.DisplayName, item.Extension);
                PreviewPanel.Children.Add(previewContainer);
            }
        }

        private void ExplorerRestart(object sender, RoutedEventArgs e)
        {
            try
            {
                // Terminate all explorer.exe processes
                foreach (Process process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit(); // Wait for the process to terminate
                }

                // Start a new explorer.exe process
                Process.Start("explorer.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка при перезапуске проводника: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}