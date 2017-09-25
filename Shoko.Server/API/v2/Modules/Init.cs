using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Pri.LongPath;
using Shoko.Models.Client;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules
{
    // ReSharper disable once UnusedMember.Global
    public class Init : NancyModule
    {
        /// <inheritdoc />
        /// <summary>
        /// Preinit Module for connection testing and setup
        /// Settings will be loaded prior to this starting
        /// Unless otherwise noted, these will only work before server init
        /// </summary>
        public Init() : base("/api/init")
        {
            // Get version, regardless of server status
            // This will work after init
            Get["/version", true] = async (x,ct) => await Task.Factory.StartNew(GetVersion, ct);

            // Get the startup state
            // This will work after init
            Get["/status", true] = async (x, ct) => await Task.Factory.StartNew(GetServerStatus, ct);

            // Get the Default User Credentials
            Get["/defaultuser", true] = async (x, ct) => await Task.Factory.StartNew(GetDefaultUserCredentials, ct);

            // Set the Default User Credentials
            // Pass this a Credentials object
            Post["/defaultuser", true] = async (x, ct) => await Task.Factory.StartNew(SetDefaultUserCredentials, ct);

            // Set AniDB user/pass
            // Pass this a Credentials object
            Post["/anidb", true] = async (x,ct) => await Task.Factory.StartNew(SetAniDB, ct);

            // Get existing AniDB user, don't provide pass
            Get["/anidb", true] = async (x,ct) => await Task.Factory.StartNew(GetAniDB, ct);

            // Test AniDB login
            Get["/anidb/test", true] = async (x,ct) => await Task.Factory.StartNew(TestAniDB, ct);

            // TODO Database Setting and Connection Endpoints
            // set db type
            // db location and test connection
            // individual specific db settings

            // Get the whole settings file
            Get["/config", true] = async (x,ct) => await Task.Factory.StartNew(ExportConfig, ct);

            // Replace the whole settings file
            Post["/config", true] = async (x,ct) => await Task.Factory.StartNew(ImportConfig, ct);

            // Get a single setting value
            Get["/setting", true] = async (x, ct) => await Task.Factory.StartNew(GetSetting, ct);

            // Set a single setting value
            Patch["/setting", true] = async (x, ct) => await Task.Factory.StartNew(SetSetting, ct);

            // Start the server
            Get["/startserver", true] = async (x, ct) => await Task.Factory.StartNew(StartServer, ct);
        }

        /// <summary>
        /// Return current version of ShokoServer
        /// This will work after init
        /// </summary>
        /// <returns></returns>
        private object GetVersion()
        {
            List<ComponentVersion> list = new List<ComponentVersion>();

            ComponentVersion version = new ComponentVersion
            {
                version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(),
                name = "server"
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "auth_module",
                version = Auth.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "common_module",
                version = Common.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "core_module",
                version = Core.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "database_module",
                version = Database.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "dev_module",
                version = Dev.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "unauth_module",
                version = Unauth.version.ToString()
            };
            list.Add(version);

            version = new ComponentVersion
            {
                name = "webui_module",
                version = Webui.version.ToString()
            };
            list.Add(version);

            if (File.Exists("webui//index.ver"))
            {
                string webui_version = File.ReadAllText("webui//index.ver");
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
        private object GetServerStatus()
        {
            ServerStatus status = new ServerStatus
            {
                server_started = ServerState.Instance.ServerOnline,
                startup_state = ServerState.Instance.CurrentSetupStatus,
                first_run = ServerSettings.FirstRun,
                startup_failed = ServerState.Instance.StartupFailed,
                startup_failed_error_message = ServerState.Instance.StartupFailedMessage
            };
            return status;
        }

        /// <summary>
        /// Gets the Default user's credentials. Will only return on first run
        /// </summary>
        /// <returns></returns>
        private object GetDefaultUserCredentials()
        {
            if (!ServerSettings.FirstRun || ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only request the default user's credentials on first run");

            return new Credentials
            {
                login = ServerSettings.DefaultUserUsername,
                password = ServerSettings.DefaultUserPassword
            };
        }

        /// <summary>
        /// Sets the default user's credentials
        /// </summary>
        /// <returns></returns>
        private object SetDefaultUserCredentials()
        {
            if (!ServerSettings.FirstRun || ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only set the default user's credentials on first run");

            try
            {
                Credentials credentials = this.Bind();
                ServerSettings.DefaultUserUsername = credentials.login;
                ServerSettings.DefaultUserPassword = credentials.password;
                return APIStatus.statusOK();
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        /// <summary>
        /// Starts the server, or does nothing
        /// </summary>
        /// <returns></returns>
        private object StartServer()
        {
            if (ServerState.Instance.ServerOnline) return APIStatus.badRequest("Already Running");
            if (ServerState.Instance.ServerStarting) return APIStatus.badRequest("Already Starting");
            ShokoServer.RunWorkSetupDB();
            return APIStatus.statusOK();
        }

        #region 01. AniDB

        /// <summary>
        /// Set AniDB account credentials with a Credentials object
        /// </summary>
        /// <returns></returns>
        private object SetAniDB()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            Credentials cred = this.Bind();
            if (string.IsNullOrEmpty(cred.login) || string.IsNullOrEmpty(cred.password))
                return new APIMessage(400, "Login and Password missing");

            ServerSettings.AniDB_Username = cred.login;
            ServerSettings.AniDB_Password = cred.password;
            if (cred.port != 0)
                ServerSettings.AniDB_ClientPort = cred.port.ToString();
            if (!string.IsNullOrEmpty(cred.apikey))
                ServerSettings.AniDB_AVDumpKey = cred.apikey;
            if (cred.apiport != 0)
                ServerSettings.AniDB_AVDumpClientPort = cred.apiport.ToString();

            return APIStatus.statusOK();
        }

        /// <summary>
        /// Test AniDB Creditentials
        /// </summary>
        /// <returns></returns>
        private object TestAniDB()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            ShokoService.AnidbProcessor.ForceLogout();
            ShokoService.AnidbProcessor.CloseConnections();

            Thread.Sleep(1000);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

            if (!ShokoService.AnidbProcessor.Login()) return APIStatus.unauthorized();
            ShokoService.AnidbProcessor.ForceLogout();

            return APIStatus.statusOK();
        }

        /// <summary>
        /// Return existing login and ports for AniDB
        /// </summary>
        /// <returns></returns>
        private object GetAniDB()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            try
            {
                Credentials cred = new Credentials
                {
                    login = ServerSettings.AniDB_Username,
                    port = int.Parse(ServerSettings.AniDB_ClientPort),
                    apiport = int.Parse(ServerSettings.AniDB_AVDumpClientPort)
                };
                return cred;
            }
            catch
            {
                return APIStatus.internalError(
                    "The ports are not set as integers. Set them and try again.\n\rThe default values are:\n\rAniDB Client Port: 4556\n\rAniDB AVDump Client Port: 4557");
            }
        }

        #endregion

        #region 02. Settings

        /// <summary>
        /// Return body of current working settings.json - this could act as backup
        /// </summary>
        /// <returns></returns>
        private object ExportConfig()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            try
            {
                return ServerSettings.appSettings;
            }
            catch
            {
                return APIStatus.internalError("Error while reading settings.");
            }
        }

        /// <summary>
        /// Import config file that was sent to in API body - this act as import from backup
        /// </summary>
        /// <returns>APIStatus</returns>
        private object ImportConfig()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            CL_ServerSettings settings = this.Bind();
            string raw_settings = settings.ToJSON();

            if (raw_settings.Length == new CL_ServerSettings().ToJSON().Length)
                return APIStatus.badRequest("Empty settings are not allowed");

            string path = Path.Combine(ServerSettings.ApplicationPath, "temp.json");
            File.WriteAllText(path, raw_settings, System.Text.Encoding.UTF8);
            try
            {
                ServerSettings.LoadSettingsFromFile(path, true);
                return APIStatus.statusOK();
            }
            catch
            {
                return APIStatus.internalError("Error while importing settings");
            }
        }

        /// <summary>
        /// Return given setting
        /// </summary>
        /// <returns></returns>
        private object GetSetting()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            try
            {
                // TODO Refactor Settings to a POCO that is serialized, and at runtime, build a dictionary of types to validate against
                Settings setting = this.Bind();
                if (string.IsNullOrEmpty(setting?.setting)) return APIStatus.badRequest("An invalid setting was passed");
                try
                {
                    var value = typeof(ServerSettings).GetProperty(setting.setting)?.GetValue(null, null);
                    if (value == null) return APIStatus.badRequest("An invalid setting was passed");

                    Settings return_setting = new Settings
                    {
                        setting = setting.setting,
                        value = value.ToString()
                    };
                    return return_setting;
                }
                catch
                {
                    return APIStatus.badRequest("An invalid setting was passed");
                }
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        /// <summary>
        /// Set given setting
        /// </summary>
        /// <returns></returns>
        private object SetSetting()
        {
            if (ServerState.Instance.ServerOnline || ServerState.Instance.ServerStarting)
                return APIStatus.badRequest("You may only do this before server init");

            // TODO Refactor Settings to a POCO that is serialized, and at runtime, build a dictionary of types to validate against
            try
            {
                Settings setting = this.Bind();
                if (string.IsNullOrEmpty(setting.setting))
                    return APIStatus.badRequest("An invalid setting was passed");

                if (setting.value == null) return APIStatus.badRequest("An invalid value was passed");

                var property = typeof(ServerSettings).GetProperty(setting.setting);
                if (property == null) return APIStatus.badRequest("An invalid setting was passed");
                if (!property.CanWrite) return APIStatus.badRequest("An invalid setting was passed");
                var settingType = property.PropertyType;
                try
                {
                    var converter = TypeDescriptor.GetConverter(settingType);
                    if (!converter.CanConvertFrom(typeof(string)))
                        return APIStatus.badRequest("An invalid value was passed");
                    var value = converter.ConvertFromInvariantString(setting.value);
                    if (value == null) return APIStatus.badRequest("An invalid value was passed");
                    property.SetValue(null, value);
                }
                catch
                {
                    // ignore, we are returning the error below
                }

                return APIStatus.badRequest("An invalid value was passed");
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        #endregion
    }
}