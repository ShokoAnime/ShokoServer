using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   One way to order a series' episodes into groups, such as a DVD order,
///   named <c>&lt;source&gt;://ordering/&lt;id&gt;</c>. Every series has its
///   default ordering, made from its own seasons; the others are kept by the
///   core through <c>IMetadataOrderingService</c>. An ordering and each of
///   its groups can carry images of their own, linked through the image
///   manager like any other entry's.
/// </summary>
public interface IOrdering : IMetadata, IWithCreationDate, IWithUpdateDate, IWithPrimaryImage, IWithBackdropImage, IWithBannerImage, IWithLogoImage
{
    /// <summary>
    ///   The series the ordering orders, on any source. The ordering's own
    ///   source need not be the series' source.
    /// </summary>
    MetadataGuid SeriesID { get; }

    /// <summary>
    ///   The ordering's name, e.g. <c>DVD Order</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   What the ordering is about, or an empty string when nothing was said.
    /// </summary>
    string Overview { get; }

    /// <summary>
    ///   What the ordering follows. <see cref="OrderingType.Default"/> for the
    ///   default ordering, and <see cref="OrderingType.User"/> for one a user
    ///   made.
    /// </summary>
    OrderingType Type { get; }

    /// <summary>
    ///   Whether this is the series' default ordering, made from its seasons.
    /// </summary>
    bool IsDefault { get; }

    /// <summary>
    ///   Whether this is the ordering chosen for the series. The default
    ///   ordering is, until another one is chosen.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    ///   How many episodes the ordering holds, each counted once.
    /// </summary>
    int EpisodeCount { get; }

    /// <summary>
    ///   How many of <see cref="Episodes"/> a user hid.
    /// </summary>
    int HiddenEpisodeCount { get; }

    /// <summary>
    ///   How many groups the ordering has.
    /// </summary>
    int SeasonCount { get; }

    /// <summary>
    ///   The series the ordering orders.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    ///   The ordering's groups in viewing order, each read as a season. For
    ///   the default ordering these are the series' own seasons; for any
    ///   other their <see cref="ISeason.OrderingID"/> names this ordering.
    /// </summary>
    IReadOnlyList<ISeason> Seasons { get; }

    /// <summary>
    ///   The ordering's episodes in viewing order, each once, where it first
    ///   comes.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }
}
