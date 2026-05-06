using ContextMenuCreateEditor.WPF.Models;
using ContextMenuCreateEditor.WPF.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ContextMenuCreateEditor.WPF.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IRegistryService _registry;
        private readonly ITemplateStorage _templates;
        private readonly IExplorerRefresher _refresher;
        private readonly IRegistryBackupService _backup;
        private readonly IIconService _icons;

        private string _searchText = string.Empty;
        private bool _showSystemItems;
        private bool _isBusy;

        public MainViewModel(
            IRegistryService registry,
            ITemplateStorage templates,
            IExplorerRefresher refresher,
            IRegistryBackupService backup,
            IIconService icons)
        {
            _registry = registry;
            _templates = templates;
            _refresher = refresher;
            _backup = backup;
            _icons = icons;

            Items = new ObservableCollection<ShellNewItemViewModel>();
            FilteredView = CollectionViewSource.GetDefaultView(Items);
            FilteredView.Filter = FilterPredicate;

            AddCommand = new RelayCommand(_ => OnAddRequested?.Invoke());
            EditCommand = new RelayCommand(p => { if (p is ShellNewItemViewModel vm) OnEditRequested?.Invoke(vm); });
            DeleteCommand = new RelayCommand(p => { if (p is ShellNewItemViewModel vm) OnDeleteRequested?.Invoke(vm); });
            RefreshCommand = new RelayCommand(async () => await ReloadAsync());
            BackupCommand = new RelayCommand(() => OnBackupRequested?.Invoke());
            RestartExplorerCommand = new RelayCommand(() => OnRestartExplorerRequested?.Invoke());
        }

        public ObservableCollection<ShellNewItemViewModel> Items { get; }
        public ICollectionView FilteredView { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                    FilteredView.Refresh();
            }
        }

        public bool ShowSystemItems
        {
            get => _showSystemItems;
            set
            {
                if (Set(ref _showSystemItems, value))
                    FilteredView.Refresh();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => Set(ref _isBusy, value);
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackupCommand { get; }
        public RelayCommand RestartExplorerCommand { get; }

        // Хост обработает диалоги/MessageBox
        public event Action? OnAddRequested;
        public event Action<ShellNewItemViewModel>? OnEditRequested;
        public event Action<ShellNewItemViewModel>? OnDeleteRequested;
        public event Action? OnBackupRequested;
        public event Action? OnRestartExplorerRequested;

        public async Task ReloadAsync()
        {
            try
            {
                IsBusy = true;
                var items = await Task.Run(() => _registry.GetItems());
                Items.Clear();
                foreach (var m in items.OrderBy(i => !i.IsOwn).ThenBy(i => i.DisplayName))
                {
                    var vm = new ShellNewItemViewModel(m)
                    {
                        Icon = _icons.GetFileIcon(m.Extension)
                    };
                    Items.Add(vm);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка загрузки списка", ex);
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public ShellNewItemViewModel AppendNew(ShellNewItem model)
        {
            var vm = new ShellNewItemViewModel(model)
            {
                Icon = _icons.GetFileIcon(model.Extension)
            };
            Items.Add(vm);
            return vm;
        }

        public void RemoveVm(ShellNewItemViewModel vm)
        {
            Items.Remove(vm);
        }

        private bool FilterPredicate(object obj)
        {
            if (obj is not ShellNewItemViewModel vm) return false;
            if (!ShowSystemItems && !vm.IsOwn) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            var s = SearchText.Trim();
            return vm.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase)
                || vm.Extension.Contains(s, StringComparison.OrdinalIgnoreCase)
                || vm.FileName.Contains(s, StringComparison.OrdinalIgnoreCase);
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
