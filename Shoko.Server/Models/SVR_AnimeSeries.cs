using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NHibernate.Util;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models;

public class SVR_AnimeSeries : AnimeSeries
{
    #region DB Columns

    public int ContractVersion { get; set; }
    public byte[] ContractBlob { get; set; }
    public int ContractSize { get; set; }

    public DateTime UpdatedAt { get; set; }

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
        string seriesName;
        if (!string.IsNullOrEmpty(SeriesNameOverride))
        {
            seriesName = SeriesNameOverride;
        }
        else
        {
            if (ServerSettings.Instance.SeriesNameSource == DataSourceType.AniDB)
            {
                seriesName = GetAnime().PreferredTitle;
            }
            else
            {
                var tvdbs = GetTvDBSeries();

                if (tvdbs != null && tvdbs.Count > 0 && !string.IsNullOrEmpty(tvdbs[0].SeriesName) &&
                    !tvdbs[0].SeriesName.ToUpper().Contains("**DUPLICATE"))
                {
                    seriesName = tvdbs[0].SeriesName;
                }
                else
                {
                    seriesName = GetAnime().PreferredTitle;
                }
            }
        }

        return seriesName;
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


    public List<SVR_AnimeEpisode> GetAnimeEpisodes(bool orderList = false)
    {
        if (orderList)
        {
            // TODO: Convert to a LINQ query once we've switched to EF Core.
            return RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
                .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
                .OrderBy(tuple => tuple.anidbEpisode.EpisodeType)
                .ThenBy(tuple => tuple.anidbEpisode.EpisodeNumber)
                .Select(tuple => tuple.episode)
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
        var sers = new List<TvDB_Series>();

        var xrefs = GetCrossRefTvDB();
        if (xrefs == null || xrefs.Count == 0)
        {
            return sers;
        }

        foreach (var xref in xrefs)
        {
            var series = xref.GetTvDBSeries();
            if (series != null)
            {
                sers.Add(series);
            }
        }

        return sers;
    }

    #endregion

    #region Trakt

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetCrossRefTraktV2(session);
        }
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
    {
        return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, AniDB_ID);
    }

    public List<Trakt_Show> GetTraktShow()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetTraktShow(session);
        }
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

    public CL_AnimeSeries_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null)
    {
        try
        {
            var contract = (CL_AnimeSeries_User)Contract?.Clone();
            if (contract == null)
            {
                logger.Trace($"Series with ID [{AniDB_ID}] has a null contract on get. Updating");
                RepoFactory.AnimeSeries.Save(this, false, false, true);
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

    public Video GetPlexContract(int userid)
    {
        var ser = GetUserContract(userid);
        var v = GetOrCreateUserRecord(userid).PlexContract;
        v.Title = ser.AniDBAnime.AniDBAnime.FormattedTitle;
        return v;
    }

    public SVR_AnimeSeries_User GetUserRecord(int userID)
    {
        return RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, AnimeSeriesID);
    }

    public SVR_AnimeSeries_User GetOrCreateUserRecord(int userID)
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
            .Where(tuple => tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode ||
                            (includeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special))
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
    /// Get the next episode for the series for a user.
    /// </summary>
    /// <param name="userID">User ID</param>
    /// <param name="onlyUnwatched">Only check for unwatched episodes.</param>
    /// <param name="includeSpecials">Include specials when searching.</param>
    /// <returns></returns>
    public SVR_AnimeEpisode GetNextEpisode(int userID, bool onlyUnwatched, bool includeSpecials = true)
    {
        // Filter the episodes to only normal or special episodes and order them in rising order.
        var episodes = GetAnimeEpisodes()
            .Select(episode => (episode, episode.AniDB_Episode))
            .Where(tuple => tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Episode ||
                            (includeSpecials && tuple.AniDB_Episode.EpisodeType == (int)EpisodeType.Special))
            .OrderBy(tuple => tuple.AniDB_Episode.EpisodeType)
            .ThenBy(tuple => tuple.AniDB_Episode.EpisodeNumber)
            .Select(tuple => tuple.episode)
            .ToList();
        // Look for active watch sessions and return the episode for the most recent session if found.
        if (!onlyUnwatched)
        {
            var (episode, _) = episodes
                .SelectMany(episode => episode.GetVideoLocals().Select(file => (episode, file.GetUserRecord(userID))))
                .Where(tuple => tuple.Item2 != null)
                .OrderByDescending(tuple => tuple.Item2.LastUpdated)
                .FirstOrDefault(tuple => tuple.Item2.ResumePosition > 0);
            if (episode != null)
            {
                return episode;
            }
        }
        // Skip check if there is an active watch session for the series and we don't allow active watch sessions.
        else if (episodes.Any(episode =>
                     episode.GetVideoLocals().Any(file => (file.GetUserRecord(userID)?.ResumePosition ?? 0) > 0)))
        {
            return null;
        }

        // Find the first episode that's unwatched.
        return episodes
            .Where(episode =>
            {
                var episodeUserRecord = episode.GetUserRecord(userID);
                if (episodeUserRecord == null)
                {
                    return true;
                }

                return episodeUserRecord.WatchedCount == 0 || !episodeUserRecord.WatchedDate.HasValue;
            })
            .FirstOrDefault(episode => episode.GetVideoLocals().Count > 0);
    }

    public SVR_AniDB_Anime GetAnime()
    {
        return RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);
    }

    public DateTime AirDate
    {
        get
        {
            var anime = GetAnime();
            if (anime?.AirDate != null)
            {
                return anime.AirDate.Value;
            }

            // This will be slower, but hopefully more accurate
            var ep = GetAnimeEpisodes()
                .Select(a => a.AniDB_Episode.GetAirDateAsDate()).Where(a => a != null).OrderBy(a => a)
                .FirstOrDefault();
            if (ep != null)
            {
                return ep.Value;
            }

            return DateTime.MinValue;
        }
    }

    public DateTime? EndDate
    {
        get
        {
            if (GetAnime() != null)
            {
                return GetAnime().EndDate;
            }

            return null;
        }
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

    public void CreateAnimeEpisodes(SVR_AniDB_Anime anime = null)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            CreateAnimeEpisodes(session, anime);
        }
    }

    public void CreateAnimeEpisodes(ISession session, SVR_AniDB_Anime anime = null)
    {
        anime = anime ?? GetAnime();
        if (anime == null)
        {
            return;
        }

        var eps = anime.GetAniDBEpisodes();
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
            ep.CreateAnimeEpisode(session, AnimeSeriesID);
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

            while (parentGroup?.AnimeGroupParentID != null)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentGroup.AnimeGroupParentID.Value);
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
                int? groupID = AnimeGroupID;
                while (groupID.HasValue)
                {
                    var grp = RepoFactory.AnimeGroup.GetByID(groupID.Value);
                    if (grp != null)
                    {
                        grps.Add(grp);
                        groupID = grp.AnimeGroupParentID;
                    }
                    else
                    {
                        groupID = null;
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

    public void UpdateGroupFilters(HashSet<GroupFilterConditionType> types, SVR_JMMUser user = null)
    {
        IReadOnlyList<SVR_JMMUser> users = new List<SVR_JMMUser> { user };
        if (user == null)
        {
            users = RepoFactory.JMMUser.GetAll();
        }

        var tosave = new List<SVR_GroupFilter>();

        var n = new HashSet<GroupFilterConditionType>(types);
        var gfs = RepoFactory.GroupFilter.GetWithConditionTypesAndAll(n);
        logger.Trace($"Updating {gfs.Count} Group Filters from Series {GetAnime().MainTitle}");
        foreach (var gf in gfs)
        {
            if (gf.UpdateGroupFilterFromSeries(Contract, null))
            {
                if (!tosave.Contains(gf))
                {
                    tosave.Add(gf);
                }
            }

            foreach (var u in users)
            {
                var cgrp = GetUserContract(u.JMMUserID, n);

                if (gf.UpdateGroupFilterFromSeries(cgrp, u))
                {
                    if (!tosave.Contains(gf))
                    {
                        tosave.Add(gf);
                    }
                }
            }
        }

        RepoFactory.GroupFilter.Save(tosave);
    }

    public void DeleteFromFilters()
    {
        foreach (var gf in RepoFactory.GroupFilter.GetAll())
        {
            var change = false;
            foreach (var k in gf.SeriesIds.Keys)
            {
                if (gf.SeriesIds[k].Contains(AnimeSeriesID))
                {
                    gf.SeriesIds[k].Remove(AnimeSeriesID);
                    change = true;
                }
            }

            if (change)
            {
                RepoFactory.GroupFilter.Save(gf);
            }
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
            if (AnimeType == AnimeType.OVA || AnimeType == AnimeType.Movie)
            {
                var ename = ep.Title;
                var empty = string.IsNullOrEmpty(ename);
                Match m = null;
                if (!empty)
                {
                    m = partmatch.Match(ename);
                }

                var s = new StatEpisodes.StatEpisode { Available = available };
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
                    Available = available
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
        }
    }

    public void MoveSeries(SVR_AnimeGroup newGroup)
    {
        // Update the stats for the series and group.
        AnimeGroupID = newGroup.AnimeGroupID;
        DateTimeUpdated = DateTime.Now;
        UpdateStats(true, true, true);

        var oldGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);
        if (oldGroup != null)
        {
            // This was the only one series in the group so delete the now orphan group.
            if (oldGroup.GetAllSeries().Count == 0)
            {
                oldGroup.DeleteGroup(false);
            }

            // Update the top group 
            var topGroup = oldGroup.TopLevelAnimeGroup;
            if (topGroup.AnimeGroupID != oldGroup.AnimeGroupID)
            {
                topGroup.UpdateStatsFromTopLevel(true, true, true);
            }
        }
    }

    public void QueueUpdateStats()
    {
        var commandFactory = ShokoServer.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        var cmdRefreshAnime = commandFactory.Create<CommandRequest_RefreshAnime>(c => c.AnimeID = AniDB_ID);
        cmdRefreshAnime.Save();
    }

    public void UpdateStats(bool watchedStats, bool missingEpsStats, bool updateAllGroupsAbove)
    {
        var start = DateTime.Now;
        var initialStart = DateTime.Now;
        var name = GetAnime()?.MainTitle ?? AniDB_ID.ToString();
        logger.Info(
            $"Starting Updating STATS for SERIES {name} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}, Update Group Stats: {updateAllGroupsAbove}");

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
                    a.GetVideoLocals().Select(b => new { a.AniDB_EpisodeID, VideoLocal = b }))
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
                    if (gs.LastEpisodeNumber >= thisEpNum)
                    {
                        epReleased = true;
                    }

                    if (userReleaseGroups.Contains(gs.GroupID) && gs.HasGroupReleasedEpisode(thisEpNum))
                    {
                        epReleasedGroup = true;
                    }
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
                    MissingEpisodeCount++;
                }
            }

            foreach (var eplst in epGroupReleasedList)
            {
                if (!eplst.Available)
                {
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
        RepoFactory.AnimeSeries.Save(this, false, false, updateAllGroupsAbove);
        ts = DateTime.Now - start;
        logger.Trace($"Saved stats for SERIES {name} in {ts.TotalMilliseconds}ms");

        if (updateAllGroupsAbove)
        {
            start = DateTime.Now;
            AnimeGroup?.TopLevelAnimeGroup?.UpdateStatsFromTopLevel(true, watchedStats, missingEpsStats);
            ts = DateTime.Now - start;
            logger.Trace($"Updated group stats for SERIES {name} in {ts.TotalMilliseconds}ms");
        }

        ts = DateTime.Now - initialStart;
        logger.Info($"Finished updating stats for SERIES {name} in {ts.TotalMilliseconds}ms");
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
                    if (!animeStaff.Item2.Name.FuzzyMatches(search))
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

    public void DeleteSeries(bool deleteFiles, bool updateGroups)
    {
        GetAnimeEpisodes().ForEach(ep =>
        {
            ep.RemoveVideoLocals(deleteFiles);
            RepoFactory.AnimeEpisode.Delete(ep.AnimeEpisodeID);
        });
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
                grp.UpdateStatsFromTopLevel(true, true, true);
            }
        }
    }
}
