using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Models.Metadata;

/// <summary>
/// A person who worked on entries of any metadata source.
/// </summary>
public class MetadataCreator : MetadataEntry
{
    /// <summary>
    /// The person's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The person's name in their own language, if the source gives it.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    /// What kind of creator it is: a person, a company, and so on.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public CreatorType CreatorType { get; init; }

    /// <summary>
    /// The person's gender, if the source says.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public PersonGender Gender { get; init; }

    /// <summary>
    /// When the person was born, as far as the source knows.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; init; }

    /// <summary>
    /// When the person died, as far as the source knows.
    /// </summary>
    public FuzzyDateOnly? DeathDay { get; init; }

    /// <summary>
    /// Where the person was born, as free text the way the source gives it,
    /// such as "Tokyo, Japan", or <c>null</c> when it does not say.
    /// </summary>
    public string? PlaceOfBirth { get; init; }

    /// <summary>
    /// Whether the source marks the person as known for adult content only.
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Whether the person is a stub: only what a credit named, kept until its
    /// source is asked for the rest.
    /// </summary>
    [Required]
    public bool IsStub { get; init; }

    /// <summary>
    /// Other names the person is known by.
    /// </summary>
    [Required]
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    /// <summary>
    /// When the person was first stored locally, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the person was last updated locally, in UTC.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// The external resources, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Resource>? Resources { get; init; }
}

/// <summary>
/// A character in entries of any metadata source.
/// </summary>
public class MetadataCharacter : MetadataEntry
{
    /// <summary>
    /// The character's name.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The character's name in the original language, if the source gives
    /// it.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    /// What kind of character it is.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public CharacterType CharacterType { get; init; }

    /// <summary>
    /// The character's gender, if the source says.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public PersonGender Gender { get; init; }

    /// <summary>
    /// The character's birthday, as far as the source knows.
    /// </summary>
    public FuzzyDateOnly? BirthDay { get; init; }

    /// <summary>
    /// Whether the character is a stub: only what a credit named, kept until its
    /// source is asked for the rest.
    /// </summary>
    [Required]
    public bool IsStub { get; init; }

    /// <summary>
    /// Other names the character is known by.
    /// </summary>
    [Required]
    public IReadOnlyList<string> AlternativeNames { get; init; } = [];

    /// <summary>
    /// When the character was first stored locally, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the character was last updated locally, in UTC.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    /// <summary>
    /// The external resources, when asked for.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Resource>? Resources { get; init; }
}

/// <summary>
/// One credit on an entry of any metadata source: a character and who voiced
/// or played them, or a job somebody did.
/// </summary>
public class MetadataRole
{
    /// <summary>
    /// Whether the credit is for the cast or the crew.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public MetadataRoleKind Kind { get; init; }

    /// <summary>
    /// The kind of role, as the cast or crew role type names it.
    /// </summary>
    [Required]
    public string RoleType { get; init; } = string.Empty;

    /// <summary>
    /// The role's name: the character played, or the job done, in English.
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The role's name in the original language, if the source gives it.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? OriginalName { get; init; }

    /// <summary>
    /// Notes on a cast role, such as the age or form the character is played
    /// in, if the source gives them.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? RoleDetails { get; init; }

    /// <summary>
    /// The group that made the dub a performance is in, if the source names
    /// one.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DubGroup { get; init; }

    /// <summary>
    /// The language of the performance or work, as a code, if known.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// The source's ID of the entry the credit is on.
    /// </summary>
    [Required]
    public string ParentID { get; init; } = string.Empty;

    /// <summary>
    /// What kind of entry the credit is on.
    /// </summary>
    [Required]
    public MetadataEntityType ParentType { get; init; } = null!;

    /// <summary>
    /// Who is credited, if anybody is.
    /// </summary>
    public MetadataPersonReference? Creator { get; init; }

    /// <summary>
    /// The character played, for a cast credit.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public MetadataPersonReference? Character { get; init; }
}

/// <summary>
/// Whether a credit is for the cast or the crew.
/// </summary>
public enum MetadataRoleKind
{
    /// <summary>
    /// A character, and who voiced or played them.
    /// </summary>
    Cast,

    /// <summary>
    /// A job somebody did.
    /// </summary>
    Crew,
}

/// <summary>
/// A creator or character as a credit names them.
/// </summary>
public class MetadataPersonReference
{
    /// <summary>
    /// The source's own ID for the creator or character.
    /// </summary>
    [Required]
    public string ID { get; init; } = string.Empty;

    /// <summary>
    /// The name, if the creator or character is stored.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The name in the original language, if the source gives it.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    /// The creator's or character's image, if there is one.
    /// </summary>
    public Image? Image { get; init; }
}
