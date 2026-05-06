using ContextMenuCreateEditor.WPF.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ContextMenuCreateEditor.WPF.ViewModels
{
    public class ShellNewItemViewModel : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _extension = string.Empty;
        private string _fileName = string.Empty;
        private bool _isOwn;
        private BitmapSource? _icon;
        private ShellNewItem _model;

        public ShellNewItemViewModel(ShellNewItem model)
        {
            _model = model;
            UpdateFromModel();
        }

        public ShellNewItem Model
        {
            get => _model;
            set
            {
                _model = value;
                UpdateFromModel();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }

        public string DisplayName
        {
            get => _displayName;
            private set => Set(ref _displayName, value);
        }

        public string Extension
        {
            get => _extension;
            private set => Set(ref _extension, value);
        }

        public string FileName
        {
            get => _fileName;
            private set => Set(ref _fileName, value);
        }

        public bool IsOwn
        {
            get => _isOwn;
            private set
            {
                if (Set(ref _isOwn, value))
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
            }
        }

        public string Subtitle => $"{FileName}{Extension}" + (IsOwn ? string.Empty : "  (системный)");

        public BitmapSource? Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        private void UpdateFromModel()
        {
            DisplayName = _model.DisplayName;
            Extension = _model.Extension;
            FileName = _model.FileName;
            IsOwn = _model.IsOwn;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            return true;
        }
    }
}
