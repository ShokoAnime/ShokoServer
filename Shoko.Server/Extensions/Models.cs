using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;

using AnidbReleaseStatus = Shoko.Server.Providers.AniDB.AnidbReleaseStatus;

namespace Shoko.Server.Extensions;

public static class Models
{
    /// <summary>
    ///   Checks whether the anime's end date has passed, by the same rule
    ///   <see cref="AniDB_Anime.ReleaseStatus"/> uses for it. Unlike that, an
    ///   anime without an end date never counts as finished here.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>
    ///   <c>true</c> when the end date is known and falls on or
    ///   before today, taking a partial date as the last day it could mean.
    /// </returns>
    public static bool GetFinishedAiring(this AniDB_Anime anime)
        => AnidbReleaseStatus.HasEnded(anime.EndDate, DateTime.Today.ToDateOnly());

    public static bool IsInYear(this AniDB_Anime anime, int year)
    {
        // We don't know when it airs, so it's not happened yet
        if (anime.AirDate == null) return false;

        // reasons to count in a year:
        // - starts in the year, unless it aired early
        // - ends well into the year
        // - airs all throughout the year (starts in 2015, ends in 2017, 2016 counts)

        var startDate = anime.AirDate.Value;

        // started after the year has ended
        if (startDate.Year > year) return false;

        if (startDate.Year == year)
        {
            // It started in the year, but nowhere near the end
            if (startDate.Month < 12) return true;

            // implied startDate.Month == 12, unless the calendar changes...
            // if it's a movie or short series, count it
            if (anime.AnimeType is AnimeType.Movie || anime.EpisodeCountNormal <= 6) return true;
        }

        // starts before the year, but continues through it
        if (startDate.Year < year)
        {
            // still airing or finished after the year has been started, with some time for late seasons
            // (a null EndDate here only means "still airing" for broadcast types; Movie/OVA/etc. fall back
            // to AirDate, i.e. a single-day release, since AniDB frequently never populates EndDate for them)
            var effectiveEndDate = anime.EffectiveEndDateForSeasons;
            if (effectiveEndDate == null || effectiveEndDate.Value >= new DateTime(year, 2, 1)) return true;
        }

        return false;
    }

    public static DateOnly ToDateOnly(this DateTime date)
        => DateOnly.FromDateTime(date);

    // AniDB frequently leaves EndDate unset for these types even after they've fully released (a movie or
    // OVA doesn't have an ongoing broadcast the way a TV series does), so a null EndDate here shouldn't be
    // read as "still airing" -- fall back to AirDate, i.e. a single-day release.
    private static readonly HashSet<AnimeType> s_animeTypesWithoutOngoingReleases =
    [
        AnimeType.Movie, AnimeType.OVA, AnimeType.Web, AnimeType.Other, AnimeType.MusicVideo,
    ];

    extension(AniDB_Anime anime)
    {
        /// <summary>
        /// Resolves the effective end date to use for the years an anime aired in: <see cref="AniDB_Anime.EndDate"/>
        /// if known, otherwise <see cref="AniDB_Anime.AirDate"/> for anime types that don't have an ongoing
        /// broadcast (Movie, OVA, Web, Other, MusicVideo), otherwise <c>null</c> (still airing) for
        /// TV series/specials.
        /// </summary>
        public PartialDateOnly? EffectiveEndDateForSeasons
            => anime.EndDate ?? (s_animeTypesWithoutOngoingReleases.Contains(anime.AnimeType) ? anime.AirDate : null);
    }

    public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        => anime.GetAllTagsSet();

