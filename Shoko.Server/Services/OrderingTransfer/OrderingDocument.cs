using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Services.OrderingTransfer;

/// <summary>
///   The document an ordering export writes: its format, its version, when
///   and by what it was written, and the orderings, of any number of series.
/// </summary>
internal sealed class OrderingDocument
{
    /// <summary>
    ///   The value of <see cref="Format"/> every export writes.
    /// </summary>
    public const string FormatName = "shoko-orderings";

    /// <summary>
    ///   The version of the format this server writes, and the newest it
    ///   reads.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    ///   Names the format, always <see cref="FormatName"/>; <c>null</c> when
    ///   a file read has none, which is refused.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    ///   The version of the format; <c>null</c> when a file read has none,
    ///   which is refused.
    /// </summary>
    public int? Version { get; set; }

    /// <summary>
    ///   When the export was written, in UTC.
    /// </summary>
    public DateTime ExportedAt { get; set; }

    /// <summary>
    ///   The server that wrote it.
    /// </summary>
    public OrderingDocumentServer? Server { get; set; }

    /// <summary>
    ///   The orderings, side by side whatever series they are for.
    /// </summary>
    public List<OrderingDocumentOrdering> Orderings { get; set; } = [];
}

/// <summary>
///   The server that wrote an export.
/// </summary>
internal sealed class OrderingDocumentServer
{
    /// <summary>
    ///   Its version.
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>
///   One ordering of an export.
/// </summary>
internal sealed class OrderingDocumentOrdering
{
    /// <summary>
    ///   The series it orders.
    /// </summary>
    public OrderingDocumentSeries? Series { get; set; }

    /// <summary>
    ///   Its ID on the server that wrote it, for the people reading the file.
    /// </summary>
    public string? Origin { get; set; }

    /// <summary>
    ///   Its name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   What it is about, if anything.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   What it followed where it was written. An import makes a user's
    ///   ordering whatever this is.
    /// </summary>
    public OrderingType Type { get; set; }

    /// <summary>
    ///   Whether it was its series' chosen ordering, or <c>null</c> when the
    ///   export did not record the choice.
    /// </summary>
    public bool? IsPreferred { get; set; }

    /// <summary>
    ///   The full IDs of the networks it follows, in order, or <c>null</c> in
    ///   a file written before exports carried them.
    /// </summary>
    public List<string>? Networks { get; set; }

    /// <summary>
    ///   Its own images.
    /// </summary>
    public List<OrderingDocumentImage> Images { get; set; } = [];

    /// <summary>
    ///   Its groups, in viewing order.
    /// </summary>
    public List<OrderingDocumentGroup> Groups { get; set; } = [];
}

/// <summary>
///   The series an ordering of an export orders, by IDs that hold across
///   servers.
/// </summary>
internal sealed class OrderingDocumentSeries
{
    /// <summary>
    ///   The AniDB anime.
    /// </summary>
    public int AnidbAnimeId { get; set; }

    /// <summary>
    ///   Its main title, for the people reading the file.
    /// </summary>
    public string? Title { get; set; }
}

/// <summary>
///   One group of an ordering of an export.
/// </summary>
internal sealed class OrderingDocumentGroup
{
    /// <summary>
    ///   Its name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   What it is about, if anything.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   Whether it holds the ordering's specials.
    /// </summary>
    public bool IsSpecial { get; set; }

    /// <summary>
    ///   Its episodes, in order.
    /// </summary>
    public List<OrderingDocumentEpisode> Episodes { get; set; } = [];

    /// <summary>
    ///   Its own images.
    /// </summary>
    public List<OrderingDocumentImage> Images { get; set; } = [];
}

/// <summary>
///   One episode of a group of an export: its AniDB episode and anime, and its
///   type and number in that anime to fall back on when the episode's ID is
///   not known.
/// </summary>
internal sealed class OrderingDocumentEpisode
{
    /// <summary>
    ///   The AniDB anime the episode is of, which may not be the ordering's
    ///   when it came from another source's ordering. The type and number are
    ///   only looked up in the ordering's anime, so an episode of another
    ///   anime is never taken for one of it.
    /// </summary>
    public int? AnidbAnimeId { get; set; }

    /// <summary>
    ///   The AniDB episode.
    /// </summary>
    public int? AnidbEpisodeId { get; set; }

    /// <summary>
    ///   Its type in the anime.
    /// </summary>
    public EpisodeType? Type { get; set; }

    /// <summary>
    ///   Its number among the anime's episodes of its type.
    /// </summary>
    public int? Number { get; set; }
}

/// <summary>
///   One image of an ordering or group of an export: how it is used, what it
///   is, where it can be fetched and, optionally, the file itself.
/// </summary>
internal sealed class OrderingDocumentImage
{
    /// <summary>
    ///   What it is for the entry, such as its poster.
    /// </summary>
    public ImageEntityType ImageType { get; set; }

    /// <summary>
    ///   Whether it is the entry's preferred image of its type.
    /// </summary>
    public bool IsPreferred { get; set; }

    /// <summary>
    ///   Its language code, if it has one.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    ///   Its width in pixels, if known.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    ///   Its height in pixels, if known.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    ///   The remote source it came from, as the source's value, if any.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///   Its resource ID at the remote source, which completes the source's
    ///   template URL.
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    ///   Its full remote URL, when known.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    ///   The SHA-256 of the file, in lowercase hex, when the file was read.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    ///   Its media type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    ///   The path of the file in a zip archive, <c>images/&lt;sha256&gt;.&lt;ext&gt;</c>.
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    ///   The file inline, as base64, in a plain JSON export.
    /// </summary>
    public string? Data { get; set; }
}
