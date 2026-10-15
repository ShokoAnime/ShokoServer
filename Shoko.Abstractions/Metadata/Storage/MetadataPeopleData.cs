using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A creator to store: a person or company credited on an entry.
/// </summary>
public sealed record MetadataCreatorData
{
    /// <summary>
    ///   The creator: its source, the <c>creator</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://creator/95</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The creator's titles, in order: its names in each language, its name
    ///   in its original script among them. They are stored under the
    ///   creator's source, whatever source each title names.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the creator's
    ///   default name. A synonym equal to the main or an official title but
    ///   for whitespace is dropped.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The language the creator is originally named in, as a language code,
    ///   when the source says. Its name in that language goes in
    ///   <see cref="Titles"/>.
    /// </summary>
    public string? OriginalLanguageCode { get; init; }

    /// <summary>
    ///   The country the creator comes from, as an ISO 3166 two-letter code,
    ///   when the source says or it is worked out from where they were born.
    /// </summary>
    public string? OriginalCountryCode { get; init; }

    /// <summary>
    ///   The creator's overviews, in order, stored like the titles. The first
    ///   is its default.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   Whether the creator is a person, a company or something else.
    /// </summary>
    public CreatorType Type { get; init; } = CreatorType.Person;

    /// <summary>
    ///   The creator's gender, when the source says.
    /// </summary>
    public PersonGender Gender { get; init; }

    /// <summary>
    ///   When the creator was born, when the source says. Any part of it
    ///   may be unknown, so it may be only a year, or only a month and a day.
    ///   A date that is not valid, such as the default one, is stored as none.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; init; }

    /// <summary>
    ///   When the creator died, when the source says, stored as
    ///   <see cref="BirthDay"/> is.
    /// </summary>
    public FuzzyDateOnly? DeathDay { get; init; }

    /// <summary>
    ///   Where the creator was born, as free text the way the source gives
    ///   it, such as <c>Tokyo, Japan</c>. A blank one is stored as none.
    /// </summary>
    public string? PlaceOfBirth { get; init; }

    /// <summary>
    ///   Whether the source marks the creator as known for adult content
    ///   only.
    /// </summary>
    public bool IsRestricted { get; init; }

    /// <summary>
    ///   Links to the creator elsewhere, such as a homepage or a wiki page.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the creator's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the creator has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}

/// <summary>
///   A character to store.
/// </summary>
public sealed record MetadataCharacterData
{
    /// <summary>
    ///   The character: its source, the <c>character</c> kind and the
    ///   source's own ID for it, e.g. <c>anilist://character/40</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The character's titles, in order: its names in each language, its name
    ///   in its original script among them. They are stored under the
    ///   character's source, whatever source each title names.
    /// </summary>
    /// <remarks>
    ///   The main title (<see cref="TitleType.Main"/>) is the character's
    ///   default name. A synonym equal to the main or an official title but
    ///   for whitespace is dropped.
    /// </remarks>
    public IReadOnlyList<ITitle> Titles { get; init; } = [];

    /// <summary>
    ///   The language the character is originally named in, as a language code,
    ///   when the source says. Its name in that language goes in
    ///   <see cref="Titles"/>.
    /// </summary>
    public string? OriginalLanguageCode { get; init; }

    /// <summary>
    ///   The country the character comes from, as an ISO 3166 two-letter code,
    ///   when the source says.
    /// </summary>
    public string? OriginalCountryCode { get; init; }

    /// <summary>
    ///   The character's overviews, in order, stored like the titles. The first
    ///   is its default.
    /// </summary>
    public IReadOnlyList<IText> Overviews { get; init; } = [];

    /// <summary>
    ///   Whether it is a character or an organization.
    /// </summary>
    public CharacterType Type { get; init; } = CharacterType.Character;

    /// <summary>
    ///   The character's gender, when the source says.
    /// </summary>
    public PersonGender Gender { get; init; }

    /// <summary>
    ///   The character's birthday, when the source says. Any part of it
    ///   may be unknown, so it may be only a year, or only a month and a day.
    ///   A date that is not valid, such as the default one, is stored as none.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; init; }

    /// <summary>
    ///   Links to the character elsewhere, such as a wiki page.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];

    /// <summary>
    ///   The source's resource ID of the character's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the character has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}

/// <summary>
///   One cast credit on an entry: a character, who voiced or played it, or
///   both. It is known by its character, its creator and its language. A
///   character or creator not stored yet is kept as a stub.
/// </summary>
public sealed record MetadataCastData
{
    /// <summary>
    ///   The character, when the credit has one.
    /// </summary>
    public MetadataGuid? CharacterID { get; init; }

    /// <summary>
    ///   The character's name, the main title of the stub the core makes when
    ///   the character is not stored yet. <see cref="Name"/> is used when unset.
    /// </summary>
    public string? CharacterName { get; init; }

    /// <summary>
    ///   The creator who voiced or played it, when known.
    /// </summary>
    public MetadataGuid? CreatorID { get; init; }

    /// <summary>
    ///   The creator's name, the main title of the stub the core makes when
    ///   the creator is not stored yet.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    ///   The name the role is credited under.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   How important the role is.
    /// </summary>
    public CastRoleType RoleType { get; init; }

    /// <summary>
    ///   The language of the performance, as a language code, for a voice
    ///   role recorded in more than one.
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    ///   Notes on the role, such as the age or form the character is played
    ///   in.
    /// </summary>
    public string? RoleNotes { get; init; }

    /// <summary>
    ///   The group that made the dub the performance is in, when the source
    ///   names one.
    /// </summary>
    public string? DubGroup { get; init; }
}

/// <summary>
///   One crew credit on an entry. It is known by its creator and its job. A
///   creator not stored yet is kept as a stub.
/// </summary>
public sealed record MetadataCrewData
{
    /// <summary>
    ///   The creator credited.
    /// </summary>
    public required MetadataGuid CreatorID { get; init; }

    /// <summary>
    ///   The creator's name, the main title of the stub the core makes when
    ///   the creator is not stored yet.
    /// </summary>
    public string? CreatorName { get; init; }

    /// <summary>
    ///   The job, as the source writes it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The kind of job, where the source's wording maps onto one.
    /// </summary>
    public CrewRoleType RoleType { get; init; }

    /// <summary>
    ///   The language the job was done in, as a language code, when it
    ///   matters, such as for a translation.
    /// </summary>
    public string? LanguageCode { get; init; }
}
