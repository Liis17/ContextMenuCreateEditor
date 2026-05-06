using ContextMenuCreateEditor.WPF.Models;
using ContextMenuCreateEditor.WPF.Services;
using ContextMenuCreateEditor.WPF.UserControls;
using ContextMenuCreateEditor.WPF.ViewModels;
using ContextMenuCreateEditor.WPF.Views;
using ConflictChoice = ContextMenuCreateEditor.WPF.Views.ConflictChoice;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ContextMenuCreateEditor.WPF
{
    public partial class MainWindow : FluentWindow
    {
        private readonly MainViewModel _vm;
        private readonly IRegistryService _registry;
        private readonly ITemplateStorage _templates;
        private readonly IExplorerRefresher _refresher;
        private readonly IRegistryBackupService _backup;

        public MainWindow(
            MainViewModel vm,
            IRegistryService registry,
            ITemplateStorage templates,
            IExplorerRefresher refresher,
            IRegistryBackupService backup)
        {
            InitializeComponent();
            ApplicationThemeManager.Apply(this);

            _vm = vm;
            _registry = registry;
            _templates = templates;
            _refresher = refresher;
            _backup = backup;

            DataContext = _vm;

            _vm.OnAddRequested += async () => await ShowAddDialog();
            _vm.OnEditRequested += async vm => await ShowEditDialog(vm);
            _vm.OnDeleteRequested += async vm => await DeleteItem(vm);
            _vm.OnBackupRequested += DoBackup;
            _vm.OnRestartExplorerRequested += async () => await DoRestartExplorer();

            Loaded += async (_, _) =>
            {
                try { await _vm.ReloadAsync(); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Ошибка загрузки: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private async void PreviewItem_EditRequested(object sender, RoutedEventArgs e)
        {
            if (sender is PreviewItem pi && pi.DataContext is ShellNewItemViewModel vm)
                await ShowEditDialog(vm);
        }

        private async void PreviewItem_DeleteRequested(object sender, RoutedEventArgs e)
        {
            if (sender is PreviewItem pi && pi.DataContext is ShellNewItemViewModel vm)
                await DeleteItem(vm);
        }

        private async Task ShowAddDialog()
        {
            var editorVm = new ItemEditorViewModel(_registry);
            var dialog = new EditItemDialog(editorVm) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var built = editorVm.BuildResult();
                string templatePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(built.Template))
                    templatePath = _templates.SaveTemplate(built.FileName, built.Extension, built.Template);

                var result = await Task.Run(() => _registry.TryAdd(built.DisplayName, built.FileName, built.Extension, templatePath));

                if (result.Outcome == AddOutcome.Conflict)
                {
                    var conflict = new ConflictDialog(built.Extension, result.ExistingProgId ?? "?") { Owner = this };
                    conflict.ShowDialog();
                    switch (conflict.Choice)
                    {
                        case ConflictChoice.UseExisting:
                            result = await Task.Run(() => _registry.AddUsingExistingProgId(
                                built.DisplayName, built.FileName, built.Extension, templatePath, result.ExistingProgId!));
                            break;
                        case ConflictChoice.ForceRecreate:
                            result = await Task.Run(() => _registry.ForceAdd(
                                built.DisplayName, built.FileName, built.Extension, templatePath));
                            break;
                        default:
                            _templates.DeleteTemplate(templatePath);
                            return;
                    }
                }

                if (result.Item != null)
                {
                    _vm.AppendNew(result.Item);
                    _refresher.RefreshAssociations();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка добавления пункта", ex);
                MessageBox.Show(this, $"Ошибка добавления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ShowEditDialog(ShellNewItemViewModel itemVm)
        {
            if (!itemVm.Model.IsOwn)
            {
                MessageBox.Show(this, "Системные пункты редактировать нельзя.", "Запрещено",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var editorVm = new ItemEditorViewModel(_registry, itemVm.Model.Clone());
            var dialog = new EditItemDialog(editorVm) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var built = editorVm.BuildResult();
                _templates.DeleteTemplate(itemVm.Model.TemplatePath);

                string templatePath = string.Empty;
                if (!string.IsNullOrWhiteSpace(built.Template))
                    templatePath = _templates.SaveTemplate(built.FileName, built.Extension, built.Template);

                built.TemplatePath = templatePath;

                await Task.Run(() => _registry.Update(itemVm.Model, built));

                var fresh = await Task.Run(() => _registry.GetItems());
                ShellNewItem? updated = null;
                foreach (var x in fresh)
                {
                    if (string.Equals(x.Extension, built.Extension, StringComparison.OrdinalIgnoreCase))
                    {
                        updated = x;
                        break;
                    }
                }
                if (updated != null) itemVm.Model = updated;

                _refresher.RefreshAssociations();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка редактирования", ex);
                MessageBox.Show(this, $"Ошибка редактирования: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteItem(ShellNewItemViewModel itemVm)
        {
            if (!itemVm.Model.IsOwn) return;
            var answer = MessageBox.Show(this,
                $"Удалить пункт «{itemVm.DisplayName}» ({itemVm.Extension})?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                _templates.DeleteTemplate(itemVm.Model.TemplatePath);
                await Task.Run(() => _registry.Remove(itemVm.Model));
                _vm.RemoveVm(itemVm);
                _refresher.RefreshAssociations();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка удаления", ex);
                MessageBox.Show(this, $"Ошибка удаления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DoBackup()
        {
            try
            {
                var path = _backup.CreateBackup();
                var answer = MessageBox.Show(this,
                    $"Резервная копия создана:\n{path}\n\nОткрыть папку с бэкапами?",
                    "Готово", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (answer == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{_backup.BackupsFolder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка бэкапа", ex);
                MessageBox.Show(this, $"Не удалось создать резервную копию: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DoRestartExplorer()
        {
            var answer = MessageBox.Show(this,
                "Перезапустить Проводник? Открытые окна Проводника закроются.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            try
            {
                await _refresher.RestartShellAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
