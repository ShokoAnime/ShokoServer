using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Search;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A movie provider users can search, so they can link or correct by hand.
///   The links themselves are the core's.
/// </summary>
/// <remarks>
///   Optional. A film is claimed whole, so there is no level below it to
///   correct; episodes belong to <see cref="IMetadataSeriesLinkingProvider"/>.
/// </remarks>
public interface IMetadataMovieLinkingProvider : IMetadataMovieProvider
{
    /// <summary>
    ///   Search your source for films a user might link to.
    /// </summary>
    /// <remarks>
    ///   Hand back what the remote said and let the caller pick.
    /// </remarks>
    /// <param name="options">What to search for.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The page asked for, and how many results there are in total.
    /// </returns>
    Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSearchOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Look one film up by its ID, the way a search would have offered it.
    /// </summary>
    /// <remarks>
    ///   Optional. Answer from what you already hold when you can, and ask
    ///   your source otherwise; nothing is stored by looking. The default
    ///   answers nothing, which the core reports as not supported.
    /// </remarks>
    /// <param name="movieID">The film, on your source and of the <c>movie</c> kind.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   The film, or <c>null</c> when your source has none by
    ///   that ID.
    /// </returns>
    /// <exception cref="MetadataProviderUnavailableException">Your source cannot be reached for now.</exception>
    Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default)
        => Task.FromResult<MetadataMovieSearchResult?>(null);
}
