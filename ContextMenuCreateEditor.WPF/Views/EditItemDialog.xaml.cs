using ContextMenuCreateEditor.WPF.ViewModels;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace ContextMenuCreateEditor.WPF.Views
{
    public partial class EditItemDialog : FluentWindow
    {
        public ItemEditorViewModel ViewModel { get; }

        public EditItemDialog(ItemEditorViewModel viewModel)
        {
            ViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            ApplicationThemeManager.Apply(this);
            Loaded += (_, _) => DisplayNameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsValid)
            {
                MessageBox.Show(this, ViewModel.ValidationError ?? "Проверьте поля.", "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
