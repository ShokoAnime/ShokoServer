using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NLog;
using Quartz;
using Shoko.Abstractions.Services;
using Shoko.Server.API.v1.Implementations;
using Shoko.Server.API.v1.Models;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules;

[Authorize("admin")]
[ApiController] // As this module requireAuthentication all request need to have apikey in header.
[Route("/api")]
[ApiVersion("2.0")]
public class Core : BaseController
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly ShokoServiceImplementation _service;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly IAnidbService _anidbService;

    private readonly ActionService _actionService;

    private readonly ISettingsProvider _settingsProvider;

    private IServerSettings _settings => _settingsProvider.GetSettings();

    public Core(ShokoServiceImplementation service, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, IAnidbService anidbService, ActionService actionService) : base(settingsProvider)
    {
        _service = service;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _anidbService = anidbService;
        _actionService = actionService;
    }

    #region 01.Settings

    /// <summary>
    /// Set JMMServer Port
    /// </summary>
    /// <returns></returns>
    [HttpPost("config/port/set")]
    public object SetPort(ushort port)
    {
        _settings.Web.Port = port;
        return APIStatus.OK();
    }

    /// <summary>
    /// Get JMMServer Port
    /// </summary>
    /// <returns>A dynamic object of x.port == port</returns>
    [HttpGet("config/port/get")]
    public object GetPort()
    {
        dynamic x = new ExpandoObject();
        x.port = _settings.Web.Port;
        return x;
    }

    /// <summary>
    /// Set Imagepath as default or custom
    /// </summary>
    /// <returns></returns>
    [HttpPost("config/imagepath/set")]
    public ActionResult SetImagepath(ImagePath imagepath)
    {
        if (imagepath.isdefault)
        {
            _settings.ImagesPath = Utils.DefaultImagePath;
            return APIStatus.OK();
        }

        if (!string.IsNullOrEmpty(imagepath.path) && imagepath.path != string.Empty)
        {
            if (Directory.Exists(imagepath.path))
            {
                _settings.ImagesPath = imagepath.path;
                return APIStatus.OK();
            }

            return new APIMessage(404, "Directory Not Found on Host");
        }

        return new APIMessage(400, "Path Missing");
    }

    /// <summary>
    /// Return ImagePath object
    /// </summary>
    /// <returns></returns>
    [HttpGet("config/imagepath/get")]
    public object GetImagepath()
    {
        var imagepath = new ImagePath
        {
            path = _settings.ImagesPath,
            isdefault = _settings.ImagesPath == Utils.DefaultImagePath
        };
        return imagepath;
    }

    /// <summary>
    /// Return body of current working settings.json - this could act as backup
    /// </summary>
    /// <returns>Server settings</returns>
    [HttpGet("config/export")]
    public ActionResult<IServerSettings> ExportConfig()
    {
        try
        {
            return new ActionResult<IServerSettings>(_settings);
        }
        catch
        {
            return APIStatus.InternalError("Error while reading settings.");
        }
    }

    /// <summary>
    /// Import config file that was sent to in API body - this act as import from backup
    /// </summary>
    /// <returns>APIStatus</returns>
    [HttpPost("config/import")]
    public ActionResult ImportConfig(CL_ServerSettings settings)
    {
        return BadRequest("This settings model is deprecated. It will break the settings file. Use APIv3");
    }

    /// <summary>
    /// Return given setting
    /// </summary>
    /// <returns></returns>
    [HttpPost("config/get")]
    public ActionResult GetSetting([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken setting)
        => new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's implementation'");

    /// <summary>
    ///
    /// </summary>
    /// <param name="jsonSettings"></param>
    /// <returns></returns>
    [HttpPost("config/set")]
    public ActionResult SetSetting(string jsonSettings)
    {
        return new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's JsonPatch implementation'");
    }

    /// <summary>
    /// Set given setting
    /// </summary>
    /// <returns></returns>
    [HttpPost("config/setmultiple")]
    public ActionResult SetSetting([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken settings)
    {
        return new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's JsonPatch implementation'");
    }

    #endregion

    #region 02.AniDB

    /// <summary>
    /// Set AniDB account with login, password and client port
    /// </summary>
    /// <returns></returns>
    [HttpPost("anidb/set")]
    public ActionResult SetAniDB(Credentials cred)
    {
        if (!string.IsNullOrEmpty(cred.login) && !string.IsNullOrEmpty(cred.password))
        {
            _settings.AniDb.Username = cred.login;
            _settings.AniDb.Password = cred.password;
            if (cred.port != 0)
            {
                _settings.AniDb.ClientPort = cred.port;
            }

            return APIStatus.OK();
        }

        return new APIMessage(400, "Login and Password missing");
    }

    /// <summary>
    /// Test AniDB Credentials
    /// </summary>
    /// <returns></returns>
    [HttpGet("anidb/test")]
    public ActionResult TestAniDB()
    {
        var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
        handler.ForceLogout();
        handler.CloseConnections();

        handler.Init(_settings.AniDb.Username, _settings.AniDb.Password,
            _settings.AniDb.UDPServerAddress,
            _settings.AniDb.UDPServerPort, _settings.AniDb.ClientPort);

        if (handler.Login())
        {
            handler.ForceLogout();
            return APIStatus.OK();
        }

        return APIStatus.Unauthorized();
    }

    /// <summary>
    /// Return login/password/port of used AniDB
    /// </summary>
    /// <returns></returns>
    [HttpGet("anidb/get")]
    public Credentials GetAniDB()
    {
        return new Credentials
        {
            login = _settings.AniDb.Username,
            password = _settings.AniDb.Password,
            port = _settings.AniDb.ClientPort
        };
    }

    /// <summary>
    /// Sync votes bettween Local and AniDB and only upload to MAL
    /// </summary>
    /// <returns></returns>
    [HttpGet("anidb/votes/sync")]
    public async Task<ActionResult> SyncAniDBVotes()
    {
        if (User.IsAniDBUser != 1)
            return BadRequest("User is not an AniDB user. Nothing to do.");

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncAniDBVotesJob>(c => c.UserID = User.JMMUserID);
        return APIStatus.OK();
    }

    /// <summary>
    /// Sync AniDB List
    /// </summary>
    /// <returns></returns>
    [HttpGet("anidb/list/sync")]
    public async Task<ActionResult> SyncAniDBList()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncAniDBMyListJob>();
        return APIStatus.OK();
    }

    /// <summary>
    /// Update all series infromation from AniDB
    /// </summary>
    /// <returns></returns>
    [HttpGet("anidb/update")]
    public async Task<ActionResult> UpdateAllAniDB()
    {
        await _actionService.RunImport_UpdateAllAniDB();
        return APIStatus.OK();
    }

    [Obsolete]
    [HttpGet("anidb/updatemissingcache")]
    public ActionResult UpdateMissingAniDBXML()
        => new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's implementation'");

    #endregion

    #region 04.Trakt

    /// <summary>
    /// Get Trakt code and url
    /// </summary>
    /// <returns></returns>
    [HttpGet("trakt/code")]
    public ActionResult<Dictionary<string, object>> GetTraktCode()
    {
        var code = _service.GetTraktDeviceCode();
        if (code.UserCode == string.Empty)
        {
            return APIStatus.InternalError("Trakt code doesn't exist on the server");
        }

        var result = new Dictionary<string, object>();
        result.Add("usercode", code.UserCode);
        result.Add("url", code.VerificationUrl);
        return result;
    }

    /// <summary>
    /// Return trakt authtoken
    /// </summary>
    /// <returns></returns>
    [HttpGet("trakt/get")]
    public ActionResult<Credentials> GetTrakt()
    {
        return new Credentials
        {
            token = _settings.TraktTv.AuthToken,
            refresh_token = _settings.TraktTv.RefreshToken
        };
    }

    /// <summary>
    /// Sync Trakt Collection
    /// </summary>
    /// <returns></returns>
    [HttpGet("trakt/sync")]
    public async Task<ActionResult> SyncTrakt()
    {
        if (_settings.TraktTv.Enabled && !string.IsNullOrEmpty(_settings.TraktTv.AuthToken))
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            await scheduler.StartJob<SendWatchStatesToTraktJob>(c => c.ForceRefresh = true);
            return APIStatus.OK();
        }

        return new APIMessage(204, "Trakt is not enabled or you are missing the authtoken");
    }

    #endregion

    #region 05.TvDB

    /// <summary>
    /// Scan TvDB
    /// </summary>
    /// <returns></returns>
    [HttpGet("tvdb/update")]
    public ActionResult ScanTvDB()
    {
        return APIStatus.OK();
    }

    [HttpGet("tvdb/regenlinks")]
    public ActionResult RegenerateAllEpisodeLinks()
    {
        return APIStatus.OK();
    }

    public class AniEpSummary
    {
        public int AniDBEpisodeType { get; set; }
        public int AniDBEpisodeNumber { get; set; }
        public string AniDBEpisodeName { get; set; }

        protected bool Equals(AniEpSummary other)
        {
            return AniDBEpisodeType == other.AniDBEpisodeType && AniDBEpisodeNumber == other.AniDBEpisodeNumber &&
                   string.Equals(AniDBEpisodeName, other.AniDBEpisodeName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((AniEpSummary)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = AniDBEpisodeType;
                hashCode = (hashCode * 397) ^ AniDBEpisodeNumber;
                hashCode = (hashCode * 397) ^ (AniDBEpisodeName != null ? AniDBEpisodeName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    [HttpGet("tvdb/checklinks")]
    public ActionResult<List<object>> CheckAllEpisodeLinksAgainstCurrent()
    {
        return new List<object>();
    }

    #endregion

    #region 06.MovieDB

    /// <summary>
    /// Scan MovieDB
    /// </summary>
    /// <returns></returns>
    [HttpGet("moviedb/update")]
    public async Task<ActionResult> ScanTMDB()
    {
        await _actionService.RunImport_ScanTMDB();
        return APIStatus.OK();
    }

    #endregion

    #region 07.User

    /// <summary>
    /// return Dictionary int = id, string = username
    /// </summary>
    /// <returns></returns>
    [HttpGet("user/list")]
    public ActionResult<Dictionary<int, string>> GetUsers()
    {
        var users = new Dictionary<int, string>();
        try
        {
            foreach (var us in RepoFactory.JMMUser.GetAll())
            {
                users.Add(us.JMMUserID, us.Username);
            }

            return users;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create user from Contract_JMMUser
    /// </summary>
    /// <returns></returns>
    [HttpPost("user/create")]
    public ActionResult CreateUser(CL_JMMUser user)
    {
        user.Password = Digest.Hash(user.Password);
        user.HideCategories = string.Empty;
        user.PlexUsers = string.Empty;
        return _service.SaveUser(user) == string.Empty
            ? APIStatus.OK()
            : APIStatus.InternalError();
    }

    /// <summary>
    ///  change current user password
    /// </summary>
    /// <returns></returns>
    [HttpPost("user/password")]
    public ActionResult ChangePassword(CL_JMMUser user)
    {
        return _service.ChangePassword(user.JMMUserID, user.Password) == string.Empty
            ? APIStatus.OK()
            : APIStatus.InternalError();
    }

    /// <summary>
    /// change given user (by uid) password
    /// </summary>
    /// <returns></returns>
    [HttpPost("user/password/{uid}")]
    public ActionResult ChangePassword(int uid, CL_JMMUser user)
    {
        return _service.ChangePassword(uid, user.Password) == string.Empty
            ? APIStatus.OK()
            : APIStatus.InternalError();
    }

    /// <summary>
    /// Delete user from his ID
    /// </summary>
    /// <returns></returns>
    [HttpPost("user/delete")]
    public ActionResult DeleteUser(CL_JMMUser user)
    {
        return _service.DeleteUser(user.JMMUserID) == string.Empty
            ? APIStatus.OK()
            : APIStatus.InternalError();
    }

    #endregion

    #region 8.OS-based operations

    /// <summary>
    /// Return OSFolder object that is a folder from which jmmserver is running
    /// </summary>
    /// <returns></returns>
    [HttpGet("os/folder/base")]
    public ActionResult GetOSBaseFolder()
        => new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's implementation'");

    /// <summary>
    /// Return OSFolder object of directory that was given via
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="dir"></param>
    /// <returns></returns>
    [HttpPost("/os/folder")]
    public ActionResult GetOSFolder([FromQuery] string folder, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JToken dir)
        => new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's implementation'");

    /// <summary>
    /// Return OSFolder with subdirs as every driver on local system
    /// </summary>
    /// <returns></returns>
    [HttpGet("os/drives")]
    public ActionResult GetOSDrives()
        => new APIMessage(HttpStatusCode.NotImplemented, "Use APIv3's implementation'");

    #endregion

    #region 10. Logs

    /// <summary>
    /// Run LogRotator with current settings
    /// </summary>
    /// <returns></returns>
    [HttpGet("log/get")]
    public ActionResult StartRotateLogs()
    {
        var rotator = HttpContext.RequestServices.GetRequiredService<LogRotator>();
        rotator.Start();
        return APIStatus.OK();
    }

    /// <summary>
    /// Set settings for LogRotator
    /// </summary>
    /// <returns></returns>
    [HttpPost("log/rotate")]
    public ActionResult SetRotateLogs(Logs rotator)
    {
        _settings.LogRotator.Enabled = rotator.rotate;
        _settings.LogRotator.Zip = rotator.zip;
        _settings.LogRotator.Delete = rotator.delete;
        _settings.LogRotator.Delete_Days = rotator.days.ToString();

        return APIStatus.OK();
    }

    /// <summary>
    /// Get settings for LogRotator
    /// </summary>
    /// <returns></returns>
    [HttpGet("log/rotate")]
    public ActionResult<Logs> GetRotateLogs()
    {
        var rotator = new Logs
        {
            rotate = _settings.LogRotator.Enabled,
            zip = _settings.LogRotator.Zip,
            delete = _settings.LogRotator.Delete
        };
        var day = 0;
        if (!string.IsNullOrEmpty(_settings.LogRotator.Delete_Days))
        {
            int.TryParse(_settings.LogRotator.Delete_Days, out day);
        }

        rotator.days = day;

        return rotator;
    }

    /// <summary>
    /// return int position - current position
    /// return string[] lines - lines from current log file
    /// </summary>
    /// <param name="lines">max lines to return</param>
    /// <param name="position">position to seek</param>
    /// <returns></returns>
    [HttpGet("log/get/{lines?}/{position?}")]
    public ActionResult<Dictionary<string, object>> GetLog(int lines = 10, int position = 0)
    {
        var log_file = LogRotator.GetCurrentLogFile();
        if (string.IsNullOrEmpty(log_file))
        {
            return APIStatus.NotFound("Could not find current log name. Sorry");
        }

        if (!System.IO.File.Exists(log_file))
        {
            return APIStatus.NotFound();
        }

        var result = new Dictionary<string, object>();
        var fs = System.IO.File.OpenRead(log_file);

        if (position >= fs.Length)
        {
            result.Add("position", fs.Length);
            result.Add("lines", new string[] { });
            return result;
        }

        var logLines = new List<string>();

        var reader = new LogReader(fs, position);
        for (var i = 0; i < lines; i++)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                break;
            }

            logLines.Add(line);
        }

        result.Add("position", reader.Position);
        result.Add("lines", logLines.ToArray());
        return result;
    }

    #endregion

    #region 11. Image Actions

    [HttpGet("images/update")]
    public ActionResult UpdateImages()
    {
        Utils.ShokoServer.DownloadAllImages();

        return APIStatus.OK();
    }

    #endregion
}
