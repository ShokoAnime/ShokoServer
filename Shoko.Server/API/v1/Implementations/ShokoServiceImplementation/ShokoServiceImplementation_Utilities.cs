using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using F23.StringSimilarity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server;

public partial class ShokoServiceImplementation
{
    [HttpPost("Series/SearchFilename/{uid}")]
    public List<CL_AnimeSeries_User> SearchSeriesWithFilename(int uid, [FromForm] string query)
    {
        var user = RepoFactory.JMMUser.GetByID(uid);
        if (user is null)
            return new();

        var series = SeriesSearch.SearchSeries(user, query, 200,
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

        var newItems = values.Select(s => SanitizeFuzzy(s, replaceinvalid)).ToList();

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
        var originalLength = value.Length;
        var releaseGroups = RepoFactory.AniDB_ReleaseGroup.GetAllReleaseGroups();
        foreach (var releaseGroup in releaseGroups)
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

    private static double GetLowestLevenshteinDistance(IList<string> languagePreference, SVR_AnimeSeries a, string query)
    {
        if (a?.Contract?.AniDBAnime?.AniDBAnime.AllTitles == null) return 1;
        double dist = 1;
        var dice = new SorensenDice();
        var languages = new HashSet<string> {"en", "x-jat"};
        languages.UnionWith(languagePreference.Select(b => b.ToLower()));
            
        foreach (var title in RepoFactory.AniDB_Anime_Title.GetByAnimeID(a.AniDB_ID)
                     .Where(b => b.TitleType != Shoko.Plugin.Abstractions.DataModels.TitleType.Short && languages.Contains(b.LanguageCode))
                     .Select(b => b.Title?.ToLowerInvariant()).ToList())
        {
            if (string.IsNullOrEmpty(title)) continue;
            var newTitle = SanitizeFuzzy(title, true);
            var newDist = dice.Distance(newTitle, query);
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
        var input = query ?? string.Empty;
        input = input.ToLower(CultureInfo.InvariantCulture);
        input = SanitizeFuzzy(input, true);

        var user = RepoFactory.JMMUser.GetByID(uid);
        var series_list = new List<CL_AniDB_Anime>();
        if (user == null) return series_list;

        var languagePreference = _settingsProvider.GetSettings().LanguagePreference;
        var series = RepoFactory.AnimeSeries.GetAll()
            .Where(a => a?.Contract?.AniDBAnime?.AniDBAnime != null)
            .AsParallel().Select(a => (a, GetLowestLevenshteinDistance(languagePreference, a, input))).OrderBy(a => a.Item2)
            .ThenBy(a => a.Item1.GetSeriesName())
            .Select(a => a.Item1).ToList();

        foreach (var ser in series)
        {
            series_list.Add(ser.GetAnime().Contract.AniDBAnime);
        }

        return series_list;
    }

    [HttpGet("ReleaseGroups")]
    public List<string> GetAllReleaseGroups()
    {
        return RepoFactory.AniDB_ReleaseGroup.GetAllReleaseGroups().ToList();
    }

    [HttpGet("File/DeleteMultipleFilesWithPreferences/{userID}")]
    public bool DeleteMultipleFilesWithPreferences(int userID)
    {
        try
        {
            var epContracts = GetAllEpisodesWithMultipleFiles(userID, false, true);
            var eps =
                epContracts.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.AniDB_EpisodeID))
                    .Where(b => b != null)
                    .ToList();

            var videosToDelete = new List<SVR_VideoLocal>();

            foreach (var ep in eps)
            {
                var videoLocals = ep.GetVideoLocals();
                videoLocals.Sort(FileQualityFilter.CompareTo);
                var keep = videoLocals
                    .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .ToList();
                foreach (var vl2 in keep) videoLocals.Remove(vl2);
                videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                videosToDelete.AddRange(videoLocals);
            }

            var result = true;
            foreach (var toDelete in videosToDelete)
            {
                result &= toDelete.Places.All(a =>
                {
                    try
                    {
                        a.RemoveRecordAndDeletePhysicalFile();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, ex.ToString());
                        return false;
                        
                    }
                });
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
        var epContracts = GetAllEpisodesWithMultipleFiles(userID, false, true);
        var eps =
            epContracts.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.AniDB_EpisodeID))
                .Where(b => b != null)
                .ToList();

        var videosToDelete = new List<SVR_VideoLocal>();

        foreach (var ep in eps)
        {
            var videoLocals = ep.GetVideoLocals();
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep) videoLocals.Remove(vl2);
            videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

            videosToDelete.AddRange(videoLocals);
        }
        return videosToDelete.Select(a => a.ToClient(userID)).ToList();
    }

    [HttpGet("File/GetMultipleFilesForDeletionByPreferences/{userID}")]
    public List<CL_VideoDetailed> GetMultipleFilesForDeletionByPreferences(int userID)
    {
        var epContracts = GetAllEpisodesWithMultipleFiles(userID, false, true);
        var eps =
            epContracts.Select(a => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(a.AniDB_EpisodeID))
                .Where(b => b != null)
                .ToList();

        var videosToDelete = new List<SVR_VideoLocal>();

        foreach (var ep in eps)
        {
            var videoLocals = ep.GetVideoLocals();
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep) videoLocals.Remove(vl2);
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
        return null;
    }

    [HttpDelete("FFDShowPreset/{videoLocalID}")]
    public void DeleteFFDPreset(int videoLocalID)
    {
        // noop
    }

    [HttpPost("FFDShowPreset")]
    public void SaveFFDPreset(FileFfdshowPreset preset)
    {
        // noop
    }

    [HttpGet("File/Search/{searchType}/{searchCriteria}/{userID}")]
    public List<CL_VideoLocal> SearchForFiles(int searchType, string searchCriteria, int userID)
    {
        try
        {
            var vids = new List<CL_VideoLocal>();

            var sType = (FileSearchCriteria)searchType;


            switch (sType)
            {
                case FileSearchCriteria.Name:
                    var results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Trim());
                    vids.AddRange(results1.Select(vid => vid.ToClient(userID)));
                    results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Replace('+', ' ').Trim());
                    vids.AddRange(results1.Select(vid => vid.ToClient(userID)));
                    break;

                case FileSearchCriteria.ED2KHash:
                    var vidl = RepoFactory.VideoLocal.GetByHash(searchCriteria.Trim());
                    if (vidl != null)
                        vids.Add(vidl.ToClient(userID));
                    break;

                case FileSearchCriteria.Size:
                    break;

                case FileSearchCriteria.LastOneHundred:
                    var number = 100;
                    if (!string.IsNullOrEmpty(searchCriteria))
                    {
                        if (int.TryParse(searchCriteria, out var temp)) number = temp;
                    }
                    var results2 = RepoFactory.VideoLocal.GetMostRecentlyAdded(number, userID);
                    vids.AddRange(results2.Select(vid => vid.ToClient(userID)));
                    break;
            }

            return vids.DistinctBy(a => a.VideoLocalID).ToList();
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
        var ret = new CL_VideoLocal_Renamed
        {
            VideoLocalID = videoLocalID,
            Success = true
        };
        try
        {
            var vid = RepoFactory.VideoLocal.GetByID(videoLocalID);
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
        var ret = new CL_VideoLocal_Renamed
        {
            VideoLocalID = videoLocalID,
            VideoLocal = null,
            Success = false,
        };
        if (scriptName.Equals(Shoko.Models.Constants.Renamer.TempFileName))
        {
            ret.NewFileName = "ERROR: Do not attempt to use a temp file to rename.";
            return ret;
        }
        try
        {
            var file = RepoFactory.VideoLocal.GetByID(videoLocalID);
            if (file == null)
            {
                ret.NewFileName = "ERROR: Could not find file.";
                return ret;
            }

            var allLocations = file.Places;
            if (allLocations.Count <= 0)
            {
                ret.NewFileName = "ERROR: No locations were found for the file. Run the \"Remove Missing Files\" action to remove the file.";
                return ret;
            }

            // First do a dry-run on the best location.
            var bestLocation = file.GetBestVideoLocalPlace();
            var previewResult = bestLocation.AutoRelocateFile(new() { Preview = true, ScriptName = scriptName, SkipMove = !move });
            if (!previewResult.Success)
            {
                ret.NewFileName = $"ERROR: {previewResult.ErrorMessage}";
                return ret;
            }

            // Relocate the file locations.
            var fullPath = string.Empty;
            var errorString = string.Empty;
            foreach (var place in allLocations)
            {
                var result = place.AutoRelocateFile(new() { ScriptName = scriptName, SkipMove = !move });
                if (result.Success)
                    fullPath = result.FullServerPath;
                else
                    errorString = result.ErrorMessage;
            }
            if (!string.IsNullOrEmpty(errorString))
            {
                ret.NewFileName = errorString;
                return ret;
            }

            // Return the full path if we moved, otherwise return the file name.
            ret.Success = true;
            ret.VideoLocal = new CL_VideoLocal { VideoLocalID = videoLocalID };
            ret.NewFileName = move ? fullPath : Path.GetFileName(fullPath);
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            ret.NewFileName = $"ERROR: {ex.Message}";
        }
        return ret;
    }

    [NonAction]
    public List<CL_VideoLocal_Renamed> RenameFiles(List<int> videoLocalIDs, string renameRules)
    {
        var ret = new List<CL_VideoLocal_Renamed>();
        try
        {
            foreach (var vid in videoLocalIDs)
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
        var response = new CL_Response<RenameScript>
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
                var allScripts = RepoFactory.RenameScript.GetAll();

                foreach (var rs in allScripts)
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
            var df = RepoFactory.RenameScript.GetByID(renameScriptID);
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
        return new List<AniDB_Recommendation>();
    }

    [HttpGet("AniDB/Anime/Search/{titleQuery}")]
    public List<CL_AnimeSearch> OnlineAnimeTitleSearch(string titleQuery)
    {
        var retTitles = new List<CL_AnimeSearch>();

        try
        {
            // check if it is a title search or an ID search
            if (int.TryParse(titleQuery, out var aid))
            {
                // user is direct entering the anime id

                // try the local database first
                // if not download the data from AniDB now
                var command = _commandFactory.Create<CommandRequest_GetAnimeHTTP>(c =>
                {
                    c.AnimeID = aid;
                    c.BubbleExceptions = true;
                    c.ForceRefresh = false;
                    c.DownloadRelations = false;
                });
                command.ProcessCommand();
                var anime = command.Result;

                if (anime != null)
                {
                    var res = new CL_AnimeSearch
                    {
                        AnimeID = anime.AnimeID,
                        MainTitle = anime.MainTitle,
                        Titles = new HashSet<string>(anime.AllTitles.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries)),
                    };

                    // check for existing series and group details
                    var ser = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                    if (ser != null)
                    {
                        res.SeriesExists = true;
                        res.AnimeSeriesID = ser.AnimeSeriesID;
                        res.AnimeSeriesName = anime.PreferredTitle;
                    }
                    else
                        res.SeriesExists = false;
                    retTitles.Add(res);
                }
            }
            else
            {
                // title search so look at the web cache
                foreach (var tit in Utils.AniDBTitleHelper.SearchTitle(HttpUtility.UrlDecode(titleQuery)))
                {
                    var res = new CL_AnimeSearch
                    {
                        AnimeID = tit.AnimeID,
                        MainTitle = tit.MainTitle,
                        Titles = tit.Titles.Select(a => a.Title).ToHashSet()
                    };

                    // check for existing series and group details
                    var ser = RepoFactory.AnimeSeries.GetByAnimeID(tit.AnimeID);
                    if (ser != null)
                    {
                        res.SeriesExists = true;
                        res.AnimeSeriesID = ser.AnimeSeriesID;
                        res.AnimeSeriesName = ser.GetAnime().PreferredTitle;
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
            var user = RepoFactory.JMMUser.GetByID(userID);
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
        var result = new List<CL_MissingEpisode>();

        var airState = (AiringState)airingState;

        try
        {
            var allSeries = RepoFactory.AnimeSeries.GetAll();
            var temp = allSeries.AsParallel().SelectMany(ser =>
            {
                var missingEps = ser.MissingEpisodeCount;
                if (onlyMyGroups) missingEps = ser.MissingEpisodeCountGroups;

                var finishedAiring = ser.GetAnime().GetFinishedAiring();

                if (!finishedAiring && airState == AiringState.FinishedAiring) return Array.Empty<CL_MissingEpisode>();
                if (finishedAiring && airState == AiringState.StillAiring) return Array.Empty<CL_MissingEpisode>();

                if (missingEps <= 0) return Array.Empty<CL_MissingEpisode>();

                var anime = ser.GetAnime();
                var summ = GetGroupVideoQualitySummary(anime.AnimeID);
                var summFiles = GetGroupFileSummary(anime.AnimeID);

                var groupSummaryBuilder = new StringBuilder();
                var groupSummarySimpleBuilder = new StringBuilder();

                foreach (var gvq in summ)
                {
                    if (groupSummaryBuilder.Length > 0)
                        groupSummaryBuilder.Append(" --- ");

                    groupSummaryBuilder.Append(
                        $"{gvq.GroupNameShort} - {gvq.Resolution}/{gvq.VideoSource}/{gvq.VideoBitDepth}bit ({gvq.NormalEpisodeNumberSummary})");
                }

                foreach (var gfq in summFiles)
                {
                    if (groupSummarySimpleBuilder.Length > 0)
                        groupSummarySimpleBuilder.Append(", ");

                    groupSummarySimpleBuilder.Append($"{gfq.GroupNameShort} ({gfq.NormalEpisodeNumberSummary})");
                }

                // find the missing episodes

                return ser.GetAnimeEpisodes()
                    .Where(aep =>
                        aep.AniDB_Episode != null && aep.GetVideoLocals().Count == 0 &&
                        (!regularEpisodesOnly || aep.EpisodeTypeEnum == EpisodeType.Episode))
                    .Select(aep => aep.AniDB_Episode)
                    .Where(aniep => !aniep.GetFutureDated())
                    .Select(aniep => new CL_MissingEpisode
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
            }).ToList().OrderBy(a => a.AnimeTitle)
                .ThenBy(a => a.EpisodeType)
                .ThenBy(a => a.EpisodeNumber)
                .ToList();
            result = temp;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }
        return result;
    }

    [HttpGet("AniDB/MyList/Missing/{userID}")]
    public List<CL_MissingFile> GetMyListFilesForRemoval(int userID)
    {
        // TODO maybe rework this
        var contracts = new List<CL_MissingFile>();
        var animeCache = new Dictionary<int, SVR_AniDB_Anime>();
        var animeSeriesCache = new Dictionary<int, SVR_AnimeSeries>();

        try
        {
            var requestFactory = HttpContext.RequestServices.GetRequiredService<IRequestFactory>();
            var settings = _settingsProvider.GetSettings();
            var request = requestFactory.Create<RequestMyList>(
                r =>
                {
                    r.Username = settings.AniDb.Username;
                    r.Password = settings.AniDb.Password;
                }
            );
            var response = request.Execute();
            if (response.Response != null)
            {
                foreach (var myitem in response.Response)
                {
                    // let's check if the file on AniDB actually exists in the user's local collection
                    var hash = string.Empty;

                    AniDB_File anifile = myitem.FileID == null ? null : RepoFactory.AniDB_File.GetByFileID(myitem.FileID.Value);
                    if (anifile != null)
                        hash = anifile.Hash;
                    else
                    {
                        // look for manually linked files
                        var xrefs = myitem.EpisodeID == null ? null : RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID.Value);
                        foreach (var xref in xrefs)
                        {
                            if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
                            {
                                hash = xref.Hash;
                                break;
                            }
                        }
                    }

                    var fileMissing = false;
                    if (string.IsNullOrEmpty(hash))
                        fileMissing = true;
                    else
                    {
                        // now check if the file actually exists on disk
                        var v = RepoFactory.VideoLocal.GetByHash(hash);
                        fileMissing = true;
                        if (v == null) break;
                        foreach (var p in v.Places)
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
                        if (myitem.AnimeID != null)
                        {
                            if (animeCache.ContainsKey(myitem.AnimeID.Value))
                                anime = animeCache[myitem.AnimeID.Value];
                            else
                            {
                                anime = RepoFactory.AniDB_Anime.GetByAnimeID(myitem.AnimeID.Value);
                                animeCache[myitem.AnimeID.Value] = anime;
                            }

                            SVR_AnimeSeries ser = null;
                            if (animeSeriesCache.ContainsKey(myitem.AnimeID.Value))
                                ser = animeSeriesCache[myitem.AnimeID.Value];
                            else
                            {
                                ser = RepoFactory.AnimeSeries.GetByAnimeID(myitem.AnimeID.Value);
                                animeSeriesCache[myitem.AnimeID.Value] = ser;
                            }


                            var missingFile = new CL_MissingFile { AnimeID = myitem.AnimeID.Value, AnimeTitle = "Data Missing" };
                            if (anime != null) missingFile.AnimeTitle = anime.MainTitle;
                            missingFile.EpisodeID = myitem.EpisodeID ?? 0;
                            var ep = myitem.EpisodeID == null ? null : RepoFactory.AniDB_Episode.GetByEpisodeID(myitem.EpisodeID.Value);
                            missingFile.EpisodeNumber = -1;
                            missingFile.EpisodeType = 1;
                            if (ep != null)
                            {
                                missingFile.EpisodeNumber = ep.EpisodeNumber;
                                missingFile.EpisodeType = ep.EpisodeType;
                            }

                            missingFile.FileID = myitem.FileID ?? 0;

                            if (ser == null) missingFile.AnimeSeries = null;
                            else missingFile.AnimeSeries = ser.GetUserContract(userID);

                            contracts.Add(missingFile);
                        }
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
        // TODO maybe rework this
        foreach (var missingFile in myListFiles)
        {
            var vl = RepoFactory.VideoLocal.GetByMyListID(missingFile.FileID);
            if (vl == null) continue;
            _commandFactory.CreateAndSave<CommandRequest_DeleteFileFromMyList>(
                c =>
                {
                    c.Hash = vl.Hash;
                    c.FileSize = vl.FileSize;
                }
            );

            // For deletion of files from Trakt, we will rely on the Daily sync
            // lets also try removing from the users trakt collection
        }
    }

    [HttpGet("Series/WithoutFiles/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesWithoutAnyFiles(int userID)
    {
        var contracts = new List<CL_AnimeSeries_User>();

        try
        {
            foreach (var ser in RepoFactory.AnimeSeries.GetAll())
            {
                if (RepoFactory.VideoLocal.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                {
                    var can = ser.GetUserContract(userID);
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
            var user = RepoFactory.JMMUser.GetByID(userID);
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
        var contracts = new List<CL_VideoLocal>();
        try
        {
            foreach (var vid in RepoFactory.VideoLocal.GetIgnoredVideos())
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
        var contracts = new List<CL_VideoLocal>();
        try
        {
            foreach (var vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
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
        var contracts = new List<CL_VideoLocal>();
        try
        {
            contracts.AddRange(RepoFactory.VideoLocal.GetVideosWithoutEpisode(true).Select(vid => vid.ToClient(userID)));
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
            var filesWithoutEpisode = RepoFactory.VideoLocal.GetVideosWithoutEpisode();

            foreach (var vl in filesWithoutEpisode.Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                    c =>
                    {
                        c.VideoLocalID = vl.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                );
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
            var files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();

            foreach (var vl in files.Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                _commandFactory.CreateAndSave<CommandRequest_ProcessFile>(
                    c =>
                    {
                        c.VideoLocalID = vl.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                );
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
        try
        {
            return RepoFactory.VideoLocal.GetExactDuplicateVideos()
                .SelectMany(file =>
                {
                    var episode = file.GetAnimeEpisodes()
                        .FirstOrDefault();
                    var anidbEpisode = episode?.AniDB_Episode;
                    var seriesName = episode?.GetAnimeSeries()?.GetSeriesName();
                    var allLocations = file.Places;
                    var bestLocation = file.GetBestVideoLocalPlace();
                    return allLocations
                        .Where(location => location.VideoLocal_Place_ID != bestLocation.VideoLocal_Place_ID)
                        .Select(location =>
                        {
                            var duplicateFile = new CL_DuplicateFile()
                            {
                                DuplicateFileID = GetFakeDuplicteID(bestLocation.VideoLocal_Place_ID, location.VideoLocal_Place_ID),
                                FilePathFile2 = location.FilePath,
                                FilePathFile1 = bestLocation.FilePath,
                                Hash = file.Hash,
                                ImportFolderIDFile1 = bestLocation.ImportFolderID,
                                ImportFolderIDFile2 = location.ImportFolderID,
                                ImportFolder1 = bestLocation.ImportFolder,
                                ImportFolder2 = location.ImportFolder,
                                DateTimeUpdated = file.DateTimeUpdated,
                            };
                            if (episode != null && anidbEpisode != null)
                            {
                                duplicateFile.EpisodeName = episode.Title;
                                duplicateFile.EpisodeNumber = anidbEpisode.EpisodeNumber;
                                duplicateFile.EpisodeType = anidbEpisode.EpisodeType;
                                duplicateFile.AnimeID = anidbEpisode.AnimeID;
                                duplicateFile.AnimeName = seriesName;
                            }
                            return duplicateFile;
                        });
                })
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return new();
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
            var (placeID1, placeID2) = GetLocationIDsFromFakeDuplicateID(duplicateFileID);
            if (placeID1 == 0 || placeID2 == 0)
                return "Unable to get VideoLocal_Place ids, refresh the view to fix.";
            var place = (fileNumber) switch
            {
                1 => RepoFactory.VideoLocalPlace.GetByID(placeID1),
                2 => RepoFactory.VideoLocalPlace.GetByID(placeID2),
                _ => null,
            };
            if (place == null)
                return "Unable to get VideoLocal_Place";

            RemoveFakeDuplicateID(duplicateFileID);
            place.RemoveRecordAndDeletePhysicalFile();
            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
            return ex.Message;
        }
    }

    private int IDCounter = 1;

    private readonly Dictionary<int, (int, int)> Lookup = new();

    private readonly Dictionary<(int, int), int> ReverseLookup = new();

    [NonAction]
    private int GetFakeDuplicteID(int fileLocationID1, int fileLocationID2)
    {
        var tuple = (fileLocationID1, fileLocationID2);
        if (!ReverseLookup.TryGetValue(tuple, out var fakeDuplicateID))
        {
            ReverseLookup.Add(tuple, fakeDuplicateID = IDCounter++);
            Lookup.Add(fakeDuplicateID, tuple);
        }
        return fakeDuplicateID;
    }

    [NonAction]
    private (int location1, int location2) GetLocationIDsFromFakeDuplicateID(int fakeDuplicateID)
    {
        if (Lookup.TryGetValue(fakeDuplicateID, out var tuple))
            return tuple;
        return (0, 0);
    }

    [NonAction]
    private void RemoveFakeDuplicateID(int fakeDuplicateID)
    {
        var tuple = Lookup[fakeDuplicateID];
        ReverseLookup.Remove(tuple);
        Lookup.Remove(fakeDuplicateID);
    }

    [HttpGet("File/ManuallyLinked/{userID}")]
    public List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID)
    {
        var manualFiles = new List<CL_VideoLocal>();
        try
        {
            foreach (var vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
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
        var eps = new List<CL_AnimeEpisode_User>();
        try
        {
            var dictSeriesAnime = new Dictionary<int, int>();
            var dictAnimeFinishedAiring = new Dictionary<int, bool>();
            var dictSeriesFinishedAiring = new Dictionary<int, bool>();

            if (onlyFinishedSeries)
            {
                var allSeries = RepoFactory.AnimeSeries.GetAll();
                foreach (var ser in allSeries)
                    dictSeriesAnime[ser.AnimeSeriesID] = ser.AniDB_ID;

                var allAnime = RepoFactory.AniDB_Anime.GetAll();
                foreach (var anime in allAnime)
                    dictAnimeFinishedAiring[anime.AnimeID] = anime.GetFinishedAiring();

                foreach (var kvp in dictSeriesAnime)
                {
                    if (dictAnimeFinishedAiring.ContainsKey(kvp.Value))
                        dictSeriesFinishedAiring[kvp.Key] = dictAnimeFinishedAiring[kvp.Value];
                }
            }

            foreach (var ep in RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations))
            {
                if (onlyFinishedSeries)
                {
                    var finishedAiring = false;
                    if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                        finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                    if (!finishedAiring) continue;
                }
                var cep = ep.GetUserContract(userID);
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
    public void ReevaluateDuplicateFiles() { }

    [HttpGet("File/Detailed/{animeID}/{relGroupName}/{resolution}/{videoSource}/{videoBitDepth}/{userID}")]
    public List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
        string resolution,
        string videoSource, int videoBitDepth, int userID)
    {
        relGroupName = relGroupName == null ? null : Uri.UnescapeDataString(relGroupName.Replace("+", " "));
        videoSource = videoSource == null ? null : Uri.UnescapeDataString(videoSource.Replace("+", " "));
        logger.Trace($"GetFilesByGroupAndResolution -- relGroupName: {relGroupName}");
        logger.Trace($"GetFilesByGroupAndResolution -- videoSource: {videoSource}");

        var vids = new List<CL_VideoDetailed>();

        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null) return vids;

            foreach (var vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
            {
                var thisBitDepth = 8;

                if (vid.Media?.VideoStream?.BitDepth != null) thisBitDepth = vid.Media.VideoStream.BitDepth;
                
                // Sometimes, especially with older files, the info doesn't quite match for resolution
                var vidResInfo = vid.VideoResolution;
                
                logger.Trace($"GetFilesByGroupAndResolution -- thisBitDepth: {thisBitDepth}");
                logger.Trace($"GetFilesByGroupAndResolution -- videoBitDepth: {videoBitDepth}");

                logger.Trace($"GetFilesByGroupAndResolution -- vidResInfo: {vidResInfo}");
                logger.Trace($"GetFilesByGroupAndResolution -- resolution: {resolution}");

                var eps = vid.GetAnimeEpisodes();
                if (eps.Count == 0) continue;

                var sourceMatches =
                    "Manual Link".EqualsInvariantIgnoreCase(videoSource) ||
                    "unknown".EqualsInvariantIgnoreCase(videoSource);
                var groupMatches = Constants.NO_GROUP_INFO.EqualsInvariantIgnoreCase(relGroupName);
                logger.Trace($"GetFilesByGroupAndResolution -- sourceMatches (manual/unkown): {sourceMatches}");
                logger.Trace($"GetFilesByGroupAndResolution -- groupMatches (NO GROUP INFO): {groupMatches}");
                
                // get the anidb file info
                var aniFile = vid.GetAniDBFile();
                if (aniFile != null)
                {
                    logger.Trace($"GetFilesByGroupAndResolution -- aniFile is not null");
                    logger.Trace($"GetFilesByGroupAndResolution -- aniFile.File_Source: {aniFile.File_Source}");
                    logger.Trace($"GetFilesByGroupAndResolution -- aniFile.Anime_GroupName: {aniFile.Anime_GroupName}");
                    logger.Trace($"GetFilesByGroupAndResolution -- aniFile.Anime_GroupNameShort: {aniFile.Anime_GroupNameShort}");
                    sourceMatches = string.Equals(videoSource, aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase) || !sourceMatches &&
                        (aniFile.File_Source?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false) &&
                        string.Equals("unknown", videoSource, StringComparison.InvariantCultureIgnoreCase);

                    if (!string.IsNullOrEmpty(aniFile.Anime_GroupName) || !string.IsNullOrEmpty(aniFile.Anime_GroupNameShort))
                        groupMatches = string.Equals(relGroupName, aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) ||
                                       string.Equals(relGroupName, aniFile.Anime_GroupNameShort, StringComparison.InvariantCultureIgnoreCase);

                    if (!"raw".Equals(aniFile.Anime_GroupNameShort) &&
                        ((aniFile.Anime_GroupName?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                         (aniFile.Anime_GroupNameShort?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false)))
                        groupMatches = Constants.NO_GROUP_INFO.EqualsInvariantIgnoreCase(relGroupName);

                    logger.Trace($"GetFilesByGroupAndResolution -- sourceMatches (aniFile): {sourceMatches}");
                    logger.Trace($"GetFilesByGroupAndResolution -- groupMatches (aniFile): {groupMatches}");
                }

                // match based on group / video source / video res
                if (groupMatches && sourceMatches && thisBitDepth == videoBitDepth &&
                    string.Equals(resolution, vidResInfo, StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.Trace($"GetFilesByGroupAndResolution -- File Matched: {vid.FileName}");
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
        var grpName = relGroupName == null ? null : Uri.UnescapeDataString(relGroupName.Replace("+", " "));
        var vids = new List<CL_VideoDetailed>();

        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null) return vids;

            foreach (var vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
            {
                var eps = vid.GetAnimeEpisodes();
                if (eps.Count == 0) continue;
                // get the anibd file info
                var aniFile = vid.GetAniDBFile();
                if (aniFile != null)
                {
                    var groupMatches = string.Equals(grpName, aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) ||
                                       string.Equals(grpName, aniFile.Anime_GroupNameShort, StringComparison.InvariantCultureIgnoreCase);
                    if ("unknown".EqualsInvariantIgnoreCase(aniFile.Anime_GroupName) || "unknown".EqualsInvariantIgnoreCase(aniFile.Anime_GroupNameShort) ||
                        string.IsNullOrEmpty(aniFile.Anime_GroupName) && string.IsNullOrEmpty(aniFile.Anime_GroupNameShort))
                        groupMatches = string.Equals(grpName, Constants.NO_GROUP_INFO) || "unknown".EqualsInvariantIgnoreCase(grpName);
                    // match based on group / video source / video res
                    if (groupMatches)
                    {
                        vids.Add(vid.ToClientDetailed(userID));
                    }
                }
                else
                {
                    if (string.Equals(grpName, Constants.NO_GROUP_INFO, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(grpName, "unknown"))
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
        var vidQuals = new List<CL_GroupVideoQuality>();

        var files = RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID);
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
        var rank = lookup.Count;
        foreach (var key in lookup)
        {
            var contract = new CL_GroupVideoQuality();
            var videoLocals = key.ToList();
            var eps = videoLocals.Select(a => a?.GetAnimeEpisodes().FirstOrDefault()).Where(a => a != null).ToList();
            var ani = videoLocals.First().GetAniDBFile();
            contract.AudioStreamCount = videoLocals.First()
                .Media?.AudioStreams.Count ?? 0;
            contract.IsChaptered =
                ani?.IsChaptered ?? (videoLocals.First()?.Media?.MenuStreams.Any() ?? false);
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
            var bitDepth = videoLocals.First().Media?.VideoStream?.BitDepth;
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
        var video = RepoFactory.VideoLocal.GetByID(vidLocalID);
        if (video == null)
        {
            return "Unable to get VideoLocal with id: " + vidLocalID;
        }

        var filePath = video.GetBestVideoLocalPlace(true)?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
        {
            return "Unable to get file location for VideoLocal with id: " + video.VideoLocalID;
        }

        var command = _commandFactory.Create<CommandRequest_AVDumpFile>(
            c => c.Videos = new() { { vidLocalID, filePath } }
        );
        command.BubbleExceptions = true;
        command.ProcessCommand();

        var output = command.Result.StandardOutput.Replace("\n", "\r\n");
        if (command.Result.IsSuccess)
            output += $"--------------------------------------------------------------------------------\r\n\r\n{string.Join("\r\n", command.Result.ED2Ks)}\r\n\r\nDumping successful.";
        else
            output += $"--------------------------------------------------------------------------------\r\n\r\nFailed to complete AVDump session {command.Result.SessionID}:\r\n\r\nFiles:\r\n{string.Join("\r\n", command.Result.AbsolutePaths)}\r\n\r\nStandard Output:\r\n{command.Result.StandardOutput}{(command.Result.StandardError.Length > 0 ? $"\r\nStandard Error:\r\n{command.Result.StandardError}" : "")}";
        return output;
    }
}
