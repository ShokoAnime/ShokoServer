using System;
using System.Collections.Generic;
using System.Linq;
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
