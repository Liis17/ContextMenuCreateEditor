using ContextMenuCreateEditor.WPF.Services;
using ContextMenuCreateEditor.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ContextMenuCreateEditor.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // CLI-режим: Explorer вызывает нас через ShellNew Command для создания файла с точным именем.
            // Формат: --create "<templatePath>" "<targetFolder>" "<baseName>"
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 5 && string.Equals(args[1], "--create", StringComparison.Ordinal))
            {
                try { FileCreator.CreateFile(args[2], args[3], args[4]); }
                catch (Exception ex) { AppLogger.Error("CLI --create failed", ex); }
                Shutdown(0);
                return;
            }

            // Глобальная обработка ошибок
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Простая ручная DI
            IIconService icons = new IconService();
            ITemplateStorage templates = new TemplateStorage();
            IRegistryService registry = new HkcuRegistryService();
            IExplorerRefresher refresher = new ExplorerRefresher();
            IRegistryBackupService backup = new RegistryBackupService();

            var mainVm = new MainViewModel(registry, templates, refresher, backup, icons);
            var window = new MainWindow(mainVm, registry, templates, refresher, backup);

            base.OnStartup(e);
            window.Show();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error("Unhandled UI exception", e.Exception);
            MessageBox.Show(
                $"Произошла ошибка:\n{e.Exception.Message}\n\nЛог: {AppLogger.LogPath}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppLogger.Error("Unhandled domain exception", e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            AppLogger.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        }
    }
}
