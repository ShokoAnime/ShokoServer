using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.WebUI;
using Shoko.Server.Databases;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
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
    private readonly ShokoServer _shokoServer;

    public InitController(ILogger<InitController> logger, ISettingsProvider settingsProvider, ShokoServer shokoServer) : base(settingsProvider)
    {
        _logger = logger;
        _shokoServer = shokoServer;
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
            Server = new() { Version = Utils.GetApplicationVersion() },
            Commons = new() { Version = Utils.GetApplicationVersion(Assembly.GetAssembly(typeof(Shoko.Commons.Culture))) },
            Models = new() { Version = Utils.GetApplicationVersion(Assembly.GetAssembly(typeof(Shoko.Models.Constants))) },
            MediaInfo = new()
        };

        var extraVersionDict = Utils.GetApplicationExtraVersion();
        if (extraVersionDict.TryGetValue("tag", out var tag))
            versionSet.Server.Tag = tag;
        if (extraVersionDict.TryGetValue("commit", out var commit))
            versionSet.Server.Commit = commit;
        if (extraVersionDict.TryGetValue("channel", out var rawChannel))
            if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
                versionSet.Server.ReleaseChannel = channel;
            else
                versionSet.Server.ReleaseChannel = ReleaseChannel.Debug;

        var mediaInfoFileInfo = new FileInfo(Path.Combine(Assembly.GetEntryAssembly().Location, "../MediaInfo", "MediaInfo.exe"));
        versionSet.MediaInfo = new()
        {
            Version = mediaInfoFileInfo.Exists ? FileVersionInfo.GetVersionInfo(mediaInfoFileInfo.FullName).FileVersion : null,
        };

        var webuiVersion = WebUIHelper.LoadWebUIVersionInfo();
        if (webuiVersion != null)
        {
            versionSet.WebUI = new()
            {
                Version = webuiVersion.package,
                ReleaseChannel = webuiVersion.debug ? ReleaseChannel.Debug : webuiVersion.package.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable,
                Commit = webuiVersion.git,
                ReleaseDate = webuiVersion.date,
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
        TimeSpan? uptime = ShokoServer.UpTime;

        string message = null;
        ServerStatus.StartupState state = ServerStatus.StartupState.Waiting;
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
    /// Starts the server, or does nothing
    /// </summary>
    /// <returns></returns>
    [HttpGet("StartServer")]
    public ActionResult StartServer()
    {
        if (ServerState.Instance.ServerOnline) return BadRequest("Already Running");
        if (ServerState.Instance.ServerStarting) return BadRequest("Already Starting");
        try
        {
            ShokoServer.RunWorkSetupDB();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error starting the server");
            return InternalError($"There was an error starting the server: {e}");
        }
        return Ok();
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
            Constants.DatabaseType.SqlServer when new SQLServer().TestConnection() => Ok(),
            Constants.DatabaseType.Sqlite => Ok(),
            _ => BadRequest("Failed to Connect")
        };
    }
}
