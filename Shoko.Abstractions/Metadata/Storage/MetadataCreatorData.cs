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
