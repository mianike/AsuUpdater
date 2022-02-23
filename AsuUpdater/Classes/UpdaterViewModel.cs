using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using NLog;
using IWshRuntimeLibrary;
using System.IO.Compression;
using System.Linq;
using File = System.IO.File;

namespace AsuUpdater.Classes
{
    internal class UpdaterViewModel : INotifyPropertyChanged
    {
        public static string ServerAddressFromArgument { get; set; }
        public static string ActualVersionPathFromArgument { get; set; }
        public static string UserNameFromArgument { get; set; }
        public static string UserPasswordFromArgument { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public event Action Close;

        private readonly Logger _logger;
        private Thread _thread;

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

        private string _partName;
        private double _totalFileCountFromServer;
        private double _copiedFileCountFromServer;
        private long _totalFileSizeFromServer;
        private long _copiedFileSizeFromServer;
        private int _skippedFiles;
        private int _copiedFiles;
        private int _deletedFiles;
        private bool _wasConnected;

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

                //Отключаем логирование на время чистки логов
                LogManager.DisableLogging();
                var clearLogsStatus = ClearLogs($"{AppDomain.CurrentDomain.BaseDirectory}Logs");
                LogManager.EnableLogging();

                _logger.Info($"{Process.GetCurrentProcess().ProcessName} версии {Release.GetInstance().GetVersion()} открыт");
                _logger.Info($"При открытии была предпринята попытка очистки логов. Статус очистки: {clearLogsStatus}");

                _serverAddress = ServerAddressFromArgument ?? Service.GetInstance().GetServiceDict()["ServerAddress"];
                string actualVersionPath = ActualVersionPathFromArgument ?? Service.GetInstance().GetServiceDict()["ActualVersionPath"];
                _parentCurrentDirectoryPath = Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName).FullName;
                _sourceDirectoryPath = $"\\\\{_serverAddress}\\{actualVersionPath}";
                _endDirectory = $"{Service.GetInstance().GetServiceDict()["UpdatedProgram"]}";
                _directoryForOldVersion = Service.GetInstance().GetServiceDict()["FolderForOldVersion"];
                _userName = UserNameFromArgument ?? Service.GetInstance().GetServiceDict()["UserName"];
                _userPassword = UserPasswordFromArgument ?? Service.GetInstance().GetServiceDict()["UserPassword"];
                _aboutVersionPath = $"{Service.GetInstance().GetServiceDict()["AboutVersionPath"]}";
                _versionKey = Service.GetInstance().GetServiceDict()["VersionKeyInAboutVersionFile"];
                _whatIsNewKey = Service.GetInstance().GetServiceDict()["WhatIsNewKeyInAboutVersionFile"];
                _runtimeFileName = Service.GetInstance().GetServiceDict()["RuntimeFileName"];

                StartArmOnCompletion = true;
                CreateShortcutOnCompletion = true;

                _logger.Info($"Успешная инициализация {this.GetType().Name}\n\tРодительская директория для работы с папками: {_parentCurrentDirectoryPath}" +
                             $"\n\tАдрес сервера ({(ServerAddressFromArgument != null ? "Передан аргументом обновляемой программой" : "Обновляемой программой аргументом не передан, получен из UpdaterService.json")}): {_serverAddress}" +
                             $"\n\tПуть к актуальной версии программы на сервере ({(ActualVersionPathFromArgument != null ? "Передан аргументом обновляемой программой" : "Обновляемой программой аргументом не передан, получен из UpdaterService.json")}): {actualVersionPath}" +
                             $"\n\tПолный путь к актуальной версии: {_sourceDirectoryPath}\n\tЦелевая папка для обновления: {_endDirectory}" +
                             $"\n\tИмя пользователя для входа ({(UserNameFromArgument != null ? "Передано аргументом обновляемой программой" : "Обновляемой программой аргументом не передано, получено из UpdaterService.json")}): {_userName}" +
                             $"\n\tПароль пользователя для входа ({(UserPasswordFromArgument != null ? "Передан аргументом обновляемой программой" : "Обновляемой программой аргументом не передан, получен из UpdaterService.json")}): {_userPassword}" +
                             $"\n\tПуть к файлу о версии внутри актуальной программы: {_aboutVersionPath}" +
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
            _thread = new Thread(Update);
            _thread.Start();
        }

