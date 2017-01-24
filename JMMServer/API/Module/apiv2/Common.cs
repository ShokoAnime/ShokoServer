using Nancy;
using Nancy.Security;
using System;
using Nancy.ModelBinding;
using JMMServer.Entities;
using JMMContracts;
using System.Collections.Generic;
using System.Threading;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories;
using System.Linq;
using Newtonsoft.Json;
using JMMServer.API.Model.core;
using JMMServer.API.Module.apiv1;
using JMMServer.API.Model.common;
using JMMContracts.PlexAndKodi;
using AniDBAPI;
using System.IO;

namespace JMMServer.API.Module.apiv2
{
    //As responds for this API we throw object that will be converted to json/xml
    public class Common : Nancy.NancyModule
    {
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)

        public static int version = 2;
        
        public Common() : base("/api")
        {
            this.RequiresAuthentication(); // its a setting per module, so every call here need apikey

            #region 1. import folders
            Get["/folder/list"] = x => { return GetFolders(); };
            Get["/folder/count"] = x => { return CountFolders(); };
            Post["/folder/add"] = x => { return AddFolder(); };
            Post["/folder/edit"] = x => { return EditFolder(); };
            Post["/folder/delete"] = x => { return DeleteFolder(); };
            Get["/folder/import"] = _ => { return RunImport(); };
            #endregion

            #region 2. upnp 
            Post["/upnp/list"] = x => { return ListUPNP(); };
            Post["/upnp/add"] = x => { return AddUPNP(); };
            Post["/upnp/delete"] = x => { return DeleteUPNP(); };
            #endregion

            #region 3. Actions
            Get["/remove_missing_files"] = _ => { return RemoveMissingFiles(); };
            Get["/stats_update"] = _ => { return UpdateStats(); };
            Get["/mediainfo_update"] = _ => { return UpdateMediaInfo(); };
            Get["/hash/sync"] = _ => { return HashSync(); };
            Get["/rescan"] = x => { return RescanVideoLocal(); };
            Get["/rehash"] = x => { return RehashVideoLocal(); };
            #endregion

            #region 4. Misc
            Get["/myid/get"] = _ => { return MyID(); };
            Get["/news/get"] = _ => { return GetNews(5); };
            Get["/dashboard"] = _ => { return GetDashboard(); };
            #endregion

            #region 5. Queue
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
            #endregion

            #region 6. Files
            Get["/file"] = _ => { return GetFile(); };
            Get["/file/count"] = _ => { return CountFiles(); };
            Get["/file/recent"] = x => { return GetRecentFiles(); };
            Get["/file/unsort"] = _ => { return GetUnsort(); };
            #endregion

            #region 6. Files - [Obsolete]
            Get["/file/recentold"] = x => { return GetRecentFiles_old(10); }; // [Obsolete] use /file/recent
            Get["/file/recentold/{max}"] = x => { return GetRecentFiles_old((int)x.max); }; //[Obsolete] use /file/recent?limit=
            Get["/file/unrecognised"] = x => { return GetUnrecognisedFiles(10); }; // [Obsolete] use /file/unsort
            Get["/file/unrecognised/{max}"] = x => { return GetUnrecognisedFiles((int)x.max); }; //[Obsolete] use /file/unsort?limit=
            Get["/file/unsort/{max}"] = x => { return GetUnsort((int)x.max); }; // [Obsolete] use /file/unsort?limit=
            Get["/file/list"] = _ => { return GetAllFiles_old(); }; // [Obsolete] use /file
            Get["/file/{id}"] = x => { return GetFileById(x.id, 0, 1); }; // [Obsolete] use /file?id=
            #endregion

            #region 7. Episodes
            Get["/ep"] = x => { return GetEpisode(); };
            Get["/ep/recent"] = x => { return GetRecentEpisodes(); };
            Get["/ep/watch"] = x => { return MarkEpisodeAsWatched(); };
            Get["/ep/unwatch"] = x => { return MarkEpisodeAsUnwatched(); };
            Get["/ep/vote"] = x => { return VoteOnEpisode(); };
            Get["/ep/unsort"] = _ => { return GetUnsort(); };
            Post["/ep/scrobble"] = x => { return EpisodeScrobble(); };
            Get["/ep/getbyfilename"] = x => { return GetEpisodeFromName(); };
            #endregion

            #region 7. Episodes - [Obsolete]
            Get["/ep/list"] = _ => { return GetAllEpisodes(); ; }; // [Obsolete] use /ep
            Get["/ep/{id}"] = x => { return GetEpisodeById(x.id); }; // [Obsolete] use /ep?id=
            Get["/ep/recent/{max}"] = x => { return GetRecentEpisodes((int)x.max); }; // [Obsolete] use /ep/recent?limit=
            Get["/ep/vote"] = x => { return VoteOnEpisode(); }; // [Obsolete] use /ep/vote?id=&score={1-10}
            Get["/ep/unsort/{max}"] = x => { return GetUnsort((int)x.max); }; // [Obsolete] use /ep/unsort?limit=
            #endregion

            #region 8. Series
            Get["/serie"] = _ => { return GetSerie(); };           
            Get["/serie/count"] = _ => { return CountSerie(); };           
            Get["/serie/recent"] = _ => { return GetSeriesRecent(); };           
            Get["/serie/search"] = x => { return SearchForSerie(); };
            Get["/serie/tag"] = x => { return SearchForTag(); };
            Get["/serie/byfolder"] = x => { return GetSeriesByFolderId(); };
            Get["/serie/watch"] = x => { return MarkSerieAsWatched(); };
            Get["/serie/unwatch"] = x => { return MarkSerieAsUnwatched(); };
            Get["/serie/vote"] = x => { return VoteOnSerie(); };
            #endregion

            #region 8. Series - [Obsolete]
            Get["/serie/list"] = _ => { return GetAllSeries(1,0,0,0,0,0); }; // [Obsolete] use /serie
            Get["/serie/{id}"] = x => { return GetSerieById(x.id, 1,0,0,0); }; // [Obsolete] use /serie?id=
            Get["/serie/recent/{max}"] = x => { return GetRecentSeries((int)x.max); }; // [Obsolete] use /serie/recent?limit=
            Get["/serie/byfolder/{id}"] = x => { return GetSerieByFolderId(x.id, 10); }; // [Obsolete] use /serie/byfolder/id=
            Get["/serie/byfolder/{id}/{max}"] = x => { return GetSerieByFolderId(x.id, x.max); }; // [Obsolete] use /serie/byfolder/id=&limit=
            Post["/serie/watch/{type}/{max}"] = x => { return MarkSerieWatched(true, x.max, x.type); }; // [Obsolete] use /serie/watch?id=
            Post["/serie/unwatch/{type}/{max}"] = x => { return MarkSerieWatched(false, x.max, x.type); }; // [Obsolete] use /serie/unwatch?id=
            Post["/serie/vote"] = x => { return VoteOnSerie2(); }; // [Obsolete] use /serie/vote?id=&score={1-10}
            #endregion         

            #region 9. Cloud accounts
            Get["/cloud/list"] = _ => { return GetCloudAccounts(); };
            Get["/cloud/count"] = _ => { return GetCloudAccountsCount(); };
            Post["/cloud/add"] = x => { return AddCloudAccount(); };
            Post["/cloud/delete"] = x => { return DeleteCloudAccount(); };
            Get["/cloud/import"] = _ => { return RunCloudImport(); };
            #endregion

            #region 10. Filters
            Get["/filter"] = _ => { return GetFilters(); };
            #endregion

            #region 11. Metadata - [Obsolete]
            Get["/metadata/{type}/{id}"] = x => { return GetMetadata_old((int)x.type, x.id); };
            Get["/metadata/{type}/{id}/nocast"] = x => { return GetMetadata_old((int)x.type, x.id, true); };
            Get["/metadata/{type}/{id}/{filter}"] = x => { return GetMetadata_old((int)x.type, x.id, false, x.filter); };
            Get["/metadata/{type}/{id}/{filter}/nocast"] = x => { return GetMetadata_old((int)x.type, x.id, true, x.filter); };

            #region test_only
            Get["/metadata2/{type}/{id}"] = x => { return GetMetadata((int)x.type, x.id); };
            #endregion

            #endregion
			
			#region 12. Groups
            Get["/group"] = _ => { return GetGroups(); };
            #endregion

        }

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

