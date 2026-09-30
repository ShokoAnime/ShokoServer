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
    ///   The creator's name, as the source writes it for most readers.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The creator's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    ///   What the source says about the creator.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   Whether the creator is a person, a company or something else.
    /// </summary>
    public CreatorType Type { get; init; } = CreatorType.Person;

    /// <summary>
    ///   Other names the creator is known by. They are stored as its titles,
    ///   of the <see cref="TitleType.Synonym"/> type.
    /// </summary>
    public IReadOnlyList<MetadataNameData> AlternativeNames { get; init; } = [];

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
    ///   Links to the creator elsewhere, such as a homepage or a wiki page.
    /// </summary>
    public IReadOnlyList<Resource> Resources { get; init; } = [];
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
    ///   The character's name, as the source writes it for most readers.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The character's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    ///   What the source says about the character.
    /// </summary>
    public string? Overview { get; init; }

    /// <summary>
    ///   Whether it is a character or an organization.
    /// </summary>
    public CharacterType Type { get; init; } = CharacterType.Character;

    /// <summary>
    ///   Other names the character is known by. They are stored as its
    ///   titles, of the <see cref="TitleType.Synonym"/> type.
    /// </summary>
    public IReadOnlyList<MetadataNameData> AlternativeNames { get; init; } = [];

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
}

/// <summary>
///   Another name a creator or a character is known by.
/// </summary>
public sealed record MetadataNameData
{
    /// <summary>
    ///   The name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The language of the name, as a language code, when known. A name
    ///   without one is stored under <c>unk</c>.
    /// </summary>
    public string? LanguageCode { get; init; }
}

/// <summary>
///   One cast credit on an entry: a character, who voiced or played it, or
///   both. It is known by its character, its creator and its language.
/// </summary>
public sealed record MetadataCastData
{
    /// <summary>
    ///   The character, when the credit has one.
    /// </summary>
    public MetadataGuid? CharacterID { get; init; }

    /// <summary>
    ///   The creator who voiced or played it, when known.
    /// </summary>
    public MetadataGuid? CreatorID { get; init; }

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
///   One crew credit on an entry. It is known by its creator and its job.
/// </summary>
public sealed record MetadataCrewData
{
    /// <summary>
    ///   The creator credited.
    /// </summary>
    public required MetadataGuid CreatorID { get; init; }

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
