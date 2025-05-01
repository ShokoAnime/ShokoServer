using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB;
using AnimeType = Shoko.Models.Enums.AnimeType;

#nullable enable
namespace Shoko.Server.Extensions
{
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

            DateTime startDate = anime.AirDate.Value;

            // started after the year has ended
            if (startDate.Year > year) return false;

            if (startDate.Year == year)
            {
                // It started in the year, but nowhere near the end
                if (startDate.Month < 12) return true;

                // implied startDate.Month == 12, unless the calendar changes...
                // if it's a movie or short series, count it
                if (anime.AnimeType == (int)AnimeType.Movie || anime.EpisodeCountNormal <= 6) return true;
            }

            // starts before the year, but continues through it
            if (startDate.Year < year)
            {
                // still airing or finished after the year has been started, with some time for late seasons
                if (anime.EndDate == null || anime.EndDate.Value >= new DateTime(year, 2, 1)) return true;
            }

            return false;
        }

        public static DateOnly ToDateOnly(this DateTime date)
            => DateOnly.FromDateTime(date);

        public static IEnumerable<(int Year, AnimeSeason Season)> GetYearlySeasons(this DateTime? startDate, DateTime? endDate = null)
            => GetYearlySeasons(startDate?.ToDateOnly(), endDate?.ToDateOnly());

        public static IEnumerable<(int Year, AnimeSeason Season)> GetYearlySeasons(this DateOnly startDate, DateOnly endDate)
            => GetYearlySeasons((DateOnly?)startDate, (DateOnly?)endDate);

        public static IEnumerable<(int Year, AnimeSeason Season)> GetYearlySeasons(this DateOnly? startDate, DateOnly? endDate = null)
        {
            if (startDate == null) yield break;
            var beginYear = startDate.Value.Year;
            var endYear = endDate?.Year ?? DateTime.Today.Year;
            for (var year = beginYear; year <= endYear; year++)
            {
                if (beginYear < year && year < endYear)
                {
                    yield return (year, AnimeSeason.Winter);
                    yield return (year, AnimeSeason.Spring);
                    yield return (year, AnimeSeason.Summer);
                    yield return (year, AnimeSeason.Fall);
                    continue;
                }
                if (IsInSeason(startDate.Value, endDate, AnimeSeason.Winter, year))
                    yield return (year, AnimeSeason.Winter);
                if (IsInSeason(startDate.Value, endDate, AnimeSeason.Spring, year))
                    yield return (year, AnimeSeason.Spring);
                if (IsInSeason(startDate.Value, endDate, AnimeSeason.Summer, year))
                    yield return (year, AnimeSeason.Summer);
                if (IsInSeason(startDate.Value, endDate, AnimeSeason.Fall, year))
                    yield return (year, AnimeSeason.Fall);
            }
        }

        // because series don't all start on the same day, we have a buffer from the start and end of the season
        private const int BufferDays = 23; // 75% of 30 days.

        private static bool IsInSeason(DateOnly startDate, DateOnly? endDate, AnimeSeason season, int year)
        {
            DateOnly seasonStart;
            DateOnly seasonStartBegin;
            DateOnly seasonStartEnd;
            switch (season)
            {
                case AnimeSeason.Winter:
                    // January - buffer to April
                    seasonStart = new(year, 1, 1);
                    seasonStartBegin = seasonStart.AddDays(-BufferDays);
                    seasonStartEnd = new(year, 4, 1);
                    break;
                case AnimeSeason.Spring:
                    // April - buffer to July
                    seasonStart = new(year, 4, 1);
                    seasonStartBegin = seasonStart.AddDays(-BufferDays);
                    seasonStartEnd = new(year, 7, 1);
                    break;
                case AnimeSeason.Summer:
                    // July - buffer to October
                    seasonStart = new(year, 7, 1);
                    seasonStartBegin = seasonStart.AddDays(-BufferDays);
                    seasonStartEnd = new(year, 10, 1);
                    break;
                case AnimeSeason.Fall:
                    // October - buffer to January (next year)
                    seasonStart = new(year, 10, 1);
                    seasonStartBegin = seasonStart.AddDays(-BufferDays);
                    seasonStartEnd = new(year + 1, 1, 1);
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
        {
            if (string.IsNullOrEmpty(anime.AllTags)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            return new HashSet<string>(anime.AllTags.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.InvariantCultureIgnoreCase);
        }

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

        public static DateTime? GetAirDateAsDate(this AniDB_Episode episode) => AniDBExtensions.GetAniDBDateAsDate(episode.AirDate);

        public static HashSet<string> GetHideCategories(this JMMUser user)
        {
            if (string.IsNullOrEmpty(user.HideCategories)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            return new HashSet<string>(user.HideCategories.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.InvariantCultureIgnoreCase);
        }

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

        public static bool IsAdminUser(this JMMUser JMMUser) => JMMUser.IsAdmin == 1;

        public static string ToSortName(this string name)
        {
            if (name.StartsWith("A ")) name = name[2..];
            else if (name.StartsWith("An ")) name = name[3..];
            else if (name.StartsWith("The ")) name = name[4..];
            return name;
        }
    }
}
