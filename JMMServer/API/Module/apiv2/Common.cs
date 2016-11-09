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

namespace JMMServer.API.Module.apiv2
{
    //As responds for this API we throw object that will be converted to json/xml or standard http codes (HttpStatusCode)
    public class Common : Nancy.NancyModule
    {
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)
        //routes are named with twitter api style
        //every function with summary is implemented 
        //private funtions are the ones for api calls directly and internal ones are support function for private ones
        public Common() : base("/api")
        {
            // As this module requireAuthentication all request need to have apikey in header.

            this.RequiresAuthentication();

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
            Get["/rescan/{vlid}"] = x => { return RescanVideoLocal(x.vlid); };
            Get["/rehash/{vlid}"] = x => { return RehashVideoLocal(x.vlid); };
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
            Get["/file/list"] = _ => { return GetAllFiles_old(); };
            Get["/file/listnew"] = _ => { return GetAllFiles(); }; //
            Get["/file/count"] = _ => { return CountFiles(); }; //
            Get["/file/{id}"] = x => { return GetFileById(x.id); }; //
            Get["/file/recent"] = x => { return GetRecentFiles_old(10); };
            Get["/file/recent/{max}"] = x => { return GetRecentFiles_old((int)x.max); };
            Get["/file/recentnew"] = x => { return GetRecentFiles(10); }; //
            Get["/file/recentnew/{max}"] = x => { return GetRecentFiles((int)x.max); }; //
            Get["/file/unrecognised"] = x => { return GetUnrecognisedFiles(10); };
            Get["/file/unrecognised/{max}"] = x => { return GetUnrecognisedFiles((int)x.max); };
            Get["/file/unsort"] = _ => { return GetUnsort(); }; //
            Get["/file/unsort/{max}"] = x => { return GetUnsort((int)x.max); }; //
            #endregion

            #region 7. Episodes
            Get["/ep/list"] = _ => { return GetAllEpisodes(); ; }; //
            Get["/ep/{id}"] = x => { return GetEpisodeById(x.id); }; //
            Get["/ep/{id}/image"] = x => { return GetEpisodeImage(x.id); }; //
            Get["/ep/recent"] = x => { return GetRecentEpisodes(10); }; //
            Get["/ep/recent/{max}"] = x => { return GetRecentEpisodes((int)x.max); }; //
            Post["/ep/watch"] = x => { return MarkEpisodeWatched(true); };
            Post["/ep/unwatch"] = x => { return MarkEpisodeWatched(false); };
            Post["/ep/vote"] = x => { return VoteOnEpisode(); };
            Post["/ep/trakt"] = x => { return EpisodeScrobble(); };
            Get["/ep/unsort"] = _ => { return GetUnsort(); }; //
            Get["/ep/unsort/{max}"] = x => { return GetUnsort((int)x.max); }; //
            #endregion

            #region 8. Series
            Get["/serie/list"] = _ => { return GetAllSeries(); ; }; //
            Get["/serie/count"] = _ => { return CountSerie(); ; }; // 
            Get["/serie/{id}"] = x => { return GetSerieById(x.id); ; }; //
            Get["/serie/recent"] = _ => { return GetRecentSeries(10); }; //
            Get["/serie/recent/{max}"] = x => { return GetRecentSeries((int)x.max); }; //
            Post["/serie/search"] = x => { return SearchForSerie(); }; //
            Post["/serie/search/{limit}"] = x => { return SearchForSerie((int)x.limit); }; //
            Post["/serie/tag"] = x => { return SearchForTag(); }; //
            Post["/serie/tag/{limit}"] = x => { return SearchForTag((int)x.limit); }; //
            Get["/serie/byfolder/{id}"] = x => { return GetSerieByFolderId(x.id, 10); }; //
            Get["/serie/byfolder/{id}/{max}"] = x => { return GetSerieByFolderId(x.id, x.max); }; //
            Post["/serie/watch/{type}/{max}"] = x => { return MarkSerieWatched(true, x.max, x.type); };
            Post["/serie/unwatch/{type}/{max}"] = x => { return MarkSerieWatched(false, x.max, x.type); };
            Post["/serie/vote"] = x => { return VoteOnSerie(); };
            Get["/serie/{id}/art"] = x => { return GetSerieArt((int)x.id); }; //
            #endregion

            #region 9. Cloud accounts
            Get["/cloud/list"] = _ => { return GetCloudAccounts(); };
            Get["/cloud/count"] = _ => { return GetCloudAccountsCount(); };
            Post["/cloud/add"] = x => { return AddCloudAccount(); };
            Post["/cloud/delete"] = x => { return DeleteCloudAccount(); };
            Get["/cloud/import"] = _ => { return RunCloudImport(); };
            #endregion

            #region 10. Images
            Get["/cover/{id}"] = x => { return GetCover((int)x.id); };
            Get["/fanart/{id}"] = x => { return GetFanart((int)x.id); };
            Get["/poster/{id}"] = x => { return GetPoster((int)x.id); };
            Get["/thumb/{type}/{id}"] = x => { return GetThumb((int)x.type, (int)x.id); };
            Get["/thumb/{type}/{id}/{ratio}"] = x => { return GetThumb((int)x.type, (int)x.id, x.ratio); };
            Get["/banner/{id}"] = x => { return GetImage((int)x.id, 4, false); };
            Get["/fanart/{id}"] = x => { return GetImage((int)x.id, 7, false); };

            Get["/image/{type}/{id}"] = x => { return GetImage((int)x.id, (int)x.type, false); };
            Get["/image/support/{name}"] = x => { return GetSupportImage(x.name); };
            #endregion

            #region 11. Filters
            Get["/filter/list"] = _ => { return GetFilters(); };
            Get["/filter/{id}"] = x => { return GetFilter((int)x.id); };
            #endregion

            #region 12. Metadata
            Get["/metadata/{type}/{id}"] = x => { return GetMetadata_old((int)x.type, x.id); };
            Get["/metadata/{type}/{id}/nocast"] = x => { return GetMetadata_old((int)x.type, x.id, true); };
            Get["/metadata/{type}/{id}/{filter}"] = x => { return GetMetadata_old((int)x.type, x.id, false, x.filter); };
            Get["/metadata/{type}/{id}/{filter}/nocast"] = x => { return GetMetadata_old((int)x.type, x.id, true, x.filter); };

            #region test_only
            Get["/metadata2/{type}/{id}"] = x => { return GetMetadata((int)x.type, x.id); };
            #endregion

            #endregion

        }

