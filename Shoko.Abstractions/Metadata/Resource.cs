
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   An external resource/link associated with an entity. This could be an
///   official website, streaming page, info page, or a link to the
///   same entity in another metadata database.
/// </summary>
public sealed class Resource
{
    /// <summary>
    ///   The category of the resource.
    /// </summary>
    public required ResourceType Type { get; init; }

    /// <summary>
    ///   The display name of the resource (e.g. the site or service
    ///   name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The URL to the resource. Empty when the source gives only an ID
    ///   on a site whose pages Shoko cannot build, with the ID in
    ///   <see cref="ID"/>.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    ///   The ID alone, as the site the resource points at knows the entry,
    ///   e.g. <c>tt0123456</c> for an IMDb page, when the resource is one
    ///   entry's page on a site with IDs of its own. <c>null</c> otherwise,
    ///   so a caller reads an external ID here rather than parsing
    ///   <see cref="Url"/>.
    /// </summary>
    public string? ID { get; init; }

    /// <summary>
    ///   The ISO 639-1 alpha-2 language code the resource's content
    ///   is in, if it's known and applies to a single language.
    ///   <c>null</c> if the resource is multi-language, the language
    ///   doesn't apply (e.g. a bare cross-reference ID lookup), or
    ///   the language is unknown.
    /// </summary>
    public string? LanguageCode { get; init; }
}
