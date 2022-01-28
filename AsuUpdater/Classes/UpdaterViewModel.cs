using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using IWshRuntimeLibrary;
using System.IO.Compression;

namespace AsuUpdater.Classes
{
    public class UpdaterViewModel : INotifyPropertyChanged
    {
        public static string ServerAddressFromArgument { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action Close;

        private readonly Logger _logger;

        private readonly string _serverAddress;
        private readonly string _sourceDirectoryPath;
        private readonly string _endDirectory;
        private readonly string _directoryForOldVersion;
        private readonly string _parentCurrentDirectoryPath;
        private readonly string _userName;
        private readonly string _userPassword;
        private readonly string _aboutVersionPath;
        private readonly string _versionKey;
        private readonly string _whatIsNewKey;
        private readonly string _runtimeFileName;
        private readonly string _endingForTempFolder = "_Backup";

        private double _totalFileCountFromServer;
        private double _copiedFileCountFromServer;
        private long _totalFileSizeFromServer;
        private long _copiedFileSizeFromServer;
        private Thread _thread;

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

        private string _alarmMessage;
        public string AlarmMessage
        {
            get { return _alarmMessage; }
            set
            {
                _alarmMessage = value;
                OnPropertyChanged(nameof(AlarmMessage));
            }
        }

        private bool _beforeUpdateProcess;
        public bool BeforeUpdateProcess
        {
            get { return _beforeUpdateProcess; }
            set
            {
                _beforeUpdateProcess = value;
                OnPropertyChanged(nameof(BeforeUpdateProcess));
            }
        }

        private bool _updateProcessIsRunning;
        public bool UpdateProcessIsRunning
        {
            get { return _updateProcessIsRunning; }
            set
            {
                _updateProcessIsRunning = value;
                OnPropertyChanged(nameof(UpdateProcessIsRunning));
            }
        }

        private bool _successfulCompletion;
        public bool SuccessfulCompletion
        {
            get { return _successfulCompletion; }
            set
            {
                _successfulCompletion = value;
                OnPropertyChanged(nameof(SuccessfulCompletion));
            }
        }

        private bool _emergencySituation;
        public bool EmergencySituation
        {
            get { return _emergencySituation; }
            set
            {
                _emergencySituation = value;
                OnPropertyChanged(nameof(EmergencySituation));
            }
        }

        private bool _emergencyCompletion;
        public bool EmergencyCompletion
        {
            get { return _emergencyCompletion; }
            set
            {
                _emergencyCompletion = value;
                OnPropertyChanged(nameof(EmergencyCompletion));
            }
        }

        private bool _visWhatIsNew;
        public bool VisWhatIsNew
        {
            get { return _visWhatIsNew; }
            set
            {
                _visWhatIsNew = value;
                OnPropertyChanged(nameof(VisWhatIsNew));
            }
        }

        private string _whatIsNew;
        public string WhatIsNew
        {
            get { return _whatIsNew; }
            set
            {
                _whatIsNew = value;
                OnPropertyChanged(nameof(WhatIsNew));
            }
        }

        private string _newVersion;
        public string NewVersion
        {
            get { return _newVersion; }
            set
            {
                _newVersion = value;
                OnPropertyChanged(nameof(NewVersion));
            }
        }

        private bool _startArmOnCompletion;
        public bool StartArmOnCompletion
        {
            get { return _startArmOnCompletion; }
            set
            {
                _startArmOnCompletion = value;
                OnPropertyChanged(nameof(StartArmOnCompletion));
            }
        }

        private bool _createShortcutOnCompletion;
        public bool CreateShortcutOnCompletion
        {
            get { return _createShortcutOnCompletion; }
            set
            {
                _createShortcutOnCompletion = value;
                OnPropertyChanged(nameof(CreateShortcutOnCompletion));
            }
        }

        public UpdaterViewModel()
        {
            try
            {
                _logger = Service.GetInstance().GetLogger();
                _logger.Info($"{Process.GetCurrentProcess().ProcessName} открыт. Версия {Release.GetInstance().GetVersion()}");

                _serverAddress = ServerAddressFromArgument ?? Service.GetInstance().GetServiceDict()["ServerAddress"];
                _parentCurrentDirectoryPath = Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName).FullName;
                _sourceDirectoryPath = $"\\\\{_serverAddress}\\{Service.GetInstance().GetServiceDict()["ActualVersionPath"]}";
                _endDirectory = $"{Service.GetInstance().GetServiceDict()["UpdatedProgram"]}";
                _directoryForOldVersion = Service.GetInstance().GetServiceDict()["FolderForOldVersion"];
                _userName = Service.GetInstance().GetServiceDict()["UserName"];
                _userPassword = Service.GetInstance().GetServiceDict()["UserPassword"];
                _aboutVersionPath = $"{Service.GetInstance().GetServiceDict()["AboutVersionPath"]}";
                _versionKey = Service.GetInstance().GetServiceDict()["VersionKeyInAboutVersionFile"];
                _whatIsNewKey = Service.GetInstance().GetServiceDict()["WhatIsNewKeyInAboutVersionFile"];
                _runtimeFileName = Service.GetInstance().GetServiceDict()["RuntimeFileName"];

                StartArmOnCompletion = true;
                CreateShortcutOnCompletion = true;

                _logger.Info($"Успешная инициализация {this.GetType().Name}\n\tРодительская директория для работы с папками: {_parentCurrentDirectoryPath}" +
                             $"\n\tАдрес сервера ({(ServerAddressFromArgument != null ? "Передан аргументом обновляемой программой" : "Обновляемой программой аргументом не передан, получен из UpdaterService.json")}) {_serverAddress}" +
                             $"\n\tПолный путь к актуальной версии: {_sourceDirectoryPath}\n\tЦелевая папка для обновления: {_endDirectory}\n\tИмя пользователя для входа: {_userName}" +
                             $"\n\tПароль пользователя для входа: {_userPassword}\n\tПуть к файлу о версии внутри актуальной программы: {_aboutVersionPath}" +
                             $"\n\tИмя ключа для десериализации номера версии из файла о версии: {_versionKey}\n\tИмя ключа для десериализации данных о том, что нового в версии из файла о версии: {_whatIsNewKey}" +
                             $"\n\tНазвание папки для устаревших версий: {(!string.IsNullOrWhiteSpace(_directoryForOldVersion) ? _directoryForOldVersion : "Значение не задано. Старая версия при успешном обновлении будет удалена")}" +
                             $"\n\tНазвание исполняемого файла в целевой папке: {_runtimeFileName}");

                BeforeUpdateProcess = true;
            }
            catch (Exception ex)
            {
                throw new Exception($"При инициализации {this.GetType().Name} возникло исключение\n{ex.Message}");
            }
        }

