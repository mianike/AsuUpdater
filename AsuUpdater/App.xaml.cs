using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
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
            Exception exception = (Exception)e.ExceptionObject;
            Directory.CreateDirectory("Logs");
            File.AppendAllText("Logs\\log.txt", $"{DateTime.Now} - {e.ExceptionObject}");
            File.AppendAllText("Logs\\logCut.txt", $"{DateTime.Now} - {exception.Message}\n");
            LogManager.GetCurrentClassLogger().Fatal($"{exception.Message}");
            MessageBox.Show($"Что-то пошло не так\n{exception.Message}", "Критическое исключение", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            MainWindow?.Close();
        }

        protected override void OnStartup(StartupEventArgs args)
        {
            base.OnStartup(args);

            var currentProcess = Process.GetCurrentProcess();
            if (Process.GetProcessesByName(currentProcess.ProcessName).Length > 1)
            {
                MessageBox.Show("Запущено более одной копии программного обеспечения", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                currentProcess.Kill();
            }
        }
    }
}