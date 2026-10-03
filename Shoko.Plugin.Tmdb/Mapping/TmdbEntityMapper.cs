using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.People;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using TmdbGender = TMDbLib.Objects.People.PersonGender;
using TmdbNetwork = TMDbLib.Objects.TvShows.Network;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns what TMDb answers into what the core's stores take.
/// </summary>
public static class TmdbEntityMapper
{
    #region Shows

    /// <summary>
    ///   A show, with the seasons and episodes worked out for it.
    /// </summary>
    /// <param name="show">The show, with its translations, alternative titles, external IDs and content ratings.</param>
    /// <param name="seasons">Its seasons.</param>
    /// <param name="episodes">Its episodes.</param>
    /// <param name="languages">The languages to keep.</param>
    /// <returns>The show to store.</returns>
    public static MetadataSeriesData ToSeriesData(TvShow show, IReadOnlyList<MetadataSeasonData> seasons, IReadOnlyList<MetadataEpisodeData> episodes, TmdbTextLanguages languages)
    {
        ArgumentNullException.ThrowIfNull(show);

        var english = TmdbTexts.English(show.Translations, data => data.Name, show.Name, show.OriginalName, show.OriginalLanguage);
        var status = ReleaseStatusOf(show.Status);
        return new()
        {
            ID = TmdbIds.Series(show.Id),
            Titles = TmdbTexts.Titles(english, show.OriginalName, show.OriginalLanguage, show.Translations, show.AlternativeTitles?.Results, languages.Titles, ownName: show.Name),
            Overviews = TmdbTexts.Overviews(TmdbTexts.English(show.Translations, data => data.Overview, show.Overview, originalLanguageCode: show.OriginalLanguage), show.Translations, languages.Overviews),
            Type = AnimeType.TV,
            AirDate = PartialDateOnly.FromDateTime(show.FirstAirDate),
            EndDate = status is ReleaseStatus.Finished or ReleaseStatus.Cancelled ? PartialDateOnly.FromDateTime(show.LastAirDate) : null,
            Rating = show.VoteAverage,
            RatingVotes = show.VoteCount,
            Restricted = show.Adult,
            ReleaseStatus = status,
            OriginalLanguageCode = TmdbTexts.Clean(show.OriginalLanguage),
            ProductionCountries = CountryCodes(show.ProductionCountries),
            Popularity = show.Popularity > 0 ? show.Popularity : null,
            Resources = Homepage(show.Homepage),
            CrossSourceIDs = CrossSourceIDs(MetadataEntityType.Series, show.ExternalIds?.ImdbId, show.ExternalIds?.TvdbId),
            ContentRatings = ContentRatings(show.ContentRatings?.Results?.Select(rating => (rating.Iso_3166_1, rating.Rating)), languages.ContentRatings),
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, show.PosterPath), (ImageEntityType.Backdrop, show.BackdropPath)),
            Seasons = seasons,
            Episodes = episodes,
        };
    }

    /// <summary>
    ///   One season of a show.
    /// </summary>
    /// <param name="season">The season, with its translations.</param>
    /// <param name="languages">The languages to keep.</param>
    /// <returns>The season to store.</returns>
    public static MetadataSeasonData ToSeasonData(TvSeason season, TmdbTextLanguages languages)
    {
        ArgumentNullException.ThrowIfNull(season);

        var english = TmdbTexts.English(season.Translations, data => data.Name, season.Name, seasonNumber: season.SeasonNumber);
        var ownName = TmdbTexts.IsGenericSeasonName(season.Name ?? string.Empty, season.SeasonNumber) ? null : season.Name;
        return new()
        {
            ID = TmdbIds.Season(season.Id ?? 0),
            SeasonNumber = season.SeasonNumber,
            Titles = TmdbTexts.Titles(english, null, null, season.Translations, null, languages.Titles, ownName: ownName, seasonNumber: season.SeasonNumber),
            Overviews = TmdbTexts.Overviews(TmdbTexts.English(season.Translations, data => data.Overview, season.Overview), season.Translations, languages.Overviews),
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, season.PosterPath)),
        };
    }

    /// <summary>
    ///   One episode of a season, as the season lists it and, when it was
    ///   fetched on its own, with its translations and external IDs.
    /// </summary>
    /// <param name="seasonID">The TMDb season ID.</param>
    /// <param name="listed">The episode as the season lists it.</param>
    /// <param name="details">The episode fetched on its own, or <see langword="null"/>.</param>
    /// <param name="languages">The languages to keep.</param>
    /// <returns>The episode to store.</returns>
    public static MetadataEpisodeData ToEpisodeData(int seasonID, TvSeasonEpisode listed, TvEpisode? details, TmdbTextLanguages languages)
    {
        ArgumentNullException.ThrowIfNull(listed);

        var episodeNumber = (int)listed.EpisodeNumber;
        var english = TmdbTexts.English(details?.Translations, data => data.Name, listed.Name);
        return new()
        {
            ID = TmdbIds.Episode(listed.Id),
            SeasonID = TmdbIds.Season(seasonID),
            SeasonNumber = listed.SeasonNumber,
            EpisodeNumber = episodeNumber,
            Type = listed.SeasonNumber is 0 ? EpisodeType.Special : EpisodeType.Episode,
            Rating = listed.VoteAverage,
            RatingVotes = listed.VoteCount,
            Runtime = listed.Runtime is > 0 ? TimeSpan.FromMinutes(listed.Runtime.Value) : TimeSpan.Zero,
            AirDate = listed.AirDate is { } airDate ? DateOnly.FromDateTime(airDate) : null,
            CrossSourceIDs = CrossSourceIDs(MetadataEntityType.Episode, details?.ExternalIds?.ImdbId, details?.ExternalIds?.TvdbId),
            Titles = TmdbTexts.Titles(english, null, null, details?.Translations, null, languages.EpisodeTitles, episodeNumber),
            Overviews = TmdbTexts.Overviews(TmdbTexts.English(details?.Translations, data => data.Overview, listed.Overview), details?.Translations, languages.Overviews),
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Backdrop, listed.StillPath ?? details?.StillPath)),
        };
    }

    /// <summary>
    ///   Where a show is in its release, from TMDb's status.
    /// </summary>
    /// <param name="status">TMDb's status, e.g. <c>Returning Series</c>.</param>
    /// <returns>The release status.</returns>
    public static ReleaseStatus ReleaseStatusOf(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "returning series" or "in production" => ReleaseStatus.Releasing,
            "ended" => ReleaseStatus.Finished,
            "canceled" or "cancelled" => ReleaseStatus.Cancelled,
            "planned" or "pilot" => ReleaseStatus.NotYetReleased,
            _ => ReleaseStatus.Unknown,
        };

    #endregion

    #region Movies & Collections

    /// <summary>
    ///   A movie.
    /// </summary>
    /// <param name="movie">The movie, with its translations, alternative titles, release dates and external IDs.</param>
    /// <param name="languages">The languages to keep.</param>
    /// <returns>The movie to store.</returns>
    public static MetadataMovieData ToMovieData(Movie movie, TmdbTextLanguages languages)
    {
        ArgumentNullException.ThrowIfNull(movie);

        var english = TmdbTexts.English(movie.Translations, data => data.Name, movie.Title, movie.OriginalTitle, movie.OriginalLanguage);
        return new()
        {
            ID = TmdbIds.Movie(movie.Id),
            Titles = TmdbTexts.Titles(english, movie.OriginalTitle, movie.OriginalLanguage, movie.Translations, movie.AlternativeTitles?.Titles, languages.Titles, ownName: movie.Title),
            Overviews = TmdbTexts.Overviews(TmdbTexts.English(movie.Translations, data => data.Overview, movie.Overview, originalLanguageCode: movie.OriginalLanguage), movie.Translations, languages.Overviews),
            ReleaseDate = ReleaseDateOf(movie) is { } released ? DateOnly.FromDateTime(released) : null,
            Runtime = movie.Runtime is > 0 ? TimeSpan.FromMinutes(movie.Runtime.Value) : null,
            Restricted = movie.Adult,
            Video = movie.Video,
            OriginalLanguageCode = TmdbTexts.Clean(movie.OriginalLanguage),
            ProductionCountries = CountryCodes(movie.ProductionCountries),
            Rating = movie.VoteAverage,
            RatingVotes = movie.VoteCount,
            Resources = Homepage(movie.Homepage),
            CrossSourceIDs = CrossSourceIDs(MetadataEntityType.Movie, movie.ExternalIds?.ImdbId ?? movie.ImdbId, null),
            ContentRatings = ContentRatings(
                (movie.ReleaseDates?.Results ?? [])
                    .SelectMany(country => (country.ReleaseDates ?? []).Select(release => (country.Iso_3166_1, release.Certification))),
                languages.ContentRatings
            ),
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, movie.PosterPath), (ImageEntityType.Backdrop, movie.BackdropPath)),
        };
    }

    /// <summary>
    ///   When a movie was first released: its theatrical release, or its
    ///   premiere, in the first country that made it, else the date TMDb
    ///   gives the movie.
    /// </summary>
    /// <param name="movie">The movie, with its release dates.</param>
    /// <returns>The date, or <see langword="null"/> when none is known.</returns>
    public static DateTime? ReleaseDateOf(Movie movie)
    {
        var country = movie.ProductionCountries?.FirstOrDefault()?.Iso_3166_1;
        if (string.IsNullOrEmpty(country) || movie.ReleaseDates?.Results?.FirstOrDefault(result => result.Iso_3166_1 == country) is not { ReleaseDates: { } releases })
            return movie.ReleaseDate;

        return releases.FirstOrDefault(release => release.Type is ReleaseDateType.TheatricalLimited or ReleaseDateType.Theatrical)?.ReleaseDate
            ?? releases.FirstOrDefault(release => release.Type is ReleaseDateType.Premiere)?.ReleaseDate
            ?? releases.FirstOrDefault()?.ReleaseDate
            ?? movie.ReleaseDate;
    }

    /// <summary>
    ///   A collection, with every movie TMDb lists in it.
    /// </summary>
    /// <param name="collection">The collection, with its parts and translations.</param>
    /// <param name="languages">The languages to keep.</param>
    /// <returns>The collection to store.</returns>
    public static MetadataCollectionData ToCollectionData(Collection collection, TmdbTextLanguages languages)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var english = TmdbTexts.English(collection.Translations, data => data.Name, collection.Name);
        return new()
        {
            ID = TmdbIds.Collection(collection.Id),
            Titles = TmdbTexts.Titles(english, null, null, collection.Translations, null, languages.Titles, ownName: collection.Name),
            Overviews = TmdbTexts.Overviews(TmdbTexts.English(collection.Translations, data => data.Overview, collection.Overview), collection.Translations, languages.Overviews),
            Members = [.. (collection.Parts ?? []).Where(part => part.Id > 0).Select(part => TmdbIds.Movie(part.Id)).Distinct()],
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, collection.PosterPath), (ImageEntityType.Backdrop, collection.BackdropPath)),
        };
    }

    #endregion

    #region People, Studios & Networks

    /// <summary>
    ///   A person, as their own record on TMDb has them.
    /// </summary>
    /// <param name="person">The person, with their translations and external IDs.</param>
    /// <returns>The creator to store.</returns>
    public static MetadataCreatorData ToCreatorData(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new()
        {
            ID = TmdbIds.Creator(person.Id),
            Name = TmdbTexts.Clean(person.Name) ?? string.Empty,
            Overview = TmdbTexts.English(person.Translations, data => data.Overview, person.Biography),
            Type = CreatorType.Person,
            AlternativeNames = [.. (person.AlsoKnownAs ?? []).Select(TmdbTexts.Clean).OfType<string>().Distinct().Select(name => new MetadataNameData { Name = name })],
            Gender = GenderOf(person.Gender),
            BirthDay = person.Birthday is { } birthday ? new FuzzyDateOnly(DateOnly.FromDateTime(birthday)) : null,
            DeathDay = person.Deathday is { } deathday ? new FuzzyDateOnly(DateOnly.FromDateTime(deathday)) : null,
            PlaceOfBirth = TmdbTexts.Clean(person.PlaceOfBirth),
            IsRestricted = person.Adult,
            Resources = [.. Homepage(person.Homepage), .. ImdbResource(person.ExternalIds?.ImdbId ?? person.ImdbId, "name")],
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, person.ProfilePath)),
        };
    }

    /// <summary>
    ///   A company, as a studio, with the logo TMDb names as its default.
    /// </summary>
    /// <param name="id">The TMDb company ID.</param>
    /// <param name="name">The company's name.</param>
    /// <param name="countryOfOrigin">The country the company is from.</param>
    /// <param name="logoPath">TMDb's path for the company's logo, or <see langword="null"/> when it has none.</param>
    /// <returns>The studio to store.</returns>
    public static MetadataStudioData ToStudioData(int id, string? name, string? countryOfOrigin, string? logoPath)
        => new()
        {
            ID = TmdbIds.Studio(id),
            Name = TmdbTexts.Clean(name) ?? string.Empty,
            CountryOfOrigin = TmdbTexts.Clean(countryOfOrigin),
            DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, logoPath)),
        };

    /// <summary>
    ///   A network.
    /// </summary>
    /// <param name="id">The TMDb network ID.</param>
    /// <param name="name">The network's name.</param>
    /// <param name="countryOfOrigin">The country the network is from.</param>
    /// <returns>The network to store, leaving its default logo as it is.</returns>
    public static MetadataNetworkData ToNetworkData(int id, string? name, string? countryOfOrigin)
        => new() { ID = TmdbIds.Network(id), Name = TmdbTexts.Clean(name) ?? string.Empty, CountryOfOrigin = TmdbTexts.Clean(countryOfOrigin) };

    /// <summary>
    ///   A network as a listing names it, with the logo TMDb names as its
    ///   default.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <returns>The network to store.</returns>
    public static MetadataNetworkData ToNetworkData(NetworkWithLogo network)
        => ToNetworkData(network.Id, network.Name, network.OriginCountry) with { DefaultImageResourceIDs = TmdbImages.Defaults((ImageEntityType.Primary, network.LogoPath)) };

    /// <summary>
    ///   The companies that made an entry, as studios, and their part in it.
    /// </summary>
    /// <param name="companies">The production companies TMDb lists.</param>
    /// <returns>The studios to store, and the entry's studios.</returns>
    public static (IReadOnlyList<MetadataStudioData> Studios, IReadOnlyList<MetadataEntryStudioData> Entry) Studios(IEnumerable<ProductionCompany>? companies)
    {
        var listed = (companies ?? []).Where(company => company.Id > 0).DistinctBy(company => company.Id).ToList();
        return (
            [.. listed.Select(company => ToStudioData(company.Id, company.Name, company.OriginCountry, company.LogoPath))],
            // TMDb does not say what a company did, so none is claimed.
            [.. listed.Select(company => new MetadataEntryStudioData { StudioID = TmdbIds.Studio(company.Id), StudioName = TmdbTexts.Clean(company.Name), Type = StudioType.None })]
        );
    }

    /// <summary>
    ///   The networks a show aired on.
    /// </summary>
    /// <param name="networks">The networks TMDb lists.</param>
    /// <returns>The networks to store, and the show's networks.</returns>
    public static (IReadOnlyList<MetadataNetworkData> Networks, IReadOnlyList<MetadataEntryNetworkData> Entry) Networks(IEnumerable<NetworkWithLogo>? networks)
    {
        var listed = (networks ?? []).Where(network => network.Id > 0).DistinctBy(network => network.Id).ToList();
        return (
            [.. listed.Select(ToNetworkData)],
            [.. listed.Select(network => new MetadataEntryNetworkData { NetworkID = TmdbIds.Network(network.Id), NetworkName = TmdbTexts.Clean(network.Name) })]
        );
    }

    /// <summary>
    ///   A network as TMDb's own record has it.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <returns>The network to store.</returns>
    public static MetadataNetworkData ToNetworkData(TmdbNetwork network)
        => ToNetworkData(network.Id, network.Name, network.OriginCountry);

    /// <summary>
    ///   A person's gender, from TMDb's.
    /// </summary>
    /// <param name="gender">TMDb's gender.</param>
    /// <returns>The gender.</returns>
    public static Shoko.Abstractions.Metadata.Enums.PersonGender GenderOf(TmdbGender gender)
        => gender switch
        {
            TmdbGender.Female => Shoko.Abstractions.Metadata.Enums.PersonGender.Female,
            TmdbGender.Male => Shoko.Abstractions.Metadata.Enums.PersonGender.Male,
            TmdbGender.NonBinary => Shoko.Abstractions.Metadata.Enums.PersonGender.NonBinary,
            _ => Shoko.Abstractions.Metadata.Enums.PersonGender.Unknown,
        };

    #endregion

    #region Tags

    /// <summary>
    ///   An entry's genres and keywords, as tags keyed by TMDb's own IDs, the
    ///   genres first, each part of a genre joining two its own tag.
    /// </summary>
    /// <param name="genres">The genres.</param>
    /// <param name="keywords">The keywords.</param>
    /// <returns>The tags to store, and the entry's tags.</returns>
    public static (IReadOnlyList<MetadataTagData> Tags, IReadOnlyList<MetadataEntryTagData> Entry) Tags(IEnumerable<Genre>? genres, IEnumerable<Keyword>? keywords)
    {
        var tags = new List<MetadataTagData>();
        foreach (var genre in (genres ?? []).Where(genre => genre.Id > 0 && !string.IsNullOrWhiteSpace(genre.Name)).DistinctBy(genre => genre.Id))
            tags.AddRange(GenreTags(genre));
        foreach (var keyword in (keywords ?? []).Where(keyword => keyword.Id > 0 && !string.IsNullOrWhiteSpace(keyword.Name)).DistinctBy(keyword => keyword.Id))
            tags.Add(new() { ID = TmdbIds.Keyword(keyword.Id), Name = keyword.Name!.Trim(), Kind = TagKind.Keyword });

        return (tags, [.. tags.Select(tag => new MetadataEntryTagData { TagID = tag.ID })]);
    }

    /// <summary>
    ///   A genre as tags: one by its name as TMDb writes it, or one per part
    ///   of a genre that joins two with <c>&amp;</c>, such as
    ///   <c>Action &amp; Adventure</c>.
    /// </summary>
    /// <param name="genre">The genre.</param>
    /// <returns>The tags.</returns>
    public static IReadOnlyList<MetadataTagData> GenreTags(Genre genre)
    {
        var names = GenreNames(genre.Name);
        return names.Count is 1
            ? [new() { ID = TmdbIds.Genre(genre.Id), Name = names[0], Kind = TagKind.Genre }]
            : [.. names.Select((name, index) => new MetadataTagData { ID = TmdbIds.GenrePart(genre.Id, index + 1), Name = name, Kind = TagKind.Genre })];
    }

    /// <summary>
    ///   The names a genre stands for: each part of one that joins two with
    ///   <c>&amp;</c>, else its own name.
    /// </summary>
    /// <param name="name">The genre's name, as TMDb writes it.</param>
    /// <returns>The names, or none for a blank name.</returns>
    public static IReadOnlyList<string> GenreNames(string? name)
        => [.. (name ?? string.Empty).Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)];

    #endregion

    #region Suggestions

    /// <summary>
    ///   What TMDb suggests for an entry: its recommendations, then its
    ///   similar titles, each ranked as TMDb ranks them.
    /// </summary>
    /// <param name="ownID">The entry's own TMDb ID, which is never suggested for itself.</param>
    /// <param name="recommended">The recommendations.</param>
    /// <param name="similar">The similar titles.</param>
    /// <param name="idOf">The identifier of a suggested ID.</param>
    /// <returns>The suggestions.</returns>
    public static IReadOnlyList<MetadataSuggestionData> Suggestions(int ownID, IEnumerable<int>? recommended, IEnumerable<int>? similar, Func<int, MetadataGuid> idOf)
    {
        var suggestions = new List<MetadataSuggestionData>();
        foreach (var (kind, ids) in new[] { (SuggestionKind.Recommended, recommended), (SuggestionKind.Similar, similar) })
        {
            var order = 0;
            foreach (var id in (ids ?? []).Where(id => id > 0 && id != ownID).Distinct())
                suggestions.Add(new() { SuggestedID = idOf(id), Kind = kind, Order = order++ });
        }

        return suggestions;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   The IDs other sources gave an entry, as TMDb lists them.
    /// </summary>
    /// <param name="entityType">The entry's kind.</param>
    /// <param name="imdbID">The IMDb ID, if any.</param>
    /// <param name="tvdbID">The TvDB ID, if any.</param>
    /// <returns>The IDs, IMDb's first.</returns>
    public static IReadOnlyList<MetadataGuid> CrossSourceIDs(MetadataEntityType entityType, string? imdbID, string? tvdbID)
    {
        var ids = new List<MetadataGuid>();
        // Parsed on every call, so a source registered by a plugin later is still found.
        if (TmdbTexts.Clean(imdbID) is { } imdb && imdb.StartsWith("tt", StringComparison.Ordinal) && MetadataSource.TryParse("imdb", out var imdbSource))
            ids.Add(new(imdbSource, entityType, imdb));
        if (int.TryParse(tvdbID, out var tvdb) && tvdb > 0 && MetadataSource.TryParse("tvdb", out var tvdbSource))
            ids.Add(new(tvdbSource, entityType, TmdbIds.Format(tvdb)));
        return ids;
    }

    /// <summary>
    ///   The ISO codes of the countries TMDb lists, sorted, each once.
    /// </summary>
    /// <param name="countries">The countries.</param>
    /// <returns>The codes.</returns>
    public static IReadOnlyList<string> CountryCodes(IEnumerable<ProductionCountry>? countries)
        => [.. (countries ?? []).Select(country => TmdbTexts.Clean(country.Iso_3166_1)?.ToUpperInvariant()).OfType<string>().Distinct().Order(StringComparer.Ordinal)];

    /// <summary>
    ///   The content ratings to keep: those of the countries whose language
    ///   is kept, in TMDb's order, a country as often as TMDb rates it but
    ///   each rating of it once.
    /// </summary>
    /// <param name="ratings">The ratings, by ISO country code.</param>
    /// <param name="languages">The languages to keep ratings for, or <see langword="null"/> for all.</param>
    /// <returns>The ratings.</returns>
    public static IReadOnlyList<MetadataContentRatingData> ContentRatings(IEnumerable<(string? Country, string? Rating)>? ratings, IReadOnlySet<TitleLanguage>? languages)
    {
        var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var languageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages ?? new HashSet<TitleLanguage>())
        {
            var (languageCode, countryCode) = language.GetLanguageAndCountryCode();
            if (countryCode is null)
                languageCodes.Add(languageCode);
            else
                countries.Add(countryCode);
        }

        return
        [
            .. (ratings ?? [])
                .Select(rating => (Country: TmdbTexts.Clean(rating.Country)?.ToUpperInvariant(), Rating: TmdbTexts.Clean(rating.Rating)))
                .Where(rating => rating.Country is not null && rating.Rating is not null)
                .Where(rating => languages is null || countries.Contains(rating.Country!) || languageCodes.Contains(rating.Country!.FromIso3166ToIso639()))
                .Distinct()
                .Select(rating => new MetadataContentRatingData { CountryCode = rating.Country!, Rating = rating.Rating! }),
        ];
    }

    /// <summary>
    ///   An entry's homepage, as a resource.
    /// </summary>
    /// <param name="homepage">The homepage, if any.</param>
    /// <returns>The resource, or none.</returns>
    public static IReadOnlyList<Resource> Homepage(string? homepage)
        => TmdbTexts.Clean(homepage) is { } url && Uri.TryCreate(url, UriKind.Absolute, out _)
            ? [new() { Type = ResourceType.Website, Name = "Homepage", Url = url }]
            : [];

    private static IEnumerable<Resource> ImdbResource(string? imdbID, string kind)
        => TmdbTexts.Clean(imdbID) is { } id
            ? [new() { Type = ResourceType.Metadata, Name = "IMDb", Url = $"https://www.imdb.com/{kind}/{id}/", ID = id }]
            : [];

    /// <summary>
    ///   The IDs of what a search page offers.
    /// </summary>
    /// <param name="results">The page.</param>
    /// <returns>The IDs.</returns>
    public static IEnumerable<int> IDs(IEnumerable<SearchBase>? results)
        => (results ?? []).Select(result => result.Id);

    #endregion
}
