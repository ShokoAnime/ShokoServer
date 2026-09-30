using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Supplies movie-shaped metadata: a film, standing on its own.
/// </summary>
/// <remarks>
///   As <see cref="IMetadataSeriesProvider"/>, for the one level a film has.
///   The core's refresh job asks you to refresh each film linked to an anime
///   (to the whole anime or one of its episodes), and you write it into
///   <see cref="Storage.IMetadataMovieStore"/>.
/// </remarks>
public interface IMetadataMovieProvider : IMetadataProvider
{
    /// <summary>
    ///   Refresh a linked film from your source and write it into the stores.
    /// </summary>
    /// <remarks>
    ///   Save the film through <see cref="Storage.IMetadataMovieStore.SaveMovie"/>
    ///   and its credits, tags, studios, relations and suggestions through
    ///   their own stores; its collections are optional. The core holds the
    ///   film's lock and has already decided it is due. Throw on failure: the
    ///   core logs it and the queue retries the job.
    /// </remarks>
    /// <param name="movieID">
    ///   The film, as its link names it: on your source and of the
    ///   <c>movie</c> kind.
    /// </param>
    /// <param name="options">What kind of refresh this is, and why.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the film is written.</returns>
    Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default);
}
