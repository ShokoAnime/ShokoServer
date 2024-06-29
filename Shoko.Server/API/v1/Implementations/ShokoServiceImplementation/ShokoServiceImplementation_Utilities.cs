using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using F23.StringSimilarity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Services;
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
            SeriesSearch.SearchFlags.Titles | SeriesSearch.SearchFlags.Fuzzy, searchById: true);

        var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
        return series.Select(a => a.Result).Select(ser => seriesService.GetV1UserContract(ser, uid)).ToList();
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
        var titles = a.AniDB_Anime.GetAllTitles();
        if ((titles?.Count ?? 0) == 0) return 1;
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
        var aniDBAnimeService = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeService>();
        var input = query ?? string.Empty;
        input = input.ToLower(CultureInfo.InvariantCulture);
        input = SanitizeFuzzy(input, true);

        var user = RepoFactory.JMMUser.GetByID(uid);
        if (user == null) return [];

        var languagePreference = _settingsProvider.GetSettings().LanguagePreference;
        var animeResults = RepoFactory.AnimeSeries.GetAll()
            .AsParallel().Select(a => (a, GetLowestLevenshteinDistance(languagePreference, a, input))).OrderBy(a => a.Item2)
            .ThenBy(a => a.Item1.SeriesName)
            .Select(a => a.Item1.AniDB_Anime).ToList();

        var seriesList = animeResults.Select(anime => aniDBAnimeService.GetV1Contract(anime)).ToList();
        return seriesList;
    }

    [HttpGet("ReleaseGroups")]
    public List<string> GetAllReleaseGroups()
    {
        return RepoFactory.AniDB_ReleaseGroup.GetUsedReleaseGroups().Select(r => r.GroupName).ToList();
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
                var videoLocals = ep.VideoLocals;
                videoLocals.Sort(FileQualityFilter.CompareTo);
                var keep = videoLocals
                    .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                    .ToList();
                foreach (var vl2 in keep) videoLocals.Remove(vl2);
                videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                videosToDelete.AddRange(videoLocals);
            }

            var result = true;
            var service = HttpContext.RequestServices.GetRequiredService<VideoLocal_PlaceService>();
            foreach (var toDelete in videosToDelete)
            {
                result &= toDelete.Places.All(a =>
                {
                    try
                    {
                        service.RemoveRecordAndDeletePhysicalFile(a).GetAwaiter().GetResult();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Deleting Files");
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
            var videoLocals = ep.VideoLocals;
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep) videoLocals.Remove(vl2);
            videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

            videosToDelete.AddRange(videoLocals);
        }
        return videosToDelete.Select(a => _videoLocalService.GetV1Contract(a, userID)).ToList();
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
            var videoLocals = ep.VideoLocals;
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep) videoLocals.Remove(vl2);
            videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

            videosToDelete.AddRange(videoLocals);
        }
        return videosToDelete.Select(a => _videoLocalService.GetV1DetailedContract(a, userID))
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
                    vids.AddRange(results1.Select(vid => _videoLocalService.GetV1Contract(vid, userID)));
                    results1 = RepoFactory.VideoLocal.GetByName(searchCriteria.Replace('+', ' ').Trim());
                    vids.AddRange(results1.Select(vid => _videoLocalService.GetV1Contract(vid, userID)));
                    break;

                case FileSearchCriteria.ED2KHash:
                    var vidl = RepoFactory.VideoLocal.GetByHash(searchCriteria.Trim());
                    if (vidl != null)
                        vids.Add(_videoLocalService.GetV1Contract(vidl, userID));
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
                    vids.AddRange(results2.Select(vid => _videoLocalService.GetV1Contract(vid, userID)));
                    break;
            }

            return vids.DistinctBy(a => a.VideoLocalID).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }
        return new List<CL_VideoLocal>();
    }

    [HttpGet("File/Rename/RandomPreview/{maxResults}/{userID}")]
    public List<CL_VideoLocal> RandomFileRenamePreview(int maxResults, int userID)
    {
        try
        {
            return RepoFactory.VideoLocal.GetRandomFiles(maxResults).Select(a => _videoLocalService.GetV1Contract(a, userID)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
            return new List<CL_VideoLocal>();
        }
    }

    [HttpGet("File/Rename/Preview/{videoLocalID}")]
    public CL_VideoLocal_Renamed RenameFilePreview(int videoLocalID) => null;

    [HttpGet("File/Rename/{videoLocalID}/{scriptName}")]
    public CL_VideoLocal_Renamed RenameFile(int videoLocalID, string scriptName) => null;

    [HttpGet("File/Rename/{videoLocalID}/{scriptName}/{move}")]
    public CL_VideoLocal_Renamed RenameAndMoveFile(int videoLocalID, string scriptName, bool move) => null;

    [HttpGet("RenameScript")]
    public List<RenameScript> GetAllRenameScripts()
    {
        return [];
    }

    [HttpPost("RenameScript")]
    public CL_Response<RenameScript> SaveRenameScript(RenameScript contract)
    {
        return null;
    }

    [HttpDelete("RenameScript/{renameScriptID}")]
    public string DeleteRenameScript(int renameScriptID)
    {
        return string.Empty;
    }

    [HttpGet("RenameScript/Types")]
    public IDictionary<string, string> GetScriptTypes()
    {
        return new Dictionary<string, string>();
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
                var command = _jobFactory.CreateJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = aid;
                    c.ForceRefresh = false;
                    c.DownloadRelations = false;
                });
                var anime = command.Process().Result;

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
                var titleHelper = Utils.ServiceContainer.GetRequiredService<AniDBTitleHelper>();
                foreach (var tit in titleHelper.SearchTitle(HttpUtility.UrlDecode(titleQuery)))
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
                        res.AnimeSeriesName = ser.AniDB_Anime.PreferredTitle;
                    }
                    else
                        res.SeriesExists = false;

                    retTitles.Add(res);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
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
            _logger.LogError(ex, "{Ex}", ex.ToString());
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
            _logger.LogError(ex, "{Ex}", ex.ToString());
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

                var finishedAiring = ser.AniDB_Anime.GetFinishedAiring();

                if (!finishedAiring && airState == AiringState.FinishedAiring) return Array.Empty<CL_MissingEpisode>();
                if (finishedAiring && airState == AiringState.StillAiring) return Array.Empty<CL_MissingEpisode>();

                if (missingEps <= 0) return Array.Empty<CL_MissingEpisode>();

                var anime = ser.AniDB_Anime;
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
                var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
                return ser.AllAnimeEpisodes
                    .Where(aep =>
                        aep.AniDB_Episode != null && aep.VideoLocals.Count == 0 &&
                        (!regularEpisodesOnly || aep.EpisodeTypeEnum == EpisodeType.Episode))
                    .Select(aep => aep.AniDB_Episode)
                    .Where(aniep => !aniep.GetFutureDated())
                    .Select(aniep => new CL_MissingEpisode
                    {
                        AnimeID = ser.AniDB_ID,
                        AnimeSeries = seriesService.GetV1UserContract(ser, userID),
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
            _logger.LogError(ex, "{Ex}", ex.ToString());
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
            var response = request.Send();
            if (response.Response != null)
            {
                var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
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

                    bool fileMissing;
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
                        SVR_AniDB_Anime anime;
                        if (myitem.AnimeID != null)
                        {
                            if (animeCache.ContainsKey(myitem.AnimeID.Value))
                                anime = animeCache[myitem.AnimeID.Value];
                            else
                            {
                                anime = RepoFactory.AniDB_Anime.GetByAnimeID(myitem.AnimeID.Value);
                                animeCache[myitem.AnimeID.Value] = anime;
                            }

                            SVR_AnimeSeries ser;
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

                            missingFile.AnimeSeries = ser == null ? null : seriesService.GetV1UserContract(ser, userID);
                            contracts.Add(missingFile);
                        }
                    }
                }
            }
            contracts = contracts.OrderBy(a => a.AnimeTitle).ThenBy(a => a.EpisodeID).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }
        return contracts;
    }

    [HttpDelete("AniDB/MyList/Missing")]
    public void RemoveMissingMyListFiles(List<CL_MissingFile> myListFiles)
    {
        // TODO maybe rework this
        var scheduler = _schedulerFactory.GetScheduler().Result;
        foreach (var missingFile in myListFiles)
        {
            var vl = RepoFactory.VideoLocal.GetByMyListID(missingFile.FileID);
            if (vl == null) continue;
            scheduler.StartJob<DeleteFileFromMyListJob>(
                c =>
                {
                    c.Hash = vl.Hash;
                    c.FileSize = vl.FileSize;
                }
            ).GetAwaiter().GetResult();

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
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            foreach (var ser in RepoFactory.AnimeSeries.GetAll())
            {
                if (RepoFactory.VideoLocal.GetByAniDBAnimeID(ser.AniDB_ID).Count == 0)
                {
                    var can = seriesService.GetV1UserContract(ser, userID);
                    if (can != null)
                        contracts.Add(can);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }
        return contracts;
    }

    [HttpGet("Series/MissingEpisodes/{maxRecords}/{userID}")]
    public List<CL_AnimeSeries_User> GetSeriesWithMissingEpisodes(int maxRecords, int userID)
    {
        try
        {
            var user = RepoFactory.JMMUser.GetByID(userID);
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            if (user != null)
                return
                    RepoFactory.AnimeSeries.GetWithMissingEpisodes()
                        .Select(a => seriesService.GetV1UserContract(a, userID))
                        .Where(a => a != null)
                        .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
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
                contracts.Add(_videoLocalService.GetV1Contract(vid, userID));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
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
                contracts.Add(_videoLocalService.GetV1Contract(vid, userID));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
        }
        return contracts;
    }

    [HttpGet("File/Unrecognised/{userID}")]
    public List<CL_VideoLocal> GetUnrecognisedFiles(int userID)
    {
        var contracts = new List<CL_VideoLocal>();
        try
        {
            contracts.AddRange(RepoFactory.VideoLocal.GetVideosWithoutEpisode(true).Select(vid => _videoLocalService.GetV1Contract(vid, userID)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.ToString());
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

            var scheduler = _schedulerFactory.GetScheduler().Result;
            foreach (var vl in filesWithoutEpisode.Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                scheduler.StartJob<ProcessFileJob>(
                    c =>
                    {
                        c.VideoLocalID = vl.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                ).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.Message);
        }
    }

    [HttpGet("File/Rescan/ManuallyLinked")]
    public void RescanManuallyLinkedFiles()
    {
        try
        {
            // files which have been hashed, but don't have an associated episode
            var files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();

            var scheduler = _schedulerFactory.GetScheduler().Result;
            foreach (var vl in files.Where(a => !string.IsNullOrEmpty(a.Hash)))
            {
                scheduler.StartJob<ProcessFileJob>(
                    c =>
                    {
                        c.VideoLocalID = vl.VideoLocalID;
                        c.ForceAniDB = true;
                    }
                ).GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex.Message);
        }
    }

    [HttpGet("File/Duplicated")]
    public List<CL_DuplicateFile> GetAllDuplicateFiles()
    {
        var dupFiles = new List<CL_DuplicateFile>();
        try
        {
            var files = RepoFactory.VideoLocalPlace.GetAll().GroupBy(a => a.VideoLocalID).Where(a => a.Count() > 1).ToList();
            foreach (var group in files)
            {
                var groupFiles = group.OrderBy(a => a.VideoLocal_Place_ID).ToList();
                var first = groupFiles.First();
                var vl = first.VideoLocal;
                SVR_AniDB_Anime anime = null;
                SVR_AniDB_Episode episode = null;
                var xref = RepoFactory.CrossRef_File_Episode.GetByHash(vl.Hash);
                if (xref.Count > 0)
                {
                    anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref[0].AnimeID);
                    episode = RepoFactory.AniDB_Episode.GetByEpisodeID(xref[0].EpisodeID);
                }

                foreach (var other in groupFiles.Skip(1))
                {
                    dupFiles.Add(new CL_DuplicateFile
                    {
                        Hash = vl.Hash,
                        File1VideoLocalPlaceID = first.VideoLocal_Place_ID,
                        ImportFolder1 = first.ImportFolder,
                        ImportFolderIDFile1 = first.ImportFolderID,
                        FilePathFile1 = first.FilePath,
                        File2VideoLocalPlaceID = other.VideoLocal_Place_ID,
                        ImportFolder2 = other.ImportFolder,
                        ImportFolderIDFile2 = other.ImportFolderID,
                        FilePathFile2 = other.FilePath,
                        AnimeID = anime?.AnimeID,
                        AnimeName = anime?.MainTitle,
                        EpisodeType = episode?.EpisodeType,
                        EpisodeNumber = episode?.EpisodeNumber,
                        EpisodeName = episode?.DefaultTitle,
                        DuplicateFileID = vl.VideoLocalID,
                        DateTimeUpdated = DateTime.Now
                    });
                }
            }

            return dupFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return dupFiles;
        }
    }

    /// <summary>
    /// Delete a duplicate file entry, and also one of the physical files
    /// </summary>
    /// <param name="videoLocalPlaceID"></param>
    /// <returns></returns>
    [HttpDelete("File/Duplicated/{videoLocalPlaceID}")]
    public string DeleteDuplicateFile(int videoLocalPlaceID)
    {
        try
        {
            var place = RepoFactory.VideoLocalPlace.GetByID(videoLocalPlaceID);
            if (place == null) return $"VideoLocal_Place of ID: {videoLocalPlaceID} not found";
            var service = HttpContext.RequestServices.GetRequiredService<VideoLocal_PlaceService>();
            service.RemoveRecordAndDeletePhysicalFile(place, false).GetAwaiter().GetResult();
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpGet("File/ManuallyLinked/{userID}")]
    public List<CL_VideoLocal> GetAllManuallyLinkedFiles(int userID)
    {
        var manualFiles = new List<CL_VideoLocal>();
        try
        {
            foreach (var vid in RepoFactory.VideoLocal.GetManuallyLinkedVideos())
            {
                manualFiles.Add(_videoLocalService.GetV1Contract(vid, userID));
            }

            return manualFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
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

            foreach (var ep in RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations))
            {
                if (onlyFinishedSeries)
                {
                    var finishedAiring = false;
                    if (dictSeriesFinishedAiring.ContainsKey(ep.AnimeSeriesID))
                        finishedAiring = dictSeriesFinishedAiring[ep.AnimeSeriesID];

                    if (!finishedAiring) continue;
                }
                var cep = _episodeService.GetV1Contract(ep, userID);
                if (cep != null)
                    eps.Add(cep);
            }

            return eps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return eps;
        }
    }

    [HttpPost("File/Duplicated/Reevaluate")]
    public void ReevaluateDuplicateFiles()
    {
        try
        {
            // Noop
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpGet("File/Detailed/{animeID}/{relGroupName}/{resolution}/{videoSource}/{videoBitDepth}/{userID}")]
    public List<CL_VideoDetailed> GetFilesByGroupAndResolution(int animeID, string relGroupName,
        string resolution,
        string videoSource, int videoBitDepth, int userID)
    {
        relGroupName = relGroupName == null || relGroupName.EqualsInvariantIgnoreCase("null") || relGroupName.Equals(string.Empty) ? null : Uri.UnescapeDataString(relGroupName.Replace("+", " "));
        videoSource = videoSource == null ? null : Uri.UnescapeDataString(videoSource.Replace("+", " "));
        _logger.LogTrace("GetFilesByGroupAndResolution -- relGroupName: {GroupName}", relGroupName);
        _logger.LogTrace("GetFilesByGroupAndResolution -- videoSource: {VideoSource}", videoSource);

        var vids = new List<CL_VideoDetailed>();

        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null) return vids;

            foreach (var vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
            {
                var thisBitDepth = 8;

                if (vid.MediaInfo?.VideoStream?.BitDepth != null) thisBitDepth = vid.MediaInfo.VideoStream.BitDepth;
                
                // Sometimes, especially with older files, the info doesn't quite match for resolution
                var vidResInfo = vid.VideoResolution;
                
                _logger.LogTrace("GetFilesByGroupAndResolution -- thisBitDepth: {BitDepth}", thisBitDepth);
                _logger.LogTrace("GetFilesByGroupAndResolution -- videoBitDepth: {BitDepth}", videoBitDepth);

                _logger.LogTrace("GetFilesByGroupAndResolution -- vidResInfo: {VidResInfo}", vidResInfo);
                _logger.LogTrace("GetFilesByGroupAndResolution -- resolution: {Resolution}", resolution);

                var eps = vid.AnimeEpisodes;
                if (eps.Count == 0) continue;

                var sourceMatches =
                    "Manual Link".EqualsInvariantIgnoreCase(videoSource) ||
                    "unknown".EqualsInvariantIgnoreCase(videoSource);
                var groupMatches = Constants.NO_GROUP_INFO.EqualsInvariantIgnoreCase(relGroupName) || "null".EqualsInvariantIgnoreCase(relGroupName) || relGroupName == null;
                _logger.LogTrace("GetFilesByGroupAndResolution -- sourceMatches (manual/unkown): {SourceMatches}", sourceMatches);
                _logger.LogTrace("GetFilesByGroupAndResolution -- groupMatches (NO GROUP INFO): {GroupMatches}", groupMatches);
                
                // get the anidb file info
                var aniFile = vid.AniDBFile;
                if (aniFile != null)
                {
                    _logger.LogTrace($"GetFilesByGroupAndResolution -- aniFile is not null");
                    _logger.LogTrace("GetFilesByGroupAndResolution -- aniFile.File_Source: {FileSource}", aniFile.File_Source);
                    _logger.LogTrace("GetFilesByGroupAndResolution -- aniFile.Anime_GroupName: {GroupName}", aniFile.Anime_GroupName);
                    _logger.LogTrace("GetFilesByGroupAndResolution -- aniFile.Anime_GroupNameShort: {GroupNameShort}", aniFile.Anime_GroupNameShort);
                    sourceMatches = string.Equals(videoSource, aniFile.File_Source, StringComparison.InvariantCultureIgnoreCase) || !sourceMatches &&
                        (aniFile.File_Source?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false) &&
                        string.Equals("unknown", videoSource, StringComparison.InvariantCultureIgnoreCase);

                    if (!string.IsNullOrEmpty(aniFile.Anime_GroupName) || !string.IsNullOrEmpty(aniFile.Anime_GroupNameShort))
                        groupMatches = string.Equals(relGroupName, aniFile.Anime_GroupName, StringComparison.InvariantCultureIgnoreCase) ||
                                       string.Equals(relGroupName, aniFile.Anime_GroupNameShort, StringComparison.InvariantCultureIgnoreCase);

                    if (!"raw".Equals(aniFile.Anime_GroupNameShort) &&
                        ((aniFile.Anime_GroupName?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                         (aniFile.Anime_GroupNameShort?.Contains("unk", StringComparison.InvariantCultureIgnoreCase) ?? false)) || aniFile.Anime_GroupName == null)
                        groupMatches = Constants.NO_GROUP_INFO.EqualsInvariantIgnoreCase(relGroupName) || relGroupName == null || "null".EqualsInvariantIgnoreCase(relGroupName);

                    _logger.LogTrace("GetFilesByGroupAndResolution -- sourceMatches (aniFile): {Matches}", sourceMatches);
                    _logger.LogTrace("GetFilesByGroupAndResolution -- groupMatches (aniFile): {Matches}", groupMatches);
                }

                // match based on group / video source / video res
                if (groupMatches && sourceMatches && thisBitDepth == videoBitDepth &&
                    string.Equals(resolution, vidResInfo, StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogTrace("GetFilesByGroupAndResolution -- File Matched: {FileName}", vid.FileName);
                    vids.Add(_videoLocalService.GetV1DetailedContract(vid, userID));
                }
            }
            return vids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return vids;
        }
    }

    [HttpGet("File/ByGroup/{animeID}/{relGroupName}/{userID}")]
    public List<CL_VideoDetailed> GetFilesByGroup(int animeID, string relGroupName, int userID)
    {
        var grpName = relGroupName == null || "null".EqualsInvariantIgnoreCase(relGroupName) || relGroupName.Equals(string.Empty) ? null : Uri.UnescapeDataString(relGroupName.Replace("+", " "));
        var vids = new List<CL_VideoDetailed>();

        try
        {
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            if (anime == null) return vids;

            foreach (var vid in RepoFactory.VideoLocal.GetByAniDBAnimeID(animeID))
            {
                var eps = vid.AnimeEpisodes;
                if (eps.Count == 0) continue;
                // get the anibd file info
                var aniFile = vid.AniDBFile;
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
                        vids.Add(_videoLocalService.GetV1DetailedContract(vid, userID));
                    }
                }
                else
                {
                    if (string.Equals(grpName, Constants.NO_GROUP_INFO, StringComparison.InvariantCultureIgnoreCase) ||
                        string.Equals(grpName, "unknown"))
                    {
                        vids.Add(_videoLocalService.GetV1DetailedContract(vid, userID));
                    }
                }
            }
            return vids;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return vids;
        }
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
            var anidbFile = a.AniDBFile;
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
            var eps = videoLocals.Select(a => (a?.AnimeEpisodes).FirstOrDefault()).Where(a => a != null).ToList();
            var ani = videoLocals.First().AniDBFile;
            contract.AudioStreamCount = videoLocals.First()
                .MediaInfo?.AudioStreams.Count ?? 0;
            contract.IsChaptered =
                ani?.IsChaptered ?? (videoLocals.First()?.MediaInfo?.MenuStreams.Any() ?? false);
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
            var bitDepth = videoLocals.First().MediaInfo?.VideoStream?.BitDepth;
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
            _logger.LogError(ex, "{Ex}", ex);
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

        var filePath = video.FirstResolvedPlace?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
        {
            return "Unable to get file location for VideoLocal with id: " + video.VideoLocalID;
        }

        var session = AVDumpHelper.DumpFiles(
            new Dictionary<int, string>() { { vidLocalID, filePath } },
            true
        );

        var output = session.StandardOutput.Replace("\n", "\r\n");
        if (session.IsSuccess)
            output +=
                $"--------------------------------------------------------------------------------\r\n\r\n{string.Join("\r\n", session.ED2Ks)}\r\n\r\nDumping successful.";
        else
            output +=
                $"--------------------------------------------------------------------------------\r\n\r\nFailed to complete AVDump session {session.SessionID}:\r\n\r\nFiles:\r\n{string.Join("\r\n", session.AbsolutePaths)}\r\n\r\nStandard Output:\r\n{session.StandardOutput}{(session.StandardError.Length > 0 ? $"\r\nStandard Error:\r\n{session.StandardError}" : "")}";
        return output;
    }
}
