using Nancy;
using Nancy.Security;
using System;
using JMMServer.API.Model;
using Nancy.ModelBinding;
using JMMServer.Entities;
using JMMContracts;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using JMMServer.Commands;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using JMMServer.Repositories.Direct;
using JMMServer.Utilities;

namespace JMMServer.API
{
    //As responds for this API we throw object that will be converted to json/xml or standard http codes (HttpStatusCode)
    public class APIv2_core_Module : Nancy.NancyModule
    {
        public static int version = 1;
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
        //routes are named with twitter api style
        //every function with summary is implemented 
        //private funtions are the ones for api calls directly and internal ones are support function for private ones
        public APIv2_core_Module() : base("/api")
        {
            // As this module requireAuthentication all request need to have apikey in header.

            this.RequiresAuthentication();

            // 1. import folders
            Get["/folder/list"] = x => { return GetFolders(); };
            Get["/folder/count"] = x => { return CountFolders(); };
            Post["/folder/add"] = x => { return AddFolder(); };
            Post["/folder/edit"] = x => { return EditFolder(); };
            Post["/folder/delete"] = x => { return DeleteFolder(); };
            Get["/folder/import"] = _ => { return RunImport(); };

            // 2. upnp 
            Post["/upnp/list"] = x => { return ListUPNP(); };
            Post["/upnp/add"] = x => { return AddUPNP(); };
            Post["/upnp/delete"] = x => { return DeleteUPNP(); };

            // 3. Settings
            Post["/config/port/set"] = _ => { return SetPort(); };
            Get["/config/port/get"] = _ => { return GetPort(); };
            Post["/config/imagepath/set"] = _ => { return SetImagepath(); };
            Get["/config/imagepath/get"] = _ => { return GetImagepath(); };
            Get["/config/export"] = _ => { return ExportConfig(); };
            Post["/config/import"] = _ => { return ImportConfig(); };

            // 4. AniDB
            Post["/anidb/set"] = _ => { return SetAniDB(); };
            Get["/anidb/get"] = _ => { return GetAniDB(); };
            Get["/anidb/test"] = _ => { return TestAniDB(); };
            Get["/anidb/votes/sync"] = _ => { return SyncAniDBVotes(); };
            Get["/anidb/list/sync"] = _ => { return SyncAniDBList(); };
            Get["/anidb/update"] = _ => { return UpdateAllAniDB(); };

            // 5. MyAnimeList
            Post["/mal/set"] = _ => { return SetMAL(); };
            Get["/mal/get"] = _ => { return GetMAL(); };
            Get["/mal/test"] = _ => { return TestMAL(); };
            //Get["/mal/votes/sync"] = _ => { return SyncMALVotes(); }; <-- not implemented as CommandRequest

            // 6. Trakt
            Post["/trakt/set"] = _ => { return SetTraktPIN(); };
            Get["/trakt/get"] = _ => { return GetTrakt(); };
            Get["/trakt/create"] = _ => { return CreateTrakt(); };
            Get["/trakt/sync"] = _ => { return SyncTrakt(); };
            Get["/trakt/update"] = _ => { return UpdateAllTrakt(); };

            // 7. TvDB
            Get["/tvdb/update"] = _ => { return UpdateAllTvDB(); };

            // 8. Actions
            Get["/remove_missing_files"] = _ => { return RemoveMissingFiles(); };
            Get["/stats_update"] = _ => { return UpdateStats(); };
            Get["/mediainfo_update"] = _ => { return UpdateMediaInfo(); };
            Get["/hash/sync"] = _ => { return HashSync(); };

            // 9. Misc
            Get["/myid/get"] = _ => { return MyID(); };
            Get["/news/get"] = _ => { return GetNews(5); };

            // 10. User
            Get["/user/list"] = _ => { return GetUsers(); };
            Post["/user/create"] = _ => { return CreateUser(); };
            Post["/user/delete"] = _ => { return DeleteUser(); };
            Post["/user/password"] = _ => { return ChangePassword(); };
            Post["/user/password/{uid}"] = x => { return ChangePassword(x.uid); };

            // 11. Queue
            Get["/queue/get"] = _ => { return GetQueue(); };
            Get["/queue/pause"] = _ => { return PauseQueue(); };
            Get["/queue/start"] = _ => { return StartQueue(); };
            Get["/queue/hash/get"] = _ => { return GetHasherQueue(); };
            Get["/queue/hash/pause"] = _ => { return PauseHasherQueue(); };
            Get["/queue/hash/start"] = _ => { return StartHasherQueue(); };
            Get["/queue/hash/clear"] = _ => { return ClearHasherQueue(); };
            Get["/queue/general/get"] = _ => { return GetGeneralQueue(); };
            Get["/queue/general/pause"] = _ => { return PauseGeneralQueue(); };
            Get["/queue/general/start"] = _ => { return StartGeneralQueue(); };
            Get["/queue/general/clear"] = _ => { return ClearGeneralQueue(); };
            Get["/queue/images/get"] = _ => { return GetImagesQueue(); };
            Get["/queue/images/pause"] = _ => { return PauseImagesQueue(); };
            Get["/queue/images/start"] = _ => { return StartImagesQueue(); };
            Get["/queue/images/clear"] = _ => { return ClearImagesQueue(); };

            // 12. Files
            Get["/file/list"] = _ => { return GetAllFiles(); };
            Get["/file/count"] = _ => { return CountFiles(); };
            Get["/file/{id}"] = x => { return GetFileById(x.id); };
            Get["/file/recent"] = x => { return GetRecentFiles(10); };
            Get["/file/recent/{max}"] = x => { return GetRecentFiles((int)x.max); };
            Get["/file/unrecognised"] = x => { return GetUnrecognisedFiles(10); };
            Get["/file/unrecognised/{max}"] = x => { return GetUnrecognisedFiles((int)x.max); };

            // 13. Episodes
            Get["/ep/list"] = _ => { return GetAllEpisodes(); ; };
            Get["/ep/{id}"] = x => { return GetEpisodeById(x.id); };
            //Get["/ep/{id}/image"] = x => { return GetEpisodeImage(x.id); };
            Get["/ep/recent"] = x => { return GetRecentEpisodes(10); };
            Get["/ep/recent/{max}"] = x => { return GetRecentEpisodes((int)x.max); };
            Post["/ep/watch"] = x => { return MarkEpisodeWatched(true); };
            Post["/ep/unwatch"] = x => { return MarkEpisodeWatched(false); };
            Post["/ep/vote"] = x => { return VoteOnEpisode(); };
            Post["/ep/trakt"] = x => { return EpisodeScrobble(); };

            // 14. Series
            Get["/serie/list"] = _ => { return GetAllSeries(); ; };
            Get["/serie/count"] = _ => { return CountSerie(); ; };
            Get["/serie/{id}"] = x => { return GetSerieById(x.id); ; };
            Get["/serie/recent"] = _ => { return GetRecentSeries(10); };
            Get["/serie/recent/{max}"] = x => { return GetRecentSeries((int)x.max); };
            Post["/serie/search"] = x => { return SearchForSerie(); };
            Post["/serie/search/{limit}"] = x => { return SearchForSerie((int)x.limit); };
            Get["/serie/byfolder/{id}"] = x => { return GetSerieByFolderId(x.id, 10); };
            Get["/serie/byfolder/{id}/{max}"] = x => { return GetSerieByFolderId(x.id, x.max); };
            Post["/serie/watch/{type}/{max}"] = x => { return MarkSerieWatched(true, x.max, x.type); };
            Post["/serie/unwatch/{type}/{max}"] = x => { return MarkSerieWatched(false, x.max, x.type); };
            Post["/serie/vote"] = x => { return VoteOnSerie(); };
            Get["/serie/{id}/art"] = x => { return GetSerieArt((int)x.id); };

            // 15. WebUI
            Get["/dashboard"] = _ => { return GetDashboard(); };
            Get["/webui/update/stable"] = _ => { return WebUIStableUpdate(); };
            Get["/webui/latest/stable"] = _ => { return WebUILatestStableVersion(); };
            Get["/webui/update/unstable"] = _ => { return WebUIUnstableUpdate(); };
            Get["/webui/latest/unstable"] = _ => { return WebUILatestUnstableVersion(); };
            Get["/webui/config"] = _ => { return GetWebUIConfig(); };
            Post["/webui/config"] = _ => { return SetWebUIConfig(); };

            // 16. OS-based operations
            Get["/os/folder/base"] = _ => { return GetOSBaseFolder(); };
            Post["/os/folder"] = x => { return GetOSFolder(x.folder); };
            Get["/os/drives"] = _ => { return GetOSDrives(); };

            // 17. Cloud accounts
            Get["/cloud/list"] = _ => { return GetCloudAccounts(); };
            Get["/cloud/count"] = _ => { return GetCloudAccountsCount(); };
            Post["/cloud/add"] = x => { return AddCloudAccount(); };
            Post["/cloud/delete"] = x => { return DeleteCloudAccount(); };
            Get["/cloud/import"] = _ => { return RunCloudImport(); };

            // 18. Images
            Get["/cover/{id}"] = x => { return GetCover(x.id); };
            Get["/fanart/{id}"] = x => { return GetFanart(x.id); };
            Get["/poster/{id}"] = x => { return GetPoster(x.id); };

            Get["/banner/{id}"] = x => { return GetImage((int)x.id, 4, false); };
            Get["/fanart/{id}"] = x => { return GetImage((int)x.id, 7, false); };
            Get["/image/{type}/{id}"] = x => { return GetImage((int)x.id, (int)x.type, false); };

            // 19. Logs
            Get["/log/get"] = x => { return GetLog(10, 0); };
            Get["/log/get/{max}/{position}"] = x => { return GetLog((int)x.max, (int)x.position); };
            Post["/log/rotate"] = x => { return SetRotateLogs(); };
            Get["/log/rotate"] = x => { return GetRotateLogs(); };
            Get["/log/rotate/start"] = x => { return StartRotateLogs(); };
        }

        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();

