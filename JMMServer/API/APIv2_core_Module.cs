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

namespace JMMServer.API
{
    //As responds for this API we throw object that will be converted to json/xml or standard http codes (HttpStatusCode)
    public class APIv2_core_Module : Nancy.NancyModule
    {
        public static int version = 1;
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
        //routes are named with twitter api style
        //every function with summary is implemented 
        public APIv2_core_Module() : base("/api")
        {
            this.RequiresAuthentication();

            // 1. folders
            Get["/folder/list"] = x => { return GetFolders(); };
            Get["/folder/count"] = x => { return CountFolders(); };
            Post["/folder/add"] = x => { return AddFolder(); };
            Post["/folder/delete"] = x => { return DeleteFolder(); };
            Get["/folder/import"] = _ => { return RunImport(); };

            // 2. upnp 
            Post["/upnp/list"] = x => { return ListUPNP(); };
            Post["/upnp/add"] = x => { return AddUPNP(); };
            Post["/upnp/delete"] = x => { return DeleteUPNP(); };

            // 3. Settings
            Post["/port/set"] = _ => { return SetPort(); };
            Get["/port/get"] = _ => { return GetPort(); };
            Post["/imagepath/set"] = _ => { return SetImagepath(); };
            Get["/imagepath/get"] = _ => { return GetImagepath(); };

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
            Get["/MyID"] = x => { return MyID(x.apikey); };
            Get["/dashboard"] = _ => { return GetDashboard(); };

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
            Get["/ep/recent"] = x => { return GetRecentEpisodes(10); };
            Get["/ep/recent/{max}"] = x => { return GetRecentEpisodes((int)x.max); };

            // 14. Series
            Get["/serie/list"] = _ => { return GetAllSeries(); ; };
            Get["/serie/count"] = _ => { return CountSerie(); ; };
            Get["/serie/{id}"] = x => { return GetSerieById(x.id); ; };
            Get["/serie/recent"] = _ => { return GetRecentSeries(10); };
            Get["/serie/recent/{max}"] = x => { return GetRecentSeries((int)x.max); };

            //dashboard
            Get["/dashboard"] = _ => { return GetDashboard(); };

        }

