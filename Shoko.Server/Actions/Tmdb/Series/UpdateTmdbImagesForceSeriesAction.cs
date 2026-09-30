using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Force a complete redownload of TMDB images for the series.
/// </summary>
public sealed class UpdateTmdbImagesForceSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    public override string Name => "Update TMDB Images - Force";

    public override string? Description => "Forces a complete redownload of images from TMDB.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var showID in Series.GetSeriesCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.DownloadImages(showID, force: true, cancellationToken: token).ConfigureAwait(false);
        foreach (var movieID in Series.GetMovieCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.DownloadImages(movieID, force: true, cancellationToken: token).ConfigureAwait(false);
    }
}
