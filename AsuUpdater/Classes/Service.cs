using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NLog;

namespace AsuUpdater.Classes
{
    public class Service
    {
        private static Service _instance;
        private static Dictionary<string, string> _serviceDict = new Dictionary<string, string>();
        private static Logger _logger;

        private Service()
        {
            _logger = LogManager.GetCurrentClassLogger();
            Parsing();
        }

        private void Parsing()
        {
            using (StreamReader sr = new StreamReader($"{AppDomain.CurrentDomain.BaseDirectory}\\Data\\UpdaterService.json"))
            {
                string json = sr.ReadToEnd();
                _serviceDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
        }

        public Dictionary<string, string> GetServiceDict()
        {
            return _serviceDict;
        }

        public Logger GetLogger()
        {
            return _logger;
        }

        public static Service GetInstance()
        {
            if (_instance == null)
            {
                _instance = new Service();
            }
            return _instance;
        }
    }
}
