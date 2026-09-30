using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every plugin source's characters, creators and credits in the
///   store's own tables, cached in memory. Alternative names are kept as
///   titles in the text table.
/// </summary>
/// <param name="creatorRepository">The creators.</param>
/// <param name="characterRepository">The characters.</param>
/// <param name="castRepository">The cast credits.</param>
/// <param name="crewRepository">The crew credits.</param>
/// <param name="textStore">Keeps the alternative names.</param>
/// <param name="writer">Writes the changes.</param>
public class MetadataPeopleStore(
    Metadata_CreatorRepository creatorRepository,
    Metadata_CharacterRepository characterRepository,
    Metadata_CastRepository castRepository,
    Metadata_CrewRepository crewRepository,
    MetadataTextStore textStore,
    MetadataRowWriter writer
) : IMetadataPeopleStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public ICreator? GetCreator(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Creator ? creatorRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public ICharacter? GetCharacter(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Character ? characterRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ICast> GetCast(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return castRepository.GetByEntry(entry);
    }

    /// <inheritdoc />
    public IReadOnlyList<ICrew> GetCrew(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return crewRepository.GetByEntry(entry);
    }

    #endregion

    #region Writing

    /// <inheritdoc />
    public void SaveCreators(IEnumerable<MetadataCreatorData> creators)
    {
        ArgumentNullException.ThrowIfNull(creators);

        var items = creators.ToList();
        lock (_writeLock)
        {
            var now = DateTime.Now;
            var saving = MetadataRows.Upsert(creatorRepository, items, MetadataEntityType.Creator, creator => creator.ID, (row, creator) =>
            {
                row.Source = creator.ID.Source;
                row.ProviderID = creator.ID.ID;
                row.Name = creator.Name ?? string.Empty;
                row.OriginalName = creator.OriginalName;
                row.Description = creator.Overview;
                row.Type = creator.Type;
                row.Gender = creator.Gender;
                row.BirthDay = CheckDate(creator.BirthDay);
                row.DeathDay = CheckDate(creator.DeathDay);
                row.Resources = MetadataEntries.CheckResources(creator.Resources, nameof(creators));
                row.LastUpdatedAt = now;
                row.LastOrphanedAt = IsCredited(row) ? null : now;
            });
            textStore.WriteWithTitles(
                Names(items, creator => creator.ID, creator => creator.AlternativeNames, nameof(creators)),
                new MetadataRowChanges<Metadata_Creator>(creatorRepository, saving, [])
            );
        }
    }

    /// <inheritdoc />
    public void SaveCharacters(IEnumerable<MetadataCharacterData> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        var items = characters.ToList();
        lock (_writeLock)
        {
            var now = DateTime.Now;
            var saving = MetadataRows.Upsert(characterRepository, items, MetadataEntityType.Character, character => character.ID, (row, character) =>
            {
                row.Source = character.ID.Source;
                row.ProviderID = character.ID.ID;
                row.Name = character.Name ?? string.Empty;
                row.OriginalName = character.OriginalName;
                row.Description = character.Overview;
                row.Type = character.Type;
                row.Gender = character.Gender;
                row.BirthDay = CheckDate(character.BirthDay);
                row.Resources = MetadataEntries.CheckResources(character.Resources, nameof(characters));
                row.LastUpdatedAt = now;
                row.LastOrphanedAt = IsCredited(row) ? null : now;
            });
            textStore.WriteWithTitles(
                Names(items, character => character.ID, character => character.AlternativeNames, nameof(characters)),
                new MetadataRowChanges<Metadata_Character>(characterRepository, saving, [])
            );
        }
    }

    /// <inheritdoc />
    public int SetCast(MetadataGuid entry, IEnumerable<MetadataCastData> cast)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(cast);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = cast.ToList();
        lock (_writeLock)
        {
            // Every name is looked up before anything is written, so a credit
            // naming someone missing leaves the entry as it was.
            var credits = new List<(MetadataCastData Item, int? CreatorID, int? CharacterID, string? LanguageCode)>(items.Count);
            foreach (var item in items)
            {
                ArgumentNullException.ThrowIfNull(item, nameof(cast));
                credits.Add((
                    item,
                    FindCreator(entry.Source, item.CreatorID, nameof(cast)),
                    FindCharacter(entry.Source, item.CharacterID, nameof(cast)),
                    MetadataEntries.CheckLanguageCode(item.LanguageCode, nameof(cast))
                ));
            }

            var (saving, deleting) = MetadataRows.Replace(
                castRepository.GetByEntry(entry),
                credits,
                row => (row.CharacterID, row.CreatorID, row.LanguageCode),
                credit => (credit.CharacterID, credit.CreatorID, credit.LanguageCode),
                (row, credit, position) =>
                {
                    MetadataRows.Place(row, entry, position);
                    row.CreatorID = credit.CreatorID;
                    row.CharacterID = credit.CharacterID;
                    row.Name = credit.Item.Name ?? string.Empty;
                    row.RoleType = credit.Item.RoleType;
                    row.LanguageCode = credit.LanguageCode;
                    row.RoleNotes = string.IsNullOrWhiteSpace(credit.Item.RoleNotes) ? null : credit.Item.RoleNotes;
                    row.DubGroup = string.IsNullOrWhiteSpace(credit.Item.DubGroup) ? null : credit.Item.DubGroup;
                },
                (stored, row) =>
                    stored.Ordering == row.Ordering &&
                    stored.Name == row.Name &&
                    stored.RoleType == row.RoleType &&
                    stored.RoleNotes == row.RoleNotes &&
                    stored.DubGroup == row.DubGroup
            );
            writer.Write(new MetadataRowChanges<Metadata_Cast>(castRepository, saving, deleting));
            Restamp(
                [.. saving.Concat(deleting).Select(row => row.CreatorID).OfType<int>()],
                [.. saving.Concat(deleting).Select(row => row.CharacterID).OfType<int>()]
            );
            return saving.Count + deleting.Count;
        }
    }

    /// <inheritdoc />
    public int SetCrew(MetadataGuid entry, IEnumerable<MetadataCrewData> crew)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(crew);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        var items = crew.ToList();
        lock (_writeLock)
        {
            // Every name is looked up before anything is written, so a credit
            // naming someone missing leaves the entry as it was.
            var credits = new List<(MetadataCrewData Item, int CreatorID, string? LanguageCode)>(items.Count);
            foreach (var item in items)
            {
                ArgumentNullException.ThrowIfNull(item, nameof(crew));
                credits.Add((
                    item,
                    FindCreator(entry.Source, item.CreatorID, nameof(crew))
                        ?? throw new ArgumentException("A crew credit must name its creator.", nameof(crew)),
                    MetadataEntries.CheckLanguageCode(item.LanguageCode, nameof(crew))
                ));
            }

            var (saving, deleting) = MetadataRows.Replace(
                crewRepository.GetByEntry(entry),
                credits,
                row => (row.CreatorID, row.Name),
                credit => (credit.CreatorID, credit.Item.Name ?? string.Empty),
                (row, credit, position) =>
                {
                    MetadataRows.Place(row, entry, position);
                    row.CreatorID = credit.CreatorID;
                    row.Name = credit.Item.Name ?? string.Empty;
                    row.RoleType = credit.Item.RoleType;
                    row.LanguageCode = credit.LanguageCode;
                },
                (stored, row) =>
                    stored.Ordering == row.Ordering &&
                    stored.RoleType == row.RoleType &&
                    stored.LanguageCode == row.LanguageCode
            );
            writer.Write(new MetadataRowChanges<Metadata_Crew>(crewRepository, saving, deleting));
            Restamp([.. saving.Concat(deleting).Select(row => row.CreatorID)], []);
            return saving.Count + deleting.Count;
        }
    }

    /// <inheritdoc />
    public int RemoveCast(MetadataGuid entry)
        => SetCast(entry, []);

    /// <inheritdoc />
    public int RemoveCrew(MetadataGuid entry)
        => SetCrew(entry, []);

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> RemoveOrphaned(MetadataSource source, DateTime orphanedBefore)
    {
        ArgumentNullException.ThrowIfNull(source);
        MetadataEntries.CheckWritableSource(source, nameof(source));

        lock (_writeLock)
        {
            var now = DateTime.Now;
            var creatorsStamping = new List<Metadata_Creator>();
            var creatorsDeleting = new List<Metadata_Creator>();
            foreach (var creator in creatorRepository.GetBySource(source).Where(creator => !IsCredited(creator)))
            {
                if (creator.LastOrphanedAt is not { } orphanedAt)
                    creatorsStamping.Add(Stamped(creator, now));
                else if (orphanedAt < orphanedBefore)
                    creatorsDeleting.Add(creator);
            }

            var charactersStamping = new List<Metadata_Character>();
            var charactersDeleting = new List<Metadata_Character>();
            foreach (var character in characterRepository.GetBySource(source).Where(character => !IsCredited(character)))
            {
                if (character.LastOrphanedAt is not { } orphanedAt)
                    charactersStamping.Add(Stamped(character, now));
                else if (orphanedAt < orphanedBefore)
                    charactersDeleting.Add(character);
            }

            List<MetadataGuid> removed =
            [
                .. creatorsDeleting.Select(creator => ((IMetadata)creator).ID),
                .. charactersDeleting.Select(character => ((IMetadata)character).ID),
            ];
            textStore.WriteWithoutEntries(
                removed,
                new MetadataRowChanges<Metadata_Creator>(creatorRepository, creatorsStamping, creatorsDeleting),
                new MetadataRowChanges<Metadata_Character>(characterRepository, charactersStamping, charactersDeleting)
            );
            return removed;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Keeps a date only when it is valid, so a date with no parts, such as
    ///   the default one, is stored as none, as the database would read it.
    /// </summary>
    /// <param name="date">The date given.</param>
    /// <returns>The date, or <c>null</c> when it is missing or not valid.</returns>
    private static FuzzyDateOnly? CheckDate(FuzzyDateOnly? date)
        => date is { } value && FuzzyDateOnly.IsValid(value.Year, value.Month, value.Day) ? value : null;

    /// <summary>
    ///   Stamps the creators and characters whose credits a write changed:
    ///   one left with no credit is orphaned from now, and one credited again
    ///   is not orphaned any more. Called under the write lock, once the
    ///   credits are written.
    /// </summary>
    /// <param name="creatorIDs">The store's IDs of the creators whose credits changed.</param>
    /// <param name="characterIDs">The store's IDs of the characters whose credits changed.</param>
    private void Restamp(IReadOnlyCollection<int> creatorIDs, IReadOnlyCollection<int> characterIDs)
    {
        var now = DateTime.Now;
        var creators = creatorIDs.Distinct()
            .Select(creatorRepository.GetByID)
            .OfType<Metadata_Creator>()
            .Where(creator => IsCredited(creator) == creator.LastOrphanedAt.HasValue)
            .Select(creator => Stamped(creator, IsCredited(creator) ? null : now))
            .ToList();
        var characters = characterIDs.Distinct()
            .Select(characterRepository.GetByID)
            .OfType<Metadata_Character>()
            .Where(character => IsCredited(character) == character.LastOrphanedAt.HasValue)
            .Select(character => Stamped(character, IsCredited(character) ? null : now))
            .ToList();
        writer.Write(
            new MetadataRowChanges<Metadata_Creator>(creatorRepository, creators, []),
            new MetadataRowChanges<Metadata_Character>(characterRepository, characters, [])
        );
    }

    /// <summary>
    ///   Whether anything credits a stored creator, in its cast or its crew.
    /// </summary>
    /// <param name="creator">The creator; a new one has no credits.</param>
    /// <returns><see langword="true"/> when something does.</returns>
    private bool IsCredited(Metadata_Creator creator)
        => creator.Metadata_CreatorID is not 0 && (
            castRepository.GetByCreatorID(creator.Metadata_CreatorID).Count > 0 ||
            crewRepository.GetByCreatorID(creator.Metadata_CreatorID).Count > 0
        );

    /// <summary>
    ///   Whether anything credits a stored character in its cast.
    /// </summary>
    /// <param name="character">The character; a new one has no credits.</param>
    /// <returns><see langword="true"/> when something does.</returns>
    private bool IsCredited(Metadata_Character character)
        => character.Metadata_CharacterID is not 0 && castRepository.GetByCharacterID(character.Metadata_CharacterID).Count > 0;

    /// <summary>
    ///   A copy of a stored creator with its orphan stamp set or cleared.
    /// </summary>
    /// <param name="creator">The stored creator.</param>
    /// <param name="orphanedAt">The stamp, or <c>null</c> to clear it.</param>
    /// <returns>The copy.</returns>
    private static Metadata_Creator Stamped(Metadata_Creator creator, DateTime? orphanedAt)
    {
        var copy = MetadataRows.Copy(creator)!;
        copy.LastOrphanedAt = orphanedAt;
        return copy;
    }

    /// <summary>
    ///   A copy of a stored character with its orphan stamp set or cleared.
    /// </summary>
    /// <param name="character">The stored character.</param>
    /// <param name="orphanedAt">The stamp, or <c>null</c> to clear it.</param>
    /// <returns>The copy.</returns>
    private static Metadata_Character Stamped(Metadata_Character character, DateTime? orphanedAt)
    {
        var copy = MetadataRows.Copy(character)!;
        copy.LastOrphanedAt = orphanedAt;
        return copy;
    }

    /// <summary>
    ///   The alternative names of the people being saved, as the titles they
    ///   are stored as. When a person comes twice, the last copy wins, as it
    ///   does for the person itself.
    /// </summary>
    /// <typeparam name="TItem">The kind of person.</typeparam>
    /// <param name="items">The people, already checked.</param>
    /// <param name="idOf">The identifier of a person.</param>
    /// <param name="namesOf">The alternative names of a person.</param>
    /// <param name="paramName">The argument the people came in through.</param>
    /// <returns>Each person and its names, as titles.</returns>
    /// <exception cref="ArgumentNullException">A name, or its value, is <c>null</c>.</exception>
    private static List<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles)> Names<TItem>(
        IReadOnlyList<TItem> items,
        Func<TItem, MetadataGuid> idOf,
        Func<TItem, IReadOnlyList<MetadataNameData>?> namesOf,
        string paramName
    )
    {
        var latest = new Dictionary<MetadataGuid, IReadOnlyList<MetadataNameData>>();
        foreach (var item in items)
            latest[idOf(item)] = namesOf(item) ?? [];

        return [.. latest.Select(pair => (pair.Key, (IReadOnlyList<ITitle>)[.. pair.Value.Select(name => ToTitle(pair.Key.Source, name, paramName))]))];
    }

    /// <summary>
    ///   An alternative name as the title it is stored as.
    /// </summary>
    /// <param name="source">The person's source.</param>
    /// <param name="name">The name.</param>
    /// <param name="paramName">The argument the name came in through.</param>
    /// <returns>A synonym title, under <c>unk</c> when the name has no language.</returns>
    /// <exception cref="ArgumentNullException">The name, or its value, is <c>null</c>.</exception>
    private static TitleStub ToTitle(MetadataSource source, MetadataNameData name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(name, paramName);
        ArgumentNullException.ThrowIfNull(name.Name, paramName);
        var languageCode = string.IsNullOrWhiteSpace(name.LanguageCode) ? "unk" : name.LanguageCode;
        return new()
        {
            Source = source,
            Language = languageCode.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown,
            LanguageCode = languageCode,
            Type = TitleType.Synonym,
            Value = name.Name,
        };
    }

    /// <summary>
    ///   The store's ID for a creator a credit names.
    /// </summary>
    /// <param name="source">The source the credit belongs to.</param>
    /// <param name="id">The creator, if the credit names one.</param>
    /// <param name="paramName">The argument the credit came in through.</param>
    /// <returns>The store's ID, or <c>null</c> when the credit names no creator.</returns>
    /// <exception cref="ArgumentException">The creator is on another source or is not stored.</exception>
    private int? FindCreator(MetadataSource source, MetadataGuid? id, string paramName)
    {
        if (id is null)
            return null;

        MetadataEntries.CheckReference(id, source, MetadataEntityType.Creator, paramName);
        return creatorRepository.GetByProviderID(id.Source, id.ID)?.Metadata_CreatorID
            ?? throw new ArgumentException($"The creator \"{id}\" is not stored.", paramName);
    }

    /// <summary>
    ///   The store's ID for a character a credit names.
    /// </summary>
    /// <param name="source">The source the credit belongs to.</param>
    /// <param name="id">The character, if the credit names one.</param>
    /// <param name="paramName">The argument the credit came in through.</param>
    /// <returns>The store's ID, or <c>null</c> when the credit names no character.</returns>
    /// <exception cref="ArgumentException">The character is on another source or is not stored.</exception>
    private int? FindCharacter(MetadataSource source, MetadataGuid? id, string paramName)
    {
        if (id is null)
            return null;

        MetadataEntries.CheckReference(id, source, MetadataEntityType.Character, paramName);
        return characterRepository.GetByProviderID(id.Source, id.ID)?.Metadata_CharacterID
            ?? throw new ArgumentException($"The character \"{id}\" is not stored.", paramName);
    }

    #endregion
}