        public void Update()
        {
            try
            {
                UpdateProcessIsRunning = true;
                DateTime startUpdateDateTime = DateTime.Now;
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
                    _wasConnected = true;
                    _logger.Info("Успешное подключение к серверу");

                    Message = "Получение данных об обновляемой версии…";
                    _logger.Info("Попытка получения номера обновляемой версии");
                    string oldVersion = string.Empty;
                    _partName = DeserializeJson($"{_parentCurrentDirectoryPath}\\{_endDirectory}\\{_aboutVersionPath}", _versionKey);
                    if (!string.IsNullOrWhiteSpace(_partName))
                    {
                        oldVersion = _partName;
                        _partName = _partName.Replace(".", "_");
                        _logger.Info($"Успешное получение номера обновляемой версии: {_partName}");
                    }
                    else
                    {
                        var currentDateTime = DateTime.Now;
                        _partName = $"UPD_{currentDateTime.Year}{currentDateTime.Month.ToString("D2")}{currentDateTime.Day.ToString("D2")}_{currentDateTime.Hour.ToString("D2")}{currentDateTime.Minute.ToString("D2")}";
                        _logger.Warn($"Не удалось получить номер обновляемой версии. Вместо номера обновляемой версии в названии при архивировании в папку со старыми версиями будет использоваться текущая дата и время: {_partName}");
                    }
                    _partName = $"_{_partName}";

                    Message = "Создание резервной копии…";
                    _logger.Info($"Попытка создания резервной копии для {_endDirectory}");
                    var copyResult = DirectoryCopy($"{_parentCurrentDirectoryPath}\\{_endDirectory}", $"{_parentCurrentDirectoryPath}\\{_endDirectory}{_partName}", false);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное создание резервной копии для {_endDirectory}. Резервная копия хранится в {_endDirectory}{_partName}");
                    }
                    else
                    {
                        _logger.Warn($"Неудачное создание резервной копии для {_endDirectory}. {copyResult}");
                    }

                    Message = "Получение данных о новой версии…";
                    _logger.Info("Попытка получения данных о новой версии");
                    WhatIsNew = DeserializeJson($"{_sourceDirectoryPath}\\{_aboutVersionPath}", _whatIsNewKey);
                    if (!string.IsNullOrWhiteSpace(WhatIsNew))
                    {
                        _logger.Info($"Успешное получение данных о том, что нового в актуальной версии: {WhatIsNew}");
                    }
                    else
                    {
                        WhatIsNew = "Исправлены ошибки, повышена стабильность работы";
                        _logger.Warn($"Не удалось получить данные о том, что нового в актуальной версии. Используется значение по умолчанию: {WhatIsNew}");
                    }
                    NewVersion = DeserializeJson($"{_sourceDirectoryPath}\\{_aboutVersionPath}", _versionKey);
                    if (!string.IsNullOrWhiteSpace(NewVersion))
                    {
                        _logger.Info($"Успешное получение номера актуальной версии: {NewVersion}");
                        if (!string.IsNullOrWhiteSpace(oldVersion) && string.Equals(oldVersion, NewVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception($"Версии программы в папке-источнике ({ _sourceDirectoryPath}) и целевой папке для обновления ({ _endDirectory}) совпадают. Возможно, новая версия программы хранится не в папке-источнике");
                        }
                        VisWhatIsNew = true;
                    }
                    else
                    {
                        _logger.Warn("Не удалось получить номер актуальной версии. Функция \"Что нового\" недоступна");
                    }

                    Message = "Подготовка к копированию…";
                    _logger.Info($"Подготовка копирования файлов с сервера {_sourceDirectoryPath} в {_parentCurrentDirectoryPath}\\{_endDirectory}");
                    var startCopyDateTime = DateTime.Now;
                    copyResult = DirectoryCopy(_sourceDirectoryPath, $"{_parentCurrentDirectoryPath}\\{_endDirectory}", true);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное завершение копирования файлов с сервера {_sourceDirectoryPath} в {_parentCurrentDirectoryPath}\\{_endDirectory}. Затраченное время на копирование: {(DateTime.Now - startCopyDateTime).TotalSeconds.ToString("F1")} сек.\n" +
                                     $"Удаленные файлы из целевой папки: {_deletedFiles}, пропущенные файлы при копировании: {_skippedFiles}, скопированные файлы (в том числе и с перезаписью): {_copiedFiles}");
                    }
                    else
                    {
                        throw new Exception($"Неудача копирования файлов с сервера. {copyResult}");
                    }
                }
                else
                {
                    UpdateProcessIsRunning = false;
                    EmergencySituation = true;
                    _logger.Error($"При попытке подключения к серверу возникла ошибка {errorConnectionMessage}. Процесс обновления прерван");
                    AlarmMessage = $"При попытке подключения к серверу возникла ошибка {errorConnectionMessage}";
                    EmergencyCompletion = true;
                    return;
                }

