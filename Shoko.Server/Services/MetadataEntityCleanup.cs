using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Server.Services;

/// <summary>
///   Removes what the other stores hold for plugin entities that are leaving
///   the store, so nothing keyed on them is left where no walk reaches it.
/// </summary>
/// <remarks>
///   The series, movie and collection stores call <see cref="Remove"/> for
///   every series, season, episode, film and collection they remove, and the
///   purge job for an entry nothing stores any more. The people, studios and
///   networks those entities named stay: their store stamps each one that
///   lost its last use, and the purge of orphaned metadata removes it once
///   it has gone unused long enough, with <see cref="RemoveImageLinks(IEnumerable{MetadataGuid})"/>.
/// </remarks>
/// <param name="peopleStore">Holds the cast, crew and the people credited.</param>
/// <param name="tagStore">Holds the tags.</param>
/// <param name="studioStore">Holds the studios and networks.</param>
/// <param name="relationStore">Holds the relations.</param>
/// <param name="suggestionStore">Holds the suggestions.</param>
/// <param name="imageManager">Holds the image links.</param>
public class MetadataEntityCleanup(
    IMetadataPeopleStore peopleStore,
    IMetadataTagStore tagStore,
    IMetadataStudioStore studioStore,
    IMetadataRelationStore relationStore,
    IMetadataSuggestionStore suggestionStore,
    IImageManager imageManager
)
{
    #region Removal

    /// <summary>
    ///   Removes the image links, tags, studios, networks, cast, crew,
    ///   relations and suggestions of some entities. The creators,
    ///   characters, studios and networks they named stay.
    /// </summary>
    /// <param name="entities">The entities, all on plugin sources.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entities"/>, or one of them, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">One of them is on a source the core keeps itself.</exception>
    public void Remove(IEnumerable<MetadataGuid> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var id in entities.Distinct())
        {
            RemoveImageLinks(id);
            tagStore.RemoveTags(id);
            studioStore.RemoveStudios(id);
            studioStore.RemoveNetworks(id);
            peopleStore.RemoveCast(id);
            peopleStore.RemoveCrew(id);
            relationStore.RemoveRelations(id);
            suggestionStore.RemoveSuggestions(id);
        }
    }

    /// <summary>
    ///   Removes the image links of some entities, such as the people, studios
    ///   and networks a purge of orphaned metadata removed.
    /// </summary>
    /// <param name="entities">The entities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entities"/>, or one of them, is <c>null</c>.</exception>
    public void RemoveImageLinks(IEnumerable<MetadataGuid> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var id in entities.Distinct())
            RemoveImageLinks(id);
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Removes the image links of an entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    private void RemoveImageLinks(MetadataGuid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        foreach (var xref in imageManager.GetImageCrossReferencesForEntity(new Entity(entity), new() { LinkedEntityImages = false }))
            imageManager.RemoveImageCrossReference(xref);
    }

    /// <summary>
    ///   An entity named only by its identifier, which is all the image links
    ///   are found by.
    /// </summary>
    /// <param name="ID">The entity.</param>
    private sealed record Entity(MetadataGuid ID) : IWithImages;

    #endregion
}
