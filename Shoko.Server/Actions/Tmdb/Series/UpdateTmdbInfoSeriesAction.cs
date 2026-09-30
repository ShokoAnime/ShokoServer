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
///   Update all TMDB shows and movies linked to the series.
/// </summary>
public sealed class UpdateTmdbInfoSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    public override string Name => "Update TMDB Info";

    public override string? Description => "Gets the latest series information from TMDB.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        var options = new MetadataRefreshOptions { DownloadImages = true, Reason = MetadataRefreshReason.Requested };
        foreach (var showID in Series.GetSeriesCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.RefreshEntry(showID, options: options, cancellationToken: token).ConfigureAwait(false);
        foreach (var movieID in Series.GetMovieCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.RefreshEntry(movieID, options: options, cancellationToken: token).ConfigureAwait(false);
    }
}
