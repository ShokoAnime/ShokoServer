using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Models.AniDB;

/// <summary>
///   One external entity of an AniDB resource, on an anime or on one of its
///   episodes, with its values kept as AniDB sent them.
/// </summary>
public class AniDB_Resource
{
    /// <summary>
    ///   Local ID.
    /// </summary>
    public int AniDB_ResourceID { get; set; }

    /// <summary>
    ///   The AniDB anime the resource belongs to, directly or through one of
    ///   its episodes.
    /// </summary>
    public int AnimeID { get; set; }

    /// <summary>
    ///   The AniDB episode the resource belongs to, or <c>null</c> when it
    ///   belongs to the anime itself.
    /// </summary>
    public int? EpisodeID { get; set; }

    /// <summary>
    ///   The AniDB resource type, known to <see cref="ResourceLinkType"/> or
    ///   not.
    /// </summary>
    public ResourceLinkType ResourceType { get; set; }

    /// <summary>
    ///   The entity's position among its owner's resources, in the order
    ///   AniDB lists them, starting at <c>0</c>.
    /// </summary>
    public int Ordering { get; set; }

    /// <summary>
    ///   The entity's <c>identifier</c> values, in order.
    /// </summary>
    public List<string> Identifiers { get; set; } = [];

    /// <summary>
    ///   The entity's <c>url</c> values, in order.
    /// </summary>
    public List<string> Urls { get; set; } = [];

    /// <summary>
    ///   Whether another row holds the same entity at the same place.
    /// </summary>
    /// <param name="other">The row to compare with.</param>
    /// <returns><c>true</c> when the owner, type, position and values match.</returns>
    public bool IsSameAs(AniDB_Resource other)
        => AnimeID == other.AnimeID &&
            EpisodeID == other.EpisodeID &&
            ResourceType == other.ResourceType &&
            Ordering == other.Ordering &&
            Identifiers.SequenceEqual(other.Identifiers) &&
            Urls.SequenceEqual(other.Urls);
}
