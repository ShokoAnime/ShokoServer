using System.ComponentModel;
using NLog;
using Shoko.Server.Utilities;

namespace Shoko.Server.Server;

public class ServerState : INotifyPropertyChangedExt
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static ServerState Instance { get; } = new();

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

    private string serverStartingStatus = string.Empty;

    public string ServerStartingStatus
    {
        get => serverStartingStatus;
        set
        {
            if (!value.Equals(serverStartingStatus))
            {
                _logger.Trace($"Starting Server: {value}");
            }

            this.SetField(() => serverStartingStatus, value);
        }
    }

    private string applicationVersion = string.Empty;

    public string ApplicationVersion
    {
        get => applicationVersion;
        set => this.SetField(() => applicationVersion, value);
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

    private DatabaseBlockedInfo databaseBlocked = new();

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
