using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services;

/// <summary>
///   The entities an image job walks for an entry: the entry, what is under
///   it, and who is credited on them, all on the entry's source.
/// </summary>
public static class MetadataImageEntities
{
    #region Walk

    /// <summary>
    ///   The entities under an entry, on its source, with the people and
    ///   studios credited on them, and the entry's original language. Any
    ///   other kind of entry, a collection among them, is walked alone.
    /// </summary>
    /// <param name="metadataService">Resolves the entry.</param>
    /// <param name="entry">The entry of any kind.</param>
    /// <returns>The entities, each once, or none when the entry is not stored.</returns>
    /// <exception cref="ArgumentNullException">An argument is <c>null</c>.</exception>
    public static (List<IWithImages> Entities, string? OriginalLanguage) Gather(IMetadataService metadataService, MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(entry);
        var entities = new List<IWithImages>();
        string? originalLanguage = null;
        var root = metadataService.GetEntry(entry);
        if (root is IWithImages rootImages)
            entities.Add(rootImages);
        if (root is ISeries series)
        {
            originalLanguage = series.OriginalLanguageCode;
            entities.AddRange(series.Seasons);
            entities.AddRange(series.Episodes);
        }
        else if (root is IMovie movie)
        {
            originalLanguage = movie.OriginalLanguageCode;
        }

        // Each person, studio or network is resolved once, however often it
        // is credited, since resolving it may take a read of its own.
        var seen = new HashSet<MetadataGuid>();
        var credited = new List<IWithImages>();
        foreach (var entity in entities)
        {
            if (entity is IWithCastAndCrew credits)
            {
                foreach (var cast in credits.Cast ?? [])
                {
                    if (cast.CreatorID is { } creatorID && creatorID.Source == entry.Source && seen.Add(creatorID) && cast.Creator is { } creator)
                        credited.Add(creator);
                    if (cast.CharacterID is { } characterID && characterID.Source == entry.Source && seen.Add(characterID) && cast.Character is { } character)
                        credited.Add(character);
                }

                foreach (var crew in credits.Crew ?? [])
                    if (crew.CreatorID.Source == entry.Source && seen.Add(crew.CreatorID) && crew.Creator is { } creator)
                        credited.Add(creator);
            }

            if (entity is IWithStudios studios)
                foreach (var studio in studios.Studios ?? [])
                    if (studio.ID.Source == entry.Source && seen.Add(studio.ID))
                        credited.Add(studio);

            if (entity is ISeries withNetworks)
                foreach (var network in withNetworks.Networks ?? [])
                    if (network.ID.Source == entry.Source && seen.Add(network.ID))
                        credited.Add(network);
        }

        return (
            [.. entities.Concat(credited).Where(entity => entity.ID.Source == entry.Source).DistinctBy(entity => entity.ID)],
            originalLanguage
        );
    }

    #endregion
}
