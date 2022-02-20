using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AsuUpdater.Classes;
using NLog;

namespace AsuUpdater
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = (Exception)e.ExceptionObject;
            Directory.CreateDirectory("Logs");
            LogManager.GetCurrentClassLogger().Fatal($"{exception}");
            MessageBox.Show($"Что-то пошло не так.\nВо время работы программы возникла критическая ошибка\n\n{exception.Message}.\nПроцесс обновления прерван.\n\nОбновитесь вручную или попробуйте ещё раз", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            MainWindow?.Close();
        }

        protected override void OnStartup(StartupEventArgs args)
        {
            base.OnStartup(args);

            var currentProcess = Process.GetCurrentProcess();
            if (Process.GetProcessesByName(currentProcess.ProcessName).Length > 1)
            {
                MessageBox.Show($"Запущено более одной копии программного обеспечения {currentProcess.ProcessName}", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                currentProcess.Kill();
            }

            UpdaterViewModel.ServerAddressFromArgument = args.Args?.Length > 0 ? string.IsNullOrWhiteSpace(args.Args[0]) ? null : args.Args[0] : null;
            UpdaterViewModel.ActualVersionPathFromArgument = args.Args?.Length > 1 ? string.IsNullOrWhiteSpace(args.Args[1]) ? null : args.Args[1] : null;
            UpdaterViewModel.UserNameFromArgument = args.Args?.Length > 2 ? string.IsNullOrWhiteSpace(args.Args[2]) ? null : args.Args[2] : null;
            UpdaterViewModel.UserPasswordFromArgument = args.Args?.Length > 3 ? string.IsNullOrWhiteSpace(args.Args[3]) ? null : args.Args[3] : null;
        }
    }
}