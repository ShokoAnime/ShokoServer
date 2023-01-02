using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Databases;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;
using DatabaseSettings = Shoko.Server.API.v2.Models.core.DatabaseSettings; //using Microsoft.SqlServer.Management.Smo;
using ServerStatus = Shoko.Server.API.v2.Models.core.ServerStatus;

//using Microsoft.SqlServer.Management.Smo;

namespace Shoko.Server.API.v2.Modules;

// ReSharper disable once UnusedMember.Global
[Route("/api/init")]
[ApiController]
[ApiVersion("2.0")]
[DatabaseBlockedExempt]
[InitFriendly]
public class Init : BaseController
{
    private readonly ILogger<Init> _logger;
    private readonly IServerSettings _settings;
    private readonly ShokoServer _shokoServer;

    public Init(ILogger<Init> logger, ISettingsProvider settingsProvider, ShokoServer shokoServer) : base(settingsProvider)
    {
        _logger = logger;
        _shokoServer = shokoServer;
        _settings = settingsProvider.GetSettings();
    }

    /// <summary>
    /// Return current version of ShokoServer and several modules
    /// This will work after init
    /// </summary>
    /// <returns></returns>
    [HttpGet("version")]
    public List<ComponentVersion> GetVersion()
    {
        List<ComponentVersion> list = new List<ComponentVersion>();

        ComponentVersion version = new ComponentVersion
        {
            version = Utils.GetApplicationVersion(),
            name = "server"
        };
        list.Add(version);

        string versionExtra = Utils.GetApplicationExtraVersion();

        if (!string.IsNullOrEmpty(versionExtra))
        {
            version = new ComponentVersion
            {
                version = versionExtra,
                name = "servercommit"
            };
            list.Add(version);
        }

        version = new ComponentVersion
        {
            version = Assembly.GetAssembly(typeof(FolderMappings)).GetName().Version.ToString(),
            name = "commons"
        };
        list.Add(version);

        version = new ComponentVersion
        {
            version = Assembly.GetAssembly(typeof(AniDB_Anime)).GetName().Version.ToString(),
            name = "models"
        };
        list.Add(version);

        string dllpath = Assembly.GetEntryAssembly().Location;
        dllpath = Path.GetDirectoryName(dllpath);
        dllpath = Path.Combine(dllpath, "x86");
        dllpath = Path.Combine(dllpath, "MediaInfo.dll");

        if (System.IO.File.Exists(dllpath))
        {
            version = new ComponentVersion
            {
                version = FileVersionInfo.GetVersionInfo(dllpath).FileVersion,
                name = "MediaInfo"
            };
            list.Add(version);
        }
        else
        {
            dllpath = Assembly.GetEntryAssembly().Location;
            dllpath = Path.GetDirectoryName(dllpath);
            dllpath = Path.Combine(dllpath, "x64");
            dllpath = Path.Combine(dllpath, "MediaInfo.dll");
            if (System.IO.File.Exists(dllpath))
            {
                version = new ComponentVersion
                {
                    version = FileVersionInfo.GetVersionInfo(dllpath).FileVersion,
                    name = "MediaInfo"
                };
                list.Add(version);
            }
            else
            {
                version = new ComponentVersion
                {
                    version = @"DLL not found, using internal",
                    name = "MediaInfo"
                };
                list.Add(version);
            }
        }

        var webUIFileInfo = new FileInfo(Path.Combine(Utils.ApplicationPath, "webui/index.ver"));
        if (webUIFileInfo.Exists)
        {
            var webui_version = System.IO.File.ReadAllText(webUIFileInfo.FullName);
            string[] versions = webui_version.Split('>');
            if (versions.Length == 2)
            {
                version = new ComponentVersion
                {
                    name = "webui/" + versions[0],
                    version = versions[1]
                };
                list.Add(version);
            }
        }

        return list;
    }

    /// <summary>
    /// Gets various information about the startup status of the server
    /// This will work after init
    /// </summary>
    /// <returns></returns>
    [HttpGet("status")]
    public ServerStatus GetServerStatus()
    {
        TimeSpan? uptime = ShokoServer.UpTime;
        string uptimemsg = uptime == null
            ? null
            : $"{(int) uptime.Value.TotalHours:00}:{uptime.Value.Minutes:00}:{uptime.Value.Seconds:00}";
        ServerStatus status = new ServerStatus
        {
            server_started = ServerState.Instance.ServerOnline,
            startup_state = ServerState.Instance.ServerStartingStatus,
            server_uptime = uptimemsg,
            first_run = _settings.FirstRun,
            startup_failed = ServerState.Instance.StartupFailed,
            startup_failed_error_message = ServerState.Instance.StartupFailedMessage
        };
        return status;
    }