        JMMServiceImplementationREST _rest = new JMMServiceImplementationREST();
        JMMServiceImplementation _binary = new JMMServiceImplementation();

        #region for 11 & 12 only
        IProvider _prov_kodi = new PlexAndKodi.Kodi.KodiProvider();
        CommonImplementation _impl = new CommonImplementation();
        #endregion

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
        /// <param name="vlid"></param>
        /// <returns></returns>
        private object RescanVideoLocal(string vlid)
        {
            int videoLocalID = -1;
            int.TryParse(vlid, out videoLocalID);
            if (videoLocalID == -1)
            {
                return APIStatus.badRequest("videolocalid is negative");
            }

            string output = _binary.RescanFile(videoLocalID);

            if (!string.IsNullOrEmpty(output))
            {
                return APIStatus.badRequest(output);
            }

            return APIStatus.statusOK();
        }

        private object RehashVideoLocal(string vlid)
        {
            int videoLocalID = -1;
            int.TryParse(vlid, out videoLocalID);
            if (videoLocalID == -1)
            {
                return APIStatus.badRequest("videolocalid is negative");
            }

            _binary.RehashFile(videoLocalID);

            return APIStatus.statusOK();
        }

        #endregion

        #region 4. Misc

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

        /// <summary>
        /// Return Dictionary with nesesery items for Dashboard inside Webui
        /// </summary>
        /// <returns></returns>
        private object GetDashboard()
        {
            Dictionary<string, object> dash = new Dictionary<string, object>();
            dash.Add("queue", GetQueue());
            dash.Add("file", GetRecentFiles_old(10));
            dash.Add("folder", GetFolders());
            dash.Add("file_count", CountFiles());
            dash.Add("serie_count", CountSerie());
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
        /// Get file info by its ID
        /// </summary>
        /// <param name="file_id"></param>
        /// <returns></returns>
        private object GetFileById(int file_id)
        {
            VideoLocal vl = RepoFactory.VideoLocal.GetByID(file_id);
            if (vl != null)
            {
                RawFile rawfile = new RawFile(vl);
                return rawfile;
            }
            else
            {
                return APIStatus.notFound404();
            }
        }

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