    public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
    {
        if (string.IsNullOrEmpty(anime.AllTitles)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        return new HashSet<string>(anime.AllTitles.Split('|').Select(a => a.Trim()), StringComparer.InvariantCultureIgnoreCase);
    }

    public static decimal GetAniDBTotalRating(this AniDB_Anime anime)
    {
        decimal totalRating = 0;
        totalRating += (decimal)anime.Rating * anime.VoteCount;
        totalRating += (decimal)anime.TempRating * anime.TempVoteCount;
        return totalRating;
    }

    public static int GetAniDBTotalVotes(this AniDB_Anime anime) => anime.TempVoteCount + anime.VoteCount;

    /// <summary>
    /// looking at the episode range determine if the group has released a file
    /// for the specified episode number
    /// </summary>
    /// <param name="grpstatus"></param>
    /// <param name="episodeNumber"></param>
    /// <returns></returns>
    public static bool HasGroupReleasedEpisode(this AniDB_GroupStatus grpstatus, int episodeNumber)
    {
        // examples
        // 1-12
        // 1
        // 5-10
        // 1-10, 12

        string[] ranges = grpstatus.EpisodeRange.Split(',');

        foreach (string range in ranges)
        {
            string[] subRanges = range.Split('-');
            if (subRanges.Length == 1) // 1 episode
            {
                if (int.Parse(subRanges[0]) == episodeNumber) return true;
            }
            if (subRanges.Length == 2) // range
            {
                if (episodeNumber >= int.Parse(subRanges[0]) && episodeNumber <= int.Parse(subRanges[1]))
                    return true;
            }
        }

        return false;
    }

    public static bool IsAdminUser(this ShokoUser user) => user.IsAdmin == 1;

    /// <summary>
    ///   The movie markers a sort name leaves out at the start of a name when
    ///   a separator and more of the title follow, compared once accents are
    ///   folded and ignoring case.
    /// </summary>
    private static readonly string[] _sortNameMovieMarkers = ["Gekijouban", "Gekijoban", "Gekijou-ban", "Gekijo-ban"];

    /// <summary>
    ///   The articles a sort name leaves out at the start of a name when a
    ///   space and more of the title follow, ignoring case.
    /// </summary>
    private static readonly string[] _sortNameArticles = ["The", "An", "A"];

    /// <summary>
    ///   The folded form of every Latin letter with accents, ligature and
    ///   the like, and full-width form, worked out once; a character not in
    ///   it is kept as it is.
    /// </summary>
    private static readonly Dictionary<char, string> _sortNameFolds = BuildSortNameFolds();

    private static Dictionary<char, string> BuildSortNameFolds()
    {
        var folds = new Dictionary<char, string>
        {
            ['ß'] = "ss", ['ẞ'] = "SS", ['æ'] = "ae", ['Æ'] = "AE", ['œ'] = "oe", ['Œ'] = "OE",
            ['ø'] = "o", ['Ø'] = "O", ['ł'] = "l", ['Ł'] = "L", ['đ'] = "d", ['Đ'] = "D",
        };
        // Latin-1 Supplement, Latin Extended-A and -B, Latin Extended Additional, and the full-width forms.
        foreach (var (first, last) in new[] { ('\u00C0', '\u024F'), ('\u1E00', '\u1EFF'), ('\uFF01', '\uFF5E') })
        {
            for (var character = first; character <= last; character++)
            {
                if (folds.ContainsKey(character))
                    continue;
                var decomposed = character.ToString().Normalize(NormalizationForm.FormKD);
                if (decomposed.Length > 0 && char.IsAscii(decomposed[0]) &&
                    decomposed.Skip(1).All(mark => char.GetUnicodeCategory(mark) is UnicodeCategory.NonSpacingMark))
                    folds[character] = decomposed[..1];
            }
        }

        return folds;
    }

    /// <summary>
    ///   A name as it sorts and is filed by letter. Latin letters lose their
    ///   accents (<c>Ōkami</c> sorts as <c>Okami</c>) and full-width forms
    ///   become plain ones; then the name loses, at its start, its punctuation
    ///   (quote marks, brackets, dots and the like, but not symbols), a movie
    ///   marker ("Gekijouban" in any spelling, or <c>劇場版</c>) and an
    ///   article ("The", "A", "An"), each only when more of the title follows.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The sort name.</returns>
    public static string ToSortName(this string name)
    {
        var folded = FoldForSorting(name);
        var sortName = SkipLeadingPunctuation(folded.AsSpan());
        if (TrySkipMovieMarker(sortName, out var afterMarker))
            sortName = SkipLeadingPunctuation(afterMarker);
        if (TrySkipArticle(sortName, out var afterArticle))
            sortName = SkipLeadingPunctuation(afterArticle);
        if (sortName.IsEmpty)
            return folded;
        return sortName.Length == folded.Length ? folded : sortName.ToString();
    }

    /// <summary>
    ///   Folds the Latin letters of a name for sorting: accents dropped,
    ///   ligatures and the like spelled out, full-width forms made plain.
    ///   Any other letter, kana and their voicing marks included, is kept.
    /// </summary>
    private static string FoldForSorting(string name)
    {
        if (Ascii.IsValid(name))
            return name;

        // Most titles in other scripts have nothing to fold, and are kept without a copy.
        var index = 0;
        while (index < name.Length && (char.IsAscii(name[index]) || !_sortNameFolds.ContainsKey(name[index])))
            index++;
        if (index == name.Length)
            return name;

        var builder = new StringBuilder(name.Length + 4);
        builder.Append(name, 0, index);
        for (; index < name.Length; index++)
        {
            var character = name[index];
            if (!char.IsAscii(character) && _sortNameFolds.TryGetValue(character, out var folded))
                builder.Append(folded);
            else
                builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>
    ///   Skips the punctuation and spaces a name starts with; a symbol stays.
    /// </summary>
    private static ReadOnlySpan<char> SkipLeadingPunctuation(ReadOnlySpan<char> name)
    {
        var index = 0;
        while (index < name.Length && (char.IsWhiteSpace(name[index]) || char.IsPunctuation(name[index]) || char.IsSeparator(name[index])))
            index++;
        return name[index..];
    }

    /// <summary>
    ///   Skips a movie marker a name starts with, when more of the title
    ///   follows it.
    /// </summary>
    /// <returns><c>true</c> when one was skipped.</returns>
    private static bool TrySkipMovieMarker(ReadOnlySpan<char> name, out ReadOnlySpan<char> rest)
    {
        const string kanji = "劇場版";
        rest = name;
        if (name.StartsWith(kanji, StringComparison.Ordinal))
        {
            if (SkipLeadingPunctuation(name[kanji.Length..]).IsEmpty)
                return false;
            rest = name[kanji.Length..];
            return true;
        }

        foreach (var marker in _sortNameMovieMarkers)
        {
            if (name.Length <= marker.Length || !name.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                continue;
            if (name[marker.Length] is not (' ' or ':' or '-' or '–' or '—' or '·' or '・' or '：') || SkipLeadingPunctuation(name[marker.Length..]).IsEmpty)
                return false;
            rest = name[marker.Length..];
            return true;
        }

        return false;
    }

    /// <summary>
    ///   Skips an article a name starts with and the space after it, when
    ///   more of the title follows.
    /// </summary>
    /// <returns><c>true</c> when one was skipped.</returns>
    private static bool TrySkipArticle(ReadOnlySpan<char> name, out ReadOnlySpan<char> rest)
    {
        rest = name;
        foreach (var article in _sortNameArticles)
        {
            if (name.Length <= article.Length + 1 || !name.StartsWith(article, StringComparison.OrdinalIgnoreCase) || name[article.Length] is not ' ')
                continue;
            rest = name[(article.Length + 1)..];
            return true;
        }

        return false;
    }
}
