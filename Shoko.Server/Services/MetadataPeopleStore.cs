using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Direct.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every plugin source's characters, creators and credits in the
///   store's own tables. The characters and creators are cached in memory,
///   while the credits are read from the database, as a source may have
///   millions. Alternative names are kept as titles in the text table.
/// </summary>
/// <param name="creatorRepository">The creators.</param>
/// <param name="characterRepository">The characters.</param>
/// <param name="castRepository">The cast credits.</param>
/// <param name="crewRepository">The crew credits.</param>
/// <param name="textStore">Keeps the alternative names.</param>
/// <param name="writer">Writes the changes.</param>
/// <param name="entityScheduler">Routes the refresh of the stub and stale people a write credits, if set.</param>
public class MetadataPeopleStore(
    Metadata_CreatorRepository creatorRepository,
    Metadata_CharacterRepository characterRepository,
    Metadata_CastRepository castRepository,
    Metadata_CrewRepository crewRepository,
    MetadataTextStore textStore,
    MetadataRowWriter writer,
    MetadataEntityRefreshScheduler? entityScheduler = null
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
                if (row.Metadata_CreatorID is 0)
                    row.CreatedAt = now;
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
                row.IsRestricted = creator.IsRestricted;
                var extra = (row.ExtraData ?? new()) with
                {
                    PlaceOfBirth = string.IsNullOrWhiteSpace(creator.PlaceOfBirth) ? null : creator.PlaceOfBirth.Trim(),
                };
                row.ExtraData = MetadataDefaultImages.Apply(extra, creator.DefaultImageResourceIDs).NullIfEmpty();
                row.LastUpdatedAt = now;
            });
            var credited = CreditedCreatorIDs(saving.Select(row => row.Metadata_CreatorID));
            foreach (var row in saving)
                row.LastOrphanedAt = credited.Contains(row.Metadata_CreatorID) ? null : now;
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
                if (row.Metadata_CharacterID is 0)
                    row.CreatedAt = now;
                row.Source = character.ID.Source;
                row.ProviderID = character.ID.ID;
                row.Name = character.Name ?? string.Empty;
                row.OriginalName = character.OriginalName;
                row.Description = character.Overview;
                row.Type = character.Type;
                row.Gender = character.Gender;
                row.BirthDay = CheckDate(character.BirthDay);
                row.Resources = MetadataEntries.CheckResources(character.Resources, nameof(characters));
                row.ExtraData = MetadataDefaultImages.Apply(row.ExtraData ?? new(), character.DefaultImageResourceIDs).NullIfEmpty();
                row.LastUpdatedAt = now;
            });
            var credited = castRepository.GetCreditedCharacterIDs(saving.Select(row => row.Metadata_CharacterID));
            foreach (var row in saving)
                row.LastOrphanedAt = credited.Contains(row.Metadata_CharacterID) ? null : now;
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

        // Every credit is checked before anything is written, so one that is
        // refused leaves the entry as it was.
        var items = cast.ToList();
        var languageCodes = new List<string?>(items.Count);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(cast));
            if (item.CreatorID is not null)
                MetadataEntries.CheckReference(item.CreatorID, entry.Source, MetadataEntityType.Creator, nameof(cast));
            if (item.CharacterID is not null)
                MetadataEntries.CheckReference(item.CharacterID, entry.Source, MetadataEntityType.Character, nameof(cast));
            languageCodes.Add(MetadataEntries.CheckLanguageCode(item.LanguageCode, nameof(cast)));
        }

        int changed;
        IReadOnlyList<IMetadataStubRow> credited;
        lock (_writeLock)
        {
            AddStubs(
                items.Where(item => item.CreatorID is not null).Select(item => (item.CreatorID!, item.CreatorName)),
                items.Where(item => item.CharacterID is not null).Select(item => (item.CharacterID!, (string?)(item.CharacterName ?? item.Name)))
            );
            var credits = new List<(MetadataCastData Item, int? CreatorID, int? CharacterID, string? LanguageCode)>(items.Count);
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                credits.Add((item, FindCreator(item.CreatorID), FindCharacter(item.CharacterID), languageCodes[index]));
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
            changed = saving.Count + deleting.Count;
            credited = Rows(credits.Select(credit => credit.CreatorID), credits.Select(credit => credit.CharacterID));
        }

        entityScheduler?.ScheduleDue(credited);
        return changed;
    }

    /// <inheritdoc />
    public int SetCrew(MetadataGuid entry, IEnumerable<MetadataCrewData> crew)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(crew);
        MetadataEntries.CheckWritableSource(entry.Source, nameof(entry));

        // Every credit is checked before anything is written, so one that is
        // refused leaves the entry as it was.
        var items = crew.ToList();
        var languageCodes = new List<string?>(items.Count);
        foreach (var item in items)
        {
            ArgumentNullException.ThrowIfNull(item, nameof(crew));
            if (item.CreatorID is null)
                throw new ArgumentException("A crew credit must name its creator.", nameof(crew));
            MetadataEntries.CheckReference(item.CreatorID, entry.Source, MetadataEntityType.Creator, nameof(crew));
            languageCodes.Add(MetadataEntries.CheckLanguageCode(item.LanguageCode, nameof(crew)));
        }

        int changed;
        IReadOnlyList<IMetadataStubRow> credited;
        lock (_writeLock)
        {
            AddStubs(items.Select(item => (item.CreatorID, item.CreatorName)), []);
            var credits = new List<(MetadataCrewData Item, int CreatorID, string? LanguageCode)>(items.Count);
            for (var index = 0; index < items.Count; index++)
                credits.Add((items[index], FindCreator(items[index].CreatorID)!.Value, languageCodes[index]));

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
            changed = saving.Count + deleting.Count;
            credited = Rows(credits.Select(credit => (int?)credit.CreatorID), []);
        }

        entityScheduler?.ScheduleDue(credited);
        return changed;
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
            var creators = creatorRepository.GetBySource(source);
            var creditedCreators = CreditedCreatorIDs(creators.Select(creator => creator.Metadata_CreatorID));
            foreach (var creator in creators.Where(creator => !creditedCreators.Contains(creator.Metadata_CreatorID)))
            {
                if (creator.LastOrphanedAt is not { } orphanedAt)
                    creatorsStamping.Add(Stamped(creator, now));
                else if (orphanedAt < orphanedBefore)
                    creatorsDeleting.Add(creator);
            }

            var charactersStamping = new List<Metadata_Character>();
            var charactersDeleting = new List<Metadata_Character>();
            var characters = characterRepository.GetBySource(source);
            var creditedCharacters = castRepository.GetCreditedCharacterIDs(characters.Select(character => character.Metadata_CharacterID));
            foreach (var character in characters.Where(character => !creditedCharacters.Contains(character.Metadata_CharacterID)))
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

    #region Refresh State

    /// <summary>
    ///   Stamps when the core last asked the source to refresh a stored
    ///   creator or character, on its own row, leaving the rest of it as it is.
    /// </summary>
    /// <param name="id">The creator or character.</param>
    /// <param name="refreshedAt">When the refresh finished, in local time.</param>
    /// <returns><c>true</c> if the creator or character is stored.</returns>
    internal bool SetLastRefreshedAt(MetadataGuid id, DateTime refreshedAt)
    {
        lock (_writeLock)
        {
            // A copy, so the cached row stays as it was until the write has committed.
            if (id.EntityType == MetadataEntityType.Creator && creatorRepository.GetByProviderID(id.Source, id.ID) is { } creator)
            {
                var row = MetadataRows.Copy(creator)!;
                row.LastRefreshedAt = refreshedAt;
                writer.Write(new MetadataRowChanges<Metadata_Creator>(creatorRepository, [row], []));
                return true;
            }

            if (id.EntityType == MetadataEntityType.Character && characterRepository.GetByProviderID(id.Source, id.ID) is { } character)
            {
                var row = MetadataRows.Copy(character)!;
                row.LastRefreshedAt = refreshedAt;
                writer.Write(new MetadataRowChanges<Metadata_Character>(characterRepository, [row], []));
                return true;
            }

            return false;
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
        var creditedCreators = CreditedCreatorIDs(creatorIDs);
        var creditedCharacters = castRepository.GetCreditedCharacterIDs(characterIDs);
        var creators = creatorIDs.Distinct()
            .Select(creatorRepository.GetByID)
            .OfType<Metadata_Creator>()
            .Where(creator => creditedCreators.Contains(creator.Metadata_CreatorID) == creator.LastOrphanedAt.HasValue)
            .Select(creator => Stamped(creator, creditedCreators.Contains(creator.Metadata_CreatorID) ? null : now))
            .ToList();
        var characters = characterIDs.Distinct()
            .Select(characterRepository.GetByID)
            .OfType<Metadata_Character>()
            .Where(character => creditedCharacters.Contains(character.Metadata_CharacterID) == character.LastOrphanedAt.HasValue)
            .Select(character => Stamped(character, creditedCharacters.Contains(character.Metadata_CharacterID) ? null : now))
            .ToList();
        writer.Write(
            new MetadataRowChanges<Metadata_Creator>(creatorRepository, creators, []),
            new MetadataRowChanges<Metadata_Character>(characterRepository, characters, [])
        );
    }

    /// <summary>
    ///   Which of some stored creators anything credits, in its cast or its
    ///   crew. A new creator, with no ID yet, has no credits.
    /// </summary>
    /// <param name="creatorIDs">The store's IDs for the creators.</param>
    /// <returns>The IDs of those credited.</returns>
    private HashSet<int> CreditedCreatorIDs(IEnumerable<int> creatorIDs)
    {
        var ids = creatorIDs.Where(id => id is not 0).Distinct().ToList();
        var credited = castRepository.GetCreditedCreatorIDs(ids).ToHashSet();
        credited.UnionWith(crewRepository.GetCreditedCreatorIDs(ids.Where(id => !credited.Contains(id))));
        return credited;
    }

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
    ///   Stores a stub for each creator and character a write credits that
    ///   is not stored yet, with the first name a credit gave it, and names a
    ///   stored stub that has none. Called under the write lock, once the
    ///   credits are checked.
    /// </summary>
    /// <remarks>
    ///   A stub is stamped as orphaned until its credit is written, so a write
    ///   that fails after leaves it to the purge.
    /// </remarks>
    /// <param name="creators">The creators credited, with the names the credits carried.</param>
    /// <param name="characters">The characters credited, with the names the credits carried.</param>
    private void AddStubs(IEnumerable<(MetadataGuid ID, string? Name)> creators, IEnumerable<(MetadataGuid ID, string? Name)> characters)
    {
        var now = DateTime.Now;
        var creatorNames = creators.ToList();
        var characterNames = characters.ToList();
        var creatorStubs = MetadataRows.Missing(creatorNames, id => creatorRepository.GetByProviderID(id.Source, id.ID) is null)
            .Select(pair => new Metadata_Creator
            {
                Source = pair.ID.Source,
                ProviderID = pair.ID.ID,
                Name = pair.Name,
                CreatedAt = now,
                LastOrphanedAt = now,
            })
            .Concat(MetadataRows.Named(creatorNames, id => creatorRepository.GetByProviderID(id.Source, id.ID)))
            .ToList();
        var characterStubs = MetadataRows.Missing(characterNames, id => characterRepository.GetByProviderID(id.Source, id.ID) is null)
            .Select(pair => new Metadata_Character
            {
                Source = pair.ID.Source,
                ProviderID = pair.ID.ID,
                Name = pair.Name,
                CreatedAt = now,
                LastOrphanedAt = now,
            })
            .Concat(MetadataRows.Named(characterNames, id => characterRepository.GetByProviderID(id.Source, id.ID)))
            .ToList();
        if (creatorStubs.Count + characterStubs.Count is 0)
            return;

        textStore.WriteWithoutEntries(
            [],
            new MetadataRowChanges<Metadata_Creator>(creatorRepository, creatorStubs, []),
            new MetadataRowChanges<Metadata_Character>(characterRepository, characterStubs, [])
        );
    }

    /// <summary>
    ///   The rows of the creators and characters a write credits.
    /// </summary>
    /// <param name="creatorIDs">The store's IDs of the creators.</param>
    /// <param name="characterIDs">The store's IDs of the characters.</param>
    /// <returns>The rows, each once.</returns>
    private List<IMetadataStubRow> Rows(IEnumerable<int?> creatorIDs, IEnumerable<int?> characterIDs)
        =>
        [
            .. creatorIDs.OfType<int>().Distinct().Select(creatorRepository.GetByID).OfType<Metadata_Creator>(),
            .. characterIDs.OfType<int>().Distinct().Select(characterRepository.GetByID).OfType<Metadata_Character>(),
        ];

    /// <summary>
    ///   The store's ID for a creator a credit names, once its stub is
    ///   stored.
    /// </summary>
    /// <param name="id">The creator, if the credit names one.</param>
    /// <returns>The store's ID, or <c>null</c> when the credit names no creator.</returns>
    private int? FindCreator(MetadataGuid? id)
        => id is null ? null : creatorRepository.GetByProviderID(id.Source, id.ID)!.Metadata_CreatorID;

    /// <summary>
    ///   The store's ID for a character a credit names, once its stub is
    ///   stored.
    /// </summary>
    /// <param name="id">The character, if the credit names one.</param>
    /// <returns>The store's ID, or <c>null</c> when the credit names no character.</returns>
    private int? FindCharacter(MetadataGuid? id)
        => id is null ? null : characterRepository.GetByProviderID(id.Source, id.ID)!.Metadata_CharacterID;

    #endregion
}