                Message = "Закрытие соединения…";
                _logger.Info("Попытка закрыть соединения");
                errorDisconnectMessage = DisconnectFromServer(_sourceDirectoryPath, false);
                _logger.Info($"Закрытие соединения: {errorDisconnectMessage ?? "Успешный разрыв соединения"}");

                ProgressValue = 99.9;
                Message = "Завершение обновления…";
                if (!string.IsNullOrWhiteSpace(_directoryForOldVersion))
                {
                    _logger.Info($"Подготовка архивирования папки с резервной копией {_endDirectory}{_partName} в папку {_directoryForOldVersion}");
                    var zipResult = ArchivingOldVersion($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_partName}", $"{_parentCurrentDirectoryPath}\\{_directoryForOldVersion}\\{_endDirectory}{_partName}.zip");
                    if (string.IsNullOrWhiteSpace(zipResult))
                    {
                        _logger.Info($"Успешное завершение архивирования папки с резервной копией {_endDirectory}{_partName} в {_directoryForOldVersion}\\{_endDirectory}{_partName}.zip");
                    }
                    else
                    {
                        _logger.Warn($"Неудача архивирования папки с резервной копией {_endDirectory}{_partName} в {_directoryForOldVersion}\\{_endDirectory}{_partName}.zip. {zipResult}");
                    }
                }
                else
                {
                    _logger.Warn($"Папка для хранения устаревших версий не задана в конфигурационном файле. Резервная копия {_endDirectory}{_partName} при её наличии будет удалена");
                }
                DirectoryDelete($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_partName}");

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

        private string ArchivingOldVersion(string sourceDirectoryPath, string destinZipPath)
        {
            try
            {
                var directory = new DirectoryInfo(sourceDirectoryPath);
                if (!directory.Exists)
                {
                    throw new DirectoryNotFoundException($"Исходной папки для архивирования ({sourceDirectoryPath}) не существует или не может быть найдена");
                }

                if (directory.GetFiles("*.*", SearchOption.AllDirectories).Length > 0)
                {
                    var spaceMessage = GetNotEnoughSpace(sourceDirectoryPath, false);
                    if (!string.IsNullOrWhiteSpace(spaceMessage))
                    {
                        throw new IOException(spaceMessage);
                    }

                    if (File.Exists(destinZipPath))
                    {
                        _logger.Trace($"Файл {destinZipPath} уже существует");
                        FileDelete(new FileInfo(destinZipPath));
                    }

                    Directory.CreateDirectory($"{_parentCurrentDirectoryPath}\\{_directoryForOldVersion}");
                    _logger.Info($"Начат процесс архивирования {sourceDirectoryPath} в {destinZipPath}");
                    ZipFile.CreateFromDirectory(sourceDirectoryPath, $"{destinZipPath}");
                    _logger.Info($"Успешное завершение архивирования {sourceDirectoryPath} в {destinZipPath}");

                    //В папке со старыми версиями оставляем только 2 последних архива
                    var zipFiles = new DirectoryInfo(new FileInfo(destinZipPath).Directory.FullName).GetFiles("*.zip");
                    if (zipFiles.Count() > 2)
                    {
                        _logger.Info("В папке со старыми версиями более 2 zip-файлов. Будут удалены все старые версии кроме 2 последних архивов");
                        DateTime firstDateTime = new DateTime();
                        DateTime secondDateTime = new DateTime();

                        //Поиск 2 последних использовавшихся архивов
                        foreach (var file in zipFiles)
                        {
                            if (file.LastWriteTime > firstDateTime)
                            {
                                secondDateTime = firstDateTime;
                                firstDateTime = file.LastWriteTime;
                            }
                        }

                        foreach (var file in zipFiles)
                        {
                            if (file.LastWriteTime < secondDateTime)
                            {
                                FileDelete(file);
                            }
                        }
                    }
                    return null;
                }
                throw new FileNotFoundException($"В исходной папке для архивирования ({sourceDirectoryPath}) не существуют файлы или не могут быть найдены");
            }
            catch (Exception ex)
            {
                return $"При создании архива {destinZipPath} для {sourceDirectoryPath} возникла ошибка. {ex.Message}";
            }
        }