        private void StartUpdate()
        {
            _thread = new Thread(UpdateAsync);
            _thread.Start();
        }

        public void UpdateAsync()
        {
            try
            {
                UpdateProcessIsRunning = true;
                var startUpdateDateTime = DateTime.Now;
                Message = "Инициализация…";
                _logger.Info("Начат процесс обновления");
                _logger.Info("Попытка высвободить подключение если мы уже подключены с текущими учетными данными");
                var errorDisconnectMessage = DisconnectFromServer(_sourceDirectoryPath, true);
                _logger.Info($"Статус высвобождения подключения: {errorDisconnectMessage ?? "Успешный разрыв соединения"}");
                Message = $"Подключение к серверу {_serverAddress}…";
                _logger.Info("Попытка подключения к серверу");
                var errorConnectionMessage = NetworkShare.ConnectToShare(_sourceDirectoryPath, _userName, _userPassword);
                if (string.IsNullOrWhiteSpace(errorConnectionMessage))
                {
                    Message = "Успешное подключение к серверу";
                    _logger.Info("Успешное подключение к серверу");

                    Message = "Создание бэкапа…";
                    _logger.Info($"Создание бэкапа для {_endDirectory}");
                    var copyResult = DirectoryCopy($"{_parentCurrentDirectoryPath}\\{_endDirectory}", $"{_parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}", false);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное создание бэкапа для {_endDirectory}");
                    }
                    else
                    {
                        _logger.Warn($"Неудачное создание бэкапа для {_endDirectory}. {copyResult}");
                    }

                    Message = "Получение данных о новой версии…";
                    WhatIsNew = DeserializeJson($"{_sourceDirectoryPath}\\{_aboutVersionPath}", _whatIsNewKey);
                    if (!string.IsNullOrWhiteSpace(WhatIsNew))
                    {
                        _logger.Info($"Успешное получение данных о том, что нового в актуальной версии: {WhatIsNew}");
                    }
                    else
                    {
                        WhatIsNew = "Исправлены ошибки, повышена стабильность работы";
                        _logger.Warn($"Не удалось получить данные о том, что нового в актуальной версии. Используется значение по умолчанию {WhatIsNew}");
                    }
                    NewVersion = DeserializeJson($"{_sourceDirectoryPath}\\{_aboutVersionPath}", _versionKey);
                    if (!string.IsNullOrWhiteSpace(NewVersion))
                    {
                        _logger.Info($"Успешное получение номера актуальной версии: {NewVersion}");
                        VisWhatIsNew = true;
                    }
                    else
                    {
                        _logger.Warn("Не удалось получить номер актуальной версии. Функция \"Что нового\" недоступна");
                    }

                    Message = "Подготовка к копированию…";
                    copyResult = DirectoryCopy(_sourceDirectoryPath, $"{_parentCurrentDirectoryPath}\\{_endDirectory}", true);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное завершение копирования {_sourceDirectoryPath}. Затраченное время - {(DateTime.Now - startUpdateDateTime).TotalSeconds.ToString("F1")} сек.");
                    }
                    else
                    {
                        _logger.Warn($"Неудача копирования {_sourceDirectoryPath}. {copyResult}");
                    }
                }
                else
                {
                    throw new EndpointNotFoundException($"При попытке подключения к серверу возникла ошибка: {errorConnectionMessage}");
                }

