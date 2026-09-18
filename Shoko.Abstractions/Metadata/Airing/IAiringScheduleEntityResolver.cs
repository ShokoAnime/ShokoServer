using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   A plugin-registered resolver for plugin-owned series, seasons and
///   episodes, playing the same role for airing schedules that
///   <c>IImageCrossReferenceResolver</c> plays for images. The core entities —
///   AniDB, TMDB, AniList and Shoko — need none.
/// </summary>
public interface IAiringScheduleEntityResolver
{
    /// <summary>
    ///   The name of the resolver, typically matching the plugin name.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Resolve an entity from its source, type and stringified identifier,
    ///   for enriching a stored schedule or airing and for range queries.
    /// </summary>
    /// <param name="source">
    ///   The source of the entity.
    /// </param>
    /// <param name="type">
    ///   The type of the entity.
    /// </param>
    /// <param name="id">
    ///   The stringified identifier of the entity, source- and type-specific.
    /// </param>
    /// <returns>
    ///   The resolved entity, or <c>null</c> if not found.
    /// </returns>
    IMetadata? GetEntity(DataSource source, DataEntityType type, string id);

    /// <summary>
    ///   Get this resolver's entities linked to a shoko entity, in link order.
    ///   They are added to the entities the service walks itself, which are
    ///   <see cref="IShokoSeries.LinkedSeries"/>,
    ///   <see cref="IShokoSeason.LinkedSeasons"/> and
    ///   <see cref="IShokoEpisode.LinkedEpisodes"/>.
    /// </summary>
    /// <param name="shokoEntity">
    ///   The shoko entity to get the linked entities for.
    /// </param>
    /// <returns>
    ///   The linked entities, in link order.
    /// </returns>
    IEnumerable<IMetadata> GetLinkedEntities(IMetadata shokoEntity);
}