        #region 3.Actions

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

        /// <summary>
        /// Rescan given location ID (folder id) to recognize new episodes
        /// </summary>
        /// <param name="Vlid"></param>
        /// <returns></returns>
        private object RescanVideoLocal()
        {
            Request request = this.Request;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                try
                {
                    VideoLocal vid = RepoFactory.VideoLocal.GetByID(para.id);
                    if (vid == null) { return APIStatus.notFound404(); }
                    if (string.IsNullOrEmpty(vid.Hash)) { return APIStatus.badRequest("Could not Update a cloud file without hash, hash it locally first"); }
                    Commands.CommandRequest_ProcessFile cmd = new Commands.CommandRequest_ProcessFile(vid.VideoLocalID, true);
                    cmd.Save();
                }
                catch (Exception ex)
                {
                    return APIStatus.internalError(ex.Message);
                }

                return APIStatus.statusOK();
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Rehash given files in given VideoLocal
        /// </summary>
        /// <param name="VLid"></param>
        /// <returns></returns>
        private object RehashVideoLocal()
        {
            Request request = this.Request;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                VideoLocal vl = RepoFactory.VideoLocal.GetByID(para.id);
                if (vl != null)
                {
                    VideoLocal_Place pl = vl.GetBestVideoLocalPlace();
                    if (pl == null)
                    {
                        return APIStatus.notFound404("videolocal_place not found");
                    }
                    Commands.CommandRequest_HashFile cr_hashfile = new Commands.CommandRequest_HashFile(pl.FullServerPath, true);
                    cr_hashfile.Save();

                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.notFound404();
                }
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        #endregion

        #region 4. Misc

        /// <summary>
        /// Returns current user ID for use in legacy calls
        /// </summary>
        /// <returns>userid = int</returns>
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
            var response = client.DownloadString(new Uri("http://shokoanime.com/wp-json/wp/v2/posts"));
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

        /// <summary>
        /// Return Dictionary with nesesery items for Dashboard inside Webui
        /// </summary>
        /// <returns></returns>
        private object GetDashboard()
        {
            Dictionary<string, object> dash = new Dictionary<string, object>();
            dash.Add("queue", GetQueue());
            dash.Add("file", GetRecentFiles(0, 1)); //updated
            dash.Add("folder", GetFolders());
            dash.Add("file_count", CountFiles()); //updated
            dash.Add("serie_count", CountSerie()); //updated
            return dash;
        }


        #endregion

        #region 5.Queue

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

        #region 6.Files

        /// <summary>
        /// Handle /api/file w/wo ?id
        /// </summary>
        /// <returns>List<RawFile> or RawFile</returns>
        private object GetFile()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllFiles(para.limit, para.level, user.JMMUserID);
            }
            else
            {
                return GetFileById(para.id, para.level, user.JMMUserID);
            }
        }

        /// <summary>
        /// Handle /api/file/count
        /// </summary>
        /// <returns>Counter</returns>
        private object CountFiles()
        {
            Counter count = new Counter();
            count.count = RepoFactory.VideoLocal.GetAll().Count;
            return count;
        }

        /// <summary>
        /// Handle /api/file/recent
        /// </summary>
        /// <returns>List<RawFile></returns>
        private object GetRecentFiles(int limit = 0, int level = 0)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (limit == 0) { if (para.limit == 0) { para.limit = 10; } }
            else { para.limit = limit; }
            if (level != 0) { para.level = level; }

            List<RawFile> list = new List<RawFile>();
            foreach (VideoLocal file in RepoFactory.VideoLocal.GetMostRecentlyAdded(para.limit))
            {
                list.Add(new RawFile(file, para.level, user.JMMUserID));
            }

