using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh everything the series is linked to, from every enabled metadata
///   provider or from one source.
/// </summary>
public sealed class RefreshLinkedMetadataSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    /// <summary>
    ///   The source to refresh from, or <see langword="null"/> for every
    ///   enabled provider.
    /// </summary>
    public MetadataSource? Source { get; set; }

    public override string Name => "Refresh Linked Metadata";

    public override string? Description => "Refreshes everything the series is linked to, however recently it was refreshed, from every enabled metadata provider or from one source.";

    public override ActionPermission Permission => ActionPermission.Admin;

    public override Task Execute(CancellationToken token = default)
        => refreshService.RefreshForAnime(Series.AnidbAnimeID, Source, force: true, cancellationToken: token);
}
