using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AniDBAPI;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using NHibernate.Util;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.API.v2.Models.common;
using Shoko.Server.API.v2.Models.core;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using System.Threading.Tasks;
using Shoko.Commons.Utils;
using Shoko.Server.Databases;

namespace Shoko.Server.API.v2.Modules
{
    //As responds for this API we throw object that will be converted to json/xml
    public class Common : Nancy.NancyModule
    {
        //class will be found automagicly thanks to inherits also class need to be public (or it will 404)

        public static int version = 2;

        public Common() : base("/api")
        {
            // its a setting per module, so every call made to this module requires apikey
            this.RequiresAuthentication();

            #region 1. import folders

            Get["/folder/list", true] = async (x,ct) => await Task.Factory.StartNew(GetFolders, ct);
            Get["/folder/count", true] = async (x,ct) => await Task.Factory.StartNew(CountFolders, ct);
            Post["/folder/add", true] = async (x,ct) => await Task.Factory.StartNew(AddFolder, ct);
            Post["/folder/edit", true] = async (x,ct) => await Task.Factory.StartNew(EditFolder, ct);
            Post["/folder/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteFolder, ct);
            Get["/folder/import", true] = async (x,ct) => await Task.Factory.StartNew(RunImport, ct);
            Get["/folder/scan", true] = async (x, ct) => await Task.Factory.StartNew(ScanDropFolders, ct);

            #endregion

            #region 2. upnp

            Post["/upnp/list", true] = async (x, ct) => await Task.Factory.StartNew(ListUPNP, ct);
            Post["/upnp/add", true] = async (x, ct) => await Task.Factory.StartNew(AddUPNP, ct);
            Post["/upnp/delete", true] = async (x, ct) => await Task.Factory.StartNew(DeleteUPNP, ct);

            #endregion

            #region 3. Actions

            Get["/remove_missing_files", true] = async (x,ct) => await Task.Factory.StartNew(RemoveMissingFiles, ct);
            Get["/stats_update", true] = async (x,ct) => await Task.Factory.StartNew(UpdateStats, ct);
            Get["/mediainfo_update", true] = async (x,ct) => await Task.Factory.StartNew(UpdateMediaInfo, ct);
            Get["/hash/sync", true] = async (x,ct) => await Task.Factory.StartNew(HashSync, ct);
            Get["/rescan", true] = async (x,ct) => await Task.Factory.StartNew(RescanVideoLocal, ct);
            Get["/rehash", true] = async (x,ct) => await Task.Factory.StartNew(RehashVideoLocal, ct);

            #endregion

            #region 4. Misc

            Get["/myid/get", true] = async (x,ct) => await Task.Factory.StartNew(MyID, ct);
            Get["/news/get", true] = async (x,ct) => await Task.Factory.StartNew(() => GetNews(5), ct);
            Get["/dashboard", true] = async (x,ct) => await Task.Factory.StartNew(GetDashboard, ct);
            Get["/search", true] = async (x,ct) => await Task.Factory.StartNew(BigSearch, ct);

            #endregion

            #region 5. Queue

            Get["/queue/get", true] = async (x,ct) => await Task.Factory.StartNew(GetQueue, ct);
            Get["/queue/pause", true] = async (x,ct) => await Task.Factory.StartNew(PauseQueue, ct);
            Get["/queue/start", true] = async (x,ct) => await Task.Factory.StartNew(StartQueue, ct);
            Get["/queue/hash/get", true] = async (x,ct) => await Task.Factory.StartNew(GetHasherQueue, ct);
            Get["/queue/hash/pause", true] = async (x,ct) => await Task.Factory.StartNew(PauseHasherQueue, ct);
            Get["/queue/hash/start", true] = async (x,ct) => await Task.Factory.StartNew(StartHasherQueue, ct);
            Get["/queue/hash/clear", true] = async (x,ct) => await Task.Factory.StartNew(ClearHasherQueue, ct);
            Get["/queue/general/get", true] = async (x,ct) => await Task.Factory.StartNew(GetGeneralQueue, ct);
            Get["/queue/general/pause", true] = async (x,ct) => await Task.Factory.StartNew(PauseGeneralQueue, ct);
            Get["/queue/general/start", true] = async (x,ct) => await Task.Factory.StartNew(StartGeneralQueue, ct);
            Get["/queue/general/clear", true] = async (x,ct) => await Task.Factory.StartNew(ClearGeneralQueue, ct);
            Get["/queue/images/get", true] = async (x,ct) => await Task.Factory.StartNew(GetImagesQueue, ct);
            Get["/queue/images/pause", true] = async (x,ct) => await Task.Factory.StartNew(PauseImagesQueue, ct);
            Get["/queue/images/start", true] = async (x,ct) => await Task.Factory.StartNew(StartImagesQueue, ct);
            Get["/queue/images/clear", true] = async (x,ct) => await Task.Factory.StartNew(ClearImagesQueue, ct);

            #endregion

            #region 6. Files

            Get["/file", true] = async (x,ct) => await Task.Factory.StartNew(GetFile, ct);
            Get["/file/count", true] = async (x,ct) => await Task.Factory.StartNew(CountFiles, ct);
            Get["/file/recent", true] = async (x,ct) => await Task.Factory.StartNew(() => GetRecentFiles(0,0), ct);
            Get["/file/unsort", true] = async (x,ct) => await Task.Factory.StartNew(GetUnsort, ct);
            Get["/file/multiples", true] = async (x,ct) => await Task.Factory.StartNew(GetMultipleFiles, ct);
            Post["/file/offset", true] = async (x,ct) => await Task.Factory.StartNew(SetFileOffset, ct);

            #endregion

            #region 7. Episodes

            Get["/ep", true] = async (x,ct) => await Task.Factory.StartNew(GetEpisode, ct);
            Get["/ep/recent", true] = async (x,ct) => await Task.Factory.StartNew(GetRecentEpisodes, ct);
            Get["/ep/watch", true] = async (x,ct) => await Task.Factory.StartNew(MarkEpisodeAsWatched, ct);
            Get["/ep/unwatch", true] = async (x,ct) => await Task.Factory.StartNew(MarkEpisodeAsUnwatched, ct);
            Get["/ep/vote", true] = async (x,ct) => await Task.Factory.StartNew(VoteOnEpisode, ct);
            Get["/ep/unsort", true] = async (x,ct) => await Task.Factory.StartNew(GetUnsort, ct);
            Get["/ep/scrobble", true] = async (x,ct) => await Task.Factory.StartNew(EpisodeScrobble, ct);
            Get["/ep/getbyfilename", true] = async (x,ct) => await Task.Factory.StartNew(GetEpisodeFromName, ct);

            #endregion

            #region 8. Series

            Get["/serie", true] = async (x,ct) => await Task.Factory.StartNew(GetSerie, ct);
            Get["/serie/count", true] = async (x,ct) => await Task.Factory.StartNew(CountSerie, ct);
            Get["/serie/recent", true] = async (x,ct) => await Task.Factory.StartNew(GetSeriesRecent, ct);
            Get["/serie/search", true] = async (x,ct) => await Task.Factory.StartNew(SearchForSerie, ct);
            Get["/serie/tag", true] = async (x,ct) => await Task.Factory.StartNew(SearchForTag, ct);
            Get["/serie/byfolder", true] = async (x,ct) => await Task.Factory.StartNew(GetSeriesByFolderId, ct);
            Get["/serie/infobyfolder", true] = async (x,ct) => await Task.Factory.StartNew(GetSeriesInfoByFolderId, ct);
            Get["/serie/watch", true] = async (x,ct) => await Task.Factory.StartNew(MarkSerieAsWatched, ct);
            Get["/serie/unwatch", true] = async (x,ct) => await Task.Factory.StartNew(MarkSerieAsUnwatched, ct);
            Get["/serie/vote", true] = async (x,ct) => await Task.Factory.StartNew(VoteOnSerie, ct);
            Get["/serie/fromep", true] = async (x,ct) => await Task.Factory.StartNew(GetSeriesFromEpisode, ct);
            Get["/serie/startswith", true] = async (x,ct) => await Task.Factory.StartNew(SearchStartsWith, ct);
            Get["/serie/today", true] = async (x,ct) => await Task.Factory.StartNew(SeriesToday, ct);

            #endregion

            #region 9. Cloud accounts

            Get["/cloud/list", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccounts, ct);
            Get["/cloud/count", true] = async (x,ct) => await Task.Factory.StartNew(GetCloudAccountsCount, ct);
            Post["/cloud/add", true] = async (x,ct) => await Task.Factory.StartNew(AddCloudAccount, ct);
            Post["/cloud/delete", true] = async (x,ct) => await Task.Factory.StartNew(DeleteCloudAccount, ct);
            Get["/cloud/import", true] = async (x,ct) => await Task.Factory.StartNew(RunCloudImport, ct);

            #endregion

            #region 10. Filters

            Get["/filter", true] = async (x,ct) => await Task.Factory.StartNew(GetFilters, ct);

            #endregion

            #region 11. Groups

            Get["/group", true] = async (x,ct) => await Task.Factory.StartNew(GetGroups, ct);
            Get["/group/watch", true] = async (x,ct) => await Task.Factory.StartNew(MarkGroupAsWatched, ct);
            Get["/group/unwatch", true] = async (x,ct) => await Task.Factory.StartNew(MarkGroupAsUnwatched, ct);

            #endregion

            Get["/links/serie", true] = async (x, ct) => await Task.Factory.StartNew(GetLinks, ct);
        }

        #region 1.Import Folders

        /// <summary>
        /// Handle /api/folder/list
        /// List all saved Import Folders
        /// </summary>
        /// <returns>List<Contract_ImportFolder></returns>
        private object GetFolders()
        {
            List<ImportFolder> list = new ShokoServiceImplementation().GetImportFolders();
            return list;
        }

        /// <summary>
        /// Handle /api/folder/count
        /// </summary>
        /// <returns>Counter</returns>
        private object CountFolders()
        {
            Counter count = new Counter
            {
                count = new ShokoServiceImplementation().GetImportFolders().Count
            };
            return count;
        }

        /// <summary>
        /// Handle /api/folder/add
        /// Add Folder to Import Folders Repository
        /// </summary>
        /// <returns>APIStatus</returns>
        private object AddFolder()
        {
            try
            {
                ImportFolder folder = this.Bind();
                if (folder.ImportFolderLocation != string.Empty)
                {
                    try
                    {
                        CL_Response<ImportFolder> response = new ShokoServiceImplementation().SaveImportFolder(folder);

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
        /// Handle /api/folder/edit
        /// Edit folder giving fulll ImportFolder object with ID
        /// </summary>
        /// <returns>APIStatus</returns>
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
                        if (folder.ImportFolderID != 0)
                        {
                            CL_Response<ImportFolder> response =
                                new ShokoServiceImplementation().SaveImportFolder(folder);
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
        /// Handle /api/folder/delete
        /// Delete Import Folder out of Import Folder Repository
        /// </summary>
        /// <returns>APIStatus</returns>
        private object DeleteFolder()
        {
            ImportFolder folder = this.Bind();
            if (folder.ImportFolderID != 0)
            {
                string res = Importer.DeleteImportFolder(folder.ImportFolderID);
                if (res == string.Empty)
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
        /// Handle /api/folder/import
        /// Run Import action on all Import Folders inside Import Folders Repository
        /// </summary>
        /// <returns>APIStatus</returns>
        private object RunImport()
        {
            ShokoServer.RunImport();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Handle /api/folder/scan
        /// Scan All Drop Folders
        /// </summary>
        /// <returns>APIStatus</returns>
        private object ScanDropFolders()
        {
            Importer.RunImport_DropFolders();
            return APIStatus.statusOK();
        }

        #endregion

        #region 2.UPNP

        private object ListUPNP()
        {
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
        /// Handle /api/remove_missing_files
        /// Scans your import folders and remove files from your database that are no longer in your collection.
        /// </summary>
        /// <returns>APIStatus</returns>
        private object RemoveMissingFiles()
        {
            ShokoServer.RemoveMissingFiles();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Handle /api/stats_update
        /// Updates all series stats such as watched state and missing files.
        /// </summary>
        /// <returns>APIStatus</returns>
        private object UpdateStats()
        {
            Importer.UpdateAllStats();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Handle /api/mediainfo_update
        /// Updates all technical details about the files in your collection via running MediaInfo on them.
        /// </summary>
        /// <returns>APIStatus</returns>
        private object UpdateMediaInfo()
        {
            ShokoServer.RefreshAllMediaInfo();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Handle /api/hash/sync
        /// Sync Hashes - download/upload hashes from/to webcache
        /// </summary>
        /// <returns>APIStatus</returns>
        private object HashSync()
        {
            ShokoServer.SyncHashes();
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Handle /api/rescan
        /// Rescan ImportFolder (with given id) to recognize new episodes
        /// </summary>
        /// <returns>APIStatus</returns>
        private object RescanVideoLocal()
        {
            Request request = this.Request;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                try
                {
                    SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(para.id);
                    if (vid == null)
                    {
                        return APIStatus.notFound404();
                    }
                    if (string.IsNullOrEmpty(vid.Hash))
                    {
                        return APIStatus.badRequest(
                            "Could not Update a cloud file without hash, hash it locally first");
                    }
                    Commands.CommandRequest_ProcessFile cmd =
                        new Commands.CommandRequest_ProcessFile(vid.VideoLocalID, true);
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
        /// Handle /api/rehash
        /// Rehash given files in given VideoLocal
        /// </summary>
        /// <returns>APIStatus</returns>
        private object RehashVideoLocal()
        {
            Request request = this.Request;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByID(para.id);
                if (vl != null)
                {
                    SVR_VideoLocal_Place pl = vl.GetBestVideoLocalPlace();
                    if (pl?.FullServerPath == null)
                    {
                        return APIStatus.notFound404("videolocal_place not found");
                    }
                    Commands.CommandRequest_HashFile cr_hashfile =
                        new Commands.CommandRequest_HashFile(pl.FullServerPath, true);
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
        /// <returns>List<WebNews></returns>
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
                WebNews wn = new WebNews
                {
                    author = post.author,
                    date = post.date,
                    link = post.link,
                    title = System.Web.HttpUtility.HtmlDecode((string)post.title.rendered),
                    description = post.excerpt.rendered
                };
                news.Add(wn);
                if (limit >= max) break;
            }
            return news;
        }

        /// <summary>
        /// Return Dictionary with nesesery items for Dashboard inside Webui
        /// </summary>
        /// <returns>Dictionary<string, object></returns>
        private object GetDashboard()
        {
            Dictionary<string, object> dash = new Dictionary<string, object>
            {
                {"queue", GetQueue()},
                {"file", GetRecentFiles(0, 1)},
                {"folder", GetFolders()},
                {"file_count", CountFiles()},
                {"serie_count", CountSerie()}
            };
            return dash;
        }

        /// <summary>
        /// Handle /api/search
        /// </summary>
        /// <returns>Filter or APIStatu</returns>
        private object BigSearch()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            string query = para.query.ToLowerInvariant();
            if (para.limit == 0)
            {
                //hardcoded
                para.limit = 100;
            }
            if (query != string.Empty)
            {
                Filter search_filter = new Filter
                {
                    name = "Search",
                    groups = new List<Group>()
                };
                Group search_group = new Group
                {
                    name = para.query,
                    series = new List<Serie>()
                };
                search_group.series = (List<Serie>) (Search(query, para.limit, para.limit_tag, (int) para.offset,
                    para.tags, user.JMMUserID, para.nocast != 0, para.notag != 0, para.level, para.all != 0,
                    para.fuzzy != 0, para.allpics != 0, para.pic, para.tagfilter));
                search_group.size = search_group.series.Count();
                search_filter.groups.Add(search_group);
                search_filter.size = search_filter.groups.Count();

                return search_filter;
            }
            else
            {
                return APIStatus.badRequest("missing 'query'");
            }
        }

        /// <summary>
        /// Handle /api/search
        /// </summary>
        /// <returns>Filter or APIStatu</returns>
        private object SearchStartsWith()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            string query = para.query.ToLowerInvariant();
            if (para.limit == 0)
            {
                //hardcoded
                para.limit = 100;
            }
            if (query != string.Empty)
            {
                Filter search_filter = new Filter
                {
                    name = "Search",
                    groups = new List<Group>()
                };
                Group search_group = new Group
                {
                    name = para.query,
                    series = new List<Serie>()
                };
                search_group.series = (List<Serie>) (StartsWith(query, para.limit, user.JMMUserID, para.nocast != 0,
                    para.notag != 0, para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter));
                search_group.size = search_group.series.Count();
                search_filter.groups.Add(search_group);
                search_filter.size = search_filter.groups.Count();

                return search_filter;
            }

            return APIStatus.badRequest("missing 'query'");
        }

        #endregion

        #region 5.Queue

        /// <summary>
        /// Return current information about Queues (hash, general, images)
        /// </summary>
        /// <returns>Dictionary<string, QueueInfo></returns>
        private object GetQueue()
        {
            Dictionary<string, QueueInfo> queues = new Dictionary<string, QueueInfo>();
            queues.Add("hash", (QueueInfo) GetHasherQueue());
            queues.Add("general", (QueueInfo) GetGeneralQueue());
            queues.Add("image", (QueueInfo) GetImagesQueue());
            return queues;
        }

        /// <summary>
        /// Pause all running Queues
        /// </summary>
        /// <returns>APIStatus</returns>
        private object PauseQueue()
        {
            ShokoService.CmdProcessorHasher.Paused = true;
            ShokoService.CmdProcessorGeneral.Paused = true;
            ShokoService.CmdProcessorImages.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start all queues that are pasued
        /// </summary>
        /// <returns>APIStatus</returns>
        private object StartQueue()
        {
            ShokoService.CmdProcessorHasher.Paused = false;
            ShokoService.CmdProcessorGeneral.Paused = false;
            ShokoService.CmdProcessorImages.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Return information about Hasher queue
        /// </summary>
        /// <returns>QueueInfo</returns>
        private object GetHasherQueue()
        {
            QueueInfo queue = new QueueInfo
            {
                count = ServerInfo.Instance.HasherQueueCount,
                state = ServerInfo.Instance.HasherQueueState,
                isrunning = ServerInfo.Instance.HasherQueueRunning,
                ispause = ServerInfo.Instance.HasherQueuePaused
            };
            return queue;
        }

        /// <summary>
        /// Return information about General queue
        /// </summary>
        /// <returns>QueueInfo</returns>
        private object GetGeneralQueue()
        {
            QueueInfo queue = new QueueInfo
            {
                count = ServerInfo.Instance.GeneralQueueCount,
                state = ServerInfo.Instance.GeneralQueueState,
                isrunning = ServerInfo.Instance.GeneralQueueRunning,
                ispause = ServerInfo.Instance.GeneralQueuePaused
            };
            return queue;
        }

        /// <summary>
        /// Return information about Images queue
        /// </summary>
        /// <returns>QueueInfo</returns>
        private object GetImagesQueue()
        {
            QueueInfo queue = new QueueInfo
            {
                count = ServerInfo.Instance.ImagesQueueCount,
                state = ServerInfo.Instance.ImagesQueueState,
                isrunning = ServerInfo.Instance.ImagesQueueRunning,
                ispause = ServerInfo.Instance.ImagesQueuePaused
            };
            return queue;
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns>APIStatus</returns>
        private object PauseHasherQueue()
        {
            ShokoService.CmdProcessorHasher.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns>APIStatus</returns>
        private object PauseGeneralQueue()
        {
            ShokoService.CmdProcessorGeneral.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Pause Queue
        /// </summary>
        /// <returns>APIStatus</returns>
        private object PauseImagesQueue()
        {
            ShokoService.CmdProcessorImages.Paused = true;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns>APIStatus</returns>
        private object StartHasherQueue()
        {
            ShokoService.CmdProcessorHasher.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns>APIStatus</returns>
        private object StartGeneralQueue()
        {
            ShokoService.CmdProcessorGeneral.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Start Queue from Pause state
        /// </summary>
        /// <returns>APIStatus</returns>
        private object StartImagesQueue()
        {
            ShokoService.CmdProcessorImages.Paused = false;
            return APIStatus.statusOK();
        }

        /// <summary>
        /// Clear Queue and Restart it
        /// </summary>
        /// <returns>APIStatus</returns>
        private object ClearHasherQueue()
        {
            try
            {
                ShokoService.CmdProcessorHasher.Stop();

                while (ShokoService.CmdProcessorHasher.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestHasher());

                ShokoService.CmdProcessorHasher.Init();

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
        /// <returns>APIStatus</returns>
        private object ClearGeneralQueue()
        {
            try
            {
                ShokoService.CmdProcessorGeneral.Stop();

                while (ShokoService.CmdProcessorGeneral.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestGeneral());

                ShokoService.CmdProcessorGeneral.Init();

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
        /// <returns>APIStatus</returns>
        private object ClearImagesQueue()
        {
            try
            {
                ShokoService.CmdProcessorImages.Stop();

                while (ShokoService.CmdProcessorImages.ProcessingCommands)
                {
                    Thread.Sleep(200);
                }
                Thread.Sleep(200);

                RepoFactory.CommandRequest.Delete(RepoFactory.CommandRequest.GetAllCommandRequestImages());

                ShokoService.CmdProcessorImages.Init();

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
        /// Handle /api/file
        /// </summary>
        /// <returns>List<RawFile> or RawFile or APIStatus</returns>
        private object GetFile()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            return para.id == 0 
                ? GetAllFiles(para.limit, para.level, user.JMMUserID) 
                : GetFileById(para.id, para.level, user.JMMUserID);
        }

        /// <summary>
        /// handle /api/file/multiple
        /// </summary>
        /// <returns></returns>
        private object GetMultipleFiles()
        {
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            int userID = user.JMMUserID;
            Dictionary<int,Serie> results = new Dictionary<int, Serie>();
            try
            {
                List<SVR_AnimeEpisode> list = RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(true);
                foreach(SVR_AnimeEpisode ep in list)
                {
                    Serie serie = null;
                    SVR_AnimeSeries series = ep?.GetAnimeSeries();
                    if (series == null) continue;
                    if (results.ContainsKey(series.AnimeSeriesID)) serie = results[series.AnimeSeriesID];
                    if (serie == null)
                        serie =
                            Serie.GenerateFromAnimeSeries(Context, series, userID, para.nocast == 1,
                                para.notag == 1, 0,
                                false, para.allpics != 0, para.pic, para.tagfilter);
                    if (serie.eps == null) serie.eps = new List<Episode>();
                    Episode episode = Episode.GenerateFromAnimeEpisode(Context, ep, userID, 0);
                    List<SVR_VideoLocal> vls = ep.GetVideoLocals();
                    if (vls.Count > 0)
                    {
                        episode.files = new List<RawFile>();
                        vls.Sort(FileQualityFilter.CompareTo);
                        bool first = true;
                        foreach (SVR_VideoLocal vl in vls)
                        {
                            RawFile file = new RawFile(Context, vl, 0, userID);
                            if (first)
                            {
                                file.is_preferred = 1;
                                first = false;
                            }
                            episode.files.Add(file);
                        }
                    }
                    serie.eps.Add(episode);
                    results[series.AnimeSeriesID] = serie;
                }
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
            }
            return results.Values;
        }

        /// <summary>
        /// Handle /api/file/count
        /// </summary>
        /// <returns>Counter</returns>
        private object CountFiles()
        {
            Counter count = new Counter
            {
                count = RepoFactory.VideoLocal.GetAll().Count
            };
            return count;
        }

        /// <summary>
        /// Handle /api/file/recent
        /// </summary>
        /// <returns>List<RawFile></returns>
        private object GetRecentFiles(int limit = 0, int level = 0)
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (limit == 0)
            {
                if (para.limit == 0)
                {
                    para.limit = 10;
                }
            }
            else
            {
                para.limit = limit;
            }
            if (level != 0)
            {
                para.level = level;
            }

            List<RawFile> list = new List<RawFile>();
            foreach (SVR_VideoLocal file in RepoFactory.VideoLocal.GetMostRecentlyAdded(para.limit))
            {
                list.Add(new RawFile(Context, file, para.level, user.JMMUserID));
            }

            return list;
        }

        /// <summary>
        /// Handle /api/file/unsort
        /// </summary>
        /// <returns>List<RawFile></returns>
        private List<RawFile> GetUnsort()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            List<RawFile> lst = new List<RawFile>();

            List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (SVR_VideoLocal vl in vids)
            {
                if (para.offset == 0)
                {
                    RawFile v = new RawFile(Context, vl, para.level, user.JMMUserID);
                    lst.Add(v);
                    if (para.limit != 0)
                    {
                        if (lst.Count >= para.limit)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    para.offset -= 1;
                }
            }

            return lst;
        }

        /// <summary>
        /// Handle /api/file/offset
        /// </summary>
        /// <returns>APIStatus</returns>
        private object SetFileOffset()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            // allow to offset be 0 to reset position
            if (para.id == 0 || para.offset < 0)
            {
                return APIStatus.badRequest("Invalid arguments");
            }
            SVR_VideoLocal vlu = RepoFactory.VideoLocal.GetByID(para.id);
            if (vlu != null)
            {
                vlu.SetResumePosition(para.offset, user.JMMUserID);
                return APIStatus.statusOK();
            }
            return APIStatus.notFound404();
        }

        #region internal function

        /// <summary>
        /// Internal function returning file with given id
        /// </summary>
        /// <param name="file_id">file id</param>
        /// <param name="level">deep level</param>
        /// <param name="uid">user id</param>
        /// <returns>RawFile or APIStatus</returns>
        internal object GetFileById(int file_id, int level, int uid)
        {
            SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByID(file_id);
            if (vl != null)
            {
                RawFile rawfile = new RawFile(Context, vl, level, uid);
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
            if (limit == 0)
            {
                limit_x = 100;
            }
            foreach (SVR_VideoLocal file in RepoFactory.VideoLocal.GetAll(limit_x))
            {
                list.Add(new RawFile(Context, file, level, uid));
                if (limit != 0)
                {
                    if (list.Count >= limit)
                    {
                        break;
                    }
                }
            }

            return list;
        }

        #endregion

        #endregion

        #region 7.Episodes

        /// <summary>
        /// Handle /api/ep
        /// </summary>
        /// <returns>List<Episode> or Episode</returns>
        private object GetEpisode()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllEpisodes(user.JMMUserID, para.limit, (int) para.offset, para.level, para.all != 0);
            }
            else
            {
                return GetEpisodeById(para.id, user.JMMUserID, para.level);
            }
        }

        /// <summary>
        /// Handle /api/ep/getbyfilename?filename=...
        /// </summary>
        /// <returns>Episode or APIStatis</returns>
        private object GetEpisodeFromName()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (String.IsNullOrEmpty(para.filename)) return APIStatus.badRequest("missing 'filename'");

            SVR_AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByFilename(para.filename);
            if (aep != null)
            {
                return Episode.GenerateFromAnimeEpisode(Context, aep, user.JMMUserID, 0);
            }
            else
            {
                return APIStatus.notFound404();
            }
        }

        /// <summary>
        /// Handle /api/ep/recent
        /// </summary>
        /// <returns>List<Episode></returns>
        private object GetRecentEpisodes()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0)
            {
                //hardcoded
                para.limit = 10;
            }
            List<object> lst = new List<object>();

            List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetMostRecentlyAdded(para.limit);

            foreach (SVR_VideoLocal vl in vids)
            {
                foreach (SVR_AnimeEpisode aep in vl.GetAnimeEpisodes())
                {
                    Episode ep = Episode.GenerateFromAnimeEpisode(Context, aep, user.JMMUserID, para.level);
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
        /// <returns>APIStatus</returns>
        private object EpisodeScrobble()
        {
            try
            {
                Request request = this.Request;
                API_Call_Parameters para = this.Bind();

                // statys 1-start, 2-pause, 3-stop
                // progres 0-100
                // type 1-movie, 2-episode
                if (para.id > 0 & para.progress >= 0 & para.status > 0)
                {
                    ShokoServiceImplementation impl = new ShokoServiceImplementation();
                    int type = 2;
                    if (para.ismovie == 0)
                    {
                        type = 2;
                    }
                    else
                    {
                        type = 1;
                    }
                    switch (impl.TraktScrobble(para.id, type, para.progress, para.status))
                    {
                        case 200:
                            return APIStatus.statusOK();
                        case 404:
                            return APIStatus.notFound404();
                        default:
                            return APIStatus.internalError();
                    }
                }
                else
                {
                    return APIStatus.badRequest();
                }
            }
            catch
            {
                return APIStatus.internalError();
            }
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
                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(id);
                if (ep == null)
                {
                    return APIStatus.notFound404();
                }
                ep.ToggleWatchedStatus(status, true, DateTime.Now, false, false, uid, true);
                ep.GetAnimeSeries()?.UpdateStats(true, false, true);
                return APIStatus.statusOK();
            }
            catch (Exception ex)
            {
                return APIStatus.internalError(ex.Message);
            }
        }

        /// <summary>
        /// Return All known Episodes for current user
        /// </summary>
        /// <returns>List<Episode></returns>
        internal object GetAllEpisodes(int uid, int limit, int offset, int level, bool all)
        {
            List<Episode> eps = new List<Episode>();
            List<int> aepul = RepoFactory.AnimeEpisode_User.GetByUserID(uid).Select(a => a.AnimeEpisodeID).ToList();
            if (limit == 0)
            {
                // hardcoded
                limit = 100;
            }

            foreach (int id in aepul)
            {
                if (offset == 0)
                {
                    eps.Add(Episode.GenerateFromAnimeEpisodeID(Context, id, uid, level));
                    if (limit != 0)
                    {
                        if (eps.Count >= limit)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    offset -= 1;
                }
            }

            return eps;
        }

        /// <summary>
        /// Internal function returning episode
        /// </summary>
        /// <param name="id">episode id</param>
        /// <param name="uid">user id</param>
        /// <returns>Episode or APIStatus</returns>
        internal object GetEpisodeById(int id, int uid, int level)
        {
            if (id > 0)
            {
                SVR_AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(id);
                if (aep != null)
                {
                    Episode ep = Episode.GenerateFromAnimeEpisode(Context, aep, uid, level);
                    if (ep != null)
                    {
                        return ep;
                    }
                    else
                    {
                        return APIStatus.notFound404("episode not found");
                    }
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
                    AniDB_Vote thisVote = RepoFactory.AniDB_Vote.GetByEntityAndType(id, AniDBVoteType.Episode);

                    if (thisVote == null)
                    {
                        thisVote = new AniDB_Vote
                        {
                            VoteType = (int)AniDBVoteType.Episode,
                            EntityID = id
                        };
                    }

                    if (score <= 10)
                    {
                        score = score * 100;
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
        /// Handle /api/serie
        /// </summary>
        /// <returns>List<Serie> or Serie</returns>
        private object GetSerie()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllSeries(para.nocast != 0, para.limit, (int) para.offset, para.notag != 0, para.level,
                    para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return GetSerieById(para.id, para.nocast != 0, para.notag != 0, para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
        }

        /// <summary>
        /// Handle /api/serie/count
        /// </summary>
        /// <returns>Counter</returns>
        private object CountSerie()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            Counter count = new Counter
            {
                count = RepoFactory.AnimeSeries.GetAll().Count
            };
            return count;
        }

        /// <summary>
        /// Handle /api/serie/today
        /// </summary>
        /// <returns>List<Serie> or Serie</returns>
        private object SeriesToday()
        {
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            // 1. get series airing
            // 2. get eps for those series
            // 3. calculate which series have most of the files released today
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll().AsParallel()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(user.GetHideCategories()));
            DateTime now = DateTime.Now;
            List<Serie> result = allSeries.Where(ser =>
            {
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(ser.AniDB_ID);
                // It might end today, but that's okay
                if (anime.EndDate != null)
                {
                    if (now > anime.EndDate.Value && now - anime.EndDate.Value > new TimeSpan(16, 0, 0)) return false;
                }
                if (ser.AirsOn == null) return false;
                return DateTime.Now.DayOfWeek == ser.AirsOn.Value;
            }).Select(ser => Serie.GenerateFromAnimeSeries(Context, ser, user.JMMUserID, para.nocast == 1,
                para.notag == 1, para.level, para.all == 1, para.allpics == 1, para.pic, para.tagfilter)).OrderBy(a => a.name).ToList();
            Group group = new Group()
            {
                id = 0,
                name = "Airing Today",
                series = result,
                size = result.Count,
                summary = "Based on AniDB Episode Air Dates. Incorrect info falls on AniDB to be corrected.",
                url = Request.Url,
            };
            return group;
        }

        /// <summary>
        /// Handle /api/serie/byfolder
        /// </summary>
        /// <returns>List<Serie> or APIStatus</returns>
        private object GetSeriesByFolderId()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                return GetSeriesByFolder(para.id, user.JMMUserID, para.nocast != 0, para.notag != 0, para.level,
                    para.all != 0, para.limit, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return APIStatus.internalError("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/serie/infobyfolder
        /// </summary>
        /// <returns>List<ObjectList> or APIStatus</returns>
        private object GetSeriesInfoByFolderId()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id != 0)
            {
                return GetSeriesInfoByFolder(para.id, user.JMMUserID, para.limit, para.tagfilter);
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            List<object> allseries = new List<object>();

            if (para.limit == 0)
            {
                para.limit = 10;
            }
            List<SVR_AnimeSeries> series = RepoFactory.AnimeSeries.GetMostRecentlyAdded(para.limit);

            foreach (SVR_AnimeSeries aser in series)
            {
                allseries.Add(Serie.GenerateFromAnimeSeries(Context, aser, user.JMMUserID, para.nocast != 0, para.notag != 0,
                    para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter));
            }

            return allseries;
        }

        /// <summary>
        /// Handle /api/serie/watch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkSerieAsWatched()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
        /// Handle /api/serie/unwatch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkSerieAsUnwatched()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
            JMMUser user = (JMMUser) this.Context.CurrentUser;
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
        /// <returns>List<Serie> or APIStatus</returns>
        private object SearchForSerie()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0)
            {
                //hardcoded
                para.limit = 100;
            }
            if (para.query != string.Empty)
            {
                return Search(para.query, para.limit, para.limit_tag, (int) para.offset, para.tags, user.JMMUserID,
                    para.nocast != 0, para.notag != 0, para.level, para.all != 0, para.fuzzy != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return APIStatus.badRequest("missing 'query'");
            }
        }

        /// <summary>
        /// Handle /api/serie/tag
        /// </summary>
        /// <returns>List<Serie> or APIStatus</returns>
        private object SearchForTag()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.limit == 0)
            {
                //hardcoded
                para.limit = 100;
            }
            if (para.query != string.Empty)
            {
                return Search(para.query, para.limit, para.limit_tag, (int) para.offset, 1, user.JMMUserID,
                    para.nocast != 0,
                    para.notag != 0, para.level, para.all != 0, para.fuzzy != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return APIStatus.badRequest("missing 'query'");
            }
        }

        /// <summary>
        /// Handle /api/serie/fromep?id=...
        /// Used to get the series related to the episode id.
        /// </summary>
        /// <returns>Serie or APIStatus</returns>
        private object GetSeriesFromEpisode()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return GetSerieFromEpisode(para.id, user.JMMUserID, para.nocast != 0, para.notag != 0, para.level,
                    para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        #region internal function

        /// <summary>
        /// Return Series that resine inside folder
        /// </summary>
        /// <param name="id">import folder id</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <param name="all"></param>
        /// <param name="limit"></param>
        /// <returns>List<Serie></returns>
        internal object GetSeriesByFolder(int id, int uid, bool nocast, bool notag, int level, bool all, int limit, bool allpic, int pic, byte tagfilter)
        {
            List<object> allseries = new List<object>();
            List<SVR_VideoLocal> vlpall = RepoFactory.VideoLocalPlace.GetByImportFolder(id)
                .Select(a => a.VideoLocal)
                .ToList();

            if (limit == 0)
            {
                // hardcoded limit
                limit = 100;
            }
            foreach (SVR_VideoLocal vl in vlpall)
            {
                Serie ser = Serie.GenerateFromVideoLocal(Context, vl, uid, nocast, notag, level, all, allpic, pic, tagfilter);
                allseries.Add(ser);
                if (allseries.Count >= limit)
                {
                    break;
                }
            }

            return allseries;
        }

        /// <summary>
        /// Return SeriesInfo inside ObjectList that resine inside folder
        /// </summary>
        /// <param name="id">import folder id</param>
        /// <param name="uid">user id</param>
        /// <param name="limit"></param>
        /// <returns>List<ObjectList></returns>
        internal object GetSeriesInfoByFolder(int id, int uid, int limit, byte tagfilter)
        {
            Dictionary<string, long> tmp_list = new Dictionary<string, long>();
            List<object> allseries = new List<object>();
            List<SVR_VideoLocal> vlpall = RepoFactory.VideoLocalPlace.GetByImportFolder(id)
                .Select(a => a.VideoLocal)
                .ToList();

            if (limit == 0)
            {
                // hardcoded limit
                limit = 100;
            }

            foreach (SVR_VideoLocal vl in vlpall)
            {
                Serie ser = Serie.GenerateFromVideoLocal(Context, vl, uid, true, true, 2, false, false, 0, tagfilter);

                ObjectList objl = new ObjectList(ser.name, ObjectList.ListType.SERIE, ser.filesize);
                if (ser.name != null)
                {
                    if (!tmp_list.ContainsKey(ser.name))
                    {
                        tmp_list.Add(ser.name, ser.filesize);
                        allseries.Add(objl);
                    }
                    else
                    {
                        if (tmp_list[ser.name] != ser.filesize)
                        {
                            while (tmp_list.ContainsKey(objl.name))
                            {
                                objl.name = objl.name + "*";
                            }
                            tmp_list.Add(objl.name, ser.filesize);
                            allseries.Add(objl);
                        }
                    }
                }
                
                if (allseries.Count >= limit)
                {
                    break;
                }
            }

            return allseries;
        }

        /// <summary>
        /// Return Serie for given episode
        /// </summary>
        /// <param name="id">episode id</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <param name="all"></param>
        /// <returns></returns>
        internal object GetSerieFromEpisode(int id, int uid, bool nocast, bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            SVR_AnimeEpisode aep = RepoFactory.AnimeEpisode.GetByID(id);
            if (aep != null)
            {
                return Serie.GenerateFromAnimeSeries(Context, aep.GetAnimeSeries(), uid, nocast, notag, level, all, allpic, pic, tagfilter);
            }
            else
            {
                return APIStatus.notFound404("serie not found");
            }
        }

        /// <summary>
        /// Return All known Series
        /// </summary>
        /// <param name="nocast">disable cast</param>
        /// <param name="limit">number of return items</param>
        /// <param name="offset">offset to start from</param>
        /// <returns>List<Serie></returns>
        internal object GetAllSeries(bool nocast, int limit, int offset, bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;

            List<Serie> allseries = new List<Serie>();

            foreach (SVR_AnimeSeries asi in RepoFactory.AnimeSeries.GetAll())
            {
                if (offset <= 0)
                {
                    allseries.Add(Serie.GenerateFromAnimeSeries(Context, asi, user.JMMUserID, nocast, notag, level, all, allpic, pic, tagfilter));
                    if (limit != 0)
                    {
                        if (allseries.Count >= limit)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    offset -= 1;
                }
            }

            return allseries;
        }

        /// <summary>
        /// Internal function returning serie with given ID
        /// </summary>
        /// <param name="series_id">serie id</param>
        /// <param name="nocast">disable cast</param>
        /// <returns></returns>
        internal object GetSerieById(int series_id, bool nocast, bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            Serie ser = Serie.GenerateFromAnimeSeries(Context, RepoFactory.AnimeSeries.GetByID(series_id), user.JMMUserID,
                nocast, notag, level, all, allpic, pic, tagfilter);
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
                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(id);
                if (ser == null) return APIStatus.badRequest("Series not Found");

                foreach (SVR_AnimeEpisode ep in ser.GetAnimeEpisodes())
                {
                    SVR_AnimeEpisode_User epUser = ep.GetUserRecord(uid);
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

                ser.UpdateStats(true, true, true);

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

            List<string> newItems = values.Select(s => SanitizeFuzzy(s, replaceinvalid)).ToList();

            return string.Join(seperator, newItems);
        }

        private static readonly char[] InvalidPathChars =
            $"{new string(Path.GetInvalidFileNameChars())}{new string(Path.GetInvalidPathChars())}()+".ToCharArray();

        private static readonly char[] ReplaceWithSpace = @"[-.]".ToCharArray();

        internal static string SanitizeFuzzy(string value, bool replaceInvalid)
        {
            if (!replaceInvalid) return value;

            value = value.FilterCharacters(InvalidPathChars, true);
            value = ReplaceWithSpace.Aggregate(value, (current, c) => current.Replace(c, ' '));

            return value.CompactWhitespaces();
        }

        /// <summary>
        /// function used in fuzzy search
        /// </summary>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        private static void CheckTitlesFuzzy(SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return;
            int dist = int.MaxValue;
            string match = string.Empty;
            foreach (string title in a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title).ToList())
            {
                if (string.IsNullOrEmpty(title)) continue;
                int k = Math.Max(Math.Min((int)(title.Length / 6D), (int)(query.Length / 6D)), 1);
                if (query.Length <= 4 || title.Length <= 4) k = 0;
                if (Shoko.Commons.Utils.Misc.BitapFuzzySearch(title, query, k, out int newDist) == -1) continue;
                if (newDist < dist)
                {
                    match = title;
                    dist = newDist;
                }
            }
            // Keep the lowest distance
            if (dist < int.MaxValue)
                distLevenshtein.AddOrUpdate(a, new Tuple<int, string>(dist, match),
                    (key, oldValue) => Math.Min(oldValue.Item1, dist) == dist
                        ? new Tuple<int, string>(dist, match)
                        : oldValue);
        }

        /// <summary>
        /// function used in fuzzy tag search
        /// </summary>
        /// <param name="a"></param>
        /// <param name="query"></param>
        /// <param name="distLevenshtein"></param>
        /// <param name="limit"></param>
        private static void CheckTagsFuzzy(SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>> distLevenshtein, int limit)
        {
            if (distLevenshtein.Count >= limit) return;
            int dist = int.MaxValue;
            string match = string.Empty;
            if (a?.Contract?.AniDBAnime?.Tags != null &&
                a.Contract.AniDBAnime.Tags.Count > 0)
            {
                foreach (string tag in a.Contract.AniDBAnime.Tags.Select(b => b.TagName).ToList())
                {
                    if (string.IsNullOrEmpty(tag)) continue;
                    int k = Math.Min((int)(tag.Length / 6D), (int)(query.Length / 6D));
                    if (Shoko.Commons.Utils.Misc.BitapFuzzySearch(tag, query, k, out int newDist) == -1) continue;
                    if (newDist < dist)
                    {
                        match = tag;
                        dist = newDist;
                    }
                }
                if (dist < int.MaxValue)
                    distLevenshtein.AddOrUpdate(a, new Tuple<int, string>(dist, match),
                        (key, oldValue) => Math.Min(oldValue.Item1, dist) == dist
                            ? new Tuple<int, string>(dist, match)
                            : oldValue);
            }

            if (distLevenshtein.Count >= limit || a?.Contract?.AniDBAnime?.CustomTags == null ||
                a.Contract.AniDBAnime.CustomTags.Count <= 0) return;

            dist = int.MaxValue;
            match = string.Empty;
            foreach (string customTag in a.Contract.AniDBAnime.CustomTags.Select(b => b.TagName).ToList())
            {
                if (string.IsNullOrEmpty(customTag)) continue;
                int k = Math.Min((int)(customTag.Length / 6D), (int)(query.Length / 6D));
                if (Shoko.Commons.Utils.Misc.BitapFuzzySearch(customTag, query, k, out int newDist) == -1) continue;
                if (newDist < dist)
                {
                    match = customTag;
                    dist = newDist;
                }
            }
            if (dist < int.MaxValue)
                distLevenshtein.AddOrUpdate(a, new Tuple<int, string>(dist, match),
                    (key, oldValue) => Math.Min(oldValue.Item1, dist) == dist
                        ? new Tuple<int, string>(dist, match)
                        : oldValue);
        }

        /// <summary>
        /// Search for serie with given query in name or tag
        /// </summary>
        /// <param name="query">target string</param>
        /// <param name="limit">number of return items</param>
        /// <param name="limit_tag">number of return items for tag</param>
        /// <param name="offset">offset to start from</param>
        /// <param name="tagSearch">True for searching in tags, False for searching in name</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <param name="all"></param>
        /// <param name="fuzzy">Disable searching for invalid path characters</param>
        /// <returns>List<Serie></returns>
        internal object Search(string query, int limit, int limit_tag, int offset, int tagSearch, int uid, bool nocast,
            bool notag, int level, bool all, bool fuzzy, bool allpic, int pic, byte tagfilter)
        {
            query = query.ToLowerInvariant();

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(uid);
            if (user == null) return APIStatus.unauthorized();

            List<Serie> series_list = new List<Serie>();
            Dictionary<SVR_AnimeSeries, string> series = new Dictionary<SVR_AnimeSeries, string>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(user.GetHideCategories()))
                .AsParallel();

            #region Search_TitlesOnly

            switch (tagSearch)
            {
                case 0:
                    if (!fuzzy || query.Length >= (IntPtr.Size * 8))
                    {
                        series = allSeries
                            .Where(a => Join(",", a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title), fuzzy)
                                            .IndexOf(SanitizeFuzzy(query, fuzzy), 0,
                                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            .OrderBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => string.Empty);
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(
                                    SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level, all,
                                        ser.Value, allpic, pic, tagfilter));
                                if (series_list.Count >= limit)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    else
                    {
                        var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>>();
                        allSeries.ForAll(a => CheckTitlesFuzzy(a, query, ref distLevenshtein, limit));

                        series = distLevenshtein.Keys.OrderBy(a => distLevenshtein[a].Item1)
                            .ThenBy(a => distLevenshtein[a].Item2.Length)
                            .ThenBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => distLevenshtein[a].Item2);
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level,
                                    all,
                                    ser.Value, allpic, pic, tagfilter));
                                if (series_list.Count >= limit)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    break;
                case 1:
                    int realLimit = limit_tag != 0 ? limit_tag : limit;
                    if (!fuzzy || query.Length >= IntPtr.Size)
                    {
                        series = allSeries
                            .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                                        (a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                             .Contains(query,
                                                 StringComparer.InvariantCultureIgnoreCase) || a.Contract.AniDBAnime
                                             .CustomTags
                                             .Select(b => b.TagName)
                                             .Contains(query, StringComparer.InvariantCultureIgnoreCase)))
                            .OrderBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => string.Empty);
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(
                                    SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level, all,
                                        ser.Value, allpic, pic, tagfilter));
                                if (series_list.Count >= realLimit)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    else
                    {
                        var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>>();
                        allSeries.ForAll(a => CheckTagsFuzzy(a, query, ref distLevenshtein, realLimit));

                        series = distLevenshtein.Keys.OrderBy(a => distLevenshtein[a].Item1)
                            .ThenBy(a => distLevenshtein[a].Item2.Length)
                            .ThenBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => distLevenshtein[a].Item2);
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level,
                                    all,
                                    ser.Value, allpic, pic, tagfilter));
                                if (series_list.Count >= realLimit)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    break;
                default:
                    bool use_extra = limit_tag != 0;
                    if (!fuzzy || query.Length >= (IntPtr.Size * 8))
                    {
                        series = allSeries
                            .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                                        Join(",", a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title), fuzzy)
                                            .IndexOf(SanitizeFuzzy(query, fuzzy), 0,
                                                StringComparison.InvariantCultureIgnoreCase) >= 0)
                            .OrderBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => string.Empty);

                        int tag_limit = use_extra ? limit_tag : limit - series.Count;
                        if (tag_limit < 0) tag_limit = 0;
                        series = series.ToList().Take(limit).ToDictionary(a => a.Key, a => a.Value);
                        if (tag_limit > 0)
                            series.AddRange(allSeries.Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                                                                 (a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                                                      .Contains(query,
                                                                          StringComparer.InvariantCultureIgnoreCase) ||
                                                                  a
                                                                      .Contract
                                                                      .AniDBAnime.CustomTags.Select(b => b.TagName)
                                                                      .Contains(query,
                                                                          StringComparer.InvariantCultureIgnoreCase)))
                                .OrderBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                                .Take(tag_limit)
                                .ToDictionary(a => a, a => string.Empty));
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(
                                    SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level, all,
                                        ser.Value, allpic, pic, tagfilter));
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    else
                    {
                        var distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>>();
                        allSeries.ForAll(a => CheckTitlesFuzzy(a, query, ref distLevenshtein, limit));

                        series.AddRange(distLevenshtein.Keys.OrderBy(a => distLevenshtein[a].Item1)
                            .ThenBy(a => distLevenshtein[a].Item2.Length)
                            .ThenBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                            .ToDictionary(a => a, a => distLevenshtein[a].Item2));
                        distLevenshtein = new ConcurrentDictionary<SVR_AnimeSeries, Tuple<int, string>>();

                        int tag_limit = use_extra ? limit_tag : limit - series.Count;
                        if (tag_limit < 0) tag_limit = 0;

                        if (tag_limit > 0)
                        {
                            allSeries.ForAll(a => CheckTagsFuzzy(a, query, ref distLevenshtein, tag_limit));
                            series.AddRange(distLevenshtein.Keys.OrderBy(a => distLevenshtein[a].Item1)
                                .ThenBy(a => distLevenshtein[a].Item2.Length)
                                .ThenBy(a => a.Contract.AniDBAnime.AniDBAnime.MainTitle)
                                .ToDictionary(a => a, a => distLevenshtein[a].Item2));
                        }
                        foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
                        {
                            if (offset == 0)
                            {
                                series_list.Add(SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level,
                                    all,
                                    ser.Value, allpic, pic, tagfilter));
                            }
                            else
                            {
                                offset -= 1;
                            }
                        }
                    }
                    break;
            }

            #endregion

            return series_list;
        }

        private static void CheckTitlesStartsWith(SVR_AnimeSeries a, string query,
            ref ConcurrentDictionary<SVR_AnimeSeries, string> series, int limit)
        {
            if (series.Count >= limit) return;
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return;
            string match = string.Empty;
            foreach (string title in a.Contract.AniDBAnime.AnimeTitles.Select(b => b.Title).ToList())
            {
                if (string.IsNullOrEmpty(title)) continue;
                if (title.StartsWith(query, StringComparison.InvariantCultureIgnoreCase))
                {
                    match = title;
                }
            }
            // Keep the lowest distance
            if (match != string.Empty)
                series.TryAdd(a, match);
        }

        internal object StartsWith(string query, int limit, int uid, bool nocast,
            bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            query = query.ToLowerInvariant();

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(uid);
            if (user == null) return APIStatus.unauthorized();

            List<Serie> series_list = new List<Serie>();
            Dictionary<SVR_AnimeSeries, string> series = new Dictionary<SVR_AnimeSeries, string>();
            ConcurrentDictionary<SVR_AnimeSeries, string> tempseries = new ConcurrentDictionary<SVR_AnimeSeries, string>();
            ParallelQuery<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null &&
                            !a.Contract.AniDBAnime.Tags.Select(b => b.TagName)
                                .FindInEnumerable(user.GetHideCategories()))
                .AsParallel();

            #region Search_TitlesOnly
            allSeries.ForAll(a => CheckTitlesStartsWith(a, query, ref tempseries, limit));
            series = tempseries.OrderBy(a => a.Value).ToDictionary(a => a.Key, a => a.Value);

            foreach (KeyValuePair<SVR_AnimeSeries, string> ser in series)
            {
                series_list.Add(
                    SearchResult.GenerateFromAnimeSeries(Context, ser.Key, uid, nocast, notag, level, all,
                        ser.Value, allpic, pic, tagfilter));
                if (series_list.Count >= limit)
                {
                    break;
                }
            }

            #endregion

            return series_list;
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
            if (id <= 0)
            {
                return APIStatus.badRequest("'id' value is wrong");
            }

            if (score <= 0 || score > 1000)
            {
                return APIStatus.badRequest("'score' value is wrong");
            }

            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByID(id);
            if (ser == null) return APIStatus.badRequest($"Series with id {id} was not found");
            int voteType = ser.Contract.AniDBAnime.AniDBAnime.GetFinishedAiring()
                ? (int) AniDBVoteType.Anime
                : (int) AniDBVoteType.AnimeTemp;

            AniDB_Vote thisVote =
                RepoFactory.AniDB_Vote.GetByEntityAndType(id, AniDBVoteType.AnimeTemp) ??
                RepoFactory.AniDB_Vote.GetByEntityAndType(id, AniDBVoteType.Anime);

            if (thisVote == null)
            {
                thisVote = new AniDB_Vote
                {
                    EntityID = ser.AniDB_ID
                };
            }

            if (score <= 10)
            {
                score = score * 100;
            }

            thisVote.VoteValue = score;
            thisVote.VoteType = voteType;

            RepoFactory.AniDB_Vote.Save(thisVote);

            Commands.CommandRequest_VoteAnime cmdVote =
                new Commands.CommandRequest_VoteAnime(ser.AniDB_ID, voteType, Convert.ToDecimal(score / 100));
            cmdVote.Save();
            return APIStatus.statusOK();
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
            ShokoServer.RunImport();
            return APIStatus.statusOK();
        }

        #endregion

        #region 10. Filters

        /// <summary>
        /// Handle /api/filter
        /// Using if without ?id consider using ?level as it will scan resursive for object from Filter to RawFile
        /// </summary>
        /// <returns>Filter or List<Filter></returns>
        private object GetFilters()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllFilters(user.JMMUserID, para.nocast != 0, para.notag != 0, para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return GetFilter(para.id, user.JMMUserID, para.nocast != 0, para.notag != 0, para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
                ;
            }
        }

        #region internal function

        /// <summary>
        /// Return all known filter for given user
        /// </summary>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <returns>List<Filter></returns>
        internal object GetAllFilters(int uid, bool nocast, bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            Filters filters = new Filters
            {
                id = 0,
                name = "Filters",
                viewed = 0,
                url = APIHelper.ConstructFilterUrl(Context)
            };
            List<SVR_GroupFilter> allGfs = RepoFactory.GroupFilter.GetTopLevel()
                .Where(a => a.InvisibleInClients == 0 &&
                            ((a.GroupsIds.ContainsKey(uid) && a.GroupsIds[uid].Count > 0) ||
                             (a.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory))
                .ToList();
            List<Filter> _filters = new List<Filter>();

            foreach (SVR_GroupFilter gf in allGfs)
            {
                Filter filter = Filter.GenerateFromGroupFilter(Context, gf, uid, nocast, notag, level, all, allpic, pic, tagfilter);
                _filters.Add(filter);
            }

            // Include 'Unsort'
            List<SVR_VideoLocal> vids = RepoFactory.VideoLocal.GetVideosWithoutEpisode();
            if (vids.Count > 0)
            {
                Filter filter = new Filter
                {
                    url = APIHelper.ConstructUnsortUrl(Context),
                    name = "Unsort"
                };
                filter.art.fanart.Add(new Art()
                {
                    url = APIHelper.ConstructSupportImageLink(Context, "plex_unsort.png"),
                    index = 0
                });
                filter.art.thumb.Add(
                    new Art() {url = APIHelper.ConstructSupportImageLink(Context, "plex_unsort.png"), index = 0});
                filter.size = vids.Count;
                filter.viewed = 0;

                _filters.Add(filter);
            }

            filters.filters = _filters.OrderBy(a => a.name).ToList();
            filters.size = _filters.Count();

            return filters;
        }

        /// <summary>
        /// Internal function that return information about given filter
        /// </summary>
        /// <param name="id">filter id</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <param name="all">include missing episodes</param>
        /// <returns>Filter or Filters</returns>
        internal object GetFilter(int id, int uid, bool nocast, bool notag, int level, bool all, bool allpic, int pic, byte tagfilter)
        {
            SVR_GroupFilter gf = RepoFactory.GroupFilter.GetByID(id);

            if ((gf.FilterType & (int) GroupFilterType.Directory) == (int) GroupFilterType.Directory)
            {
                // if it's a directory, it IS a filter-inception;
                Filters fgs = Filters.GenerateFromGroupFilter(Context, gf, uid, nocast, notag, all, level, allpic, pic, tagfilter);
                return fgs;
            }

            Filter filter = Filter.GenerateFromGroupFilter(Context, gf, uid, nocast, notag, level, all, allpic, pic, tagfilter);
            return filter;
        }

        #endregion

        #endregion

        #region 11. Group

        /// <summary>
        /// Handle /api/group
        /// </summary>
        /// <returns>Group or List<Group> or APIStatus</returns>
        public object GetGroups()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            if (para.id == 0)
            {
                return GetAllGroups(user.JMMUserID, para.nocast != 0, para.notag != 0, para.level, para.all != 0, para.allpics != 0, para.pic, para.tagfilter);
            }
            else
            {
                return GetGroup(para.id, user.JMMUserID, para.nocast != 0, para.notag != 0, para.level, para.all != 0,
                    para.filter, para.allpics != 0, para.pic, para.tagfilter);
            }
        }

        /// <summary>
        /// Handle /api/group/watch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkGroupAsWatched()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkWatchedStatusOnGroup(para.id, user.JMMUserID, true);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        /// <summary>
        /// Handle /api/group/unwatch
        /// </summary>
        /// <returns>APIStatus</returns>
        private object MarkGroupAsUnwatched()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser) this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();
            if (para.id != 0)
            {
                return MarkWatchedStatusOnGroup(para.id, user.JMMUserID, false);
            }
            else
            {
                return APIStatus.badRequest("missing 'id'");
            }
        }

        #region internal function

        /// <summary>
        /// Return list of all known groups
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="nocast"></param>
        /// <param name="notag"></param>
        /// <param name="level"></param>
        /// <param name="all"></param>
        /// <returns>List<Group></returns>
        internal object GetAllGroups(int uid, bool nocast, bool notag, int level, bool all, bool allpics, int pic, byte tagfilter)
        {
            List<Group> grps = new List<Group>();
            List<SVR_AnimeGroup_User> allGrps = RepoFactory.AnimeGroup_User.GetByUserID(uid);
            foreach (SVR_AnimeGroup_User gr in allGrps)
            {
                SVR_AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(gr.AnimeGroupID);
                Group grp = Group.GenerateFromAnimeGroup(Context, ag, uid, nocast, notag, level, all, 0, allpics, pic, tagfilter);
                grps.Add(grp);
            }
            return grps;
        }

        /// <summary>
        /// Return group of given id
        /// </summary>
        /// <param name="id">group id</param>
        /// <param name="uid">user id</param>
        /// <param name="nocast">disable cast</param>
        /// <param name="notag">disable tag</param>
        /// <param name="level">deep level</param>
        /// <param name="all">add all known episodes</param>
        /// <param name="filterid"></param>
        /// <returns>Group or APIStatus</returns>
        internal object GetGroup(int id, int uid, bool nocast, bool notag, int level, bool all, int filterid, bool allpics, int pic, byte tagfilter)
        {
            SVR_AnimeGroup ag = Repositories.RepoFactory.AnimeGroup.GetByID(id);
            if (ag != null)
            {
                Group gr = Group.GenerateFromAnimeGroup(Context, ag, uid, nocast, notag, level, all, filterid, allpics, pic, tagfilter);
                return gr;
            }
            else
            {
                return APIStatus.notFound404("group not found");
            }
        }

        /// <summary>
        /// Set watch status for group
        /// </summary>
        /// <param name="groupid">group id</param>
        /// <param name="userid">user id</param>
        /// <param name="watchedstatus">watch status</param>
        /// <returns>APIStatus</returns>
        internal object MarkWatchedStatusOnGroup(int groupid, int userid, bool watchedstatus)
        {
            try
            {
                SVR_AnimeGroup group = RepoFactory.AnimeGroup.GetByID(groupid);
                if (group == null)
                {
                    return APIStatus.notFound404("Group not Found");
                }

                foreach (SVR_AnimeSeries series in group.GetAllSeries())
                {
                    foreach (SVR_AnimeEpisode ep in series.GetAnimeEpisodes())
                    {
                        if (ep?.EpisodeTypeEnum == EpisodeType.Credits) continue;
                        if (ep?.EpisodeTypeEnum == EpisodeType.Trailer) continue;

                        ep?.ToggleWatchedStatus(watchedstatus, true, DateTime.Now, false, false, userid, true);
                    }
                    series.UpdateStats(true, false, false);
                }
                group.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true, false);

                return APIStatus.statusOK();
            }
            catch (Exception ex)
            {
                APIStatus.internalError("Internal Error : " + ex);
                LogManager.GetCurrentClassLogger().Error(ex, ex.ToString());
            }
            return APIStatus.badRequest();
        }

        #endregion

        #endregion

        public object GetLinks()
        {
            Request request = this.Request;
            JMMUser user = (JMMUser)this.Context.CurrentUser;
            API_Call_Parameters para = this.Bind();

            Dictionary<string, object> links = new Dictionary<string, object>();

            var serie = RepoFactory.AnimeSeries.GetByID(para.id);
            var trakt = serie.GetTraktShow();
            links.Add("trakt", trakt?.Select(x => x.URL));
            var tvdb = serie.GetTvDBSeries();
            if (tvdb != null) links.Add("tvdb", tvdb.Select(x => x.SeriesID));
            var tmdb = serie.CrossRefMovieDB;
            if (tmdb != null) links.Add("tmdb", tmdb.CrossRefID); //not sure this will work.

            return links;
        }
    }
}