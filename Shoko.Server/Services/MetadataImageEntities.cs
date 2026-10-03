using System;
using System.Collections.Frozen;
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
    #region Kinds

    private static readonly FrozenSet<MetadataEntityType> _seriesKinds = FrozenSet.ToFrozenSet([
        MetadataEntityType.Series,
        MetadataEntityType.Season,
        MetadataEntityType.Episode,
        MetadataEntityType.Creator,
        MetadataEntityType.Character,
        MetadataEntityType.Studio,
        MetadataEntityType.Network,
    ]);

    private static readonly FrozenSet<MetadataEntityType> _movieKinds = FrozenSet.ToFrozenSet([
        MetadataEntityType.Movie,
        MetadataEntityType.Creator,
        MetadataEntityType.Character,
        MetadataEntityType.Studio,
    ]);

    private static readonly FrozenSet<MetadataEntityType> _collectionKinds = FrozenSet.ToFrozenSet([MetadataEntityType.Collection]);

    private static readonly FrozenDictionary<MetadataEntityType, FrozenSet<MetadataEntityType>> _entityKinds = MetadataEntityRefreshScheduler.EntityKinds
        .ToFrozenDictionary(kind => kind, kind => FrozenSet.ToFrozenSet([kind]));

    /// <summary>
    ///   The kinds of entities the walk of an entry of a kind can reach.
    /// </summary>
    /// <param name="entryKind">The kind of entry: series, movie, collection, creator, character, studio or network.</param>
    /// <returns>The kinds, or none for any other kind of entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryKind"/> is <see langword="null"/>.</exception>
    public static IReadOnlySet<MetadataEntityType> GetReachableKinds(MetadataEntityType entryKind)
    {
        ArgumentNullException.ThrowIfNull(entryKind);
        return entryKind == MetadataEntityType.Series ? _seriesKinds
            : entryKind == MetadataEntityType.Movie ? _movieKinds
            : entryKind == MetadataEntityType.Collection ? _collectionKinds
            : _entityKinds.GetValueOrDefault(entryKind) ?? FrozenSet<MetadataEntityType>.Empty;
    }

    #endregion

    #region Walk

    /// <summary>
    ///   The entities under an entry, on its source, with the people and
    ///   studios credited on them, and the entry's original language. A
    ///   creator, character, studio or network is walked alone.
    /// </summary>
    /// <param name="metadataService">Resolves the entry.</param>
    /// <param name="entry">The series, film, collection, creator, character, studio or network.</param>
    /// <returns>The entities, each once, or none when the entry is not stored.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static (List<IWithImages> Entities, string? OriginalLanguage) Gather(IMetadataService metadataService, MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(entry);
        var entities = new List<IWithImages>();
        string? originalLanguage = null;
        if (entry.EntityType == MetadataEntityType.Series && metadataService.GetSeries(entry) is { } series)
        {
            originalLanguage = series.OriginalLanguageCode;
            entities.Add(series);
            entities.AddRange(series.Seasons);
            entities.AddRange(series.Episodes);
        }
        else if (entry.EntityType == MetadataEntityType.Movie && metadataService.GetMovie(entry) is { } movie)
        {
            originalLanguage = movie.OriginalLanguageCode;
            entities.Add(movie);
        }
        else if (entry.EntityType == MetadataEntityType.Collection && metadataService.GetCollection(entry) is { } collection)
        {
            entities.Add(collection);
        }
        else if (MetadataEntityRefreshScheduler.EntityKinds.Contains(entry.EntityType))
        {
            return (metadataService.GetEntry(entry) is IWithImages entity && entity.ID.Source == entry.Source ? [entity] : [], null);
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
