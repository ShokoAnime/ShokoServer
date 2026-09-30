using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh all TMDB movies linked to the series.
/// </summary>
public sealed class RefreshTmdbMoviesSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    public override string Name => "Refresh TMDB Movies";

    public override string? Description => "Refresh all linked TMDB movie metadata.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var options = new MetadataRefreshOptions { DownloadImages = true, Reason = MetadataRefreshReason.Requested };
        foreach (var movieID in Series.GetMovieCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.RefreshEntry(movieID, options: options, cancellationToken: token).ConfigureAwait(false);
    }
}
