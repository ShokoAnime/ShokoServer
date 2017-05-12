using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Commands;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Models;
using Shoko.Server.PlexAndKodi;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v2.Modules
{
    public class Core : Nancy.NancyModule
    {
        public static int version = 1;

        public Core() : base("/api")
        {
            // As this module requireAuthentication all request need to have apikey in header.

            this.RequiresAuthentication();

            #region 01.Settings

            Post["/config/port/set", true] = async (x,ct) => await Task.Factory.StartNew(SetPort);
            Get["/config/port/get", true] = async (x,ct) => await Task.Factory.StartNew(GetPort);
            Post["/config/imagepath/set", true] = async (x,ct) => await Task.Factory.StartNew(SetImagepath);
            Get["/config/imagepath/get", true] = async (x,ct) => await Task.Factory.StartNew(GetImagepath);
            Get["/config/export", true] = async (x,ct) => await Task.Factory.StartNew(ExportConfig);
            Post["/config/import", true] = async (x,ct) => await Task.Factory.StartNew(ImportConfig);

            #endregion

            #region 02.AniDB

            Post["/anidb/set", true] = async (x,ct) => await Task.Factory.StartNew(SetAniDB);
            Get["/anidb/get", true] = async (x,ct) => await Task.Factory.StartNew(GetAniDB);
            Get["/anidb/test", true] = async (x,ct) => await Task.Factory.StartNew(TestAniDB);
            Get["/anidb/votes/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncAniDBVotes);
            Get["/anidb/list/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncAniDBList);
            Get["/anidb/update", true] = async (x,ct) => await Task.Factory.StartNew(UpdateAllAniDB);

            #endregion

            #region 03.MyAnimeList

            Post["/mal/set", true] = async (x,ct) => await Task.Factory.StartNew(SetMAL);
            Get["/mal/get", true] = async (x,ct) => await Task.Factory.StartNew(GetMAL);
            Get["/mal/test", true] = async (x,ct) => await Task.Factory.StartNew(TestMAL);
            Get["/mal/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanMAL);
            Get["/mal/download", true] = async (x,ct) => await Task.Factory.StartNew(DownloadFromMAL);
            Get["/mal/upload", true] = async (x,ct) => await Task.Factory.StartNew(UploadToMAL);
            //Get["/mal/votes/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncMALVotes); <-- not implemented as CommandRequest

            #endregion

            #region 04.Trakt

            Post["/trakt/set", true] = async (x,ct) => await Task.Factory.StartNew(SetTraktPIN);
            Get["/trakt/get", true] = async (x,ct) => await Task.Factory.StartNew(GetTrakt);
            Get["/trakt/create", true] = async (x,ct) => await Task.Factory.StartNew(CreateTrakt);
            Get["/trakt/sync", true] = async (x,ct) => await Task.Factory.StartNew(SyncTrakt);
            Get["/trakt/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanTrakt);

            #endregion

            #region 05.TvDB

            Get["/tvdb/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanTvDB);

            #endregion

            #region 06.MovieDB

            Get["/moviedb/update", true] = async (x,ct) => await Task.Factory.StartNew(ScanMovieDB);

            #endregion

            #region 07.User

            Get["/user/list", true] = async (x,ct) => await Task.Factory.StartNew(GetUsers);
            Post["/user/create", true] = async (x,ct) => await Task.Factory.StartNew(CreateUser);
            Post["/user/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteUser);
            Post["/user/password", true] = async (x,ct) => await Task.Factory.StartNew(ChangePassword);
            Post["/user/password/{uid}", true] = async (x,ct) => await Task.Factory.StartNew(() => ChangePassword(x.uid));

            #endregion

            #region 08.OS-based operations

            Get["/os/folder/base", true] = async (x,ct) => await Task.Factory.StartNew(GetOSBaseFolder);
            Post["/os/folder", true] = async (x,ct) => await Task.Factory.StartNew(() => GetOSFolder(x.folder));
            Get["/os/drives", true] = async (x,ct) => await Task.Factory.StartNew(GetOSDrives);

            #endregion

            #region 09.Cloud accounts

            Get["/cloud/list", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccounts);
            Get["/cloud/count", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccountsCount);
            Post["/cloud/add", true] = async (x,ct) => await Task.Factory.StartNew(AddCloudAccount);
            Post["/cloud/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteCloudAccount);
            Get["/cloud/import", true] = async (x,ct) => await Task.Factory.StartNew(RunCloudImport);

            #endregion

            #region 10.Logs

            Get["/log/get", true] = async (x,ct) => await Task.Factory.StartNew(() => GetLog(10, 0));
            Get["/log/get/{max}/{position}", true] = async (x,ct) => await Task.Factory.StartNew(() => GetLog((int) x.max, (int) x.position));
            Post["/log/rotate", true] = async (x,ct) => await Task.Factory.StartNew(SetRotateLogs);
            Get["/log/rotate", true] = async (x,ct) => await Task.Factory.StartNew(GetRotateLogs);
            Get["/log/rotate/start", true] = async (x,ct) => await Task.Factory.StartNew(StartRotateLogs);

            #endregion
        }

        #region 01.Settings

        /// <summary>
        /// Set JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object SetPort()
        {
            Creditentials cred = this.Bind();
            if (cred.port != 0)
            {
                ServerSettings.JMMServerPort = cred.port.ToString();
                return APIStatus.statusOK();
            }
            else
            {
                return new APIMessage(400, "Port Missing");
            }
        }

        /// <summary>
        /// Get JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object GetPort()
        {
            dynamic x = new System.Dynamic.ExpandoObject();
            x.port = int.Parse(ServerSettings.JMMServerPort);
            return x;
        }

        /// <summary>
        /// Set Imagepath as default or custom
        /// </summary>
        /// <returns></returns>
        private object SetImagepath()
        {
            ImagePath imagepath = this.Bind();
            if (imagepath.isdefault)
            {
                ServerSettings.ImagesPath = ServerSettings.DefaultImagePath;
                return APIStatus.statusOK();
            }
            else
            {
                if (!String.IsNullOrEmpty(imagepath.path) && imagepath.path != "")
                {
                    if (Directory.Exists(imagepath.path))
                    {
                        ServerSettings.ImagesPath = imagepath.path;
                        return APIStatus.statusOK();
                    }
                    else
                    {
                        return new APIMessage(404, "Directory Not Found on Host");
                    }
                }
                else
                {
                    return new APIMessage(400, "Path Missing");
                }
            }
        }

        /// <summary>
        /// Return ImagePath object
        /// </summary>
        /// <returns></returns>
        private object GetImagepath()
        {
            ImagePath imagepath = new ImagePath();
            imagepath.path = ServerSettings.ImagesPath;
            imagepath.isdefault = ServerSettings.ImagesPath == ServerSettings.DefaultImagePath;
            return imagepath;
        }

        /// <summary>
        /// Return body of current working settings.json - this could act as backup
        /// </summary>
        /// <returns></returns>
        private object ExportConfig()
        {
            try
            {
                return ServerSettings.appSettings;
            }
            catch
            {
                return APIStatus.internalError("Error while reading settings.");
            }
        }

        private object ImportConfig()
        {
            CL_ServerSettings settings = this.Bind();
            string raw_settings = settings.ToJSON();
            if (raw_settings.Length != new CL_ServerSettings().ToJSON().Length)
            {
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
            else
            {
                return APIStatus.badRequest("Empty settings are not allowed");
            }
        }

        private object GetSetting(string setting)
        {
            return ServerSettings.Get(setting);
        }

        private object SetSetting(string setting, string value)
        {
            return ServerSettings.Set(setting, value);
        }

        #endregion

        #region 02.AniDB

        /// <summary>
        /// Set AniDB account with login, password and client port
        /// </summary>
        /// <returns></returns>
        private object SetAniDB()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != "" && !String.IsNullOrEmpty(cred.password) &&
                cred.password != "")
            {
                ServerSettings.AniDB_Username = cred.login;
                ServerSettings.AniDB_Password = cred.password;
                if (cred.port != 0)
                {
                    ServerSettings.AniDB_ClientPort = cred.port.ToString();
                }
                return APIStatus.statusOK();
            }
            else
            {
                return new APIMessage(400, "Login and Password missing");
            }
        }

        /// <summary>
        /// Test AniDB Creditentials
        /// </summary>
        /// <returns></returns>
        private object TestAniDB()
        {
            ShokoService.AnidbProcessor.ForceLogout();
            ShokoService.AnidbProcessor.CloseConnections();

            Thread.Sleep(1000);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            ShokoService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

            if (ShokoService.AnidbProcessor.Login())
            {
                ShokoService.AnidbProcessor.ForceLogout();
                return APIStatus.statusOK();
            }
            else
            {
                return APIStatus.unauthorized();
            }
        }

        /// <summary>
        /// Return login/password/port of used AniDB
        /// </summary>
        /// <returns></returns>
        private object GetAniDB()
        {
            Creditentials cred = new Creditentials();
            cred.login = ServerSettings.AniDB_Username;
            cred.password = ServerSettings.AniDB_Password;
            cred.port = int.Parse(ServerSettings.AniDB_ClientPort);
            return cred;
        }

        /// <summary>
        /// Sync votes bettween Local and AniDB and only upload to MAL
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBVotes()
        {
            //TODO APIv2: Command should be split into AniDb/MAL sepereate
            CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
            cmdVotes.Save();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Sync AniDB List
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBList()
        {
            ShokoServer.SyncMyList();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Update all series infromation from AniDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllAniDB()
        {
            Importer.RunImport_UpdateAllAniDB();
            return APIStatus.statusOK();
        }

        #endregion

        #region 03.MyAnimeList

        /// <summary>
        /// Set MAL account with login, password
        /// </summary>
        /// <returns></returns>
        private object SetMAL()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != "" && !String.IsNullOrEmpty(cred.password) &&
                cred.password != "")
            {
                ServerSettings.MAL_Username = cred.login;
                ServerSettings.MAL_Password = cred.password;
                return APIStatus.statusOK();
            }
            else
            {
                return new APIMessage(400, "Login and Password missing");
            }
        }

        /// <summary>
        /// Return current used MAL Creditentials
        /// </summary>
        /// <returns></returns>
        private object GetMAL()
        {
            Creditentials cred = new Creditentials();
            cred.login = ServerSettings.MAL_Username;
            cred.password = ServerSettings.MAL_Password;
            return cred;
        }

        /// <summary>
        /// Test MAL Creditionals against MAL
        /// </summary>
        /// <returns></returns>
        private object TestMAL()
        {
            if (Providers.MyAnimeList.MALHelper.VerifyCredentials())
            {
                return APIStatus.statusOK();
            }
            else
            {
                return APIStatus.unauthorized();
            }
        }

        /// <summary>
        /// Scan MAL
        /// </summary>
        /// <returns></returns>
        private object ScanMAL()
        {
            Importer.RunImport_ScanMAL();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Download Watched States from MAL
        /// </summary>
        /// <returns></returns>
        private object DownloadFromMAL()
        {
            CommandRequest_MALDownloadStatusFromMAL cmd = new CommandRequest_MALDownloadStatusFromMAL();
            cmd.Save();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Upload Watched States to MAL
        /// </summary>
        /// <returns></returns>
        private object UploadToMAL()
        {
            CommandRequest_MALUploadStatusToMAL cmd = new CommandRequest_MALUploadStatusToMAL();
            cmd.Save();
            return APIStatus.statusOK();
        }

        #endregion

        #region 04.Trakt

        /// <summary>
        /// Set Trakt PIN
        /// </summary>
        /// <returns></returns>
        private object SetTraktPIN()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.token) && cred.token != "")
            {
                ServerSettings.Trakt_PIN = cred.token;
                return APIStatus.statusOK();
            }
            else
            {
                return new APIMessage(400, "Token missing");
            }
        }

        /// <summary>
        /// Create AuthToken and RefreshToken from PIN
        /// </summary>
        /// <returns></returns>
        private object CreateTrakt()
        {
            if (Providers.TraktTV.TraktTVHelper.EnterTraktPIN(ServerSettings.Trakt_PIN) == "Success")
            {
                return APIStatus.statusOK();
            }
            else
            {
                return APIStatus.unauthorized();
            }
        }

        /// <summary>
        /// Return trakt authtoken
        /// </summary>
        /// <returns></returns>
        private object GetTrakt()
        {
            Creditentials cred = new Creditentials();
            cred.token = ServerSettings.Trakt_AuthToken;
            cred.refresh_token = ServerSettings.Trakt_RefreshToken;
            return cred;
        }

        /// <summary>
        /// Sync Trakt Collection
        /// </summary>
        /// <returns></returns>
        private object SyncTrakt()
        {
            if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
            {
                CommandRequest_TraktSyncCollection cmd = new CommandRequest_TraktSyncCollection(true);
                cmd.Save();
                return APIStatus.statusOK();
            }
            else
            {
                return new APIMessage(204, "Trak is not enabled or you missing authtoken");
            }
        }

        /// <summary>
        /// Scan Trakt
        /// </summary>
        /// <returns></returns>
        private object ScanTrakt()
        {
            Importer.RunImport_ScanTrakt();
            return APIStatus.statusOK();
        }

        #endregion

        #region 05.TvDB

        /// <summary>
        /// Scan TvDB
        /// </summary>
        /// <returns></returns>
        private object ScanTvDB()
        {
            Importer.RunImport_ScanTvDB();
            return APIStatus.statusOK();
        }

        #endregion

        #region 06.MovieDB

        /// <summary>
        /// Scan MovieDB
        /// </summary>
        /// <returns></returns>
        private object ScanMovieDB()
        {
            Importer.RunImport_ScanMovieDB();
            return APIStatus.statusOK();
        }

        #endregion

        #region 07.User

        /// <summary>
        /// return Dictionary int = id, string = username
        /// </summary>
        /// <returns></returns>
        private object GetUsers()
        {
            return new CommonImplementation().GetUsers();
        }

        /// <summary>
        /// Create user from Contract_JMMUser
        /// </summary>
        /// <returns></returns>
        private object CreateUser()
        {
            Request request = this.Request;
            SVR_JMMUser _user = (SVR_JMMUser) this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                JMMUser user = this.Bind();
                user.Password = Digest.Hash(user.Password);
                user.HideCategories = "";
                user.PlexUsers = "";
                if (new ShokoServiceImplementation().SaveUser(user) == "")
                {
                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.internalError();
                }
            }
            else
            {
                return APIStatus.adminNeeded();
            }
        }

        /// <summary>
        ///  change current user password
        /// </summary>
        /// <returns></returns>
        private object ChangePassword()
        {
            Request request = this.Request;
            SVR_JMMUser user = (SVR_JMMUser) this.Context.CurrentUser;
            return ChangePassword(user.JMMUserID);
        }

        /// <summary>
        /// change given user (by uid) password
        /// </summary>
        /// <returns></returns>
        private object ChangePassword(int uid)
        {
            Request request = this.Request;
            SVR_JMMUser _user = (SVR_JMMUser) this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                SVR_JMMUser user = this.Bind();
                if (new ShokoServiceImplementation().ChangePassword(uid, user.Password) == "")
                {
                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.internalError();
                }
            }
            else
            {
                return APIStatus.adminNeeded();
            }
        }

        /// <summary>
        /// Delete user from his ID
        /// </summary>
        /// <returns></returns>
        private object DeleteUser()
        {
            Request request = this.Request;
            SVR_JMMUser _user = (SVR_JMMUser) this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                SVR_JMMUser user = this.Bind();
                if (new ShokoServiceImplementation().DeleteUser(user.JMMUserID) == "")
                {
                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.internalError();
                }
            }
            else
            {
                return APIStatus.adminNeeded();
            }
        }

        #endregion

        #region 8.OS-based operations

        /// <summary>
        /// Return OSFolder object that is a folder from which jmmserver is running
        /// </summary>
        /// <returns></returns>
        private object GetOSBaseFolder()
        {
            OSFolder dir = new OSFolder();
            dir.full_path = Environment.CurrentDirectory;
            System.IO.DirectoryInfo dir_info = new DirectoryInfo(dir.full_path);
            dir.dir = dir_info.Name;
            dir.subdir = new List<OSFolder>();

            foreach (DirectoryInfo info in dir_info.GetDirectories())
            {
                OSFolder subdir = new OSFolder();
                subdir.full_path = info.FullName;
                subdir.dir = info.Name;
                dir.subdir.Add(subdir);
            }
            return dir;
        }

        /// <summary>
        /// Return OSFolder object of directory that was given via 
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private object GetOSFolder(string folder)
        {
            OSFolder dir = this.Bind();
            if (!String.IsNullOrEmpty(dir.full_path))
            {
                System.IO.DirectoryInfo dir_info = new DirectoryInfo(dir.full_path);
                dir.dir = dir_info.Name;
                dir.subdir = new List<OSFolder>();

                foreach (DirectoryInfo info in dir_info.GetDirectories())
                {
                    OSFolder subdir = new OSFolder();
                    subdir.full_path = info.FullName;
                    subdir.dir = info.Name;
                    dir.subdir.Add(subdir);
                }
                return dir;
            }
            else
            {
                return new APIMessage(400, "full_path missing");
            }
        }

        /// <summary>
        /// Return OSFolder with subdirs as every driver on local system
        /// </summary>
        /// <returns></returns>
        private object GetOSDrives()
        {
            string[] drives = System.IO.Directory.GetLogicalDrives();
            OSFolder dir = new OSFolder();
            dir.dir = "/";
            dir.full_path = "/";
            dir.subdir = new List<OSFolder>();
            foreach (string str in drives)
            {
                OSFolder driver = new OSFolder();
                driver.dir = str;
                driver.full_path = str;
                dir.subdir.Add(driver);
            }

            return dir;
        }

        #endregion

        #region 09.Cloud Accounts

        private object GetCloudAccounts()
        {
            // TODO APIv2: Cloud
            return APIStatus.notImplemented();
        }

        private object GetCloudAccountsCount()
        {
            // TODO APIv2: Cloud
            return APIStatus.notImplemented();
        }

        private object AddCloudAccount()
        {
            // TODO APIv2: Cloud
            return APIStatus.notImplemented();
        }

        private object DeleteCloudAccount()
        {
            // TODO APIv2: Cloud
            return APIStatus.notImplemented();
        }

        private object RunCloudImport()
        {
            ShokoServer.RunImport();
            return APIStatus.statusOK();
        }

        #endregion

        #region 10. Logs

        /// <summary>
        /// Run LogRotator with current settings
        /// </summary>
        /// <returns></returns>
        private object StartRotateLogs()
        {
            ShokoServer.logrotator.Start();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Set settings for LogRotator
        /// </summary>
        /// <returns></returns>
        private object SetRotateLogs()
        {
            Request request = this.Request;
            SVR_JMMUser user = (SVR_JMMUser) this.Context.CurrentUser;
            Logs rotator = this.Bind();

            if (user.IsAdmin == 1)
            {
                ServerSettings.RotateLogs = rotator.rotate;
                ServerSettings.RotateLogs_Zip = rotator.zip;
                ServerSettings.RotateLogs_Delete = rotator.delete;
                ServerSettings.RotateLogs_Delete_Days = rotator.days.ToString();

                return APIStatus.statusOK();
            }
            else
            {
                return APIStatus.adminNeeded();
            }
        }

        /// <summary>
        /// Get settings for LogRotator
        /// </summary>
        /// <returns></returns>
        private object GetRotateLogs()
        {
            Logs rotator = new Logs();
            rotator.rotate = ServerSettings.RotateLogs;
            rotator.zip = ServerSettings.RotateLogs_Zip;
            rotator.delete = ServerSettings.RotateLogs_Delete;
            int day = 0;
            if (!String.IsNullOrEmpty(ServerSettings.RotateLogs_Delete_Days))
            {
                int.TryParse(ServerSettings.RotateLogs_Delete_Days, out day);
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
        private object GetLog(int lines, int position)
        {
            string log_file = LogRotator.GetCurrentLogFile();
            if (string.IsNullOrEmpty(log_file))
            {
                return APIStatus.notFound404("Could not find current log name. Sorry");
            }

            if (!File.Exists(log_file))
            {
                return APIStatus.notFound404();
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            FileStream fs = File.OpenRead(@log_file);

            if (position >= fs.Length)
            {
                result.Add("position", fs.Length);
                result.Add("lines", new string[] { });
                return result;
            }

            List<string> logLines = new List<string>();

            LogReader reader = new LogReader(fs, position);
            for (int i = 0; i < lines; i++)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                logLines.Add(line);
            }
            result.Add("position", reader.Position);
            result.Add("lines", logLines.ToArray());
            return result;
        }

        #endregion
    }
}