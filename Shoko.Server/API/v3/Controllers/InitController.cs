using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using Constants = Shoko.Server.Server.Constants;
using ReleaseChannel = Shoko.Server.Server.ReleaseChannel;
using ServerStatus = Shoko.Server.API.v3.Models.Shoko.ServerStatus;

namespace Shoko.Server.API.v3.Controllers;

// ReSharper disable once UnusedMember.Global
/// <summary>
/// The init controller. Use this for first time setup. Settings will also allow full control to the init user.
/// </summary>
[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[DatabaseBlockedExempt]
[InitFriendly]
public class InitController : BaseController
{
    private readonly ILogger<InitController> _logger;
    private readonly SystemService _systemService;
    private readonly IConnectivityService _connectivityService;
    private readonly IUDPConnectionHandler _udpHandler;
    private readonly IHttpConnectionHandler _httpHandler;

    public InitController(
        ILogger<InitController> logger,
        ISystemService systemService,
        ISettingsProvider settingsProvider,
        IConnectivityService connectivityService,
        IUDPConnectionHandler udpHandler,
        IHttpConnectionHandler httpHandler
    ) : base(settingsProvider)
    {
        _logger = logger;
        _systemService = (SystemService)systemService;
        _connectivityService = connectivityService;
        _udpHandler = udpHandler;
        _httpHandler = httpHandler;
    }

    /// <summary>
    /// Return current version of ShokoServer and several modules
    /// This will work after init
    /// </summary>
    /// <returns></returns>
    [HttpGet("Version")]
    public ComponentVersionSet GetVersion()
    {
        var versionSet = new ComponentVersionSet()
        {
            Server = new()
            {
                Version = _systemService.Version.Version,
                ReleaseChannel = Enum.Parse<ReleaseChannel>(_systemService.Version.Channel.ToString()),
                Commit = _systemService.Version.SourceRevision,
                Tag = _systemService.Version.Tag,
                ReleaseDate = _systemService.Version.ReleasedAt,
            },
        };

        try
        {
            if (MediaInfoUtility.GetVersion() is { Length: > 0 } mediaInfoVersion)
                versionSet.MediaInfo = new() { Version = mediaInfoVersion };
        }
        catch { }

        var webuiVersion = WebUIUpdateService.LoadWebUIVersionInfo();
        if (webuiVersion != null)
        {
            versionSet.WebUI = new()
            {
                Version = webuiVersion.Version,
                Tag = webuiVersion.Tag,
                ReleaseChannel = webuiVersion.Channel,
                Commit = webuiVersion.Commit,
                ReleaseDate = webuiVersion.Date,
            };
        }

        return versionSet;
    }

    /// <summary>
    /// Gets various information about the startup status of the server
    /// This will work after init
    /// </summary>
    /// <returns></returns>
    [HttpGet("Status")]
    public ServerStatus GetServerStatus()
    {
        var uptime = _systemService.StartedAt.HasValue
            ? DateTime.Now - _systemService.StartedAt.Value
            : (TimeSpan?)null;

        var message = (string)null;
        var state = ServerStatus.StartupState.Waiting;
        if (ServerState.Instance.StartupFailed)
        {
            message = ServerState.Instance.StartupFailedMessage;
            state = ServerStatus.StartupState.Failed;
        }
        else if (ServerState.Instance.ServerStarting)
        {
            message = ServerState.Instance.ServerStartingStatus;
            if (message.Equals("Complete!")) message = null;
            state = ServerStatus.StartupState.Starting;
        }
        else if (ServerState.Instance.ServerOnline)
        {
            state = ServerStatus.StartupState.Started;
        }
        ServerStatus status = new ServerStatus
        {
            State = state,
            StartupMessage = message,
            Uptime = uptime,
            DatabaseBlocked = ServerState.Instance.DatabaseBlocked
        };
        return status;
    }

    /// <summary>
    /// Gets the current network connectivity details for the server.
    /// </summary>
    /// <returns></returns>
    [InitFriendly]
    [HttpGet("Connectivity")]
    public ActionResult<ConnectivityDetails> GetNetworkAvailability()
    {
        return new ConnectivityDetails
        {
            NetworkAvailability = _connectivityService.NetworkAvailability,
            LastChangedAt = _connectivityService.LastChangedAt,
            IsAniDBUdpReachable = _udpHandler.IsAlive && _udpHandler.IsNetworkAvailable,
            IsAniDBUdpBanned = _udpHandler.IsBanned,
            IsAniDBHttpBanned = _httpHandler.IsBanned
        };
    }

    /// <summary>
    /// Forcefully re-checks the current network connectivity, then returns the
    /// updated details for the server.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("Connectivity")]
    public async Task<ActionResult<object>> CheckNetworkAvailability()
    {
        await _connectivityService.CheckAvailability();

        return GetNetworkAvailability();
    }

    /// <summary>
    /// Gets whether anything is actively using the API
    /// </summary>
    /// <returns></returns>
    [HttpGet("InUse")]
    public bool ApiInUse()
    {
        return ServerState.Instance.ApiInUse;
    }

    /// <summary>
    /// Gets the Default user's credentials. Will only return on first run
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("DefaultUser")]
    public ActionResult<Credentials> GetDefaultUserCredentials()
    {
        var settings = SettingsProvider.GetSettings();
        return new Credentials
        {
            Username = settings.Database.DefaultUserUsername,
            Password = settings.Database.DefaultUserPassword
        };
    }

    /// <summary>
    /// Sets the default user's credentials
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("DefaultUser")]
    public ActionResult SetDefaultUserCredentials(Credentials credentials)
    {
        try
        {
            var settings = SettingsProvider.GetSettings();
            settings.Database.DefaultUserUsername = credentials.Username;
            settings.Database.DefaultUserPassword = credentials.Password;
            return Ok();
        }
        catch
        {
            return InternalError();
        }
    }

    /// <summary>
    /// Starts the server unless it's already running.
    /// </summary>
    /// <returns></returns>
    [HttpGet("StartServer")]
    [HttpPost("StartServer")]
    public ActionResult StartServer()
    {
        if (_systemService.IsStarted)
            return BadRequest("Already Running");
        if (ServerState.Instance.ServerStarting)
            return BadRequest("Already Starting");
        try
        {
            _systemService.LateStart();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error starting the server");
            return InternalError($"There was an error starting the server: {e}");
        }
        return Ok();
    }

    /// <summary>
    /// Requests the server to shutdown.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("StopServer")]
    public ActionResult StopServer()
    {
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown Already Requested");
        if (!_systemService.RequestShutdown())
            return BadRequest("Shutdown Request Blocked");
        return Ok("Shutdown Requested");
    }

    /// <summary>
    /// Requests the server to restart if possible.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("RestartServer")]
    public ActionResult RestartServer()
    {
        if (!_systemService.CanRestart)
            return BadRequest("Restart Not Possible for this instance");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown Already Requested");
        if (!_systemService.RequestRestart())
            return BadRequest("Restart Request Blocked");
        return Ok("Restart Requested");
    }

    /// <summary>
    /// Test Database Connection with Current Settings
    /// </summary>
    /// <returns>200 if connection successful, 400 otherwise</returns>
    [Authorize("init")]
    [HttpGet("Database/Test")]
    public ActionResult TestDatabaseConnection()
    {
        var settings = SettingsProvider.GetSettings();
        return settings.Database.Type switch
        {
            Constants.DatabaseType.MySQL when new MySQL().TestConnection() => Ok(),
            Constants.DatabaseType.SQLServer when new SQLServer().TestConnection() => Ok(),
            Constants.DatabaseType.SQLite => Ok(),
            _ => BadRequest("Failed to Connect")
        };
    }
}
