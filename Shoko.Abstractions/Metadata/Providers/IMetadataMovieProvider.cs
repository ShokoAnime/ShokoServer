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
    ///   their own stores. Name its collection by ID in
    ///   <see cref="Storage.MetadataMovieData.CollectionID"/> and leave the
    ///   collection to the core, which fetches it through your source's
    ///   <see cref="IMetadataCollectionProvider"/> while its <c>collection</c>
    ///   kind is turned on. As an <see cref="IMetadataEntityProvider"/>, name
    ///   the people and studios by ID and leave their refresh to the core.
    ///   The core holds the film's lock and has already decided it is due.
    ///   Throw on failure: the core logs it and the queue retries the job.
    /// </remarks>
    /// <param name="movieID">
    ///   The film, as its link names it: on your source and of the
    ///   <c>movie</c> kind.
    /// </param>
    /// <param name="options">What kind of refresh this is, and why.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task that completes once the film is written.</returns>
    Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    ///   The address of an entry's own page on your source's site: a movie or
    ///   collection of your source.
    /// </summary>
    /// <remarks>
    ///   Called for every row of a list, so keep it cheap: build it from the
    ///   entry, and reach for the network or a database only when there is no
    ///   other way. A resolver taking the entry's source and kind is asked
    ///   instead of you. A thrown exception is logged and read as no page.
    /// </remarks>
    /// <param name="entry">
    ///   The entry, of your source. When nothing holds it, such as a search
    ///   hit not stored yet, it is only an <see cref="IMetadata"/> carrying
    ///   its ID.
    /// </param>
    /// <returns>
    ///   The absolute URL, or <c>null</c> when the entry has no
    ///   page.
    /// </returns>
    string? GetSiteUrl(IMetadata entry) => null;

    /// <summary>
    ///   The embedded resource of your source's icon, shown beside the
    ///   source's name. Must be an absolute resource name, including the
    ///   assembly name.
    /// </summary>
    /// <remarks>
    ///   Any format the image system takes is accepted, SVG staying sharpest;
    ///   make it square and readable at 16
    ///   pixels. It is extracted beside your plugin as
    ///   <c>&lt;source&gt;-icon.&lt;ext&gt;</c>, or
    ///   <c>&lt;dll&gt;.&lt;source&gt;-icon.&lt;ext&gt;</c> beside a lone dll,
    ///   and a file already there by that name is used instead. A source has
    ///   one icon: the series
    ///   provider's wins over yours when both declare one.
    /// </remarks>
    /// <example>
    ///   <c>"Shoko.Plugin.Example.assets.example-icon.svg"</c>, or the same
    ///   name as <see cref="Plugin.IPlugin.EmbeddedIconResourceName"/> to
    ///   reuse the plugin's icon.
    /// </example>
    string? EmbeddedIconResourceName { get => null; }
}
