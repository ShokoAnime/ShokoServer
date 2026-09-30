using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Copies the season and numbers of the series store's episodes onto the
///   episode links naming them. A write to the series store queues it for
///   that series; a recurring sweep, with nothing set, covers every plugin
///   source's links. Links to episodes the store does not hold are left as
///   they were written.
/// </summary>
/// <remarks>
///   The links to an episode a save drops are removed by the series store.
/// </remarks>
/// <param name="crossReferences">The links.</param>
[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Metadata)]
public class SyncEpisodeLinksJob(MetadataCrossReferenceStore crossReferences) : BaseJob
{
    #region Properties

    /// <summary>
    ///   The value of the source whose links are synced, or <c>null</c> for
    ///   every plugin source.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///   The source's own ID for the stored series whose episodes' links are
    ///   synced, or <c>null</c> for every link of the source.
    /// </summary>
    public string? SeriesID { get; set; }

    /// <inheritdoc />
    public override string TypeName => "Sync Episode Links";

    /// <inheritdoc />
    public override string Title => "Syncing Episode Links";

    /// <inheritdoc />
    public override Dictionary<string, object> Details => Source is null
        ? []
        : SeriesID is null
            ? new() { { "Source", Source } }
            : new() { { "Source", Source }, { "SeriesID", SeriesID } };

    #endregion

    #region Execution

    /// <inheritdoc />
    public override Task Execute()
    {
        MetadataSource? source = null;
        if (Source is not null && !MetadataSource.TryParse(Source, out source))
        {
            _logger.LogWarning("Not syncing the episode links of {Source}, which is not a metadata source.", Source);
            return Task.CompletedTask;
        }

        var changed = crossReferences.SyncFromSeriesStore(source, SeriesID);
        _logger.LogDebug("Synced {Count} episode links from the series store for {Source} {SeriesID}.", changed.Count, Source ?? "every source", SeriesID);
        return Task.CompletedTask;
    }

    #endregion
}
