﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using AniDBAPI;
using AniDBAPI.Commands;
using F23.StringSimilarity;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.AniDB_API.Titles;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server
{
    public partial class ShokoServiceImplementation
    {
        [HttpPost("Series/SearchFilename/{uid}")]
        public List<CL_AnimeSeries_User> SearchSeriesWithFilename(int uid, [FromForm] string query)
        {
            string input = query ?? string.Empty;

            var series = SeriesSearch.Search(uid, query, int.MaxValue,
                SeriesSearch.SearchFlags.Titles | SeriesSearch.SearchFlags.Fuzzy);

            return series.Select(a => a.Result).Select(ser => ser.GetUserContract(uid)).ToList();
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
            $"{new string(Path.GetInvalidFileNameChars())}{new string(Path.GetInvalidPathChars())}".ToCharArray();

        private static readonly char[] ReplaceWithSpace = @"[]_-.+&()".ToCharArray();

        private static readonly string[] ReplacementStrings =
            {"h264", "x264", "x265", "bluray", "blu-ray", "remux", "avc", "flac", "dvd", "1080p", "720p", "480p", "hevc", "webrip", "web", "h265", "ac3", "aac", "mp3", "dts", "bd"};

        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            return Regex.Replace(input, Regex.Escape(search), replacement.Replace("$", "$$"),
                RegexOptions.IgnoreCase);
        }

        private static string RemoveSubgroups(string value)
        {
            int originalLength = value.Length;
            var releaseGroups = SVR_AniDB_Anime.GetAllReleaseGroups();
            foreach (string releaseGroup in releaseGroups)
            {
                value = ReplaceCaseInsensitive(value, releaseGroup, string.Empty);
                if (originalLength > value.Length) break;
            }
            return value;
        }

        internal static string SanitizeFuzzy(string value, bool replaceInvalid)
        {
            if (!replaceInvalid) return value;

            value = ReplacementStrings.Aggregate(value, (current, c) => ReplaceCaseInsensitive(current, c, string.Empty));
            value = ReplaceWithSpace.Aggregate(value, (current, c) => current.Replace(c, ' '));
            value = value.FilterCharacters(InvalidPathChars, true);

            // Takes too long
            //value = RemoveSubgroups(value);

            return value.CompactWhitespaces();
        }

        private static double GetLowestLevenshteinDistance(SVR_AnimeSeries a, string query)
        {
            if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return 1;
            double dist = 1;
            SorensenDice dice = new SorensenDice();
            var languages = new HashSet<string> {"en", "x-jat"};
            languages.UnionWith(ServerSettings.Instance.LanguagePreference.Select(b => b.ToLower()));
            foreach (string title in a.Contract.AniDBAnime.AnimeTitles
                .Where(b => b.TitleType != Shoko.Models.Constants.AnimeTitleType.ShortName &&
                            languages.Contains(b.Language.ToLower()))
                .Select(b => b.Title?.ToLowerInvariant()).ToList())
            {
                if (string.IsNullOrEmpty(title)) continue;
                var newTitle = SanitizeFuzzy(title, true);
                double newDist = dice.Distance(newTitle, query);
                if (newDist >= 1) continue;
                if (newDist < dist)
                {
                    dist = newDist;
                }
            }

            return dist;
        }

        [HttpPost("AniDB/Anime/SearchFilename/{uid}")]
        public List<CL_AniDB_Anime> SearchAnimeWithFilename(int uid, [FromForm]string query)
        {
            string input = query ?? string.Empty;
            input = input.ToLower(CultureInfo.InvariantCulture);
            input = SanitizeFuzzy(input, true);

            SVR_JMMUser user = RepoFactory.JMMUser.GetByID(uid);
            List<CL_AniDB_Anime> series_list = new List<CL_AniDB_Anime>();
            if (user == null) return series_list;

            var series = RepoFactory.AnimeSeries.GetAll()
                .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null)
                .AsParallel().Select(a => (a, GetLowestLevenshteinDistance(a, input))).OrderBy(a => a.Item2)
                .ThenBy(a => a.Item1.GetSeriesName())
                .Select(a => a.Item1).ToList();

            foreach (SVR_AnimeSeries ser in series)
            {
                series_list.Add(ser.GetAnime().Contract.AniDBAnime);
            }

            return series_list;
        }

        [HttpGet("ReleaseGroups")]
        public List<string> GetAllReleaseGroups()
        {
            return SVR_AniDB_Anime.GetAllReleaseGroups().ToList();
        }

        [HttpGet("File/DeleteMultipleFilesWithPreferences/{userID}")]
        public bool DeleteMultipleFilesWithPreferences(int userID)
        {
            try
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

                bool result = true;
                foreach (SVR_VideoLocal toDelete in videosToDelete)
                {
                    result &= toDelete.Places.All(a => a.RemoveAndDeleteFile().Item1);
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error Deleting Files");
                return false;
            }
        }

        [HttpGet("File/PreviewDeleteMultipleFilesWithPreferences/{userID}")]
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

        [HttpGet("File/GetMultipleFilesForDeletionByPreferences/{userID}")]
        public List<CL_VideoDetailed> GetMultipleFilesForDeletionByPreferences(int userID)
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
            return videosToDelete.Select(a => a.ToClientDetailed(userID))
                .OrderByNatural(a => a.VideoLocal_FileName)
                .ToList();
        }

        [HttpGet("FFDShowPreset/{videoLocalID}")]
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

        [HttpDelete("FFDShowPreset/{videoLocalID}")]
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

        [HttpPost("FFDShowPreset")]
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

        [HttpGet("File/Search/{searchType}/{searchCriteria}/{userID}")]
        public List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
        {
            try
            {
                List<CL_VideoLocal> vids = new List<CL_VideoLocal>();

                FileSearchCriteria sType = (FileSearchCriteria)searchType;


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
                        int number = 100;
                        if (!string.IsNullOrEmpty(searchCriteria))
                        {
                            if (int.TryParse(searchCriteria, out int temp)) number = temp;
                        }
                        List<SVR_VideoLocal> results2 = RepoFactory.VideoLocal.GetMostRecentlyAdded(number, userID);
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
				VideoLocalRepo.Instance.itory repVids = new VideoLocalRepo.Instance.itory();
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

        [HttpGet("File/Rename/RandomPreview/{maxResults}/{userID}")]
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

        [HttpGet("File/Rename/Preview/{videoLocalID}")]
        public CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed
            {
                VideoLocalID = videoLocalID,
                Success = true
            };
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = "ERROR: Could not find file record";
                    ret.Success = false;
                }
                else
                {
                    ret.VideoLocal = null;
                    if (string.IsNullOrEmpty(vid?.GetBestVideoLocalPlace(true)?.FullServerPath))
                    {
                        ret.VideoLocal = null;
                        ret.Success = false;
                        ret.NewFileName = "ERROR: The file could not be found.";
                        return ret;
                    }
                    ret.NewFileName = RenameFileHelper.GetFilename(vid?.GetBestVideoLocalPlace(), Shoko.Models.Constants.Renamer.TempFileName);

                    if (string.IsNullOrEmpty(ret.NewFileName))
                    {
                        ret.VideoLocal = null;
                        ret.Success = false;
                        ret.NewFileName = "ERROR: The file renamer returned a null or empty value.";
                        return ret;
                    }

                    if (ret.NewFileName.StartsWith("*Error: "))
                    {
                        ret.VideoLocal = null;
                        ret.Success = false;
                        ret.NewFileName = "ERROR: " + ret.NewFileName.Substring(7);
                        return ret;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = $"ERROR: {ex.Message}";
                ret.Success = false;
            }
            return ret;
        }

        [HttpGet("File/Rename/{videoLocalID}/{scriptName}")]
        public CL_VideoLocal_Renamed RenameFile(int videoLocalID, string scriptName)
        {
            return RenameAndMoveFile(videoLocalID, scriptName, false);
        }

        [HttpGet("File/Rename/{videoLocalID}/{scriptName}/{move}")]
        public CL_VideoLocal_Renamed RenameAndMoveFile(int videoLocalID, string scriptName, bool move)
        {
            CL_VideoLocal_Renamed ret = new CL_VideoLocal_Renamed
            {
                VideoLocalID = videoLocalID,
                Success = true
            };
            if (scriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
            {
                ret.VideoLocal = null;
                ret.NewFileName = "ERROR: Do not attempt to use a temp file to rename.";
                ret.Success = false;
                return ret;
            }
            try
            {
                SVR_VideoLocal vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
                if (vid == null)
                {
                    ret.VideoLocal = null;
                    ret.NewFileName = "ERROR: Could not find file record";
                    ret.Success = false;
                    return ret;
                }

                ret.NewFileName = RenameFileHelper.GetFilename(vid?.GetBestVideoLocalPlace(), scriptName);

                if (string.IsNullOrEmpty(ret.NewFileName))
                {
                    ret.VideoLocal = null;
                    ret.Success = false;
                    ret.NewFileName = "ERROR: The file renamer returned a null or empty value.";
                    return ret;
                }

                if (ret.NewFileName.StartsWith("*Error: ", StringComparison.OrdinalIgnoreCase))
                {
                    ret.VideoLocal = null;
                    ret.Success = false;
                    ret.NewFileName = "ERROR: " + ret.NewFileName.Substring(7);
                    return ret;
                }

                if (vid.Places.Count <= 0)
                {
                    ret.VideoLocal = null;
                    ret.Success = false;
                    ret.NewFileName = "ERROR: No Places were found for the VideoLocal. Run Remove Missing Files.";
                    return ret;
                }

                int errorCount = 0;
                string errorString = string.Empty;
                string name = Path.GetFileName(vid.GetBestVideoLocalPlace().FilePath);

                foreach (SVR_VideoLocal_Place place in vid.Places)
                {
                    if (move)
                    {
                        (string, string) resultString = place.MoveWithResultString(scriptName);
                        if (!string.IsNullOrEmpty(resultString.Item2))
                        {
                            errorCount++;
                            errorString = resultString.Item2;
                            continue;
                        }
                        ret.NewDestination = resultString.Item1;
                    }

                    (bool, string, string) output = place.RenameFile(false, scriptName);
                    string error = output.Item3;
                    if (string.IsNullOrEmpty(error)) name = output.Item2;
                    else
                    {
                        errorCount++;
                        errorString = error;
                    }
                }
                if (errorCount >= vid.Places.Count) // should never be greater but shit happens
                {
                    ret.VideoLocal = null;
                    ret.Success = false;
                    ret.NewFileName = errorString;
                    return ret;
                }
                if (ret.VideoLocal == null)
                    ret.VideoLocal = new CL_VideoLocal {VideoLocalID = videoLocalID };
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                ret.VideoLocal = null;
                ret.NewFileName = $"ERROR: {ex.Message}";
                ret.Success = false;
            }
            return ret;
        }

        [NonAction]
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

        [HttpGet("RenameScript")]
        public List<RenameScript> GetAllRenameScripts()
        {
            try
            {
                return RepoFactory.RenameScript.GetAll().Where(a =>
                        !a.ScriptName.EqualsInvariantIgnoreCase(Shoko.Models.Constants.Renamer.TempFileName))
                    .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<RenameScript>();
        }

        [HttpPost("RenameScript")]
        public CL_Response<RenameScript> SaveRenameScript(RenameScript contract)
        {
            CL_Response<RenameScript> response = new CL_Response<RenameScript>
            {
                ErrorMessage = string.Empty,
                Result = null
            };
            try
            {
                RenameScript script = null;
                if (contract.ScriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
                {
                    script = RepoFactory.RenameScript.GetByName(Shoko.Models.Constants.Renamer.TempFileName) ??
                             new RenameScript();
                }
                else if (contract.RenameScriptID != 0)
                {
                    // update
                    script = RepoFactory.RenameScript.GetByID(contract.RenameScriptID);
                    if (script == null)
                    {
                        response.ErrorMessage = "Could not find Rename Script ID: " + contract.RenameScriptID;
                        return response;
                    }
                }
                else
                {
                    //response.ErrorMessage = "Could not find Rename Script ID: " + contract.RenameScriptID;
                    //return response;
                    script = new RenameScript();
                }

                if (string.IsNullOrEmpty(contract.ScriptName))
                {
                    response.ErrorMessage = "Must specify a Script Name";
                    return response;
                }


                if (contract.IsEnabledOnImport == 1)
                {

                    // check to make sure we multiple scripts enable on import (only one can be selected)
                    IReadOnlyList<RenameScript> allScripts = RepoFactory.RenameScript.GetAll();

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
                script.RenamerType = contract.RenamerType;
                script.ExtraData = contract.ExtraData;
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

        [HttpDelete("RenameScript/{renameScriptID}")]
        public string DeleteRenameScript(int renameScriptID)
        {
            try
            {
                RenameScript df = RepoFactory.RenameScript.GetByID(renameScriptID);
                if (df == null) return "Database entry does not exist";
                RepoFactory.RenameScript.Delete(renameScriptID);
                return string.Empty;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        [HttpGet("RenameScript/Types")]
        public IDictionary<string, string> GetScriptTypes()
        {
            return RenameFileHelper.Renamers
                .Select(s => new KeyValuePair<string, string>(s.Key, s.Value.description))
                .ToDictionary(x => x.Key, x => x.Value);
        }

        [HttpGet("AniDB/Recommendation/{animeID}")]
        public List<AniDB_Recommendation> GetAniDBRecommendations(int animeID)
        {
            try
            {
                return RepoFactory.AniDB_Recommendation.GetByAnimeID(animeID).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<AniDB_Recommendation>();
            }
        }

        [HttpGet("AniDB/Anime/Search/{titleQuery}")]
        public List<CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
        {
            List<CL_AnimeSearch> retTitles = new List<CL_AnimeSearch>();


            try
            {
                // check if it is a title search or an ID search
                if (int.TryParse(titleQuery, out int aid))
                {
                    // user is direct entering the anime id

                    // try the local database first
                    // if not download the data from AniDB now
                    SVR_AniDB_Anime anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(aid, false,
                        ServerSettings.Instance.AniDb.DownloadRelatedAnime);
                    if (anime != null)
                    {
                        CL_AnimeSearch res = new CL_AnimeSearch
                        {
                            AnimeID = anime.AnimeID,
                            MainTitle = anime.MainTitle,
                            Titles =
                            new HashSet<string>(anime.AllTitles.Split(new[] { '|' },
                                StringSplitOptions.RemoveEmptyEntries))
                        };

                        // check for existing series and group details
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                        if (ser != null)
                        {
                            res.SeriesExists = true;
                            res.AnimeSeriesID = ser.AnimeSeriesID;
                            res.AnimeSeriesName = anime.GetFormattedTitle();
                        }
                        else
                            res.SeriesExists = false;
                        retTitles.Add(res);
                    }
                }
                else
                {
                    // title search so look at the web cache
                    List<AniDBRaw_AnimeTitle_Anime> titles = AniDB_TitleHelper.Instance.SearchTitle(HttpUtility.UrlDecode(titleQuery));

                    foreach (AniDBRaw_AnimeTitle_Anime tit in titles)
                    {
                        CL_AnimeSearch res = new CL_AnimeSearch
                        {
                            AnimeID = tit.AnimeID,
                            MainTitle = tit.Titles.FirstOrDefault(a =>
                                            a.TitleLanguage == "x-jat" && a.TitleType == "main")?.Title ??
                                        tit.Titles.FirstOrDefault()?.Title,
                            Titles = tit.Titles.Select(a => a.Title).ToHashSet()
                        };

                        // check for existing series and group details
                        SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(tit.AnimeID);
                        if (ser != null)
                        {
                            res.SeriesExists = true;
                            res.AnimeSeriesID = ser.AnimeSeriesID;
                            res.AnimeSeriesName = ser.GetAnime().GetFormattedTitle();
                        }
                        else
                            res.SeriesExists = false;

                        retTitles.Add(res);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return retTitles;
        }

        [HttpGet("AniDB/Anime/Ignore/{userID}")]
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


        [HttpDelete("AniDB/Anime/Ignore/{ignoreAnimeID}")]
        public void RemoveIgnoreAnime(int ignoreAnimeID)
        {
            try
            {
                RepoFactory.IgnoreAnime.Delete(ignoreAnimeID);
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        [HttpGet("Episode/Missing/{userID}/{onlyMyGroups}/{regularEpisodesOnly}/{airingState}")]
        public List<CL_MissingEpisode> GetMissingEpisodes(int userID, bool onlyMyGroups, bool regularEpisodesOnly,
            int airingState)
        {
            List<CL_MissingEpisode> contracts = new List<CL_MissingEpisode>();

            AiringState airState = (AiringState)airingState;

            try
            {
                IReadOnlyList<SVR_AnimeSeries> allSeries = RepoFactory.AnimeSeries.GetAll();
                foreach (SVR_AnimeSeries ser in allSeries)
                {
                    int missingEps = ser.MissingEpisodeCount;
                    if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                    bool finishedAiring = ser.GetAnime().GetFinishedAiring();

                    if (!finishedAiring && airState == AiringState.FinishedAiring) continue;
                    if (finishedAiring && airState == AiringState.StillAiring) continue;

                    if (missingEps <= 0) continue;

                    SVR_AniDB_Anime anime = ser.GetAnime();
                    List<CL_GroupVideoQuality> summ = GetGroupVideoQualitySummary(anime.AnimeID);
                    List<CL_GroupFileSummary> summFiles = GetGroupFileSummary(anime.AnimeID);

                    StringBuilder groupSummaryBuilder = new StringBuilder();
                    StringBuilder groupSummarySimpleBuilder = new StringBuilder();

                    foreach (CL_GroupVideoQuality gvq in summ)
                    {
                        if (groupSummaryBuilder.Length > 0)
                            groupSummaryBuilder.Append(" --- ");

                        groupSummaryBuilder.Append(
                            $"{gvq.GroupNameShort} - {gvq.Resolution}/{gvq.VideoSource}/{gvq.VideoBitDepth}bit ({gvq.NormalEpisodeNumberSummary})");
                    }

                    foreach (CL_GroupFileSummary gfq in summFiles)
                    {
                        if (groupSummarySimpleBuilder.Length > 0)
                            groupSummarySimpleBuilder.Append(", ");

                        groupSummarySimpleBuilder.Append($"{gfq.GroupNameShort} ({gfq.NormalEpisodeNumberSummary})");
                    }

                    // find the missing episodes
                    foreach (SVR_AnimeEpisode aep in ser.GetAnimeEpisodes())
                    {
                        if (aep.AniDB_Episode == null) continue;
                        if (regularEpisodesOnly && aep.EpisodeTypeEnum != EpisodeType.Episode) continue;

                        AniDB_Episode aniep = aep.AniDB_Episode;
                        if (aniep.GetFutureDated()) continue;

                        List<SVR_VideoLocal> vids = aep.GetVideoLocals();

                        if (vids.Count != 0) continue;

                        contracts.Add(new CL_MissingEpisode
                        {
                            AnimeID = ser.AniDB_ID,
                            AnimeSeries = ser.GetUserContract(userID),
                            AnimeTitle = anime.MainTitle,
                            EpisodeID = aniep.EpisodeID,
                            EpisodeNumber = aniep.EpisodeNumber,
                            EpisodeType = aniep.EpisodeType,
                            GroupFileSummary = groupSummaryBuilder.ToString(),
                            GroupFileSummarySimple = groupSummarySimpleBuilder.ToString()
                        });
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

        [HttpGet("AniDB/MyList/Missing/{userID}")]
        public List<CL_MissingFile> GetMyListFilesForRemoval(int userID)
        {
            List<CL_MissingFile> contracts = new List<CL_MissingFile>();
            Dictionary<int, SVR_AniDB_Anime> animeCache = new Dictionary<int, SVR_AniDB_Anime>();
            Dictionary<int, SVR_AnimeSeries> animeSeriesCache = new Dictionary<int, SVR_AnimeSeries>();

            try
            {
                AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password);
                AniDBUDPResponseCode ev = cmd.Process();
                if (ev == AniDBUDPResponseCode.GotMyListHTTP)
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
                                if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
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
                            if (v == null) break;
                            foreach (SVR_VideoLocal_Place p in v.Places)
                            {
                                if (System.IO.File.Exists(p.FullServerPath))
                                {
                                    fileMissing = false;
                                    break;
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


                            CL_MissingFile missingFile = new CL_MissingFile
                            {
                                AnimeID = myitem.AnimeID,
                                AnimeTitle = "Data Missing"
                            };
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

        [HttpDelete("AniDB/MyList/Missing")]
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

        [HttpGet("Series/WithoutFiles/{userID}")]
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

        [HttpGet("Series/MissingEpisodes/{maxRecords}/{userID}")]
        public List<CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int userID)
        {
            try
            {
                SVR_JMMUser user = RepoFactory.JMMUser.GetByID(userID);
                if (user != null)
                    return
                        RepoFactory.AnimeSeries.GetWithMissingEpisodes()
                            .Select(a => a.GetUserContract(userID))
                            .Where(a => a != null)
                            .ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return new List<CL_AnimeSeries_User>();
        }

        [HttpGet("File/Ignored/{userID}")]
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

        //[HttpGet("File/ManuallyLinked/{userID}")]
        [NonAction]
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

        [HttpGet("File/Unrecognised/{userID}")]
        public List<CL_VideoLocal> GetUnrecognisedFiles(int userID)
        {
            List<CL_VideoLocal> contracts = new List<CL_VideoLocal>();
            try
            {
                contracts.AddRange(RepoFactory.VideoLocal.GetVideosWithoutEpisode().Select(vid => vid.ToClient(userID)));
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
            return contracts;
        }

        [HttpPost("File/Unlinked/Rescan")]
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

        [HttpGet("File/Rescan/ManuallyLinked")]
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

        [HttpGet("File/Duplicated")]
        public List<CL_DuplicateFile> GetAllDuplicateFiles()
        {
            List<CL_DuplicateFile> dupFiles = new List<CL_DuplicateFile>();
            try
            {
                return RepoFactory.DuplicateFile.GetAll().Select(a => a.ToClient()).ToList();
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
        [HttpDelete("File/Duplicated/{duplicateFileID}/{fileNumber}")]
        public string DeleteDuplicateFile(int duplicateFileID, int fileNumber)
        {
            try
            {
                DuplicateFile df = RepoFactory.DuplicateFile.GetByID(duplicateFileID);
                if (df == null) return "Database entry does not exist";

                if (fileNumber != 1 && fileNumber != 2) return string.Empty;
                SVR_VideoLocal_Place place;
                switch (fileNumber)
                {
                    case 1:
                        place =
                            RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(df.FilePathFile1,
                                df.ImportFolderIDFile1);
                        break;
                    case 2:
                        place =
                            RepoFactory.VideoLocalPlace.GetByFilePathAndImportFolderID(df.FilePathFile2,
                                df.ImportFolderIDFile2);
                        break;
                    default:
                        place = null;
                        break;
                }
                if (place == null) return "Unable to get VideoLocal_Place";

                return place.RemoveAndDeleteFile().Item2;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return ex.Message;
            }
        }

        [HttpGet("File/ManuallyLinked/{userID}")]
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

        [HttpGet("Episode/ForMultipleFiles/{userID}/{onlyFinishedSeries}/{ignoreVariations}")]
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

        [HttpPost("File/Duplicated/Reevaluate")]
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
                    if (!System.IO.File.Exists(df.GetFullServerPath1()) || !System.IO.File.Exists(df.GetFullServerPath2()))
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

        [HttpGet("File/Detailed/{animeID}/{relGroupName}/{resolution}/{videoSource}/{videoBitDepth}/{userID}")]
        public List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
            string resolution,
            string videoSource, int videoBitDepth, int userID)
        {
            relGroupName = WebUtility.UrlDecode(relGroupName);
            videoSource = WebUtility.UrlDecode(videoSource);

            List<CL_VideoDetailed> vids = new List<CL_VideoDetailed>();

            try
            {
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                if (anime == null) return vids;

                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
                {
                    int thisBitDepth = 8;

                    if (vid.Media?.VideoStream?.BitDepth != null) thisBitDepth = vid.Media.VideoStream.BitDepth;

                    List<SVR_AnimeEpisode> eps = vid.GetAnimeEpisodes();
                    if (eps.Count == 0) continue;

                    bool sourceMatches =
                        videoSource.EqualsInvariantIgnoreCase(string.Intern("Manual Link")) ||
                        videoSource.EqualsInvariantIgnoreCase(string.Intern("unknown"));
                    bool groupMatches = relGroupName.EqualsInvariantIgnoreCase(Constants.NO_GROUP_INFO);
                    // get the anidb file info
                    AniDB_File aniFile = vid.GetAniDBFile();
                    if (aniFile != null)
                    {
                        sourceMatches = videoSource.EqualsInvariantIgnoreCase(aniFile.File_Source) || !sourceMatches &&
                                        aniFile.File_Source.Contains(string.Intern("unknown"),
                                            StringComparison.InvariantCultureIgnoreCase) &&
                                        videoSource.EqualsInvariantIgnoreCase(string.Intern("unknown"));
                        groupMatches =
                            relGroupName.EqualsInvariantIgnoreCase(aniFile.Anime_GroupName) ||
                            relGroupName.EqualsInvariantIgnoreCase(aniFile.Anime_GroupNameShort);
                        if (!aniFile.Anime_GroupNameShort.Equals("raw") &&
                            (aniFile.Anime_GroupName.Contains("unknown") ||
                            aniFile.Anime_GroupNameShort.Contains("unknown")))
                            groupMatches = relGroupName.EqualsInvariantIgnoreCase(Constants.NO_GROUP_INFO);
                    }
                    // Sometimes, especially with older files, the info doesn't quite match for resolution
                    string vidResInfo = vid.VideoResolution;

                    // match based on group / video source / video res
                    if (groupMatches && sourceMatches && thisBitDepth == videoBitDepth &&
                        resolution.EqualsInvariantIgnoreCase(vidResInfo))
                    {
                        vids.Add(vid.ToClientDetailed(userID));
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

        [HttpGet("File/ByGroup/{animeID}/{relGroupName}/{userID}")]
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
                    // get the anibd file info
                    AniDB_File aniFile = vid.GetAniDBFile();
                    if (aniFile != null)
                    {
                        bool groupMatches =
                            relGroupName.EqualsInvariantIgnoreCase(aniFile.Anime_GroupName) ||
                            relGroupName.EqualsInvariantIgnoreCase(aniFile.Anime_GroupNameShort);
                        if (aniFile.Anime_GroupName.EqualsInvariantIgnoreCase("unknown") ||
                            aniFile.Anime_GroupNameShort.EqualsInvariantIgnoreCase("unknown"))
                            groupMatches = relGroupName.EqualsInvariantIgnoreCase(Constants.NO_GROUP_INFO) ||
                                           relGroupName.EqualsInvariantIgnoreCase("unknown");
                        // match based on group / video source / video res
                        if (groupMatches)
                        {
                            vids.Add(vid.ToClientDetailed(userID));
                        }
                    }
                    else
                    {
                        if (relGroupName.EqualsInvariantIgnoreCase(Constants.NO_GROUP_INFO) ||
                            relGroupName.EqualsInvariantIgnoreCase("unknown"))
                        {
                            vids.Add(vid.ToClientDetailed(userID));
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

            if (origSource.EqualsInvariantIgnoreCase("DTV") ||
                origSource.EqualsInvariantIgnoreCase("HDTV") ||
                origSource.EqualsInvariantIgnoreCase("www"))
            {
                return "TV";
            }

            return origSource;
        }

        [HttpGet("AniDB/ReleaseGroup/Quality/{animeID}")]
        public List<CL_GroupVideoQuality> GetGroupVideoQualitySummary(int animeID)
        {
            List<CL_GroupVideoQuality> vidQuals = new List<CL_GroupVideoQuality>();

            List<SVR_VideoLocal> files = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID);
            files.Sort(FileQualityFilter.CompareTo);
            var lookup = files.ToLookup(a =>
            {
                // Fallback on groupID, this will make it easier to distinguish for deletion and grouping
                var anidbFile = a.GetAniDBFile();
                return new
                {
                    GroupName = anidbFile?.Anime_GroupName ?? Constants.NO_GROUP_INFO,
                    GroupNameShort = anidbFile?.Anime_GroupNameShort ?? Constants.NO_GROUP_INFO,
                    File_Source = anidbFile == null
                        ? string.Intern("Manual Link")
                        : anidbFile.File_Source ?? string.Intern("unknown"),
                    a.VideoResolution
                };
            });
            int rank = lookup.Count;
            foreach (var key in lookup)
            {
                CL_GroupVideoQuality contract = new CL_GroupVideoQuality();
                List<SVR_VideoLocal> videoLocals = key.ToList();
                List<SVR_AnimeEpisode> eps = videoLocals.Select(a => a?.GetAnimeEpisodes().FirstOrDefault()).Where(a => a != null).ToList();
                SVR_AniDB_File ani = videoLocals.First().GetAniDBFile();
                contract.AudioStreamCount = videoLocals.First()
                    .Media?.AudioStreams.Count ?? 0;
                contract.IsChaptered =
                    (ani?.IsChaptered ?? (videoLocals.First()?.Media?.MenuStreams.Any() ?? false ? 1 : 0)) == 1;
                contract.FileCountNormal = eps.Count(a => a?.EpisodeTypeEnum == EpisodeType.Episode);
                contract.FileCountSpecials = eps.Count(a => a?.EpisodeTypeEnum == EpisodeType.Special);
                contract.GroupName = key.Key.GroupName;
                contract.GroupNameShort = key.Key.GroupNameShort;
                contract.NormalEpisodeNumbers = eps.Where(a => a?.EpisodeTypeEnum == EpisodeType.Episode)
                    .Select(a => a.AniDB_Episode.EpisodeNumber).OrderBy(a => a).ToList();
                contract.NormalEpisodeNumberSummary = contract.NormalEpisodeNumbers.ToRanges();
                contract.Ranking = rank;
                contract.Resolution = key.Key.VideoResolution;
                contract.TotalFileSize = videoLocals.Sum(a => a?.FileSize ?? 0);
                contract.TotalRunningTime = videoLocals.Sum(a => a?.Duration ?? 0);
                contract.VideoSource = key.Key.File_Source;
                int? bitDepth = videoLocals.First().Media?.VideoStream?.BitDepth;
                if (bitDepth != null)
                {
                    contract.VideoBitDepth = bitDepth.Value;
                }
                vidQuals.Add(contract);

                rank--;
            }

            return vidQuals;
        }

        [HttpGet("Group/Summary/{animeID}")]
        public List<CL_GroupFileSummary> GetGroupFileSummary(int animeID)
        {
            try
            {
                var videoQuality = GetGroupVideoQualitySummary(animeID);
                return videoQuality.Select(a => new CL_GroupFileSummary
                {
                    FileCountNormal = a.FileCountNormal,
                    FileCountSpecials = a.FileCountSpecials,
                    GroupName = a.GroupName,
                    GroupNameShort = a.GroupNameShort,
                    TotalFileSize = a.TotalFileSize,
                    TotalRunningTime = a.TotalRunningTime,
                    NormalComplete = a.NormalComplete,
                    SpecialsComplete = a.SpecialsComplete,
                    NormalEpisodeNumbers = a.NormalEpisodeNumbers,
                    NormalEpisodeNumberSummary = a.NormalEpisodeNumberSummary
                }).ToList();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
                return new List<CL_GroupFileSummary>();
            }
        }

        [HttpGet("AniDB/AVDumpFile/{vidLocalID}")]
        public string AVDumpFile(int vidLocalID)
        {
            return AVDumpHelper.DumpFile(vidLocalID);
        }
    }
}