    /// <summary>
    /// Gets whether anything is actively using the API
    /// </summary>
    /// <returns></returns>
    [HttpGet("inuse")]
    public bool ApiInUse()
    {
        return ServerState.Instance.ApiInUse;
    }
        
    /// <summary>
    /// Gets the Default user's credentials. Will only return on first run
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("defaultuser")]
    public ActionResult<Credentials> GetDefaultUserCredentials()
    {
        return new Credentials
        {
            login = _settings.Database.DefaultUserUsername,
            password = _settings.Database.DefaultUserPassword
        };
    }

    /// <summary>
    /// Sets the default user's credentials
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("defaultuser")]
    public ActionResult SetDefaultUserCredentials(Credentials credentials)
    {
        try
        {
            _settings.Database.DefaultUserUsername = credentials.login;
            _settings.Database.DefaultUserPassword = credentials.password;
            return APIStatus.OK();
        }
        catch
        {
            return APIStatus.InternalError();
        }
    }

    /// <summary>
    /// Starts the server, or does nothing
    /// </summary>
    /// <returns></returns>
    [HttpGet("startserver")]
    public ActionResult StartServer()
    {
        if (ServerState.Instance.ServerOnline) return APIStatus.BadRequest("Already Running");
        if (ServerState.Instance.ServerStarting) return APIStatus.BadRequest("Already Starting");
        try
        {
            ShokoServer.RunWorkSetupDB();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "There was an error starting the server");
            return APIStatus.InternalError($"There was an error starting the server: {e}");
        }
        return APIStatus.OK();
    }

    #region 01. AniDB

    /// <summary>
    /// Set AniDB account credentials with a Credentials object
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("anidb")]
    public ActionResult SetAniDB(Credentials cred)
    {
        var details = new List<(string, string)>();
        if (string.IsNullOrEmpty(cred.login))
            details.Add(("login", "Username missing"));
        if (string.IsNullOrEmpty(cred.password))
            details.Add(("password", "Password missing"));
        if (details.Count > 0) return new APIMessage(400, "Login or Password missing", details);

        _settings.AniDb.Username = cred.login;
        _settings.AniDb.Password = cred.password;
        if (cred.port != 0)
            _settings.AniDb.ClientPort = cred.port;
        if (!string.IsNullOrEmpty(cred.apikey))
            _settings.AniDb.AVDumpKey = cred.apikey;
        if (cred.apiport != 0)
            _settings.AniDb.AVDumpClientPort = cred.apiport;

        return APIStatus.OK();
    }

    /// <summary>
    /// Test AniDB Creditentials
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("anidb/test")]
    public ActionResult TestAniDB()
    {
        var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
        handler.ForceLogout();
        handler.CloseConnections();

        Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(_settings.Culture);

        handler.Init(_settings.AniDb.Username, _settings.AniDb.Password,
            _settings.AniDb.ServerAddress,
            _settings.AniDb.ServerPort, _settings.AniDb.ClientPort);

        if (!handler.Login()) return APIStatus.BadRequest("Failed to log in");
        handler.ForceLogout();

        return APIStatus.OK();
    }

    /// <summary>
    /// Return existing login and ports for AniDB
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("anidb")]
    public ActionResult<Credentials> GetAniDB()
    {
        try
        {
            return new Credentials
            {
                login = _settings.AniDb.Username,
                port = _settings.AniDb.ClientPort,
                apiport = _settings.AniDb.AVDumpClientPort
            };
        }
        catch
        {
            return APIStatus.InternalError(
                "The ports are not set as integers. Set them and try again.\n\rThe default values are:\n\rAniDB Client Port: 4556\n\rAniDB AVDump Client Port: 4557");
        }
    }

    #endregion

    #region 02. Database

    /// <summary>
    /// Get Database Settings
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("database")]
    public ActionResult<DatabaseSettings> GetDatabaseSettings()
    {
        var settings = new DatabaseSettings
        {
            db_type = _settings.Database.Type,
            mysql_hostname = _settings.Database.Hostname,
            mysql_password = _settings.Database.Password,
            mysql_schemaname = _settings.Database.Schema,
            mysql_username = _settings.Database.Username,
            sqlite_databasefile = _settings.Database.SQLite_DatabaseFile,
            sqlserver_databasename = _settings.Database.Schema,
            sqlserver_databaseserver = _settings.Database.Hostname,
            sqlserver_password = _settings.Database.Password,
            sqlserver_username = _settings.Database.Username
        };

        return settings;
    }

    /// <summary>
    /// Set Database Settings
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPost("database")]
    public ActionResult SetDatabaseSettings(DatabaseSettings settings)
    {
        string dbtype = settings?.db_type;
        if (dbtype == null)
            return APIStatus.BadRequest("You must specify database type and use valid xml or json.");
        if (dbtype == Constants.DatabaseType.MySQL)
        {
            var details = new List<(string, string)>();
            if (string.IsNullOrEmpty(settings.mysql_hostname))
                details.Add(("mysql_hostname", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.mysql_schemaname))
                details.Add(("mysql_schemaname", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.mysql_username))
                details.Add(("mysql_username", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.mysql_password))
                details.Add(("mysql_password", "Must not be empty"));
            if (details.Count > 0)
                return new APIMessage(HttpStatusCode.BadRequest, "An invalid setting was passed", details);
            _settings.Database.Type = dbtype;
            _settings.Database.Hostname = settings.mysql_hostname;
            _settings.Database.Password = settings.mysql_password;
            _settings.Database.Schema = settings.mysql_schemaname;
            _settings.Database.Username = settings.mysql_username;
            return APIStatus.OK();
        }
        if (dbtype == Constants.DatabaseType.SqlServer)
        {
            var details = new List<(string, string)>();
            if (string.IsNullOrEmpty(settings.sqlserver_databaseserver))
                details.Add(("sqlserver_databaseserver", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.sqlserver_databasename))
                details.Add(("sqlserver_databaseserver", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.sqlserver_username))
                details.Add(("sqlserver_username", "Must not be empty"));
            if(string.IsNullOrEmpty(settings.sqlserver_password))
                details.Add(("sqlserver_password", "Must not be empty"));
            if (details.Count > 0)
                return new APIMessage(HttpStatusCode.BadRequest, "An invalid setting was passed", details);
            _settings.Database.Type = dbtype;
            _settings.Database.Hostname = settings.sqlserver_databaseserver;
            _settings.Database.Schema = settings.sqlserver_databasename;
            _settings.Database.Username = settings.sqlserver_username;
            _settings.Database.Password = settings.sqlserver_password;
            return APIStatus.OK();
        }
        if (dbtype == Constants.DatabaseType.Sqlite)
        {
            _settings.Database.Type = dbtype;
            if (!string.IsNullOrEmpty(settings.sqlite_databasefile))
                _settings.Database.SQLite_DatabaseFile = settings.sqlite_databasefile;
            return APIStatus.OK();
        }
        return APIStatus.BadRequest("An invalid setting was passed");
    }

    /// <summary>
    /// Test Database Connection with Current Settings
    /// </summary>
    /// <returns>200 if connection successful, 400 otherwise</returns>
    [Authorize("init")]
    [HttpGet("database/test")]
    public APIMessage TestDatabaseConnection()
    {
        if (_settings.Database.Type == Constants.DatabaseType.MySQL && new MySQL().TestConnection())
            return APIStatus.OK();

        if (_settings.Database.Type == Constants.DatabaseType.SqlServer  && new SQLServer().TestConnection())
            return APIStatus.OK();

        if (_settings.Database.Type == Constants.DatabaseType.Sqlite)
            return APIStatus.OK();

        return APIStatus.BadRequest("Failed to Connect");
    }

    /// <summary>
    /// Get SQL Server Instances Running on this Machine
    /// </summary>
    /// <returns>List of strings that may be passed as sqlserver_databaseserver</returns>
    [Authorize("init")]
    [HttpGet("database/sqlserverinstance")]
    public ActionResult<List<string>> GetMSSQLInstances()
    {
        return new ActionResult<List<string>>(new List<string>());
    }
    #endregion

    #region 03. Settings

    /// <summary>
    /// Return body of current working settings.json - this could act as backup
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("config")]
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
    [Authorize("init")]
    [HttpPost("config")]
    [Obsolete]
    public ActionResult ImportConfig(CL_ServerSettings settings)
    {
        return BadRequest("The model that this method takes is deprecated and will break the settings file. Use APIv3");
    }

    /// <summary>
    /// Return given setting
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpGet("setting")]
    private ActionResult<Setting> GetSetting(Setting setting)
    {
        return NoContent();
    }

    /// <summary>
    /// Set given setting
    /// </summary>
    /// <returns></returns>
    [Authorize("init")]
    [HttpPatch("setting")]
    public ActionResult SetSetting(Setting setting)
    {
        return NoContent();
    }

    #endregion
}
