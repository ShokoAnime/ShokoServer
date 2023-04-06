using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;
using AnimeType = Shoko.Models.Enums.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models;

public class SVR_AnimeGroup : AnimeGroup, IGroup
{
    #region DB Columns

    public int ContractVersion { get; set; }
    public byte[] ContractBlob { get; set; }
    public int ContractSize { get; set; }

    #endregion

    public const int CONTRACT_VERSION = 6;


    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Get a predictable sort name that stuffs everything that's not between
    /// A-Z under #.
    /// </summary>
    public string GetSortName()
    {
        var sortName = !string.IsNullOrWhiteSpace(SortName) ? SortName.ToUpperInvariant() :
                !string.IsNullOrWhiteSpace(GroupName) ? GroupName.ToUpperInvariant() : "";
        var initialChar = (short)(sortName.Length > 0 ? sortName[0] : ' ');
        return initialChar is >= 65 and <= 90 ? sortName : "#" + sortName;
    }


    internal CL_AnimeGroup_User _contract;

    public virtual CL_AnimeGroup_User Contract
    {
        get
        {
            if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
            {
                _contract = CompressionHelper.DeserializeObject<CL_AnimeGroup_User>(ContractBlob, ContractSize);
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

    public SVR_AnimeGroup Parent => AnimeGroupParentID.HasValue
        ? RepoFactory.AnimeGroup.GetByID(AnimeGroupParentID.Value)
        : null;

    public void CollectContractMemory()
    {
        _contract = null;
    }

    public CL_AnimeGroup_User GetUserContract(int userid, HashSet<GroupFilterConditionType> types = null, bool cloned = true)
    {
        if (Contract == null)
        {
            return new CL_AnimeGroup_User();
        }

        var contract = Contract;
        if (cloned && contract != null) contract = contract.DeepCopy();
        var rr = GetUserRecord(userid);
        if (rr != null)
        {
            contract.IsFave = rr.IsFave;
            contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
            contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
            contract.WatchedDate = rr.WatchedDate;
            contract.PlayedCount = rr.PlayedCount;
            contract.WatchedCount = rr.WatchedCount;
            contract.StoppedCount = rr.StoppedCount;
        }
        else if (types != null)
        {
            if (!types.Contains(GroupFilterConditionType.HasUnwatchedEpisodes))
            {
                types.Add(GroupFilterConditionType.HasUnwatchedEpisodes);
            }

            if (!types.Contains(GroupFilterConditionType.Favourite))
            {
                types.Add(GroupFilterConditionType.Favourite);
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

        return contract;
    }

    public Video GetPlexContract(int userid)
    {
        return GetOrCreateUserRecord(userid).PlexContract;
    }

    private SVR_AnimeGroup_User GetOrCreateUserRecord(int userid)
    {
        var rr = GetUserRecord(userid);
        if (rr != null)
        {
            return rr;
        }

        rr = new SVR_AnimeGroup_User(userid, AnimeGroupID)
        {
            WatchedCount = 0,
            UnwatchedEpisodeCount = 0,
            PlayedCount = 0,
            StoppedCount = 0,
            WatchedEpisodeCount = 0,
            WatchedDate = null
        };
        RepoFactory.AnimeGroup_User.Save(rr);
        return rr;
    }

    public static bool IsRelationTypeInExclusions(string type)
    {
        var list = Utils.SettingsProvider.GetSettings().AutoGroupSeriesRelationExclusions.Split('|');
        return list.Any(a => a.Equals(type, StringComparison.InvariantCultureIgnoreCase));
    }

    public SVR_AnimeGroup_User GetUserRecord(int userID)
    {
        return RepoFactory.AnimeGroup_User.GetByUserAndGroupID(userID, AnimeGroupID);
    }

    public bool HasCustomName()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
        {
            var groupingCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
            return HasCustomName(groupingCalculator);
        }
    }

    /// <summary>
    /// This checks all related anime by settings for the GroupName. It will likely be slow
    /// </summary>
    /// <returns></returns>
    public bool HasCustomName(AutoAnimeGroupCalculator groupingCalculator)
    {
        var animeID = GetSeries().FirstOrDefault()?.GetAnime().AnimeID ?? 0;
        if (animeID == 0)
        {
            return false;
        }

        var animes = groupingCalculator.GetIdsOfAnimeInSameGroup(animeID)
            .Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a)).Where(a => a != null)
            .SelectMany(a => a.GetAllTitles()).ToHashSet();
        return !animes.Contains(GroupName);
    }

    /// <summary>
    /// Renames all Anime groups based on the user's language preferences
    /// </summary>
    public static void RenameAllGroups()
    {
        logger.Info("Starting RenameAllGroups");
        using (var session = DatabaseFactory.SessionFactory.OpenStatelessSession())
        {
            var groupingCalculator = AutoAnimeGroupCalculator.CreateFromServerSettings();
            foreach (var grp in RepoFactory.AnimeGroup.GetAll().ToList())
            {
                var list = grp.GetSeries();

                // rename the group if it only has one direct child Anime Series
                if (list.Count == 1)
                {
                    var newTitle = list[0].GetSeriesName();
                    grp.GroupName = newTitle;
                    grp.SortName = newTitle;
                    RepoFactory.AnimeGroup.Save(grp, true, true);
                }
                else if (list.Count > 1)
                {
                    #region Naming

                    SVR_AnimeSeries series = null;
                    var hasCustomName = grp.HasCustomName(groupingCalculator);
                    if (grp.DefaultAnimeSeriesID.HasValue)
                    {
                        series = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value);
                        if (series == null)
                        {
                            grp.DefaultAnimeSeriesID = null;
                        }
                    }

                    if (!grp.DefaultAnimeSeriesID.HasValue)
                    {
                        foreach (var ser in list)
                        {
                            if (ser == null)
                            {
                                continue;
                            }

                            // Check all titles for custom naming, in case user changed language preferences
                            if (ser.SeriesNameOverride.Equals(grp.GroupName))
                            {
                                hasCustomName = false;
                            }
                            else
                            {
                                if (hasCustomName)
                                {
                                    #region tvdb names

                                    var tvdbs = ser.GetTvDBSeries();
                                    if (tvdbs != null && tvdbs.Count != 0)
                                    {
                                        if (tvdbs.Any(tvdbser => tvdbser.SeriesName.Equals(grp.GroupName)))
                                        {
                                            hasCustomName = false;
                                        }
                                    }

                                    #endregion
                                }

                                if (series == null)
                                {
                                    series = ser;
                                    continue;
                                }

                                if (ser.AirDate < series.AirDate)
                                {
                                    series = ser;
                                }
                            }
                        }
                    }

                    if (series != null)
                    {
                        var newTitle = series.GetSeriesName();
                        if (grp.DefaultAnimeSeriesID.HasValue &&
                            grp.DefaultAnimeSeriesID.Value != series.AnimeSeriesID)
                        {
                            newTitle = RepoFactory.AnimeSeries.GetByID(grp.DefaultAnimeSeriesID.Value)
                                .GetSeriesName();
                        }

                        if (hasCustomName)
                        {
                            newTitle = grp.GroupName;
                        }

                        // reset tags, description, etc to new series
                        grp.Populate(series);
                        grp.GroupName = newTitle;
                        grp.SortName = newTitle;
                        RepoFactory.AnimeGroup.Save(grp, true, true);
                        grp.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, false);
                    }

                    #endregion
                }
            }
        }

        logger.Info("Finished RenameAllGroups");
    }


