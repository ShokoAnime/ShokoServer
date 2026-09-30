using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.HTTP.GetAnime;

/// <summary>
///   One external entity of an AniDB resource, as the anime XML gives it.
/// </summary>
public class ResponseResource
{
    /// <summary>
    ///   The anime the resource belongs to.
    /// </summary>
    public int AnimeID { get; set; }

    /// <summary>
    ///   The episode the resource belongs to, or <c>null</c> for the anime
    ///   itself.
    /// </summary>
    public int? EpisodeID { get; set; }

    /// <summary>
    ///   The AniDB resource type, known to <see cref="ResourceLinkType"/> or
    ///   not.
    /// </summary>
    public ResourceLinkType ResourceType { get; set; }

    /// <summary>
    ///   The entity's position among its owner's resources, starting at
    ///   <c>0</c>.
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
}
