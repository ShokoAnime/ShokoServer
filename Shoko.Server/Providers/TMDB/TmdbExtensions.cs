using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.TMDB;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public static class TmdbExtensions
{
    private static readonly TimeOnly MidDay = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12));

    public static CL_MovieDBMovieSearch_Response ToContract(this SearchMovie movie)
        => new()
        {
            MovieID = movie.Id,
            MovieName = movie.Title,
            OriginalName = movie.OriginalTitle,
            Overview = movie.Overview,
        };

    public static DateOnly? GetAirDateAsDateOnly(this AniDB_Episode episode)
    {
        var dateTime = episode.GetAirDateAsDate();
        if (!dateTime.HasValue)
            return null;

        return DateOnly.FromDateTime(dateTime.Value);
    }

    public static DateTime ToDateTime(this DateOnly date)
        => date.ToDateTime(MidDay, DateTimeKind.Utc);

    public static TMDB_Title? GetByLanguage(this IEnumerable<TMDB_Title> titles, TitleLanguage language)
    {
        var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
        Func<TMDB_Title, bool> filter = string.IsNullOrEmpty(countryCode)
            ? (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            : (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(title.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
        return titles.FirstOrDefault(filter);
    }

    public static IEnumerable<TMDB_Title> WhereInLanguages(this IEnumerable<TMDB_Title> contentRatings, IReadOnlySet<TitleLanguage>? languages)
    {
        if (languages is null)
            return contentRatings;

        var countyCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is not null)
            .Select(c => c.countryCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languagesCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is null)
            .Select(c => c.languageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return contentRatings.Where(cr => languagesCodes.Contains(cr.LanguageCode) || countyCodes.Contains(cr.CountryCode));
    }

    public static TMDB_Overview? GetByLanguage(this IEnumerable<TMDB_Overview> titles, TitleLanguage language)
    {
        var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
        Func<TMDB_Overview, bool> filter = string.IsNullOrEmpty(countryCode)
            ? (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            : (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(title.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
        return titles.FirstOrDefault(filter);
    }

    public static IEnumerable<TMDB_Overview> WhereInLanguages(this IEnumerable<TMDB_Overview> contentRatings, IReadOnlySet<TitleLanguage>? languages)
    {
        if (languages is null)
            return contentRatings;

        var countyCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is not null)
            .Select(c => c.countryCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languagesCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is null)
            .Select(c => c.languageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return contentRatings.Where(cr => languagesCodes.Contains(cr.LanguageCode) || countyCodes.Contains(cr.CountryCode));
    }

    public static IEnumerable<TMDB_ContentRating> WhereInLanguages(this IEnumerable<TMDB_ContentRating> contentRatings, IReadOnlySet<TitleLanguage>? languages)
    {
        if (languages is null)
            return contentRatings;

        var countyCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is not null)
            .Select(c => c.countryCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languagesCodes = languages
            .Select(c => c.GetLanguageAndCountryCode())
            .Where(c => c.countryCode is null)
            .Select(c => c.languageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return contentRatings.Where(cr => languagesCodes.Contains(cr.LanguageCode) || countyCodes.Contains(cr.CountryCode));
    }
}