        #region 1.Import Folders

        /// <summary>
        /// List all saved Import Folders
        /// </summary>
        /// <returns></returns>
        private object GetFolders()
        {
            List<Contract_ImportFolder> list = new JMMServiceImplementation().GetImportFolders();
            return list;
        }

        /// <summary>
        /// return number of Import Folders
        /// </summary>
        /// <returns></returns>
        private object CountFolders()
        {
            Counter count = new Counter();
            count.count = new JMMServiceImplementation().GetImportFolders().Count;
            return count;
        }

        /// <summary>
        /// Add Folder to Import Folders Repository
        /// </summary>
        /// <returns></returns>
        private object AddFolder()
        {
            try
            {
                Contract_ImportFolder folder = this.Bind();
                if (folder.ImportFolderLocation != "")
                {
                    try
                    {
                        Contract_ImportFolder_SaveResponse response = new JMMServiceImplementation().SaveImportFolder(folder);

                        if (string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            return APIStatus.statusOK();
                        }
                        else
                        {
                            return new APIMessage(500, response.ErrorMessage);
                        }
                    }
                    catch
                    {
                        return APIStatus.internalError();
                    }
                }
                else
                {
                    return new APIMessage(400, "Bad Request: The Folder path must not be Empty");
                }
            }
            catch (ModelBindingException)
            {
                return new APIMessage(400, "Bad binding");
            }
        }

