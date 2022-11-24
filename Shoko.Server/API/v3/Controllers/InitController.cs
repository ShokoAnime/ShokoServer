using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
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

    public InitController(ILogger<InitController> logger)
    {
        _logger = logger;
    }

    private record WebUIVersion {
        /// <summary>
        /// Package version.
        /// </summary>
        public string package = "1.0.0";
        /// <summary>
        /// Short-form git commit sha digest.
        /// </summary>
        public string git = "0000000";
        /// <summary>
        /// True if this is a debug package.
        /// </summary>
        public bool debug = false;
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

        foreach (var raw in Utils.GetApplicationExtraVersion().Split(","))
        {
            var pair = raw.Split("=");
            if (pair.Length != 2) continue;
            switch (pair[0])
            {
                case "tag":
                    versionSet.Server.Tag = pair[1];
                    break;
                case "commit":
                    versionSet.Server.Commit = pair[1];
                    break;
                case "channel":
                    if (Enum.TryParse<ReleaseChannel>(pair[1], true, out var channel))
                        versionSet.Server.ReleaseChannel = channel;
                    else
                        versionSet.Server.ReleaseChannel = ReleaseChannel.Debug;
                    break;
            }
        }

        var mediaInfoFileInfo = new FileInfo(Path.Combine(Assembly.GetEntryAssembly().Location, "../MediaInfo", "MediaInfo.exe"));
        versionSet.MediaInfo = new()
        {
            Version = mediaInfoFileInfo.Exists ? FileVersionInfo.GetVersionInfo(mediaInfoFileInfo.FullName).FileVersion : null,
        };

        var webUIFileInfo = new FileInfo(Path.Combine(ServerSettings.ApplicationPath, "webui/version.json"));
        if (webUIFileInfo.Exists)
        {
            var webuiVersion = Newtonsoft.Json.JsonConvert.DeserializeObject<WebUIVersion>(System.IO.File.ReadAllText(webUIFileInfo.FullName));
            versionSet.WebUI = new()
            {
                Version = webuiVersion.package,
                ReleaseChannel = webuiVersion.debug ? ReleaseChannel.Debug : webuiVersion.package.Contains("-dev") ? ReleaseChannel.Dev : ReleaseChannel.Stable,
                Commit = webuiVersion.git,
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
        return new Credentials
        {
            Username = ServerSettings.Instance.Database.DefaultUserUsername,
            Password = ServerSettings.Instance.Database.DefaultUserPassword
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
            ServerSettings.Instance.Database.DefaultUserUsername = credentials.Username;
            ServerSettings.Instance.Database.DefaultUserPassword = credentials.Password;
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
            _logger.LogError(e, $"There was an error starting the server: {e}");
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
        if (ServerSettings.Instance.Database.Type == Constants.DatabaseType.MySQL && new MySQL().TestConnection())
            return Ok();

        if (ServerSettings.Instance.Database.Type == Constants.DatabaseType.SqlServer  && new SQLServer().TestConnection())
            return Ok();

        if (ServerSettings.Instance.Database.Type == Constants.DatabaseType.Sqlite)
            return Ok();

        return BadRequest("Failed to Connect");
    }
}
