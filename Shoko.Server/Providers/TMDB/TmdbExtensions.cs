using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.TMDB;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public static class TmdbExtensions
{
    private static readonly TimeOnly MidDay = TimeOnly.FromTimeSpan(TimeSpan.FromHours(12));

    public static List<string> GetGenres(this Movie movie)
        => movie.Genres!
            .SelectMany(genre => genre.Name!.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .OrderBy(genre => genre)
            .ToList();

    public static IReadOnlyList<string> GetGenres(this SearchMovie movie)
    {
        var instance = TmdbMetadataService.Instance;
        if (instance is null)
            return [];

        var allMovieGenres = instance.GetMovieGenres().ConfigureAwait(false).GetAwaiter().GetResult();
        return movie.GenreIds!
            .Select(id => allMovieGenres.TryGetValue(id, out var genre) ? genre : null)
            .WhereNotNull()
            .SelectMany(genre => genre.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .OrderBy(genre => genre)
            .ToList();
    }

    public static List<string> GetGenres(this TvShow movie)
        => movie.Genres!
            .SelectMany(genre => genre.Name!.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .OrderBy(genre => genre)
            .ToList();

    public static IReadOnlyList<string> GetGenres(this SearchTv show)
    {
        var instance = TmdbMetadataService.Instance;
        if (instance is null)
            return [];

        var allShowGenres = instance.GetShowGenres().ConfigureAwait(false).GetAwaiter().GetResult();
        return show.GenreIds!
            .Select(id => allShowGenres.TryGetValue(id, out var genre) ? genre : null)
            .WhereNotNull()
            .SelectMany(genre => genre.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .OrderBy(genre => genre)
            .ToList();
    }

    public static DateTime ToDateTime(this DateOnly date)
        => date.ToDateTime(MidDay, DateTimeKind.Utc);

    public static TText? GetByLanguage<TText>(this IEnumerable<TText> texts, TitleLanguage language) where TText : IText
    {
        var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
        Func<TText, bool> filter = string.IsNullOrEmpty(countryCode)
            ? (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            : (title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(title.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
        return texts.FirstOrDefault(filter);
    }

    public static IEnumerable<TText> WhereInLanguages<TText>(this IEnumerable<TText> texts, IReadOnlySet<TitleLanguage>? languages) where TText : IText
    {
        if (languages is null)
            return texts;

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
        return texts.Where(cr => languagesCodes.Contains(cr.LanguageCode) || countyCodes.Contains(cr.CountryCode));
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
