using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Automatically search for a TMDB match for the series.
/// </summary>
public sealed class AutoSearchTmdbSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    public override string Name => "Auto-Search TMDB Match";

    public override string? Description => "Automatically searches for a TMDB match.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.User;

    public override Task Execute(CancellationToken token = default)
        => refreshService.AutoSearch(MetadataSource.TMDB, Series.AnidbAnimeID, cancellationToken: token);
}