                Message = "Закрытие соединения…";
                errorDisconnectMessage = DisconnectFromServer(_sourceDirectoryPath, false);
                _logger.Info($"Закрытие соединения: {errorDisconnectMessage ?? "Успешный разрыв соединения"}");

                Message = "Завершение обновления…";
                _logger.Info($"Начат процесс переноса папки со старой версией {_endDirectory}{_endingForTempFolder}");
                if (!string.IsNullOrWhiteSpace(_directoryForOldVersion))
                {
                    string partName = DeserializeJson($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}\\{_aboutVersionPath}", _versionKey);
                    if (!string.IsNullOrWhiteSpace(partName))
                    {
                        _logger.Info($"Успешное получение номера обновляемой версии: {partName}");
                    }
                    else
                    {
                        _logger.Warn("Не удалось получить номер обновляемой версии. Вместо номера обновляемой версии в названии при переносе в папку со старыми версиями будет использоваться текущая дата и время");
                        var currentDateTime = DateTime.Now;
                        partName = $"UPD_{currentDateTime.Year}{currentDateTime.Month.ToString("D2")}{currentDateTime.Day.ToString("D2")}.{currentDateTime.Hour.ToString("D2")}{currentDateTime.Minute.ToString("D2")}";
                    }

                    partName = $"_{partName}";

                    var copyResult = DirectoryCopy($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}", $"{_parentCurrentDirectoryPath}\\{_directoryForOldVersion}\\{_endDirectory}{partName}", false);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное завершение копирования старой версии из {_endDirectory}{_endingForTempFolder} в {_directoryForOldVersion}\\{_endDirectory}{partName}");
                    }
                    else
                    {
                        _logger.Warn($"Неудача копирования старой версии из {_endDirectory}{_endingForTempFolder} в {_directoryForOldVersion}\\{_endDirectory}{partName}. {copyResult}");
                    }
                }
                else
                {
                    _logger.Warn($"Конечная папка {_directoryForOldVersion} не задана в {_aboutVersionPath}. Папка со старой версией {_endDirectory}{_endingForTempFolder} будет удалена");
                }
                ProgressValue = 99.9;
                DirectoryDelete($"{ _parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}");

                ProgressValue = 100.0;
                Message = $"Успешное завершение обновления.\nЗатраченное время на обновление: {(DateTime.Now - startUpdateDateTime).TotalSeconds.ToString("F1")} сек.";
                _logger.Info($"Успешное завершение обновления. Затраченное время на обновление: {(DateTime.Now - startUpdateDateTime).TotalSeconds.ToString("F1")} сек.");
                UpdateProcessIsRunning = false;
                SuccessfulCompletion = true;

            }
            catch (Exception ex)
            {
                UpdateProcessIsRunning = false;
                EmergencySituation = true;
                _logger.Error($"При обновлении возникла ошибка: {ex.Message}. Процесс обновления прерван");
                AlarmMessage = ex.Message;
                EmergencyStop();
                EmergencyCompletion = true;
            }
        }


        //TODO: Реализровать копирование через архивы

        //private string ZipCopy(string sourceDirectoryPath, string destinDirectoryPath)
        //{
        //    try
        //    {
        //        _logger.Info($"Подготовка к созданию и копированию архива {sourceDirectoryPath} в {destinDirectoryPath}");
        //        var directory = new DirectoryInfo(sourceDirectoryPath);
        //        if (!directory.Exists)
        //        {
        //            throw new DirectoryNotFoundException(
        //                $"Исходной папки не существует или не может быть найдена: {sourceDirectoryPath}");
        //        }

        //        //Один раз получаем общее количество файлов для копирования
        //        if (_totalFileCountFromServer == 0)
        //        {
        //            _totalFileCountFromServer = directory.GetFiles("*.*", SearchOption.AllDirectories).Length;
        //            _logger.Info(
        //                $"Общее количество файлов для копирования в {sourceDirectoryPath}: {_totalFileCountFromServer}");
        //        }

        //        if (_totalFileCountFromServer > 0)
        //        {
        //            Message = "Проверка свободного места на диске…";
        //            var spaceMessage = GetNotEnoughSpace(sourceDirectoryPath, true);
        //            if (!string.IsNullOrWhiteSpace(spaceMessage))
        //            {
        //                throw new IOException(spaceMessage);
        //            }

        //            DirectoryDelete(destinDirectoryPath, true);

        //            ZipFile.CreateFromDirectory(sourceDirectoryPath, $"{destinDirectoryPath}Zip");
        //            ZipFile.ExtractToDirectory($"{destinDirectoryPath}Zip", destinDirectoryPath);
        //            return null;
        //        }
        //        else
        //        {
        //            throw new FileNotFoundException(
        //                $"В исходной папке не существуют файлы или не могут быть найдены: {sourceDirectoryPath}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return
        //            $"При копировании файлов из {sourceDirectoryPath} в {destinDirectoryPath} возникла ошибка {ex.Message}";
        //    }
        //}

        private string DirectoryCopy(string sourceDirectoryPath, string destinDirectoryPath, bool isCopyFromServer, bool сheckSpace = true)
        {
            try
            {
                _logger.Info($"Подготовка к копированию {sourceDirectoryPath} в {destinDirectoryPath}");
                var directory = new DirectoryInfo(sourceDirectoryPath);
                if (!directory.Exists)
                {
                    throw new DirectoryNotFoundException($"Исходной папки не существует или не может быть найдена: {sourceDirectoryPath}");
                }

                //Один раз получаем общее количество файлов для копирования
                if (_totalFileCountFromServer == 0 && isCopyFromServer)
                {
                    _totalFileCountFromServer = directory.GetFiles("*.*", SearchOption.AllDirectories).Length;
                    _logger.Info($"Общее количество файлов для копирования в {sourceDirectoryPath}: {_totalFileCountFromServer}");
                }

                if (_totalFileCountFromServer > 0 || !isCopyFromServer)
                {
                    if (сheckSpace)
                    {
                        Message = "Проверка свободного места на диске…";
                        var spaceMessage = GetNotEnoughSpace(sourceDirectoryPath, isCopyFromServer);
                        if (!string.IsNullOrWhiteSpace(spaceMessage))
                        {
                            throw new IOException(spaceMessage);
                        }
                    }

                    DirectoryDelete(destinDirectoryPath, isCopyFromServer);

                    Message = "Создание папок…";
                    Directory.CreateDirectory(destinDirectoryPath);
                    if (isCopyFromServer)
                    {
                        _logger.Info($"Создана папка {destinDirectoryPath}");
                    }

                    var files = directory.GetFiles();
                    foreach (var file in files)
                    {
                        if (isCopyFromServer)
                        {
                            _logger.Info($"Начато копирование {file.Name}");
                            Message = $"Копирование {file.Name}…";
                        }
                        var filePath = Path.Combine(destinDirectoryPath, file.Name);
                        file.CopyTo(filePath, true);

                        if (isCopyFromServer)
                        {
                            _logger.Info($"Завершено копирование {file.Name}");
                            _copiedFileCountFromServer++;
                            _copiedFileSizeFromServer += file.Length;

                            double progressValue;
                            if (_totalFileSizeFromServer > 0 && _copiedFileSizeFromServer > 0)
                            {
                                progressValue = (double)_copiedFileSizeFromServer / (double)_totalFileSizeFromServer * 100;
                            }
                            else
                            {
                                progressValue = _copiedFileCountFromServer / _totalFileCountFromServer * 100;
                            }
                            ProgressValue = progressValue > 99.8 ? ProgressValue : progressValue;
                        }
                    }

                    Message = "Получение подпапок…";
                    var subDirectories = directory.GetDirectories();
                    foreach (var subDirectory in subDirectories)
                    {
                        var directoryPath = Path.Combine(destinDirectoryPath, subDirectory.Name);
                        DirectoryCopy(subDirectory.FullName, directoryPath, isCopyFromServer, false);
                    }
                    return null;
                }
                else
                {
                    throw new FileNotFoundException($"В исходной папке не существуют файлы или не могут быть найдены: {sourceDirectoryPath}");
                }
            }
            catch (Exception ex)
            {
                if (isCopyFromServer)
                {
                    throw;
                }
                return $"При копировании файлов из {sourceDirectoryPath} в {destinDirectoryPath} возникла ошибка {ex.Message}";
            }
        }

        private string GetNotEnoughSpace(string directoryPath, bool isCopyFromServer)
        {
            try
            {
                var drive = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                var userName = Environment.UserName;
                _logger.Info($"Попытка определить достаточно ли места для текущего пользователя {userName} на текущем диске {drive} для копирования папки {directoryPath}");
                var freeSpaceOnCurrentDrive = new DriveInfo(drive).AvailableFreeSpace;
                _logger.Info($"Для текущего пользователя {userName} на текущем диске {drive} доступно {((double)freeSpaceOnCurrentDrive / 1024 / 1024).ToString("F2")} МБ ({freeSpaceOnCurrentDrive} Б)");
                var needSpace = GetDirectorySize(directoryPath);
                _totalFileSizeFromServer = isCopyFromServer ? needSpace : _totalFileSizeFromServer;
                _logger.Info($"Для успешного копирования файлов из папки {directoryPath} на текущем диске {drive} для текущего пользователя {userName} должно быть доступно не менее {((double)needSpace / 1024 / 1024).ToString("F2")} МБ ({needSpace} Б)");

                if (freeSpaceOnCurrentDrive > needSpace)
                {
                    _logger.Info($"На текущем диске {drive} для текущего пользователя {userName} достаточно места для копирования папки {directoryPath}");
                    return null;
                }

                var message = $"На текущем диске {drive} для текущего пользователя {userName} НЕ достаточно места для копирования папки {directoryPath}. Копирование не возможно";
                _logger.Warn(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.Warn($"При попытке определить достаточно ли места на диске для копирования папки {directoryPath} возникла ошибка {ex.Message}. Копирование может завершиться неудачей");
                return null;
            }
        }

        private long GetDirectorySize(string directoryPath)
        {
            try
            {
                long size = 0;
                var directory = new DirectoryInfo(directoryPath);
                var files = directory.GetFiles();
                foreach (var file in files)
                {
                    size += file.Length;
                }
                var subDirectories = directory.GetDirectories();
                foreach (var subDirectory in subDirectories)
                {
                    size += GetDirectorySize(subDirectory.FullName);
                }
                return size;
            }
            catch (Exception ex)
            {
                throw new IOException($"При попытке определить размер папки {directoryPath} возникла ошибка {ex.Message}");
            }
        }

        private string DeserializeJson(string filePath, string keyName)
        {
            string result;
            try
            {
                _logger.Info($"Попытка десериализации JSON-файла {filePath} для ключа {keyName}");
                string json = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
                result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)[keyName];
            }
            catch (Exception ex)
            {
                result = null;
                _logger.Warn($"Ошибка десериализации JSON-файла {filePath} для ключа {keyName}: {ex.Message}");
            }
            return result;
        }

        private void DirectoryDelete(string directoryPath, bool isCopyFromServer = false)
        {
            if (isCopyFromServer)
            {
                _logger.Info($"Попытка удаления папки {directoryPath}");
            }
            if (Directory.Exists(directoryPath))
            {
                bool result = false;
                while (!result)
                {
                    try
                    {
                        Message = "Удаление временных файлов и папок…";
                        Directory.Delete(directoryPath, true);
                        if (isCopyFromServer)
                        {
                            _logger.Info($"Успешное удаление папки {directoryPath}");
                        }
                        Message = "Успешное удаление временных файлов и папок";
                        result = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"При попытке удаления папки {directoryPath} возникла ошибка {ex.Message}");
                        if (MessageBoxResult.OK == MessageBox.Show($"Ошибка манипуляций с папкой {directoryPath}.\nВозможно, папка открыта или используется файл из папки.\n{ex.Message}.\n\nПопробуйте закрыть папку или открытые файлы из папки и нажмите ОК для повтора.\nНажмите Отмена для отмены обновления", "Ошибка доступа", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                        {
                            _logger.Info($"Пользователем предпринята ещё одна попытка удаления папки {directoryPath}");
                        }
                        else
                        {
                            _logger.Warn($"Пользователь отменил удаление при невозможности удалить папку {directoryPath}");
                            throw;
                        }
                    }
                }
            }
            else
            {
                if (isCopyFromServer)
                {
                    _logger.Info($"Папка {directoryPath} не найдена");
                }
            }
        }

        private string DisconnectFromServer(string sourceDirectoryPath, bool force)
        {
            return NetworkShare.DisconnectFromShare(sourceDirectoryPath, force);
        }

        private void EmergencyStop()
        {
            try
            {
                _logger.Info("Начат процесс отмены изменений");
                var errorDisconnectMessage = DisconnectFromServer(_sourceDirectoryPath, false);
                _logger.Info($"Закрытие соединения: {errorDisconnectMessage ?? "Успешный разрыв соединения"}");
                _logger.Info($"Попытка восстановить бэкап {_endDirectory} из {_endDirectory}{_endingForTempFolder}");
                var copyResult = DirectoryCopy($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}", $"{_parentCurrentDirectoryPath}\\{_endDirectory}", false);
                if (string.IsNullOrWhiteSpace(copyResult))
                {
                    _logger.Info($"Успешное восстановление бэкапа {_endDirectory} из {_endDirectory}{_endingForTempFolder}");
                }
                else
                {
                    _logger.Warn($"Неудачное восстановление бэкапа {_endDirectory} из {_endDirectory}{_endingForTempFolder}. {copyResult}");
                }

                DirectoryDelete($"{ _parentCurrentDirectoryPath}\\{_endDirectory}{_endingForTempFolder}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"Ошибка отмены изменений после неудачной попытки обновления. {ex.Message}");
            }
        }

        public void StartProcess()
        {
            try
            {
                _logger.Info($"Запуск {_endDirectory}\\{_runtimeFileName}");
                var process = new Process();
                process.StartInfo = new ProcessStartInfo($"{_parentCurrentDirectoryPath}\\{_endDirectory}\\{_runtimeFileName}")
                {
                    UseShellExecute = true,
                    WorkingDirectory = $"{_parentCurrentDirectoryPath}\\{_endDirectory}"
                };
                process.Start();
                _logger.Info($"{_endDirectory}\\{_runtimeFileName} автоматически запущен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"При попытке автоматического запуска {_endDirectory}\\{_runtimeFileName} возникла ошибка.\nПопробуйте запустить приложение вручную",
                    "Ошибка автоматического запуска", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);
                _logger.Warn($"Ошибка автоматического запуска {_endDirectory}\\{_runtimeFileName}. {ex.Message}");
            }
        }

        public void CreateShortcut(string shortcutPath)
        {
            try
            {
                _logger.Info($"Создание ярлыка для {_endDirectory}\\{_runtimeFileName} на {shortcutPath}");
                string shortcutLocation = Path.Combine(shortcutPath, $"{_runtimeFileName} — ярлык.lnk");

                if (System.IO.File.Exists(shortcutLocation))
                {
                    System.IO.File.Delete(shortcutLocation);
                }

                WshShell shell = new WshShell();
                IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutLocation);
                shortcut.WorkingDirectory = $"{_parentCurrentDirectoryPath}\\{_endDirectory}";
                shortcut.TargetPath = $"{_parentCurrentDirectoryPath}\\{_endDirectory}\\{_runtimeFileName}";
                shortcut.Save();
                _logger.Info($"Создан ярлык для {_endDirectory}\\{_runtimeFileName} на {shortcutPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"При попытке создания ярлыка для {_endDirectory}\\{_runtimeFileName} на {shortcutPath} возникла ошибка.",
                    "Ошибка создания ярлыка", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly);
                _logger.Warn($"Ошибка создания ярлыка для {_endDirectory}\\{_runtimeFileName} на {shortcutPath} по завершению установки. {ex.Message}");
            }
        }

        public void OnWindowClosing(object sender, CancelEventArgs e)
        {
            _logger.Info("Процесс закрытия программы обновления");
            if (!UpdateProcessIsRunning)
            {
                if (SuccessfulCompletion)
                {
                    if (CreateShortcutOnCompletion)
                    {
                        CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                    }
                    if (StartArmOnCompletion)
                    {
                        StartProcess();
                    }
                }

                if (EmergencySituation && !EmergencyCompletion)
                {
                    e.Cancel = true;
                    _logger.Info("Закрытие программы обновления отменено: отмена изменений не окончена");
                    MessageBox.Show("Дождитесь окончания отмены изменений", "Дождитесь окончания отмены изменений", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    return;
                }

                _logger.Info("Программа обновления закрыта");
                LogManager.DisableLogging();
                DirectoryCopy($"{AppDomain.CurrentDomain.BaseDirectory}\\Logs", $"{_parentCurrentDirectoryPath}\\{_endDirectory}\\AsuUpdater\\Logs", false, false);
            }
            else
            {
                if (MessageBoxResult.OK == MessageBox.Show("Процесс обновления не завершен.\n\nОтменить обновление?", "Процесс прерывания обновления", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly))
                {
                    UpdateProcessIsRunning = false;
                    _thread.Abort();//прерываем поток
                    _thread.Join(1000);//таймаут на завершение
                    _thread = null;
                    EmergencyStop();
                    _logger.Info("Программа обновления закрыта");
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }

        private RelayCommand _closeApplicationCommand;
        public RelayCommand CloseApplicationCommand
        {
            get
            {
                return _closeApplicationCommand ??
                       (_closeApplicationCommand = new RelayCommand(obj =>
                       {
                           Close?.Invoke();
                       }));
            }
        }

        private RelayCommand _startUpdateCommand;
        public RelayCommand StartUpdateCommand
        {
            get
            {
                return _startUpdateCommand ??
                       (_startUpdateCommand = new RelayCommand(obj =>
                       {
                           BeforeUpdateProcess = false;
                           StartUpdate();
                       }));
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
