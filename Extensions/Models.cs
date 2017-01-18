using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Server;

namespace Shoko.Commons.Extensions
{
    public static class Models
    {
        //TODO Move this to a cache Dictionary when time, memory consumption should be low but, who knows.
        private static Dictionary<string, HashSet<string>> _alltagscache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _alltitlescache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _hidecategoriescache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _plexuserscache = new Dictionary<string, HashSet<string>>();


        public static DateTime GetMessageDateAsDate(this Azure_AdminMessage message)
        {
            return TimeZone.CurrentTimeZone.ToLocalTime(AniDB.GetAniDBDateAsDate((int) message.MessageDate).Value);
        }

        public static bool GetHasMessageURL(this Azure_AdminMessage message)
        {
            return !String.IsNullOrEmpty(message.MessageURL);
        }

        public static string ToStringEx(this Azure_AdminMessage message)
        {
            return $"{message.AdminMessageId} - {message.GetMessageDateAsDate()} - {message.Message}";
        }

        public static double GetApprovalPercentage(this AniDB_Anime_Similar similar)
        {
            if (similar.Total == 0) return (double) 0;
            return (double) similar.Approval / (double) similar.Total * (double) 100;
        }

        public static enAnimeType GetAnimeTypeEnum(this AniDB_Anime anime)
        {
            if (anime.AnimeType > 5) return enAnimeType.Other;
            return (enAnimeType) anime.AnimeType;
        }

        public static bool GetFinishedAiring(this AniDB_Anime anime)
        {
            if (!anime.EndDate.HasValue) return false; // ongoing

            // all series have finished airing 
            if (anime.EndDate.Value < DateTime.Now) return true;

            return false;
        }

