using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Extensions;

public static class Models
{
    public static bool GetFinishedAiring(this AniDB_Anime anime)
    {
        if (!anime.EndDate.HasValue) return false; // ongoing

        // all series have finished airing 
        if (anime.EndDate.Value < DateTime.Now) return true;

        return false;
    }

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
        /// Resolves the effective end date to use for year/season calculations: <see cref="AniDB_Anime.EndDate"/>
        /// if known, otherwise <see cref="AniDB_Anime.AirDate"/> for anime types that don't have an ongoing
        /// broadcast (Movie, OVA, Web, Other, MusicVideo), otherwise <see langword="null"/> (still airing) for
        /// TV series/specials.
        /// </summary>
        public PartialDateOnly? EffectiveEndDateForSeasons
            => anime.EndDate ?? (s_animeTypesWithoutOngoingReleases.Contains(anime.AnimeType) ? anime.AirDate : null);
    }

    public static IEnumerable<(int Year, YearlySeason Season)> GetYearlySeasons(this DateTime? startDate, DateTime? endDate = null)
        => (startDate?.ToDateOnly()).GetYearlySeasons(endDate?.ToDateOnly());

    public static IEnumerable<(int Year, YearlySeason Season)> GetYearlySeasons(this PartialDateOnly? startDate, PartialDateOnly? endDate)
        => (startDate?.ToDateOnly()).GetYearlySeasons(endDate?.ToDateOnly());

    public static IEnumerable<(int Year, YearlySeason Season)> GetYearlySeasons(this PartialDateOnly startDate, PartialDateOnly endDate)
        => ((DateOnly?)startDate.ToDateOnly()).GetYearlySeasons(endDate.ToDateOnly());

    public static IEnumerable<(int Year, YearlySeason Season)> GetYearlySeasons(this DateOnly startDate, DateOnly endDate)
        => ((DateOnly?)startDate).GetYearlySeasons(endDate);

    public static IEnumerable<(int Year, YearlySeason Season)> GetYearlySeasons(this DateOnly? startDate, DateOnly? endDate = null)
    {
        if (startDate == null) yield break;
        var beginYear = startDate.Value.Year;
        var endYear = endDate?.Year ?? DateTime.Today.Year;
        // Start one year early and end one year late so the buffered tail of the previous year's Fall
        // (which reaches into January of beginYear) and the buffered head of the next year's Winter
        // (which reaches back into December of endYear) are evaluated too, mirroring the Winter/Spring/
        // Summer/Fall buffers at every other quarter boundary; IsInSeason naturally returns false when it
        // doesn't apply.
        var loopStart = Math.Max(1900, beginYear - 1);
        var loopEnd = Math.Min(9999, endYear + 1);
        for (var year = loopStart; year <= loopEnd; year++)
        {
            if (beginYear < year && year < endYear)
            {
                yield return (year, YearlySeason.Winter);
                yield return (year, YearlySeason.Spring);
                yield return (year, YearlySeason.Summer);
                yield return (year, YearlySeason.Fall);
                continue;
            }
            if (IsInSeason(startDate.Value, endDate, YearlySeason.Winter, year))
                yield return (year, YearlySeason.Winter);
            if (IsInSeason(startDate.Value, endDate, YearlySeason.Spring, year))
                yield return (year, YearlySeason.Spring);
            if (IsInSeason(startDate.Value, endDate, YearlySeason.Summer, year))
                yield return (year, YearlySeason.Summer);
            if (IsInSeason(startDate.Value, endDate, YearlySeason.Fall, year))
                yield return (year, YearlySeason.Fall);
        }
    }

    // because series don't all start on the same day, we have a buffer from the start and end of the season
    private const int BufferDays = 23; // 75% of 30 days.

    private static bool IsInSeason(DateOnly startDate, DateOnly? endDate, YearlySeason season, int year)
    {
        DateOnly seasonStart;
        DateOnly seasonStartBegin;
        DateOnly seasonStartEnd;
        switch (season)
        {
            case YearlySeason.Winter:
                // January (starts 1w early), runs until Spring's own buffered start
                seasonStart = new(year - 1, 12, 25);
                seasonStartBegin = seasonStart.AddDays(-BufferDays);
                seasonStartEnd = new DateOnly(year, 3, 25).AddDays(-BufferDays);
                break;
            case YearlySeason.Spring:
                // April (starts 1w early), runs until Summer's own buffered start
                seasonStart = new(year, 3, 25);
                seasonStartBegin = seasonStart.AddDays(-BufferDays);
                seasonStartEnd = new DateOnly(year, 6, 24).AddDays(-BufferDays);
                break;
            case YearlySeason.Summer:
                // July (starts 1w early), runs until Fall's own buffered start
                seasonStart = new(year, 6, 24);
                seasonStartBegin = seasonStart.AddDays(-BufferDays);
                seasonStartEnd = new DateOnly(year, 9, 24).AddDays(-BufferDays);
                break;
            case YearlySeason.Fall:
                // October (starts 1w early), runs until next year's Winter's own buffered start
                seasonStart = new(year, 9, 24);
                seasonStartBegin = seasonStart.AddDays(-BufferDays);
                seasonStartEnd = new DateOnly(year, 12, 25).AddDays(-BufferDays);
                break;
            default:
                return false;
        }
        // Don't even count seasons that haven't happened yet
        if (seasonStartBegin > DateTime.Today.ToDateOnly()) return false;

        // If it starts in a season, then it is definitely going to be in it
        if (startDate >= seasonStartBegin && startDate <= seasonStartEnd) return true;
        // If it aired before the season, but hasn't finished by the time the season has started, count it.
        if (startDate < seasonStartBegin)
        {
            // null EndDate means it's still airing now
            if (endDate == null) return true;
            // A season can run long, so don't count it unless it continues well into the season (buffer * 2)
            if (endDate.Value > seasonStart.AddDays(BufferDays * 2)) return true;
        }

        return false;
    }

    public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        => anime.GetAllTagsSet();

    public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
    {
        if (string.IsNullOrEmpty(anime.AllTitles)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        return new HashSet<string>(anime.AllTitles.Split('|').Select(a => a.Trim()), StringComparer.InvariantCultureIgnoreCase);
    }

    public static double GetApprovalPercentage(this AniDB_Anime_Similar similar)
    {
        if (similar.Total == 0) return 0;
        return similar.Approval / (double)similar.Total * 100;
    }

    public static decimal GetAniDBRating(this AniDB_Anime anime)
    {
        if (anime.GetAniDBTotalVotes() == 0)
            return 0;
        return anime.GetAniDBTotalRating() / anime.GetAniDBTotalVotes();
    }

    public static decimal GetAniDBTotalRating(this AniDB_Anime anime)
    {
        decimal totalRating = 0;
        totalRating += (decimal)anime.Rating * anime.VoteCount;
        totalRating += (decimal)anime.TempRating * anime.TempVoteCount;
        return totalRating;
    }

    public static int GetAniDBTotalVotes(this AniDB_Anime anime) => anime.TempVoteCount + anime.VoteCount;

    public static HashSet<string> GetPlexUsers(this JMMUser user)
    {
        if (string.IsNullOrEmpty(user.PlexUsers)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        return new HashSet<string>(user.PlexUsers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.InvariantCultureIgnoreCase);
    }

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

    public static bool IsAdminUser(this JMMUser user) => user.IsAdmin == 1;

    public static string ToSortName(this string name)
    {
        if (name.StartsWith("A ", StringComparison.InvariantCulture)) name = name[2..];
        else if (name.StartsWith("An ", StringComparison.InvariantCulture)) name = name[3..];
        else if (name.StartsWith("The ", StringComparison.InvariantCulture)) name = name[4..];
        return name;
    }
}