        /// <summary>
        /// Edit folder giving fulll ImportFolder object with ID
        /// </summary>
        /// <returns></returns>
        private object EditFolder()
        {
            ImportFolder folder = this.Bind();
            if (!String.IsNullOrEmpty(folder.ImportFolderLocation) && folder.ImportFolderID != 0)
            {
                try
                {
                    if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                    {
                        return new APIMessage(409, "The Folder Can't be both Destination and Source Simultaneously");
                    }
                    else
                    {
                        if (folder.ImportFolderID != 0 & folder.ToContract().ImportFolderID.HasValue)
                        {
                            Contract_ImportFolder_SaveResponse response = new JMMServiceImplementation().SaveImportFolder(folder.ToContract());
                            if (!string.IsNullOrEmpty(response.ErrorMessage))
                            {
                                return new APIMessage(500, response.ErrorMessage);
                            }
                            else
                            {
                                return APIStatus.statusOK();
                            }
                        }
                        else
                        {
                            return new APIMessage(409, "The Import Folder must have an ID");
                        }
                    }
                }
                catch
                {
                    return APIStatus.internalError();
                }
            }
            else
            {
                return new APIMessage(400, "ImportFolderLocation and ImportFolderID missing");
            }
        }

        /// <summary>
        /// Delete Import Folder out of Import Folder Repository
        /// </summary>
        /// <returns></returns>
        private object DeleteFolder()
        {
            ImportFolder folder = this.Bind();
            if (folder.ImportFolderID != 0)
            {
                string res = Importer.DeleteImportFolder(folder.ImportFolderID);
                if (res == "")
                {
                    return APIStatus.statusOK();
                }
                else
                {
                    return new APIMessage(500, res);
                }
            }
            else
            {
                return new APIMessage(400, "ImportFolderID missing");
            }
        }

        /// <summary>
        /// Run Import action on all Import Folders inside Import Folders Repository
        /// </summary>
        /// <returns></returns>
        private object RunImport()
        {
            MainWindow.RunImport();
            return APIStatus.statusOK();
        }

        #endregion

        #region 2.UPNP

        private object ListUPNP()
        {
            UPNPLib.UPnPDeviceFinder discovery = new UPNPLib.UPnPDeviceFinder();
            UPnPFinderCallback call = new UPnPFinderCallback();
            discovery.StartAsyncFind(discovery.CreateAsyncFind("urn:schemas-upnp-org:device:MediaServer:1", 0, call));

            //TODO APIv2 ListUPNP: Need a tweak as this now should return it as list?
            return APIStatus.notImplemented();
        }

        private object AddUPNP()
        {
            //TODO APIv2 AddUPNP: implement this
            return APIStatus.notImplemented();
        }

        private object DeleteUPNP()
        {
            //TODO APIv2 DeleteUPN: implement this
            return APIStatus.notImplemented();
        }

        #endregion

