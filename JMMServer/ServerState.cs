using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.ComponentModel;
using JMMServer.Providers.Azure;

namespace JMMServer
{
	public class ServerState : INotifyPropertyChanged
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private static ServerState _instance;
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

		public ServerState()
		{
			
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, e);
			}
		}

		private bool databaseAvailable = false;
		public bool DatabaseAvailable
		{
			get { return databaseAvailable; }
			set
			{
				databaseAvailable = value;
				OnPropertyChanged(new PropertyChangedEventArgs("DatabaseAvailable"));
			}
		}

		private bool serverOnline = false;
		public bool ServerOnline
		{
			get { return serverOnline; }
			set
			{
				serverOnline = value;
				OnPropertyChanged(new PropertyChangedEventArgs("ServerOnline"));
			}
		}

        

		private string currentSetupStatus = "";
		public string CurrentSetupStatus
		{
			get { return currentSetupStatus; }
			set
			{
				currentSetupStatus = value;
				OnPropertyChanged(new PropertyChangedEventArgs("CurrentSetupStatus"));
			}
		}

		private bool databaseIsSQLite = false;
		public bool DatabaseIsSQLite
		{
			get { return databaseIsSQLite; }
			set
			{
				databaseIsSQLite = value;
				OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsSQLite"));
			}
		}

		private bool databaseIsSQLServer = false;
		public bool DatabaseIsSQLServer
		{
			get { return databaseIsSQLServer; }
			set
			{
				databaseIsSQLServer = value;
				OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsSQLServer"));
			}
		}

		private bool databaseIsMySQL = false;
		public bool DatabaseIsMySQL
		{
			get { return databaseIsMySQL; }
			set
			{
				databaseIsMySQL = value;
				OnPropertyChanged(new PropertyChangedEventArgs("DatabaseIsMySQL"));
			}
		}

		private string baseImagePath = "";
		public string BaseImagePath
		{
			get { return baseImagePath; }
			set
			{
				baseImagePath = value;
				OnPropertyChanged(new PropertyChangedEventArgs("BaseImagePath"));
			}
		}

		private bool newVersionAvailable = false;
		public bool NewVersionAvailable
		{
			get { return newVersionAvailable; }
			set
			{
				newVersionAvailable = value;
				OnPropertyChanged(new PropertyChangedEventArgs("NewVersionAvailable"));
			}
		}

		private string newVersionNumber = "";
		public string NewVersionNumber
		{
			get { return newVersionNumber; }
			set
			{
				newVersionNumber = value;
				OnPropertyChanged(new PropertyChangedEventArgs("NewVersionNumber"));
			}
		}

		private string newVersionDownloadLink = "";
		public string NewVersionDownloadLink
		{
			get { return newVersionDownloadLink; }
			set
			{
				newVersionDownloadLink = value;
				OnPropertyChanged(new PropertyChangedEventArgs("NewVersionDownloadLink"));
			}
		}

		private string applicationVersion = "";
		public string ApplicationVersion
		{
			get { return applicationVersion; }
			set
			{
				applicationVersion = value;
				OnPropertyChanged(new PropertyChangedEventArgs("ApplicationVersion"));
			}
		}

        private string applicationVersionLatest = "";
        public string ApplicationVersionLatest
        {
            get { return applicationVersionLatest; }
            set
            {
                applicationVersionLatest = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ApplicationVersionLatest"));
            }
        }

        private string aniDB_Username = "";
		public string AniDB_Username
		{
			get { return aniDB_Username; }
			set
			{
				aniDB_Username = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_Username"));
			}
		}

		private string aniDB_Password = "";
		public string AniDB_Password
		{
			get { return aniDB_Password; }
			set
			{
				aniDB_Password = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_Password"));
			}
		}

		private string aniDB_ServerAddress = "";
		public string AniDB_ServerAddress
		{
			get { return aniDB_ServerAddress; }
			set
			{
				aniDB_ServerAddress = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ServerAddress"));
			}
		}

		private string aniDB_ServerPort = "";
		public string AniDB_ServerPort
		{
			get { return aniDB_ServerPort; }
			set
			{
				aniDB_ServerPort = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ServerPort"));
			}
		}

		private string aniDB_ClientPort = "";
		public string AniDB_ClientPort
		{
			get { return aniDB_ClientPort; }
			set
			{
				aniDB_ClientPort = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_ClientPort"));
			}
		}

		private string aniDB_TestStatus = "";
		public string AniDB_TestStatus
		{
			get { return aniDB_TestStatus; }
			set
			{
				aniDB_TestStatus = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AniDB_TestStatus"));
			}
		}

		private bool minOnStartup = false;
		public bool MinOnStartup
		{
			get { return minOnStartup; }
			set
			{
				minOnStartup = value;
				OnPropertyChanged(new PropertyChangedEventArgs("MinOnStartup"));
			}
		}

		private bool maxOnStartup = true;
		public bool MaxOnStartup
		{
			get { return maxOnStartup; }
			set
			{
				maxOnStartup = value;
				OnPropertyChanged(new PropertyChangedEventArgs("MaxOnStartup"));
			}
		}

		private bool disallowMultipleInstances = false;
		public bool DisallowMultipleInstances
		{
			get { return disallowMultipleInstances; }
			set
			{
				disallowMultipleInstances = value;
				OnPropertyChanged(new PropertyChangedEventArgs("DisallowMultipleInstances"));
			}
		}

		private bool allowMultipleInstances = true;
		public bool AllowMultipleInstances
		{
			get { return allowMultipleInstances; }
			set
			{
				allowMultipleInstances = value;
				OnPropertyChanged(new PropertyChangedEventArgs("AllowMultipleInstances"));
			}
		}

		private string vLCLocation = "";
		public string VLCLocation
		{
			get { return vLCLocation; }
			set
			{
				vLCLocation = value;
				OnPropertyChanged(new PropertyChangedEventArgs("VLCLocation"));
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
