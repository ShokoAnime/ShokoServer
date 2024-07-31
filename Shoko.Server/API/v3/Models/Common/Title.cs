using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.Titles;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Title object, stores the title, type, language, and source
/// if using a TvDB title, assume "eng:official". If using AniList, assume "x-jat:main"
/// AniDB's MainTitle is "x-jat:main"
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
    /// AniDB, TvDB, AniList, etc.
    /// </summary>
    [Required]
    public string Source { get; init; }

    public Title(SVR_AniDB_Anime_Title title, string? mainTitle = null, string? preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = title.TitleType;
        Default = !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Title, mainTitle);
        Preferred = !string.IsNullOrEmpty(preferredTitle) && string.Equals(title.Title, preferredTitle);
        Source = "AniDB";
    }

    public Title(ResponseAniDBTitles.Anime.AnimeTitle title, string? mainTitle = null, string? preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = title.TitleType;
        Default = !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Title, mainTitle);
        Preferred = !string.IsNullOrEmpty(preferredTitle) && string.Equals(title.Title, preferredTitle);
        Source = "AniDB";
    }

    public Title(SVR_AniDB_Episode_Title title, string? mainTitle = null, SVR_AniDB_Episode_Title? preferredTitle = null)
    {
        Name = title.Title;
        Language = title.LanguageCode;
        Type = TitleType.None;
        Default = title.Language == TitleLanguage.English && !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Title, mainTitle);
        Preferred = preferredTitle is not null && title.AniDB_Episode_TitleID == preferredTitle.AniDB_Episode_TitleID;
        Source = "AniDB";
    }

    public Title(TMDB_Title title, string? mainTitle = null, TMDB_Title? preferredTitle = null)
    {
        Name = title.Value;
        Language = title.Language.GetString();
        Type = TitleType.None;
        Default = title.Language == TitleLanguage.English && !string.IsNullOrEmpty(mainTitle) && string.Equals(title.Value, mainTitle);
        Preferred = title.Equals(preferredTitle);
        Source = "TMDB";
    }
}