        /// <summary>
        /// Get list of all files
        /// </summary>
        /// <returns></returns>
        private object GetAllFiles()
        {
            ObjectList ob = new ObjectList("all files", ObjectList.ListType.FILE);
            List<object> list = new List<object>();
            
            foreach (VideoLocal file in RepoFactory.VideoLocal.GetAll())
            {
                list.Add(new RawFile(file));
            }

            ob.Add(list);
            return ob;
        }

        /// <summary>
        /// return how many files collection have
        /// </summary>
        /// <returns></returns>
        private object CountFiles()
        {
            Counter count = new Counter();
            count.count = RepoFactory.VideoLocal.GetAll().Count;
            return count;
        }

        //TODO APIv2: Deprecated use GetRecentFiles
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
        /// Return recent added files
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentFiles(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            ObjectList ob = new ObjectList("recent files", ObjectList.ListType.FILE);
            List<object> list = new List<object>();

            foreach (VideoLocal file in RepoFactory.VideoLocal.GetMostRecentlyAdded(max_limit))
            {
                list.Add(new RawFile(file));
            }

            ob.Add(list);
            return ob;
        }

        /// <summary>
        /// Return all unsort items in collection
        /// </summary>
        /// <returns></returns>
        private object GetUnsort()
        {
            return GetUnsort(-1);
        }

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
                    RawFile v = new RawFile(vl);
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

        //TODO APIv2: Deprecated use GetUnsort
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

        #region 7.Episodes

        /// <summary>
        /// return all known anime series
        /// </summary>
        /// <returns></returns>
        private object GetAllEpisodes()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;

            ObjectList ob = new ObjectList("all episodes", ObjectList.ListType.EPISODE);
            List<object> eps = new List<object>();
            List<int> aepul = RepoFactory.AnimeEpisode_User.GetByUserID(user.JMMUserID).Select(a => a.AnimeEpisodeID).ToList();
            foreach(int id in aepul)
            {
                eps.Add(new Episode().GenerateFromAnimeEpisodeID(id, user.JMMUserID));
            }
            ob.Add(eps);

            return ob;
        }

        /// <summary>
        /// Return information about episode by given ID for current user
        /// </summary>
        /// <param name="ep_id"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Return Episode object with given Id
        /// </summary>
        /// <param name="ep_id">Episode id</param>
        /// <param name="uid">User id</param>
        /// <returns></returns>
        internal Episode GetEpisode(int ep_id, int uid)
        {
            AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(ep_id);

            Episode ep = new Episode().GenerateFromAnimeEpisode(aep, uid);
            return ep;
        }

        /// <summary>
        /// Return ArtCollection item of given episode
        /// </summary>
        /// <param name="ep_id"></param>
        /// <returns></returns>
        private object GetEpisodeImage(int ep_id)
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;

