using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Judges again, by the matching rules as they are now, the series and
///   movie links the auto-linker made on dates alone, and removes the ones
///   the rules no longer take.
/// </summary>
/// <remarks>
///   Only links rated <see cref="MatchRating.DateMatches"/> or
///   <see cref="MatchRating.DateKindaMatches"/> are judged, since those stand
///   on the date rules alone and the stored entry says all they need; a link
///   with title evidence, one a user verified and one taken from AniDB's own
///   resources are left as they are. Each is judged from the stored entry,
///   without asking its provider. A link the rules now turn down is removed
///   and a search is queued for its anime on the source; one they rate
///   otherwise keeps its new rating.
/// </remarks>
/// <param name="crossReferences">The links.</param>
/// <param name="metadataService">The stored entries.</param>
/// <param name="matchingEngine">The matching rules.</param>
/// <param name="linkingService">Removes the links turned down.</param>
/// <param name="providerScheduler">Queues the searches.</param>
/// <param name="anidbAnimeRepository">The AniDB anime.</param>
/// <param name="anidbEpisodeRepository">The AniDB episodes, for the movie links.</param>
/// <param name="cancellationAccessor">Cancels the work.</param>
/// <param name="progressAccessor">Takes how far the work is.</param>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class RejudgeMetadataLinksJob(
    IMetadataCrossReferenceStore crossReferences,
    IMetadataService metadataService,
    IMetadataMatchingEngine matchingEngine,
    IMetadataLinkingService linkingService,
    MetadataProviderScheduler providerScheduler,
    AniDB_AnimeRepository anidbAnimeRepository,
    AniDB_EpisodeRepository anidbEpisodeRepository,
    IJobCancellationAccessor cancellationAccessor,
    IJobProgressAccessor progressAccessor
) : BaseJob
{
    #region Properties

    /// <inheritdoc />
    public override string TypeName => "Re-judge Metadata Links";

    /// <inheritdoc />
    public override string Title => "Re-judging Metadata Links";

    #endregion

    #region Execution

    /// <summary>
    ///   The ratings that stand on a date alone, and so are judged again.
    /// </summary>
    internal static readonly HashSet<MatchRating> DateOnlyRatings = [MatchRating.DateMatches, MatchRating.DateKindaMatches];

    /// <inheritdoc />
    public override async Task Execute()
    {
        var token = cancellationAccessor.Token;
        var links = new List<IMetadataCrossReference>();
        links.AddRange(crossReferences.GetAllSeriesLinks().Where(link => DateOnlyRatings.Contains(link.MatchRating)));
        links.AddRange(crossReferences.GetAllMovieLinks().Where(link => DateOnlyRatings.Contains(link.MatchRating)));

        var (kept, rerated, removed) = (0, 0, 0);
        var searches = new HashSet<(MetadataSource Source, int AnimeID)>();
        for (var index = 0; index < links.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            progressAccessor.Progress?.Report(links.Count is 0 ? 100 : index * 100m / links.Count);
            var link = links[index];
            if (link.ProviderID is not { } providerID || anidbAnimeRepository.GetByAnimeID(link.AnidbAnimeID) is not { } anime)
                continue;

            var rating = Judge(link, providerID, anime);
            if (rating is null)
            {
                kept++;
                continue;
            }

            if (rating is MatchRating.None)
            {
                var removedRating = link.MatchRating;
                if (await Remove(link, providerID).ConfigureAwait(false))
                {
                    removed++;
                    searches.Add((link.Source, link.AnidbAnimeID));
                    _logger.LogInformation("Removed the link of AniDB anime {AnimeID} to {Entry}, rated {Rating}, which the matching rules no longer take.", link.AnidbAnimeID, providerID, removedRating);
                }

                continue;
            }

            if (rating == link.MatchRating)
            {
                kept++;
                continue;
            }

            // Read first, as the link is the cached row and the write updates it.
            var previous = link.MatchRating;
            await Rerate(link, providerID, rating.Value).ConfigureAwait(false);
            rerated++;
            _logger.LogInformation("Rated the link of AniDB anime {AnimeID} to {Entry} {Rating}, rated {Previous} before.", link.AnidbAnimeID, providerID, rating, previous);
        }

        foreach (var (source, animeID) in searches)
            await providerScheduler.ScheduleSearch(source, animeID, cancellationToken: token).ConfigureAwait(false);

        progressAccessor.Progress?.Report(100);
        _logger.LogInformation("Re-judged {Count} date-only metadata links: {Kept} kept, {Rerated} re-rated and {Removed} removed.", links.Count, kept, rerated, removed);
    }

    /// <summary>
    ///   The rating the matching rules give a link now.
    /// </summary>
    /// <returns>The rating, or <c>null</c> when the stored entry or episode is not there to judge.</returns>
    private MatchRating? Judge(IMetadataCrossReference link, MetadataGuid providerID, IAnidbAnime anime)
    {
        switch (metadataService.GetEntry(providerID))
        {
            case IMovie movie when link is IMetadataMovieCrossReference movieLink:
                if (anidbEpisodeRepository.GetByEpisodeID(movieLink.AnidbEpisodeID) is not { } episode)
                    return null;
                return matchingEngine.MatchMovies(anime, episode, [Describe(providerID, movie)]).FirstOrDefault()?.Rating ?? MatchRating.None;
            case ISeries series:
                return matchingEngine.MatchSeries(anime, [Describe(providerID, series)]).FirstOrDefault()?.Rating ?? MatchRating.None;
            default:
                return null;
        }
    }

    internal static MetadataSeriesSearchResult Describe(MetadataGuid providerID, ISeries series)
        => new()
        {
            ID = providerID,
            Title = series.Title,
            AlternateTitles = [.. series.Titles.Select(title => title.Value).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()],
            IsRestricted = series.Restricted,
            FirstAiredAt = series.AirDate,
            Type = series.Type,
            EpisodeCount = series.EpisodeCounts.Episodes > 0 ? series.EpisodeCounts.Episodes : null,
        };

    internal static MetadataMovieSearchResult Describe(MetadataGuid providerID, IMovie movie)
        => new()
        {
            ID = providerID,
            Title = movie.Title,
            AlternateTitles = [.. movie.Titles.Select(title => title.Value).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()],
            IsRestricted = movie.Restricted,
            ReleasedAt = movie.ReleaseDate is { } released ? new PartialDateOnly(DateOnly.FromDateTime(released)) : null,
        };

    private Task<bool> Remove(IMetadataCrossReference link, MetadataGuid providerID)
        => link is IMetadataMovieCrossReference movieLink
            ? linkingService.RemoveMovieLink(new()
            {
                Source = link.Source,
                EntityType = MetadataEntityType.Movie,
                ProviderID = providerID,
                AnidbAnimeID = link.AnidbAnimeID,
                AnidbEpisodeID = movieLink.AnidbEpisodeID,
            }, cancellationAccessor.Token)
            : linkingService.RemoveSeriesLink(new()
            {
                Source = link.Source,
                EntityType = providerID.EntityType,
                ProviderID = providerID,
                AnidbAnimeID = link.AnidbAnimeID,
            }, cancellationAccessor.Token);

    // Written over in place, keeping who wrote it.
    private async Task Rerate(IMetadataCrossReference link, MetadataGuid providerID, MatchRating rating)
    {
        if (link is IMetadataMovieCrossReference movieLink)
            await crossReferences.MergeMovieLinks(
                [new() { Source = link.Source, AnidbAnimeID = link.AnidbAnimeID, AnidbEpisodeID = movieLink.AnidbEpisodeID, ProviderID = providerID, MatchRating = rating }],
                cancellationToken: cancellationAccessor.Token
            ).ConfigureAwait(false);
        else
            await crossReferences.MergeSeriesLinks(
                [new() { Source = link.Source, AnidbAnimeID = link.AnidbAnimeID, ProviderID = providerID, MatchRating = rating }],
                cancellationToken: cancellationAccessor.Token
            ).ConfigureAwait(false);
    }

    #endregion
}
