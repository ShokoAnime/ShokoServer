using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.ComponentModel;

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

	}
}
