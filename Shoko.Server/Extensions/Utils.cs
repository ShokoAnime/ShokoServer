using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Commons;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;

namespace Shoko.Server.Extensions
{
    public static class Utils
    {
        public static bool Contains(this string item, string other, StringComparison comparer)
        {
            return item.IndexOf(other, comparer) >= 0;
        }

        public static void ShallowCopyTo(this object s, object d)
        {
            foreach (PropertyInfo pis in s.GetType().GetProperties())
            {
                foreach (PropertyInfo pid in d.GetType().GetProperties())
                {
                    if (pid.Name == pis.Name)
                        pid.GetSetMethod().Invoke(d, new[] {pis.GetGetMethod().Invoke(s, null)});
                }
            }
            ;
        }

        public static void AddRange<K, V>(this Dictionary<K, V> dict, Dictionary<K, V> otherdict)
        {
            otherdict.ForEach(a =>
            {
                if (!dict.ContainsKey(a.Key)) dict.Add(a.Key, a.Value);
            });
        }

        public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
        {
            // Trim, to lower in both lists, remove null and empty strings
            HashSet<string> listhash = list.Select(a => a.ToLowerInvariant().Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> itemhash = items.Select(a => a.ToLowerInvariant().Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            return listhash.Overlaps(itemhash);
        }

        public static bool FindInEnumerable(this IEnumerable<int> items, IEnumerable<int> list)
        {
            return list.ToHashSet().Overlaps(items.ToHashSet());
        }

        public static bool FindIn(this string item, IEnumerable<string> list)
        {
            return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
        }

        public static int? ParseNullableInt(this string input)
        {
            return int.TryParse(input, out int output) ? output : (int?)null;
        }

        public static CL_AnimeGroup_User DeepCopy(this CL_AnimeGroup_User c)
        {
            CL_AnimeGroup_User contract = new CL_AnimeGroup_User(new SeasonComparator())
            {
                AnimeGroupID = c.AnimeGroupID,
                AnimeGroupParentID = c.AnimeGroupParentID,
                DefaultAnimeSeriesID = c.DefaultAnimeSeriesID,
                GroupName = c.GroupName,
                Description = c.Description,
                IsFave = c.IsFave,
                IsManuallyNamed = c.IsManuallyNamed,
                UnwatchedEpisodeCount = c.UnwatchedEpisodeCount,
                DateTimeUpdated = c.DateTimeUpdated,
                WatchedEpisodeCount = c.WatchedEpisodeCount,
                SortName = c.SortName,
                WatchedDate = c.WatchedDate,
                EpisodeAddedDate = c.EpisodeAddedDate,
                LatestEpisodeAirDate = c.LatestEpisodeAirDate,
                PlayedCount = c.PlayedCount,
                WatchedCount = c.WatchedCount,
                StoppedCount = c.StoppedCount,
                OverrideDescription = c.OverrideDescription,
                MissingEpisodeCount = c.MissingEpisodeCount,
                MissingEpisodeCountGroups = c.MissingEpisodeCountGroups,
                Stat_AirDate_Min = c.Stat_AirDate_Min,
                Stat_AirDate_Max = c.Stat_AirDate_Max,
                Stat_EndDate = c.Stat_EndDate,
                Stat_SeriesCreatedDate = c.Stat_SeriesCreatedDate,
                Stat_UserVotePermanent = c.Stat_UserVotePermanent,
                Stat_UserVoteTemporary = c.Stat_UserVoteTemporary,
                Stat_UserVoteOverall = c.Stat_UserVoteOverall,
                Stat_IsComplete = c.Stat_IsComplete,
                Stat_HasFinishedAiring = c.Stat_HasFinishedAiring,
                Stat_IsCurrentlyAiring = c.Stat_IsCurrentlyAiring,
                Stat_HasTvDBLink = c.Stat_HasTvDBLink,
                Stat_HasMALLink = c.Stat_HasMALLink,
                Stat_HasMovieDBLink = c.Stat_HasMovieDBLink,
                Stat_HasMovieDBOrTvDBLink = c.Stat_HasMovieDBOrTvDBLink,
                Stat_SeriesCount = c.Stat_SeriesCount,
                Stat_EpisodeCount = c.Stat_EpisodeCount,
                Stat_AniDBRating = c.Stat_AniDBRating,
                ServerPosterPath = c.ServerPosterPath,
                SeriesForNameOverride = c.SeriesForNameOverride,

                Stat_AllCustomTags =
                new HashSet<string>(c.Stat_AllCustomTags, StringComparer.InvariantCultureIgnoreCase),
                Stat_AllTags = new HashSet<string>(c.Stat_AllTags, StringComparer.InvariantCultureIgnoreCase),
                Stat_AllYears = new HashSet<int>(c.Stat_AllYears),
                Stat_AllTitles = new HashSet<string>(c.Stat_AllTitles, StringComparer.InvariantCultureIgnoreCase),
                Stat_AnimeTypes = new HashSet<string>(c.Stat_AnimeTypes,
                StringComparer.InvariantCultureIgnoreCase),
                Stat_AllVideoQuality =
                new HashSet<string>(c.Stat_AllVideoQuality, StringComparer.InvariantCultureIgnoreCase),
                Stat_AllVideoQuality_Episodes = new HashSet<string>(c.Stat_AllVideoQuality_Episodes,
                StringComparer.InvariantCultureIgnoreCase),
                Stat_AudioLanguages =
                new HashSet<string>(c.Stat_AudioLanguages, StringComparer.InvariantCultureIgnoreCase),
                Stat_SubtitleLanguages = new HashSet<string>(c.Stat_SubtitleLanguages,
                StringComparer.InvariantCultureIgnoreCase)
            };
            return contract;
        }
    }
}