        private string DirectoryCopy(string sourceDirectoryPath, string destinDirectoryPath, bool isCopyFromServer, bool сheckSpace = true)
        {
            try
            {
                var sourceDirectory = new DirectoryInfo(sourceDirectoryPath);
                if (!sourceDirectory.Exists)
                {
                    throw new DirectoryNotFoundException($"Исходной папки для копирования ({sourceDirectoryPath}) не существует или не может быть найдена");
                }
                var destinDirectory = new DirectoryInfo(destinDirectoryPath);

                //Один раз получаем общее количество файлов для копирования с сервера
                if (_totalFileCountFromServer == 0 && isCopyFromServer)
                {
                    _totalFileCountFromServer = sourceDirectory.GetFiles("*.*", SearchOption.AllDirectories).Length;

                    _logger.Info(
                        $"Общее количество файлов для копирования с папки-источника на сервере {sourceDirectoryPath}: {_totalFileCountFromServer}.\n{(destinDirectory.Exists ? $"Общее количество файлов в целевой папке {destinDirectory} до начала копирования: {destinDirectory.GetFiles("*.*", SearchOption.AllDirectories).Length}" : "Целевой папки до начала копирования не существует")}");
                }

                if (_totalFileCountFromServer > 0 || !isCopyFromServer)
                {
                    if (сheckSpace)
                    {
                        var spaceMessage = GetNotEnoughSpace(sourceDirectoryPath, isCopyFromServer);
                        if (!string.IsNullOrWhiteSpace(spaceMessage))
                        {
                            throw new IOException(spaceMessage);
                        }
                    }

                    if (isCopyFromServer)
                    {
                        Message = "Синхронизация каталогов…";
                    }
                    var filesToCopy = sourceDirectory.GetFiles();
                    var subDirectoriesToCopy = sourceDirectory.GetDirectories();
                    if (destinDirectory.Exists)
                    {
                        //Удаляем файлы и папки из целевой папки, если их нет в папке-источнике для копирования
                        var existFiles = destinDirectory.GetFiles();
                        foreach (var existFile in existFiles)
                        {
                            if (sourceDirectory.GetFiles(existFile.Name).Length == 0)
                            {
                                _logger.Trace($"Файл {existFile.Name} не найден в папке-источнике для копирования по такому относительному пути ({sourceDirectoryPath}\\{existFile.Name}) и будет удален");
                                FileDelete(existFile);
                                if (isCopyFromServer)
                                {
                                    _deletedFiles++;
                                }
                            }
                        }
                        var existsDirectories = destinDirectory.GetDirectories();
                        foreach (var existsDirectory in existsDirectories)
                        {
                            if (sourceDirectory.GetDirectories(existsDirectory.Name).Length == 0)
                            {
                                _logger.Trace($"Папка {existsDirectory.FullName} не найдена в папке-источнике для копирования по такому относительному пути ({sourceDirectoryPath}\\{existsDirectory.Name}) и будет удалена");
                                DirectoryDelete(existsDirectory.FullName);
                            }
                        }
                    }

                    Directory.CreateDirectory(destinDirectoryPath);
                    //Копируем из источника только те файлы, которых нет в целевой папке или которые отличаются по размеру и дате изменения
                    foreach (var fileToCopy in filesToCopy)
                    {
                        var existFile = destinDirectory.Exists ? destinDirectory.GetFiles(fileToCopy.Name) : null;
                        if (existFile?.Length > 0 && existFile.First().Length == fileToCopy.Length && existFile.First().LastWriteTimeUtc == fileToCopy.LastWriteTimeUtc)
                        {
                            _logger.Trace($"Пропущено копирование файла {fileToCopy.Name}. Данный файл с размером {fileToCopy.Length} и датой изменения (UTC) {fileToCopy.LastWriteTimeUtc} уже есть в каталоге {destinDirectoryPath} по такому относительному пути {destinDirectoryPath}\\{fileToCopy.Name}");
                            if (isCopyFromServer)
                            {
                                _skippedFiles++;
                            }
                        }
                        else
                        {
                            _logger.Trace($"Начато копирование {fileToCopy.Name}");
                            if (isCopyFromServer)
                            {
                                Message = $"Копирование {fileToCopy.Name}…";
                            }
                            var filePath = Path.Combine(destinDirectoryPath, fileToCopy.Name);
                            while (true)
                            {
                                try
                                {
                                    fileToCopy.CopyTo(filePath, true);
                                    _logger.Trace($"Завершено копирование {fileToCopy.Name}");
                                    if (isCopyFromServer)
                                    {
                                        _copiedFiles++;
                                    }
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _logger.Warn($"При попытке копирования или замены файла {fileToCopy.Name} возникла ошибка {ex.Message}");
                                    if (MessageBoxResult.OK == MessageBox.Show($"Ошибка доступа к файлу {fileToCopy.Name}.\nВозможно, файл используется.\n{ex.Message}.\n\nЗакройте файл и нажмите ОК для повтора.\nНажмите Отмена чтобы пропустить файл", "Ошибка доступа", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                                    {
                                        _logger.Trace($"Пользователем предпринята ещё одна попытка копирования или замены файла {fileToCopy.Name}");
                                    }
                                    else
                                    {
                                        _logger.Warn($"Пользователь отменил копирование или замену файла {fileToCopy.Name} при ошибке доступа");
                                        throw;
                                    }
                                }
                            }
                        }

                        if (isCopyFromServer)
                        {
                            _copiedFileCountFromServer++;
                            _copiedFileSizeFromServer += fileToCopy.Length;

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

                    foreach (var subDirectory in subDirectoriesToCopy)
                    {
                        var directoryPath = Path.Combine(destinDirectoryPath, subDirectory.Name);
                        DirectoryCopy(subDirectory.FullName, directoryPath, isCopyFromServer, false);
                    }
                    return null;
                }
                throw new FileNotFoundException($"В исходной папке для копирования ({sourceDirectoryPath}) не существуют файлы или не могут быть найдены");
            }
            catch (Exception ex)
            {
                if (isCopyFromServer)
                {
                    throw;
                }
                return $"При копировании файлов из {sourceDirectoryPath} в {destinDirectoryPath} возникла ошибка. {ex.Message}";
            }
        }

        private string GetNotEnoughSpace(string directoryPath, bool isCopyFromServer)
        {
            while (true)
            {
                try
                {
                    var drive = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory);
                    var userName = Environment.UserName;
                    _logger.Info($"Попытка определить достаточно ли места для текущего пользователя {userName} на текущем диске {drive} для работы с папкой {directoryPath}");
                    var freeSpaceOnCurrentDrive = new DriveInfo(drive).AvailableFreeSpace;
                    _logger.Trace($"Для текущего пользователя {userName} на текущем диске {drive} доступно {((double)freeSpaceOnCurrentDrive / 1024 / 1024).ToString("F2")} МБ ({freeSpaceOnCurrentDrive} Б)");
                    var needSpace = GetDirectorySize(directoryPath);
                    _totalFileSizeFromServer = isCopyFromServer ? needSpace : _totalFileSizeFromServer;
                    _logger.Trace($"Для успешной работы с файлами из папки {directoryPath} на текущем диске {drive} для текущего пользователя {userName} должно быть доступно не менее {((double)needSpace / 1024 / 1024).ToString("F2")} МБ ({needSpace} Б)");

                    if (freeSpaceOnCurrentDrive > needSpace)
                    {
                        _logger.Info($"На текущем диске {drive} для текущего пользователя {userName} достаточно места для работы с папкой {directoryPath}");
                        return null;
                    }

                    var message = $"На текущем диске {drive} для текущего пользователя {userName} недостаточно места для работы с папкой {directoryPath}. Необходимое количество памяти: не менее {((double)needSpace / 1024 / 1024).ToString("F2")} МБ ({needSpace} Б)";
                    _logger.Warn(message);
                    if (MessageBoxResult.OK == MessageBox.Show($"{message}.\n\nОсвободите место и нажмите ОК для повтора.\nНажмите Отмена для попытки завершить процесс с имеющимся пространством (Это может повлиять на успешность переноса данных)", "Недостаточно места на диске", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                    {
                        _logger.Trace($"Пользователем предпринята ещё одна попытка определить количество свободного пространства для работы с папкой {directoryPath}");
                    }
                    else
                    {
                        _logger.Warn($"Пользователь отменил работу с папкой {directoryPath} при недостаточном количестве свободного места на диске.");
                        return message;
                    }

                }
                catch (Exception ex)
                {
                    _logger.Warn($"При попытке определить достаточно ли места на диске для работы с папкой {directoryPath} возникла ошибка {ex.Message}. Работы с данной папкой может завершиться неудачей");
                    return null;
                }
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
                _logger.Trace($"Попытка десериализации JSON-файла {filePath} для ключа {keyName}");
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)[keyName];
                _logger.Trace($"Успешная десериализация JSON-файла {filePath} для ключа {keyName}");
            }
            catch (Exception ex)
            {
                result = null;
                _logger.Warn($"Ошибка десериализации JSON-файла {filePath} для ключа {keyName}: {ex.Message}");
            }
            return result;
        }

        private void FileDelete(FileInfo file)
        {
            if (file.Exists)
            {
                _logger.Trace($"Попытка удаления файла {file.FullName}");
                while (true)
                {
                    try
                    {
                        File.Delete(file.FullName);
                        _logger.Trace($"Успешное удаление файла {file.FullName}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"При попытке удаления файла {file.FullName} возникла ошибка {ex.Message}");
                        if (MessageBoxResult.OK == MessageBox.Show($"Ошибка доступа к файлу {file.FullName}.\nВозможно, файл используется.\n{ex.Message}.\n\nЗакройте файл и нажмите ОК для повтора.\nНажмите Отмена чтобы пропустить файл", "Ошибка доступа", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                        {
                            _logger.Trace($"Пользователем предпринята ещё одна попытка удаления файла {file.FullName}");
                        }
                        else
                        {
                            _logger.Warn($"Пользователь отменил удаление файла {file.FullName} при ошибке доступа");
                            throw;
                        }
                    }
                }
            }
        }

        private void DirectoryDelete(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                _logger.Trace($"Попытка удаления папки {directoryPath}");
                while (true)
                {
                    try
                    {
                        Directory.Delete(directoryPath, true);
                        _logger.Trace($"Успешное удаление папки {directoryPath}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"При попытке удаления папки {directoryPath} возникла ошибка {ex.Message}");
                        if (MessageBoxResult.OK == MessageBox.Show($"Ошибка доступа к папке {directoryPath}.\nВозможно, папка открыта или используется файл из папки.\n{ex.Message}.\n\nЗакройте папку и открытые файлы из папки и нажмите ОК для повтора.\nНажмите Отмена для попытки завершить процесс без доступа к папке (Это может повлиять на успешность переноса данных)", "Ошибка доступа", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly))
                        {
                            _logger.Trace($"Пользователем предпринята ещё одна попытка удаления папки {directoryPath}");
                        }
                        else
                        {
                            _logger.Warn($"Пользователь отменил удаление папки {directoryPath} при ошибке доступа");
                            throw;
                        }
                    }
                }
            }
        }

        private string ClearLogs(string directoryPath)
        {
            try
            {
                var directory = new DirectoryInfo(directoryPath);
                if (directory.Exists)
                {
                    var files = directory.GetFiles("*.log");
                    var lastWriteDateTime = DateTime.Now;
                    foreach (var file in files)
                    {
                        if (file.LastWriteTime < lastWriteDateTime)
                        {
                            lastWriteDateTime = file.LastWriteTime;
                        }
                    }

                    if (files.Length > 0 && lastWriteDateTime < DateTime.Now.AddDays(-10))
                    {
                        foreach (var file in files)
                        {
                            FileDelete(file);
                        }
                        return $"Успешная очистка логов в папке {directoryPath}. Последняя запись в логах до чистки: {lastWriteDateTime}";
                    }
                    return $"Чистка логов в папке {directoryPath} не требуется ({(files.Length > 0 ? "Дата последней записи не превышает 10 дней" : "Файлы не найдены")})";
                }
                return $"Очистка логов не требуется. Папка с логами ({directoryPath}) не найдена";
            }
            catch (Exception ex)
            {
                return $"При попытке очистки логов в папке {directoryPath} возникла ошибка. {ex.Message}";
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
                if (_wasConnected)
                {
                    _logger.Info($"Попытка восстановить резервную копию {_endDirectory} из {_endDirectory}{_partName}");
                    var copyResult = DirectoryCopy($"{_parentCurrentDirectoryPath}\\{_endDirectory}{_partName}", $"{_parentCurrentDirectoryPath}\\{_endDirectory}", false);
                    if (string.IsNullOrWhiteSpace(copyResult))
                    {
                        _logger.Info($"Успешное восстановление резервной копии {_endDirectory} из {_endDirectory}{_partName}");
                        DirectoryDelete($"{ _parentCurrentDirectoryPath}\\{_endDirectory}{_partName}");
                    }
                    else
                    {
                        _logger.Warn($"Неудачное восстановление резервной копии {_endDirectory} из {_endDirectory}{_partName}. {copyResult}");
                    }
                }
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

                if (File.Exists(shortcutLocation))
                {
                    FileDelete(new FileInfo(shortcutLocation));
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
                    _logger.Info("Закрытие программы обновления прервано: отмена изменений не окончена");
                    MessageBox.Show("Дождитесь окончания отмены изменений", "Дождитесь окончания отмены изменений", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    return;
                }

                _logger.Info("Программа обновления закрыта");
                LogManager.DisableLogging();
                DirectoryCopy($"{AppDomain.CurrentDomain.BaseDirectory}Logs", $"{_parentCurrentDirectoryPath}\\{_endDirectory}\\AsuUpdater\\Logs", false, false);
            }
            else
            {
                if (MessageBoxResult.OK == MessageBox.Show("Процесс обновления не завершен.\n\nОтменить обновление?", "Процесс прерывания обновления", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel, MessageBoxOptions.DefaultDesktopOnly))
                {
                    UpdateProcessIsRunning = false;
                    _thread.Abort();//Прерываем поток
                    _thread.Join(1500);//Таймаут на завершение
                    _thread = null;
                    EmergencyStop();
                    _logger.Info("Программа обновления закрыта");
                    LogManager.DisableLogging();
                    DirectoryCopy($"{AppDomain.CurrentDomain.BaseDirectory}Logs", $"{_parentCurrentDirectoryPath}\\{_endDirectory}\\AsuUpdater\\Logs", false, false);
                }
                else
                {
                    _logger.Info("Закрытие программы отменено пользователем");
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