    public List<SVR_AniDB_Anime> Anime =>
        GetSeries().Select(serie => serie.GetAnime()).Where(anime => anime != null).ToList();

    public decimal AniDBRating
    {
        get
        {
            try
            {
                decimal totalRating = 0;
                var totalVotes = 0;

                foreach (var anime in Anime)
                {
                    totalRating += anime.GetAniDBTotalRating();
                    totalVotes += anime.GetAniDBTotalVotes();
                }

                if (totalVotes == 0)
                {
                    return 0;
                }

                return totalRating / totalVotes;
            }
            catch (Exception ex)
            {
                logger.Error($"Error in  AniDBRating: {ex}");
                return 0;
            }
        }
    }

    public List<SVR_AnimeGroup> GetChildGroups()
    {
        return RepoFactory.AnimeGroup.GetByParentID(AnimeGroupID);
    }

    public List<SVR_AnimeGroup> GetAllChildGroups()
    {
        var grpList = new List<SVR_AnimeGroup>();
        GetAnimeGroupsRecursive(AnimeGroupID, ref grpList);
        return grpList;
    }

    /// <summary>
    /// Get the main series for the group.
    /// </summary>
    public SVR_AnimeSeries GetMainSeries()
    {
        SVR_AnimeSeries series = null;

        // User overridden main series.
        if (DefaultAnimeSeriesID.HasValue)
        {
            series = RepoFactory.AnimeSeries.GetByID(DefaultAnimeSeriesID.Value);
            if (series != null)
            {
                return series;
            }
        }

        // Auto selected main series.
        if (MainAniDBAnimeID.HasValue)
        {
            series = RepoFactory.AnimeSeries.GetByAnimeID(MainAniDBAnimeID.Value);
            if (series != null)
            {
                return series;
            }
        }

        // Earliest airing series.
        return GetAllSeries()
            .OrderBy(series => series.GetAnime()?.AirDate ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    public void SetMainSeries(SVR_AnimeSeries series)
    {
        // Set the id before potentially reseting the fields, so the getter uses
        // the new id instead of the old.
        DefaultAnimeSeriesID = series?.AnimeSeriesID;

        // Reset the name/description if the group is not manually named.
        if (series == null && (IsManuallyNamed == 0 || OverrideDescription == 0))
            series = GetMainSeries();
        if (IsManuallyNamed == 0)
            GroupName = SortName = series.GetSeriesName();
        if (OverrideDescription == 0)
            Description = series.GetAnime().Description;

        // Save the changes for this group only.
        DateTimeUpdated = DateTime.Now;
        RepoFactory.AnimeGroup.Save(this, false, false);
    }

    public List<SVR_AnimeSeries> GetSeries()
    {
        var seriesList = RepoFactory.AnimeSeries.GetByGroupID(AnimeGroupID);
        // Make everything that relies on GetSeries[0] have the proper result
        seriesList = seriesList.OrderBy(a => a.Year).ThenBy(a => a.AirDate).ToList();
        if (!DefaultAnimeSeriesID.HasValue)
        {
            return seriesList;
        }

        var series = RepoFactory.AnimeSeries.GetByID(DefaultAnimeSeriesID.Value);
        if (series == null)
        {
            return seriesList;
        }

        seriesList.Remove(series);
        seriesList.Insert(0, series);
        return seriesList;
    }

    public List<SVR_AnimeSeries> GetAllSeries(bool skipSorting = false)
    {
        var seriesList = new List<SVR_AnimeSeries>();
        GetAnimeSeriesRecursive(AnimeGroupID, ref seriesList);
        if (skipSorting)
        {
            return seriesList;
        }

        return seriesList
            .OrderBy(a => a.Contract?.AniDBAnime?.AniDBAnime?.BeginYear ?? int.Parse(a.Year.Split('-')[0]))
            .ThenBy(a => a.AirDate)
            .ToList();
    }

    public static Dictionary<int, GroupVotes> BatchGetVotes(IReadOnlyCollection<SVR_AnimeGroup> animeGroups)
    {
        if (animeGroups == null)
        {
            throw new ArgumentNullException(nameof(animeGroups));
        }

        var votesByGroup = new Dictionary<int, GroupVotes>();

        if (animeGroups.Count == 0)
        {
            return votesByGroup;
        }

        var seriesByGroup = animeGroups.ToDictionary(g => g.AnimeGroupID, g => g.GetAllSeries());
        var allAnimeIds = seriesByGroup.Values.SelectMany(serLst => serLst.Select(series => series.AniDB_ID))
            .ToArray();
        var votesByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(allAnimeIds);

        foreach (var animeGroup in animeGroups)
        {
            var allVoteTotal = 0m;
            var permVoteTotal = 0m;
            var tempVoteTotal = 0m;
            var allVoteCount = 0;
            var permVoteCount = 0;
            var tempVoteCount = 0;
            var groupSeries = seriesByGroup[animeGroup.AnimeGroupID];

            foreach (var series in groupSeries)
            {
                if (votesByAnime.TryGetValue(series.AniDB_ID, out var vote))
                {
                    allVoteCount++;
                    allVoteTotal += vote.VoteValue;

                    switch (vote.VoteType)
                    {
                        case (int)AniDBVoteType.Anime:
                            permVoteCount++;
                            permVoteTotal += vote.VoteValue;
                            break;
                        case (int)AniDBVoteType.AnimeTemp:
                            tempVoteCount++;
                            tempVoteTotal += vote.VoteValue;
                            break;
                    }
                }
            }

            var groupVotes = new GroupVotes(
                allVoteCount == 0 ? (decimal?)null : allVoteTotal / allVoteCount / 100m,
                permVoteCount == 0 ? (decimal?)null : permVoteTotal / permVoteCount / 100m,
                tempVoteCount == 0 ? (decimal?)null : tempVoteTotal / tempVoteCount / 100m);

            votesByGroup[animeGroup.AnimeGroupID] = groupVotes;
        }

        return votesByGroup;
    }

    public List<AniDB_Tag> Tags
    {
        get
        {
            var animeTagIDs = new List<int>();
            var animeTags = new List<AniDB_Anime_Tag>();

            foreach (var ser in GetAllSeries())
            foreach (var aac in ser.GetAnime().GetAnimeTags())
            {
                if (!animeTagIDs.Contains(aac.AniDB_Anime_TagID))
                {
                    animeTagIDs.Add(aac.AniDB_Anime_TagID);
                    animeTags.Add(aac);
                }
            }

            return animeTags.OrderByDescending(a => a.Weight)
                .Select(animeTag => RepoFactory.AniDB_Tag.GetByTagID(animeTag.TagID)).Where(tag => tag != null)
                .ToList();
        }
    }

    public List<CustomTag> CustomTags
    {
        get
        {
            var tags = new List<CustomTag>();
            var tagIDs = new List<int>();


            // get a list of all the unique custom tags for all the series in this group
            foreach (var ser in GetAllSeries())
            foreach (var tag in RepoFactory.CustomTag.GetByAnimeID(ser.AniDB_ID))
            {
                if (!tagIDs.Contains(tag.CustomTagID))
                {
                    tagIDs.Add(tag.CustomTagID);
                    tags.Add(tag);
                }
            }

            return tags.OrderBy(a => a.TagName).ToList();
        }
    }

    public List<SVR_AniDB_Anime_Title> Titles
    {
        get
        {
            var animeTitleIDs = new List<int>();
            var animeTitles = new List<SVR_AniDB_Anime_Title>();

            // get a list of all the unique titles for this all the series in this group
            foreach (var ser in GetAllSeries())
            foreach (var aat in ser.GetAnime().GetTitles())
            {
                if (!animeTitleIDs.Contains(aat.AniDB_Anime_TitleID))
                {
                    animeTitleIDs.Add(aat.AniDB_Anime_TitleID);
                    animeTitles.Add(aat);
                }
            }

            return animeTitles;
        }
    }

    public override string ToString()
    {
        return $"Group: {GroupName} ({AnimeGroupID})";
    }

    /// <summary>
    /// Update stats for all child groups and series
    /// This should only be called from the very top level group.
    /// </summary>
    public void UpdateStatsFromTopLevel(bool watchedStats, bool missingEpsStats)
    {
        if (AnimeGroupParentID.HasValue)
        {
            return;
        }

        var start = DateTime.Now;
        logger.Info(
            $"Starting Updating STATS for GROUP {GroupName} from Top Level (recursively) - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

        // now recursively update stats for all the child groups
        // and update the stats for the groups
        foreach (var grp in GetAllChildGroups())
        {
            grp.UpdateStats(watchedStats, missingEpsStats);
        }

        UpdateStats(watchedStats, missingEpsStats);
        logger.Trace(
            $"Finished Updating STATS for GROUP {GroupName} from Top Level (recursively) in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Update the stats for this group based on the child series
    /// Assumes that all the AnimeSeries have had their stats updated already
    /// </summary>
    private void UpdateStats(bool watchedStats, bool missingEpsStats)
    {
        var start = DateTime.Now;
        logger.Info(
            $"Starting Updating STATS for GROUP {GroupName} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");
        var seriesList = GetAllSeries();

        if (missingEpsStats)
        {
            UpdateMissingEpisodeStats(this, seriesList);
        }

        if (watchedStats)
        {
            var allUsers = RepoFactory.JMMUser.GetAll();

            UpdateWatchedStats(this, seriesList, allUsers, (userRecord, isNew) =>
            {
                // Now update the stats for the groups
                logger.Trace("Updating stats for {0}", ToString());
                RepoFactory.AnimeGroup_User.Save(userRecord);
            });
        }

        RepoFactory.AnimeGroup.Save(this, true, false);
        logger.Trace($"Finished Updating STATS for GROUP {GroupName} in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Batch updates watched/missing episode stats for the specified sequence of <see cref="SVR_AnimeGroup"/>s.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the AnimeSeries have had their stats updated already.
    /// </remarks>
    /// <param name="animeGroups">The sequence of <see cref="SVR_AnimeGroup"/>s whose missing episode stats are to be updated.</param>
    /// <param name="watchedStats"><c>true</c> to update watched stats; otherwise, <c>false</c>.</param>
    /// <param name="missingEpsStats"><c>true</c> to update missing episode stats; otherwise, <c>false</c>.</param>
    /// <param name="createdGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="SVR_AnimeGroup_User"/> records
    /// that were created when updating watched stats.</param>
    /// <param name="updatedGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="SVR_AnimeGroup_User"/> records
    /// that were modified when updating watched stats.</param>
    /// <exception cref="ArgumentNullException"><paramref name="animeGroups"/> is <c>null</c>.</exception>
    public static void BatchUpdateStats(IEnumerable<SVR_AnimeGroup> animeGroups, bool watchedStats = true,
        bool missingEpsStats = true,
        ICollection<SVR_AnimeGroup_User> createdGroupUsers = null,
        ICollection<SVR_AnimeGroup_User> updatedGroupUsers = null)
    {
        if (animeGroups == null)
        {
            throw new ArgumentNullException(nameof(animeGroups));
        }

        if (!watchedStats && !missingEpsStats)
        {
            return; // Nothing to do
        }

        var allUsers =
            new Lazy<IReadOnlyList<SVR_JMMUser>>(() => RepoFactory.JMMUser.GetAll(), false);

        foreach (var animeGroup in animeGroups)
        {
            var animeSeries = animeGroup.GetAllSeries();

            if (missingEpsStats)
            {
                UpdateMissingEpisodeStats(animeGroup, animeSeries);
            }

            if (watchedStats)
            {
                UpdateWatchedStats(animeGroup, animeSeries, allUsers.Value, (userRecord, isNew) =>
                {
                    if (isNew)
                    {
                        createdGroupUsers?.Add(userRecord);
                    }
                    else
                    {
                        updatedGroupUsers?.Add(userRecord);
                    }
                });
            }
        }
    }

    /// <summary>
    /// Updates the watched stats for the specified anime group.
    /// </summary>
    /// <param name="animeGroup">The <see cref="SVR_AnimeGroup"/> that is to have it's watched stats updated.</param>
    /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> that belong to <paramref name="animeGroup"/>.</param>
    /// <param name="allUsers">A sequence of all JMM users.</param>
    /// <param name="newAnimeGroupUsers">A methed that will be called for each processed <see cref="SVR_AnimeGroup_User"/>
    /// and whether or not the <see cref="SVR_AnimeGroup_User"/> is new.</param>
    private static void UpdateWatchedStats(SVR_AnimeGroup animeGroup,
        IReadOnlyCollection<SVR_AnimeSeries> seriesList,
        IEnumerable<SVR_JMMUser> allUsers, Action<SVR_AnimeGroup_User, bool> newAnimeGroupUsers)
    {
        allUsers.ForEach(juser =>
        {
            var userRecord = animeGroup.GetUserRecord(juser.JMMUserID);
            var isNewRecord = false;

            if (userRecord == null)
            {
                userRecord = new SVR_AnimeGroup_User(juser.JMMUserID, animeGroup.AnimeGroupID);
                isNewRecord = true;
            }

            // Reset stats
            userRecord.WatchedCount = 0;
            userRecord.UnwatchedEpisodeCount = 0;
            userRecord.PlayedCount = 0;
            userRecord.StoppedCount = 0;
            userRecord.WatchedEpisodeCount = 0;
            userRecord.WatchedDate = null;

            foreach (var serUserRecord in seriesList.Select(ser => ser.GetUserRecord(juser.JMMUserID))
                         .Where(serUserRecord => serUserRecord != null))
            {
                userRecord.WatchedCount += serUserRecord.WatchedCount;
                userRecord.UnwatchedEpisodeCount += serUserRecord.UnwatchedEpisodeCount;
                userRecord.PlayedCount += serUserRecord.PlayedCount;
                userRecord.StoppedCount += serUserRecord.StoppedCount;
                userRecord.WatchedEpisodeCount += serUserRecord.WatchedEpisodeCount;

                if (serUserRecord.WatchedDate != null
                    && (userRecord.WatchedDate == null || serUserRecord.WatchedDate > userRecord.WatchedDate))
                {
                    userRecord.WatchedDate = serUserRecord.WatchedDate;
                }
            }

            newAnimeGroupUsers(userRecord, isNewRecord);
        });
    }

    /// <summary>
    /// Updates the missing episode stats for the specified anime group.
    /// </summary>
    /// <remarks>
    /// NOTE: This method does NOT save the changes made to the database.
    /// NOTE 2: Assumes that all the AnimeSeries have had their stats updated already.
    /// </remarks>
    /// <param name="animeGroup">The <see cref="SVR_AnimeGroup"/> that is to have it's missing episode stats updated.</param>
    /// <param name="seriesList">The list of <see cref="SVR_AnimeSeries"/> that belong to <paramref name="animeGroup"/>.</param>
    private static void UpdateMissingEpisodeStats(SVR_AnimeGroup animeGroup,
        IEnumerable<SVR_AnimeSeries> seriesList)
    {
        var missingEpisodeCount = 0;
        var missingEpisodeCountGroups = 0;
        DateTime? latestEpisodeAirDate = null;

        seriesList.AsParallel().ForAll(series =>
        {
            Interlocked.Add(ref missingEpisodeCount, series.MissingEpisodeCount);
            Interlocked.Add(ref missingEpisodeCountGroups, series.MissingEpisodeCountGroups);

            // Now series.LatestEpisodeAirDate should never be greater than today
            if (!series.LatestEpisodeAirDate.HasValue)
            {
                return;
            }

            if (latestEpisodeAirDate == null)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
            else if (series.LatestEpisodeAirDate.Value > latestEpisodeAirDate.Value)
            {
                latestEpisodeAirDate = series.LatestEpisodeAirDate;
            }
        });

        animeGroup.MissingEpisodeCount = missingEpisodeCount;
        animeGroup.MissingEpisodeCountGroups = missingEpisodeCountGroups;
        animeGroup.LatestEpisodeAirDate = latestEpisodeAirDate;
    }


    public static HashSet<GroupFilterConditionType> GetConditionTypesChanged(CL_AnimeGroup_User oldcontract,
        CL_AnimeGroup_User newcontract)
    {
        var h = new HashSet<GroupFilterConditionType>();
        if (oldcontract == null || oldcontract.Stat_IsComplete != newcontract.Stat_IsComplete)
        {
            h.Add(GroupFilterConditionType.CompletedSeries);
        }

        if (oldcontract == null ||
            (oldcontract.MissingEpisodeCount > 0 || oldcontract.MissingEpisodeCountGroups > 0) !=
            (newcontract.MissingEpisodeCount > 0 || newcontract.MissingEpisodeCountGroups > 0))
        {
            h.Add(GroupFilterConditionType.MissingEpisodes);
        }

        if (oldcontract == null || !oldcontract.Stat_AllTags.SetEquals(newcontract.Stat_AllTags))
        {
            h.Add(GroupFilterConditionType.Tag);
        }

        if (oldcontract == null || oldcontract.Stat_AirDate_Min != newcontract.Stat_AirDate_Min ||
            oldcontract.Stat_AirDate_Max != newcontract.Stat_AirDate_Max)
        {
            h.Add(GroupFilterConditionType.AirDate);
        }

        if (oldcontract == null || oldcontract.Stat_HasTvDBLink != newcontract.Stat_HasTvDBLink)
        {
            h.Add(GroupFilterConditionType.AssignedTvDBInfo);
        }

        if (oldcontract == null || oldcontract.Stat_HasTraktLink != newcontract.Stat_HasTraktLink)
        {
            h.Add(GroupFilterConditionType.AssignedTraktInfo);
        }

        if (oldcontract == null || oldcontract.Stat_HasMALLink != newcontract.Stat_HasMALLink)
        {
            h.Add(GroupFilterConditionType.AssignedMALInfo);
        }

        if (oldcontract == null || oldcontract.Stat_HasMovieDBLink != newcontract.Stat_HasMovieDBLink)
        {
            h.Add(GroupFilterConditionType.AssignedMovieDBInfo);
        }

        if (oldcontract == null || oldcontract.Stat_HasMovieDBOrTvDBLink != newcontract.Stat_HasMovieDBOrTvDBLink)
        {
            h.Add(GroupFilterConditionType.AssignedTvDBOrMovieDBInfo);
        }

        if (oldcontract == null || !oldcontract.Stat_AnimeTypes.SetEquals(newcontract.Stat_AnimeTypes))
        {
            h.Add(GroupFilterConditionType.AnimeType);
        }

        if (oldcontract == null || !oldcontract.Stat_AllVideoQuality.SetEquals(newcontract.Stat_AllVideoQuality) ||
            !oldcontract.Stat_AllVideoQuality_Episodes.SetEquals(newcontract.Stat_AllVideoQuality_Episodes))
        {
            h.Add(GroupFilterConditionType.VideoQuality);
        }

        if (oldcontract == null || oldcontract.AnimeGroupID != newcontract.AnimeGroupID)
        {
            h.Add(GroupFilterConditionType.AnimeGroup);
        }

        if (oldcontract == null || oldcontract.Stat_AniDBRating != newcontract.Stat_AniDBRating)
        {
            h.Add(GroupFilterConditionType.AniDBRating);
        }

        if (oldcontract == null || oldcontract.Stat_SeriesCreatedDate != newcontract.Stat_SeriesCreatedDate)
        {
            h.Add(GroupFilterConditionType.SeriesCreatedDate);
        }

        if (oldcontract == null || oldcontract.EpisodeAddedDate != newcontract.EpisodeAddedDate)
        {
            h.Add(GroupFilterConditionType.EpisodeAddedDate);
        }

        if (oldcontract == null || oldcontract.Stat_HasFinishedAiring != newcontract.Stat_HasFinishedAiring ||
            oldcontract.Stat_IsCurrentlyAiring != newcontract.Stat_IsCurrentlyAiring)
        {
            h.Add(GroupFilterConditionType.FinishedAiring);
        }

        if (oldcontract == null ||
            oldcontract.MissingEpisodeCountGroups > 0 != newcontract.MissingEpisodeCountGroups > 0)
        {
            h.Add(GroupFilterConditionType.MissingEpisodesCollecting);
        }

        if (oldcontract == null || !oldcontract.Stat_AudioLanguages.SetEquals(newcontract.Stat_AudioLanguages))
        {
            h.Add(GroupFilterConditionType.AudioLanguage);
        }

        if (oldcontract == null ||
            !oldcontract.Stat_SubtitleLanguages.SetEquals(newcontract.Stat_SubtitleLanguages))
        {
            h.Add(GroupFilterConditionType.SubtitleLanguage);
        }

        if (oldcontract == null || oldcontract.Stat_EpisodeCount != newcontract.Stat_EpisodeCount)
        {
            h.Add(GroupFilterConditionType.EpisodeCount);
        }

        if (oldcontract == null || !oldcontract.Stat_AllCustomTags.SetEquals(newcontract.Stat_AllCustomTags))
        {
            h.Add(GroupFilterConditionType.CustomTags);
        }

        if (oldcontract == null || oldcontract.LatestEpisodeAirDate != newcontract.LatestEpisodeAirDate)
        {
            h.Add(GroupFilterConditionType.LatestEpisodeAirDate);
        }

        var oldyear = -1;
        var newyear = -1;
        if (oldcontract?.Stat_AirDate_Min != null)
        {
            oldyear = oldcontract.Stat_AirDate_Min.Value.Year;
        }

        if (newcontract?.Stat_AirDate_Min != null)
        {
            newyear = newcontract.Stat_AirDate_Min.Value.Year;
        }

        if (oldyear != newyear)
        {
            h.Add(GroupFilterConditionType.Year);
        }

        if (oldcontract?.Stat_AllYears == null || !oldcontract.Stat_AllYears.SetEquals(newcontract.Stat_AllYears))
        {
            h.Add(GroupFilterConditionType.Year);
        }

        if (oldcontract?.Stat_AllSeasons == null || !oldcontract.Stat_AllSeasons.SetEquals(newcontract.Stat_AllSeasons))
        {
            h.Add(GroupFilterConditionType.Season);
        }

        //TODO This two should be moved to AnimeGroup_User in the future...
        if (oldcontract == null || oldcontract.Stat_UserVotePermanent != newcontract.Stat_UserVotePermanent)
        {
            h.Add(GroupFilterConditionType.UserVoted);
        }

        if (oldcontract == null || oldcontract.Stat_UserVoteOverall != newcontract.Stat_UserVoteOverall)
        {
            h.Add(GroupFilterConditionType.UserRating);
            h.Add(GroupFilterConditionType.UserVotedAny);
        }

        return h;
    }

    public static Dictionary<int, HashSet<GroupFilterConditionType>> BatchUpdateContracts(ISessionWrapper session,
        IReadOnlyCollection<SVR_AnimeGroup> animeGroups, bool updateStats)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeGroups == null)
        {
            throw new ArgumentNullException(nameof(animeGroups));
        }

        var grpFilterCondTypesByGroup = new Dictionary<int, HashSet<GroupFilterConditionType>>();

        if (animeGroups.Count == 0)
        {
            return grpFilterCondTypesByGroup;
        }

        var seriesByGroup = animeGroups.ToDictionary(g => g.AnimeGroupID, g => g.GetAllSeries());
        var allAnimeIds = new Lazy<int[]>(
            () => seriesByGroup.Values.SelectMany(serLst => serLst.Select(series => series.AniDB_ID)).ToArray(),
            false);
        var allGroupIds = new Lazy<int[]>(
            () => animeGroups.Select(grp => grp.AnimeGroupID).ToArray(), false);
        var audioLangStatsByAnime = new Lazy<Dictionary<int, HashSet<string>>>(
            () => RepoFactory.CrossRef_Languages_AniDB_File.GetLanguagesByAnime(allAnimeIds.Value), false);
        var subLangStatsByAnime = new Lazy<Dictionary<int, HashSet<string>>>(
            () => RepoFactory.CrossRef_Subtitles_AniDB_File.GetLanguagesByAnime(allAnimeIds.Value), false);
        var tvDbXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_TvDB>>(
            () => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeIDs(allAnimeIds.Value), false);
        var traktXrefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_TraktV2>>(
            () => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeIDs(allAnimeIds.Value), false);
        var allVidQualByGroup = new Lazy<ILookup<int, string>>(() => GetVideoQualities(allGroupIds.Value), false);
        var movieDbXRefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_Other>>(
            () => RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDsAndType(session, allAnimeIds.Value,
                CrossRefType.MovieDB), false);
        var malXRefByAnime = new Lazy<ILookup<int, CrossRef_AniDB_MAL>>(
            () => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeIDs(session, allAnimeIds.Value), false);
        var votesByGroup = BatchGetVotes(animeGroups);
        var now = DateTime.Now;

        foreach (var animeGroup in animeGroups)
        {
            var contract = animeGroup.Contract?.DeepCopy();
            var localUpdateStats = updateStats;

            if (contract == null)
            {
                contract = new CL_AnimeGroup_User();
                localUpdateStats = true;
            }

            contract.AnimeGroupID = animeGroup.AnimeGroupID;
            contract.AnimeGroupParentID = animeGroup.AnimeGroupParentID;
            contract.DefaultAnimeSeriesID = animeGroup.DefaultAnimeSeriesID;
            contract.GroupName = animeGroup.GroupName;
            contract.Description = animeGroup.Description;
            contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;
            contract.SortName = animeGroup.SortName;
            contract.EpisodeAddedDate = animeGroup.EpisodeAddedDate;
            contract.OverrideDescription = animeGroup.OverrideDescription;
            contract.DateTimeUpdated = animeGroup.DateTimeUpdated;
            contract.IsFave = 0;
            contract.UnwatchedEpisodeCount = 0;
            contract.WatchedEpisodeCount = 0;
            contract.WatchedDate = null;
            contract.PlayedCount = 0;
            contract.WatchedCount = 0;
            contract.StoppedCount = 0;
            contract.MissingEpisodeCount = animeGroup.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = animeGroup.MissingEpisodeCountGroups;

            var allSeriesForGroup = seriesByGroup[animeGroup.AnimeGroupID];

            if (localUpdateStats)
            {
                DateTime? airDateMin = null;
                DateTime? airDateMax = null;
                DateTime? groupEndDate = new DateTime(1980, 1, 1);
                DateTime? seriesCreatedDate = null;
                var isComplete = false;
                var hasFinishedAiring = false;
                var isCurrentlyAiring = false;
                var videoQualityEpisodes =
                    new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                var audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                var subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                // Even though the contract value says 'has link', it's easier to think about whether it's missing
                var missingTvDBLink = false;
                var missingTraktLink = false;
                var missingMALLink = false;
                var missingMovieDBLink = false;
                var missingTvDBAndMovieDBLink = false;
                var seriesCount = 0;
                var epCount = 0;

                var allYears = new HashSet<int>();
                var allSeasons = new SortedSet<string>(new SeasonComparator());

                foreach (var series in allSeriesForGroup)
                {
                    seriesCount++;

                    var vidsTemp = RepoFactory.VideoLocal.GetByAniDBAnimeID(series.AniDB_ID);
                    var crossRefs =
                        RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID);
                    var crossRefsLookup = crossRefs.ToLookup(cr => cr.EpisodeID);
                    var dictVids = new Dictionary<string, SVR_VideoLocal>();

                    foreach (var vid in vidsTemp)
                        //Hashes may be repeated from multiple locations but we don't care
                    {
                        dictVids[vid.Hash] = vid;
                    }

                    // All Video Quality Episodes
                    // Try to determine if this anime has all the episodes available at a certain video quality
                    // e.g.  the series has all episodes in blu-ray
                    // Also look at languages
                    var vidQualEpCounts = new Dictionary<string, int>();
                    // video quality, count of episodes
                    var anime = series.GetAnime();

                    foreach (var ep in series.GetAnimeEpisodes())
                    {
                        if (ep.AniDB_Episode == null || ep.EpisodeTypeEnum != EpisodeType.Episode)
                        {
                            continue;
                        }

                        var epVids = new List<SVR_VideoLocal>();

                        foreach (var xref in crossRefsLookup[ep.AniDB_EpisodeID])
                        {
                            if (xref.EpisodeID != ep.AniDB_EpisodeID)
                            {
                                continue;
                            }


                            if (dictVids.TryGetValue(xref.Hash, out var video))
                            {
                                epVids.Add(video);
                            }
                        }

                        var qualityAddedSoFar = new HashSet<string>();

                        // Handle mutliple files of the same quality for one episode
                        foreach (var vid in epVids)
                        {
                            var anifile = vid.GetAniDBFile();

                            if (anifile == null)
                            {
                                continue;
                            }

                            if (!qualityAddedSoFar.Contains(anifile.File_Source))
                            {
                                vidQualEpCounts.TryGetValue(anifile.File_Source, out var srcCount);
                                vidQualEpCounts[anifile.File_Source] =
                                    srcCount +
                                    1; // If the file source wasn't originally in the dictionary, then it will be set to 1

                                qualityAddedSoFar.Add(anifile.File_Source);
                            }
                        }
                    }

                    epCount += anime.EpisodeCountNormal;

                    // Add all video qualities that span all of the normal episodes
                    videoQualityEpisodes.UnionWith(
                        vidQualEpCounts
                            .Where(vqec => anime.EpisodeCountNormal == vqec.Value)
                            .Select(vqec => vqec.Key));


                    // Audio languages
                    if (audioLangStatsByAnime.Value.TryGetValue(anime.AnimeID, out var langStats))
                    {
                        audioLanguages.UnionWith(langStats);
                    }

                    // Subtitle languages
                    if (subLangStatsByAnime.Value.TryGetValue(anime.AnimeID, out langStats))
                    {
                        subtitleLanguages.UnionWith(langStats);
                    }

                    // Calculate Air Date
                    var seriesAirDate = series.AirDate;

                    if (seriesAirDate != DateTime.MinValue)
                    {
                        if (airDateMin == null || seriesAirDate < airDateMin.Value)
                        {
                            airDateMin = seriesAirDate;
                        }

                        if (airDateMax == null || seriesAirDate > airDateMax.Value)
                        {
                            airDateMax = seriesAirDate;
                        }
                    }

                    // Calculate end date
                    // If the end date is NULL it actually means it is ongoing, so this is the max possible value
                    var seriesEndDate = series.EndDate;

                    if (seriesEndDate == null || groupEndDate == null)
                    {
                        groupEndDate = null;
                    }
                    else if (seriesEndDate.Value > groupEndDate.Value)
                    {
                        groupEndDate = seriesEndDate;
                    }

                    // Note - only one series has to be finished airing to qualify
                    if (series.EndDate != null && series.EndDate.Value < now)
                    {
                        hasFinishedAiring = true;
                    }

                    // Note - only one series has to be finished airing to qualify
                    if (series.EndDate == null || series.EndDate.Value > now)
                    {
                        isCurrentlyAiring = true;
                    }

                    // We evaluate IsComplete as true if
                    // 1. series has finished airing
                    // 2. user has all episodes locally
                    // Note - only one series has to be complete for the group to be considered complete
                    if (series.EndDate != null && series.EndDate.Value < now
                                               && series.MissingEpisodeCount == 0 &&
                                               series.MissingEpisodeCountGroups == 0)
                    {
                        isComplete = true;
                    }

                    // Calculate Series Created Date
                    var createdDate = series.DateTimeCreated;

                    if (seriesCreatedDate == null || createdDate < seriesCreatedDate.Value)
                    {
                        seriesCreatedDate = createdDate;
                    }

                    // For the group, if any of the series don't have a tvdb link
                    // we will consider the group as not having a tvdb link
                    var foundTvDBLink = tvDbXrefByAnime.Value[anime.AnimeID].Any();
                    var foundTraktLink = traktXrefByAnime.Value[anime.AnimeID].Any();
                    var foundMovieDBLink = movieDbXRefByAnime.Value[anime.AnimeID].Any();
                    var isMovie = anime.AnimeType == (int)AnimeType.Movie;
                    if (!foundTvDBLink)
                    {
                        if (!isMovie && !(anime.Restricted > 0))
                        {
                            missingTvDBLink = true;
                        }
                    }

                    if (!foundTraktLink)
                    {
                        missingTraktLink = true;
                    }

                    if (!foundMovieDBLink)
                    {
                        if (isMovie && !(anime.Restricted > 0))
                        {
                            missingMovieDBLink = true;
                        }
                    }

                    if (!malXRefByAnime.Value[anime.AnimeID].Any())
                    {
                        missingMALLink = true;
                    }

                    missingTvDBAndMovieDBLink |= !(anime.Restricted > 0) && !foundTvDBLink && !foundMovieDBLink;

                    var endyear = anime.EndYear;
                    if (endyear == 0)
                    {
                        endyear = DateTime.Today.Year;
                    }

                    var startyear = anime.BeginYear;
                    if (endyear < startyear)
                    {
                        endyear = startyear;
                    }

                    if (startyear != 0)
                    {
                        List<int> years;
                        if (startyear == endyear)
                        {
                            years = new List<int> { startyear };
                        }
                        else
                        {
                            years = Enumerable.Range(anime.BeginYear, endyear - anime.BeginYear + 1)
                                .Where(anime.IsInYear).ToList();
                        }

                        allYears.UnionWith(years);
                        allSeasons.UnionWith(anime.GetSeasons().Select(tuple => $"{tuple.Season} {tuple.Year}"));
                    }
                }

                contract.Stat_AllYears = allYears;
                contract.Stat_AllSeasons = allSeasons;
                contract.Stat_AllTags = animeGroup.Tags
                    .Select(a => a.TagName.Trim())
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllCustomTags = animeGroup.CustomTags
                    .Select(a => a.TagName)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllTitles = animeGroup.Titles
                    .Select(a => a.Title)
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AnimeTypes = allSeriesForGroup
                    .Select(a => a.Contract?.AniDBAnime?.AniDBAnime)
                    .Where(a => a != null)
                    .Select(a => a.AnimeType.ToString())
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_AllVideoQuality = allVidQualByGroup.Value[animeGroup.AnimeGroupID]
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
                contract.Stat_IsComplete = isComplete;
                contract.Stat_HasFinishedAiring = hasFinishedAiring;
                contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
                contract.Stat_HasTvDBLink = !missingTvDBLink; // Has a link if it isn't missing
                contract.Stat_HasTraktLink = !missingTraktLink; // Has a link if it isn't missing
                contract.Stat_HasMALLink = !missingMALLink; // Has a link if it isn't missing
                contract.Stat_HasMovieDBLink = !missingMovieDBLink; // Has a link if it isn't missing
                contract.Stat_HasMovieDBOrTvDBLink = !missingTvDBAndMovieDBLink; // Has a link if it isn't missing
                contract.Stat_SeriesCount = seriesCount;
                contract.Stat_EpisodeCount = epCount;
                contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
                contract.Stat_AirDate_Min = airDateMin;
                contract.Stat_AirDate_Max = airDateMax;
                contract.Stat_EndDate = groupEndDate;
                contract.Stat_SeriesCreatedDate = seriesCreatedDate;
                contract.Stat_AniDBRating = animeGroup.AniDBRating;
                contract.Stat_AudioLanguages = audioLanguages;
                contract.Stat_SubtitleLanguages = subtitleLanguages;
                contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;


                votesByGroup.TryGetValue(animeGroup.AnimeGroupID, out var votes);
                contract.Stat_UserVoteOverall = votes?.AllVotes;
                contract.Stat_UserVotePermanent = votes?.PermanentVotes;
                contract.Stat_UserVoteTemporary = votes?.TemporaryVotes;
            }

            grpFilterCondTypesByGroup[animeGroup.AnimeGroupID] =
                GetConditionTypesChanged(animeGroup.Contract, contract);
            animeGroup.Contract = contract;
        }

        return grpFilterCondTypesByGroup;
    }

    public HashSet<GroupFilterConditionType> UpdateContract(ISessionWrapper session, bool updatestats)
    {
        var grpFilterCondTypesByGroup = BatchUpdateContracts(session, new[] { this }, updatestats);

        return grpFilterCondTypesByGroup[AnimeGroupID];
    }

    private static ILookup<int, string> GetVideoQualities(IEnumerable<int> groupIds = null)
    {
        groupIds ??= RepoFactory.AnimeGroup.GetAll().Select(a => a.AnimeGroupID).ToList();
        return groupIds.SelectMany(id =>
            RepoFactory.AnimeSeries.GetByGroupID(id)
                .SelectMany(a =>
                    RepoFactory.CrossRef_File_Episode.GetByAnimeID(a.AniDB_ID)
                        .Select(b => RepoFactory.AniDB_File.GetByHash(b.Hash)?.File_Source).Where(b => b != null))
                .Distinct().Select(a => (GroupID: id, Source: a))).ToLookup(a => a.GroupID, a => a.Source);
    }

    public void DeleteFromFilters()
    {
        foreach (var gf in RepoFactory.GroupFilter.GetAll())
        {
            var change = false;
            foreach (var k in gf.GroupsIds.Keys)
            {
                if (gf.GroupsIds[k].Contains(AnimeGroupID))
                {
                    gf.GroupsIds[k].Remove(AnimeGroupID);
                    change = true;
                }
            }

            if (change)
            {
                RepoFactory.GroupFilter.Save(gf);
            }
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
        logger.Trace($"Updating {gfs.Count} Group Filters from Group {GroupName}");
        foreach (var gf in gfs)
        {
            if (gf.UpdateGroupFilterFromGroup(Contract, null))
            {
                if (!tosave.Contains(gf))
                {
                    tosave.Add(gf);
                }
            }

            foreach (var u in users)
            {
                var cgrp = GetUserContract(u.JMMUserID, n);

                if (gf.UpdateGroupFilterFromGroup(cgrp, u))
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


    public static void GetAnimeGroupsRecursive(int animeGroupID, ref List<SVR_AnimeGroup> groupList)
    {
        var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
        if (grp == null)
        {
            return;
        }

        // get the child groups for this group
        groupList.AddRange(grp.GetChildGroups());

        foreach (var childGroup in grp.GetChildGroups())
        {
            GetAnimeGroupsRecursive(childGroup.AnimeGroupID, ref groupList);
        }
    }

    public static void GetAnimeSeriesRecursive(int animeGroupID, ref List<SVR_AnimeSeries> seriesList)
    {
        var grp = RepoFactory.AnimeGroup.GetByID(animeGroupID);
        if (grp == null)
        {
            return;
        }

        // get the series for this group
        var thisSeries = grp.GetSeries();
        seriesList.AddRange(thisSeries);

        foreach (var childGroup in grp.GetChildGroups())
        {
            GetAnimeSeriesRecursive(childGroup.AnimeGroupID, ref seriesList);
        }
    }

    public void DeleteGroup(bool updateParent = true)
    {
        // delete all sub groups
        foreach (var subGroup in GetAllChildGroups())
        {
            subGroup.DeleteGroup(false);
        }

        var gfs =
            RepoFactory.GroupFilter.GetWithConditionsTypes(new HashSet<GroupFilterConditionType>
            {
                GroupFilterConditionType.AnimeGroup
            });
        foreach (var gf in gfs)
        {
            var c = gf.Conditions.RemoveAll(a =>
            {
                if (a.ConditionType != (int)GroupFilterConditionType.AnimeGroup)
                {
                    return false;
                }

                if (!int.TryParse(a.ConditionParameter, out var thisGrpID))
                {
                    return false;
                }

                if (thisGrpID != AnimeGroupID)
                {
                    return false;
                }

                return true;
            });
            if (gf.Conditions.Count <= 0)
            {
                RepoFactory.GroupFilter.Delete(gf.GroupFilterID);
            }
            else
            {
                gf.CalculateGroupsAndSeries();
                RepoFactory.GroupFilter.Save(gf);
            }
        }

        RepoFactory.AnimeGroup.Delete(this);

        // finally update stats
        if (updateParent)
        {
            Parent?.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true);
        }
    }

    public SVR_AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parent = Parent;
            if (parent == null)
            {
                return this;
            }

            while (true)
            {
                var next = parent.Parent;
                if (next == null)
                {
                    return parent;
                }

                parent = next;
            }
        }
    }

    public bool IsDescendantOf(int groupID)
        => IsDescendantOf(new int[] { groupID });

    public bool IsDescendantOf(IEnumerable<int> groupIDs)
    {
        var idSet = groupIDs.ToHashSet();
        if (idSet.Count == 0)
            return false;

        var parent = Parent;
        while (parent != null)
        {
            if (idSet.Contains(parent.AnimeGroupID))
                return true;

            parent = parent.Parent;
        }

        return false;
    }

    string IGroup.Name => GroupName;
    public IAnime MainSeries => GetMainSeries()?.GetAnime();

    IReadOnlyList<IAnime> IGroup.Series => GetAllSeries().Select(a => a.GetAnime()).Where(a => a != null)
        .OrderBy(a => a.BeginYear).ThenBy(a => a.AirDate ?? DateTime.MaxValue).ThenBy(a => a.MainTitle)
        .Cast<IAnime>().ToList();
}

public class GroupVotes
{
    public GroupVotes(decimal? allVotes = null, decimal? permanentVotes = null, decimal? temporaryVotes = null)
    {
        AllVotes = allVotes;
        PermanentVotes = permanentVotes;
        TemporaryVotes = temporaryVotes;
    }

    public decimal? AllVotes { get; }

    public decimal? PermanentVotes { get; }

    public decimal? TemporaryVotes { get; }
}
