using System.ComponentModel;
using NLog;

namespace JMMServer
{
    public class ServerState : INotifyPropertyChanged
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static ServerState _instance;

        private bool allowMultipleInstances = true;

        private string aniDB_ClientPort = "";

        private string aniDB_Password = "";

        private string aniDB_ServerAddress = "";

        private string aniDB_ServerPort = "";

        private string aniDB_TestStatus = "";

        private string aniDB_Username = "";

        private string applicationVersion = "";

        private string applicationVersionLatest = "";

        private string baseImagePath = "";


        private string currentSetupStatus = "";

        private bool databaseAvailable;

        private bool databaseIsMySQL;

        private bool databaseIsSQLite;

        private bool databaseIsSQLServer;

        private bool disallowMultipleInstances;

        private bool maxOnStartup = true;

        private bool minOnStartup;

        private bool newVersionAvailable;

        private string newVersionDownloadLink = "";

        private string newVersionNumber = "";

        private bool serverOnline;

        private string vLCLocation = "";

        public static ServerState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ServerState();
                }
                return _instance;
            }
        }

        public bool DatabaseAvailable
        {
            get { return databaseAvailable; }
            set
            {
                databaseAvailable = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseAvailable"));
            }
        }

        public bool ServerOnline
        {
            get { return serverOnline; }
            set
            {
                serverOnline = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ServerOnline"));
            }
        }

        public string CurrentSetupStatus
        {
            get { return currentSetupStatus; }
            set
            {
                currentSetupStatus = value;
                OnPropertyChanged(new PropertyChangedEventArgs("CurrentSetupStatus"));
            }
        }

        public bool DatabaseIsSQLite
        {
            get { return databaseIsSQLite; }
            set
            {
                databaseIsSQLite = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsSQLite"));
            }
        }

        public bool DatabaseIsSQLServer
        {
            get { return databaseIsSQLServer; }
            set
            {
                databaseIsSQLServer = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsSQLServer"));
            }
        }

        public bool DatabaseIsMySQL
        {
            get { return databaseIsMySQL; }
            set
            {
                databaseIsMySQL = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsMySQL"));
            }
        }

        public string BaseImagePath
        {
            get { return baseImagePath; }
            set
            {
                baseImagePath = value;
                OnPropertyChanged(new PropertyChangedEventArgs("BaseImagePath"));
            }
        }

        public bool NewVersionAvailable
        {
            get { return newVersionAvailable; }
            set
            {
                newVersionAvailable = value;
                OnPropertyChanged(new PropertyChangedEventArgs("NewVersionAvailable"));
            }
        }

        public string NewVersionNumber
        {
            get { return newVersionNumber; }
            set
            {
                newVersionNumber = value;
                OnPropertyChanged(new PropertyChangedEventArgs("NewVersionNumber"));
            }
        }

        public string NewVersionDownloadLink
        {
            get { return newVersionDownloadLink; }
            set
            {
                newVersionDownloadLink = value;
                OnPropertyChanged(new PropertyChangedEventArgs("NewVersionDownloadLink"));
            }
        }

        public string ApplicationVersion
        {
            get { return applicationVersion; }
            set
            {
                applicationVersion = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ApplicationVersion"));
            }
        }

        public string ApplicationVersionLatest
        {
            get { return applicationVersionLatest; }
            set
            {
                applicationVersionLatest = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ApplicationVersionLatest"));
            }
        }

        public string AniDB_Username
        {
            get { return aniDB_Username; }
            set
            {
                aniDB_Username = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_Username"));
            }
        }

        public string AniDB_Password
        {
            get { return aniDB_Password; }
            set
            {
                aniDB_Password = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_Password"));
            }
        }

        public string AniDB_ServerAddress
        {
            get { return aniDB_ServerAddress; }
            set
            {
                aniDB_ServerAddress = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ServerAddress"));
            }
        }

        public string AniDB_ServerPort
        {
            get { return aniDB_ServerPort; }
            set
            {
                aniDB_ServerPort = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ServerPort"));
            }
        }

        public string AniDB_ClientPort
        {
            get { return aniDB_ClientPort; }
            set
            {
                aniDB_ClientPort = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ClientPort"));
            }
        }

        public string AniDB_TestStatus
        {
            get { return aniDB_TestStatus; }
            set
            {
                aniDB_TestStatus = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AniDB_TestStatus"));
            }
        }

        public bool MinOnStartup
        {
            get { return minOnStartup; }
            set
            {
                minOnStartup = value;
                OnPropertyChanged(new PropertyChangedEventArgs("MinOnStartup"));
            }
        }

        public bool MaxOnStartup
        {
            get { return maxOnStartup; }
            set
            {
                maxOnStartup = value;
                OnPropertyChanged(new PropertyChangedEventArgs("MaxOnStartup"));
            }
        }

        public bool DisallowMultipleInstances
        {
            get { return disallowMultipleInstances; }
            set
            {
                disallowMultipleInstances = value;
                OnPropertyChanged(new PropertyChangedEventArgs("DisallowMultipleInstances"));
            }
        }

        public bool AllowMultipleInstances
        {
            get { return allowMultipleInstances; }
            set
            {
                allowMultipleInstances = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AllowMultipleInstances"));
            }
        }

        public string VLCLocation
        {
            get { return vLCLocation; }
            set
            {
                vLCLocation = value;
                OnPropertyChanged(new PropertyChangedEventArgs("VLCLocation"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void LoadSettings()
        {
            AniDB_Username = ServerSettings.AniDB_Username;
            AniDB_Password = ServerSettings.AniDB_Password;
            AniDB_ServerAddress = ServerSettings.AniDB_ServerAddress;
            AniDB_ServerPort = ServerSettings.AniDB_ServerPort;
            AniDB_ClientPort = ServerSettings.AniDB_ClientPort;

            MinOnStartup = ServerSettings.MinimizeOnStartup;
            MaxOnStartup = !ServerSettings.MinimizeOnStartup;

            AllowMultipleInstances = ServerSettings.AllowMultipleInstances;
            DisallowMultipleInstances = !ServerSettings.AllowMultipleInstances;

            VLCLocation = ServerSettings.VLCLocation;
        }
    }
}