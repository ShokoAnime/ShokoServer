using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;

using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;

namespace Shoko.Server.Services;

/// <summary>
///   Lists the cached AniDB anime, in the collection or not, for season
///   views and other browsing, and looks up the extra details such a list
///   shows for a page of them at a time.
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
    IMetadataTextManager textManager
)
{
    // The least prior weight, in votes, the season ranking gives the mean.
    private const int MinimumPriorVotes = 50;

    // AniDB sends no episode air dates before this day.
    private static readonly DateOnly UndatedEpisodesBefore = new(1970, 1, 1);

    #region Listing

    /// <summary>
    ///   Lists the cached anime matching the options, in their order.
    /// </summary>
    /// <param name="options">The filters and order.</param>
    /// <returns>The anime, each with its Shoko series when there is one.</returns>
    public IReadOnlyList<(AniDB_Anime Anime, AnimeSeries? Series)> GetAnime(AnidbAnimeListOptions? options = null)
    {
        options ??= new();
        Func<AniDB_Anime, bool>? inSeasons = null;
        if (options.Seasons is { Count: > 0 })
        {
            var (_, last) = GetListedSeasons();
            var seasons = options.Seasons.Where(season => Ordinal(season) <= Ordinal(last)).ToHashSet();
            if (seasons.Count == 0)
                return [];

            var episodes = GetRegularEpisodesByAnime();
            inSeasons = anime => GetAiring(anime, episodes, last) is { } airing && airing.Seasons.Overlaps(seasons);
        }

        var entries = Filter(options, inSeasons)
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
    ///   Lists the seasons the regular episodes of the cached anime matching
    ///   the options air in, with how many anime are in each, newest first,
    ///   up to the season after the one under way.
    /// </summary>
    /// <remarks>
    ///   A season's images are those of its best ranked anime with a poster,
    ///   among those starting in it, as <see cref="PickImages"/> ranks them.
    /// </remarks>
    /// <param name="options">The filters; the seasons and order are ignored.</param>
    /// <param name="includeImages">Whether to pick a poster and a backdrop for each season.</param>
    /// <returns>The seasons, the one under way always among them.</returns>
    public IReadOnlyList<AnidbAnimeSeasonCount> GetSeasons(AnidbAnimeListOptions? options = null, bool includeImages = false)
    {
        var (current, last) = GetListedSeasons();
        var episodes = GetRegularEpisodesByAnime();
        var members = new Dictionary<(int Year, YearlySeason Season), List<Member>>();
        foreach (var (anime, series, _) in Filter(options ?? new(), inSeasons: null))
        {
            if (GetAiring(anime, episodes, last) is not { } airing)
                continue;

            var member = new Member(anime, series, airing);
            foreach (var season in airing.Seasons)
            {
                if (!members.TryGetValue(season, out var list))
                    members[season] = list = [];

                list.Add(member);
            }
        }

        members.TryAdd(current, []);
        return
        [
            .. members
                .OrderByDescending(pair => Ordinal(pair.Key))
                .Select(pair =>
                {
                    var (poster, backdrop) = includeImages ? PickImages(pair.Key, pair.Value) : (null, null);
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
    ///   best ranked anime with a poster, among those starting in it. The
    ///   backdrop is <c>null</c> when that anime has none.
    /// </summary>
    /// <remarks>
    ///   The rank is a Bayesian weighted rating, (v·R + m·C) / (v + m): R and
    ///   v are the anime's rating and votes, C the mean rating of the rated
    ///   starters, and m the median of their votes, at least
    ///   <see cref="MinimumPriorVotes"/>. Ties go to the earlier start, then
    ///   the lower ID.
    /// </remarks>
    /// <param name="season">The season.</param>
    /// <param name="members">The season's anime.</param>
    /// <returns>The poster and backdrop, each <c>null</c> when not found.</returns>
    private (IImage? Poster, IImage? Backdrop) PickImages((int Year, YearlySeason Season) season, IReadOnlyList<Member> members)
    {
        var starters = members.Where(member => member.Airing.FirstSeason == season).ToList();
        var rated = starters.Where(member => member.Anime.VoteCount > 0).ToList();
        var mean = rated.Count > 0 ? rated.Average(member => (double)member.Anime.Rating) : 0d;
        var prior = Math.Max(MinimumPriorVotes, Median(rated.Select(member => member.Anime.VoteCount)));
        var ranked = starters
            .OrderByDescending(member => (member.Anime.VoteCount * (double)member.Anime.Rating + prior * mean) / (member.Anime.VoteCount + prior))
            .ThenBy(member => member.Airing.First)
            .ThenBy(member => member.Anime.AnimeID);
        foreach (var member in ranked)
        {
            if (GetImage(member.Anime, member.Series, ImageEntityType.Primary) is { } poster)
                return (poster, GetImage(member.Anime, member.Series, ImageEntityType.Backdrop));
        }

        return (null, null);
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
    protected virtual IImage? GetImage(AniDB_Anime anime, AnimeSeries? series, ImageEntityType type)
    {
        IWithImages entry = series is not null ? series : anime;
        return entry.GetBestImageForType(type);
    }

    /// <summary>
    ///   The season under way and the last one listed, the one after it.
    ///   Later seasons are yet to be decided.
    /// </summary>
    /// <returns>The current and last listed seasons.</returns>
    private static ((int Year, YearlySeason Season) Current, (int Year, YearlySeason Season) Last) GetListedSeasons()
    {
        var current = Extensions.Models.GetYearlySeason(DateTime.Today.ToDateOnly());
        return (current, Extensions.Models.GetNextYearlySeason(current));
    }

    /// <summary>
    ///   A season's place in time, for comparing seasons.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>A number growing with each season.</returns>
    private static int Ordinal((int Year, YearlySeason Season) season)
        => season.Year * 4 + (int)season.Season;

    /// <summary>
    ///   The regular episodes with an air date of every cached anime, in one
    ///   pass over the cached episodes.
    /// </summary>
    /// <returns>The episodes by AniDB anime ID.</returns>
    private Dictionary<int, List<AniDB_Episode>> GetRegularEpisodesByAnime()
    {
        var episodes = new Dictionary<int, List<AniDB_Episode>>();
        foreach (var episode in episodeRepository.GetAll())
        {
            if (!IsDatedRegular(episode))
                continue;

            if (!episodes.TryGetValue(episode.AnimeID, out var list))
                episodes[episode.AnimeID] = list = [];

            list.Add(episode);
        }

        return episodes;
    }

    /// <summary>
    ///   Whether an episode counts towards the seasons: a regular episode
    ///   with an air date.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns><c>true</c> when it counts.</returns>
    private static bool IsDatedRegular(AniDB_Episode episode)
        => episode.EpisodeType is EpisodeType.Episode && episode.AirDate != 0;

    /// <summary>
    ///   When an anime's regular episodes air, looked up in the episodes of
    ///   every anime.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="episodes">The regular episodes by anime, from <see cref="GetRegularEpisodesByAnime"/>.</param>
    /// <param name="last">The last listed season.</param>
    /// <returns>The airing, or <c>null</c> when it has no air dates to go by.</returns>
    private static Airing? GetAiring(AniDB_Anime anime, Dictionary<int, List<AniDB_Episode>> episodes, (int Year, YearlySeason Season) last)
        => GetAiring(anime, episodes.TryGetValue(anime.AnimeID, out var list) ? list : [], last);

    /// <summary>
    ///   When an anime's regular episodes air, by their regular broadcast
    ///   dates, and the listed seasons that puts it in. AniDB sends no
    ///   episode air dates before 1970, so an anime starting before then
    ///   covers the seasons from its own start date up to its first dated
    ///   episode, or to its end date when no episode is dated.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="episodes">The anime's dated regular episodes.</param>
    /// <param name="last">The last listed season.</param>
    /// <returns>The airing, or <c>null</c> when it has no air dates to go by.</returns>
    private static Airing? GetAiring(AniDB_Anime anime, IEnumerable<AniDB_Episode> episodes, (int Year, YearlySeason Season) last)
    {
        DateOnly? first = null;
        var seasons = new HashSet<(int Year, YearlySeason Season)>();
        foreach (var episode in episodes)
        {
            if ((anime.GetRegularAirDate(episode) ?? episode.GetAirDateAsDateOnly()) is not { } date)
                continue;

            if (first is null || date < first)
                first = date;

            var season = Extensions.Models.GetYearlySeason(date);
            if (Ordinal(season) <= Ordinal(last))
                seasons.Add(season);
        }

        if (anime.AirDate?.ToDateOnly() is { } animeStart && animeStart < UndatedEpisodesBefore && (first is null || animeStart < first))
        {
            var startSeason = Extensions.Models.GetYearlySeason(animeStart);
            var until = first is { } dated
                ? Extensions.Models.GetYearlySeason(dated)
                : anime.EndDate?.ToDateOnly() is { } end && end >= animeStart ? Extensions.Models.GetYearlySeason(end) : startSeason;
            for (var season = startSeason; Ordinal(season) <= Ordinal(until) && Ordinal(season) <= Ordinal(last); season = Extensions.Models.GetNextYearlySeason(season))
                seasons.Add(season);

            first = animeStart;
        }

        return first is { } start ? new(start, Extensions.Models.GetYearlySeason(start), seasons) : null;
    }

    /// <summary>
    ///   When an anime's regular episodes air.
    /// </summary>
    /// <param name="First">The first regular episode's air date.</param>
    /// <param name="FirstSeason">The season the anime starts in.</param>
    /// <param name="Seasons">The listed seasons a regular episode airs in.</param>
    private sealed record Airing(DateOnly First, (int Year, YearlySeason Season) FirstSeason, IReadOnlySet<(int Year, YearlySeason Season)> Seasons);

    /// <summary>
    ///   An anime counted in a season.
    /// </summary>
    /// <param name="Anime">The anime.</param>
    /// <param name="Series">Its Shoko series, if any.</param>
    /// <param name="Airing">When its regular episodes air.</param>
    private sealed record Member(AniDB_Anime Anime, AnimeSeries? Series, Airing Airing);

    /// <summary>
    ///   Every cached anime passing the filters, the cheap ones first, so a
    ///   title is only worked out for an anime that got that far.
    /// </summary>
    /// <param name="options">The filters other than the seasons.</param>
    /// <param name="inSeasons">The season filter, if any.</param>
    /// <returns>The anime, with their series, and their titles when the prefix needed them.</returns>
    private IEnumerable<(AniDB_Anime Anime, AnimeSeries? Series, string? Title)> Filter(AnidbAnimeListOptions options, Func<AniDB_Anime, bool>? inSeasons)
    {
        var types = options.Types is { Count: > 0 } ? options.Types.ToHashSet() : null;
        var prefix = string.IsNullOrEmpty(options.TitlePrefix) ? null : options.TitlePrefix;
        foreach (var anime in animeRepository.GetAll())
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

            if (options.IncludeMissing is not InclusionFilter.True && !options.IncludeMissing.Passes(series is not null && GetVideoCount(anime.AnimeID) is 0))
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
    ///   The season an anime starts in: the one its first regular episode
    ///   airs in, by the rule the seasons are listed with.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The season, or <c>null</c> when it has no air dates to go by.</returns>
    public (int Year, YearlySeason Season)? GetStartSeason(AniDB_Anime anime)
        => GetAiring(anime, episodeRepository.GetByAnimeID(anime.AnimeID).Where(IsDatedRegular), GetListedSeasons().Last)?.FirstSeason;

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
