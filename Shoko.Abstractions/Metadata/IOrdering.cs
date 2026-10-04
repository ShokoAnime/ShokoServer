using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   One way to order a series' episodes into groups, such as a DVD order,
///   named <c>&lt;source&gt;://ordering/&lt;id&gt;</c>. Every series has its
///   default ordering, made from its own seasons; the others are kept by the
///   core through <c>IMetadataOrderingService</c>. An ordering and each of
///   its groups can carry images of their own, linked through the image
///   manager like any other entry's, and their titles and overviews come from
///   the text manager like any other entry's.
/// </summary>
public interface IOrdering : IMetadata, IWithTitles, IWithOverviews, IWithCreationDate, IWithUpdateDate, IWithPrimaryImage, IWithBackdropImage, IWithBannerImage, IWithLogoImage
{
    /// <summary>
    ///   The series the ordering orders, on any source. The ordering's own
    ///   source need not be the series' source.
    /// </summary>
    MetadataGuid SeriesID { get; }

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
    ///   The networks the ordering follows, such as the broadcaster whose
    ///   order it is. The default ordering's are the series' own
    ///   <see cref="ISeries.Networks"/>.
    /// </summary>
    IReadOnlyList<INetwork> Networks { get; }

    /// <summary>
    ///   The series the ordering orders, as it presents it: the series itself
    ///   for the default ordering, and for any other one with the ordering's
    ///   groups as its seasons and its episodes numbered here. Its
    ///   <see cref="ISeries.CurrentOrdering"/> is this ordering.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    ///   The ordering's groups in viewing order, each read as a season. For
    ///   the default ordering these are the series' own seasons; for any
    ///   other they are the ordering's own groups. Each one's
    ///   <see cref="ISeason.OrderingID"/> names this ordering.
    /// </summary>
    IReadOnlyList<ISeason> Seasons { get; }

    /// <summary>
    ///   The ordering's episodes in viewing order, each once: a placed
    ///   special (in the special group and a regular one) where it airs, and
    ///   any other episode where it first comes. For any ordering but the
    ///   default, each is numbered and typed by its first place here.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    #region Static Helpers

    /// <summary>
    ///   The ID of a series' default ordering: the series' own ID for a
    ///   source the core keeps, and <c>default/</c> and the series' ID for any
    ///   other source, or a hash of the ID when that would be too long.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The default ordering's ID, under the series' source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seriesID"/> is <c>null</c>.</exception>
    public static MetadataGuid DefaultOrderingID(MetadataGuid seriesID)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (seriesID.Source.IsCore)
            return new(seriesID.Source, MetadataEntityType.Ordering, seriesID.ID);

        var id = DefaultIDPrefix + seriesID.ID;
        if (id.Length > MetadataGuid.MaxIDLength)
            id = DefaultIDPrefix + "#" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(seriesID.ID)));
        return new(seriesID.Source, MetadataEntityType.Ordering, id);
    }

    /// <summary>
    ///   The start of a plugin source's default ordering IDs, which a global
    ///   ordering's ID may not start with.
    /// </summary>
    internal const string DefaultIDPrefix = "default/";

    #endregion
}

/// <summary>
///   An ordering with its series, groups and episodes typed. The groups are
///   the ordering's own, not the source's seasons, unless it is the default
///   ordering.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
public interface IOrdering<out TSeries, out TEpisode> : IOrdering
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   The series the ordering orders, as its source keeps it.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    new TSeries Series { get; }

    /// <summary>
    ///   The ordering's groups in viewing order, each read as a season.
    /// </summary>
    new IReadOnlyList<ISeason<TSeries, TEpisode>> Seasons { get; }

    /// <summary>
    ///   The ordering's episodes in viewing order, each once: a placed
    ///   special where it airs, and any other episode where it first comes,
    ///   each as its source keeps it.
    /// </summary>
    new IReadOnlyList<TEpisode> Episodes { get; }
}
