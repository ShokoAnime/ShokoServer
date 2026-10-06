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
    ///   studios, relations and suggestions through their own stores. As an
    ///   <see cref="IMetadataEntityProvider"/>, name the people, studios and
    ///   networks by ID and leave their refresh to the core. Only the core's
    ///   refresh job calls it, holding the series' lock once it is due, so
    ///   check no freshness. Throw on failure: the queue retries the job.
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

    /// <summary>
    ///   The address of an entry's own page on your source's site: a series,
    ///   season or episode of your source.
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
    ///   provider's wins over the movie provider's.
    /// </remarks>
    /// <example>
    ///   <c>"Shoko.Plugin.Example.assets.example-icon.svg"</c>, or the same
    ///   name as <see cref="Plugin.IPlugin.EmbeddedIconResourceName"/> to
    ///   reuse the plugin's icon.
    /// </example>
    string? EmbeddedIconResourceName { get => null; }
}
