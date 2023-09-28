using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_ContentRating
{
    public string? LanguageCode
    {
        get => Language == TitleLanguage.None ? null : Language.GetString();
        private set => Language = string.IsNullOrEmpty(value) ? TitleLanguage.None : value.GetTitleLanguage();
    }

    public TitleLanguage Language { get; private set; }

    /// <summary>
    /// content ratings (certifications) that have been added to a TV show.
    /// </summary>
    public string Rating { get; private set; }

    public TMDB_ContentRating(TitleLanguage lang, string rating)
    {
        Language = lang;
        Rating = rating;
    }

    public TMDB_ContentRating(string lang, string rating)
    {
        LanguageCode = lang;
        Rating = rating;
    }

    public override string ToString()
    {
        return $"{LanguageCode}|{Rating}";
    }

    public static TMDB_ContentRating FromString(string str)
    {
        var (langCode, rating, _) = str.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new(langCode, rating);
    }
}
