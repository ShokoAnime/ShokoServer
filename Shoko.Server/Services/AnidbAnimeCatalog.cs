using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.User;
using Shoko.Server.Extensions;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Services.Airing;
using Shoko.Server.Utilities;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;

namespace Shoko.Server.Services;

/// <summary>
///   Lists the cached AniDB anime, in the collection or not, for season
///   views and other browsing, and looks up the extra details a season
///   view's cards show.
/// </summary>
public class AnidbAnimeCatalog(
    AniDB_AnimeRepository animeRepository,
    AniDB_EpisodeRepository episodeRepository,
    AnimeSeriesRepository seriesRepository,
    AniDB_Anime_StaffRepository staffRepository,
    AniDB_CreatorRepository creatorRepository,
    AniDB_Anime_TagRepository animeTagRepository,
    AniDB_TagRepository tagRepository,
    CrossRef_File_EpisodeRepository fileCrossReferenceRepository,
    VideoLocalRepository videoRepository,
    IMetadataTextManager textManager,
    IMetadataFilteringService filteringService,
    AiringScheduleService airingScheduleService
)
{
    // The least prior weight, in votes, the season ranking gives the mean.
    private const int MinimumPriorVotes = 50;

    #region Listing

    /// <summary>
    ///   Lists the cached anime matching the options, in their order, or in
    ///   the order of their filter when it has a sorting expression.
    /// </summary>
    /// <param name="options">The filters and order.</param>
    /// <exception cref="ArgumentNullException">
    ///   The filter depends on the user and the options name none.
    /// </exception>
    /// <returns>The anime, each with its Shoko series when there is one.</returns>
    public IReadOnlyList<(AniDB_Anime Anime, AnimeSeries? Series)> GetAnime(AnidbAnimeListOptions? options = null)
    {
        options ??= new();
        Func<AniDB_Anime, bool>? inSeasons = null;
        var channelAirings = options.ChannelIDs is { Count: > 0 } channelIDs ? GetChannelAirings(channelIDs) : null;
        if (options.Seasons is { Count: > 0 })
        {
            var (current, last) = GetListedSeasons();
            var seasons = options.Seasons.Where(season => season.CompareTo(last) <= 0).ToHashSet();
            if (seasons.Count == 0)
                return [];

            inSeasons = anime => anime.SeasonSpan is { } span && span.Seasons.Any(season => season.CompareTo(last) <= 0 && seasons.Contains(season)) &&
                (channelAirings is null || seasons.Any(season => IsOnChannels(channelAirings, anime.AnimeID, season, current)));
        }
        else if (channelAirings is not null)
        {
            inSeasons = anime => channelAirings.ContainsKey(anime.AnimeID);
        }

        var filtered = Filter(options, inSeasons, GetFilteredAnimeIDs(options));
        if (options.Filter?.SortingExpression is not null)
            return [.. filtered.Select(entry => (entry.Anime, entry.Series))];

        var entries = filtered
            .Select(entry => new Entry(entry.Anime, entry.Series, (entry.Title ?? GetTitle(entry.Anime, entry.Series)).ToSortName().ToLowerInvariant()))
            .ToList();

        var orderBy = options.OrderBy ?? (inSeasons is null ? AnidbAnimeListOrder.Title : AnidbAnimeListOrder.AirDate);
        IOrderedEnumerable<Entry> ordered = orderBy switch
        {
            AnidbAnimeListOrder.AirDate => entries
                .OrderBy(entry => entry.Anime.AirDate is null)
                .ThenBy(entry => entry.Anime.AirDate?.ToDateOnly())
                .ThenBy(entry => entry.SortName, StringComparer.Ordinal),
            AnidbAnimeListOrder.Rating => entries
                .OrderByDescending(entry => entry.Anime.Rating)
                .ThenBy(entry => entry.SortName, StringComparer.Ordinal),
            _ => entries
                .OrderBy(entry => entry.SortName, StringComparer.Ordinal),
        };

        return [.. ordered.ThenBy(entry => entry.Anime.AnimeID).Select(entry => (entry.Anime, entry.Series))];
    }

    /// <summary>
    ///   Lists the seasons the cached anime matching the options are in, by
    ///   the rule in <see cref="SeasonCalendar"/>, with how many anime are in
    ///   each, newest first, up to the season after the one under way.
    /// </summary>
    /// <remarks>
    ///   A season's images are those of its best ranked anime with a poster,
    ///   among those starting in it, or else among the newest carried over,
    ///   as <see cref="PickImages"/> ranks them.
    /// </remarks>
    /// <param name="options">The filters; the seasons and order are ignored.</param>
    /// <param name="includeImages">Whether to pick a poster and a backdrop for each season.</param>
    /// <exception cref="ArgumentNullException">
    ///   The filter depends on the user and the options name none.
    /// </exception>
    /// <returns>The seasons, the one under way always among them.</returns>
    public IReadOnlyList<AnidbAnimeSeasonCount> GetSeasons(AnidbAnimeListOptions? options = null, bool includeImages = false)
    {
        options ??= new();
        var (current, last) = GetListedSeasons();
        var channelAirings = options.ChannelIDs is { Count: > 0 } channelIDs ? GetChannelAirings(channelIDs) : null;
        var members = new Dictionary<(int Year, YearlySeason Season), List<Member>>();
        foreach (var (anime, series, _) in Filter(options, inSeasons: null, GetFilteredAnimeIDs(options)))
        {
            if (anime.SeasonSpan is not { } span)
                continue;

            var member = new Member(anime, series, span);
            foreach (var season in SeasonCalendar.GetSeasons(span, last))
            {
                if (channelAirings is not null && !IsOnChannels(channelAirings, anime.AnimeID, season, current))
                    continue;

                if (!members.TryGetValue(season, out var list))
                    members[season] = list = [];

                list.Add(member);
            }
        }

        members.TryAdd(current, []);
        return
        [
            .. members
                .OrderByDescending(pair => pair.Key)
                .Select(pair =>
                {
                    var (poster, backdrop) = includeImages ? PickImages(pair.Value) : (null, null);
                    return new AnidbAnimeSeasonCount(
                        pair.Key.Year,
                        pair.Key.Season,
                        pair.Value.Count,
                        pair.Key == current,
                        poster,
                        backdrop
                    );
                }),
        ];
    }

    /// <summary>
    ///   The images standing for a season: the poster and backdrop of its
    ///   best ranked anime with a poster, among those starting in it, or
    ///   else among those carried over from the latest season before it
    ///   with one. The backdrop is <c>null</c> when that anime has none.
    /// </summary>
    /// <remarks>
    ///   The anime are ranked by start season, newest first, and then by a
    ///   Bayesian weighted rating, (v·R + m·C) / (v + m): R and v are the
    ///   anime's rating and votes, C the mean rating of the rated anime
    ///   starting in the same season, and m the median of their votes, at
    ///   least <see cref="MinimumPriorVotes"/>. Ties go to the earlier
    ///   start, then the lower ID.
    /// </remarks>
    /// <param name="members">The season's anime.</param>
    /// <returns>The poster and backdrop, each <c>null</c> when not found.</returns>
    private (IImage? Poster, IImage? Backdrop) PickImages(IReadOnlyList<Member> members)
    {
        var groups = members
            .GroupBy(member => member.Span.StartSeason)
            .OrderByDescending(group => group.Key);
        foreach (var group in groups)
        {
            foreach (var member in Rank([.. group]))
            {
                if (GetImage(member.Anime, member.Series, ImageEntityType.Primary) is { } poster)
                    return (poster, GetImage(member.Anime, member.Series, ImageEntityType.Backdrop));
            }
        }

        return (null, null);
    }

    /// <summary>
    ///   Ranks anime starting in the same season by their Bayesian weighted
    ///   rating, then the earlier start, then the lower ID.
    /// </summary>
    /// <param name="starters">The anime.</param>
    /// <returns>The anime, best first.</returns>
    private static IEnumerable<Member> Rank(IReadOnlyList<Member> starters)
    {
        var rated = starters.Where(member => member.Anime.VoteCount > 0).ToList();
        var mean = rated.Count > 0 ? rated.Average(member => (double)member.Anime.Rating) : 0d;
        var prior = Math.Max(MinimumPriorVotes, Median(rated.Select(member => member.Anime.VoteCount)));
        return starters
            .OrderByDescending(member => (member.Anime.VoteCount * (double)member.Anime.Rating + prior * mean) / (member.Anime.VoteCount + prior))
            .ThenBy(member => member.Span.First)
            .ThenBy(member => member.Anime.AnimeID);
    }

    /// <summary>
    ///   The median of some counts.
    /// </summary>
    /// <param name="counts">The counts.</param>
    /// <returns>The median, or <c>0</c> when there are none.</returns>
    private static double Median(IEnumerable<int> counts)
    {
        var sorted = counts.Order().ToList();
        if (sorted.Count == 0)
            return 0d;

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + (double)sorted[middle]) / 2;
    }

    /// <summary>
    ///   An image of an anime, picked as the APIv3 series route picks its
    ///   own: its series' best one when it is in the collection, else its own.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="series">Its Shoko series, if any.</param>
    /// <param name="type">The image type.</param>
    /// <returns>The image, or <c>null</c> when it has none of the type.</returns>
    private IImage? GetImage(AniDB_Anime anime, AnimeSeries? series, ImageEntityType type)
        => GetImage(series is not null ? series : anime, type);

    /// <summary>
    ///   The best image of one type of an anime or a series.
    /// </summary>
    /// <param name="entry">The anime or series.</param>
    /// <param name="type">The image type.</param>
    /// <returns>The image, or <c>null</c> when it has none of the type.</returns>
    protected virtual IImage? GetImage(IWithImages entry, ImageEntityType type)
        => entry.GetBestImageForType(type);

    /// <summary>
    ///   The stored airings of every AniDB anime on some channels, by anime.
    /// </summary>
    /// <param name="channelIDs">The channels.</param>
    /// <returns>The airings, by AniDB anime ID.</returns>
    internal virtual IReadOnlyDictionary<int, AnidbAnimeChannelAirings> GetChannelAirings(IReadOnlySet<Guid> channelIDs)
        => airingScheduleService.GetAnidbAnimeOnChannels(channelIDs);

    /// <summary>
    ///   Whether an anime is on the channels in a season: it has an airing on
    ///   them in the season, or one still to come when the season is the one
    ///   under way or a later one.
    /// </summary>
    /// <param name="channelAirings">The anime's airings on the channels.</param>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="season">The season.</param>
    /// <param name="current">The season under way.</param>
    /// <returns><c>true</c> when the anime is on the channels in the season.</returns>
    private static bool IsOnChannels(
        IReadOnlyDictionary<int, AnidbAnimeChannelAirings> channelAirings,
        int animeID,
        (int Year, YearlySeason Season) season,
        (int Year, YearlySeason Season) current
    )
        => channelAirings.TryGetValue(animeID, out var airings) &&
            (airings.Seasons.Contains(season) || (airings.HasUpcoming && season.CompareTo(current) >= 0));

    /// <summary>
    ///   The season under way and the last one listed, the one after it.
    ///   Later seasons are yet to be decided.
    /// </summary>
    /// <returns>The current and last listed seasons.</returns>
    private static ((int Year, YearlySeason Season) Current, (int Year, YearlySeason Season) Last) GetListedSeasons()
    {
        var current = SeasonCalendar.GetCurrentYearlySeason();
        return (current, SeasonCalendar.GetNextYearlySeason(current));
    }

    /// <summary>
    ///   An anime counted in a season.
    /// </summary>
    /// <param name="Anime">The anime.</param>
    /// <param name="Series">Its Shoko series, if any.</param>
    /// <param name="Span">Where it is placed in the seasons.</param>
    private sealed record Member(AniDB_Anime Anime, AnimeSeries? Series, SeasonCalendar.SeasonSpan Span);

    /// <summary>
    ///   The anime of the series the options' filter passes, evaluated once
    ///   for the read.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">
    ///   The filter depends on the user and the options name none.
    /// </exception>
    /// <returns>The AniDB anime IDs, in the filter's order, or <c>null</c> without a filter.</returns>
    private IReadOnlyList<int>? GetFilteredAnimeIDs(AnidbAnimeListOptions options)
        => options.Filter is { } filter ? filteringService.GetFilteredAnimeIDs(filter, options.User) : null;

    /// <summary>
    ///   Every cached anime passing the filters, the cheap ones first, so a
    ///   title is only worked out for an anime that got that far.
    /// </summary>
    /// <param name="options">The filters other than the seasons and the filter.</param>
    /// <param name="inSeasons">The season filter, if any.</param>
    /// <param name="filteredAnimeIDs">The anime the filter passed, in its order, or <c>null</c> for every anime.</param>
    /// <returns>The anime, with their series, and their titles when the prefix needed them.</returns>
    private IEnumerable<(AniDB_Anime Anime, AnimeSeries? Series, string? Title)> Filter(
        AnidbAnimeListOptions options,
        Func<AniDB_Anime, bool>? inSeasons,
        IReadOnlyList<int>? filteredAnimeIDs
    )
    {
        var types = options.Types is { Count: > 0 } ? options.Types.ToHashSet() : null;
        var prefix = string.IsNullOrEmpty(options.TitlePrefix) ? null : options.TitlePrefix;
        var candidates = filteredAnimeIDs is null
            ? animeRepository.GetAll()
            : filteredAnimeIDs.Select(animeRepository.GetByAnimeID).OfType<AniDB_Anime>();
        foreach (var anime in candidates)
        {
            if (types is not null && !types.Contains(anime.AnimeType))
                continue;

            if (!options.IncludeRestricted.Passes(anime.IsRestricted))
                continue;

            if (inSeasons is not null && !inSeasons(anime))
                continue;

            var series = seriesRepository.GetByAnimeID(anime.AnimeID);
            if (!options.InCollection.Passes(series is not null))
                continue;

            if (options.User is { } user && !user.IsAllowedToSee(anime))
                continue;

            if (prefix is null)
            {
                yield return (anime, series, null);
                continue;
            }

            var title = GetTitle(anime, series);
            if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return (anime, series, title);
        }
    }

    /// <summary>
    ///   A cached anime on its way through the listing.
    /// </summary>
    /// <param name="Anime">The anime.</param>
    /// <param name="Series">Its Shoko series, if any.</param>
    /// <param name="SortName">Its preferred title as sorted.</param>
    private sealed record Entry(AniDB_Anime Anime, AnimeSeries? Series, string SortName);

    #endregion

    #region Details

    /// <summary>
    ///   The preferred title of an anime: its series' one when it is in the
    ///   collection, else its own.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="series">Its Shoko series, if any.</param>
    /// <returns>The title.</returns>
    public string GetTitle(AniDB_Anime anime, AnimeSeries? series)
    {
        IWithTitles entry = series is not null ? series : anime;
        return (textManager.GetPreferredTitle(entry) ?? entry.DefaultTitle).Value;
    }

    /// <summary>
    ///   The preferred title of a cached anime by its ID: its series' one
    ///   when it is in the collection, else its own.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="user">The user whose restrictions apply, if any.</param>
    /// <returns>The title, or <c>null</c> when the anime is not cached or the user may not see it.</returns>
    public string? GetTitle(int animeID, IUser? user = null)
    {
        if (animeRepository.GetByAnimeID(animeID) is not { } anime || (user is not null && !user.IsAllowedToSee(anime)))
            return null;

        return GetTitle(anime, seriesRepository.GetByAnimeID(animeID));
    }

    /// <summary>
    ///   The preferred overview of an anime: its series' one when it is in
    ///   the collection, else its own, falling back to the default one.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="series">Its Shoko series, if any.</param>
    /// <returns>The overview, or <c>null</c> when there is none.</returns>
    public string? GetOverview(AniDB_Anime anime, AnimeSeries? series)
    {
        IWithOverviews entry = series is not null ? series : anime;
        return (textManager.GetPreferredOverview(entry) ?? entry.DefaultOverview)?.Value;
    }

    /// <summary>
    ///   The poster of an anime: its series' primary image when it is in the
    ///   collection and has one, else its own.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="series">Its Shoko series, if any.</param>
    /// <returns>The poster, or <c>null</c> when neither has one.</returns>
    public IImage? GetPoster(AniDB_Anime anime, AnimeSeries? series)
        => (series is not null ? GetImage(series, ImageEntityType.Primary) : null) ?? GetImage(anime, ImageEntityType.Primary);

    /// <summary>
    ///   The animation studios of many anime, from their "Animation Work"
    ///   roles, in one database read.
    /// </summary>
    /// <param name="animeIDs">The AniDB anime IDs.</param>
    /// <returns>The studios by anime, in AniDB's order; an anime without any is left out.</returns>
    public IReadOnlyDictionary<int, IReadOnlyList<AniDB_Creator>> GetStudios(IReadOnlyCollection<int> animeIDs)
        => staffRepository.GetStudiosByAnimeIDs(animeIDs)
            .GroupBy(xref => xref.AnimeID)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<AniDB_Creator>)
                [
                    .. group
                        .OrderBy(xref => xref.Ordering)
                        .Select(xref => creatorRepository.GetByCreatorID(xref.CreatorID))
                        .OfType<AniDB_Creator>()
                        .Where(creator => creator.Type is CreatorType.Company)
                        .DistinctBy(creator => creator.CreatorID),
                ]
            );

    /// <summary>
    ///   The heaviest genre tags of an anime, leaving out spoilers: those
    ///   AniDB marks, for the anime or for every anime, and the plot tags
    ///   the tag filter hides.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="limit">The most tags to return.</param>
    /// <returns>The tags, heaviest first, then by name.</returns>
    public IReadOnlyList<AniDB_Tag> GetGenreTags(int animeID, int limit)
    {
        if (limit <= 0)
            return [];

        return
        [
            .. animeTagRepository.GetByAnimeID(animeID)
                .Where(xref => !xref.LocalSpoiler)
                .Select(xref => (xref.Weight, Tag: tagRepository.GetByTagID(xref.TagID)))
                .Where(pair => pair.Tag is { Verified: true, GlobalSpoiler: false } tag
                    && !TagFilter.IsTagBlackListed(tag.TagName, TagFilter.Filter.Genre | TagFilter.Filter.Invert)
                    && !TagFilter.IsTagBlackListed(tag.TagName, TagFilter.Filter.Plot))
                .OrderByDescending(pair => pair.Weight)
                .ThenBy(pair => pair.Tag!.TagName, StringComparer.OrdinalIgnoreCase)
                .DistinctBy(pair => pair.Tag!.TagName, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(pair => pair.Tag!),
        ];
    }

    /// <summary>
    ///   The season an anime starts in, by the rule the seasons are listed
    ///   with: the one a user set by hand, else the one its first regular
    ///   episode airs in, after an early premiere or a batch drop is
    ///   accounted for, or the one of its start date without dated regular
    ///   episodes.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The season, or <c>null</c> when it has no dates to go by.</returns>
    public (int Year, YearlySeason Season)? GetStartSeason(AniDB_Anime anime)
        => anime.SeasonSpan?.StartSeason;

    /// <summary>
    ///   Whether the season an anime starts in was set by hand rather than
    ///   worked out by the rule.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns><c>true</c> when a user set its start season.</returns>
    public bool IsStartSeasonOverridden(AniDB_Anime anime)
        => anime.SeasonSpan?.IsStartOverridden ?? false;

    /// <summary>
    ///   The usual length of an anime's regular episodes: the median of
    ///   their known lengths.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The length, or <c>null</c> when no regular episode has a known length.</returns>
    public TimeSpan? GetEpisodeDuration(int animeID)
    {
        var lengths = episodeRepository.GetByAnimeID(animeID)
            .Where(episode => episode.EpisodeType is EpisodeType.Episode && episode.LengthSeconds > 0)
            .Select(episode => episode.LengthSeconds)
            .ToList();
        return lengths.Count > 0 ? TimeSpan.FromSeconds(Median(lengths)) : null;
    }

    /// <summary>
    ///   How many local files are linked to an anime, each counted once
    ///   however many of its episodes it covers.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The count.</returns>
    public int GetVideoCount(int animeID)
        => fileCrossReferenceRepository.GetByAnimeID(animeID)
            .DistinctBy(xref => (xref.Hash, xref.FileSize))
            .Count(xref => videoRepository.GetByEd2kAndSize(xref.Hash, xref.FileSize) is not null);

    #endregion
}
