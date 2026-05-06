using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ContextMenuCreateEditor.WPF.Views
{
    public enum ConflictChoice
    {
        Cancel,
        UseExisting,
        ForceRecreate
    }

    public partial class ConflictDialog : FluentWindow, INotifyPropertyChanged
    {
        public string HeaderText { get; }
        public ConflictChoice Choice { get; private set; } = ConflictChoice.Cancel;

        public ConflictDialog(string extension, string existingProgId)
        {
            HeaderText = $"Расширение {extension} уже зарегистрировано (ProgID: {existingProgId}).";
            DataContext = this;
            InitializeComponent();
            ApplicationThemeManager.Apply(this);
        }

        private void UseExisting_Click(object sender, RoutedEventArgs e)
        {
            Choice = ConflictChoice.UseExisting;
            DialogResult = true;
            Close();
        }

        private void ForceRecreate_Click(object sender, RoutedEventArgs e)
        {
            Choice = ConflictChoice.ForceRecreate;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Choice = ConflictChoice.Cancel;
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
