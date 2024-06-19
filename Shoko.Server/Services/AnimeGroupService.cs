using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services;

public class AnimeGroupService
{
    private readonly ILogger<AnimeGroupService> _logger;
    private readonly AnimeGroup_UserRepository _groupUsers;
    private readonly CrossRef_Languages_AniDB_FileRepository _languages;
    private readonly CrossRef_Subtitles_AniDB_FileRepository _subtitles;
    private readonly CrossRef_File_EpisodeRepository _fileEpisodes;
    private readonly AniDB_FileRepository _files;
    private readonly AnimeGroupRepository _groups;
    private readonly AnimeSeries_UserRepository _seriesUsers;

    public AnimeGroupService(ILogger<AnimeGroupService> logger, AnimeGroup_UserRepository groupUsers, CrossRef_Languages_AniDB_FileRepository languages, CrossRef_Subtitles_AniDB_FileRepository subtitles, CrossRef_File_EpisodeRepository fileEpisodes, AniDB_FileRepository files, AnimeGroupRepository groups, AnimeSeries_UserRepository seriesUsers)
    {
        _groupUsers = groupUsers;
        _logger = logger;
        _languages = languages;
        _subtitles = subtitles;
        _fileEpisodes = fileEpisodes;
        _files = files;
        _groups = groups;
        _seriesUsers = seriesUsers;
    }

    public void DeleteGroup(SVR_AnimeGroup group, bool updateParent = true)
    {
        // delete all sub groups
        foreach (var subGroup in group.AllChildren)
        {
            DeleteGroup(subGroup, false);
        }

        _groups.Delete(group);

        // finally update stats
        if (updateParent)
        {
            UpdateStatsFromTopLevel(group.Parent?.TopLevelAnimeGroup, true, true);
        }
    }

    public CL_AnimeGroup_User GetV1Contract(SVR_AnimeGroup group, int userid)
    {
        if (group == null) return null;
        var contract = GetContract(group);
        var rr = _groupUsers.GetByUserAndGroupID(userid, group.AnimeGroupID);
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

        return contract;
    }

    /// <summary>
    /// Rename all groups without a manually set name according to the current
    /// language preference.
    /// </summary>
    public void RenameAllGroups()
    {
        _logger.LogInformation("Starting RenameAllGroups");
        foreach (var grp in _groups.GetAll())
        {
            // Skip renaming any groups that are fully manually managed.
            if (grp.IsManuallyNamed == 1 && grp.OverrideDescription == 1)
                continue;

            var series = grp.MainSeries;
            if (series != null)
            {
                // Reset the name/description as needed.
                if (grp.IsManuallyNamed == 0)
                    grp.GroupName = series.SeriesName;
                if (grp.OverrideDescription == 0)
                    grp.Description = series.AniDB_Anime.Description;

                // Save the changes for this group only.
                grp.DateTimeUpdated = DateTime.Now;
                _groups.Save(grp, false);
            }
        }
        _logger.LogInformation("Finished RenameAllGroups");
    }

    /// <summary>
    /// Update stats for all child groups and series
    /// This should only be called from the very top level group.
    /// </summary>
    public void UpdateStatsFromTopLevel(SVR_AnimeGroup group, bool watchedStats, bool missingEpsStats)
    {
        if (group.AnimeGroupParentID.HasValue)
        {
            return;
        }

        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} from Top Level (recursively) - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");

        // now recursively update stats for all the child groups
        // and update the stats for the groups
        foreach (var grp in group.AllChildren)
        {
            UpdateStats(grp, watchedStats, missingEpsStats);
        }

