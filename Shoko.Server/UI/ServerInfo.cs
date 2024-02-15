using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Commons.Notification;
using Shoko.Commons.Properties;
using Shoko.Models.Azure;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Utils = Shoko.Server.Utilities.Utils;

namespace Shoko.Server;

public class ServerInfo : INotifyPropertyChangedExt
{
    private static ServerInfo _instance;

    public static ServerInfo Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ServerInfo();
            }

            return _instance;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void NotifyPropertyChanged(string propname)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
    }


    private ServerInfo()
    {
        ImportFolders = new AsyncObservableCollection<SVR_ImportFolder>();
        AdminMessages = new AsyncObservableCollection<Azure_AdminMessage>();

        var http = Utils.ServiceContainer.GetRequiredService<IHttpConnectionHandler>();
        var udp = Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        http.AniDBStateUpdate += OnAniDBStateUpdate;
        udp.AniDBStateUpdate += OnAniDBStateUpdate;
    }

    ~ServerInfo()
    {
        var http = Utils.ServiceContainer.GetRequiredService<IHttpConnectionHandler>();
        var udp = Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
        http.AniDBStateUpdate -= OnAniDBStateUpdate;
        udp.AniDBStateUpdate -= OnAniDBStateUpdate;
    }

    private void OnAniDBStateUpdate(object sender, AniDBStateUpdate e)
    {
        switch (e.UpdateType)
        {
            case UpdateType.None:
                // We might use this somehow, but currently not fired 
                break;
            case UpdateType.UDPBan:
                if (e.Value == IsUDPBanned) return;
                if (e.Value)
                {
                    IsUDPBanned = true;
                    UDPBanTime = e.UpdateTime;
                    BanOrigin = @"UDP";
                    BanReason = e.UpdateTime.ToString(CultureInfo.CurrentCulture);
                }
                else
                {
                    IsUDPBanned = false;
                    if (!IsHTTPBanned)
                    {
                        BanOrigin = string.Empty;
                        BanReason = string.Empty;
                    }
                    else
                    {
                        BanOrigin = @"HTTP";
                        BanReason = HTTPBanTime.ToString(CultureInfo.CurrentCulture);
                    }
                }

                break;
            case UpdateType.HTTPBan:
                if (e.Value == IsHTTPBanned) return;
                if (e.Value)
                {
                    IsHTTPBanned = true;
                    HTTPBanTime = e.UpdateTime;
                    BanOrigin = @"HTTP";
                    BanReason = e.UpdateTime.ToString(CultureInfo.CurrentCulture);
                }
                else
                {
                    IsHTTPBanned = false;
                    if (!IsUDPBanned)
                    {
                        BanOrigin = string.Empty;
                        BanReason = string.Empty;
                    }
                    else
                    {
                        BanOrigin = @"UDP";
                        BanReason = UDPBanTime.ToString(CultureInfo.CurrentCulture);
                    }
                }

                break;
            case UpdateType.InvalidSession:
                if (e.Value == IsInvalidSession) return;
                IsInvalidSession = e.Value;
                break;
            case UpdateType.WaitingOnResponse:
                if (e.Value == WaitingOnResponseAniDBUDP) return;
                if (e.Value)
                {
                    // TODO Start the Update Timer to add seconds to the waiting on AniDB message
                    WaitingOnResponseAniDBUDP = true;
                    WaitingOnResponseAniDBUDPString = Resources.AniDB_ResponseWait;
                }
                else
                {
                    // TODO Stop the timer
                    WaitingOnResponseAniDBUDP = false;
                    WaitingOnResponseAniDBUDPString = Resources.Command_Idle;
                }

                break;
            case UpdateType.OverloadBackoff:
                if (e.Value == HasExtendedPause) return;
                if (e.Value)
                {
                    ExtendedPauseString = string.Format(Resources.AniDB_Paused, e.PauseTimeSecs, e.Message);
                    HasExtendedPause = true;
                }
                else
                {
                    ExtendedPauseString = string.Empty;
                    HasExtendedPause = false;
                }

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #region Observable Properties

    public AsyncObservableCollection<Azure_AdminMessage> AdminMessages { get; set; }

    public void RefreshAdminMessages()
    {
        AdminMessages.Clear();

        try
        {
            AdminMessagesAvailable = false;
        }
        catch (Exception ex)
        {
            Utils.ShowErrorMessage(ex);
        }
    }

    private bool adminMessagesAvailable = false;

    public bool AdminMessagesAvailable
    {
        get => adminMessagesAvailable;
        set => this.SetField(() => adminMessagesAvailable, value);
    }

    private string banReason = string.Empty;

    public string BanReason
    {
        get => banReason;
        set => this.SetField(() => banReason, value);
    }

    private string banOrigin = string.Empty;

    public string BanOrigin
    {
        get => banOrigin;
        set => this.SetField(() => banOrigin, value);
    }

    private bool _isUDPBanned { get; set; }

    private bool IsUDPBanned
    {
        get => _isUDPBanned;
        set
        {
            _isUDPBanned = value;
            var newValue = _isUDPBanned || _isHTTPBanned;
            if (IsBanned != newValue)
            {
                IsBanned = newValue;
            }
        }
    }

    private bool _isHTTPBanned { get; set; }

    private bool IsHTTPBanned
    {
        get => _isHTTPBanned;
        set
        {
            _isHTTPBanned = value;
            var newValue = _isUDPBanned || _isHTTPBanned;
            if (IsBanned != newValue)
            {
                IsBanned = newValue;
            }
        }
    }

    private DateTime UDPBanTime { get; set; }
    private DateTime HTTPBanTime { get; set; }

    private bool isBanned = false;

    public bool IsBanned
    {
        get => isBanned;
        set => this.SetField(() => isBanned, value);
    }

    private bool isInvalidSession = false;

    public bool IsInvalidSession
    {
        get => isInvalidSession;
        set => this.SetField(() => isInvalidSession, value);
    }

    private bool waitingOnResponseAniDBUDP = false;

    public bool WaitingOnResponseAniDBUDP
    {
        get => waitingOnResponseAniDBUDP;
        set
        {
            this.SetField(() => waitingOnResponseAniDBUDP, value);
            NotWaitingOnResponseAniDBUDP = !value;
        }
    }

    private bool notWaitingOnResponseAniDBUDP = true;

    public bool NotWaitingOnResponseAniDBUDP
    {
        get => notWaitingOnResponseAniDBUDP;
        set => this.SetField(() => notWaitingOnResponseAniDBUDP, value);
    }

    private string waitingOnResponseAniDBUDPString = Resources.Command_Idle;

    public string WaitingOnResponseAniDBUDPString
    {
        get
        {
            waitingOnResponseAniDBUDPString = Resources.Command_Idle;
            return waitingOnResponseAniDBUDPString;
        }
        set => this.SetField(() => waitingOnResponseAniDBUDPString, value);
    }

    private string extendedPauseString = string.Empty;

    public string ExtendedPauseString
    {
        get => extendedPauseString;
        set => this.SetField(() => extendedPauseString, value);
    }

    private bool hasExtendedPause = false;

    public bool HasExtendedPause
    {
        get => hasExtendedPause;
        set => this.SetField(() => hasExtendedPause, value);
    }

    public AsyncObservableCollection<SVR_ImportFolder> ImportFolders { get; set; }

    public void RefreshImportFolders()
    {
        try
        {
            ImportFolders.Clear();
            RepoFactory.ImportFolder.GetAll().ForEach(a => ImportFolders.Add(a));
        }
        catch (Exception ex)
        {
            Utils.ShowErrorMessage(ex);
        }
    }

    #endregion
}