            if (ep_id != 0)
            {
                AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(ep_id);
                if (aep != null)
                {
                    Episode ep = new Episode().GenerateFromAnimeEpisode(aep, user.JMMUserID);
                    if (ep!=null)
                    {
                        return ep.art;
                    }
                }
                return APIStatus.notFound404();
            }
            else
            {
                return APIStatus.badRequest();
            }
        }

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
                    Episode ep = new Episode().GenerateFromAnimeEpisode(aep, user.JMMUserID);
                    lst.Add(ep);
                }
            }

            obl.Add(lst);

            return obl;
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

        #region 8.Series

        /// <summary>
        /// Return number of series inside collection
        /// </summary>
        /// <returns></returns>
        private object CountSerie()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Counter count = new Counter();
            count.count = RepoFactory.AnimeSeries.GetAll().Count;
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

            ObjectList ob = new ObjectList("all series", ObjectList.ListType.SERIE);
            List<object> allseries = new List<object>();

            foreach (AnimeSeries asi in RepoFactory.AnimeSeries.GetAll())
            {
                allseries.Add(new Serie().GenerateFromAnimeSeries(asi, user.JMMUserID));
            }

            ob.Add(allseries);
            return ob;
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
            Serie ser = new Serie().GenerateFromAnimeSeries(RepoFactory.AnimeSeries.GetByID(series_id), user.JMMUserID);
            return ser;
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

            ObjectList ob = new ObjectList("all series", ObjectList.ListType.SERIE);
            List<object> allseries = new List<object>();

            List<VideoLocal> vlpall = RepoFactory.VideoLocalPlace.GetByImportFolder(folder_id).Select(a => a.VideoLocal).ToList();

            foreach (VideoLocal vl in vlpall)
            {
                Serie ser = new Serie().GenerateFromVideoLocal(vl, user.JMMUserID);
                allseries.Add(ser);
                if (allseries.Count >= max) { break; }
            }

            ob.Add(allseries);
            return ob;
        }

        /// <summary>
        /// Return Recent added series
        /// </summary>
        /// <param name="max_limit"></param>
        /// <returns></returns>
        private object GetRecentSeries(int max_limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            ObjectList ob = new ObjectList("all series", ObjectList.ListType.SERIE);
            List<object> allseries = new List<object>();

            List<AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(max_limit);

            foreach (AnimeSeries aser in series)
            {
                allseries.Add(new Serie().GenerateFromAnimeSeries(aser, user.JMMUserID));
            }

            ob.Add(allseries);
            return ob;
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

        /// <summary>
        /// Return ArtCollection
        /// </summary>
        /// <param name="serie_id">Serie id</param>
        /// <returns></returns>
        private object GetSerieArt(int serie_id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            JMMServiceImplementation _impl = new JMMServiceImplementation();

            AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(serie_id);
            if (ser == null) { return APIStatus.notFound404(); }
            Serie seri = new Serie().GenerateFromAnimeSeries(ser, user.JMMUserID);
            if (seri == null) { return APIStatus.notFound404(); }

            return seri.art;
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
        /// POST: query
        /// </summary>
        /// <returns></returns>
        private object SearchForSerie(int limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, limit, false, user.JMMUserID);
        }

        /// <summary>
        /// Return list of 100 series with matching Tag
        /// POST: query
        /// </summary>
        /// <returns></returns>
        private object SearchForTag()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, 100, true, user.JMMUserID);
        }

        /// <summary>
        /// Return list of 100 series with matching Tag
        /// POST: query
        /// </summary>
        /// <returns></returns>
        private object SearchForTag(int limit)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;
            Search ser = this.Bind();

            return Search(ser.query, limit, true, user.JMMUserID);
        }

        /// <summary>
        /// Return list of series that match the serach query
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="limit">maximum number of items in list</param>
        /// <param name="tag_search">True for searching Tags, False for searching name</param>
        /// <param name="uid">User id</param>
        /// <returns></returns>
        internal object Search(string query, int limit, bool tag_search, int uid)
        {
            ObjectList ob = new ObjectList("search", ObjectList.ListType.SERIE);
            List<object> list = new List<object>();

            IEnumerable<AnimeSeries> series = tag_search
                ? RepoFactory.AnimeSeries.GetAll()
                    .Where(
                        a =>
                            a.Contract != null && a.Contract.AniDBAnime != null &&
                            a.Contract.AniDBAnime.AniDBAnime != null &&
                            (a.Contract.AniDBAnime.AniDBAnime.AllTags.Contains(query,
                                StringComparer.InvariantCultureIgnoreCase) ||
                            a.Contract.AniDBAnime.CustomTags.Select(b => b.TagName)
                                .Contains(query, StringComparer.InvariantCultureIgnoreCase)))
                : RepoFactory.AnimeSeries.GetAll()
                    .Where(
                        a =>
                            a.Contract != null && a.Contract.AniDBAnime != null &&
                            a.Contract.AniDBAnime.AniDBAnime != null &&
                            string.Join(",", a.Contract.AniDBAnime.AniDBAnime.AllTitles).IndexOf(query, 0, StringComparison.InvariantCultureIgnoreCase) >= 0);

            foreach (AnimeSeries ser in series)
            {
                list.Add(new Serie().GenerateFromAnimeSeries(ser, uid));
                if (list.Count >= limit) { break; }
            }
            ob.Add(list);
            return ob;
        }

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

        #region 10. Images

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
        private object GetThumb(int thumb_type, int thumb_id, string ratio = "1.0")
        {
            string contentType;
            System.IO.Stream image = _rest.GetThumb(thumb_type.ToString(), thumb_id.ToString(), ratio, out contentType);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, contentType);
            return response;
        }

        /// <summary>
        /// Return SupportImage (build-in server)
        /// </summary>
        /// <param name="name">image file name</param>
        /// <returns></returns>
        private object GetSupportImage(string name)
        {
            System.IO.Stream image = _impl.GetSupportImage(name);
            Nancy.Response response = new Nancy.Response();
            response = Response.FromStream(image, "image/png");
            return response;
        }

        #endregion

        #region 11. Filters

        /// <summary>
        /// Return Filters as List
        /// </summary>
        /// <returns></returns>
        private object GetFilters()
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            List<GroupFilter> allGfs = RepoFactory.GroupFilter.GetTopLevel().Where(a => a.InvisibleInClients == 0 && ((a.GroupsIds.ContainsKey(user.JMMUserID) && a.GroupsIds[user.JMMUserID].Count > 0) || (a.FilterType & (int)GroupFilterType.Directory) == (int)GroupFilterType.Directory)).ToList();
            List<Filter> filters = new List<Filter>();

            foreach (GroupFilter gf in allGfs)
            {
                Filter filter = new Filter().GenerateFromGroupFilter(gf, user.JMMUserID);
                filters.Add(filter);
            }

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
        /// Return filter with given id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private object GetFilter(int id)
        {
            Request request = this.Request;
            Entities.JMMUser user = (Entities.JMMUser)this.Context.CurrentUser;

            GroupFilter gf = RepoFactory.GroupFilter.GetByID(id);
            Filter filter = new Filter().GenerateFromGroupFilter(gf, user.JMMUserID);

            return filter;
        }

        #endregion

        #region 12. Metadata

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
            Legacy.request = this.Request;
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
            sers.art.fanart.Add(new Art() { url = cseries.AniDBAnime?.AniDBAnime?.DefaultImageFanart?.GenArt(), index = 0 });
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
                        ob.type = "AnimeType";
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
                    Episode ep = new Episode().GenerateFromAnimeEpisode(epi.Key, uid);
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
                    Episode epi = new Episode().GenerateFromAnimeEpisode(e, uid);
                    return epi;
                }
                catch (Exception ex)
                {
                    return APIStatus.internalError(ex.Message.ToString());
                }
            }
            return APIStatus.notFound404("Ep id not found");
        }

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
                        fr.type = "show";
                        fr.url = APIHelper.ConstructGroupIdUrl(gid.ToString());

                        fr.art.fanart.Add(new Art() { url = Helper.GetRandomFanartFromVideo(v) ?? v.Art, index = 0 });
                        fr.art.banner.Add(new Art() { url = v.Banner = Helper.GetRandomBannerFromVideo(v) ?? v.Banner, index = 0 });

                        obl.list.Add(fr);                 
                    }
                }
                foreach (AnimeSeries ser in seriesList)
                {
                    Serie seri = new Serie().GenerateFromAnimeSeries(ser, uid);
                    obl.list.Add(seri);
                }
            }

            return obl;
        }

        private object GetFromFile(int uid, string vl_Id)
        {
            int id;
            if (!int.TryParse(vl_Id, out id)) { return APIStatus.badRequest("bad group id"); }

            VideoLocal vi = RepoFactory.VideoLocal.GetByID(id);

            RawFile rf = new RawFile(vi);

            return rf;
        }
        #endregion

        #endregion

    }
}
