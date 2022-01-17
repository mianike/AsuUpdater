using System;

namespace AsuUpdater.Classes
{
    public class Release
    {
        private static Version _version;
        private static Release _instance;

        private readonly DateTime _buildDateTime;
        private readonly string _currentVersion;
        private readonly string _copyright;
        private readonly string _releaseTitle;

        private Release()
        {
            _buildDateTime = new DateTime(2000, 1, 1).AddDays(_version.Build).AddSeconds(_version.Revision * 2);
            _currentVersion = "v." + _version.Major + "." + _version.Minor + "." + _buildDateTime.Year + _buildDateTime.Month.ToString("D2") + _buildDateTime.Day.ToString("D2") + "." + _buildDateTime.Hour.ToString("D2") + _buildDateTime.Minute.ToString("D2");
            _copyright = "© ООО «‎АСУ-Техно»‎, " + _buildDateTime.Year;
            _releaseTitle = _currentVersion + " " + _copyright;
        }

        public DateTime GetBuildDateTime()
        {
            return _buildDateTime;
        }

        public string GetVersion()
        {
            return _currentVersion;
        }

        public string GetCopyright()
        {
            return _copyright;
        }

        public string GetReleaseTitle()
        {
            return _releaseTitle;
        }

        public static void Init(Version version)
        {
            if (_version == null)
            {
                _version = version;
            }
            else
            {
                throw new ArgumentException("Release is already inited.");
            }
        }

        public static Release GetInstance()
        {
            if (_instance == null)
            {
                if (_version != null)
                {
                    _instance = new Release();
                }
                else
                {
                    throw new ArgumentException("Release is not inited.");
                }
            }
            return _instance;
        }
    }
}