        #region 1.Folders

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
            ImportFolder folder = this.Bind();
            if (folder.ImportFolderLocation != "")
            {
                try
                {
                    if (folder.IsDropDestination == 1 && folder.IsDropSource == 1)
                    {
                        return HttpStatusCode.Conflict;
                    }
                    else
                    {
                        Contract_ImportFolder_SaveResponse response = new JMMServiceImplementation().SaveImportFolder(folder.ToContract());

                        if (!string.IsNullOrEmpty(response.ErrorMessage))
                        {
                            return HttpStatusCode.InternalServerError;
                        }

                        return HttpStatusCode.OK;
                    }
                }
                catch
                {
                    return HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                return HttpStatusCode.BadRequest;
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
                if (Importer.DeleteImportFolder(folder.ImportFolderID) == "")
                {
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Run Import action on all Import Folders inside Import Folders Repository
        /// </summary>
        /// <returns></returns>
        private object RunImport()
        {
            MainWindow.RunImport();
            return HttpStatusCode.OK;
        }

        #endregion

        #region 2.UPNP

        private object ListUPNP()
        {
            UPNPLib.UPnPDeviceFinder discovery = new UPNPLib.UPnPDeviceFinder();
            UPnPFinderCallback call = new UPnPFinderCallback();
            discovery.StartAsyncFind(discovery.CreateAsyncFind("urn:schemas-upnp-org:device:MediaServer:1", 0, call));

            //TODO APIv2 ListUPNP: Need a tweak as this now should return it as list?

            return call;
        }

        private object AddUPNP()
        {
            //TODO APIv2 AddUPNP: implement this
            throw new NotImplementedException();
        }

        private object DeleteUPNP()
        {
            //TODO APIv2 DeleteUPN: implement this
            throw new NotImplementedException();
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Get JMMServer Port
        /// </summary>
        /// <returns></returns>
        private object GetPort()
        {
            return "{ \"port\":" + ServerSettings.JMMServerPort.ToString() + "}";
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
                ServerSettings.BaseImagesPathIsDefault = imagepath.isdefault;
                return HttpStatusCode.OK;
            }
            else
            {
                if (!String.IsNullOrEmpty(imagepath.path) && imagepath.path != "")
                {
                    ServerSettings.BaseImagesPath = imagepath.path;
                    return HttpStatusCode.OK;
                }
                else
                {
                    return HttpStatusCode.BadRequest;
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
            imagepath.path = ServerSettings.BaseImagesPath;
            imagepath.isdefault = ServerSettings.BaseImagesPathIsDefault;

            return imagepath;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.Unauthorized;
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
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Sync AniDB List
        /// </summary>
        /// <returns></returns>
        private object SyncAniDBList()
        {
            MainWindow.SyncMyList();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Update all series infromation from AniDB
        /// </summary>
        /// <returns></returns>
        private object UpdateAllAniDB()
        {
            Importer.RunImport_UpdateAllAniDB();
            return HttpStatusCode.OK;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.Unauthorized;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.BadRequest;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.Unauthorized;
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
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.NoContent;
            }
        }

        /// <summary>
        /// Update All information from Trakt
        /// </summary>
        /// <returns></returns>
        private object UpdateAllTrakt()
        {
            Providers.TraktTV.TraktTVHelper.UpdateAllInfo();
            return HttpStatusCode.OK;
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
            return HttpStatusCode.OK;
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
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Updates all series stats such as watched state and missing files.
        /// </summary>
        /// <returns></returns>
        private object UpdateStats()
        {
            Importer.UpdateAllStats();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Updates all technical details about the files in your collection via running MediaInfo on them.
        /// </summary>
        /// <returns></returns>
        private object UpdateMediaInfo()
        {
            MainWindow.RefreshAllMediaInfo();
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Sync Hashes - download/upload hashes from/to webcache
        /// </summary>
        /// <returns></returns>
        private object HashSync()
        {
            MainWindow.SyncHashes();
            return HttpStatusCode.OK;
        }

        #endregion

        #region 9.Misc

        /// <summary>
        /// return userid as it can be needed in legacy implementation
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private object MyID(string s)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                return " { \"userid\":\"" + user.JMMUserID.ToString() + "\" }";
            }
            else
            {
                return null;
            }
        }


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
            Contract_JMMUser user = this.Bind();
            user.Password = Digest.Hash(user.Password);

            if (new JMMServiceImplementation().SaveUser(user) == "")
            {
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.InternalServerError;
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
            JMMUser _user = this.Bind();
            if (new JMMServiceImplementation().ChangePassword(uid, _user.Password) == "")
            {
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.InternalServerError;
            }
        }

        /// <summary>
        /// Delete user from his ID
        /// </summary>
        /// <returns></returns>
        private object DeleteUser()
        {
            JMMUser user = this.Bind();
            if (new JMMServiceImplementation().DeleteUser(user.JMMUserID) == "")
            {
                return HttpStatusCode.OK;
            }
            else
            {
                return HttpStatusCode.InternalServerError;
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
            return HttpStatusCode.OK;
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
            return HttpStatusCode.OK;
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
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns></returns>
        private object PauseGeneralQueue()
        {
            JMMService.CmdProcessorGeneral.Paused = true;
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns></returns>
        private object PauseImagesQueue()
        {
            JMMService.CmdProcessorImages.Paused = true;
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartHasherQueue()
        {
            JMMService.CmdProcessorHasher.Paused = false;
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartGeneralQueue()
        {
            JMMService.CmdProcessorGeneral.Paused = false;
            return HttpStatusCode.OK;
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns></returns>
        private object StartImagesQueue()
        {
            JMMService.CmdProcessorImages.Paused = false;
            return HttpStatusCode.OK;
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

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestHasher())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorHasher.Init();

                return HttpStatusCode.OK;
            }
            catch
            {
                return HttpStatusCode.InternalServerError;
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

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestGeneral())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorGeneral.Init();

                return HttpStatusCode.OK;
            }
            catch
            {
                return HttpStatusCode.InternalServerError;
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

                CommandRequestRepository repCR = new CommandRequestRepository();
                foreach (CommandRequest cr in repCR.GetAllCommandRequestImages())
                    repCR.Delete(cr.CommandRequestID);

                JMMService.CmdProcessorImages.Init();

                return HttpStatusCode.OK;
            }
            catch
            {
                return HttpStatusCode.InternalServerError;
            }
        }
        #endregion

        #region 12.Files

        private object GetFileById(int file_id)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get List of all files
        /// </summary>
        /// <returns></returns>
        private object GetAllFiles()
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            List<string> files = new List<string>();
            foreach (VideoLocal file in _impl.GetAllFiles())
            {
                files.Add(file.FilePath);
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

            List<string> files = new List<string>();

            foreach (VideoLocal file in _impl.GetFilesRecentlyAdded(max_limit))
            {
                files.Add(file.FilePath);
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
            List<string> files = new List<string>();
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            int i = 0;
            foreach (Contract_VideoLocal file in _impl.GetUnrecognisedFiles(user.JMMUserID))
            {
                i++;
                files.Add(file.FilePath);
                if (i >= 10) break;
            }
            return files;
        }

        #endregion

        #region 13.Episodes

        private object GetAllEpisodes()
        {
            throw new NotImplementedException();
            return null;
        }

        private object GetEpisodeById(int ep_id)
        {
            throw new NotImplementedException();
            return null;
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

        #endregion

    }
}
