using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Utils;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class Models
    {
        //TODO Move this to a cache Dictionary when time, memory consumption should be low but, who knows.
        private static Dictionary<int, HashSet<string>> _alltagscache = new();
        private static Dictionary<int, HashSet<string>> _alltitlescache = new();
        private static Dictionary<int, HashSet<string>> _hidecategoriescache = new();
        private static Dictionary<string, HashSet<string>> _plexuserscache = new();

        public static List<T> CastList<T>(this IEnumerable<dynamic> list) => list?.Cast<T>().ToList();

        public static AnimeType GetAnimeTypeEnum(this AniDB_Anime anime)
        {
            if (anime.AnimeType > 5) return AnimeType.Other;
            return (AnimeType) anime.AnimeType;
        }

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

        public static bool IsInSeason(this AniDB_Anime anime, AnimeSeason season, int year)
        {
            if (anime.AirDate == null) return false;
            // If it isn't a normal series, then it won't adhere to standard airing norms
            if (anime.AnimeType != (int) AnimeType.TVSeries && anime.AnimeType != (int) AnimeType.Web) return false;
            return IsInSeason(anime.AirDate.Value, anime.EndDate, season, year);
        }

        public static bool IsInSeason(DateTime startDate, DateTime? endDate, AnimeSeason season, int year)
        {
            DateTime seasonStartBegin;
            DateTime seasonStartEnd;
            // because series don't all start on the same day, we have a buffer from the start and end of the season
            const double Buffer = 0.75D;
            var days = (int)Math.Ceiling(Buffer * 30);
            DateTime seasonStart;
            switch (season)
            {
                case AnimeSeason.Winter:
                    // January +- buffer
                    seasonStart = new DateTime(year, 1, 1);
                    seasonStartBegin = seasonStart.AddDays(-days);
                    seasonStartEnd = seasonStart.AddDays(days);
                    break;
                case AnimeSeason.Spring:
                    // April +- buffer
                    seasonStart = new DateTime(year, 4, 1);
                    seasonStartBegin = seasonStart.AddDays(-days);
                    seasonStartEnd = seasonStart.AddDays(days);
                    break;
                case AnimeSeason.Summer:
                    // July +- buffer
                    seasonStart = new DateTime(year, 7, 1);
                    seasonStartBegin = seasonStart.AddDays(-days);
                    seasonStartEnd = seasonStart.AddDays(days);
                    break;
                case AnimeSeason.Fall:
                    // October +- buffer
                    seasonStart = new DateTime(year, 10, 1);
                    seasonStartBegin = seasonStart.AddDays(-days);
                    seasonStartEnd = seasonStart.AddDays(days);
                    break;
                default:
                    return false;
            }
            // Don't even count seasons that haven't happened yet
            if (seasonStartBegin > DateTime.Today) return false;

            // If it starts in a season, then it is definitely going to be in it
            if (startDate >= seasonStartBegin && startDate <= seasonStartEnd) return true;
            // If it aired before the season, but hasn't finished by the time the season has started, count it.
            if (startDate < seasonStartBegin)
            {
                // null EndDate means it's still airing now
                if (endDate == null) return true;
                // A season can run long, so don't count it unless it continues well into the season (buffer * 2)
                if (endDate.Value > seasonStart.AddDays(days * 2)) return true;
            }

            return false;
        }

        public static int GetAirDateAsSeconds(this AniDB_Anime anime) => AniDB.GetAniDBDateAsSeconds(anime.AirDate);

        public static string GetAnimeTypeRAW(this AniDB_Anime anime) => ConvertToRAW((AnimeType) anime.AnimeType);

        public static AnimeType RawToType(string raw)
        {
            switch (raw.ToLowerInvariant().Trim())
            {
                case "movie":
                    return AnimeType.Movie;
                case "ova":
                    return AnimeType.OVA;
                case "tv series":
                    return AnimeType.TVSeries;
                case "tv special":
                    return AnimeType.TVSpecial;
                case "web":
                    return AnimeType.Web;
                default:
                    return AnimeType.Other;
            }
        }

        public static string ConvertToRAW(AnimeType t)
        {
            switch (t)
            {
                case AnimeType.Movie:
                    return "movie";
                case AnimeType.OVA:
                    return "ova";
                case AnimeType.TVSeries:
                    return "tv series";
                case AnimeType.TVSpecial:
                    return "tv special";
                case AnimeType.Web:
                    return "web";
                default:
                    return "other";
            }
        }

        public static string GetAnimeTypeName(this AniDB_Anime anime)
        {
            return Enum.GetName(typeof(AnimeType), (AnimeType) anime.AnimeType).Replace('_', ' ');
        }

        public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        {
            if (string.IsNullOrEmpty(anime.AllTags)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            lock (_alltagscache)
            {
                if (!_alltagscache.ContainsKey(anime.AnimeID))
                    _alltagscache[anime.AnimeID] = new HashSet<string>(
                        anime.AllTags.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                                .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                return _alltagscache[anime.AnimeID];
            }
        }

        public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
        {
            if (string.IsNullOrEmpty(anime.AllTitles)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            lock (_alltitlescache)
            {
                if (!_alltitlescache.ContainsKey(anime.AnimeID))
                    _alltitlescache[anime.AnimeID] = new HashSet<string>(
                        anime.AllTitles.Split('|')
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                return _alltitlescache[anime.AnimeID];
            }
        }

        public static decimal GetAniDBRating(this AniDB_Anime anime)
        {
            if (anime.GetAniDBTotalVotes() == 0)
                return 0;
            return anime.GetAniDBTotalRating() / (decimal) anime.GetAniDBTotalVotes();
        }

        public static decimal GetAniDBTotalRating(this AniDB_Anime anime)
        {
            decimal totalRating = 0;
            totalRating += (decimal) anime.Rating * anime.VoteCount;
            totalRating += (decimal) anime.TempRating * anime.TempVoteCount;
            return totalRating;
        }

        public static int GetAniDBTotalVotes(this AniDB_Anime anime) => anime.TempVoteCount + anime.VoteCount;

        public static string GetAirDateFormatted(this AniDB_Episode episode)
        {
            try
            {
                return AniDB.GetAniDBDate(episode.AirDate);
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static DateTime? GetAirDateAsDate(this AniDB_Episode episode) => AniDB.GetAniDBDateAsDate(episode.AirDate);

        public static EpisodeType GetEpisodeTypeEnum(this AniDB_Episode episode) => (EpisodeType) episode.EpisodeType;

        public static bool IsWatched(this AnimeEpisode_User epuser) => epuser.WatchedCount > 0;


        public static HashSet<string> GetHideCategories(this JMMUser user)
        {
            if (string.IsNullOrEmpty(user.HideCategories)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            lock (_hidecategoriescache)
            {
                if (!_hidecategoriescache.ContainsKey(user.JMMUserID))
                    _hidecategoriescache[user.JMMUserID] = new HashSet<string>(
                        user.HideCategories.Trim().Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
                return _hidecategoriescache[user.JMMUserID];
            }
        }

        public static void InvalidateHideCategoriesCache(this JMMUser user)
        {
            lock (_hidecategoriescache)
            {
                _hidecategoriescache.Remove(user.JMMUserID);
            }
        }

        public static HashSet<string> GetPlexUsers(this JMMUser user)
        {
            if (string.IsNullOrEmpty(user.PlexUsers)) return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            lock (_plexuserscache)
            {
                if (!_plexuserscache.ContainsKey(user.PlexUsers))
                    _plexuserscache[user.PlexUsers] = new HashSet<string>(
                        user.PlexUsers.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                        StringComparer.InvariantCultureIgnoreCase);
                return _plexuserscache[user.PlexUsers];
            }
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

        public static ScanStatus GetScanStatus(this Scan scan) => (ScanStatus) scan.Status;

        public static string GetStatusText(this Scan scan)
        {
            switch (scan.GetScanStatus())
            {
                case ScanStatus.Finish:
                    return "Finished";
                case ScanStatus.Running:
                    return "Running";
                default:
                    return "Standby";
            }
        }

        public static List<int> GetImportFolderList(this Scan scan) => scan.ImportFolders.Split(',').Select(a => int.Parse(a)).ToList();

        public static ScanFileStatus GetScanFileStatus(this ScanFile scanfile) => (ScanFileStatus) scanfile.Status;

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
