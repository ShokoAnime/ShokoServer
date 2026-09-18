using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories.Cached.Anilist;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Supplies the broadcast times AniList reports for an anime. AniList names
/// neither a station nor a platform, so everything it knows lands on one
/// channel-less schedule per anime, with a single original-language track.
/// </summary>
/// <remarks>
/// The provider itself only carries refresh requests. The schedules and
/// airings are written by <see cref="AnilistMetadataService"/> as part of the
/// anime update, since that is where the airing schedule arrives and where the
/// episode rows it attaches to are rebuilt.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="metadataService">The AniList metadata service doing the work.</param>
/// <param name="xrefAnidbAnilistAnime">The AniDB — AniList anime cross-reference repository.</param>
public class AnilistAiringScheduleProvider(
    ILogger<AnilistAiringScheduleProvider> logger,
    AnilistMetadataService metadataService,
    CrossRef_AniDB_Anilist_AnimeRepository xrefAnidbAnilistAnime
) : IAiringScheduleProvider
{
    #region Provider Info

    /// <inheritdoc/>
    public string Name => "AniList";

    /// <inheritdoc/>
    public string Description => "Broadcast times from AniList's airing schedule, as one channel-less schedule per anime.";

    /// <inheritdoc/>
    public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind>() { AiringKind.Original };

    #endregion

    #region Refreshing

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///   <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public async Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        cancellationToken.ThrowIfCancellationRequested();

        var anilistAnimeIDs = GetAnilistAnimeIDs(series);
        if (anilistAnimeIDs.Count is 0)
        {
            logger.LogDebug("No AniList anime is linked to {Source} series {SeriesID}. Nothing to refresh.", series.Source, series.ID);
            return false;
        }

        foreach (var anilistAnimeID in anilistAnimeIDs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // An update already in flight rewrites the schedule when it lands,
            // so queueing a second pass behind it is wasted work.
            if (metadataService.IsAnimeUpdating(anilistAnimeID))
            {
                logger.LogDebug("AniList anime {AnimeID} is already updating. Leaving the refresh to it.", anilistAnimeID);
                continue;
            }

            await metadataService.ScheduleUpdateOfAnime(new() { AnimeId = anilistAnimeID }).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// The AniList anime the series can be keyed to: itself when it is an
    /// AniList anime, and otherwise everything linked to the AniDB anime
    /// behind it.
    /// </summary>
    /// <param name="series">The series to key.</param>
    /// <returns>The AniList anime IDs, which is empty when the series cannot be keyed.</returns>
    private List<int> GetAnilistAnimeIDs(ISeries series)
    {
        if (series.Source is DataSource.AniList)
            return [series.ID];

        var anidbAnimeIDs = new HashSet<int>();
        if (series.Source is DataSource.AniDB)
            anidbAnimeIDs.Add(series.ID);
        foreach (var shokoSeries in series is IShokoSeries own ? [own] : series.ShokoSeries)
            anidbAnimeIDs.Add(shokoSeries.AnidbAnimeID);

        return anidbAnimeIDs
            .SelectMany(xrefAnidbAnilistAnime.GetByAnidbAnimeID)
            .Select(xref => xref.AnilistAnimeID)
            .Where(anilistAnimeID => anilistAnimeID is not 0)
            .Distinct()
            .ToList();
    }

    #endregion
}
