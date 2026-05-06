using ContextMenuCreateEditor.WPF.Models;
using ContextMenuCreateEditor.WPF.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ContextMenuCreateEditor.WPF.ViewModels
{
    public class ItemEditorViewModel : INotifyPropertyChanged
    {
        private readonly IRegistryService _registry;

        private string _displayName = string.Empty;
        private string _fileName = string.Empty;
        private string _extension = string.Empty;
        private string _template = string.Empty;
        private string? _validationError;

        public ItemEditorViewModel(IRegistryService registry, ShellNewItem? source = null)
        {
            _registry = registry;
            IsEditMode = source != null;
            if (source != null)
            {
                _displayName = source.DisplayName;
                _fileName = source.FileName;
                _extension = source.Extension;
                _template = source.Template;
                Source = source;
            }
        }

        public bool IsEditMode { get; }
        public ShellNewItem? Source { get; }
        public string Title => IsEditMode ? "Редактировать пункт" : "Добавить пункт";

        public string DisplayName
        {
            get => _displayName;
            set { if (Set(ref _displayName, value)) Validate(); }
        }

        public string FileName
        {
            get => _fileName;
            set { if (Set(ref _fileName, value)) Validate(); }
        }

        public string Extension
        {
            get => _extension;
            set { if (Set(ref _extension, value)) Validate(); }
        }

        public string Template
        {
            get => _template;
            set => Set(ref _template, value);
        }

        public string? ValidationError
        {
            get => _validationError;
            private set => Set(ref _validationError, value);
        }

        public bool IsValid => Validate();

        public ShellNewItem BuildResult()
        {
            return new ShellNewItem
            {
                DisplayName = DisplayName.Trim(),
                FileName = FileName.Trim(),
                Extension = NormalizeExt(Extension),
                Template = Template ?? string.Empty,
                ProgId = Source?.ProgId,
                IsOwn = Source?.IsOwn ?? true
            };
        }

        public bool ExtensionWillBeDerived => string.IsNullOrWhiteSpace(Extension);

        private bool Validate()
        {
            if (!_registry.ValidateName(DisplayName, out var dnErr)) { ValidationError = $"Имя в меню: {dnErr}"; return false; }
            if (!_registry.ValidateName(FileName, out var fnErr)) { ValidationError = $"Имя файла: {fnErr}"; return false; }
            if (!_registry.ValidateExtension(Extension, out _, out var extErr)) { ValidationError = $"Расширение: {extErr}"; return false; }
            ValidationError = null;
            return true;
        }

        private static string NormalizeExt(string ext)
        {
            var t = (ext ?? string.Empty).Trim().TrimStart('.');
            return string.IsNullOrEmpty(t) ? string.Empty : "." + t.ToLowerInvariant();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsValid)));
            return true;
        }
    }
}
