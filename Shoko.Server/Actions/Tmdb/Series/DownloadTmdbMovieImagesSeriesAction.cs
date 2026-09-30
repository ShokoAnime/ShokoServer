using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download images for all TMDB movies linked to the series.
/// </summary>
public sealed class DownloadTmdbMovieImagesSeriesAction(IMetadataRefreshService refreshService) : SeriesAction
{
    public override string Name => "Download TMDB Movie Images";

    public override string? Description => "Download any missing images for linked TMDB movies.";

    public override ActionCategory Category => ActionCategory.TMDB;

    public override ActionPermission Permission => ActionPermission.Admin;

    public override async Task Execute(CancellationToken token = default)
    {
        foreach (var movieID in Series.GetMovieCrossReferences(MetadataSource.TMDB).Select(xref => xref.ProviderID).WhereNotNull())
            await refreshService.DownloadImages(movieID, cancellationToken: token).ConfigureAwait(false);
    }
}
