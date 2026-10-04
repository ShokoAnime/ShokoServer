using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// APIv3 Title Data Transfer Object (DTO)
/// </summary>
public class Title
{
    /// <summary>
    /// The title.
    /// </summary>
    [Required]
    public string Name { get; init; }

    /// <summary>
    /// convert to AniDB style (x-jat is the special one, but most are standard 3-digit short names)
    /// </summary>
    [Required]
    public string Language { get; init; }

    /// <summary>
    /// Title Type
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public TitleType Type { get; init; }

    /// <summary>
    /// Indicates this is the default title for the entity.
    /// </summary>
    public bool Default { get; init; }

    /// <summary>
    /// Indicates this is the user preferred title.
    /// </summary>
    /// <value></value>
    public bool Preferred { get; init; }

    /// <summary>
    /// AniDB, TMDB, AniList, etc.
    /// </summary>
    [Required]
    public string Source { get; init; }

    /// <summary>
    /// Set when the title was synthesized for an episode with no title of its
    /// own, rather than given by a source. Left out otherwise.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? Synthesized { get; init; }

    public Title(ITitle title, string? mainTitle = null, string? preferredTitle = null)
    {
        Name = title.Value;
        Language = title.LanguageCode;
        Type = title.Type;
        Default = !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Value, mainTitle);
        Preferred = !string.IsNullOrEmpty(preferredTitle) && string.Equals(title.Value, preferredTitle);
        Source = "AniDB";
    }

    public Title(ITitle title, string? mainTitle = null, ITitle? preferredTitle = null)
    {
        Name = title.Value;
        Language = title.Language.GetString();
        Default = title.Language is TitleLanguage.EnglishAmerican && !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Value, mainTitle);
        // The English title is a TMDB entry's main one.
        Type = Default ? TitleType.Main : TitleType.None;
        Preferred = title.Equals(preferredTitle);
        Source = "TMDB";
    }
}
