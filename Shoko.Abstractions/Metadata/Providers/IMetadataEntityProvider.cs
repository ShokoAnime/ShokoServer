using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Refreshes the creators, characters, studios and networks of your source
///   one at a time, when the core asks.
/// </summary>
/// <remarks>
///   Optional. Your series and movie refreshes then always name these
///   entries by ID in their credits and links, with whatever names they
///   carry, and the core keeps a stub for each one not stored yet. For each
///   kind turned on for you, the core asks you to refresh each stub, and each
///   linked entry older than <see cref="EntityStaleAfter"/>, in a job of your
///   own, once however many entries share it. Keep no download switch of
///   your own for them: a kind turned off is the switch.
/// </remarks>
public interface IMetadataEntityProvider : IMetadataProvider
{
    /// <summary>
    ///   The kinds of entries you refresh on your source: any of
    ///   <c>creator</c>, <c>character</c>, <c>studio</c> and <c>network</c>.
    /// </summary>
    /// <remarks>
    ///   Read once, at registration, so return a constant. A pair on another
    ///   source, or of another kind, is dropped with a warning in the log.
    /// </remarks>
    MetadataEntityScope EntityScope { get; }

    /// <summary>
    ///   How long a refreshed entry stays fresh, or <c>null</c> to
    ///   refresh stubs only. Thirty days by default.
    /// </summary>
    /// <remarks>
    ///   Read whenever the core checks an entry, so keep it cheap. A stub you
    ///   did not find waits out <see cref="EntityMissRetryAfter"/> instead.
    /// </remarks>
    TimeSpan? EntityStaleAfter { get => TimeSpan.FromDays(30); }

    /// <summary>
    ///   How long a stub you did not find waits before it is asked for again,
    ///   or <c>null</c> to wait out <see cref="EntityStaleAfter"/>.
    ///   Seven days by default.
    /// </summary>
    /// <remarks>
    ///   A stub still a stub after a refresh counts as not found. Used even
    ///   when <see cref="EntityStaleAfter"/> is <c>null</c>.
    /// </remarks>
    TimeSpan? EntityMissRetryAfter { get => TimeSpan.FromDays(7); }

    /// <summary>
    ///   Fetch one entry from your source and write it into the stores.
    /// </summary>
    /// <remarks>
    ///   Save it through <see cref="Storage.IMetadataPeopleStore.SaveCreators"/>,
    ///   <see cref="Storage.IMetadataPeopleStore.SaveCharacters"/>,
    ///   <see cref="Storage.IMetadataStudioStore.SaveStudios"/> or
    ///   <see cref="Storage.IMetadataStudioStore.SaveNetworks"/>, which turns a
    ///   stub into a full entry. Only the core's job calls it, holding the
    ///   entry's lock once it is due. Throw on failure: the queue retries.
    /// </remarks>
    /// <param name="entityID">The entry: on your source and of a kind in <see cref="EntityScope"/>.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <c>true</c> when you found and wrote the entry, or
    ///   <c>false</c> when your source does not have it.
    /// </returns>
    Task<bool> RefreshEntity(MetadataGuid entityID, CancellationToken cancellationToken = default);

    /// <summary>
    ///   The page of one of your creators, characters, studios or networks on
    ///   your source's own site.
    /// </summary>
    /// <remarks>
    ///   Optional. Asked before your source's series and movie providers,
    ///   which are asked next when you answer nothing. A thrown exception is
    ///   logged and read as no page.
    /// </remarks>
    /// <param name="entry">
    ///   The entry, of your source and a kind in <see cref="EntityScope"/>.
    ///   When nothing holds it, it is only an <see cref="IMetadata"/> carrying
    ///   its ID.
    /// </param>
    /// <returns>
    ///   The absolute URL, or <c>null</c> when the entry has no
    ///   page.
    /// </returns>
    string? GetSiteUrl(IMetadata entry) => null;
}
