using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

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
