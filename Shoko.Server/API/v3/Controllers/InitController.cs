using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Databases;
using Shoko.Server.MediaInfo;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using Constants = Shoko.Server.Server.Constants;
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
    private readonly ISystemUpdateService _webUIUpdateService;

    public InitController(
        ILogger<InitController> logger,
        SystemService systemService,
        ISettingsProvider settingsProvider,
        IConnectivityService connectivityService,
        IUDPConnectionHandler udpHandler,
        IHttpConnectionHandler httpHandler,
        ISystemUpdateService webUIUpdateService
    ) : base(settingsProvider)
    {
        _logger = logger;
        _systemService = systemService;
        _connectivityService = connectivityService;
        _udpHandler = udpHandler;
        _httpHandler = httpHandler;
        _webUIUpdateService = webUIUpdateService;
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
                Version = _systemService.Version.Version.ToSemanticVersioningString(),
                ReleaseChannel = Enum.Parse<ReleaseChannel>(_systemService.Version.Channel.ToString()),
                Commit = _systemService.Version.SourceRevision,
                Tag = _systemService.Version.ReleaseTag,
                ReleaseDate = _systemService.Version.ReleasedAt,
            },
        };

        try
        {
            if (MediaInfoUtility.GetVersion() is { Length: > 0 } mediaInfoVersion)
                versionSet.MediaInfo = new() { Version = mediaInfoVersion };
        }
        catch { }

        var webuiVersion = _webUIUpdateService.LoadWebComponentVersionInformation();
        if (webuiVersion != null)
        {
            versionSet.WebUI = new()
            {
                Version = webuiVersion.Version.ToSemanticVersioningString(),
                MinimumServerVersion = webuiVersion.MinimumServerVersion?.ToSemanticVersioningString(),
                Tag = webuiVersion.ReleaseTag,
                ReleaseChannel = webuiVersion.Channel,
                Commit = webuiVersion.SourceRevision,
                ReleaseDate = webuiVersion.ReleasedAt,
            };
        }

        return versionSet;
    }

    /// <summary>
    /// Gets various information about the startup status of the server
    /// This will work after init
    /// </summary>
    /// <remarks>
    /// To get the uptime, database blocked, etc. you need to authenticate when
    /// not in setup mode or a failed startup.
    /// </remarks>
    /// <returns></returns>
    [HttpGet("Status")]
    public ServerStatus GetServerStatus()
    {
        var isLoggedIn = User is not null;
        var message = (string)null;
        var state = ServerStatus.StartupState.Waiting;
        if (_systemService.IsStarted)
        {
            state = ServerStatus.StartupState.Started;
        }
        else if (_systemService.StartupFailedException is { } ex)
        {
            message = ex.Message;
            state = ServerStatus.StartupState.Failed;
        }
        else if (!_systemService.InSetupMode)
        {
            message = _systemService.StartupMessage;
            if (message.Equals("Complete!")) message = null;
            state = ServerStatus.StartupState.Starting;
        }
        if (!isLoggedIn)
            return new()
            {
                State = state,
                StartupMessage = message,
            };
        return new()
        {
            State = state,
            StartupMessage = message,
            BootstrappedAt = _systemService.BootstrappedAt.ToUniversalTime(),
            StartedAt = _systemService.StartedAt?.ToUniversalTime(),
            Uptime = _systemService.Uptime,
            StartupTime = _systemService.StartupTime ?? TimeSpan.Zero,
            CanShutdown = _systemService.CanShutdown,
            CanRestart = _systemService.CanRestart,
            DatabaseBlocked = new()
            {
                Blocked = _systemService.IsDatabaseBlocked,
                Reason = string.Empty,
            },
        };
    }

    /// <summary>
    /// Gets the current network connectivity details for the server.
    /// </summary>
    /// <returns></returns>
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
    public async Task<ActionResult<ConnectivityDetails>> CheckNetworkAvailability()
    {
        await _connectivityService.CheckAvailability();

        return GetNetworkAvailability();
    }

    /// <summary>
    /// Gets whether anything is actively using the API
    /// </summary>
    /// <returns></returns>
    [HttpGet("InUse")]
    public bool ApiInUse() => ApiInUseAttribute.IsInUse;

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
    [Obsolete("This is now deprecated, please use CompleteSetup instead.")]
    [Authorize("init")]
    [HttpGet("StartServer")]
    public ActionResult StartServer()
        => CompleteSetup();

    /// <summary>
    /// Tells the server that the setup process is complete and it can start
    /// now.
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("CompleteSetup")]
    public ActionResult CompleteSetup()
    {
        if (_systemService.IsStarted)
            return BadRequest("Already Running");
        if (_systemService.StartupFailedException is not null)
            return BadRequest("Startup Failed");
        if (!_systemService.InSetupMode)
            return BadRequest("Already Starting");
        try
        {
            _systemService.CompleteSetup();
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
    [HttpPost("Shutdown")]
    public ActionResult StopServer()
    {
        if (!_systemService.CanShutdown)
            return BadRequest("Shutdown is disabled for this instance");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown already requested");
        if (!_systemService.RequestShutdown())
            return BadRequest("Shutdown request blocked");
        return Ok("Shutdown Requested");
    }

    /// <summary>
    /// Requests the server to restart if possible.
    /// </summary>
    /// <returns></returns>
    [Authorize(Roles = "admin,init")]
    [HttpPost("Restart")]
    public ActionResult RestartServer()
    {
        if (!_systemService.CanRestart)
            return BadRequest("Restart is disabled for this instance");
        if (_systemService.ShutdownPending)
            return BadRequest("Shutdown already requested");
        if (!_systemService.RequestRestart())
            return BadRequest("Restart request blocked");
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
            Constants.DatabaseType.MySQL when new MySQL(_systemService).TestConnection() => Ok(),
            Constants.DatabaseType.SQLServer when new SQLServer(_systemService).TestConnection() => Ok(),
            Constants.DatabaseType.SQLite when new SQLite(_systemService).TestConnection() => Ok(),
            _ => BadRequest("Failed to Connect")
        };
    }
}
