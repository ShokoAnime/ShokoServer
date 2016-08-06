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

            // Operations on collection
            Get["/folder/list"] = x => { return ListFolders(); };
            Post["/folder/add"] = x => { return AddFolder(); };
            Post["/folder/delete"] = x => { return DeleteFolder(); };
            Post["/upnp/list"] = x => { return ListUPNP(); };
            Post["/upnp/add"] = x => { return AddUPNP(); };
            Post["/upnp/delete"] = x => { return DeleteUPNP(); };
            Get["/import"] = _ => { return RunImport(); };

            // Settings
            Post["/port/set"] = _ => { return SetPort(); };
            Get["/port/get"] = _ => { return GetPort(); };
            Post["/imagepath/set"] = _ => { return SetImagepath(); };
            Get["/imagepath/get"] = _ => { return GetImagepath(); };
            Post["/anidb/set"] = _ => { return SetAniDB(); };
            Get["/anidb/get"] = _ => { return GetAniDB(); };
            Get["/anidb/test"] = _ => { return TestAniDB(); };
            Post["/mal/set"] = _ => { return SetMAL(); };
            Get["/mal/get"] = _ => { return GetMAL(); };
            Get["/mal/test"] = _ => { return TestMAL(); };
            Post["/trakt/set"] = _ => { return SetTraktPIN(); };
            Get["/trakt/get"] = _ => { return GetTrakt(); };
            Get["/trakt/create"] = _ => { return CreateTrakt(); };

            // Actions
            Get["/remove_missing_files"] = _ => { return RemoveMissingFiles(); };
            Get["/stats_update"] = _ => { return UpdateStats(); };
            Get["/mediainfo_update"] = _ => { return UpdateMediaInfo(); };
            Get["/hash/sync"] = _ => { return HashSync(); };
            Get["/trakt/sync"] = _ => { return SyncTrakt(); };
            Get["/trakt/update"] = _ => { return UpdateAllTrakt(); };
            Get["/anidb/votes/sync"] = _ => { return SyncAniDBVotes(); };
            Get["/anidb/list/sync"] = _ => { return SyncAniDBList(); };
            Get["/anidb/update"] = _ => { return UpdateAllAniDB(); };
            //Get["/mal/votes/sync"] = _ => { return SyncMALVotes(); }; <-- not implemented as CommandRequest
            Get["/tvdb/update"] = _ => { return UpdateAllTvDB(); };

            // Misc
            Get["/MyID"] = x => { return MyID(x.apikey); };
            Get["/user/list"] = _ => { return GetUsers(); };
            Post["/user/create"] = _ => { return CreateUser(); };
            Post["/user/delete"] = _ => { return DeleteUser(); };
            Post["/user/password"] = _ => { return ChangePassword(); };

            // Queue
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

            //Files
            Get["/file/list"] = _ => { return "get all files"; };
            Get["/file/{id}"] = x => { return x.id; };
            Get["/file/create"] = x => { return "ok"; };
            Get["/file/delete"] = x => { return "ok"; };
            Get["/file/recent"] = x => { return GetRecentFiles(10); };
            Get["/file/recent/{max}"] = x => { return GetRecentFiles((int)x.max); };
            Get["/file/unrecognised"] = x => { return GetUnrecognisedFiles(); };

            //Episodes
            Get["/ep/list"] = _ => { return "get all episodes"; };
            Get["/ep/{id}"] = x => { return x.id; };
            Get["/ep/recent"] = x => { return GetRecentEpisodes(10); };
            Get["/ep/recent/{max}"] = x => { return GetRecentEpisodes((int)x.max); };

        }

        #region Operations on collection

        /// <summary>
        /// List all saved Import Folders
        /// </summary>
        /// <returns></returns>
        private object ListFolders()
        {
            List<Contract_ImportFolder> list = new JMMServiceImplementation().GetImportFolders();
            return list;
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

        #region Settings

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

        #endregion

        #region Actions

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

        //This is not implemented  yet/or its deep inside code
        //private object SyncMALVotes()
        //{
        //    CommandRequest_SyncMyVotes cmdVotes = new CommandRequest_SyncMyVotes();
        //    cmdVotes.Save();
        //    return HttpStatusCode.OK;
        //}

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

        #region Misc

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
        /// return List of PlexContract_Users
        /// </summary>
        /// <returns></returns>
        private object GetUsers()
        {
            return new CommonImplementation().GetUsers(null);
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
            if (user != null)
            {
                JMMUser _user = this.Bind();
                if (new JMMServiceImplementation().ChangePassword(user.JMMUserID, _user.Password) == "")
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
                return HttpStatusCode.Unauthorized;
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

        #region Queue

        /// <summary>
        /// Return current information about Queues (hash, general, images)
        /// </summary>
        /// <returns></returns>
        private object GetQueue()
        {
            List<QueueInfo> queues = new List<QueueInfo>();
            queues.Add((QueueInfo)GetHasherQueue());
            queues.Add((QueueInfo)GetGeneralQueue());
            queues.Add((QueueInfo)GetImagesQueue());
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

        #region Files

        private object GetRecentFiles(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            //List<Contract_AnimeEpisode> eps = _impl.GetEpisodesRecentlyAdded(max_limit, user.JMMUserID);

            //List<List<Contract_VideoDetailed>> list = new List<List<Contract_VideoDetailed>>();

            //foreach (Contract_AnimeEpisode ep in eps)
            //{
            //    list.Add(_impl.GetFilesForEpisode(ep.AnimeEpisodeID, user.JMMUserID));
            //}

            return _impl.GetFilesRecentlyAdded(max_limit, user.JMMUserID);
        }

        private object GetUnrecognisedFiles()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            return _impl.GetUnrecognisedFiles(user.JMMUserID);
        }

        #endregion

        #region Episodes

        private object GetRecentEpisodes(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();
            //return _impl.GetEpisodesRecentlyAddedSummary(max_limit, user.JMMUserID);
            return _impl.GetEpisodesRecentlyAdded(max_limit, user.JMMUserID);
        }

        #endregion

    }
}
