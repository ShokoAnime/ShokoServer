using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Supplies collection-shaped metadata: a set of works that belong
///   together.
/// </summary>
/// <remarks>
///   Implement it alongside <see cref="IMetadataSeriesProvider"/> or
///   <see cref="IMetadataMovieProvider"/>. Collections are not linked to anime:
///   one gets into <see cref="Storage.IMetadataCollectionStore"/> when you save
///   it while refreshing a member, and is refreshed on its own in the library
///   refresh or through <see cref="Services.IMetadataRefreshService.RefreshEntry"/>.
///   The core purges a collection none of whose members is linked any more.
/// </remarks>
public interface IMetadataCollectionProvider : IMetadataProvider
{
    /// <summary>
    ///   Refresh a stored collection from your source and write it into the
    ///   stores.
    /// </summary>
    /// <remarks>
    ///   Save the collection and its members through
    ///   <see cref="Storage.IMetadataCollectionStore.SaveCollection"/>. The core
    ///   holds the collection's lock and has already decided it is due. Throw
    ///   on failure: the core logs it and the queue retries the job.
    /// </remarks>
    /// <param name="collectionID">
    ///   The collection: on your source and of the <c>collection</c> kind.
    /// </param>
    /// <param name="options">What kind of refresh this is, and why.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the collection is written.</returns>
    Task RefreshCollection(MetadataGuid collectionID, MetadataRefreshOptions options, CancellationToken cancellationToken = default);
}
