using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// An Anilist anime search result implementation.
/// </summary>
public class AnilistAnimeSearchResult : IAnilistAnimeSearchResult
{
    /// <inheritdoc/>
    public int ID { get; }

    /// <inheritdoc/>
    public string Title { get; }

    /// <inheritdoc/>
    public string OriginalTitle { get; }

    /// <inheritdoc/>
    public string OriginalLanguage { get; }

    /// <inheritdoc/>
    public string Overview { get; }

    /// <inheritdoc/>
    public AnilistMediaStatus ReleasingStatus { get; }

    /// <inheritdoc/>
    public AnilistMediaSource MediaSource { get; }

    /// <inheritdoc/>
    public YearlySeason? Season { get; }

    /// <inheritdoc/>
    public int? SeasonYear { get; }

    /// <inheritdoc/>
    public PartialDateOnly? FirstAiredAt { get; }

    /// <inheritdoc/>
    public string? CoverImageUrl { get; }

    /// <inheritdoc/>
    public string? BannerImageUrl { get; }

    /// <inheritdoc/>
    public decimal UserRating { get; }

    /// <inheritdoc/>
    public int UserVotes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> Genres { get; }

    /// <inheritdoc/>
    public AnimeType Type { get; }

    /// <summary>
    /// Whether the anime is adult content.
    /// </summary>
    public bool IsRestricted { get; }

    /// <summary>
    /// The total episode count, if known.
    /// </summary>
    public int? EpisodeCount { get; }

    /// <summary>
    /// English title, if set.
    /// </summary>
    public string? EnglishTitle { get; }

    /// <summary>
    /// Main title. A transcription of the native title (romaji, pinyin, etc.),
    /// which AniList treats as the canonical title.
    /// </summary>
    public string MainTitle { get; }

    /// <summary>
    /// Native title, if set.
    /// </summary>
    public string? NativeTitle { get; }

    /// <summary>
    /// Alternative titles.
    /// </summary>
    public IReadOnlyList<string> Synonyms { get; }

    /// <summary>
    /// Every title and synonym, for matching.
    /// </summary>
    public IReadOnlySet<string> AllTitles { get; }

    /// <summary>
    /// Creates a new <see cref="AnilistAnimeSearchResult"/> from a JSON node.
    /// </summary>
    /// <param name="media">The media JSON node from Anilist GraphQL response.</param>
    public AnilistAnimeSearchResult(JsonNode media)
    {
        ID = media["id"]?.GetValue<int>() ?? 0;

        // Titles
        var title = media["title"];
        var englishTitle = title?["english"]?.GetValue<string>() ?? string.Empty;
        var romajiTitle = title?["romaji"]?.GetValue<string>() ?? string.Empty;
        var nativeTitle = title?["native"]?.GetValue<string>() ?? string.Empty;

        Title = !string.IsNullOrEmpty(englishTitle) ? englishTitle : romajiTitle;
        OriginalTitle = nativeTitle;
        EnglishTitle = string.IsNullOrEmpty(englishTitle) ? null : englishTitle;
        MainTitle = romajiTitle;
        NativeTitle = string.IsNullOrEmpty(nativeTitle) ? null : nativeTitle;
        Synonyms = media["synonyms"] is JsonArray synonyms
            ? synonyms.Select(s => s?.GetValue<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct().ToList()
            : [];
        AllTitles = new[] { englishTitle, romajiTitle, nativeTitle }.Concat(Synonyms).Where(t => !string.IsNullOrWhiteSpace(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Country of origin determines original language
        OriginalLanguage = AnilistUtility.ParseOriginalLanguage(media["countryOfOrigin"]?.GetValue<string>());

        // Description
        Overview = AnilistUtility.StripHtml(media["description"]?.GetValue<string>());

        ReleasingStatus = AnilistUtility.ParseMediaStatus(media["status"]?.GetValue<string>());
        MediaSource = AnilistUtility.ParseMediaSource(media["source"]?.GetValue<string>());

        // Season
        var season = media["season"]?.GetValue<string>();
        Season = AnilistUtility.ParseSeason(season);
        SeasonYear = media["seasonYear"]?.GetValue<int?>();

        // Air date
        var startDate = media["startDate"];
        if (startDate is not null)
        {
            var year = startDate["year"]?.GetValue<int?>();
            var month = startDate["month"]?.GetValue<int?>();
            var day = startDate["day"]?.GetValue<int?>();
            if (year.HasValue)
                FirstAiredAt = new(year.Value, month, day);
        }

        // Images
        CoverImageUrl = media["coverImage"]?["extraLarge"]?.GetValue<string>();
        BannerImageUrl = media["bannerImage"]?.GetValue<string>();

        // Rating (0-100 scale)
        UserRating = media["averageScore"]?.GetValue<int?>() ?? 0;
        UserVotes = media["favourites"]?.GetValue<int?>() ?? 0;

        // Genres
        var genresNode = media["genres"];
        if (genresNode is JsonArray genresArray)
        {
            Genres = genresArray
                .Select(g => g?.GetValue<string>())
                .Where(g => !string.IsNullOrEmpty(g))
                .ToList()!;
        }
        else
        {
            Genres = [];
        }

        // Type (from format)
        Type = AnilistUtility.ParseFormat(media["format"]?.GetValue<string>());

        // Adult content
        IsRestricted = media["isAdult"]?.GetValue<bool>() ?? false;

        // Episode count
        EpisodeCount = media["episodes"]?.GetValue<int?>();
    }

    /// <summary>
    /// Creates a new <see cref="AnilistAnimeSearchResult"/> from an existing Anilist_Anime model.
    /// </summary>
    /// <param name="anime">The database anime model.</param>
    public AnilistAnimeSearchResult(Models.Anilist.Anilist_Anime anime)
    {
        ID = anime.AnilistAnimeID;
        Title = anime.PreferredTitle;
        OriginalTitle = anime.NativeTitle;
        EnglishTitle = string.IsNullOrEmpty(anime.EnglishTitle) ? null : anime.EnglishTitle;
        MainTitle = anime.MainTitle;
        NativeTitle = string.IsNullOrEmpty(anime.NativeTitle) ? null : anime.NativeTitle;
        Synonyms = anime.Synonyms;
        AllTitles = new[] { anime.EnglishTitle, anime.MainTitle, anime.NativeTitle }.Concat(anime.Synonyms).Where(t => !string.IsNullOrWhiteSpace(t)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        OriginalLanguage = anime.OriginalLanguageCode;
        Overview = anime.EnglishOverview;
        ReleasingStatus = anime.ReleasingStatus;
        MediaSource = anime.MediaSource;
        Season = anime.Season;
        SeasonYear = anime.SeasonYear;
        FirstAiredAt = anime.FirstAiredAt;
        CoverImageUrl = AnilistImageService.ToImageUrl(anime.CoverImagePath);
        BannerImageUrl = AnilistImageService.ToImageUrl(anime.BannerImagePath);
        UserRating = (decimal)anime.UserRating;
        UserVotes = anime.FavoriteCount;
        Genres = anime.Genres;
        Type = anime.Type;
        IsRestricted = anime.IsRestricted;
        EpisodeCount = anime.EpisodeCount > 0 ? anime.EpisodeCount : null;
    }

}
