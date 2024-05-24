using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Utilities;
using AnimeType = Shoko.Models.Enums.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models;

public class SVR_AnimeSeries : AnimeSeries
{
    #region DB Columns

    public int ContractVersion { get; set; }
    public byte[] ContractBlob { get; set; }
    public int ContractSize { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DataSourceType DisableAutoMatchFlags { get; set; } = 0;

    #endregion

    #region Disabled Auto Matching

    public bool IsTvDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TvDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TvDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TvDB;
        }
    }

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TMDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TMDB;
        }
    }

    public bool IsTraktAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Trakt);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Trakt;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Trakt;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.MAL;
            else
                DisableAutoMatchFlags &= ~DataSourceType.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.AniList;
            else
                DisableAutoMatchFlags &= ~DataSourceType.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Animeshon;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Kitsu;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Kitsu;
        }
    }

    #endregion

    public const int CONTRACT_VERSION = 9;


    private CL_AnimeSeries_User _contract;

    public virtual CL_AnimeSeries_User Contract
    {
        get
        {
            if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
            {
                _contract = CompressionHelper.DeserializeObject<CL_AnimeSeries_User>(ContractBlob, ContractSize);
            }

            return _contract;
        }
        set
        {
            _contract = value;
            ContractBlob = CompressionHelper.SerializeObject(value, out var outsize);
            ContractSize = outsize;
            ContractVersion = CONTRACT_VERSION;
        }
    }

    public void CollectContractMemory()
    {
        _contract = null;
    }


    public string Year => GetAnime().GetYear();

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public string GetSeriesName()
    {
        // Return the override if it's set.
        if (!string.IsNullOrEmpty(SeriesNameOverride))
            return SeriesNameOverride;

        if (Utils.SettingsProvider.GetSettings().SeriesNameSource == DataSourceType.AniDB)
            return GetAnime().PreferredTitle;

        // Try to find the TvDB title if we prefer TvDB titles.
        var tvdbShows = GetTvDBSeries();
        var tvdbShowTitle = tvdbShows
            .FirstOrDefault(show => !show.SeriesName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.SeriesName;
        if (!string.IsNullOrEmpty(tvdbShowTitle))
            return tvdbShowTitle;

        // Otherwise just return the anidb title.
        return GetAnime().PreferredTitle;
    }

    public HashSet<string> GetAllTitles()
    {
        var titles = new HashSet<string>();

        // Override
        if (SeriesNameOverride != null)
        {
            titles.Add(SeriesNameOverride);
        }

        // AniDB
        if (GetAnime() != null)
        {
            titles.UnionWith(GetAnime().GetAllTitles());
        }
        else
        {
            logger.Error($"A Series has a null AniDB_Anime. That is bad. The AniDB ID is {AniDB_ID}");
        }

        // TvDB
        var tvdb = GetTvDBSeries();
        if (tvdb != null)
        {
            titles.UnionWith(tvdb.Select(a => a?.SeriesName).Where(a => a != null));
        }

        // MovieDB
        var movieDB = GetMovieDB();
        if (movieDB != null)
        {
            titles.Add(movieDB.MovieName);
            titles.Add(movieDB.OriginalName);
        }

        return titles;
    }

    public string GenresRaw
    {
        get
        {
            if (GetAnime() == null)
            {
                return string.Empty;
            }

            return GetAnime().TagsString;
        }
    }

    /// <summary>
    /// Get video locals for anime series.
    /// </summary>
    /// <param name="xrefSource">Set to a value to only select video locals from
    /// a select source.</param>
    /// <returns>All or some video locals for the anime series.</returns>
    public List<SVR_VideoLocal> GetVideoLocals(CrossRefSource? xrefSource = null)
    {
        return RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID, xrefSource);
    }


    /// <summary>
    /// Get episodes for the series.
    /// </summary>
    /// <param name="orderList">Order the returned list.</param>
    /// <param name="includeHidden">Include ignored episodes in the list.</param>
    /// <returns>A list of episodes for the series.</returns>
    public List<SVR_AnimeEpisode> GetAnimeEpisodes(bool orderList = false, bool includeHidden = true)
    {
        if (orderList)
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
                .Where(episode => includeHidden || !episode.IsHidden)
                .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
                .OrderBy(tuple => tuple.anidbEpisode.EpisodeType)
                .ThenBy(tuple => tuple.anidbEpisode.EpisodeNumber)
                .Select(tuple => tuple.episode)
                .ToList();
        }
        if (!includeHidden)
        {
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
                .Where(episode => !episode.IsHidden)
                .ToList();
        }
        return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID);
    }

    public int GetAnimeEpisodesCountWithVideoLocal()
    {
        return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a => a.GetVideoLocals().Any());
    }

    public int GetAnimeEpisodesNormalCountWithVideoLocal()
    {
        return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a =>
            a.AniDB_Episode != null && a.GetVideoLocals().Any() && a.EpisodeTypeEnum == EpisodeType.Episode);
    }

    public int GetAnimeEpisodesAndSpecialsCountWithVideoLocal()
    {
        return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Count(a =>
            a.AniDB_Episode != null && a.GetVideoLocals().Any() &&
            (a.EpisodeTypeEnum == EpisodeType.Episode || a.EpisodeTypeEnum == EpisodeType.Special));
    }

    public int GetAnimeNumberOfEpisodeTypes()
    {
        return RepoFactory.AnimeEpisode
            .GetBySeriesID(AnimeSeriesID)
            .Where(a => a.AniDB_Episode != null && RepoFactory.CrossRef_File_Episode
                .GetByEpisodeID(
                    RepoFactory.AniDB_Episode.GetByEpisodeID(a.AniDB_EpisodeID)?.EpisodeID ?? 0)
                .Select(b => RepoFactory.VideoLocal.GetByHash(b.Hash))
                .Count(b => b != null) > 0)
            .Select(a => a.EpisodeTypeEnum)
            .Distinct()
            .Count();
    }

    public MovieDB_Movie GetMovieDB()
    {
        var movieDBXRef = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);
        if (movieDBXRef?.CrossRefID == null || !int.TryParse(movieDBXRef.CrossRefID, out var movieID))
        {
            return null;
        }

        var movieDB = RepoFactory.MovieDb_Movie.GetByOnlineID(movieID);
        return movieDB;
    }


    #region TvDB

    public List<CrossRef_AniDB_TvDB> GetCrossRefTvDB()
    {
        return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AniDB_ID);
    }

    public List<TvDB_Series> GetTvDBSeries()
    {
        var xrefs = GetCrossRefTvDB()?.WhereNotNull().ToArray();
        if (xrefs == null || xrefs.Length == 0) return [];
        return xrefs.Select(xref => xref.GetTvDBSeries()).WhereNotNull().ToList();
    }

    #endregion

    #region Trakt

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
    {
        return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AniDB_ID);
    }

    public List<Trakt_Show> GetTraktShow()
    {
        using var session = DatabaseFactory.SessionFactory.OpenSession();
        return GetTraktShow(session);
    }

    public List<Trakt_Show> GetTraktShow(ISession session)
    {
        var sers = new List<Trakt_Show>();

        var xrefs = GetCrossRefTraktV2(session);
        if (xrefs == null || xrefs.Count == 0)
        {
            return sers;
        }

        foreach (var xref in xrefs)
        {
            sers.Add(xref.GetByTraktShow(session));
        }

        return sers;
    }

    #endregion

    public CrossRef_AniDB_Other CrossRefMovieDB =>
        RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);

    public List<CrossRef_AniDB_MAL> CrossRefMAL => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

    public CL_AnimeSeries_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null, bool cloned = true)
    {
        try
        {
            var contract = Contract;
            if (cloned && contract != null) contract = (CL_AnimeSeries_User)contract.Clone();
            if (contract == null)
            {
                logger.Trace($"Series with ID [{AniDB_ID}] has a null contract on get. Updating");
                RepoFactory.AnimeSeries.Save(this, false, false);
                contract = (CL_AnimeSeries_User)_contract?.Clone();
            }

            if (contract == null)
            {
                logger.Warn($"Series with ID [{AniDB_ID}] has a null contract even after updating");
                return null;
            }

            var rr = GetUserRecord(userid);
            if (rr != null)
            {
                contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                contract.WatchedDate = rr.WatchedDate;
                contract.PlayedCount = rr.PlayedCount;
                contract.WatchedCount = rr.WatchedCount;
                contract.StoppedCount = rr.StoppedCount;
                contract.AniDBAnime.AniDBAnime.FormattedTitle = GetSeriesName();
                return contract;
            }

            if (types != null)
            {
                if (!types.Contains(GroupFilterConditionType.HasUnwatchedEpisodes))
                {
                    types.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
                }

                if (!types.Contains(GroupFilterConditionType.EpisodeWatchedDate))
                {
                    types.Add(GroupFilterConditionType.EpisodeWatchedDate);
                }

                if (!types.Contains(GroupFilterConditionType.HasWatchedEpisodes))
                {
                    types.Add(GroupFilterConditionType.HasWatchedEpisodes);
                }
            }

            if (contract.AniDBAnime?.AniDBAnime != null)
            {
                contract.AniDBAnime.AniDBAnime.FormattedTitle = GetSeriesName();
            }

            return contract;
        }
        catch
        {
            return null;
        }
    }

    public SVR_AnimeSeries_User GetUserRecord(int userID)
    {
        return RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);
    }

    public SVR_AnimeSeries_User GetOrCreateUserRecord(int userID)
    {
        lock (this)
        {
            var userRecord = GetUserRecord(userID);
            if (userRecord != null)
            {
                return userRecord;
            }

            userRecord = new SVR_AnimeSeries_User(userID, AnimeSeriesID);
            RepoFactory.AnimeSeries_User.Save(userRecord);
            return userRecord;
        }
    }

    public SVR_AnimeEpisode GetLastEpisodeWatched(int userID)
    {
        SVR_AnimeEpisode watchedep = null;
        SVR_AnimeEpisode_User userRecordWatched = null;

        foreach (var ep in GetAnimeEpisodes())
        {
            var userRecord = ep.GetUserRecord(userID);
            if (userRecord != null && ep.AniDB_Episode != null && ep.EpisodeTypeEnum == EpisodeType.Episode)
            {
                if (watchedep == null)
                {
                    watchedep = ep;
                    userRecordWatched = userRecord;
                }

                if (userRecord.WatchedDate > userRecordWatched.WatchedDate)
                {
                    watchedep = ep;
                    userRecordWatched = userRecord;
                }
            }
        }

        return watchedep;
    }

    /// <summary>
    /// Get the most recent activly watched episode for the user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="includeSpecials">Include specials when searching.</param>
    /// <returns></returns>
    public SVR_AnimeEpisode GetActiveEpisode(int userID, bool includeSpecials = true)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var episodes = GetAnimeEpisodes()
            .Select(episode => (episode, episode.AniDB_Episode))
            .Where(tuple => !tuple.episode.IsHidden && (tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode ||
                            (includeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special)))
            .OrderBy(tuple => tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        var (episode, _) = episodes
            .SelectMany(episode => episode.GetVideoLocals().Select(file => (episode, file.GetUserRecord(userID))))
            .Where(tuple => tuple.Item2 != null)
            .OrderByDescending(tuple => tuple.Item2.LastUpdated)
            .FirstOrDefault(tuple => tuple.Item2.ResumePosition > 0);
        return episode;
    }

    /// <summary>
    /// Series next-up query options for use with <see cref="GetNextEpisode"/>.
    /// </summary>
    public class NextUpQueryOptions
    {
        /// <summary>
        /// Disable the first episode in the series from showing up.
        /// /// </summary>
        public bool DisableFirstEpisode = false;

        /// <summary>
        /// Include currently watching episodes in the search.
        /// </summary>
        public bool IncludeCurrentlyWatching = false;

        /// <summary>
        /// Include hidden episodes in the search.
        /// </summary>
        public bool IncludeHidden = false;

        /// <summary>
        /// Include missing episodes in the search.
        /// </summary>
        public bool IncludeMissing = false;

        /// <summary>
        /// Include already watched episodes in the search if we determine the
        /// user is "re-watching" the series.
        /// </summary>
        public bool IncludeRewatching = false;

        /// <summary>
        /// Include specials in the search.
        /// </summary>
        public bool IncludeSpecials = true;
    }

    /// <summary>
    /// Get the next episode for the series for a user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="options">Next-up query options.</param>
    /// <returns></returns>
    public SVR_AnimeEpisode GetNextEpisode(int userID, NextUpQueryOptions options = null)
    {
        // Initialise the options if they're not provided.
        if (options == null)
            options = new();

        // Filter the episodes to only normal or special episodes and order them
        // in rising order. Also count the number of episodes and specials if
        // we're searching for the next episode for "re-watching" sessions.
        var episodesCount = 0;
        var speicalsCount = 0;
        var episodeList = GetAnimeEpisodes(orderList: false, includeHidden: options.IncludeHidden)
            .Select(episode => (episode, episode.AniDB_Episode))
            .Where(tuple =>
            {
                if (tuple.episode.IsHidden)
                {
                    return false;
                }

                if (tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode)
                {
                    episodesCount++;
                    return true;
                }

                if (options.IncludeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special)
                {
                    speicalsCount++;
                    return true;
                }

                return false;
            })
            .OrderBy(tuple => tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .ToList();

        // Look for active watch sessions and return the episode for the most
        // recent session if found.
        if (options.IncludeCurrentlyWatching)
        {
            var (currentlyWatchingEpisode, _) = episodeList
                .SelectMany(tuple => tuple.episode.GetVideoLocals().Select(file => (episode: tuple.episode, fileUR: file.GetUserRecord(userID))))
                .Where(tuple => tuple.fileUR != null)
                .OrderByDescending(tuple => tuple.fileUR.LastUpdated)
                .FirstOrDefault(tuple => tuple.fileUR.ResumePosition > 0);

            if (currentlyWatchingEpisode != null)
            {
                return currentlyWatchingEpisode;
            }
        }
        // Skip check if there is an active watch session for the series and we
        // don't allow active watch sessions.
        else if (episodeList.Any(tuple =>
                     tuple.episode.GetVideoLocals().Any(file => (file.GetUserRecord(userID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // When "re-watching" we look for the next episode after the last
        // watched episode.
        if (options.IncludeRewatching)
        {
            var (lastWatchedEpisode, _) = episodeList
                .SelectMany(tuple => tuple.episode.GetVideoLocals().Select(file => (episode: tuple.episode, fileUR: file.GetUserRecord(userID))))
                .Where(tuple => tuple.fileUR != null && tuple.fileUR.WatchedDate.HasValue)
                .OrderByDescending(tuple => tuple.fileUR.LastUpdated)
                .FirstOrDefault();

            if (lastWatchedEpisode != null) {
                // Return `null` if we're on the last episode in the list, or
                // if we're on the last normal episode and there is no specials
                // after it.
                var nextIndex = episodeList.FindIndex(tuple => tuple.episode == lastWatchedEpisode) + 1;
                if ((nextIndex == episodeList.Count) || (nextIndex == episodesCount) && (!options.IncludeSpecials || speicalsCount == 0))
                    return null;

                var (nextEpisode, _) = episodeList.Skip(nextIndex)
                    .FirstOrDefault(options.IncludeMissing ? _ => true : tuple => tuple.episode.GetVideoLocals().Count > 0);
                return nextEpisode;
            }
        }

        // Find the first episode that's unwatched.
        var (unwatchedEpisode, anidbEpisode) = episodeList
            .Where(tuple =>
            {
                var episodeUserRecord = tuple.episode.GetUserRecord(userID);
                if (episodeUserRecord == null)
                {
                    return true;
                }

                return !episodeUserRecord.WatchedDate.HasValue;
            })
            .FirstOrDefault(options.IncludeMissing ? _ => true : tuple => tuple.episode.GetVideoLocals().Count > 0);

        // Disable first episode from showing up in the search.
        if (options.DisableFirstEpisode && anidbEpisode != null && anidbEpisode.EpisodeType == (int)EpisodeType.Episode && anidbEpisode.EpisodeNumber == 1)
            return null;

        return unwatchedEpisode;
    }

    public SVR_AniDB_Anime GetAnime()
    {
        return RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);
    }

    private DateTime? _airDate;
    public DateTime? AirDate
    {
        get
        {
            if (_airDate != null) return _airDate;
            var anime = GetAnime();
            if (anime?.AirDate != null)
                return _airDate = anime.AirDate.Value;

            // This will be slower, but hopefully more accurate
            var ep = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID)
                .Where(a => a.EpisodeType == (int)EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
                .MinBy(a => a.AirDate);
            return _airDate = ep?.GetAirDateAsDate();
        }
    }

    private DateTime? _endDate;
    public DateTime? EndDate
    {
        get
        {
            if (_endDate != null) return _endDate;
            return _endDate = GetAnime()?.EndDate;
        }
    }

    /// <summary>
    /// Get the most recent days in the week the show airs on.
    /// </summary>
    /// <param name="animeEpisodes">Optionally pass in the episodes so we don't have to fetch them.</param>
    /// <param name="includeThreshold">Threshold of episodes to include in the calculation.</param>
    /// <returns></returns>
    public List<DayOfWeek> GetAirsOnDaysOfWeek(List<SVR_AnimeEpisode> animeEpisodes = null, int includeThreshold = 24)
    {
        // Fetch the anime episodes now if we didn't get them supplied to us.
        if (animeEpisodes == null)
            animeEpisodes = GetAnimeEpisodes();

        var now = DateTime.Now;
        var filteredEpisodes = animeEpisodes
            .Select(episode =>
            {
                var aniDB = episode.AniDB_Episode;
                var airDate = aniDB.GetAirDateAsDate();
                return (episode, aniDB, airDate);
            })
            .Where(tuple =>
            {
                // We ignore all other types except the "normal" type.
                if ((EpisodeType)tuple.aniDB.EpisodeType != EpisodeType.Episode)
                    return false;

                // We ignore any unknown air dates and dates in the future.
                if (!tuple.airDate.HasValue || tuple.airDate.Value > now)
                    return false;

                return true;
            })
            .ToList();

        // Threshold used to filter out outliners, e.g. a weekday that only happens
        // once or twice for whatever reason, or when a show gets an early preview,
        // an episode moving, etc..
        var outlierThreshold = Math.Min((int)Math.Ceiling(filteredEpisodes.Count / 12D), 4);
        return filteredEpisodes
            .OrderByDescending(tuple => tuple.aniDB.EpisodeNumber)
            // We check up to the `x` last aired episodes to get a grasp on which days
            // it airs on. This helps reduce variance in days for long-running
            // shows, such as One Piece, etc..
            .Take(includeThreshold)
            .Select(tuple => tuple.airDate.Value.DayOfWeek)
            .GroupBy(weekday => weekday)
            .Where(list => list.Count() > outlierThreshold)
            .Select(list => list.Key)
            .OrderBy(weekday => weekday)
            .ToList();
    }

    public void Populate(SVR_AniDB_Anime anime)
    {
        AniDB_ID = anime.AnimeID;
        LatestLocalEpisodeNumber = 0;
        DateTimeUpdated = DateTime.Now;
        DateTimeCreated = DateTime.Now;
        UpdatedAt = DateTime.Now;
        SeriesNameOverride = string.Empty;
    }

    public async Task CreateAnimeEpisodes(SVR_AniDB_Anime anime = null)
    {
        anime ??= GetAnime();
        if (anime == null)
        {
            return;
        }

        var eps = anime.GetAniDBEpisodes();
        // Cleanup deleted episodes
        var epsToRemove = RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Where(a => a.AniDB_Episode == null).ToList();
        var filesToUpdate = epsToRemove
            .SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(a.AniDB_EpisodeID)).ToList();
        var vlIDsToUpdate = filesToUpdate.Select(a => RepoFactory.VideoLocal.GetByHash(a.Hash)?.VideoLocalID)
            .Where(a => a != null).Select(a => a.Value).ToList();
        // remove existing xrefs
        RepoFactory.CrossRef_File_Episode.Delete(filesToUpdate);

        // queue rescan for the files
        var schedulerFactory = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();
        foreach (var id in vlIDsToUpdate)
        {
            await scheduler.StartJob<ProcessFileJob>(a => a.VideoLocalID = id);
        }

        RepoFactory.AnimeEpisode.Delete(epsToRemove);

        var one_forth = (int)Math.Round(eps.Count / 4D, 0, MidpointRounding.AwayFromZero);
        var one_half = (int)Math.Round(eps.Count / 2D, 0, MidpointRounding.AwayFromZero);
        var three_forths = (int)Math.Round(eps.Count * 3 / 4D, 0, MidpointRounding.AwayFromZero);

        logger.Trace($"Generating {eps.Count} episodes for {anime.MainTitle}");
        for (var i = 0; i < eps.Count; i++)
        {
            if (i == one_forth)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 25%");
            }

            if (i == one_half)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 50%");
            }

            if (i == three_forths)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 75%");
            }

            if (i == eps.Count - 1)
            {
                logger.Trace($"Generating episodes for {anime.MainTitle}: 100%");
            }

            var ep = eps[i];
            ep.CreateAnimeEpisode(AnimeSeriesID);
        }
    }

    public bool NeedsEpisodeUpdate()
    {
        var anime = GetAnime();
        if (anime == null)
        {
            return false;
        }

        return anime.GetAniDBEpisodes()
            .Select(episode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episode.EpisodeID))
            .Any(ep => ep == null) || GetAnimeEpisodes()
            .Select(episode => RepoFactory.AniDB_Episode.GetByEpisodeID(episode.AniDB_EpisodeID))
            .Any(ep => ep == null);
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public SVR_AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public SVR_AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

            int parentID;
            while ((parentID = parentGroup?.AnimeGroupParentID ?? 0) != 0)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentID);
            }

            return parentGroup;
        }
    }

    public List<SVR_AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<SVR_AnimeGroup>();
            try
            {
                var groupID = AnimeGroupID;
                while (groupID != 0)
                {
                    var grp = RepoFactory.AnimeGroup.GetByID(groupID);
                    if (grp != null)
                    {
                        grps.Add(grp);
                        groupID = grp.AnimeGroupParentID ?? 0;
                    }
                    else
                    {
                        groupID = 0;
                    }
                }

                return grps;
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return grps;
        }
    }

    public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(ISessionWrapper session,
        IReadOnlyCollection<SVR_AnimeSeries> seriesBatch, bool onlyStats = false)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (seriesBatch == null)
        {
            throw new ArgumentNullException(nameof(seriesBatch));
        }

        var grpFilterCondTypesPerSeries = new Dictionary<int, HashSet<GroupFilterConditionType>>();

        if (seriesBatch.Count == 0)
        {
            return grpFilterCondTypesPerSeries;
        }

        var animeIds = new Lazy<int[]>(() => seriesBatch.Select(s => s.AniDB_ID).ToArray(), false);
        var tvDbByAnime = new Lazy<ILookup<int, Tuple<CrossRef_AniDB_TvDB, TvDB_Series>>>(
            () => RepoFactory.TvDB_Series.GetByAnimeIDs(session, animeIds.Value), false);
        var movieByAnime = new Lazy<Dictionary<int, Tuple<CrossRef_AniDB_Other, MovieDB_Movie>>>(
            () => RepoFactory.MovieDb_Movie.GetByAnimeIDs(session, animeIds.Value), false);
        var malXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_MAL>>(
            () => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeIDs(session, animeIds.Value), false);
        var defImagesByAnime = new Lazy<Dictionary<int, DefaultAnimeImages>>(
            () => RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds.Value), false);

        foreach (var series in seriesBatch)
        {
            var contract = (CL_AnimeSeries_User)series.Contract?.Clone();
            var seriesOnlyStats = onlyStats;

            if (contract == null)
            {
                contract = new CL_AnimeSeries_User();
                seriesOnlyStats = false;
            }

            contract.AniDB_ID = series.AniDB_ID;
            contract.AnimeGroupID = series.AnimeGroupID;
            contract.AnimeSeriesID = series.AnimeSeriesID;
            contract.DateTimeUpdated = series.DateTimeUpdated;
            contract.DateTimeCreated = series.DateTimeCreated;
            contract.DefaultAudioLanguage = series.DefaultAudioLanguage;
            contract.DefaultSubtitleLanguage = series.DefaultSubtitleLanguage;
            contract.LatestLocalEpisodeNumber = series.LatestLocalEpisodeNumber;
            contract.LatestEpisodeAirDate = series.LatestEpisodeAirDate;
            contract.AirsOn = series.AirsOn;
            contract.EpisodeAddedDate = series.EpisodeAddedDate;
            contract.MissingEpisodeCount = series.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = series.MissingEpisodeCountGroups;
            contract.SeriesNameOverride = series.SeriesNameOverride;
            contract.DefaultFolder = series.DefaultFolder;
            contract.PlayedCount = 0;
            contract.StoppedCount = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedCount = 0;
            contract.WatchedDate = null;
            contract.WatchedEpisodeCount = 0;

            if (!seriesOnlyStats)
            {
                // AniDB contract
                var animeRec = series.GetAnime();

                if (animeRec != null)
                {
                    contract.AniDBAnime = (CL_AniDB_AnimeDetailed)animeRec.Contract.Clone();

                    var aniDbAnime = contract.AniDBAnime.AniDBAnime;

                    if (!defImagesByAnime.Value.TryGetValue(animeRec.AnimeID, out var defImages))
                    {
                        defImages = new DefaultAnimeImages { AnimeID = animeRec.AnimeID };
                    }

                    aniDbAnime.DefaultImagePoster = defImages.GetPosterContractNoBlanks();
                    aniDbAnime.DefaultImageFanart = defImages.GetFanartContractNoBlanks(aniDbAnime);
                    aniDbAnime.DefaultImageWideBanner = defImages.WideBanner?.ToContract();
                }

                // TvDB contracts
                var tvDbCrossRefs = tvDbByAnime.Value[series.AniDB_ID].ToList();

                foreach (var missingTvDbSeries in tvDbCrossRefs.Where(cr => cr.Item2 == null)
                             .Select(cr => cr.Item1))
                {
                    logger.Warn("You are missing database information for TvDB series: {0} - {1}",
                        missingTvDbSeries.TvDBID, missingTvDbSeries.GetTvDBSeries()?.SeriesName ?? "Series Not Found");
                }

                contract.CrossRefAniDBTvDBV2 = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(series.AniDB_ID);
                contract.TvDB_Series = tvDbCrossRefs
                    .Select(s => s.Item2)
                    .ToList();

                // MovieDB contracts

                if (movieByAnime.Value.TryGetValue(series.AniDB_ID, out var movieDbInfo))
                {
                    contract.CrossRefAniDBMovieDB = movieDbInfo.Item1;
                    contract.MovieDB_Movie = movieDbInfo.Item2;
                }
                else
                {
                    contract.CrossRefAniDBMovieDB = null;
                    contract.MovieDB_Movie = null;
                }

                // MAL contracts
                contract.CrossRefAniDBMAL = malXrefByAnime.Value[series.AniDB_ID]
                    .ToList();
            }

            var typesChanged = GetConditionTypesChanged(series.Contract, contract);

            series.Contract = contract;
            grpFilterCondTypesPerSeries.Add(series.AnimeSeriesID, typesChanged);
        }

        return grpFilterCondTypesPerSeries;
    }

    public HashSet<GroupFilterConditionType> UpdateContract(bool onlystats = false)
    {
        var start = DateTime.Now;
        TimeSpan ts;
        var contract = (CL_AnimeSeries_User)Contract?.Clone();
        ts = DateTime.Now - start;
        logger.Trace(
            $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Cloned Series Contract in {ts.TotalMilliseconds}ms");
        if (contract == null)
        {
            contract = new CL_AnimeSeries_User();
            onlystats = false;
        }

        contract.AniDB_ID = AniDB_ID;
        contract.AnimeGroupID = AnimeGroupID;
        contract.AnimeSeriesID = AnimeSeriesID;
        contract.DateTimeUpdated = DateTimeUpdated;
        contract.DateTimeCreated = DateTimeCreated;
        contract.DefaultAudioLanguage = DefaultAudioLanguage;
        contract.DefaultSubtitleLanguage = DefaultSubtitleLanguage;
        contract.LatestLocalEpisodeNumber = LatestLocalEpisodeNumber;
        contract.LatestEpisodeAirDate = LatestEpisodeAirDate;
        contract.AirsOn = AirsOn;
        contract.EpisodeAddedDate = EpisodeAddedDate;
        contract.MissingEpisodeCount = MissingEpisodeCount;
        contract.MissingEpisodeCountGroups = MissingEpisodeCountGroups;
        contract.SeriesNameOverride = SeriesNameOverride;
        contract.DefaultFolder = DefaultFolder;
        contract.PlayedCount = 0;
        contract.StoppedCount = 0;
        contract.UnwatchedEpisodeCount = 0;
        contract.WatchedCount = 0;
        contract.WatchedDate = null;
        contract.WatchedEpisodeCount = 0;
        if (onlystats)
        {
            start = DateTime.Now;
            var types2 = GetConditionTypesChanged(Contract, contract);
            Contract = contract;
            ts = DateTime.Now - start;
            logger.Trace(
                $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
            return types2;
        }

        var animeRec = GetAnime();
        var tvDBCrossRefs = GetCrossRefTvDB();
        var movieDBCrossRef = CrossRefMovieDB;
        MovieDB_Movie movie = null;
        if (movieDBCrossRef != null)
        {
            movie = movieDBCrossRef.GetMovieDB_Movie();
        }

        var sers = new List<TvDB_Series>();
        foreach (var xref in tvDBCrossRefs)
        {
            var tvser = xref.GetTvDBSeries();
            if (tvser != null)
            {
                sers.Add(tvser);
            }
            else
            {
                logger.Warn("You are missing database information for TvDB series: {0}", xref.TvDBID);
            }
        }

        // get AniDB data
        if (animeRec != null)
        {
            start = DateTime.Now;
            if (animeRec.Contract == null)
            {
                RepoFactory.AniDB_Anime.Save(animeRec);
            }

            contract.AniDBAnime = (CL_AniDB_AnimeDetailed)animeRec.Contract.Clone();
            ts = DateTime.Now - start;
            logger.Trace(
                $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got and Cloned AniDB_Anime Contract in {ts.TotalMilliseconds}ms");
            contract.AniDBAnime.AniDBAnime.DefaultImagePoster = animeRec.GetDefaultPoster()?.ToClient();
            if (contract.AniDBAnime.AniDBAnime.DefaultImagePoster == null)
            {
                var im = animeRec.GetDefaultPosterDetailsNoBlanks();
                if (im != null)
                {
                    contract.AniDBAnime.AniDBAnime.DefaultImagePoster = new CL_AniDB_Anime_DefaultImage
                    {
                        AnimeID = im.ImageID, ImageType = (int)im.ImageType
                    };
                }
            }

            contract.AniDBAnime.AniDBAnime.DefaultImageFanart = animeRec.GetDefaultFanart()?.ToClient();
            if (contract.AniDBAnime.AniDBAnime.DefaultImageFanart == null)
            {
                var im = animeRec.GetDefaultFanartDetailsNoBlanks();
                if (im != null)
                {
                    contract.AniDBAnime.AniDBAnime.DefaultImageFanart = new CL_AniDB_Anime_DefaultImage
                    {
                        AnimeID = im.ImageID, ImageType = (int)im.ImageType
                    };
                }
            }

            contract.AniDBAnime.AniDBAnime.DefaultImageWideBanner = animeRec.GetDefaultWideBanner()?.ToClient();
        }

        contract.CrossRefAniDBTvDBV2 = RepoFactory.CrossRef_AniDB_TvDB.GetV2LinksFromAnime(AniDB_ID);


        contract.TvDB_Series = sers;
        contract.CrossRefAniDBMovieDB = null;
        if (movieDBCrossRef != null)
        {
            contract.CrossRefAniDBMovieDB = movieDBCrossRef;
            contract.MovieDB_Movie = movie;
        }

        contract.CrossRefAniDBMAL = CrossRefMAL?.ToList() ?? new List<CrossRef_AniDB_MAL>();
        start = DateTime.Now;
        var types = GetConditionTypesChanged(Contract, contract);
        ts = DateTime.Now - start;
        logger.Trace(
            $"While Updating SERIES {GetAnime()?.MainTitle ?? AniDB_ID.ToString()}, Got GroupFilterConditionTypesChanged in {ts.TotalMilliseconds}ms");
        Contract = contract;
        return types;
    }


    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeSeries_User oldcontract,
        CL_AnimeSeries_User newcontract)
    {
        var h = new HashSet<GroupFilterConditionType>();

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
             !(oldcontract.MissingEpisodeCount > 0 ||
               oldcontract.MissingEpisodeCountGroups > 0)) !=
            (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now &&
             !(newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0)))
        {
            h.Add(GroupFilterConditionType.CompletedSeries);
        }

        if (oldcontract == null ||
            (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) !=
            (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
        {
            h.Add(GroupFilterConditionType.MissingEpisodes);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.AniDBAnime.GetAllTags()
                .SetEquals(newcontract.AniDBAnime.AniDBAnime.GetAllTags()))
        {
            h.Add(GroupFilterConditionType.Tag);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.AirDate != newcontract.AniDBAnime.AniDBAnime.AirDate)
        {
            h.Add(GroupFilterConditionType.AirDate);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
            (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))
        {
            h.Add(GroupFilterConditionType.AssignedTvDBInfo);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBMAL == null || oldcontract.CrossRefAniDBMAL.Count == 0) !=
            (newcontract.CrossRefAniDBMAL == null || newcontract.CrossRefAniDBMAL.Count == 0))
        {
            h.Add(GroupFilterConditionType.AssignedMALInfo);
        }

        if (oldcontract == null ||
            oldcontract.CrossRefAniDBMovieDB == null != (newcontract.CrossRefAniDBMovieDB == null))
        {
            h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
        }

        if (oldcontract == null ||
            (oldcontract.CrossRefAniDBMovieDB == null &&
             (oldcontract.CrossRefAniDBTvDBV2 == null || oldcontract.CrossRefAniDBTvDBV2.Count == 0) !=
             (newcontract.CrossRefAniDBMovieDB == null &&
              (newcontract.CrossRefAniDBTvDBV2 == null || newcontract.CrossRefAniDBTvDBV2.Count == 0))))
        {
            h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.AnimeType != newcontract.AniDBAnime.AniDBAnime.AnimeType)
        {
            h.Add(GroupFilterConditionType.AnimeType);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_AllVideoQuality.SetEquals(newcontract.AniDBAnime.Stat_AllVideoQuality) ||
            !oldcontract.AniDBAnime.Stat_AllVideoQuality_Episodes.SetEquals(
                newcontract.AniDBAnime.Stat_AllVideoQuality_Episodes))
        {
            h.Add(GroupFilterConditionType.VideoQuality);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.VoteCount != newcontract.AniDBAnime.AniDBAnime.VoteCount ||
            oldcontract.AniDBAnime.AniDBAnime.TempVoteCount != newcontract.AniDBAnime.AniDBAnime.TempVoteCount ||
            oldcontract.AniDBAnime.AniDBAnime.Rating != newcontract.AniDBAnime.AniDBAnime.Rating ||
            oldcontract.AniDBAnime.AniDBAnime.TempRating != newcontract.AniDBAnime.AniDBAnime.TempRating)
        {
            h.Add(GroupFilterConditionType.AniDBRating);
        }

        if (oldcontract == null || oldcontract.DateTimeCreated != newcontract.DateTimeCreated)
        {
            h.Add(GroupFilterConditionType.SeriesCreatedDate);
        }

        if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
        {
            h.Add(GroupFilterConditionType.EpisodeAddedDate);
        }

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             oldcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now) !=
            (newcontract.AniDBAnime.AniDBAnime.EndDate.HasValue &&
             newcontract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now))
        {
            h.Add(GroupFilterConditionType.FinishedAiring);
        }

        if (oldcontract == null ||
            oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
        {
            h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_AudioLanguages.SetEquals(newcontract.AniDBAnime.Stat_AudioLanguages))
        {
            h.Add(GroupFilterConditionType.AudioLanguage);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.Stat_SubtitleLanguages.SetEquals(newcontract.AniDBAnime.Stat_SubtitleLanguages))
        {
            h.Add(GroupFilterConditionType.SubtitleLanguage);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.AniDBAnime.EpisodeCount != newcontract.AniDBAnime.AniDBAnime.EpisodeCount)
        {
            h.Add(GroupFilterConditionType.EpisodeCount);
        }

        if (oldcontract == null ||
            !oldcontract.AniDBAnime.CustomTags.Select(a => a.TagName)
                .ToHashSet()
                .SetEquals(newcontract.AniDBAnime.CustomTags.Select(a => a.TagName).ToHashSet()))
        {
            h.Add(GroupFilterConditionType.CustomTags);
        }

        if (oldcontract == null || oldcontract.LatestEpisodeAirDate != newcontract.LatestEpisodeAirDate)
        {
            h.Add(GroupFilterConditionType.LatestEpisodeAirDate);
        }

        var oldyear = -1;
        var newyear = -1;
        if (oldcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
        {
            oldyear = oldcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
        }

        if (newcontract?.AniDBAnime?.AniDBAnime?.AirDate != null)
        {
            newyear = newcontract.AniDBAnime.AniDBAnime.AirDate.Value.Year;
        }

        if (oldyear != newyear)
        {
            h.Add(GroupFilterConditionType.Year);
        }

        if (oldcontract?.AniDBAnime?.Stat_AllSeasons == null ||
            !oldcontract.AniDBAnime.Stat_AllSeasons.SetEquals(newcontract.AniDBAnime.Stat_AllSeasons))
        {
            h.Add(GroupFilterConditionType.Season);
        }

        //TODO This three should be moved to AnimeSeries_User in the future...
        if (oldcontract == null ||
            (oldcontract.AniDBAnime.UserVote != null &&
             oldcontract.AniDBAnime.UserVote.VoteType == (int)AniDBVoteType.Anime) !=
            (newcontract.AniDBAnime.UserVote != null &&
             newcontract.AniDBAnime.UserVote.VoteType == (int)AniDBVoteType.Anime))
        {
            h.Add(GroupFilterConditionType.UserVoted);
        }

        if (oldcontract == null ||
            oldcontract.AniDBAnime.UserVote != null != (newcontract.AniDBAnime.UserVote != null))
        {
            h.Add(GroupFilterConditionType.UserVotedAny);
        }

        if (oldcontract == null ||
            (oldcontract.AniDBAnime.UserVote?.VoteValue ?? 0) !=
            (newcontract.AniDBAnime.UserVote?.VoteValue ?? 0))
        {
            h.Add(GroupFilterConditionType.UserRating);
        }

        return h;
    }

    public override string ToString()
    {
        return $"Series: {GetAnime().MainTitle} ({AnimeSeriesID})";
        //return string.Empty;
    }

    internal class EpisodeList : List<EpisodeList.StatEpisodes>
    {
        public EpisodeList(AnimeType ept)
        {
            AnimeType = ept;
        }

        private AnimeType AnimeType { get; set; }

        private readonly Regex partmatch = new("part (\\d.*?) of (\\d.*)");

        private readonly Regex remsymbols = new("[^A-Za-z0-9 ]");

        private readonly Regex remmultispace = new("\\s+");

        public void Add(SVR_AnimeEpisode ep, bool available)
        {
            var hidden = ep.IsHidden;
            if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
            {
                var ename = ep.Title;
                var empty = string.IsNullOrEmpty(ename);
                Match m = null;
                if (!empty)
                {
                    m = partmatch.Match(ename);
                }

                var s = new StatEpisodes.StatEpisode { Available = available, Hidden = hidden };
                if (m?.Success ?? false)
                {
                    int.TryParse(m.Groups[1].Value, out var part_number);
                    int.TryParse(m.Groups[2].Value, out var part_count);
                    var rname = partmatch.Replace(ename, string.Empty);
                    rname = remsymbols.Replace(rname, string.Empty);
                    rname = remmultispace.Replace(rname, " ");


                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Part;
                    s.PartCount = part_count;
                    s.Match = rname.Trim();
                    if (s.Match == "complete movie" || s.Match == "movie" || s.Match == "ova")
                    {
                        s.Match = string.Empty;
                    }
                }
                else
                {
                    if (empty || ename == "complete movie" || ename == "movie" || ename == "ova")
                    {
                        s.Match = string.Empty;
                    }
                    else
                    {
                        var rname = partmatch.Replace(ep.Title, string.Empty);
                        rname = remsymbols.Replace(rname, string.Empty);
                        rname = remmultispace.Replace(rname, " ");
                        s.Match = rname.Trim();
                    }

                    s.EpisodeType = StatEpisodes.StatEpisode.EpType.Complete;
                    s.PartCount = 0;
                }

                StatEpisodes fnd = null;
                foreach (var k in this)
                {
                    foreach (var ss in k)
                    {
                        if (ss.Match == s.Match)
                        {
                            fnd = k;
                            break;
                        }
                    }

                    if (fnd != null)
                    {
                        break;
                    }
                }

                if (fnd == null)
                {
                    var eps = new StatEpisodes();
                    eps.Add(s);
                    Add(eps);
                }
                else
                {
                    fnd.Add(s);
                }
            }
            else
            {
                var eps = new StatEpisodes();
                var es = new StatEpisodes.StatEpisode
                {
                    Match = string.Empty,
                    EpisodeType = StatEpisodes.StatEpisode.EpType.Complete,
                    PartCount = 0,
                    Available = available,
                    Hidden = hidden,
                };
                eps.Add(es);
                Add(eps);
            }
        }

        public class StatEpisodes : List<StatEpisodes.StatEpisode>
        {
            public class StatEpisode
            {
                public enum EpType
                {
                    Complete,
                    Part
                }

                public string Match;
                public int PartCount;
                public EpType EpisodeType { get; set; }
                public bool Available { get; set; }
                public bool Hidden { get; set; }
            }

            public bool Available
            {
                get
                {
                    var maxcnt = this.Select(k => k.PartCount).Concat(new[] { 0 }).Max();
                    var parts = new int[maxcnt + 1];
                    foreach (var k in this)
                    {
                        switch (k.EpisodeType)
                        {
                            case StatEpisode.EpType.Complete when k.Available:
                                return true;
                            case StatEpisode.EpType.Part when k.Available:
                                parts[k.PartCount]++;
                                if (parts[k.PartCount] == k.PartCount)
                                {
                                    return true;
                                }

                                break;
                        }
                    }

                    return false;
                }
            }

            public bool Hidden
                => this.Any(e => e.Hidden);
        }
    }

    public void MoveSeries(SVR_AnimeGroup newGroup, bool updateGroupStats = true)
    {
        // Skip moving series if it's already part of the group.
        if (AnimeGroupID == newGroup.AnimeGroupID)
            return;

        var oldGroupID = AnimeGroupID;
        // Update the stats for the series and group.
        AnimeGroupID = newGroup.AnimeGroupID;
        DateTimeUpdated = DateTime.Now;
        UpdateStats(true, true);
        if (updateGroupStats)
            newGroup.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, true);

        var oldGroup = RepoFactory.AnimeGroup.GetByID(oldGroupID);
        if (oldGroup != null)
        {
            // This was the only one series in the group so delete the now orphan group.
            if (oldGroup.GetAllSeries().Count == 0)
            {
                oldGroup.DeleteGroup(false);
            }
            else
            {
                var updatedOldGroup = false;
                if (oldGroup.DefaultAnimeSeriesID.HasValue && oldGroup.DefaultAnimeSeriesID.Value == AnimeSeriesID)
                {
                    oldGroup.DefaultAnimeSeriesID = null;
                    updatedOldGroup = true;
                }

                if (oldGroup.MainAniDBAnimeID.HasValue && oldGroup.MainAniDBAnimeID.Value == AniDB_ID)
                {
                    oldGroup.MainAniDBAnimeID = null;
                    updatedOldGroup = true;
                }

                if (updatedOldGroup)
                    RepoFactory.AnimeGroup.Save(oldGroup);
            }

            // Update the top group 
            var topGroup = oldGroup.TopLevelAnimeGroup;
            if (topGroup.AnimeGroupID != oldGroup.AnimeGroupID)
            {
                topGroup.UpdateStatsFromTopLevel(true, true);
            }
        }
    }

    public void QueueUpdateStats()
    {
        var scheduler = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler().Result;
        scheduler.StartJob<RefreshAnimeStatsJob>(c => c.AnimeID = AniDB_ID).GetAwaiter().GetResult();
    }

    public void UpdateStats(bool watchedStats, bool missingEpsStats)
    {
        lock (this)
        {
            var start = DateTime.Now;
            var initialStart = DateTime.Now;
            var name = GetAnime()?.MainTitle ?? AniDB_ID.ToString();
            logger.Info(
                $"Starting Updating STATS for SERIES {name} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

            var startEps = DateTime.Now;
            var eps = GetAnimeEpisodes().Where(a => a.AniDB_Episode != null).ToList();
            var tsEps = DateTime.Now - startEps;
            logger.Trace($"Got episodes for SERIES {name} in {tsEps.TotalMilliseconds}ms");

            if (watchedStats)
            {
                var vls = RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID)
                    .Where(a => !string.IsNullOrEmpty(a?.Hash)).Select(xref =>
                        (xref.EpisodeID, VideoLocal: RepoFactory.VideoLocal.GetByHash(xref.Hash)))
                    .Where(a => a.VideoLocal != null).ToLookup(a => a.EpisodeID, b => b.VideoLocal);
                var vlUsers = vls.SelectMany(
                    xref =>
                    {
                        var users = xref?.SelectMany(a => RepoFactory.VideoLocalUser.GetByVideoLocalID(a.VideoLocalID));
                        return users?.Select(a => (EpisodeID: xref.Key, VideoLocalUser: a)) ??
                               Array.Empty<(int EpisodeID, SVR_VideoLocal_User VideoLocalUser)>();
                    }
                ).Where(a => a.VideoLocalUser != null).ToLookup(a => (a.EpisodeID, UserID: a.VideoLocalUser.JMMUserID),
                    b => b.VideoLocalUser);
                var epUsers = eps.SelectMany(
                        ep =>
                        {
                            var users = RepoFactory.AnimeEpisode_User.GetByEpisodeID(ep.AnimeEpisodeID);
                            return users.Select(a => (EpisodeID: ep.AniDB_EpisodeID, AnimeEpisode_User: a));
                        }
                    ).Where(a => a.AnimeEpisode_User != null)
                    .ToLookup(a => (a.EpisodeID, UserID: a.AnimeEpisode_User.JMMUserID), b => b.AnimeEpisode_User);

                foreach (var juser in RepoFactory.JMMUser.GetAll())
                {
                    var userRecord = GetOrCreateUserRecord(juser.JMMUserID);

                    var unwatchedCount = 0;
                    var hiddenUnwatchedCount = 0;
                    var watchedCount = 0;
                    var watchedEpisodeCount = 0;
                    DateTime? lastEpisodeUpdate = null;
                    DateTime? watchedDate = null;

                    var lck = new object();

                    eps.AsParallel().Where(ep =>
                        vls.Contains(ep.AniDB_EpisodeID) &&
                        ep.EpisodeTypeEnum is EpisodeType.Episode or EpisodeType.Special).ForAll(
                        ep =>
                        {
                            SVR_VideoLocal_User vlUser = null;
                            if (vlUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                            {
                                vlUser = vlUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                                    .OrderByDescending(a => a.LastUpdated)
                                    .FirstOrDefault(a => a.WatchedDate != null);
                            }

                            var lastUpdated = vlUser?.LastUpdated;

                            SVR_AnimeEpisode_User epUser = null;
                            if (epUsers.Contains((ep.AniDB_EpisodeID, juser.JMMUserID)))
                            {
                                epUser = epUsers[(ep.AniDB_EpisodeID, juser.JMMUserID)]
                                    .FirstOrDefault(a => a.WatchedDate != null);
                            }

                            if (vlUser?.WatchedDate == null && epUser?.WatchedDate == null)
                            {
                                if (ep.IsHidden)
                                    Interlocked.Increment(ref hiddenUnwatchedCount);
                                else
                                    Interlocked.Increment(ref unwatchedCount);
                                return;
                            }

                            lock (lck)
                            {
                                if (vlUser != null)
                                {
                                    if (watchedDate == null || (vlUser.WatchedDate != null &&
                                                                vlUser.WatchedDate.Value > watchedDate.Value))
                                    {
                                        watchedDate = vlUser.WatchedDate;
                                    }

                                    if (lastEpisodeUpdate == null || lastUpdated.Value > lastEpisodeUpdate.Value)
                                    {
                                        lastEpisodeUpdate = lastUpdated;
                                    }
                                }

                                if (epUser != null)
                                {
                                    if (watchedDate == null || (epUser.WatchedDate != null &&
                                                                epUser.WatchedDate.Value > watchedDate.Value))
                                    {
                                        watchedDate = epUser.WatchedDate;
                                    }
                                }
                            }

                            Interlocked.Increment(ref watchedEpisodeCount);
                            Interlocked.Add(ref watchedCount, vlUser?.WatchedCount ?? epUser.WatchedCount);
                        });
                    userRecord.UnwatchedEpisodeCount = unwatchedCount;
                    userRecord.HiddenUnwatchedEpisodeCount = hiddenUnwatchedCount;
                    userRecord.WatchedEpisodeCount = watchedEpisodeCount;
                    userRecord.WatchedCount = watchedCount;
                    userRecord.WatchedDate = watchedDate;
                    userRecord.LastEpisodeUpdate = lastEpisodeUpdate;
                    RepoFactory.AnimeSeries_User.Save(userRecord);
                }
            }

            var ts = DateTime.Now - start;
            logger.Trace($"Updated WATCHED stats for SERIES {name} in {ts.TotalMilliseconds}ms");
            start = DateTime.Now;

            if (missingEpsStats)
            {
                var animeType = GetAnime()?.GetAnimeTypeEnum() ?? AnimeType.TVSeries;

                MissingEpisodeCount = 0;
                MissingEpisodeCountGroups = 0;
                HiddenMissingEpisodeCount = 0;
                HiddenMissingEpisodeCountGroups = 0;

                // get all the group status records
                var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(AniDB_ID);

                // find all the episodes for which the user has a file
                // from this we can determine what their latest episode number is
                // find out which groups the user is collecting

                var latestLocalEpNumber = 0;
                DateTime? lastEpAirDate = null;
                var epReleasedList = new EpisodeList(animeType);
                var epGroupReleasedList = new EpisodeList(animeType);
                var daysofweekcounter = new Dictionary<DayOfWeek, int>();

                var userReleaseGroups = eps.Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).SelectMany(
                    a =>
                    {
                        var vls = a.GetVideoLocals();
                        if (!vls.Any())
                        {
                            return Array.Empty<int>();
                        }

                        var aniFiles = vls.Select(b => b.GetAniDBFile()).Where(b => b != null).ToList();
                        if (!aniFiles.Any())
                        {
                            return Array.Empty<int>();
                        }

                        return aniFiles.Select(b => b.GroupID);
                    }
                ).ToList();

                var videoLocals = eps.Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).SelectMany(a =>
                        a.GetVideoLocals().Select(b => new
                        {
                            a.AniDB_EpisodeID, VideoLocal = b
                        }))
                    .ToLookup(a => a.AniDB_EpisodeID, a => a.VideoLocal);

                // This was always Episodes only. Maybe in the future, we'll have a reliable way to check specials.
                eps.AsParallel().Where(a => a.EpisodeTypeEnum == EpisodeType.Episode).ForAll(ep =>
                {
                    var vids = videoLocals[ep.AniDB_EpisodeID].ToList();

                    var aniEp = ep.AniDB_Episode;
                    var thisEpNum = aniEp.EpisodeNumber;

                    if (thisEpNum > latestLocalEpNumber && vids.Any())
                    {
                        latestLocalEpNumber = thisEpNum;
                    }

                    var airdate = ep.AniDB_Episode.GetAirDateAsDate();

                    // Only count episodes that have already aired
                    if (!aniEp.GetFutureDated())
                    {
                        // Only convert if we have time info
                        DateTime airdateLocal;
                        // ignore the possible null on airdate, it's checked in GetFutureDated
                        if (airdate!.Value.Hour == 0 && airdate.Value.Minute == 0 && airdate.Value.Second == 0)
                        {
                            airdateLocal = airdate.Value;
                        }
                        else
                        {
                            airdateLocal = DateTime.SpecifyKind(airdate.Value, DateTimeKind.Unspecified);
                            airdateLocal = TimeZoneInfo.ConvertTime(airdateLocal,
                                TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), TimeZoneInfo.Local);
                        }

                        lock (daysofweekcounter)
                        {
                            if (!daysofweekcounter.ContainsKey(airdateLocal.DayOfWeek))
                            {
                                daysofweekcounter.Add(airdateLocal.DayOfWeek, 0);
                            }

                            daysofweekcounter[airdateLocal.DayOfWeek]++;
                        }

                        if (lastEpAirDate == null || lastEpAirDate < airdate)
                        {
                            lastEpAirDate = airdate.Value;
                        }
                    }

                    // does this episode have a file released
                    // does this episode have a file released by the group the user is collecting
                    var epReleased = false;
                    var epReleasedGroup = false;
                    foreach (var gs in grpStatuses)
                    {
                        // if it's complete, then assume the episode is included
                        if (gs.CompletionState is (int)Group_CompletionStatus.Complete or (int)Group_CompletionStatus.Finished)
                        {
                            epReleased = true;
                            if (userReleaseGroups.Contains(gs.GroupID)) epReleasedGroup = true;
                            continue;
                        }

                        if (!gs.HasGroupReleasedEpisode(thisEpNum)) continue;

                        epReleased = true;
                        if (userReleaseGroups.Contains(gs.GroupID)) epReleasedGroup = true;
                    }

                    try
                    {
                        lock (epReleasedList)
                        {
                            epReleasedList.Add(ep, !epReleased || vids.Any());
                        }

                        lock (epGroupReleasedList)
                        {
                            epGroupReleasedList.Add(ep, !epReleasedGroup || vids.Any());
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Trace($"Error updating release group stats {e}");
                        throw;
                    }
                });

                foreach (var eplst in epReleasedList)
                {
                    if (!eplst.Available)
                    {
                        if (eplst.Hidden)
                            HiddenMissingEpisodeCount++;
                        else
                            MissingEpisodeCount++;
                    }
                }

                foreach (var eplst in epGroupReleasedList)
                {
                    if (!eplst.Available)
                    {
                        if (eplst.Hidden)
                            HiddenMissingEpisodeCountGroups++;
                        else
                            MissingEpisodeCountGroups++;
                    }
                }

                LatestLocalEpisodeNumber = latestLocalEpNumber;
                if (daysofweekcounter.Count > 0)
                {
                    AirsOn = daysofweekcounter.OrderByDescending(a => a.Value).FirstOrDefault().Key;
                }

                LatestEpisodeAirDate = lastEpAirDate;
            }

            ts = DateTime.Now - start;
            logger.Trace($"Updated MISSING EPS stats for SERIES {name} in {ts.TotalMilliseconds}ms");
            start = DateTime.Now;

            // Skip group filters if we are doing group stats, as the group stats will regenerate group filters
            RepoFactory.AnimeSeries.Save(this, false, false);
            ts = DateTime.Now - start;
            logger.Trace($"Saved stats for SERIES {name} in {ts.TotalMilliseconds}ms");


            ts = DateTime.Now - initialStart;
            logger.Info($"Finished updating stats for SERIES {name} in {ts.TotalMilliseconds}ms");
        }
    }

    public static Dictionary<SVR_AnimeSeries, CrossRef_Anime_Staff> SearchSeriesByStaff(string staffname,
        bool fuzzy = false)
    {
        var allseries = RepoFactory.AnimeSeries.GetAll();
        var results = new Dictionary<SVR_AnimeSeries, CrossRef_Anime_Staff>();
        var stringsToSearchFor = new List<string>();
        if (staffname.Contains(" "))
        {
            stringsToSearchFor.AddRange(staffname.Split(' ').GetPermutations()
                .Select(permutation => string.Join(" ", permutation)));
            stringsToSearchFor.Remove(staffname);
            stringsToSearchFor.Insert(0, staffname);
        }
        else
        {
            stringsToSearchFor.Add(staffname);
        }

        foreach (var series in allseries)
        {
            List<(CrossRef_Anime_Staff, AnimeStaff)> staff = RepoFactory.CrossRef_Anime_Staff
                .GetByAnimeID(series.AniDB_ID).Select(a => (a, RepoFactory.AnimeStaff.GetByID(a.StaffID))).ToList();

            foreach (var animeStaff in staff)
            foreach (var search in stringsToSearchFor)
            {
                if (fuzzy)
                {
                    if (!animeStaff.Item2.Name.FuzzyMatch(search))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!animeStaff.Item2.Name.Equals(search, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                }

                if (!results.ContainsKey(series))
                {
                    results.Add(series, animeStaff.Item1);
                }
                else
                {
                    if (!Enum.TryParse(results[series].Role, out CharacterAppearanceType type1))
                    {
                        continue;
                    }

                    if (!Enum.TryParse(animeStaff.Item1.Role, out CharacterAppearanceType type2))
                    {
                        continue;
                    }

                    var comparison = ((int)type1).CompareTo((int)type2);
                    if (comparison == 1)
                    {
                        results[series] = animeStaff.Item1;
                    }
                }

                goto label0;
            }

            // People hate goto, but this is a legit use for it.
            label0: ;
        }

        return results;
    }

    public async Task DeleteSeries(bool deleteFiles, bool updateGroups, bool completelyRemove = false)
    {
        foreach (var ep in GetAnimeEpisodes())
        {
            var service = Utils.ServiceContainer.GetRequiredService<VideoLocal_PlaceService>();
            foreach (var place in GetVideoLocals().SelectMany(a => a.Places).Where(a => a != null))
            {
                if (deleteFiles) await service.RemoveRecordAndDeletePhysicalFile(place, false);
                else await service.RemoveRecord(place);
            }

            RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
        }
        RepoFactory.AnimeSeries.Delete(this);

        if (!updateGroups)
        {
            return;
        }

        // finally update stats
        var grp = AnimeGroup;
        if (grp != null)
        {
            if (!grp.GetAllSeries().Any())
            {
                // Find the topmost group without series
                var parent = grp;
                while (true)
                {
                    var next = parent.Parent;
                    if (next == null || next.GetAllSeries().Any())
                    {
                        break;
                    }

                    parent = next;
                }

                parent.DeleteGroup();
            }
            else
            {
                grp.UpdateStatsFromTopLevel(true, true);
            }
        }

        if (completelyRemove)
        {
            // episodes, anime, characters, images, staff relations, tag relations, titles
            var images = RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeID(AniDB_ID);
            RepoFactory.AniDB_Anime_DefaultImage.Delete(images);

            var characterXrefs = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AniDB_ID);
            var characters = characterXrefs.Select(a => RepoFactory.AniDB_Character.GetByCharID(a.CharID)).ToList();
            var seiyuuXrefs = characters.SelectMany(a => RepoFactory.AniDB_Character_Seiyuu.GetByCharID(a.CharID)).ToList();
            RepoFactory.AniDB_Character_Seiyuu.Delete(seiyuuXrefs);
            RepoFactory.AniDB_Character.Delete(characters);
            RepoFactory.AniDB_Anime_Character.Delete(characterXrefs);

            var staffXrefs = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(AniDB_ID);
            RepoFactory.AniDB_Anime_Staff.Delete(staffXrefs);

            var tagXrefs = RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AniDB_ID);
            RepoFactory.AniDB_Anime_Tag.Delete(tagXrefs);

            var titles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(AniDB_ID);
            RepoFactory.AniDB_Anime_Title.Delete(titles);

            var aniDBEpisodes = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID);
            var episodeTitles = aniDBEpisodes.SelectMany(a => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(a.EpisodeID)).ToList();
            RepoFactory.AniDB_Episode_Title.Delete(episodeTitles);
            RepoFactory.AniDB_Episode.Delete(aniDBEpisodes);

            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AniDB_ID);
            RepoFactory.AniDB_AnimeUpdate.Delete(update);
        }
    }
}
