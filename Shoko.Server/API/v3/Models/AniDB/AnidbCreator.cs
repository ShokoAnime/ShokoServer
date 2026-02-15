using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// AniDB Creator APIv3 Data Transfer Object (DTO).
/// </summary>
public class AnidbCreator
{
    /// <summary>
    /// The global ID of the creator.
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// The name of the creator, transcribed to use the latin alphabet.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The original name of the creator.
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// The type of creator.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public CreatorType Type { get; set; }

    /// <summary>
    /// The URL of the creator's English homepage.
    /// </summary>
    public string? EnglishHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese homepage.
    /// </summary>
    public string? JapaneseHomepageUrl { get; set; }

    /// <summary>
    /// The URL of the creator's English Wikipedia page.
    /// </summary>
    public string? EnglishWikiUrl { get; set; }

    /// <summary>
    /// The URL of the creator's Japanese Wikipedia page.
    /// </summary>
    public string? JapaneseWikiUrl { get; set; }

    /// <summary>
    /// The date that the creator was last updated on AniDB.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// The image of the creator, if available.
    /// </summary>
    public Image? Image { get; set; }

    public AnidbCreator(AniDB_Creator creator)
    {
        ID = creator.CreatorID;
        Name = creator.Name;
        OriginalName = creator.OriginalName;
        Type = creator.AbstractType;
        EnglishHomepageUrl = creator.EnglishHomepageUrl;
        JapaneseHomepageUrl = creator.JapaneseHomepageUrl;
        EnglishWikiUrl = creator.EnglishWikiUrl;
        JapaneseWikiUrl = creator.JapaneseWikiUrl;
        LastUpdatedAt = creator.LastUpdatedAt.ToUniversalTime();
        Image = creator.GetImageMetadata() is { } image ? new Image(image) : null;
    }
}
