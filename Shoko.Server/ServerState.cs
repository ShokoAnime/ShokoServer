using System.Collections.Generic;
using System.ComponentModel;
using NLog;
using Microsoft.Win32;
using System;
using Microsoft.Win32.TaskScheduler;
using NutzCode.CloudFileSystem;
using Shoko.Commons.Notification;
using Shoko.Models;
using Shoko.Models.Enums;

namespace Shoko.Server
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
            get { return databaseAvailable; }
            set { this.SetField(() => databaseAvailable, value); }
        }

        private bool serverOnline = false;

        public bool ServerOnline
        {
            get { return serverOnline; }
            set { this.SetField(() => serverOnline, value); }
        }


        private string currentSetupStatus = "";

        public string CurrentSetupStatus
        {
            get { return currentSetupStatus; }
            set { this.SetField(() => currentSetupStatus, value); }
        }

        private bool databaseIsSQLite = false;

        public bool DatabaseIsSQLite
        {
            get { return databaseIsSQLite; }
            set { this.SetField(() => databaseIsSQLite, value); }
        }

        private bool databaseIsSQLServer = false;

        public bool DatabaseIsSQLServer
        {
            get { return databaseIsSQLServer; }
            set { this.SetField(() => databaseIsSQLServer, value); }
        }

        private bool databaseIsMySQL = false;

        public bool DatabaseIsMySQL
        {
            get { return databaseIsMySQL; }
            set { this.SetField(() => databaseIsMySQL, value); }
        }

        private string baseImagePath = "";

        public string BaseImagePath
        {
            get { return baseImagePath; }
            set { this.SetField(() => baseImagePath, value); }
        }

        private bool newVersionAvailable = false;

        public bool NewVersionAvailable
        {
            get { return newVersionAvailable; }
            set { this.SetField(() => newVersionAvailable, value); }
        }

        private string newVersionNumber = "";

        public string NewVersionNumber
        {
            get { return newVersionNumber; }
            set { this.SetField(() => newVersionNumber, value); }
        }

        private string newVersionDownloadLink = "";

        public string NewVersionDownloadLink
        {
            get { return newVersionDownloadLink; }
            set { this.SetField(() => newVersionDownloadLink, value); }
        }

        private string applicationVersion = "";

        public string ApplicationVersion
        {
            get { return applicationVersion; }
            set { this.SetField(() => applicationVersion, value); }
        }

        private string applicationVersionExtra = "";

        public string ApplicationVersionExtra
        {
            get { return applicationVersionExtra; }
            set { this.SetField(() => applicationVersionExtra, value); }
        }

        private string applicationVersionLatest = "";

        public string ApplicationVersionLatest
        {
            get { return applicationVersionLatest; }
            set { this.SetField(() => applicationVersionLatest, value); }
        }

        private string aniDB_Username = "";

        public string AniDB_Username
        {
            get { return aniDB_Username; }
            set { this.SetField(() => aniDB_Username, value); }
        }

        private string aniDB_Password = "";

        public string AniDB_Password
        {
            get { return aniDB_Password; }
            set { this.SetField(() => aniDB_Password, value); }
        }

        private string aniDB_ServerAddress = "";

        public string AniDB_ServerAddress
        {
            get { return aniDB_ServerAddress; }
            set { this.SetField(() => aniDB_ServerAddress, value); }
        }

        private string aniDB_ServerPort = "";

        public string AniDB_ServerPort
        {
            get { return aniDB_ServerPort; }
            set { this.SetField(() => aniDB_ServerPort, value); }
        }

        private string aniDB_ClientPort = "";

        public string AniDB_ClientPort
        {
            get { return aniDB_ClientPort; }
            set { this.SetField(() => aniDB_ClientPort, value); }
        }

        private string aniDB_TestStatus = "";

        public string AniDB_TestStatus
        {
            get { return aniDB_TestStatus; }
            set { this.SetField(() => aniDB_TestStatus, value); }
        }

        private bool minOnStartup = false;

        public bool MinOnStartup
        {
            get { return minOnStartup; }
            set { this.SetField(() => minOnStartup, value); }
        }

        private bool maxOnStartup = true;

        public bool MaxOnStartup
        {
            get { return maxOnStartup; }
            set { this.SetField(() => maxOnStartup, value); }
        }


        private string vLCLocation = "";

        public string VLCLocation
        {
            get { return vLCLocation; }
            set { this.SetField(() => vLCLocation, value); }
        }

        private bool isAutostartEnabled = false;

        public bool IsAutostartEnabled
        {
            get { return isAutostartEnabled; }
            set { this.SetField(() => isAutostartEnabled, value); }
        }

        private bool isAutostartDisabled = false;

        public bool IsAutostartDisabled
        {
            get { return isAutostartDisabled; }
            set { this.SetField(() => isAutostartDisabled, value); }
        }

        /* Swith this to "Registry" when we no longer need elevated run level */
        public readonly AutostartMethod autostartMethod = AutostartMethod.Registry;

        public readonly string autostartTaskName = "JMMServer";

        public readonly string autostartKey = "JMMServer";

        public RegistryKey autostartRegistryKey
        {
            get { return Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true); }
        }


        public Dictionary<string, IFileSystem> ConnectedFileSystems { get; private set; }


        public void LoadSettings()
        {
            AniDB_Username = ServerSettings.AniDB_Username;
            AniDB_Password = ServerSettings.AniDB_Password;
            AniDB_ServerAddress = ServerSettings.AniDB_ServerAddress;
            AniDB_ServerPort = ServerSettings.AniDB_ServerPort;
            AniDB_ClientPort = ServerSettings.AniDB_ClientPort;

            MinOnStartup = ServerSettings.MinimizeOnStartup;
            MaxOnStartup = !ServerSettings.MinimizeOnStartup;


            VLCLocation = ServerSettings.VLCLocation;

            if (autostartMethod == AutostartMethod.Registry)
            {
                try
                {
                    IsAutostartEnabled = autostartRegistryKey.GetValue(autostartKey) != null;
                    IsAutostartDisabled = !isAutostartEnabled;
                }
                catch (Exception ex)
                {
                    logger.DebugException("Unable to get autostart registry value", ex);
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
    }
}