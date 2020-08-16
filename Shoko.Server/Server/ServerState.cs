using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using NLog;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Models.Enums;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Server
{
    public class ServerState : INotifyPropertyChangedExt
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static ServerState _instance;

        public static ServerState Instance => _instance ?? (_instance = new ServerState());

        public ServerState()
        {
            ConnectedFileSystems = new Dictionary<string, IFileSystem>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }

        private bool databaseAvailable = false;

        public bool DatabaseAvailable
        {
            get => databaseAvailable;
            set => this.SetField(() => databaseAvailable, value);
        }

        private bool serverOnline = false;

        public bool ServerOnline
        {
            get => serverOnline;
            set => this.SetField(() => serverOnline, value);
        }

        private bool serverStarting = false;

        public bool ServerStarting
        {
            get => serverStarting;
            set => this.SetField(() => serverStarting, value);
        }

        private string _serverStartingStatus = string.Empty;

        public string ServerStartingStatus
        {
            get => _serverStartingStatus;
            set
            {
                if (!value.Equals(_serverStartingStatus))
                {
                    logger.Trace($"Starting Server: {value}");
                }
                this.SetField(() => _serverStartingStatus, value);
            }
        }

        private bool databaseIsSQLite = false;

        public bool DatabaseIsSQLite
        {
            get => databaseIsSQLite;
            set => this.SetField(() => databaseIsSQLite, value);
        }

        private bool databaseIsSQLServer = false;

        public bool DatabaseIsSQLServer
        {
            get => databaseIsSQLServer;
            set => this.SetField(() => databaseIsSQLServer, value);
        }

        private bool databaseIsMySQL = false;

        public bool DatabaseIsMySQL
        {
            get => databaseIsMySQL;
            set => this.SetField(() => databaseIsMySQL, value);
        }

        private string baseImagePath = string.Empty;

        public string BaseImagePath
        {
            get => baseImagePath;
            set => this.SetField(() => baseImagePath, value);
        }

        private bool newVersionAvailable = false;

        public bool NewVersionAvailable
        {
            get => newVersionAvailable;
            set => this.SetField(() => newVersionAvailable, value);
        }

        private string newVersionNumber = string.Empty;

        public string NewVersionNumber
        {
            get => newVersionNumber;
            set => this.SetField(() => newVersionNumber, value);
        }

        private string newVersionDownloadLink = string.Empty;

        public string NewVersionDownloadLink
        {
            get => newVersionDownloadLink;
            set => this.SetField(() => newVersionDownloadLink, value);
        }

        private string applicationVersion = string.Empty;

        public string ApplicationVersion
        {
            get => applicationVersion;
            set => this.SetField(() => applicationVersion, value);
        }

        private string applicationVersionExtra = string.Empty;

        public string ApplicationVersionExtra
        {
            get => applicationVersionExtra;
            set => this.SetField(() => applicationVersionExtra, value);
        }

        private string applicationVersionLatest = string.Empty;

        public string ApplicationVersionLatest
        {
            get => applicationVersionLatest;
            set => this.SetField(() => applicationVersionLatest, value);
        }

        private string aniDB_Username = string.Empty;

        public string AniDB_Username
        {
            get => aniDB_Username;
            set => this.SetField(() => aniDB_Username, value);
        }

        private string aniDB_Password = string.Empty;

        public string AniDB_Password
        {
            get => aniDB_Password;
            set => this.SetField(() => aniDB_Password, value);
        }

        private string aniDB_ServerAddress = string.Empty;

        public string AniDB_ServerAddress
        {
            get => aniDB_ServerAddress;
            set => this.SetField(() => aniDB_ServerAddress, value);
        }

        private string aniDB_ServerPort = string.Empty;

        public string AniDB_ServerPort
        {
            get => aniDB_ServerPort;
            set => this.SetField(() => aniDB_ServerPort, value);
        }

        private string aniDB_ClientPort = string.Empty;

        public string AniDB_ClientPort
        {
            get => aniDB_ClientPort;
            set => this.SetField(() => aniDB_ClientPort, value);
        }

        private string aniDB_TestStatus = string.Empty;

        public string AniDB_TestStatus
        {
            get => aniDB_TestStatus;
            set => this.SetField(() => aniDB_TestStatus, value);
        }

        private bool isAutostartEnabled = false;

        public bool IsAutostartEnabled
        {
            get => isAutostartEnabled;
            set => this.SetField(() => isAutostartEnabled, value);
        }

        private bool isAutostartDisabled = false;

        public bool IsAutostartDisabled
        {
            get => isAutostartDisabled;
            set => this.SetField(() => isAutostartDisabled, value);
        }

        private bool startupFailed = false;

        public bool StartupFailed
        {
            get => startupFailed;
            set => this.SetField(() => startupFailed, value);
        }

        private string startupFailedMessage = string.Empty;

        public string StartupFailedMessage
        {
            get => startupFailedMessage;
            set => this.SetField(() => startupFailedMessage, value);
        }

        private DatabaseBlockedInfo databaseBlocked = new DatabaseBlockedInfo();

        public DatabaseBlockedInfo DatabaseBlocked
        {
            get => databaseBlocked;
            set => this.SetField(() => databaseBlocked, value);
        }

        private bool apiInUse = false;

        public bool ApiInUse
        {
            get => apiInUse;
            set => this.SetField(() => apiInUse, value);
        }

        /* Swith this to "Registry" when we no longer need elevated run level */
        public readonly AutostartMethod autostartMethod = AutostartMethod.Registry;

        public readonly string autostartTaskName = "JMMServer";

        public readonly string autostartKey = "JMMServer";

        public RegistryKey AutostartRegistryKey => Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);


        public Dictionary<string, IFileSystem> ConnectedFileSystems { get; private set; }


        public void LoadSettings()
        {
            AniDB_Username = ServerSettings.Instance.AniDb.Username;
            AniDB_Password = ServerSettings.Instance.AniDb.Password;
            AniDB_ServerAddress = ServerSettings.Instance.AniDb.ServerAddress;
            AniDB_ServerPort = ServerSettings.Instance.AniDb.ServerPort.ToString();
            AniDB_ClientPort = ServerSettings.Instance.AniDb.ClientPort.ToString();

            if (Utils.IsRunningOnLinuxOrMac()) return;
            
            if (autostartMethod == AutostartMethod.Registry)
            {
                try
                {
                    IsAutostartEnabled = AutostartRegistryKey.GetValue(autostartKey) != null;
                    IsAutostartDisabled = !isAutostartEnabled;
                }
                catch (Exception ex)
                {
                    logger.Debug(ex, "Unable to get autostart registry value");
                }
            }
            else if (autostartMethod == AutostartMethod.TaskScheduler)
            {
                Task task = TaskService.Instance.GetTask(autostartTaskName);
                if (task != null && task.State != TaskState.Disabled)
                {
                    IsAutostartEnabled = true;
                }
                else
                {
                    IsAutostartEnabled = false;
                }
                IsAutostartDisabled = !isAutostartEnabled;
            }
        }

        public class DatabaseBlockedInfo
        {
            /// <summary>
            /// Out of 100, the progress percentage, if available
            /// </summary>
            public double? Progress { get; set; }

            /// <summary>
            /// Whether the system is blocked or not
            /// </summary>
            public bool Blocked { get; set; }

            /// <summary>
            /// A message about the blocked state
            /// </summary>
            public string Status { get; set; }
        }
    }
}