using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NutzCode.CloudFileSystem;
using FluentNHibernate.Utils;
using Shoko.Server.Collections;
using Shoko.Models.Client;

namespace Shoko.Server
{
    public static class Extensions
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

	    public static bool FindInEnumerable(this IEnumerable<string> items, IEnumerable<string> list)
	    {
		    // Trim, to lower in both lists, remove null and empty strings
		    HashSet<string> listhash = list.Select(a => a.ToLowerInvariant().Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
		    HashSet<string> itemhash = items.Select(a => a.ToLowerInvariant().Trim()).Where(a => !string.IsNullOrWhiteSpace(a)).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
		    return listhash.Overlaps(itemhash);
	    }

	    public static bool FindIn(this string item, IEnumerable<string> list)
        {
            return list.Contains(item, StringComparer.InvariantCultureIgnoreCase);
        }

	    public static int? ParseNullableInt(this string input)
	    {
		    int output;
		    return int.TryParse(input, out output) ? output : (int?) null;
	    }

        public static CL_AnimeGroup_User DeepCopy(this CL_AnimeGroup_User c)
        {
            CL_AnimeGroup_User contract = new CL_AnimeGroup_User();
            contract.AnimeGroupID = c.AnimeGroupID;
            contract.AnimeGroupParentID = c.AnimeGroupParentID;
            contract.DefaultAnimeSeriesID = c.DefaultAnimeSeriesID;
            contract.GroupName = c.GroupName;
            contract.Description = c.Description;
            contract.IsFave = c.IsFave;
            contract.IsManuallyNamed = c.IsManuallyNamed;
            contract.UnwatchedEpisodeCount = c.UnwatchedEpisodeCount;
            contract.DateTimeUpdated = c.DateTimeUpdated;
            contract.WatchedEpisodeCount = c.WatchedEpisodeCount;
            contract.SortName = c.SortName;
            contract.WatchedDate = c.WatchedDate;
            contract.EpisodeAddedDate = c.EpisodeAddedDate;
            contract.LatestEpisodeAirDate = c.LatestEpisodeAirDate;
            contract.PlayedCount = c.PlayedCount;
            contract.WatchedCount = c.WatchedCount;
            contract.StoppedCount = c.StoppedCount;
            contract.OverrideDescription = c.OverrideDescription;
            contract.MissingEpisodeCount = c.MissingEpisodeCount;
            contract.MissingEpisodeCountGroups = c.MissingEpisodeCountGroups;
            contract.Stat_AirDate_Min = c.Stat_AirDate_Min;
            contract.Stat_AirDate_Max = c.Stat_AirDate_Max;
            contract.Stat_EndDate = c.Stat_EndDate;
            contract.Stat_SeriesCreatedDate = c.Stat_SeriesCreatedDate;
            contract.Stat_UserVotePermanent = c.Stat_UserVotePermanent;
            contract.Stat_UserVoteTemporary = c.Stat_UserVoteTemporary;
            contract.Stat_UserVoteOverall = c.Stat_UserVoteOverall;
            contract.Stat_IsComplete = c.Stat_IsComplete;
            contract.Stat_HasFinishedAiring = c.Stat_HasFinishedAiring;
            contract.Stat_IsCurrentlyAiring = c.Stat_IsCurrentlyAiring;
            contract.Stat_HasTvDBLink = c.Stat_HasTvDBLink;
            contract.Stat_HasMALLink = c.Stat_HasMALLink;
            contract.Stat_HasMovieDBLink = c.Stat_HasMovieDBLink;
            contract.Stat_HasMovieDBOrTvDBLink = c.Stat_HasMovieDBOrTvDBLink;
            contract.Stat_SeriesCount = c.Stat_SeriesCount;
            contract.Stat_EpisodeCount = c.Stat_EpisodeCount;
            contract.Stat_AniDBRating = c.Stat_AniDBRating;
            contract.ServerPosterPath = c.ServerPosterPath;
            contract.SeriesForNameOverride = c.SeriesForNameOverride;

            contract.Stat_AllCustomTags = new HashSet<string>(c.Stat_AllCustomTags,StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_AllTags = new HashSet<string>(c.Stat_AllTags, StringComparer.InvariantCultureIgnoreCase);
	        contract.Stat_AllYears = new HashSet<int>(c.Stat_AllYears);
	        contract.Stat_AllTitles = new HashSet<string>(c.Stat_AllTitles, StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_AnimeTypes = new HashSet<string>(c.Stat_AnimeTypes, StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_AllVideoQuality = new HashSet<string>(c.Stat_AllVideoQuality, StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(c.Stat_AllVideoQuality_Episodes, StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_AudioLanguages = new HashSet<string>(c.Stat_AudioLanguages, StringComparer.InvariantCultureIgnoreCase);
            contract.Stat_SubtitleLanguages = new HashSet<string>(c.Stat_SubtitleLanguages, StringComparer.InvariantCultureIgnoreCase);
            return contract;
        }

        public static BitmapImage CreateIconImage(this ICloudPlugin plugin)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                if (plugin?.Icon == null)
                    return null;

                MemoryStream ms = new MemoryStream(plugin.Icon);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage icon = new BitmapImage();
                icon.BeginInit();
                icon.StreamSource = ms;
                icon.EndInit();
                return icon;
            });
        }
    }
}