        public static bool GetIsTvDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkTvDB) > 0;
        }

        public static bool GetIsTraktLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkTrakt) > 0;
        }

        public static bool GetIsMALLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkMAL) > 0;
        }

        public static bool GetIsMovieDBLinkDisabled(this AniDB_Anime anime)
        {
            return (anime.DisableExternalLinksFlag & Constants.FlagLinkMovieDB) > 0;
        }

        public static int GetAirDateAsSeconds(this AniDB_Anime anime)
        {
            return AniDB.GetAniDBDateAsSeconds(anime.AirDate);
        }

        public static string GetAirDateFormatted(this AniDB_Anime anime)
        {
            return AniDB.GetAniDBDate(anime.GetAirDateAsSeconds());
        }

        public static void SetAnimeTypeRAW(this AniDB_Anime anime, string value)
        {
            anime.AnimeType = (int) RawToType(value);
        }

        public static string GetAnimeTypeRAW(this AniDB_Anime anime)
        {
            return ConvertToRAW((AnimeTypes) anime.AnimeType);
        }

        public static AnimeTypes RawToType(string raw)
        {
            switch (raw.ToLowerInvariant().Trim())
            {
                case "movie":
                    return AnimeTypes.Movie;
                case "ova":
                    return AnimeTypes.OVA;
                case "tv series":
                    return AnimeTypes.TV_Series;
                case "tv special":
                    return AnimeTypes.TV_Special;
                case "web":
                    return AnimeTypes.Web;
                default:
                    return AnimeTypes.Other;
            }
        }

        public static string ConvertToRAW(AnimeTypes t)
        {
            switch (t)
            {
                case AnimeTypes.Movie:
                    return "movie";
                case AnimeTypes.OVA:
                    return "ova";
                case AnimeTypes.TV_Series:
                    return "tv series";
                case AnimeTypes.TV_Special:
                    return "tv special";
                case AnimeTypes.Web:
                    return "web";
                default:
                    return "other";
            }
        }

        public static string GetAnimeTypeName(this AniDB_Anime anime)
        {
            return Enum.GetName(typeof(AnimeTypes), (AnimeTypes) anime.AnimeType).Replace('_', ' ');
        }

        public static HashSet<string> GetAllTags(this AniDB_Anime anime)
        {
            if (!String.IsNullOrEmpty(anime.AllTags))
            {
                lock (_alltagscache)
                {
                    if (!_alltagscache.ContainsKey(anime.AllTags))
                        _alltagscache[anime.AllTags] = new HashSet<string>(
                            anime.AllTags.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                    return _alltagscache[anime.AllTags];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static HashSet<string> GetAllTitles(this AniDB_Anime anime)
        {
            if (!String.IsNullOrEmpty(anime.AllTitles))
            {
                lock (_alltitlescache)
                {
                    if (!_alltitlescache.ContainsKey(anime.AllTitles))
                        _alltitlescache[anime.AllTitles] = new HashSet<string>(
                            anime.AllTitles.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)), StringComparer.InvariantCultureIgnoreCase);
                    return _alltitlescache[anime.AllTitles];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static bool GetSearchOnTvDB(this AniDB_Anime anime)
        {
            return anime.AnimeType != (int) AnimeTypes.Movie && !(anime.Restricted > 0);
        }

        public static bool GetSearchOnMovieDB(this AniDB_Anime anime)
        {
            return anime.AnimeType == (int) AnimeTypes.Movie && !(anime.Restricted > 0);
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

        public static int GetAniDBTotalVotes(this AniDB_Anime anime)
        {
            return anime.TempVoteCount + anime.VoteCount;
        }

        public static string ToStringEx(this AniDB_Anime anime)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AnimeID: " + anime.AnimeID);
            sb.Append(" | Main Title: " + anime.MainTitle);
            sb.Append(" | EpisodeCount: " + anime.EpisodeCount);
            sb.Append(" | AirDate: " + anime.AirDate);
            sb.Append(" | Picname: " + anime.Picname);
            sb.Append(" | Type: " + anime.GetAnimeTypeRAW());
            return sb.ToString();
        }

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

        public static DateTime? GetAirDateAsDate(this AniDB_Episode episode)
        {
            return AniDB.GetAniDBDateAsDate(episode.AirDate);
        }

        public static bool GetFutureDated(this AniDB_Episode episode)
        {
            if (!episode.GetAirDateAsDate().HasValue) return true;

            return episode.GetAirDateAsDate().Value > DateTime.Now;
        }

        public static enEpisodeType GetEpisodeTypeEnum(this AniDB_Episode episode)
        {
            return (enEpisodeType) episode.EpisodeType;
        }

        public static bool IsWatched(this AnimeEpisode_User epuser)
        {
            return epuser.WatchedCount > 0;
        }

        public static bool GetHasUnwatchedFiles(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount > 0;
        public static bool GetAllFilesWatched(this AnimeGroup_User grpuser) => grpuser.UnwatchedEpisodeCount == 0;
        public static bool GetAnyFilesWatched(this AnimeGroup_User grpuser) => grpuser.WatchedEpisodeCount > 0;

        public static bool GetHasMissingEpisodesAny(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCount > 0 || grp.MissingEpisodeCountGroups > 0;
        }

        public static bool GetHasMissingEpisodesGroups(this AnimeGroup gr)
        {
            return gr.MissingEpisodeCountGroups > 0;
        }

        public static bool GetHasMissingEpisodes(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCountGroups > 0;
        }

        public static GroupFilterConditionType GetConditionTypeEnum(this GroupFilterCondition grpf)
        {
            return (GroupFilterConditionType) grpf.ConditionType;
        }

        public static GroupFilterOperator GetConditionOperatorEnum(this GroupFilterCondition grpf)
        {
            return (GroupFilterOperator) grpf.ConditionOperator;
        }

        public static HashSet<string> GetHideCategories(this JMMUser user)
        {
            if (!String.IsNullOrEmpty(user.HideCategories))
            {
                lock (_hidecategoriescache)
                {
                    if (!_hidecategoriescache.ContainsKey(user.HideCategories))
                        _hidecategoriescache[user.HideCategories] = new HashSet<string>(
                            user.HideCategories.Trim().Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _hidecategoriescache[user.HideCategories];
                }

            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public static HashSet<string> GetPlexUsers(this JMMUser user)
        {
            if (!String.IsNullOrEmpty(user.PlexUsers))
            {
                lock (_plexuserscache)
                {
                    if (!_plexuserscache.ContainsKey(user.PlexUsers))
                        _plexuserscache[user.PlexUsers] = new HashSet<string>(
                            user.PlexUsers.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                .Select(a => a.Trim())
                                .Where(a => !String.IsNullOrEmpty(a)).Distinct(StringComparer.InvariantCultureIgnoreCase),
                            StringComparer.InvariantCultureIgnoreCase);
                    return _plexuserscache[user.PlexUsers];
                }
            }
            return new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
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
                    if (Int32.Parse(subRanges[0]) == episodeNumber) return true;
                }
                if (subRanges.Length == 2) // range
                {
                    if (episodeNumber >= Int32.Parse(subRanges[0]) && episodeNumber <= Int32.Parse(subRanges[1]))
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

        public static List<int> GetImportFolderList(this Scan scan)
        {
            return scan.ImportFolders.Split(',').Select(a => Int32.Parse(a)).ToList();
        }

        public static string GetTitleText(this Scan scan) => scan.CreationTIme.ToString(CultureInfo.CurrentUICulture) + " (" + scan.ImportFolders + ")";
        public static ScanFileStatus GetScanFileStatus(this ScanFile scanfile) => (ScanFileStatus) scanfile.Status;

        public static string GetStatusText(this ScanFile scanfile)
        {
            switch (scanfile.GetScanFileStatus())
            {
                case ScanFileStatus.Waiting:
                    return "Waiting";
                case ScanFileStatus.ErrorFileNotFound:
                    return "File Not Found";
                case ScanFileStatus.ErrorInvalidHash:
                    return "Hash do not match";
                case ScanFileStatus.ErrorInvalidSize:
                    return "Size do not match";
                case ScanFileStatus.ErrorMissingHash:
                    return "Missing Hash";
                case ScanFileStatus.ErrorIOError:
                    return "I/O Error";
                default:
                    return "Processed";
            }
        }

        public static bool HasMissingEpisodesAny(this AnimeGroup agroup)
        {
            return agroup.MissingEpisodeCount > 0 || agroup.MissingEpisodeCountGroups > 0;
        }

        public static bool HasMissingEpisodesGroups(this AnimeGroup agroup)
        {
            return agroup.MissingEpisodeCountGroups > 0;
        }

        public static bool HasMissingEpisodes(this AnimeGroup agroup)
        {
            return agroup.MissingEpisodeCountGroups > 0;
        }

        public const int LastYear = 2050;

        public static string GetYear(this AniDB_Anime anidbanime)
        {
            string y = anidbanime.BeginYear.ToString();
            if (anidbanime.BeginYear != anidbanime.EndYear)
            {
                if (anidbanime.EndYear == LastYear)
                    y += "-Ongoing";
                else
                    y += "-" + anidbanime.EndYear.ToString();
            }
            return y;
        }
    }
}