        #region 3.Settings

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
                Contract_ServerSettings settings = ServerSettings.ToContract();
                return settings;
            }
            catch
            {
                return APIStatus.internalError("Error while parsing file");
            }
        }

        private object ImportConfig()
        {
            Contract_ServerSettings settings = this.Bind();
            string raw_settings = settings.ToJSON();
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

        #endregion

        #region 4.AniDB

        /// <summary>
        /// Set AniDB account with login, password and client port
        /// </summary>
        /// <returns></returns>
        private object SetAniDB()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != "" && !String.IsNullOrEmpty(cred.password) && cred.password != "")
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
            JMMService.AnidbProcessor.ForceLogout();
            JMMService.AnidbProcessor.CloseConnections();

            Thread.Sleep(1000);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            JMMService.AnidbProcessor.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password,
                ServerSettings.AniDB_ServerAddress,
                ServerSettings.AniDB_ServerPort, ServerSettings.AniDB_ClientPort);

            if (JMMService.AnidbProcessor.Login())
            {
                JMMService.AnidbProcessor.ForceLogout();
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
            MainWindow.SyncMyList();
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

        #region 5.MyAnimeList

        /// <summary>
        /// Set MAL account with login, password
        /// </summary>
        /// <returns></returns>
        private object SetMAL()
        {
            Creditentials cred = this.Bind();
            if (!String.IsNullOrEmpty(cred.login) && cred.login != "" && !String.IsNullOrEmpty(cred.password) && cred.password != "")
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

        #endregion

        #region 6.Trakt

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
        /// Update All information from Trakt
        /// </summary>
        /// <returns></returns>
        private object UpdateAllTrakt()
        {
            Providers.TraktTV.TraktTVHelper.UpdateAllInfo();
            return APIStatus.statusOK();
        }

        #endregion

        #region 7.TvDB

        /// <summary>
        /// Update all information from TvDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllTvDB()
        {
            Importer.RunImport_UpdateTvDB(false);
            return APIStatus.statusOK();
        }


        #endregion

        #region 8.Actions

        /// <summary>
        /// Scans your import folders and remove files from your database that are no longer in your collection.
        /// </summary>
        /// <returns></returns>
        private object RemoveMissingFiles()
        {
            MainWindow.RemoveMissingFiles();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Updates all series stats such as watched state and missing files.
        /// </summary>
        /// <returns></returns>
        private object UpdateStats()
        {
            Importer.UpdateAllStats();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Updates all technical details about the files in your collection via running MediaInfo on them.
        /// </summary>
        /// <returns></returns>
        private object UpdateMediaInfo()
        {
            MainWindow.RefreshAllMediaInfo();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Sync Hashes - download/upload hashes from/to webcache
        /// </summary>
        /// <returns></returns>
        private object HashSync()
        {
            MainWindow.SyncHashes();
            return APIStatus.statusOK();
        }

        #endregion

        #region 9.Misc

        /// <summary>
        /// return userid as it can be needed in legacy implementation
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private object MyID()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            dynamic x = new System.Dynamic.ExpandoObject();
            if (user != null)
            {
                x.userid = user.JMMUserID;
                return x;
            }
            else
            {
                x.userid = 0;
                return x;
            }
        }

        /// <summary>
        /// Return newest posts from 
        /// </summary>
        /// <returns></returns>
        private object GetNews(int max)
        {
            var client = new System.Net.WebClient();
            client.Headers.Add("User-Agent", "jmmserver");
            client.Headers.Add("Accept", "application/json");
            var response = client.DownloadString(new Uri("http://jmediamanager.org/wp-json/wp/v2/posts"));
            List<dynamic> news_feed = JsonConvert.DeserializeObject<List<dynamic>>(response);
            List<WebNews> news = new List<WebNews>();
            int limit = 0;
            foreach (dynamic post in news_feed)
            {
                limit++;
                WebNews wn = new WebNews();
                wn.author = post.author;
                wn.date = post.date;
                wn.link = post.link;
                wn.title = System.Web.HttpUtility.HtmlDecode((string)post.title.rendered);
                wn.description = post.excerpt.rendered;
                news.Add(wn);
                if (limit >= max) break;
            }
            return news;
        }

        #endregion

        #region 10.User

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
            Entities.JMMUser _user = (Entities.JMMUser)this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                Contract_JMMUser user = this.Bind();
                user.Password = Digest.Hash(user.Password);
                user.HideCategories = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                user.PlexUsers = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                if (new JMMServiceImplementation().SaveUser(user) == "")
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
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            return ChangePassword(user.JMMUserID);
        }

        /// <summary>
        /// change given user (by uid) password
        /// </summary>
        /// <returns></returns>
        private object ChangePassword(int uid)
        {
            Request request = this.Request;
            Entities.JMMUser _user = (Entities.JMMUser)this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                JMMUser user = this.Bind();
                if (new JMMServiceImplementation().ChangePassword(uid, user.Password) == "")
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
            JMMUser _user = (JMMUser)this.Context.CurrentUser;
            if (_user.IsAdmin == 1)
            {
                JMMUser user = this.Bind();
                if (new JMMServiceImplementation().DeleteUser(user.JMMUserID) == "")
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

        #region 11.Queue

        /// <summary>
        /// Return current information about Queues (hash, general, images)
        /// </summary>
        /// <returns></returns>
        private object GetQueue()
        {
            Dictionary<string, QueueInfo> queues = new Dictionary<string, QueueInfo>();
            queues.Add("hash", (QueueInfo)GetHasherQueue());
            queues.Add("general", (QueueInfo)GetGeneralQueue());
            queues.Add("image", (QueueInfo)GetImagesQueue());
            return queues;
        }

        /// <summary>
        /// Pause all running Queues
        /// </summary>
        /// <returns></returns>
        private object PauseQueue()
        {
            JMMService.CmdProcessorHasher.Paused = true;
            JMMService.CmdProcessorGeneral.Paused = true;
            JMMService.CmdProcessorImages.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start all queues that are pasued
        /// </summary>
        /// <returns></returns>
        private object StartQueue()
        {
            JMMService.CmdProcessorHasher.Paused = false;
            JMMService.CmdProcessorGeneral.Paused = false;
            JMMService.CmdProcessorImages.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Return information about Hasher queue
        /// </summary>
        /// <returns></returns>
        private object GetHasherQueue()
        {
            QueueInfo queue = new QueueInfo();
            queue.count = ServerInfo.Instance.HasherQueueCount;
            queue.state = ServerInfo.Instance.HasherQueueState;
            queue.isrunning = ServerInfo.Instance.HasherQueueRunning;
            queue.ispause = ServerInfo.Instance.HasherQueuePaused;
            return queue;
        }

        /// <summary>
        /// Return information about General queue
        /// </summary>
        /// <returns></returns>
        private object GetGeneralQueue()
        {
            QueueInfo queue = new QueueInfo();
            queue.count = ServerInfo.Instance.GeneralQueueCount;
            queue.state = ServerInfo.Instance.GeneralQueueState;
            queue.isrunning = ServerInfo.Instance.GeneralQueueRunning;
            queue.ispause = ServerInfo.Instance.GeneralQueuePaused;
            return queue;
        }

        /// <summary>
        /// Return information about Images queue
        /// </summary>
        /// <returns></returns>
        private object GetImagesQueue()
        {
            QueueInfo queue = new QueueInfo();
            queue.count = ServerInfo.Instance.ImagesQueueCount;
            queue.state = ServerInfo.Instance.ImagesQueueState;
            queue.isrunning = ServerInfo.Instance.ImagesQueueRunning;
            queue.ispause = ServerInfo.Instance.ImagesQueuePaused;
            return queue;
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns></returns>
        private object PauseHasherQueue()
        {
            JMMService.CmdProcessorHasher.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns></returns>
        private object PauseGeneralQueue()
        {
            JMMService.CmdProcessorGeneral.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns></returns>
        private object PauseImagesQueue()
        {
            JMMService.CmdProcessorImages.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartHasherQueue()
        {
            JMMService.CmdProcessorHasher.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartGeneralQueue()
        {
            JMMService.CmdProcessorGeneral.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartImagesQueue()
        {
            JMMService.CmdProcessorImages.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Clear Queue and Restart it
        /// </summary>
        /// <returns></returns>
        private object ClearHasherQueue()
        {
            try
            {
                JMMService.CmdProcessorHasher.Stop();

                while (JMMService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                JMMService.CmdProcessorHasher.Init();

                return APIStatus.statusOK();
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        /// <summary>
        /// Clear Queue and Restart it
        /// </summary>
        /// <returns></returns>
        private object ClearGeneralQueue()
        {
            try
            {
                JMMService.CmdProcessorGeneral.Stop();

                while (JMMService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());

                JMMService.CmdProcessorGeneral.Init();

                return APIStatus.statusOK();
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        /// <summary>
        /// Clear Queue and Restart it
        /// </summary>
        /// <returns></returns>
        private object ClearImagesQueue()
        {
            try
            {
                JMMService.CmdProcessorImages.Stop();

                while (JMMService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());

                JMMService.CmdProcessorImages.Init();

                return APIStatus.statusOK();
            }
            catch
            {
                return APIStatus.internalError();
            }
        }
        #endregion

        #region 12.Files

        /// <summary>
        /// Get file info by its ID
        /// </summary>
        /// <param name="file_id"></param>
        /// <returns></returns>
        private object GetFileById(int file_id)
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            VideoLocal file = _impl.GetFileByID(file_id);
            return file;
        }

        /// <summary>
        /// Get List of all files
        /// </summary>
        /// <returns></returns>
        private object GetAllFiles()
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            Dictionary<int, string> files = new Dictionary<int, string>();
            foreach (VideoLocal file in _impl.GetAllFiles())
            {
                files.Add(file.VideoLocalID, file.FileName);
            }

            return files;
        }

        /// <summary>
        /// return how many files collection have
        /// </summary>
        /// <returns></returns>
        private object CountFiles()
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            Counter count = new Counter();
            count.count = _impl.GetAllFiles().Count;
            return count;
        }

        /// <summary>
        /// Return List<> of recently added files paths
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentFiles(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            List<RecentFile> files = new List<RecentFile>();

            foreach (VideoLocal file in _impl.GetFilesRecentlyAdded(max_limit))
            {
                RecentFile recent = new RecentFile();
                recent.path = file.FileName;
                recent.id = file.VideoLocalID;
                if (file.EpisodeCrossRefs.Count() == 0)
                {
                    recent.success = false;
                }
                else
                {
                    recent.success = true;
                }
                files.Add(recent);
            }

            return files;
        }

        /// <summary>
        /// Return list of paths of files that have benn makred as Unrecognised
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetUnrecognisedFiles(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Dictionary<int, string> files = new Dictionary<int, string>();
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            int i = 0;
            foreach (Contract_VideoLocal file in _impl.GetUnrecognisedFiles(user.JMMUserID))
            {
                i++;
                files.Add(file.VideoLocalID, file.FileName);
                if (i >= max_limit) break;
            }
            return files;
        }

        #endregion

        #region 13.Episodes

        /// <summary>
        /// return all known anime series
        /// </summary>
        /// <returns></returns>
        private object GetAllEpisodes()
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetAllEpisodes();
        }

        /// <summary>
        /// Return information about episode by given ID for current user
        /// </summary>
        /// <param name="ep_id"></param>
        /// <returns></returns>
        private object GetEpisodeById(int ep_id)
        {
            if (ep_id != 0)
            {
                return GetEpisode(ep_id);
            }
            else
            {
                return APIStatus.badRequest();
            }
        }

        internal Contract_AnimeEpisode GetEpisode(int ep_id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetEpisode(ep_id, user.JMMUserID);
        }

        private object GetEpisodeImage(int ep_id)
        {
            if (ep_id != 0)
            {
                //_rest.GetImage("6/12", )
                //GetEpisode(ep_id).
                return APIStatus.notImplemented();
            }
            else
            {
                return APIStatus.badRequest();
            }
        }

        /// <summary>
        /// Get recent Episodes
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentEpisodes(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetEpisodesRecentlyAdded(max_limit, user.JMMUserID);
        }

        /// <summary>
        /// Set watch status (true) or unwatch (false) for episode that 'id' was given in post body
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        private object MarkEpisodeWatched(bool status)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            //we need just 'id'
            Rating epi = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.ToggleWatchedStatusOnEpisode(epi.id, status, user.JMMUserID);
        }

        /// <summary>
        /// Set score for episode
        /// </summary>
        /// <returns></returns>
        private object VoteOnEpisode()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Rating epi = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            _impl.VoteAnime(epi.id, (decimal)epi.score, (int)AniDBAPI.enAniDBVoteType.Episode);

            return APIStatus.statusOK();
        }

        private object EpisodeScrobble()
        {
            return APIStatus.notImplemented();
        }

        #endregion

        #region 14.Series

        /// <summary>
        /// Return number of series inside collection
        /// </summary>
        /// <returns></returns>
        private object CountSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            Counter count = new Counter();
            count.count = _impl.GetAllSeries(user.JMMUserID).Count;
            return count;
        }

        /// <summary>
        /// return all series for current user
        /// </summary>
        /// <returns></returns>
        private object GetAllSeries()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetAllSeries(user.JMMUserID);
        }

        /// <summary>
        /// return information about serie with given ID
        /// </summary>
        /// <param name="series_id"></param>
        /// <returns></returns>
        private object GetSerieById(int series_id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetSeries(series_id, user.JMMUserID);
        }

        /// <summary>
        /// Return list of series inside given folder
        /// </summary>
        /// <param name="folder_id"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private object GetSerieByFolderId(int folder_id, int max)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetSeriesFileStatsByFolderID(folder_id, user.JMMUserID, max);
        }

        /// <summary>
        /// return Recent added series
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentSeries(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetSeriesRecentlyAdded(max_limit, user.JMMUserID);
        }

        /// <summary>
        /// Mark given number files of given type for series as un/watched
        /// </summary>
        /// <param name="status">true = watched, false = unwatched</param>
        /// <param name="max_episodes">max number or episode to mark</param>
        /// <param name="type">1 = episodes, 2 = credits, 3 = special, 4 = trailer, 5 = parody, 6 = other</param>
        /// <returns></returns>
        private object MarkSerieWatched(bool status, int max_episodes, int type)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            //we need just 'id'
            Rating ser = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.SetWatchedStatusOnSeries(ser.id, status, max_episodes, type, user.JMMUserID);
        }

        /// <summary>
        /// Set score for serie
        /// </summary>
        /// <returns></returns>
        private object VoteOnSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Rating ser = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            _impl.VoteAnime(ser.id, (decimal)ser.score, (int)AniDBAPI.enAniDBVoteType.Anime);

            return APIStatus.statusOK();
        }

        private object GetSerieArt(int serie_id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(serie_id);
            if (ser == null) { return APIStatus.notFound404(); }
            Contract_AnimeSeries cseries = ser.GetUserContract(user.JMMUserID);
            if (cseries == null) { return APIStatus.accessDenied(); }
            if (cseries.AniDBAnime != null && cseries.AniDBAnime.AniDBAnime != null)
            {
                //cseries.AniDBAnime.AniDBAnime.Banners
                // TODO Apiv2 - This is all around aproche We dont need > Series then contract just to access this lets ask directly image with animeid = id but how?!
                return cseries.AniDBAnime.AniDBAnime.Fanarts;
            }
            else
            {
                return APIStatus.internalError();
            }

        }

        /// <summary>
        /// Search for serie that contain given query
        /// </summary>
        /// <returns>first 100 results</returns>
        private object SearchForSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, 100, false, user.JMMUserID);
        }

        /// <summary>
        /// Search for serie that contain given query, limit your results with variable
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private object SearchForSerie(int limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, limit, false, user.JMMUserID);
        }


        /// <summary>
        /// search for tag with given query inside
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        private object SearchForTag()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, 100, true, user.JMMUserID);
        }

        internal object Search(string query, int limit, bool tag_search, int userid)
        {
            CommonImplementation _comm = new CommonImplementation();
            IProvider _prov_kodi = new PlexAndKodi.Kodi.KodiProvider();

            return _comm.Search(_prov_kodi, userid.ToString(), limit.ToString(), query, tag_search);
        }

        #endregion

        #region 15.WebUI

        /// <summary>
        /// Return Dictionary with nesesery items for Dashboard inside Webui
        /// </summary>
        /// <returns></returns>
        private object GetDashboard()
        {
            Dictionary<string, object> dash = new Dictionary<string, object>();
            dash.Add("queue", GetQueue());
            dash.Add("file", GetRecentFiles(10));
            dash.Add("folder", GetFolders());
            dash.Add("file_count", CountFiles());
            dash.Add("serie_count", CountSerie());
            return dash;
        }

        /// <summary>
        /// Download the latest stable version of WebUI
        /// </summary>
        /// <returns></returns>
        private object WebUIStableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestStableVersion().version);
        }

        /// <summary>
        /// Download the latest unstable version of WebUI
        /// </summary>
        /// <returns></returns>
        private object WebUIUnstableUpdate()
        {
            return WebUIGetUrlAndUpdate(WebUILatestUnstableVersion().version);
        }

        /// <summary>
        /// Get url for update and start update
        /// </summary>
        /// <param name="tag_name"></param>
        /// <returns></returns>
        internal object WebUIGetUrlAndUpdate(string tag_name)
        {
            try
            {
                var client = new System.Net.WebClient();
                client.Headers.Add("Accept: application/vnd.github.v3+json");
                client.Headers.Add("User-Agent", "jmmserver");
                var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/jmmserver-webui/releases/tags/" + tag_name));

                dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                string url = "";
                foreach (dynamic obj in result.assets)
                {
                    if (obj.name == "latest.zip")
                    {
                        url = obj.browser_download_url;
                        break;
                    }
                }

                //check if tag was parsed corrently as it make the url
                if (url != "")
                {
                    return WebUIUpdate(url);
                }
                else
                {
                    return new APIMessage(204, "Content is missing");
                }
            }
            catch
            {
                return APIStatus.internalError();
            }
        }

        /// <summary>
        /// Update WebUI with version from given url
        /// </summary>
        /// <param name="url">direct link to version you want to install</param>
        /// <returns></returns>
        internal object WebUIUpdate(string url)
        {
            //list all files from root /webui/ and all directories
            string[] files = Directory.GetFiles("webui");
            string[] directories = Directory.GetDirectories("webui");

            try
            {
                //download latest version
                var client = new System.Net.WebClient();
                client.Headers.Add("User-Agent", "jmmserver");
                client.DownloadFile(url, "webui\\latest.zip");

                //create 'old' dictionary
                if (!Directory.Exists("webui\\old")) { System.IO.Directory.CreateDirectory("webui\\old"); }
                try
                {
                    //move all directories and files to 'old' folder as fallback recovery
                    foreach (string dir in directories)
                    {
                        if (Directory.Exists(dir) && dir != "webui\\old")
                        {
                            string n_dir = dir.Replace("webui", "webui\\old");
                            Directory.Move(dir, n_dir);
                        }
                    }
                    foreach (string file in files)
                    {
                        if (File.Exists(file))
                        {
                            string n_file = file.Replace("webui", "webui\\old");
                            File.Move(file, n_file);
                        }
                    }

                    try
                    {
                        //extract latest webui
                        System.IO.Compression.ZipFile.ExtractToDirectory("webui\\latest.zip", "webui");

                        //clean because we already have working updated webui
                        Directory.Delete("webui\\old", true);
                        File.Delete("webui\\latest.zip");

                        return APIStatus.statusOK();
                    }
                    catch
                    {
                        //when extracting latest.zip failes
                        return new APIMessage(405, "MethodNotAllowed");
                    }
                }
                catch
                {
                    //when moving files to 'old' folder failed
                    return new APIMessage(423, "Locked");
                }
            }
            catch
            {
                //when download failed
                return new APIMessage(499, "download failed");
            }
        }

        /// <summary>
        /// Check for newest stable version and return object { version: string, url: string }
        /// </summary>
        /// <returns></returns>
        private ComponentVersion WebUILatestStableVersion()
        {
            ComponentVersion version = new ComponentVersion();
            version = WebUIGetLatestVersion(true);

            return version;
        }

        /// <summary>
        /// Check for newest unstable version and return object { version: string, url: string }
        /// </summary>
        /// <returns></returns>
        private ComponentVersion WebUILatestUnstableVersion()
        {
            ComponentVersion version = new ComponentVersion();
            version = WebUIGetLatestVersion(false);

            return version;
        }

        /// <summary>
        /// Find version that match requirements
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        internal ComponentVersion WebUIGetLatestVersion(bool stable)
        {
            var client = new System.Net.WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "jmmserver");
            var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/jmmserver-webui/releases/latest"));

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            ComponentVersion version = new ComponentVersion();

            if (result.prerelease == "False")
            {
                //not pre-build
                if (stable)
                {
                    version.version = result.tag_name;
                }
                else
                {
                    version.version = WebUIGetVersionsTag(false);
                }
            }
            else
            {
                //pre-build
                if (stable)
                {
                    version.version = WebUIGetVersionsTag(true);
                }
                else
                {
                    version.version = result.tag_name;
                }
            }

            return version;
        }

        /// <summary>
        /// Return tag_name of version that match requirements and is not present in /latest/
        /// </summary>
        /// <param name="stable">do version have to be stable</param>
        /// <returns></returns>
        internal string WebUIGetVersionsTag(bool stable)
        {
            var client = new System.Net.WebClient();
            client.Headers.Add("Accept: application/vnd.github.v3+json");
            client.Headers.Add("User-Agent", "jmmserver");
            var response = client.DownloadString(new Uri("https://api.github.com/repos/japanesemediamanager/jmmserver-webui/releases"));

            dynamic result = Newtonsoft.Json.JsonConvert.DeserializeObject(response);

            foreach (dynamic obj in result)
            {
                if (stable)
                {
                    if (obj.prerelease == "False")
                    {
                        foreach (dynamic file in obj.assets)
                        {
                            if ((string)file.name == "latest.zip")
                            {
                                return obj.tag_name;
                            }
                        }
                    }
                }
                else
                {
                    if (obj.prerelease == "True")
                    {
                        foreach (dynamic file in obj.assets)
                        {
                            if ((string)file.name == "latest.zip")
                            {
                                return obj.tag_name;
                            }
                        }
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// Read json file that is converted into string from .config file of jmmserver
        /// </summary>
        /// <returns></returns>
        private object GetWebUIConfig()
        {
            if (!String.IsNullOrEmpty(ServerSettings.WebUI_Settings))
            {
                try
                {
                    WebUI_Settings settings = JsonConvert.DeserializeObject<WebUI_Settings>(ServerSettings.WebUI_Settings);
                    return settings;
                }
                catch
                {
                    return APIStatus.internalError("error while reading webui settings");
                }
                
            }
            else
            {
                return APIStatus.notFound404();
            }
        }

        /// <summary>
        /// Save webui settings as json converted into string inside .config file of jmmserver
        /// </summary>
        /// <returns></returns>
        private object SetWebUIConfig()
        {
            WebUI_Settings settings = this.Bind();
            if (settings.Valid())
            {
                try
                {
                    ServerSettings.WebUI_Settings = JsonConvert.SerializeObject(settings);
                    return APIStatus.statusOK();
                }
                catch
                {
                    return APIStatus.internalError("error at saving webui settings");
                }
            }
            else
            {
                return new APIMessage(400, "Config is not a Valid.");
            }
        }

        #endregion

        #region 16.OS-based operations

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

        #region 17.Cloud Accounts

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
            MainWindow.RunImport();
            return APIStatus.statusOK();
        }

        #endregion

        #region 18. Images

        /// <summary>
        /// Return image with given Id type and information if its should be thumb
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <param name="thumb"></param>
        /// <returns></returns>
        private object GetImage(int id, int type, bool thumb)
        {
            string contentType;
            System.IO.Stream image = _rest.GetImage(type.ToString(), id.ToString(), thumb, out contentType);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }

        private object GetFanart(int serie_id)
        {
            //Request request = this.Request;
            //Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            //JMMServiceImplementation _impl = new JMMServiceImplementation();
            //Contract_AnimeSeries ser = _impl.GetSeries(serie_id, user.JMMUserID);

            //Currently hack this, as the end result should find image for series id not image id.
            //TODO APIv2 This should return default image for series_id not image_id

            string contentType;
            System.IO.Stream image = _rest.GetImage("7".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("11".ToString(), serie_id.ToString(), false, out contentType);
            }
            if (image == null)
            {
                image = _rest.GetImage("8".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }
        private object GetCover(int serie_id)
        {
            //TODO APIv2 This should return default image for series_id not image_id
            string contentType;
            System.IO.Stream image = _rest.GetImage("1".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("5".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }
        private object GetPoster(int serie_id)
        {
            //TODO APIv2 This should return default image for series_id not image_id
            string contentType;
            System.IO.Stream image = _rest.GetImage("10".ToString(), serie_id.ToString(), false, out contentType);
            if (image == null)
            {
                image = _rest.GetImage("9".ToString(), serie_id.ToString(), false, out contentType);
            }
            else
            {
                image = _rest.BlankImage();
            }
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }


        #endregion

        #region 19. Logs

        /// <summary>
        /// Run LogRotator with current settings
        /// </summary>
        /// <returns></returns>
        private object StartRotateLogs()
        {
            MainWindow.logrotator.Start();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Set settings for LogRotator
        /// </summary>
        /// <returns></returns>
        private object SetRotateLogs()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
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
            rotator.rotate= ServerSettings.RotateLogs;
            rotator.zip = ServerSettings.RotateLogs_Zip;
            rotator.delete=ServerSettings.RotateLogs_Delete;
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
            for (int i=0; i<lines; i++)
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
