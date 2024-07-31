
#nullable enable
using System.ComponentModel.DataAnnotations;

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

    public Resource() { }

    public Resource((string type, string name, string url) tuple)
        : this(tuple.type, tuple.name, tuple.url) { }

    public Resource(string type, string name, string url)
    {
        Type = type;
        Name = name;
        URL = url;
    }
}