        UpdateStats(group, watchedStats, missingEpsStats);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} from Top Level (recursively) in {(DateTime.Now - start).TotalMilliseconds}ms");
    }

    /// <summary>
    /// Update the stats for this group based on the child series
    /// Assumes that all the AnimeSeries have had their stats updated already
    /// </summary>
    private void UpdateStats(SVR_AnimeGroup group, bool watchedStats, bool missingEpsStats)
    {
        var start = DateTime.Now;
        _logger.LogInformation(
            $"Starting Updating STATS for GROUP {group.GroupName} - Watched Stats: {watchedStats}, Missing Episodes: {missingEpsStats}");
        var seriesList = group.AllSeries;

        // Reset the name/description for the group if needed.
        var mainSeries = group.IsManuallyNamed == 0 || group.OverrideDescription == 0 ? group.MainSeries : null;
        if (mainSeries is not null)
        {
            if (group.IsManuallyNamed == 0)
                group.GroupName = mainSeries.SeriesName;
            if (group.OverrideDescription == 0)
                group.Description = mainSeries.AniDB_Anime.Description;
        }

        if (missingEpsStats)
        {
            UpdateMissingEpisodeStats(group, seriesList);
        }

        if (watchedStats)
        {
            var allUsers = RepoFactory.JMMUser.GetAll();

            UpdateWatchedStats(group, seriesList, allUsers, (userRecord, _) =>
            {
                // Now update the stats for the groups
                _logger.LogTrace("Updating stats for {0}", ToString());
                RepoFactory.AnimeGroup_User.Save(userRecord);
            });
        }

        _groups.Save(group, false);
        _logger.LogTrace($"Finished Updating STATS for GROUP {group.GroupName} in {(DateTime.Now - start).TotalMilliseconds}ms");
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
    /// <param name="createdGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="AnimeGroup_User"/> records
    /// that were created when updating watched stats.</param>
    /// <param name="updatedGroupUsers">The <see cref="ICollection{T}"/> to add any <see cref="AnimeGroup_User"/> records
    /// that were modified when updating watched stats.</param>
    /// <exception cref="ArgumentNullException"><paramref name="animeGroups"/> is <c>null</c>.</exception>
    public void BatchUpdateStats(IEnumerable<SVR_AnimeGroup> animeGroups, bool watchedStats = true,
        bool missingEpsStats = true,
        ICollection<AnimeGroup_User> createdGroupUsers = null,
        ICollection<AnimeGroup_User> updatedGroupUsers = null)
    {
        if (animeGroups == null)
        {
            throw new ArgumentNullException(nameof(animeGroups));
        }

        if (!watchedStats && !missingEpsStats)
        {
            return; // Nothing to do
        }

        var allUsers = RepoFactory.JMMUser.GetAll();

        foreach (var animeGroup in animeGroups)
        {
            var animeSeries = animeGroup.AllSeries;

            if (missingEpsStats)
            {
                UpdateMissingEpisodeStats(animeGroup, animeSeries);
            }

            if (watchedStats)
            {
                UpdateWatchedStats(animeGroup, animeSeries, allUsers, (userRecord, isNew) =>
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
    /// <param name="newAnimeGroupUsers">A methed that will be called for each processed <see cref="AnimeGroup_User"/>
    /// and whether or not the <see cref="AnimeGroup_User"/> is new.</param>
    private void UpdateWatchedStats(SVR_AnimeGroup animeGroup,
        IReadOnlyCollection<SVR_AnimeSeries> seriesList,
        IEnumerable<SVR_JMMUser> allUsers, Action<AnimeGroup_User, bool> newAnimeGroupUsers)
    {
        foreach (var juser in allUsers)
        {
            var userRecord = _groupUsers.GetByUserAndGroupID(juser.JMMUserID, animeGroup.AnimeGroupID);
            var isNewRecord = false;

            if (userRecord == null)
            {
                userRecord = new AnimeGroup_User
                {
                    JMMUserID = juser.JMMUserID, AnimeGroupID = animeGroup.AnimeGroupID
                };
                isNewRecord = true;
            }

            // Reset stats
            userRecord.WatchedCount = 0;
            userRecord.UnwatchedEpisodeCount = 0;
            userRecord.PlayedCount = 0;
            userRecord.StoppedCount = 0;
            userRecord.WatchedEpisodeCount = 0;
            userRecord.WatchedDate = null;

            foreach (var serUserRecord in seriesList.Select(ser => _seriesUsers.GetByUserAndSeriesID(juser.JMMUserID, ser.AnimeSeriesID))
                         .WhereNotNull())
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
        }
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

    public CL_AnimeGroup_User GetContract(SVR_AnimeGroup animeGroup)
    {
        if (animeGroup == null) return null;

        var votes = GetVotes(animeGroup);
        var now = DateTime.Now;

        var contract = new CL_AnimeGroup_User();
        contract.AnimeGroupID = animeGroup.AnimeGroupID;
        contract.AnimeGroupParentID = animeGroup.AnimeGroupParentID;
        contract.DefaultAnimeSeriesID = animeGroup.DefaultAnimeSeriesID;
        contract.GroupName = animeGroup.GroupName;
        contract.Description = animeGroup.Description;
        contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;
        contract.SortName = animeGroup.GroupName.ToSortName();
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

        var allSeriesForGroup = animeGroup.AllSeries;
        var allIDs = allSeriesForGroup.Select(a => a.AniDB_ID).ToArray();

        DateTime? airDateMin = null;
        DateTime? airDateMax = null;
        DateTime? groupEndDate = new DateTime(1980, 1, 1);
        DateTime? seriesCreatedDate = null;
        var isComplete = false;
        var hasFinishedAiring = false;
        var isCurrentlyAiring = false;
        var videoQualityEpisodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var tvDbXrefByAnime = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeIDs(allIDs);
        var traktXrefByAnime = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeIDs(allIDs);
        var allVidQualByGroup = allSeriesForGroup.SelectMany(a => _fileEpisodes.GetByAnimeID(a.AniDB_ID)).Select(a => _files.GetByHash(a.Hash)?.File_Source)
            .WhereNotNull().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        var movieDbXRefByAnime = allIDs.Select(a => RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(a, CrossRefType.MovieDB)).WhereNotNull()
            .ToDictionary(a => a.AnimeID);
        var malXRefByAnime = allIDs.SelectMany(a => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(a)).ToLookup(a => a.AnimeID);
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
            var crossRefs = RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID);
            var crossRefsLookup = crossRefs.ToLookup(cr => cr.EpisodeID);
            var dictVids = new Dictionary<string, SVR_VideoLocal>();

            foreach (var vid in vidsTemp)
                // Hashes may be repeated from multiple locations, but we don't care
            {
                dictVids[vid.Hash] = vid;
            }

            // All Video Quality Episodes
            // Try to determine if this anime has all the episodes available at a certain video quality
            // e.g.  the series has all episodes in blu-ray
            // Also look at languages
            var vidQualEpCounts = new Dictionary<string, int>();
            // video quality, count of episodes
            var anime = series.AniDB_Anime;

            foreach (var ep in series.AllAnimeEpisodes)
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
                    var anifile = vid.AniDBFile;

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

            // Calculate Air Date
            var seriesAirDate = series.AirDate;

            if (seriesAirDate.HasValue)
            {
                if (airDateMin == null || seriesAirDate.Value < airDateMin.Value)
                {
                    airDateMin = seriesAirDate.Value;
                }

                if (airDateMax == null || seriesAirDate.Value > airDateMax.Value)
                {
                    airDateMax = seriesAirDate.Value;
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
            var foundTvDBLink = tvDbXrefByAnime[anime.AnimeID].Any();
            var foundTraktLink = traktXrefByAnime[anime.AnimeID].Any();
            var foundMovieDBLink = movieDbXRefByAnime.TryGetValue(anime.AnimeID, out var movieDbLink) && movieDbLink != null;
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

            if (!malXRefByAnime[anime.AnimeID].Any())
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
                    years = new List<int>
                    {
                        startyear
                    };
                }
                else
                {
                    years = Enumerable.Range(anime.BeginYear, endyear - anime.BeginYear + 1)
                        .Where(anime.IsInYear).ToList();
                }

                allYears.UnionWith(years);
                allSeasons.UnionWith(anime.Seasons.Select(tuple => $"{tuple.Season} {tuple.Year}"));
            }
        }

        contract.Stat_AllYears = allYears;
        contract.Stat_AllSeasons = allSeasons;
        contract.Stat_AllTags = animeGroup.Tags.Select(a => a.TagName.Trim()).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllCustomTags = animeGroup.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllTitles = animeGroup.Titles.Select(a => a.Title).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AnimeTypes = allSeriesForGroup.Select(a => a.AniDB_Anime.GetAnimeTypeName()).WhereNotNull().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllVideoQuality = allVidQualByGroup;
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
        contract.Stat_AudioLanguages = _languages.GetLanguagesForGroup(animeGroup);
        contract.Stat_SubtitleLanguages = _subtitles.GetLanguagesForGroup(animeGroup);
        contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;
        contract.Stat_UserVoteOverall = votes?.AllVotes;
        contract.Stat_UserVotePermanent = votes?.PermanentVotes;
        contract.Stat_UserVoteTemporary = votes?.TemporaryVotes;

        return contract;
    }

    private GroupVotes GetVotes(SVR_AnimeGroup animeGroup)
    {
        var groupSeries = animeGroup.AllSeries;
        var votesByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(groupSeries.Select(a => a.AniDB_ID).ToList());

        var allVoteTotal = 0m;
        var permVoteTotal = 0m;
        var tempVoteTotal = 0m;
        var allVoteCount = 0;
        var permVoteCount = 0;
        var tempVoteCount = 0;

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
            allVoteCount == 0 ? null : allVoteTotal / allVoteCount / 100m,
            permVoteCount == 0 ? null : permVoteTotal / permVoteCount / 100m,
            tempVoteCount == 0 ? null : tempVoteTotal / tempVoteCount / 100m);

        return groupVotes;
    }
}