            return list;
        }

        /// <summary>
        /// Handle /api/file/unsort
        /// </summary>
        /// <returns>List<RawFile></returns>
        private object GetUnsort()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            List<object> lst = new List<object>();

            List<VideoLocal> vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (VideoLocal vl in vids)
            {
                if (para.offset == 0)
                {
                    RawFile v = new RawFile(vl, para.level, user.JMMUserID);
                    lst.Add(v);
                    if (para.limit != 0) { if (lst.Count >= para.limit) { break; } }
                }
                else { para.offset -= 1; }
            }

            return lst;
        }

        #region internal function

        /// <summary>
        /// Internal function returning file with given id
        /// </summary>
        /// <param name="file_id">file id</param>
        /// <returns>RawFile</returns>
        internal object GetFileById(int file_id, int level, int uid)
        {
            VideoLocal vl = RepoFactory.VideoLocal.GetByID(file_id);
            if (vl != null)
            {
                RawFile rawfile = new RawFile(vl, level, uid);
                return rawfile;
            }
            else
            {
                return APIStatus.notFound404();
            }
        }

        /// <summary>
        /// Internal function returning files
        /// </summary>
        /// <param name="limit">number of return items</param>
        /// <param name="offset">offset to start from</param>
        /// <returns>List<RawFile></returns>
        internal object GetAllFiles(int limit, int level, int uid)
        {
            List<RawFile> list = new List<RawFile>();
            int limit_x = limit;
            if (limit == 0) { limit_x = 100; }
            foreach (VideoLocal file in RepoFactory.VideoLocal.GetAll(limit_x))
            {
                list.Add(new RawFile(file, level, uid));
                if (limit != 0) { if (list.Count >= limit) { break; } }
            }

            return list;
        }

        #endregion

        #endregion

        #region 7.Episodes

        /// <summary>
        /// Handle /api/ep w/wo ?id
        /// </summary>
        /// <returns>List<Episode> or Episode</returns>
        private object GetEpisode()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllEpisodes(user.JMMUserID, para.limit, para.offset, para.level, para.all);
            }
            else
            {
                return GetEpisodeById(para.id, user.JMMUserID);
            }
        }

        /// <summary>
        /// handles /api/ep/getbyfilename?filename=...
        /// </summary>
        /// <returns>The found Episode given the on system filename.</returns>
        private object GetEpisodeFromName()
        {
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            string filename = this.Context.Request.Query.filename;
            if (String.IsNullOrEmpty(filename)) return new Nancy.Response { StatusCode = HttpStatusCode.BadRequest };

            AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByFilename(filename);
            return new Episode().GenerateFromAnimeEpisode(aep, user.JMMUserID, 0, 0);
        }
 
        /// <summary>
        /// Handle /api/ep/recent
        /// </summary>
        /// <returns>List<Episode></returns>
        private object GetRecentEpisodes()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0) { para.limit = 10; }
            List<object> lst = new List<object>();

            List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(para.limit);

            foreach (VideoLocal vl in vids)
            {
                foreach (AnimeEpisode aep in vl.GetAnimeEpisodes())
                {
                    Episode ep = new Episode().GenerateFromAnimeEpisode(aep, user.JMMUserID, para.level, para.all);
                    if (ep != null)
                    {
                        lst.Add(ep);
                    }
                }
            }

            return lst;
        }

        /// <summary>
        /// Handle /api/ep/watch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkEpisodeAsWatched()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkEpisode(true, para.id, user.JMMUserID);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/ep/unwatch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkEpisodeAsUnwatched()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkEpisode(false, para.id, user.JMMUserID);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/ep/vote
        /// </summary>
        /// <returns>APIStatus</returns>
        private object VoteOnEpisode()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                if (para.score != 0)
                {
                    return EpisodeVote(para.id, para.score, user.JMMUserID);
                }
                else
                {
                    return APIStatus.badRequest("missing 'score'");
                }
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/ep/scrobble
        /// </summary>
        /// <returns></returns>
        private object EpisodeScrobble()
        {
            return APIStatus.notImplemented();
        }

        #region internal function

        /// <summary>
        /// Internal function that change episode watched status
        /// </summary>
        /// <param name="status">true is watched, false is unwatched</param>
        /// <param name="id">episode id</param>
        /// <param name="uid">user id</param>
        /// <returns>APIStatus</returns>
        internal object MarkEpisode(bool status, int id, int uid)
        {
            try
            {
                AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(id);
                if (ep == null) { return APIStatus.notFound404(); }
                ep.ToggleWatchedStatus(status, true, DateTime.Now, false, false, uid, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                return APIStatus.statusOK();
            }
            catch (Exception ex)
            {
                return APIStatus.internalError(ex.Message);
            }
        }
        
        /// <summary>
        /// Internal function returning episodes
        /// </summary>
        /// <returns></returns>
        internal object GetAllEpisodes(int uid, int limit, int offset, int level, int all)
        {
            List<Episode> eps = new List<Episode>();
            List<int> aepul = RepoFactory.AnimeEpisode_User.GetByUserID(uid).Select(a => a.AnimeEpisodeID).ToList();
            foreach (int id in aepul)
            {
                if (offset == 0)
                {
                    eps.Add(new Episode().GenerateFromAnimeEpisodeID(id, uid, level, all));
                    if (limit != 0) { if (eps.Count >= limit) { break; } }
                }
                else { offset -= 1; }
            }

            return eps;
        }

        /// <summary>
        /// Internal function returning episode
        /// </summary>
        /// <param name="id">episode id</param>
        /// <param name="uid">user id</param>
        /// <returns>Episode</returns>
        internal object GetEpisodeById(int id, int uid)
        {
            if (id > 0)
            {
                Episode ep = GetEpisode(id, uid);
                if (ep != null) { return ep; }
                else { return APIStatus.notFound404("episode not found"); }
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Internal function for saving vote on given episode
        /// </summary>
        /// <param name="id">episode id</param>
        /// <param name="score">rating score as 1-10 or 100-1000</param>
        /// <param name="uid"></param>
        /// <returns>APIStatus</returns>
        internal object EpisodeVote(int id, int score, int uid)
        {
            if (id > 0)
            {
                if (score > 0 && score < 1000)
                {
                    List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(id);
                    AniDB_Vote thisVote = null;
                    foreach (AniDB_Vote dbVote in dbVotes)
                    {
                        if (dbVote.VoteType == (int)enAniDBVoteType.Episode)
                        {
                            thisVote = dbVote;
                        }
                    }

                    if (thisVote == null)
                    {
                        thisVote = new AniDB_Vote();
                        thisVote.VoteType = (int)enAniDBVoteType.Episode;
                        thisVote.EntityID = id;
                    }

                    if (score <= 10)
                    {
                        score = (int)(score * 100);
                    }

                    thisVote.VoteValue = score;
                    RepoFactory.AniDB_Vote.Save(thisVote);

                    //CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(animeID, voteType, voteValue);
                    //cmdVote.Save();

                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.badRequest("'score' value is wrong");
                }
            }
            else
            {
                return APIStatus.badRequest("'id' value is wrong");
            }
        }

        #endregion

        #endregion

        #region 8.Series

        /// <summary>
        /// Handle /api/serie w/wo ?id
        /// </summary>
        /// <returns>List<Serie> or Serie</returns>
        private object GetSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllSeries(para.nocast, para.limit, para.offset, para.notag, para.level, para.all);
            }
            else
            {
                return GetSerieById(para.id, para.nocast, para.notag, para.level, para.all);
            }
        }

        /// <summary>
        /// Handle /api/serie/count
        /// </summary>
        /// <returns>Counter</returns>
        private object CountSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Counter count = new Counter();
            count.count = RepoFactory.AnimeSeries.GetAll().Count;
            return count;
        }

        /// <summary>
        /// Handle /api/serie/byfolder
        /// </summary>
        /// <returns>List<Serie></returns>
        private object GetSeriesByFolderId()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                List<object> allseries = new List<object>();
                List<VideoLocal> vlpall = RepoFactory.VideoLocalPlace.GetByImportFolder(para.id).Select(a => a.VideoLocal).ToList();
                if (para.limit == 0) { para.limit = 10; }
                foreach (VideoLocal vl in vlpall)
                {
                    Serie ser = new Serie().GenerateFromVideoLocal(vl, user.JMMUserID, para.nocast, para.notag, para.level, para.all);
                    allseries.Add(ser);
                    if (allseries.Count >= para.limit) { break; }
                }

                return allseries;
            }
            else
            {
                return APIStatus.internalError("missing 'id'");
            }
        }
              
        /// <summary>
        /// Handle /api/serie/recent
        /// </summary>
        /// <returns>List<Serie></returns>
        private object GetSeriesRecent()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            List<object> allseries = new List<object>();

            if (para.limit == 0) { para.limit = 10; }
            List<AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(para.limit);

            foreach (AnimeSeries aser in series)
            {
                allseries.Add(new Serie().GenerateFromAnimeSeries(aser, user.JMMUserID, para.nocast, para.notag, para.level, para.all));
            }

            return allseries;
        }

        /// <summary>
        /// Handle /api/serie/watch
        /// </summary>
        /// <returns>APIStatus<Serie></returns>  
        private object MarkSerieAsWatched()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkSerieWatchStatus(para.id, true, user.JMMUserID);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/serie/watch
        /// </summary>
        /// <returns>APIStatus</returns>  
        private object MarkSerieAsUnwatched()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkSerieWatchStatus(para.id, false, user.JMMUserID);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/serie/vote
        /// </summary>
        /// <returns>APIStatus</returns>
        private object VoteOnSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                if (para.score != 0)
                {
                    return SerieVote(para.id, para.score, user.JMMUserID);
                }
                else
                {
                    return APIStatus.badRequest("missing 'score'");
                }
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/serie/search
        /// </summary>
        /// <returns>List<Serie></returns>
        private object SearchForSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0) { para.limit = 100; }
            if (para.query != "")
            {
                return Search(para.query, para.limit, para.offset, false, user.JMMUserID, para.nocast, para.notag, para.level, this.Request.Query.fuzzy);
            }
            else
            {
                return APIStatus.badRequest("missing 'query'");
            }
        }

        /// <summary>
        /// Handle /api/serie/tag
        /// </summary>
        /// <returns>List<Serie></returns>
        private object SearchForTag()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0) { para.limit = 100; }
            if (para.query != "")
            {
                return Search(para.query, para.limit, para.offset, true, user.JMMUserID, para.nocast, para.notag, para.level, para.all);
            }
            else
            {
                return APIStatus.badRequest("missing 'query'");
            }
        }

        #region internal function

        /// <summary>
        /// Internal function returning series
        /// </summary>
        /// <param name="nocast">disable cast</param>
        /// <param name="limit">number of return items</param>
        /// <param name="offset">offset to start from</param>
        /// <returns>List<Serie></returns>
        internal object GetAllSeries(int nocast, int limit, int offset, int notag, int level, int all)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            List<Serie> allseries = new List<Serie>();

            foreach (AnimeSeries asi in RepoFactory.AnimeSeries.GetAll())
            {
                if (offset <= 0)
                {
                    allseries.Add(new Serie().GenerateFromAnimeSeries(asi, user.JMMUserID, nocast, notag, level, all));
                    if (limit != 0) { if (allseries.Count >= limit) { break; } }
                }
                else { offset -= 1; }
            }

            return allseries;
        }

        /// <summary>
        /// Internal function returning serie with given ID
        /// </summary>
        /// <param name="series_id">serie id</param>
        /// <param name="nocast">disable cast</param>
        /// <returns></returns>
        internal object GetSerieById(int series_id, int nocast, int notag, int level, int all)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Serie ser = new Serie().GenerateFromAnimeSeries(RepoFactory.AnimeSeries.GetByID(series_id), user.JMMUserID, nocast, notag, level, all);
            return ser;
        }
       
        /// <summary>
        /// Internal function that mark serie
        /// </summary>
        /// <param name="id">serie id</param>
        /// <param name="watched">true is watched, false is unwatched</param>
        /// <param name="uid">user id</param>
        /// <returns>APIStatus</returns>
        internal object MarkSerieWatchStatus(int id, bool watched, int uid)
        {
            try
            {
                List<AnimeEpisode> eps = RepoFactory.AnimeEpisode.GetBySeriesID(id);

                AnimeSeries ser = null;
                foreach (AnimeEpisode ep in eps)
                {
                    AnimeEpisode_User epUser = ep.GetUserRecord(uid);
                    if (epUser != null)
                    {
                        if (epUser.WatchedCount <= 0 && watched)
                        {
                            ep.ToggleWatchedStatus(watched, true, DateTime.Now, false, false, uid, false);
                        }
                        else
                        {
                            if (epUser.WatchedCount > 0 && !watched)
                            {
                                ep.ToggleWatchedStatus(watched, true, DateTime.Now, false, false, uid, false);
                            }
                        }
                    }
                }

                if (ser != null)
                {
                    ser.UpdateStats(true, true, true);
                }
                return APIStatus.statusOK();
            }
            catch (Exception ex)
            {
                return APIStatus.internalError(ex.Message);
            }
        }

        /// <summary>
        /// Join a string like string.Join but 
        /// </summary>
        /// <param name="seperator"></param>
        /// <param name="values"></param>
        /// <param name="replaceinvalid"></param>
        /// <returns></returns>
        internal string Join(string seperator, IEnumerable<string> values, bool replaceinvalid)
        {
            if (!replaceinvalid) return string.Join(seperator, values);
            List<string> newItems = new List<string>();
   
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()) + "()+";
            System.Text.RegularExpressions.Regex remove = new System.Text.RegularExpressions.Regex(string.Format("[{0}]", System.Text.RegularExpressions.Regex.Escape(regexSearch)));
            System.Text.RegularExpressions.Regex extraSpaces = new System.Text.RegularExpressions.Regex(@"[ ]{2,}", System.Text.RegularExpressions.RegexOptions.None);
            System.Text.RegularExpressions.Regex replaceWithSpace = new System.Text.RegularExpressions.Regex("[\\-\\.]");

            foreach (string s in values)
            {
                //This is set up in such this way so that any duplicate spaces created are incedentally removed by the replaceWithSpace.
                //If there is a better way, feel free to optimise this.
                var actualItem = extraSpaces.Replace(remove.Replace(replaceWithSpace.Replace(s, " "), ""), "");
                newItems.Add(actualItem);
            }

            return string.Join(seperator, newItems);
        }

        /// <summary>
        /// Internal function that search for given query in name or tag inside series
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="limit">number of return items</param>
        /// <param name="offset">offset to start from</param>
        /// <param name="tag_search">True for searching in tags, False for searching in name</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="fuzzy">Disable searching for invalid path characters</param>
        /// <returns>List<Serie></returns>
        internal object Search(string query, int limit, int offset, bool tag_search, int uid, int nocast, int notag, int level, int all, bool fuzzy = false)
        {
            Filter search_filter = new Filter();
            search_filter.name = "Search";
            search_filter.groups = new List<Group>();

            Group search_group = new Group();
            search_group.name = query;
            search_group.series = new List<Serie>();
            
            IEnumerable<AnimeSeries> series = tag_search
	            ? RepoFactory.AnimeSeries.GetAll()
		            .Where(
			            a =>
				            a.Contract?.AniDBAnime?.AniDBAnime != null &&
				            (a.Contract.AniDBAnime.AniDBAnime.AllTags.Contains(query,
					             StringComparer.InvariantCultureIgnoreCase) ||
				             a.Contract.AniDBAnime.CustomTags.Select(b => b.TagName)
					             .Contains(query, StringComparer.InvariantCultureIgnoreCase)))
	            : RepoFactory.AnimeSeries.GetAll()
		            .Where(
			            a =>
				            a.Contract?.AniDBAnime?.AniDBAnime != null &&
				            Join(",", a.Contract.AniDBAnime.AniDBAnime.AllTitles, fuzzy)
					            .IndexOf(query, 0, StringComparison.InvariantCultureIgnoreCase) >= 0);

            foreach (AnimeSeries ser in series)
            {
                if (offset == 0)
                {
                    search_group.series.Add(new Serie().GenerateFromAnimeSeries(ser, uid, nocast, notag, level, all));
                    if (search_group.series.Count >= limit) { break; }
                }
                else { offset -= 1; }
            }

            search_group.size = search_group.series.Count();
            search_filter.groups.Add(search_group);
            search_filter.size = search_filter.groups.Count();

            return search_filter;
        }

        /// <summary>
        /// Internal function for saving vote on given serie
        /// </summary>
        /// <param name="id">serie id</param>
        /// <param name="score">rating score as 1-10 or 100-1000</param>
        /// <param name="uid"></param>
        /// <returns>APIStatus</returns>
        internal object SerieVote(int id, int score, int uid)
        {
            if (id > 0)
            {
                if (score > 0 && score <= 1000)
                {
                    List<AniDB_Vote> dbVotes = RepoFactory.AniDB_Vote.GetByEntity(id);
                    AniDB_Vote thisVote = null;
                    foreach (AniDB_Vote dbVote in dbVotes)
                    {
                        if (dbVote.VoteType == (int)enAniDBVoteType.Anime)
                        {
                            thisVote = dbVote;
                        }
                    }

                    if (thisVote == null)
                    {
                        thisVote = new AniDB_Vote();
                        thisVote.VoteType = (int)enAniDBVoteType.Anime;
                        thisVote.EntityID = id;
                    }

                    if (score <= 10)
                    {
                        score = (int)(score * 100);
                    }

                    thisVote.VoteValue = score;
                    RepoFactory.AniDB_Vote.Save(thisVote);
                    //Commands.CommandRequest_VoteAnime cmdVote = new Commands.CommandRequest_VoteAnime(para.id, (int)enAniDBVoteType.Anime, Convert.ToDecimal(para.score));
                    //cmdVote.Save();
                    return APIStatus.statusOK();
                }
                else
                {
                    return APIStatus.badRequest("'score' value is wrong");
                }
            }
            else
            {
                return APIStatus.badRequest("'id' value is wrong");
            }
        }
        
        #endregion

        #endregion

        #region 9.Cloud Accounts

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
 
        #region 10. Filters

        /// <summary>
        /// Handle /api/filter w/wo ?id
        /// Using if without ?id consider using ?level as it will scan resursive for object from Filter to RawFile
        /// </summary>
        /// <returns></returns>
        private object GetFilters()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllFilters(user.JMMUserID, para.nocast, para.notag, para.level, para.all);
            }
            else
            {
                return GetFilter(para.id, user.JMMUserID, para.nocast, para.notag, para.level, para.all);;
            }           
        }

        #region internal function

        /// <summary>
        /// Internal function that return all filter for given user
        /// </summary>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag/genre</param>
        /// <param name="level">deep level</param>
        /// <returns>List<Filter></returns>
        internal object GetAllFilters(int uid, int nocast, int notag, int level, int all)
        {
            List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetTopLevel().Where(a => a.InvisibleInClients == 0 && ((a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0) || (a.FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)).ToList();
            List<Filter> filters = new List<Filter>();

            foreach (GroupFilter gf in allGfs)
            {
                Filter filter = new Filter().GenerateFromGroupFilter(gf, uid, nocast, notag, level, all);
                filters.Add(filter);
            }

            // Unsort
            List<VideoLocal> vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
            if (vids.Count > 0)
            {
                Filter filter = new Filter();

                filter.url = APIHelper.ConstructUnsortUrl();
                filter.name = "Unsort";
                filter.art.fanart.Add(new Art() { url = APIHelper.ConstructSupportImageLink("plex_unsort.png"), index = 0 });
                filter.art.thumb.Add(new Art() { url = APIHelper.ConstructSupportImageLink("plex_unsort.png"), index = 0 });
                filter.size = vids.Count;
                filter.viewed = 0;

                filters.Add(filter);
            }

            return filters;
        }

        /// <summary>
        /// Internal function that return information about given filter
        /// </summary>
        /// <param name="id">filter id</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag/genre</param>
        /// <param name="level">deep level</param>
        /// <returns>Filter</returns>
        internal object GetFilter(int id, int uid, int nocast, int notag, int level, int all)
        {
            GroupFilter gf = RepoFactory.GroupFilter.GetByID(id);
            Filter filter = new Filter().GenerateFromGroupFilter(gf, uid, nocast, notag, level, all);

            return filter;
        }

        #endregion

        #endregion

        #region 11. Metadata - [Obsolete]

        [Obsolete]
        /// <summary>
        /// Return Metadata about object you asked for via MediaContainer (Legacy)
        /// </summary>
        /// <param name="typeid">type id</param>
        /// <param name="id">object id</param>
        /// <param name="nocast">disable roles output</param>
        /// <param name="filter"></param>
        /// <returns></returns>
        private object GetMetadata_old(int typeid, int id, bool nocast = false, string filter = "")
        {
  
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                int? filterid = filter.ParseNullableInt();
                return _impl.GetMetadata(_prov_kodi, user.JMMUserID.ToString(), typeid.ToString(), id.ToString(), null, nocast, filterid);
            }
            else
            {
                return new APIMessage(500, "Unable to get User");
            }
        }

        [Obsolete]
        private object GetMetadata(int type_id, string id, bool nocast = false, string filter = "")
        {
            Core.request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            if (user != null)
            {
                switch ((JMMType)type_id)
                {
                    //0
                    case JMMType.GroupFilter:
                        return GetGroupsOrSubFiltersFromFilter(id, user.JMMUserID);
                    //1 
                    case JMMType.GroupUnsort:
                        return GetUnsort();
                    //2 
                    case JMMType.Group:
                        return GetItemsFromGroup(user.JMMUserID, id, nocast);
                    //3
                    case JMMType.Serie:
                        return GetItemsFromSerie(user.JMMUserID, id, nocast);
                    //5
                    case JMMType.Episode:
                        return GetFromEpisode(user.JMMUserID, id);
                    //6
                    case JMMType.File:
                        return GetFromFile(user.JMMUserID, id);
                    //7
            //        case JMMType.Playlist:
            //            return GetItemsFromPlaylist(prov, user.JMMUserID, Id, his);
                    //8
            //        case JMMType.FakeIosThumb:
            //            return FakeParentForIOSThumbnail(prov, Id);
                    default:
                        return APIStatus.badRequest("bad type");
                }
            }
            else
            {
                return APIStatus.accessDenied();
            }
        }

        #region test_only
        [Obsolete]
        private object GetGroupsOrSubFiltersFromFilter(string GroupFilterID, int uid)
        {
            try
            {
                int groupFilterID = -1;
                int.TryParse(GroupFilterID, out groupFilterID);
                ObjectList dir = new ObjectList();
                if (groupFilterID >= 0)
                {
                    GroupFilter gf = RepoFactory.GroupFilter.GetByID(groupFilterID);

                    if (gf == null) { return APIStatus.notFound404(); }

                    dir.name = gf.GroupFilterName;
                    dir.type = "show";

                    List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetByParentID(groupFilterID).Where(a => a.InvisibleInClients == 0 &&
                    (
                        (a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0)
                        || (a.FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)
                    ).ToList();

                    List<Filter> dirs = new List<Filter>();
                    foreach (GroupFilter gg in allGfs)
                    {
                        Filter pp = APIHelper.FilterFromGroupFilter(gg, uid);
                        dirs.Add(pp);
                    }

                    if (dirs.Count > 0)
                    {

                        dir.Add(new List<object>(dirs.OrderBy(a => a.name).Cast<object>().ToList()));
                        return dir;
                    }

                    if (gf.GroupsIds.ContainsKey(uid))
                    {
                        foreach (AnimeGroup grp in gf.GroupsIds[uid].ToList().Select(a => RepoFactory.AnimeGroup.GetByID(a)).Where(a => a != null))
                        {
                            Filter pp = APIHelper.FilterFromAnimeGroup(grp, uid);
                            dirs.Add(pp);
                        }

                        dir.Add(new List<object>(dirs.Cast<object>().ToList())); 
                    }
                }
                
                return dir;
            }
            catch (Exception ex)
            {
                return APIStatus.internalError(ex.Message.ToString());
            }
        }

        [Obsolete]
        private object GetItemsFromSerie(int uid, string SerieId, bool nocast = false)
        {
            int serieID;
            enEpisodeType? eptype = null;
            if (SerieId.Contains("_"))
            {
                int ept;
                string[] ndata = SerieId.Split('_');
                if (!int.TryParse(ndata[0], out ept)) { return APIStatus.notFound404("Invalid Serie Id"); }
                if (!int.TryParse(ndata[1], out serieID)) { return APIStatus.notFound404("Invalid Serie Id"); }
                eptype = (enEpisodeType)ept;
            }
            else
            {
                if (!int.TryParse(SerieId, out serieID)) { return APIStatus.notFound404("Invalid Serie Id"); }
            }

            AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(serieID);
            if (ser == null) { return APIStatus.notFound404("Series not found"); }
            Contract_AnimeSeries cseries = ser.GetUserContract(uid);
            if (cseries == null) { return APIStatus.notFound404("Invalid Series, Contract Not Found"); }

            Serie sers = new Serie();

            Video nv = ser.GetPlexContract(uid);


            Dictionary<AnimeEpisode, Contract_AnimeEpisode> episodes = ser.GetAnimeEpisodes().ToDictionary(a => a, a => a.GetUserContract(uid));
            episodes = episodes.Where(a => a.Value == null || a.Value.LocalFileCount > 0).ToDictionary(a => a.Key, a => a.Value);

            sers.size = (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
            sers.art.fanart.Add(new Art() { url = cseries.AniDBAnime?.AniDBAnime?.DefaultImageFanart?.GenArt(null), index = 0 });
            sers.viewed = cseries.WatchedEpisodeCount.ToString();

            if (eptype.HasValue)
            {
                episodes = episodes.Where(a => a.Key.EpisodeTypeEnum == eptype.Value).ToDictionary(a => a.Key, a => a.Value);
            }
            else
            {
                List<enEpisodeType> types = episodes.Keys.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                if (types.Count > 1)
                {
                    List<PlexEpisodeType> eps = new List<PlexEpisodeType>();
                    foreach (enEpisodeType ee in types)
                    {
                        PlexEpisodeType k2 = new PlexEpisodeType();
                        PlexEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)cseries.AniDBAnime.AniDBAnime.AnimeType, episodes.Count(a => a.Key.EpisodeTypeEnum == ee));
                        eps.Add(k2);
                    }

                    List<Serie> dirs = new List<Serie>();

                    foreach (PlexEpisodeType ee in eps.OrderBy(a => a.Name))
                    {
                        Serie ob = new Serie();
                        ob.art.fanart.Add(new Art() { url = APIHelper.ConstructImageLinkFromRest(nv.Art), index = 0 });
                        ob.art.thumb.Add(new Art() { url = APIHelper.ConstructSupportImageLink(ee.Image), index = 0 });
                        ob.titles.Add(new AnimeTitle() { Title = ee.Name });
                        ob.size = ee.Count.ToString();
                        ob.viewed = "0";
                        // ob.url = APIHelper.ConstructSerieIdUrl(ee.Type + "_" + ser.AnimeSeriesID);
                        dirs.Add(ob);
                    }

                    return dirs;
                }
            }

            List<Episode> lep = new List<Episode>();

            foreach (KeyValuePair<AnimeEpisode, Contract_AnimeEpisode> epi in episodes)
            {
                try
                {
                    Episode ep = new Episode().GenerateFromAnimeEpisode(epi.Key, uid, 0, 0);
                    lep.Add(ep);
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }

                
                sers.eps = lep.OrderBy(a => a.epnumber).ToList();

                return sers;
            }

            return sers;
        }

        [Obsolete]
        private object GetFromEpisode(int uid, string aep_Id)
        {
            int aep_id = -1;
            int.TryParse(aep_Id, out aep_id);

            if (aep_id > 0)
            {
                List<Video> dirs = new List<Video>();

                AnimeEpisode e = RepoFactory.AnimeEpisode.GetByID(aep_id);
                if (e == null) { return APIStatus.notFound404("Episode not found"); }

                //KeyValuePair<AnimeEpisode, Contract_AnimeEpisode> ep = new KeyValuePair<AnimeEpisode, Contract_AnimeEpisode>(e, e.GetUserContract(uid));
                //if (ep.Value != null && ep.Value.LocalFileCount == 0) { return APIStatus.notFound404("Episode do not have videolocals"); }

                //AniDB_Episode aep = ep.Key.AniDB_Episode;
                //if (aep == null) { return APIStatus.notFound404("Invalid Episode AniDB link not found"); }

                //AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(ep.Key.AnimeSeriesID);
                //if (ser == null) { return APIStatus.notFound404("Invalid Serie"); }
                //AniDB_Anime anime = ser.GetAnime();

                //Contract_AnimeSeries con = ser.GetUserContract(uid);
                //if (con == null) { return APIStatus.notFound404("Invalid Serie, Contract not found"); }

                try
                {
                    Episode epi = new Episode().GenerateFromAnimeEpisode(e, uid, 0, 0);
                    return epi;
                }
                catch (Exception ex)
                {
                    return APIStatus.internalError(ex.Message.ToString());
                }
            }
            return APIStatus.notFound404("Ep id not found");
        }

        [Obsolete]
        private object GetItemsFromGroup(int uid, string GroupId, bool nocast = false)
        {
            int gid;
            int.TryParse(GroupId, out gid);
            if (gid == -1) { return APIStatus.internalError("Invalid Group Id"); }

            ObjectList obl = new ObjectList();

            List<Video> retGroups = new List<Video>();
            AnimeGroup grp = RepoFactory.AnimeGroup.GetByID(gid);
            if (grp == null) { return APIStatus.notFound404("Group not found"); }

            Contract_AnimeGroup basegrp = grp?.GetUserContract(uid);
            if (basegrp != null)
            {
                List<AnimeSeries> seriesList = grp.GetSeries();
                
                foreach (AnimeGroup grpChild in grp.GetChildGroups())
                {
                    Filter fr = new Filter();

                    var v = grpChild.GetPlexContract(uid);
                    if (v != null)
                    {
                        fr.url = APIHelper.ConstructGroupIdUrl(gid.ToString());

                        fr.art.fanart.Add(new Art() { url = Helper.GetRandomFanartFromVideo(v,null) ?? v.Art, index = 0 });
                        fr.art.banner.Add(new Art() { url = v.Banner = Helper.GetRandomBannerFromVideo(v,null) ?? v.Banner, index = 0 });

                        obl.list.Add(fr);                 
                    }
                }
                foreach (AnimeSeries ser in seriesList)
                {
                    Serie seri = new Serie().GenerateFromAnimeSeries(ser, uid, 0, 0, 0, 0);
                    obl.list.Add(seri);
                }
            }

            return obl;
        }

        [Obsolete]
        private object GetFromFile(int uid, string vl_Id)
        {
            int id;
            if (!int.TryParse(vl_Id, out id)) { return APIStatus.badRequest("bad group id"); }

            VideoLocal vi = RepoFactory.VideoLocal.GetByID(id);

            RawFile rf = new RawFile(vi, 0,1 );

            return rf;
        }
        #endregion

        #endregion

        #region Obsolete

        #region 11 only
        IProvider _prov_kodi = new PlexAndKodi.Kodi.KodiProvider();
        CommonImplementation _impl = new CommonImplementation();
        #endregion

        #region Obsolete - calls
        [Obsolete]
        /// <summary>
        /// Get List of all files
        /// </summary>
        /// <returns></returns>
        private object GetAllFiles_old()
        {
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            Dictionary<int, string> files = new Dictionary<int, string>();
            foreach (VideoLocal file in _impl.GetAllFiles())
            {
                files.Add(file.VideoLocalID, file.FileName);
            }

            return files;
        }
        [Obsolete]
        /// <summary>
        /// Return List<> of recently added files paths
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentFiles_old(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            List<RecentFile> files = new List<RecentFile>();

            foreach (VideoLocal file in _impl.GetFilesRecentlyAdded(max_limit))
            {
                RecentFile recent = new RecentFile();
                recent.path = "";
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

        [Obsolete]
        /// <summary>
        /// Return given number of unsort items from collection
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetUnsort(int max_limit)
        {
            ObjectList dir = new ObjectList("unsort", ObjectList.ListType.FILE);
            List<object> lst = new List<object>();

            List<VideoLocal> vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (VideoLocal vl in vids)
            {
                try
                {
                    RawFile v = new RawFile(vl, 0, 1);
                    lst.Add(v);
                }
                catch { }

                if (max_limit != -1)
                {
                    if (lst.Count >= max_limit)
                    {
                        break;
                    }
                }
            }

            dir.Add(lst);
            return dir;
        }

        [Obsolete]
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
        [Obsolete]
        internal object GetAllEpisodes()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            ObjectList ob = new ObjectList("all episodes", ObjectList.ListType.EPISODE);
            List<object> eps = new List<object>();
            List<int> aepul = RepoFactory.AnimeEpisode_User.GetByUserID(user.JMMUserID).Select(a => a.AnimeEpisodeID).ToList();
            foreach (int id in aepul)
            {
                eps.Add(new Episode().GenerateFromAnimeEpisodeID(id, user.JMMUserID, 0, 0));
            }
            ob.Add(eps);

            return ob;
        }
        [Obsolete]
        private object GetEpisodeById(int ep_id)
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;

            if (ep_id > 0)
            {
                Episode ep = GetEpisode(ep_id, user.JMMUserID);
                if (ep != null) { return ep; }
                else { return APIStatus.notFound404("episode not found"); }
            }
            else
            {
                return APIStatus.badRequest();
            }
        }

        [Obsolete]
        /// <summary>
        /// Return Episode object with given Id
        /// </summary>
        /// <param name="ep_id">Episode id</param>
        /// <param name="uid">User id</param>
        /// <returns></returns>
        internal Episode GetEpisode(int ep_id, int uid)
        {
            AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(ep_id);

            Episode ep = new Episode().GenerateFromAnimeEpisode(aep, uid, 0, 0);
            return ep;
        }
        [Obsolete]
        /// <summary>
        /// Get recent Episodes for current user
        /// </summary>
        /// <param name="max_limit">maximal number of items</param>
        /// <returns></returns>
        private object GetRecentEpisodes(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            ObjectList obl = new ObjectList("recent episodes", ObjectList.ListType.EPISODE);
            List<object> lst = new List<object>();

            List<VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(max_limit);

            foreach (VideoLocal vl in vids)
            {
                foreach (AnimeEpisode aep in vl.GetAnimeEpisodes())
                {
                    Episode ep = new Episode().GenerateFromAnimeEpisode(aep, user.JMMUserID, 0, 0);
                    lst.Add(ep);
                }
            }

            obl.Add(lst);

            return obl;
        }

        [Obsolete]
        /// <summary>
        /// Set score for episode
        /// </summary>
        /// <returns></returns>
        private object VoteOnEpisode2()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters epi = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            _impl.VoteAnime(epi.id, (decimal)epi.score, (int)AniDBAPI.enAniDBVoteType.Episode);

            return APIStatus.statusOK();
        }
        [Obsolete]
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

            ObjectList ob = new ObjectList("all series", ObjectList.ListType.SERIE);
            List<object> allseries = new List<object>();

            List<VideoLocal> vlpall = RepoFactory.VideoLocalPlace.GetByImportFolder(folder_id).Select(a => a.VideoLocal).ToList();

            foreach (VideoLocal vl in vlpall)
            {
                Serie ser = new Serie().GenerateFromVideoLocal(vl, user.JMMUserID, 1, 0, 0, 0);
                allseries.Add(ser);
                if (allseries.Count >= max) { break; }
            }

            ob.Add(allseries);
            return ob;
        }

        [Obsolete]
        private object GetRecentSeries(int limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            ObjectList ob = new ObjectList("all series", ObjectList.ListType.SERIE);
            List<object> allseries = new List<object>();

            List<AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(limit);

            foreach (AnimeSeries aser in series)
            {
                allseries.Add(new Serie().GenerateFromAnimeSeries(aser, user.JMMUserID, 1,0,0,0));
            }

            ob.Add(allseries);
            return ob;
        }
        [Obsolete]
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
            JMMServiceImplementation _impl = new JMMServiceImplementation();
            API_Call_Parameters para = this.Bind();
            return _impl.SetWatchedStatusOnSeries(para.id, status, max_episodes, type, user.JMMUserID);
        }
        [Obsolete]
        /// <summary>
        /// Set score for serie
        /// </summary>
        /// <returns></returns>
        private object VoteOnSerie2()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            API_Call_Parameters ser = this.Bind();

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            _impl.VoteAnime(ser.id, (decimal)ser.score, (int)AniDBAPI.enAniDBVoteType.Anime);

            return APIStatus.statusOK();
        }
        #endregion

        #endregion
		
		#region 12. Group

        public object GetGroups()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllGroups(user.JMMUserID, para.nocast, para.notag, para.level, para.all);
            }
            else
            {
                return GetGroup(para.id, user.JMMUserID, para.nocast, para.notag, para.level, para.all, para.filter);
            }
        }

        #region internal function

        internal object GetAllGroups(int uid, int nocast, int notag, int level, int all)
        {
            List<API.Model.common.Group> grps = new List<API.Model.common.Group>();
            List<AnimeGroup_User> allGrps = RepoFactory.AnimeGroup_User.GetByUserID(uid);
            foreach (AnimeGroup_User gr in allGrps)
            {
                AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(gr.AnimeGroupID);
                Group grp = new Group().GenerateFromAnimeGroup(ag, uid, nocast, notag, level, all, 0);
                grps.Add(grp);
            }
            return grps;
        }

        internal object GetGroup(int id, int uid, int nocast, int notag, int level, int all, int filterid)
        {
            //SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(id);
            AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(id);
            API.Model.common.Group gr = new API.Model.common.Group().GenerateFromAnimeGroup(ag, uid, nocast, notag, level, all, filterid);
            return gr;
        }

        #endregion

        #endregion
    }
}
