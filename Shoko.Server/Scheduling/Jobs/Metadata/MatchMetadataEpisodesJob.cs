using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Matches an anime's episodes again against every series it is linked to
///   on one source, filling the episodes left unmatched or linked to nothing
///   by the matching, and keeping the links a person made.
/// </summary>
/// <remarks>
///   Queued after a refresh of the anime or of a series it is linked to, and
///   in place of a refresh when the anime's entries are still fresh. One job
///   type for every source, as the matching only reads what the stores hold;
///   one at a time, so two anime never claim the same episode at once.
/// </remarks>
/// <param name="linkingService">Matches the episodes.</param>
/// <param name="cancellation">Stops the matching.</param>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class MatchMetadataEpisodesJob(
    MetadataLinkingService linkingService,
    IJobCancellationAccessor cancellation
) : BaseJob
{
    #region Properties

    /// <summary>
    ///   The value of the source whose links are matched.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///   The AniDB anime whose episodes are matched.
    /// </summary>
    public int AnimeID { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Match Metadata Episodes";

    /// <inheritdoc />
    public override string Title => "Matching Metadata Episodes";

    /// <inheritdoc />
    public override Dictionary<string, object> Details => new() { { "Source", Source }, { "AnimeID", AnimeID } };

    #endregion

    #region Execution

    /// <inheritdoc />
    public override async Task Execute()
    {
        if (!MetadataSource.TryParse(Source, out var source))
        {
            _logger.LogWarning("Not matching the episodes of AniDB anime {AnimeID} on {Source}, which is not a metadata source.", AnimeID, Source);
            return;
        }

        var written = await linkingService.RematchEpisodes(source, AnimeID, cancellation.Token).ConfigureAwait(false);
        _logger.LogDebug("Matched the episodes of AniDB anime {AnimeID} on {Source} again, writing {Count} episode links.", AnimeID, source, written);
    }

    #endregion
}
