using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;

namespace AsuUpdater.Classes
{
    public class UpdaterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action Close;

        private readonly Logger _logger;
        private string _sourceDirectoryPath;
        private string _destinDirectoryPath;
        private double _totalFileCount;
        private double _copiedFileCount;
        private DateTime _startDateTime;
        private bool _messageBoxIsOpen;
        private bool _isWorking;
        private bool _isStopping;

        private double _progressValue;
        public double ProgressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        private string _message;
        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        private Thread thread;

        public UpdaterViewModel()
        {
            _sourceDirectoryPath = $"\\\\{Service.GetInstance().GetServiceDict()["ServerAddress"]}\\{Service.GetInstance().GetServiceDict()["ServerDirectoryPath"]}"; ;
            _destinDirectoryPath = $"{Directory.GetParent(Directory.GetParent(Directory.GetCurrentDirectory()).FullName).FullName}\\{Service.GetInstance().GetServiceDict()["DestinDirectoryName"]}";
            _logger = Service.GetInstance().GetLogger();
            _logger.Info($"Программа открыта. Версия программы - {Release.GetInstance().GetVersion()}");

            //UpdateAsync();

        }

        public void Test()
        {
            thread = new Thread(UpdateAsync);
            thread.Start();
        }

        public void UpdateAsync()
        {
            //await Task.Run(() =>
            //{

            _isWorking = true;
            _startDateTime = DateTime.Now;
            Message = "Инициализация…";
            //Отключаемся от сервера на случай, если мы в настоящее время подключены с нашими учетными данными
            NetworkShare.DisconnectFromShare(_sourceDirectoryPath, true);
            Message = "Подключение к серверу…";
            //Подключаемся к серверу с нашими учетными данными
            var errorMessage = NetworkShare.ConnectToShare(_sourceDirectoryPath, Service.GetInstance().GetServiceDict()["UserName"], Service.GetInstance().GetServiceDict()["UserPassword"]);
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                Message = "Успешное подключение к серверу";
                DirectoryCopy(_sourceDirectoryPath, _destinDirectoryPath);
            }
            else
            {
                Message = errorMessage;
                throw new EndpointNotFoundException(errorMessage);
            }
            _isWorking = false;
            

           //if (!_messageBoxIsOpen || true)
            //{
            //    Close?.Invoke();
            //}
        }


        private void DirectoryCopy(string sourceDirectoryPath, string destinDirectoryPath)
        {
            Message = "Подготовка к копированию…";
            var directory = new DirectoryInfo(sourceDirectoryPath);
            if (!directory.Exists)
            {
                throw new DirectoryNotFoundException($"Исходный каталог не существует или не может быть найден: {sourceDirectoryPath}");
            }

            //Один раз получаем общее количество файлов для копирования
            if (_totalFileCount == 0)
            {
                _totalFileCount = Directory.GetFiles(sourceDirectoryPath, "*.*", SearchOption.AllDirectories).Length;
            }

            if (_totalFileCount > 0)
            {
                DirectoryDelete(destinDirectoryPath);
                Directory.CreateDirectory(destinDirectoryPath);
                var files = directory.GetFiles();
                foreach (var file in files)
                {
                    if (_isStopping)
                    {
                        return;
                    }
                    Message = $"Копирование {file.Name}…";
                    var filePath = Path.Combine(destinDirectoryPath, file.Name);
                    file.CopyTo(filePath, true);

                    _copiedFileCount++;
                    ProgressValue = (_copiedFileCount / _totalFileCount) * 100;
                }

                //Копируем подпапки с содержимым
                Message = "Получение подкаталогов…";
                var subDirectories = directory.GetDirectories();
                foreach (var subDirectory in subDirectories)
                {
                    if (_isStopping)
                    {
                        return;
                    }
                    var directoryPath = Path.Combine(destinDirectoryPath, subDirectory.Name);
                    DirectoryCopy(subDirectory.FullName, directoryPath);
                }
            }
            else
            {
                throw new FileNotFoundException($"В исходном каталоге не существуют файлы или не могут быть найдены: {sourceDirectoryPath}");
            }
        }

        private void DirectoryDelete(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }

        private void StopApp(bool withCloseCommand)
        {
            //Отключаемся от сервера
            Message = "Разрыв соединения…";
            NetworkShare.DisconnectFromShare(_sourceDirectoryPath, false);
            if (!_isStopping)
            {
                Message = $"Копирование файлов завершено. Затраченное время - {(DateTime.Now - _startDateTime).TotalSeconds.ToString("F1")} сек.";
            }
            else
            {
                ProgressValue = Math.Round(ProgressValue, 0);
                for (; ProgressValue > 0.0; ProgressValue--)
                {
                    Thread.Sleep(10);
                }
            }

            Thread.Sleep(2000);
            DirectoryDelete(_destinDirectoryPath);
            _logger.Warn("Программа закрыта до окончания обновления. Все изменения отменены");
            if (withCloseCommand)
            {
                Close?.Invoke();
            }
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (!_isWorking)
            {
                _logger.Info("Программа закрыта");
            }
            else
            {
                thread.Abort();
                //thread.Interrupt();
                _messageBoxIsOpen = true;
                Message = "Тестовое сообщение";
                if (MessageBoxResult.OK == MessageBox.Show("Процесс обновления не завершен.\nОтменить обновление?", "Подтвердите действие", MessageBoxButton.OKCancel))
                {
                    _messageBoxIsOpen = false;
                    _isStopping = true;
                    //if (!_isWorking)
                    //{
                    thread.Abort();
                    //StopApp(false);
                    //}
                    StopApp(false);
                }
                else
                {
                    e.Cancel = true;
                }
                _messageBoxIsOpen = false;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
