using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Supplies series-shaped metadata: the anime, its seasons and its episodes.
/// </summary>
/// <remarks>
///   The core's refresh job asks you to refresh each series linked to an
///   anime on your source, and you write what you fetch into the core's
///   stores yourself. Everything the server shows of your series is read back
///   from <see cref="Storage.IMetadataSeriesStore"/> and the other stores, so
///   you keep no getters.
/// </remarks>
public interface IMetadataSeriesProvider : IMetadataProvider
{
    /// <summary>
    ///   Refresh a linked series from your source and write it into the
    ///   stores.
    /// </summary>
    /// <remarks>
    ///   Save the series with its seasons and episodes through
    ///   <see cref="Storage.IMetadataSeriesStore.SaveSeries"/>, its credits
    ///   through <see cref="Storage.IMetadataPeopleStore"/>, and its tags,
    ///   studios, relations and suggestions through their own stores. Only the
    ///   core's refresh job calls it, holding the series' lock once it is due,
    ///   so check no freshness. Throw on failure: the queue retries the job.
    /// </remarks>
    /// <param name="seriesID">
    ///   The series, as its link names it (the
    ///   <see cref="IMetadataCrossReference.ProviderID"/>): on your source and
    ///   of the <c>series</c> kind.
    /// </param>
    /// <param name="options">What kind of refresh this is, and why.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the series is written.</returns>
    Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default);
}
