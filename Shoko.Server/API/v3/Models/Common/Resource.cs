
using System.ComponentModel.DataAnnotations;

using AbstractResource = Shoko.Abstractions.Metadata.Resource;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// A site link, as in hyperlink.
/// </summary>
public class Resource
{
    /// <summary>
    /// Resource type.
    /// </summary>
    [Required]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// site name
    /// </summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// the url to the series page
    /// </summary>
    [Required]
    public string URL { get; init; } = string.Empty;

    /// <summary>
    ///   The ID the site knows the entry by, if known. The only value of a
    ///   resource with an empty <see cref="URL"/>.
    /// </summary>
    public string? ID { get; init; }

    /// <summary>
    ///   The ISO 639-1 alpha-2 language code the resource's content is
    ///   in, if known and applicable.
    /// </summary>
    public string? LanguageCode { get; init; }

    public Resource() { }

    public Resource(string type, string name, string url)
    {
        Type = type;
        Name = name;
        URL = url;
    }

    public Resource(AbstractResource resource)
        : this(resource.Type.ToString(), resource.Name, resource.Url)
    {
        ID = resource.ID;
        LanguageCode = resource.LanguageCode;
    }
}
