using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using AniDBAPI;
using AniDBAPI.Commands;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Server;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Client;
using Shoko.Models.Interfaces;
using NLog;
using Shoko.Server.API.core;
using NutzCode.CloudFileSystem;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.MAL;
using Shoko.Server.Commands.TvDB;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Models.TvDB;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Tasks;
using Pri.LongPath;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation
    {
        public List<string> GetAllReleaseGroups()
        {
            return SVR_AniDB_Anime.GetAllReleaseGroups().ToList();
        }

        public void DeleteMultipleFilesWithPreferences(int userID)
        {
            List<CL_AnimeEpisode_User> epContracts = GetAllEpisodesWithMultipleFiles(userID, false, true);
            List<SVR_AnimeEpisode> eps =
                epContracts.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.AniDB_EpisodeID))
                    .Where(b => b != null)
                    .ToList();

            List<SVR_VideoLocal> videosToDelete = new List<SVR_VideoLocal>();

            foreach (SVR_AnimeEpisode ep in eps)
            {
                List<SVR_VideoLocal> videoLocals = ep.GetVideoLocals();
                videoLocals.Sort(FileQualityFilter.CompareTo);
                List<SVR_VideoLocal> keep = videoLocals
                    .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .ToList();
                foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                videosToDelete.AddRange(videoLocals);
            }

            foreach (SVR_VideoLocal toDelete in videosToDelete)
            {
                toDelete.Places.ForEach(a => a.RemoveAndDeleteFile());
            }
        }

        public List<CL_VideoLocal> PreviewDeleteMultipleFilesWithPreferences(int userID)
        {
            List<CL_AnimeEpisode_User> epContracts = GetAllEpisodesWithMultipleFiles(userID, false, true);
            List<SVR_AnimeEpisode> eps =
                epContracts.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.AniDB_EpisodeID))
                    .Where(b => b != null)
                    .ToList();

            List<SVR_VideoLocal> videosToDelete = new List<SVR_VideoLocal>();

            foreach (SVR_AnimeEpisode ep in eps)
            {
                List<SVR_VideoLocal> videoLocals = ep.GetVideoLocals();
                videoLocals.Sort(FileQualityFilter.CompareTo);
                List<SVR_VideoLocal> keep = videoLocals
                    .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .ToList();
                foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                videosToDelete.AddRange(videoLocals);
            }
            return videosToDelete.Select(a => a.ToClient(userID)).ToList();
        }

        public FileFfdshowPreset GetFFDPreset(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return null;

                return RepoFactory.FileFfdshowPreset.GetByHashAndSize(vid.Hash, vid.FileSize);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return null;
        }

        public void DeleteFFDPreset(int videoLocalID)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null) return;

                FileFfdshowPreset ffd = RepoFactory.FileFfdshowPreset.GetByHashAndSize(vid.Hash, vid.FileSize);
                if (ffd == null) return;

                RepoFactory.FileFfdshowPreset.Delete(ffd.FileFfdshowPresetID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public void SaveFFDPreset(FileFfdshowPreset preset)
        {
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (vid == null) return;

                FileFfdshowPreset ffd = RepoFactory.FileFfdshowPreset.GetByHashAndSize(preset.Hash, preset.FileSize);
                if (ffd == null) ffd = new FileFfdshowPreset();

                ffd.FileSize = preset.FileSize;
                ffd.Hash = preset.Hash;
                ffd.Preset = preset.Preset;

                RepoFactory.FileFfdshowPreset.Save(ffd);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
        {
            try
            {
                List<CL_VideoLocal> vids = new List<CL_VideoLocal>();

                FileSearchCriteria sType = (FileSearchCriteria) searchType;


                switch (sType)
                {
                    case FileSearchCriteria.Name:
                        List<SVR_VideoLocal> results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Trim());
                        foreach (SVR_VideoLocal vid in results1)
                            vids.Add(vid.ToClient(userID));
                        break;

                    case FileSearchCriteria.ED2KHash:
                        SVR_VideoLocal vidl = RepoFactory.VideoLocal.GetByHash(searchCriteria.Trim());
                        if (vidl != null)
                            vids.Add(vidl.ToClient(userID));
                        break;

                    case FileSearchCriteria.Size:
                        break;

                    case FileSearchCriteria.LastOneHundred:
                        List<SVR_VideoLocal> results2 = RepoFactory.VideoLocal.GetMostRecentlyAdded(100);
                        foreach (SVR_VideoLocal vid in results2)
                            vids.Add(vid.ToClient(userID));
                        break;
                }

                return vids;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_VideoLocal>();
        }

        /*public List<Contract_VideoLocalRenamed> RandomFileRenamePreview(int maxResults, int userID, string renameRules)
		{
			List<Contract_VideoLocalRenamed> ret = new List<Contract_VideoLocalRenamed>();
			try
			{
				VideoLocalRepository repVids = new VideoLocalRepository();
				foreach (VideoLocal vid in repVids.GetRandomFiles(maxResults))
				{
					Contract_VideoLocalRenamed vidRen = new Contract_VideoLocalRenamed();
					vidRen.VideoLocalID = vid.VideoLocalID;
					vidRen.VideoLocal = vid.ToContract(userID);
					vidRen.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);
					ret.Add(vidRen);
				}
			}
			catch (Exception ex)
			{
				logger.Error( ex,ex.ToString());

			}
			return ret;
		}*/

        public List<CL_VideoLocal> RandomFileRenamePreview(int maxResults, int userID)
        {
            try
            {
                return RepoFactory.VideoLocal.GetRandomFiles(maxResults).Select(a => a.ToClient(userID)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<CL_VideoLocal>();
            }
        }

        public CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID, string renameRules)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;

            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = string.Format("ERROR: Could not find file record");
                    ret.Success = false;
                }
                else
                {
                    if (videoLocalID == 726)
                        Debug.Write("test");

                    ret.VideoLocal = null;
                    ret.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);
                    if (string.IsNullOrEmpty(ret.NewFileName)) ret.Success = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public CL_VideoLocal_Renamed RenameFile(int videoLocalID, string renameRules)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed();
            ret.VideoLocalID = videoLocalID;
            ret.Success = true;
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = string.Format("ERROR: Could not find file record");
                    ret.Success = false;
                    return ret;
                }

                ret.NewFileName = RenameFileHelper.GetNewFileName(vid, renameRules);

                if (string.IsNullOrEmpty(ret.NewFileName))
                {
                    ret.VideoLocal = null;
                    ret.Success = false;
                    return ret;
                }

                string name = string.Empty;
                if (vid.Places.Count > 0)
                {
                    foreach (SVR_VideoLocal_Place place in vid.Places)
                    {
                        // check if the file exists
                        string fullFileName = place.FullServerPath;
                        IFileSystem fs = place.ImportFolder.FileSystem;
                        FileSystemResult<IObject> obj = fs.Resolve(fullFileName);
                        if (!obj.IsOk || obj.Result is IDirectory)
                        {
                            ret.NewFileName = "Error could not find the original file";
                            ret.Success = false;
                            return ret;
                        }

                        // actually rename the file
                        string path = Path.GetDirectoryName(fullFileName);
                        string newFullName = Path.Combine(path, ret.NewFileName);

                        try
                        {
                            logger.Info(string.Format("Renaming file From ({0}) to ({1})....", fullFileName,
                                newFullName));

                            if (fullFileName.Equals(newFullName, StringComparison.InvariantCultureIgnoreCase))
                            {
                                logger.Info(string.Format(
                                    "Renaming file SKIPPED, no change From ({0}) to ({1})",
                                    fullFileName, newFullName));
                                ret.NewFileName = newFullName;
                            }
                            else
                            {
                                string dir = Path.GetDirectoryName(newFullName);

                                ((IFile) obj.Result).Rename(ret.NewFileName);
                                logger.Info(string.Format("Renaming file SUCCESS From ({0}) to ({1})",
                                    fullFileName,
                                    newFullName));
                                ret.NewFileName = newFullName;
                                var tup = VideoLocal_PlaceRepository.GetFromFullPath(newFullName);
                                place.FilePath = tup.Item2;
                                name = Path.GetFileName(tup.Item2);
                                RepoFactory.VideoLocalPlace.Save(place);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Info(string.Format("Renaming file FAIL From ({0}) to ({1}) - {2}",
                                fullFileName,
                                newFullName, ex.Message));
                            logger.Error(ex, ex.ToString());
                            ret.Success = false;
                            ret.NewFileName = ex.Message;
                        }
                    }
                    vid.FileName = name;
                    ret.VideoLocal.FileName = name;
                    RepoFactory.VideoLocal.Save(vid, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = string.Format("ERROR: {0}", ex.Message);
                ret.Success = false;
            }
            return ret;
        }

        public List<CL_VideoLocal_Renamed> RenameFiles(List<int> videoLocalIDs, string renameRules)
        {
            List<CL_VideoLocal_Renamed> ret = new List<CL_VideoLocal_Renamed>();
            try
            {
                foreach (int vid in videoLocalIDs)
                {
                    ret.Add(RenameFile(vid, renameRules));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return ret;
        }

        public List<RenameScript> GetAllRenameScripts()
        {
            try
            {
                return RepoFactory.RenameScript.GetAll().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<RenameScript>();
        }

        public CL_Response<RenameScript> SaveRenameScript(RenameScript contract)
        {
            CL_Response<RenameScript> response = new CL_Response<RenameScript>();
            response.ErrorMessage = "";
            response.Result = null;

            try
            {
                RenameScript script = null;
                if (contract.RenameScriptID != 0)
                {
                    // update
                    script = RepoFactory.RenameScript.GetByID(contract.RenameScriptID);
                    if (script == null)
                    {
                        response.ErrorMessage = "Could not find Rename Script ID: " +
                                                contract.RenameScriptID.ToString();
                        return response;
                    }
                }
                else
                {
                    // create
                    script = new RenameScript();
                }

                if (string.IsNullOrEmpty(contract.ScriptName))
                {
                    response.ErrorMessage = "Must specify a Script Name";
                    return response;
                }

                // check to make sure we multiple scripts enable on import (only one can be selected)
                IReadOnlyList<RenameScript> allScripts = RepoFactory.RenameScript.GetAll();

                if (contract.IsEnabledOnImport == 1)
                {
                    foreach (RenameScript rs in allScripts)
                    {
                        if (rs.IsEnabledOnImport == 1 &&
                            (contract.RenameScriptID == 0 || (contract.RenameScriptID != rs.RenameScriptID)))
                        {
                            rs.IsEnabledOnImport = 0;
                            RepoFactory.RenameScript.Save(rs);
                        }
                    }
                }

                script.IsEnabledOnImport = contract.IsEnabledOnImport;
                script.Script = contract.Script;
                script.ScriptName = contract.ScriptName;
                RepoFactory.RenameScript.Save(script);

                response.Result = script;

                return response;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                response.ErrorMessage = ex.Message;
                return response;
            }
        }

        public string DeleteRenameScript(int renameScriptID)
        {
            try
            {
                RenameScript df = RepoFactory.RenameScript.GetByID(renameScriptID);
                if (df == null) return "Database entry does not exist";
                RepoFactory.RenameScript.Delete(renameScriptID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<AniDB_Recommendation> GetAniDBRecommendations(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Recommendation.GetByAnimeID(animeID).Cast<AniDB_Recommendation>().ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<AniDB_Recommendation>();
            }
        }

        public List<CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
        {
            List<CL_AnimeSearch> retTitles = new List<CL_AnimeSearch>();


            try
            {
                // check if it is a title search or an ID search
                int aid = 0;
                if (int.TryParse(titleQuery, out aid))
                {
                    // user is direct entering the anime id

                    // try the local database first
                    // if not download the data from AniDB now
                    SVR_AniDB_Anime anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                        ServerSettings.AniDB_DownloadRelatedAnime);
                    if (anime != null)
                    {
                        CL_AnimeSearch res = new CL_AnimeSearch();
                        res.AnimeID = anime.AnimeID;
                        res.MainTitle = anime.MainTitle;
                        res.Titles =
                            new HashSet<string>(anime.AllTitles.Split(new char[] {'|'},
                                StringSplitOptions.RemoveEmptyEntries));

                        // check for existing series and group details
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (ser != null)
                        {
                            res.SeriesExists = true;
                            res.AnimeSeriesID = ser.AnimeSeriesID;
                            res.AnimeSeriesName = anime.GetFormattedTitle();
                        }
                        else
                        {
                            res.SeriesExists = false;
                        }
                        retTitles.Add(res);
                    }
                }
                else
                {
                    // title search so look at the web cache
                    List<Shoko.Models.Azure.Azure_AnimeIDTitle> titles = AzureWebAPI.Get_AnimeTitle(titleQuery);

                    using (var session = DatabaseFactory.SessionFactory.OpenSession())
                    {
                        ISessionWrapper sessionWrapper = session.Wrap();

                        foreach (Shoko.Models.Azure.Azure_AnimeIDTitle tit in titles)
                        {
                            CL_AnimeSearch res = new CL_AnimeSearch();
                            res.AnimeID = tit.AnimeID;
                            res.MainTitle = tit.MainTitle;
                            res.Titles =
                                new HashSet<string>(tit.Titles.Split(new char[] {'|'},
                                    StringSplitOptions.RemoveEmptyEntries));

                            // check for existing series and group details
                            SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(tit.AnimeID);
                            if (ser != null)
                            {
                                res.SeriesExists = true;
                                res.AnimeSeriesID = ser.AnimeSeriesID;
                                res.AnimeSeriesName = ser.GetAnime().GetFormattedTitle();
                            }
                            else
                            {
                                res.SeriesExists = false;
                            }


                            retTitles.Add(res);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retTitles;
        }

        public List<CL_IgnoreAnime> GetIgnoredAnime(int userID)
        {
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user == null) return new List<CL_IgnoreAnime>();

                return RepoFactory.IgnoreAnime.GetByUser(userID).Select(a => a.ToClient()).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return new List<CL_IgnoreAnime>();
        }


        public void RemoveIgnoreAnime(int ignoreAnimeID)
        {
            try
            {
                IgnoreAnime ignore = RepoFactory.IgnoreAnime.GetByID(ignoreAnimeID);
                if (ignore == null) return;

                RepoFactory.IgnoreAnime.Delete(ignoreAnimeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<CL_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState)
        {
            List<CL_MissingEpisode> contracts = new List<CL_MissingEpisode>();

            AiringState airState = (AiringState) airingState;

            Dictionary<int, SVR_AniDB_Anime> animeCache = new Dictionary<int, SVR_AniDB_Anime>();
            Dictionary<int, List<CL_GroupVideoQuality>> gvqCache =
                new Dictionary<int, List<CL_GroupVideoQuality>>();
            Dictionary<int, List<CL_GroupFileSummary>> gfqCache =
                new Dictionary<int, List<CL_GroupFileSummary>>();

            try
            {
                int i = 0;
                IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    i++;
                    //string msg = string.Format("Updating series {0} of {1} ({2}) -  {3}", i, allSeries.Count, ser.Anime.MainTitle, DateTime.Now);
                    //logger.Debug(msg);

                    //if (ser.Anime.AnimeID != 69) continue;

                    int missingEps = ser.MissingEpisodeCount;
                    if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                    bool finishedAiring = ser.GetAnime().GetFinishedAiring();

                    if (!finishedAiring && airState == AiringState.FinishedAiring) continue;
                    if (finishedAiring && airState == AiringState.StillAiring) continue;

                    DateTime start = DateTime.Now;
                    TimeSpan ts = DateTime.Now - start;

                    double totalTiming = 0;
                    double timingVids = 0;
                    double timingSeries = 0;
                    double timingAnime = 0;
                    double timingQuality = 0;
                    double timingEps = 0;
                    double timingAniEps = 0;
                    int epCount = 0;

                    DateTime oStart = DateTime.Now;

                    if (missingEps > 0)
                    {
                        // find the missing episodes
                        start = DateTime.Now;
                        List<SVR_AnimeEpisode> eps = ser.GetAnimeEpisodes();
                        ts = DateTime.Now - start;
                        timingEps += ts.TotalMilliseconds;

                        epCount = eps.Count;
                        foreach (SVR_AnimeEpisode aep in ser.GetAnimeEpisodes())
                        {
                            if (regularEpisodesOnly && aep.EpisodeTypeEnum != enEpisodeType.Episode) continue;

                            AniDB_Episode aniep = aep.AniDB_Episode;
                            if (aniep.GetFutureDated()) continue;

                            start = DateTime.Now;
                            List<SVR_VideoLocal> vids = aep.GetVideoLocals();
                            ts = DateTime.Now - start;
                            timingVids += ts.TotalMilliseconds;

                            if (vids.Count == 0)
                            {
                                CL_MissingEpisode cl = new CL_MissingEpisode();
                                cl.AnimeID = ser.AniDB_ID;
                                start = DateTime.Now;
                                cl.AnimeSeries = ser.GetUserContract(userID);
                                ts = DateTime.Now - start;
                                timingSeries += ts.TotalMilliseconds;

                                SVR_AniDB_Anime anime = null;
                                if (animeCache.ContainsKey(ser.AniDB_ID))
                                    anime = animeCache[ser.AniDB_ID];
                                else
                                {
                                    start = DateTime.Now;
                                    anime = ser.GetAnime();
                                    ts = DateTime.Now - start;
                                    timingAnime += ts.TotalMilliseconds;
                                    animeCache[ser.AniDB_ID] = anime;
                                }

                                cl.AnimeTitle = anime.MainTitle;

                                start = DateTime.Now;
                                cl.GroupFileSummary = "";
                                List<CL_GroupVideoQuality> summ = null;
                                if (gvqCache.ContainsKey(ser.AniDB_ID))
                                    summ = gvqCache[ser.AniDB_ID];
                                else
                                {
                                    summ = GetGroupVideoQualitySummary(anime.AnimeID);
                                    gvqCache[ser.AniDB_ID] = summ;
                                }

                                foreach (CL_GroupVideoQuality gvq in summ)
                                {
                                    if (cl.GroupFileSummary.Length > 0)
                                        cl.GroupFileSummary += " --- ";

                                    cl.GroupFileSummary += string.Format("{0} - {1}/{2}/{3}bit ({4})",
                                        gvq.GroupNameShort, gvq.Resolution,
                                        gvq.VideoSource, gvq.VideoBitDepth, gvq.NormalEpisodeNumberSummary);
                                }

                                cl.GroupFileSummarySimple = "";
                                List<CL_GroupFileSummary> summFiles = null;
                                if (gfqCache.ContainsKey(ser.AniDB_ID))
                                    summFiles = gfqCache[ser.AniDB_ID];
                                else
                                {
                                    summFiles = GetGroupFileSummary(anime.AnimeID);
                                    gfqCache[ser.AniDB_ID] = summFiles;
                                }

                                foreach (CL_GroupFileSummary gfq in summFiles)
                                {
                                    if (cl.GroupFileSummarySimple.Length > 0)
                                        cl.GroupFileSummarySimple += ", ";

                                    cl.GroupFileSummarySimple += string.Format("{0} ({1})", gfq.GroupNameShort,
                                        gfq.NormalEpisodeNumberSummary);
                                }

                                ts = DateTime.Now - start;
                                timingQuality += ts.TotalMilliseconds;
                                animeCache[ser.AniDB_ID] = anime;

                                start = DateTime.Now;
                                cl.EpisodeID = aniep.EpisodeID;
                                cl.EpisodeNumber = aniep.EpisodeNumber;
                                cl.EpisodeType = aniep.EpisodeType;
                                contracts.Add(cl);
                                ts = DateTime.Now - start;
                                timingAniEps += ts.TotalMilliseconds;
                            }
                        }

                        ts = DateTime.Now - oStart;
                        totalTiming = ts.TotalMilliseconds;

                        string msg2 =
                            string.Format("Timing for series {0} ({1}) : {2}/{3}/{4}/{5}/{6}/{7} - {8} eps (AID: {9})",
                                ser.GetAnime().MainTitle, totalTiming, timingVids, timingSeries,
                                timingAnime, timingQuality, timingEps, timingAniEps, epCount, ser.GetAnime().AnimeID);
                        //logger.Debug(msg2);
                    }
                }
                contracts = contracts.OrderBy(a => a.AnimeTitle)
                    .ThenBy(a => a.EpisodeType)
                    .ThenBy(a => a.EpisodeNumber)
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public List<CL_MissingFile> GetMyListFilesForRemoval(int userID)
        {
            List<CL_MissingFile> contracts = new List<CL_MissingFile>();

            /*Contract_MissingFile missingFile2 = new Contract_MissingFile();
			missingFile2.AnimeID = 1;
			missingFile2.AnimeTitle = "Gundam Age";
			missingFile2.EpisodeID = 2;
			missingFile2.EpisodeNumber = 7;
			missingFile2.FileID = 8;
			missingFile2.AnimeSeries = null;
			contracts.Add(missingFile2);

			Thread.Sleep(5000);

			return contracts;*/


            Dictionary<int, SVR_AniDB_Anime> animeCache = new Dictionary<int, SVR_AniDB_Anime>();
            Dictionary<int, SVR_AnimeSeries> animeSeriesCache = new Dictionary<int, SVR_AnimeSeries>();

            try
            {
                AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                enHelperActivityType ev = cmd.Process();
                if (ev == enHelperActivityType.GotMyListHTTP)
                {
                    foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
                    {
                        // let's check if the file on AniDB actually exists in the user's local collection
                        string hash = string.Empty;

                        AniDB_File anifile = RepoFactory.AniDB_File.GetByFileID(myitem.FileID);
                        if (anifile != null)
                            hash = anifile.Hash;
                        else
                        {
                            // look for manually linked files
                            List<CrossRef_File_Episode> xrefs =
                                RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID);
                            foreach (CrossRef_File_Episode xref in xrefs)
                            {
                                if (xref.CrossRefSource != (int) CrossRefSource.AniDB)
                                {
                                    hash = xref.Hash;
                                    break;
                                }
                            }
                        }

                        bool fileMissing = false;
                        if (string.IsNullOrEmpty(hash))
                            fileMissing = true;
                        else
                        {
                            // now check if the file actually exists on disk
                            SVR_VideoLocal v = RepoFactory.VideoLocal.GetByHash(hash);
                            fileMissing = true;
                            foreach (SVR_VideoLocal_Place p in v.Places)
                            {
                                IFileSystem fs = p.ImportFolder.FileSystem;
                                if (fs != null)
                                {
                                    FileSystemResult<IObject> res = fs.Resolve(p.FullServerPath);
                                    if (res != null && res.IsOk)
                                    {
                                        fileMissing = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (fileMissing)
                        {
                            // this means we can't find the file
                            SVR_AniDB_Anime anime = null;
                            if (animeCache.ContainsKey(myitem.AnimeID))
                                anime = animeCache[myitem.AnimeID];
                            else
                            {
                                anime = RepoFactory.AniDB_Anime.GetByAnimeID(myitem.AnimeID);
                                animeCache[myitem.AnimeID] = anime;
                            }

                            SVR_AnimeSeries ser = null;
                            if (animeSeriesCache.ContainsKey(myitem.AnimeID))
                                ser = animeSeriesCache[myitem.AnimeID];
                            else
                            {
                                ser = RepoFactory.AnimeSeries.GetByAnimeID(myitem.AnimeID);
                                animeSeriesCache[myitem.AnimeID] = ser;
                            }


                            CL_MissingFile missingFile = new CL_MissingFile();
                            missingFile.AnimeID = myitem.AnimeID;
                            missingFile.AnimeTitle = "Data Missing";
                            if (anime != null) missingFile.AnimeTitle = anime.MainTitle;
                            missingFile.EpisodeID = myitem.EpisodeID;
                            AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(myitem.EpisodeID);
                            missingFile.EpisodeNumber = -1;
                            missingFile.EpisodeType = 1;
                            if (ep != null)
                            {
                                missingFile.EpisodeNumber = ep.EpisodeNumber;
                                missingFile.EpisodeType = ep.EpisodeType;
                            }
                            missingFile.FileID = myitem.FileID;

                            if (ser == null) missingFile.AnimeSeries = null;
                            else missingFile.AnimeSeries = ser.GetUserContract(userID);

                            contracts.Add(missingFile);
                        }
                    }
                }
                contracts = contracts.OrderBy(a => a.AnimeTitle).ThenBy(a => a.EpisodeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public void RemoveMissingMyListFiles(List<CL_MissingFile> myListFiles)
        {
            foreach (CL_MissingFile missingFile in myListFiles)
            {
                CommandRequest_DeleteFileFromMyList cmd = new CommandRequest_DeleteFileFromMyList(missingFile.FileID);
                cmd.Save();

                // For deletion of files from Trakt, we will rely on the Daily sync
                // lets also try removing from the users trakt collecion
            }
        }

        public List<CL_AnimeSeries_User> GetSeriesWithoutAnyFiles(int userID)
        {
            List<CL_AnimeSeries_User> contracts = new List<CL_AnimeSeries_User>();


            try
            {
                foreach (SVR_AnimeSeries ser in RepoFactory.AnimeSeries.GetAll())
                {
                    if (RepoFactory.VideoLocal.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                    {
                        CL_AnimeSeries_User can = ser.GetUserContract(userID);
                        if (can != null)
                            contracts.Add(can);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public List<CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int jmmuserID)
        {
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(jmmuserID);
                if (user != null)
                    return
                        RepoFactory.AnimeSeries.GetWithMissingEpisodes()
                            .Select(a => a.GetUserContract(jmmuserID))
                            .Where(a => a != null)
                            .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_AnimeSeries_User>();
        }

        public List<CL_VideoLocal> GetIgnoredFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetIgnoredVideos())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public List<CL_VideoLocal> GetManuallyLinkedFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public List<CL_VideoLocal> GetUnrecognisedFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetVideosWithoutEpisode())
                {
                    contracts.Add(vid.ToClient(userID));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        public void RescanUnlinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                List<SVR_VideoLocal> filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

                foreach (SVR_VideoLocal vl in filesWithoutEpisode.Where(a => !string.IsNullOrEmpty(a.Hash)))
                {
                    CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
            }
        }

        public void RescanManuallyLinkedFiles()
        {
            try
            {
                // files which have been hashed, but don't have an associated episode
                List<SVR_VideoLocal> files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();

                foreach (SVR_VideoLocal vl in files.Where(a => !string.IsNullOrEmpty(a.Hash)))
                {
                    CommandRequest_ProcessFile cmd = new CommandRequest_ProcessFile(vl.VideoLocalID, true);
                    cmd.Save();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
            }
        }

        public List<CL_DuplicateFile> GetAllDuplicateFiles()
        {
            List<CL_DuplicateFile> dupFiles = new List<CL_DuplicateFile>();
            try
            {
                return RepoFactory.DuplicateFile.GetAll().Select(a => ModelClients.ToClient(a)).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return dupFiles;
            }
        }

        /// <summary>
        /// Delete a duplicate file entry, and also one of the physical files
        /// </summary>
        /// <param name="duplicateFileID"></param>
        /// <param name="fileNumber">0 = Don't delete any physical files, 1 = Delete file 1, 2 = Deleet file 2</param>
        /// <returns></returns>
        public string DeleteDuplicateFile(int duplicateFileID, int fileNumber)
        {
            try
            {
                DuplicateFile df = RepoFactory.DuplicateFile.GetByID(duplicateFileID);
                if (df == null) return "Database entry does not exist";

                if (fileNumber == 1 || fileNumber == 2)
                {
                    string fileName = "";
                    if (fileNumber == 1) fileName = df.GetFullServerPath1();
                    if (fileNumber == 2) fileName = df.GetFullServerPath2();
                    IFile file = SVR_VideoLocal.ResolveFile(fileName);
                    file?.Delete(false);
                }

                RepoFactory.DuplicateFile.Delete(duplicateFileID);

                return "";
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        public List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID)
        {
            List<CL_VideoLocal> manualFiles = new List<CL_VideoLocal>();
            try
            {
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
                {
                    manualFiles.Add(vid.ToClient(userID));
                }

                return manualFiles;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return manualFiles;
            }
        }

        public List<CL_AnimeEpisode_User> GetAllEpisodesWithMultipleFiles(int userID, bool onlyFinishedSeries,
            bool ignoreVariations)
        {
            List<CL_AnimeEpisode_User> eps = new List<CL_AnimeEpisode_User>();
            try
            {
                Dictionary<int, int> dictSeriesAnime = new Dictionary<int, int>();
                Dictionary<int, bool> dictAnimeFinishedAiring = new Dictionary<int, bool>();
                Dictionary<int, bool> dictSeriesFinishedAiring = new Dictionary<int, bool>();

                if (onlyFinishedSeries)
                {
                    IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                    foreach (SVR_AnimeSeries ser in allSeries)
                        dictSeriesAnime[ser.AnimeSeriesID] = ser.AniDB_ID;

                    IReadOnlyList<SVR_AniDB_Anime> allAnime = RepoFactory.AniDB_Anime.GetAll();
                    foreach (SVR_AniDB_Anime anime in allAnime)
                        dictAnimeFinishedAiring[anime.AnimeID] = anime.GetFinishedAiring();

                    foreach (KeyValuePair<int, int> kvp in dictSeriesAnime)
                    {
                        if (dictAnimeFinishedAiring.ContainsKey(kvp.Value))
                            dictSeriesFinishedAiring[kvp.Key] = dictAnimeFinishedAiring[kvp.Value];
                    }
                }

                foreach (SVR_AnimeEpisode ep in RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations))
                {
                    if (onlyFinishedSeries)
                    {
                        bool finishedAiring = false;
                        if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                            finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                        if (!finishedAiring) continue;
                    }
                    CL_AnimeEpisode_User cep = ep.GetUserContract(userID);
                    if (cep != null)
                        eps.Add(cep);
                }

                return eps;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return eps;
            }
        }

        public void ReevaluateDuplicateFiles()
        {
            try
            {
                foreach (DuplicateFile df in RepoFactory.DuplicateFile.GetAll())
                {
                    if (df.GetImportFolder1() == null || df.GetImportFolder2() == null)
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as one of the import folders can't be found: {0} --- {1}",
                                df.FilePathFile1, df.FilePathFile2);
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                        continue;
                    }

                    // make sure that they are not actually the same file
                    if (df.GetFullServerPath1()
                        .Equals(df.GetFullServerPath2(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as they are actually point to the same file: {0}",
                                df.GetFullServerPath1());
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                    }

                    // check if both files still exist
                    IFile file1 = SVR_VideoLocal.ResolveFile(df.GetFullServerPath1());
                    IFile file2 = SVR_VideoLocal.ResolveFile(df.GetFullServerPath2());
                    if (file1 == null || file2 == null)
                    {
                        string msg =
                            string.Format(
                                "Deleting duplicate file record as one of the files can't be found: {0} --- {1}",
                                df.GetFullServerPath1(), df.GetFullServerPath2());
                        logger.Info(msg);
                        RepoFactory.DuplicateFile.Delete(df.DuplicateFileID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        public List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
            string resolution,
            string videoSource, int videoBitDepth, int userID)
        {
            List<CL_VideoDetailed> vids = new List<CL_VideoDetailed>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    int thisBitDepth = 8;

                    int bitDepth = 0;
                    if (int.TryParse(vid.VideoBitDepth, out bitDepth))
                        thisBitDepth = bitDepth;

                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    SVR_AnimeEpisode animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == Shoko.Models.Enums.enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == Shoko.Models.Enums.enEpisodeType.Special)
                    {
                        // get the anibd file info
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile != null)
                        {
                            videoSource = SimplifyVideoSource(videoSource);
                            string fileSource = SimplifyVideoSource(aniFile.File_Source);
                            string vidResAniFile = Utils.GetStandardisedVideoResolution(aniFile.File_VideoResolution);

                            // match based on group / video sorce / video res
                            if (
                                relGroupName.Equals(aniFile.Anime_GroupName,
                                    StringComparison.InvariantCultureIgnoreCase) &&
                                videoSource.Equals(fileSource, StringComparison.InvariantCultureIgnoreCase) &&
                                resolution.Equals(vidResAniFile, StringComparison.InvariantCultureIgnoreCase) &&
                                thisBitDepth == videoBitDepth)
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                        else
                        {
                            string vidResInfo = Utils.GetStandardisedVideoResolution(vid.VideoResolution);

                            // match based on group / video sorce / video res
                            if (
                                relGroupName.Equals(Constants.NO_GROUP_INFO,
                                    StringComparison.InvariantCultureIgnoreCase) &&
                                videoSource.Equals(Constants.NO_SOURCE_INFO,
                                    StringComparison.InvariantCultureIgnoreCase) &&
                                resolution.Equals(vidResInfo, StringComparison.InvariantCultureIgnoreCase) &&
                                thisBitDepth == videoBitDepth)
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                    }
                }
                return vids;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return vids;
            }
        }

        public List<CL_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID)
        {
            List<CL_VideoDetailed> vids = new List<CL_VideoDetailed>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;
                    SVR_AnimeEpisode animeEp = eps[0];
                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                        animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                    {
                        // get the anibd file info
                        AniDB_File aniFile = vid.GetAniDBFile();
                        if (aniFile != null)
                        {
                            // match based on group / video sorce / video res
                            if (relGroupName.Equals(aniFile.Anime_GroupName,
                                StringComparison.InvariantCultureIgnoreCase))
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                        else
                        {
                            if (relGroupName.Equals(Constants.NO_GROUP_INFO,
                                StringComparison.InvariantCultureIgnoreCase))
                            {
                                vids.Add(vid.ToClientDetailed(userID));
                            }
                        }
                    }
                }
                return vids;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return vids;
            }
        }

        /// <summary>
        /// www is usually not used correctly
        /// </summary>
        /// <param name="origSource"></param>
        /// <returns></returns>
        private string SimplifyVideoSource(string origSource)
        {
            //return origSource;

            if (origSource.Equals("DTV", StringComparison.InvariantCultureIgnoreCase) ||
                origSource.Equals("HDTV", StringComparison.InvariantCultureIgnoreCase) ||
                origSource.Equals("www", StringComparison.InvariantCultureIgnoreCase))
            {
                return "TV";
            }
            else
                return origSource;
        }

        public List<CL_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID)
        {
            List<CL_GroupVideoQuality> vidQuals = new List<CL_GroupVideoQuality>();

            List<SVR_VideoLocal> files = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID);
            files.Sort(FileQualityFilter.CompareTo);
            var lookup = files.ToLookup(a => new
            {
                GroupName = a.ReleaseGroup?.GroupName ?? "NO GROUP INFO",
                GroupNameShort = a.ReleaseGroup?.GroupNameShort ?? "NO GROUP INFO",
                File_Source = a.GetAniDBFile()?.File_Source ?? "unknown",
                VideoResolution = a.VideoResolution
            });
            int rank = lookup.Count;
            foreach (var key in lookup)
            {
                CL_GroupVideoQuality contract = new CL_GroupVideoQuality();
                List<SVR_VideoLocal> videoLocals = key.ToList();
                List<SVR_AnimeEpisode> eps = videoLocals.Select(a => a.GetAnimeEpisodes().FirstOrDefault()).ToList();
                SVR_AniDB_File ani = videoLocals.First().GetAniDBFile();
                contract.AudioStreamCount = videoLocals.First()
                    .Media.Parts.SelectMany(a => a.Streams)
                    .Count(a => a.StreamType.Equals("2"));
                contract.FileCountNormal = eps.Count(a => a.EpisodeTypeEnum == enEpisodeType.Episode);
                contract.FileCountSpecials = eps.Count(a => a.EpisodeTypeEnum == enEpisodeType.Special);
                contract.GroupName = key.Key.GroupName;
                contract.GroupNameShort = key.Key.GroupNameShort;
                contract.NormalEpisodeNumbers = eps.Where(a => a.EpisodeTypeEnum == enEpisodeType.Episode)
                    .Select(a => a.AniDB_Episode.EpisodeNumber).OrderBy(a => a).ToList();
                contract.NormalEpisodeNumberSummary = contract.NormalEpisodeNumbers.ToRanges();
                contract.Ranking = rank;
                contract.Resolution = key.Key.VideoResolution;
                contract.TotalFileSize = videoLocals.Sum(a => a.FileSize);
                contract.TotalRunningTime = videoLocals.Sum(a => a.Duration);
                contract.VideoSource = key.Key.File_Source;
                string bitDepth = videoLocals.First().VideoBitDepth;
                if (!string.IsNullOrEmpty(bitDepth))
                {
                    int bit;
                    if (int.TryParse(bitDepth, out bit))
                        contract.VideoBitDepth = bit;
                }
                vidQuals.Add(contract);

                rank--;
            }

            return vidQuals;
        }


        public List<CL_GroupFileSummary> GetGroupFileSummary(int animeID)
        {
            List<CL_GroupFileSummary> vidQuals = new List<CL_GroupFileSummary>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);

                if (anime == null) return vidQuals;


                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();

                    if (eps.Count == 0) continue;

                    foreach (SVR_AnimeEpisode animeEp in eps)
                    {
                        //AnimeEpisode animeEp = eps[0];
                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode ||
                            animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                        {
                            AniDB_Episode anidbEp = animeEp.AniDB_Episode;

                            // get the anibd file info
                            AniDB_File aniFile = vid.GetAniDBFile();
                            if (aniFile != null)
                            {
                                // match based on group / video sorce / video res
                                bool foundSummaryRecord = false;
                                foreach (CL_GroupFileSummary contract in vidQuals)
                                {
                                    if (contract.GroupName.Equals(aniFile.Anime_GroupName,
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foundSummaryRecord = true;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += aniFile.FileSize;
                                        contract.TotalRunningTime += aniFile.File_LengthSeconds;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupFileSummary cl = new CL_GroupFileSummary();
                                    cl.FileCountNormal = 0;
                                    cl.FileCountSpecials = 0;
                                    cl.TotalFileSize = 0;
                                    cl.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode) cl.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special) cl.FileCountSpecials++;
                                    cl.TotalFileSize += aniFile.FileSize;
                                    cl.TotalRunningTime += aniFile.File_LengthSeconds;

                                    cl.GroupName = aniFile.Anime_GroupName;
                                    cl.GroupNameShort = aniFile.Anime_GroupNameShort;
                                    cl.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!cl.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            cl.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }

                                    vidQuals.Add(cl);
                                }
                            }
                            else
                            {
                                // look at the Video Info record
                                bool foundSummaryRecord = false;
                                foreach (CL_GroupFileSummary contract in vidQuals)
                                {
                                    if (contract.GroupName.Equals("NO GROUP INFO",
                                        StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        foundSummaryRecord = true;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                            contract.FileCountNormal++;
                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                            contract.FileCountSpecials++;
                                        contract.TotalFileSize += vid.FileSize;
                                        contract.TotalRunningTime += vid.Duration;

                                        if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        {
                                            if (!contract.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                                contract.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                        }
                                    }
                                }
                                if (!foundSummaryRecord)
                                {
                                    CL_GroupFileSummary cl = new CL_GroupFileSummary();
                                    cl.FileCountNormal = 0;
                                    cl.FileCountSpecials = 0;
                                    cl.TotalFileSize = 0;
                                    cl.TotalRunningTime = 0;

                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                        cl.FileCountNormal++;
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Special)
                                        cl.FileCountSpecials++;
                                    cl.TotalFileSize += vid.FileSize;
                                    cl.TotalRunningTime += vid.Duration;

                                    cl.GroupName = "NO GROUP INFO";
                                    cl.GroupNameShort = "NO GROUP INFO";
                                    cl.NormalEpisodeNumbers = new List<int>();
                                    if (animeEp.EpisodeTypeEnum == enEpisodeType.Episode)
                                    {
                                        if (!cl.NormalEpisodeNumbers.Contains(anidbEp.EpisodeNumber))
                                            cl.NormalEpisodeNumbers.Add(anidbEp.EpisodeNumber);
                                    }
                                    vidQuals.Add(cl);
                                }
                            }
                        }
                    }
                }

                foreach (CL_GroupFileSummary contract in vidQuals)
                {
                    contract.NormalComplete = contract.FileCountNormal >= anime.EpisodeCountNormal;
                    contract.SpecialsComplete = (contract.FileCountSpecials >= anime.EpisodeCountSpecial) &&
                                                (anime.EpisodeCountSpecial > 0);

                    contract.NormalEpisodeNumberSummary = "";
                    contract.NormalEpisodeNumbers.Sort();
                    int lastEpNum = 0;
                    int baseEpNum = 0;
                    foreach (int epNum in contract.NormalEpisodeNumbers)
                    {
                        if (baseEpNum == 0)
                        {
                            baseEpNum = epNum;
                            lastEpNum = epNum;
                        }

                        if (epNum == lastEpNum) continue;

                        int epNumDiff = epNum - lastEpNum;
                        if (epNumDiff == 1)
                        {
                            lastEpNum = epNum;
                            continue;
                        }


                        // this means we have missed an episode
                        if (contract.NormalEpisodeNumberSummary.Length > 0)
                            contract.NormalEpisodeNumberSummary += ", ";

                        if (baseEpNum == lastEpNum)
                            contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                        else
                            contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum, lastEpNum);

                        lastEpNum = epNum;
                        baseEpNum = epNum;
                    }

                    if (contract.NormalEpisodeNumbers.Count > 0)
                    {
                        if (contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1] >= baseEpNum)
                        {
                            // this means we have missed an episode
                            if (contract.NormalEpisodeNumberSummary.Length > 0)
                                contract.NormalEpisodeNumberSummary += ", ";

                            if (baseEpNum == contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1])
                                contract.NormalEpisodeNumberSummary += string.Format("{0}", baseEpNum);
                            else
                                contract.NormalEpisodeNumberSummary += string.Format("{0}-{1}", baseEpNum,
                                    contract.NormalEpisodeNumbers[contract.NormalEpisodeNumbers.Count - 1]);
                        }
                    }
                }

                return vidQuals.OrderBy(a => a.GroupNameShort).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return vidQuals;
            }
        }
    }
}