using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SqlServer.Management.Smo;
using NLog;
using Shoko.Commons;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Databases;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;
using ServerStatus = Shoko.Server.API.v3.Models.Shoko.ServerStatus;

namespace Shoko.Server.API.v3.Controllers
{
    // ReSharper disable once UnusedMember.Global
    /// <summary>
    /// The init controller. Use this for first time setup. Settings will also allow full control to the init user.
    /// </summary>
    [ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
    [DatabaseBlockedExempt]
    [InitFriendly]
    public class InitController : BaseController
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Return current version of ShokoServer and several modules
        /// This will work after init
        /// </summary>
        /// <returns></returns>
        [HttpGet("Version")]
        public List<ComponentVersion> GetVersion()
        {
            List<ComponentVersion> list = new List<ComponentVersion>();

            ComponentVersion version = new ComponentVersion
            {
                Version = Utils.GetApplicationVersion(),
                Name = "Server"
            };
            list.Add(version);

            string versionExtra = Utils.GetApplicationExtraVersion();

            if (!string.IsNullOrEmpty(versionExtra))
            {
                version = new ComponentVersion
                {
                    Version = versionExtra,
                    Name = "ServerCommit"
                };
                list.Add(version);
            }

            version = new ComponentVersion
            {
                Version = Assembly.GetAssembly(typeof(FolderMappings)).GetName().Version.ToString(),
                Name = "Commons"
            };
            list.Add(version);

            version = new ComponentVersion
            {
                Version = Assembly.GetAssembly(typeof(AniDB_Anime)).GetName().Version.ToString(),
                Name = "Models"
            };
            list.Add(version);

            string dllpath = Assembly.GetEntryAssembly().Location;
            dllpath = Path.GetDirectoryName(dllpath);
            dllpath = Path.Combine(dllpath, "MediaInfo", "MediaInfo.exe");

            if (System.IO.File.Exists(dllpath))
            {
                version = new ComponentVersion
                {
                    Version = FileVersionInfo.GetVersionInfo(dllpath).FileVersion,
                    Name = "MediaInfo"
                };
                list.Add(version);
            }
            else
            {
                version = new ComponentVersion
                {
                    Version = @"MediaInfo not found",
                    Name = "MediaInfo"
                };
                list.Add(version);
            }

            // TODO new webui location
            if (System.IO.File.Exists("webui//index.ver"))
            {
                string webui_version = System.IO.File.ReadAllText("webui//index.ver");
                string[] versions = webui_version.Split('>');
                if (versions.Length == 2)
                {
                    version = new ComponentVersion
                    {
                        Name = "WebUI/" + versions[0],
                        Version = versions[1]
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
                Uptime = uptimemsg,
                DatabaseBlocked = ServerState.Instance.DatabaseBlocked
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
                Username = ServerSettings.Instance.Database.DefaultUserUsername,
                Password = ServerSettings.Instance.Database.DefaultUserPassword
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
        [HttpGet("startserver")]
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
                logger.Error($"There was an error starting the server: {e}");
                return InternalError($"There was an error starting the server: {e}");
            }
            return Ok();
        }

        /// <summary>
        /// Test Database Connection with Current Settings
        /// </summary>
        /// <returns>200 if connection successful, 400 otherwise</returns>
        [Authorize("init")]
        [HttpGet("database/test")]
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

        /// <summary>
        /// Get SQL Server Instances Running on this Machine
        /// </summary>
        /// <returns>List of strings that may be passed as sqlserver_databaseserver</returns>
        [Authorize("init")]
        [HttpGet("database/sqlserverinstance")]
        public ActionResult<List<string>> GetMSSQLInstances()
        {
            List<string> instances = new List<string>();

            DataTable dt = SmoApplication.EnumAvailableSqlServers();
            if (dt?.Rows.Count > 0) instances.AddRange(from DataRow row in dt.Rows select row[0].ToString());

            return instances;
        }
    }
}