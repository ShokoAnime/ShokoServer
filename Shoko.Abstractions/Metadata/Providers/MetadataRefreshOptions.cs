using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   What kind of refresh is asked for: what to fetch, and why.
/// </summary>
/// <remarks>
///   Passed to <see cref="Services.IMetadataRefreshService"/> and handed on to
///   the provider for each entry refreshed. A switch left
///   <see langword="null"/> means "as the provider's settings say". There is
///   no force flag: the core only asks for an entry that is due or forced.
///   <see cref="LastRefreshedAt"/> and <see cref="AnidbAnimeID"/> are set by
///   the core, replacing what a caller put there.
/// </remarks>
public sealed record MetadataRefreshOptions
{
    /// <summary>
    ///   Whether the entry's images should be linked and downloaded after the
    ///   refresh. The core queues its image job only when this is set and the
    ///   refresh is not a quick one.
    /// </summary>
    public bool DownloadImages { get; init; }

    /// <summary>
    ///   Whether to fetch the cast and crew, or <see langword="null"/> to go by
    ///   the settings.
    /// </summary>
    public bool? DownloadCrewAndCast { get; init; }

    /// <summary>
    ///   Whether to fetch a series' alternate orderings of its episodes, or
    ///   <see langword="null"/> to go by the settings.
    /// </summary>
    public bool? DownloadAlternateOrdering { get; init; }

    /// <summary>
    ///   Whether to fetch the networks a series aired on, or
    ///   <see langword="null"/> to go by the settings.
    /// </summary>
    public bool? DownloadNetworks { get; init; }

    /// <summary>
    ///   Whether to fetch the collections a film belongs to, or
    ///   <see langword="null"/> to go by the settings.
    /// </summary>
    public bool? DownloadCollections { get; init; }

    /// <summary>
    ///   Whether this is a quick refresh: bring the entry and its parts up to
    ///   date and leave out what is costly to fetch, such as every episode's
    ///   credits. The core queues no image job after a quick refresh, and
    ///   does not count it as a refresh, so the next one still runs in full.
    /// </summary>
    public bool QuickRefresh { get; init; }

    /// <summary>
    ///   Why the refresh was asked for. A forced refresh is never told it was
    ///   <see cref="MetadataRefreshReason.Scheduled"/>; the core makes it
    ///   <see cref="MetadataRefreshReason.Requested"/>.
    /// </summary>
    public MetadataRefreshReason Reason { get; init; } = MetadataRefreshReason.Scheduled;

    /// <summary>
    ///   When the entry was last refreshed without failing, or
    ///   <see langword="null"/> when this is its first refresh, such as for
    ///   an entry that was just linked, or when the refresh was forced, which
    ///   vouches for nothing fetched before. Filled in by the core.
    /// </summary>
    public DateTime? LastRefreshedAt { get; init; }

    /// <summary>
    ///   The AniDB anime whose linked entries are being refreshed, or
    ///   <see langword="null"/> when the entry was asked for on its own.
    ///   Filled in by the core.
    /// </summary>
    public int? AnidbAnimeID { get; init; }
}
