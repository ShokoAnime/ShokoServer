using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Models;

namespace Shoko.Server
{
    public class GroupFilterHelper
    {
        public static string GetTextForEnum_Sorting(GroupFilterSorting sort)
        {
            switch (sort)
            {
                case GroupFilterSorting.AniDBRating:
                    return "AniDB Rating";
                case GroupFilterSorting.EpisodeAddedDate:
                    return "Episode Added Date";
                case GroupFilterSorting.EpisodeAirDate:
                    return "Episode Air Date";
                case GroupFilterSorting.EpisodeWatchedDate:
                    return "Episode Watched Date";
                case GroupFilterSorting.GroupName:
                    return "Group Name";
                case GroupFilterSorting.SortName:
                    return "Sort Name";
                case GroupFilterSorting.MissingEpisodeCount:
                    return "Missing Episode Count";
                case GroupFilterSorting.SeriesAddedDate:
                    return "Series Added Date";
                case GroupFilterSorting.SeriesCount:
                    return "Series Count";
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    return "Unwatched Episode Count";
                case GroupFilterSorting.UserRating:
                    return "User Rating";
                case GroupFilterSorting.Year:
                    return "Year";
                default:
                    return "AniDB Rating";
            }
        }

        public static GroupFilterSorting GetEnumForText_Sorting(string enumDesc)
        {
            switch (enumDesc)
            {
                case "AniDB Rating":
                    return GroupFilterSorting.AniDBRating;
                case "Episode Added Date":
                    return GroupFilterSorting.EpisodeAddedDate;
                case "Episode Air Date":
                    return GroupFilterSorting.EpisodeAirDate;
                case "Episode Watched Date":
                    return GroupFilterSorting.EpisodeWatchedDate;
                case "Group Name":
                    return GroupFilterSorting.GroupName;
                case "Sort Name":
                    return GroupFilterSorting.SortName;
                case "Missing Episode Count":
                    return GroupFilterSorting.MissingEpisodeCount;
                case "Series Added Date":
                    return GroupFilterSorting.SeriesAddedDate;
                case "Series Count":
                    return GroupFilterSorting.SeriesCount;
                case "Unwatched Episode Count":
                    return GroupFilterSorting.UnwatchedEpisodeCount;
                case "User Rating":
                    return GroupFilterSorting.UserRating;
                case "Year":
                    return GroupFilterSorting.Year;
                default:
                    return GroupFilterSorting.AniDBRating;
            }
        }

        public static string GetTextForEnum_SortDirection(GroupFilterSortDirection sort)
        {
            switch (sort)
            {
                case GroupFilterSortDirection.Asc:
                    return "Asc";
                case GroupFilterSortDirection.Desc:
                    return "Desc";
                default:
                    return "Asc";
            }
        }

        public static GroupFilterSortDirection GetEnumForText_SortDirection(string enumDesc)
        {
            switch (enumDesc)
            {
                case "Asc":
                    return GroupFilterSortDirection.Asc;
                case "Desc":
                    return GroupFilterSortDirection.Desc;
                default:
                    return GroupFilterSortDirection.Asc;
            }

        }

        public static List<string> GetAllSortTypes()
        {
            List<string> cons = new List<string>
            {
                GetTextForEnum_Sorting(GroupFilterSorting.AniDBRating),
                GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAddedDate),
                GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAirDate),
                GetTextForEnum_Sorting(GroupFilterSorting.EpisodeWatchedDate),
                GetTextForEnum_Sorting(GroupFilterSorting.GroupName),
                GetTextForEnum_Sorting(GroupFilterSorting.MissingEpisodeCount),
                GetTextForEnum_Sorting(GroupFilterSorting.SeriesAddedDate),
                GetTextForEnum_Sorting(GroupFilterSorting.SeriesCount),
                GetTextForEnum_Sorting(GroupFilterSorting.SortName),
                GetTextForEnum_Sorting(GroupFilterSorting.UnwatchedEpisodeCount),
                GetTextForEnum_Sorting(GroupFilterSorting.UserRating),
                GetTextForEnum_Sorting(GroupFilterSorting.Year)
            };
            cons.Sort();

            return cons;
        }

        public static List<string> GetQuickSortTypes()
        {
            List<string> cons = new List<string>
            {

                //GetTextForEnum_Sorting(GroupFilterSorting.AniDBRating); removed for performance reasons
                GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAddedDate),
                //GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAirDate);
                GetTextForEnum_Sorting(GroupFilterSorting.EpisodeWatchedDate),
                GetTextForEnum_Sorting(GroupFilterSorting.GroupName),
                GetTextForEnum_Sorting(GroupFilterSorting.MissingEpisodeCount),
                GetTextForEnum_Sorting(GroupFilterSorting.SeriesAddedDate),
                GetTextForEnum_Sorting(GroupFilterSorting.SeriesCount),
                GetTextForEnum_Sorting(GroupFilterSorting.SortName),
                GetTextForEnum_Sorting(GroupFilterSorting.UnwatchedEpisodeCount),
                GetTextForEnum_Sorting(GroupFilterSorting.UserRating),
                GetTextForEnum_Sorting(GroupFilterSorting.Year)
            };
            cons.Sort();

            return cons;
        }


        public static string GetDateAsString(DateTime aDate)
        {
            return aDate.Year.ToString().PadLeft(4, '0') +
                   aDate.Month.ToString().PadLeft(2, '0') +
                   aDate.Day.ToString().PadLeft(2, '0');
        }

        public static DateTime GetDateFromString(string sDate)
        {
            try
            {
                int year = int.Parse(sDate.Substring(0, 4));
                int month = int.Parse(sDate.Substring(4, 2));
                int day = int.Parse(sDate.Substring(6, 2));

                return new DateTime(year, month, day);
            }
            catch
            {
                return DateTime.Today;
            }
        }

        public static string GetDateAsFriendlyString(DateTime aDate)
        {
            return aDate.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        }

        public static IEnumerable<CL_AnimeGroup_User> Sort(IEnumerable<CL_AnimeGroup_User> groups, SVR_GroupFilter gf)
        {
            bool isfirst = true;
            IEnumerable<CL_AnimeGroup_User> query = groups;
            foreach (GroupFilterSortingCriteria gfsc in gf.SortCriteriaList)
            {
                query = Order(query, gfsc, isfirst);
                isfirst = false;
            }
            return query;
        }

        public static IOrderedEnumerable<CL_AnimeGroup_User> Order(IEnumerable<CL_AnimeGroup_User> groups,
            GroupFilterSortingCriteria gfsc, bool isfirst)
        {
            switch (gfsc.SortType)
            {
                case GroupFilterSorting.Year:
                    if (gfsc.SortDirection == GroupFilterSortDirection.Asc)
                        return Order(groups, a => a.Stat_AirDate_Min, gfsc.SortDirection, isfirst);
                    return Order(groups, a => a.Stat_AirDate_Max, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.AniDBRating:
                    return Order(groups, a => a.Stat_AniDBRating, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.EpisodeAddedDate:
                    return Order(groups, a => a.EpisodeAddedDate, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.EpisodeAirDate:
                    return Order(groups, a => a.LatestEpisodeAirDate, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.EpisodeWatchedDate:
                    return Order(groups, a => a.WatchedDate, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.MissingEpisodeCount:
                    return Order(groups, a => a.MissingEpisodeCount, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.SeriesAddedDate:
                    return Order(groups, a => a.Stat_SeriesCreatedDate, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.SeriesCount:
                    return Order(groups, a => a.Stat_SeriesCount, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.SortName:
                    return Order(groups, a => a.SortName, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    return Order(groups, a => a.UnwatchedEpisodeCount, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.UserRating:
                    return Order(groups, a => a.Stat_UserVoteOverall, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.GroupName:
                case GroupFilterSorting.GroupFilterName:
                    return Order(groups, a => a.GroupName, gfsc.SortDirection, isfirst);
                default:
                    return Order(groups, a => a.GroupName, gfsc.SortDirection, isfirst);
            }
        }

        private static IOrderedEnumerable<CL_AnimeGroup_User> Order<T>(IEnumerable<CL_AnimeGroup_User> groups,
            Func<CL_AnimeGroup_User, T> o,
            GroupFilterSortDirection direc, bool isfirst)
        {
            if (isfirst)
                return direc == GroupFilterSortDirection.Asc 
                    ? groups.OrderBy(o) 
                    : groups.OrderByDescending(o);
            return direc == GroupFilterSortDirection.Asc 
                ? ((IOrderedEnumerable<CL_AnimeGroup_User>) groups).ThenBy(o) 
                : ((IOrderedEnumerable<CL_AnimeGroup_User>) groups).ThenByDescending(o);
        }

        /*
        public static List<SortPropOrFieldAndDirection> GetSortDescriptions(GroupFilter gf)
        {
            List<SortPropOrFieldAndDirection> sortlist = new List<SortPropOrFieldAndDirection>();

            return sortlist;
        }

        public static SortPropOrFieldAndDirection GetSortDescription(GroupFilterSorting sortType,
            GroupFilterSortDirection sortDirection)
        {
            string sortColumn = string.Empty;
            bool sortDescending = sortDirection == GroupFilterSortDirection.Desc;
            SortType sortFieldType = SortType.eString;

            switch (sortType)
            {
                case GroupFilterSorting.AniDBRating:
                    sortColumn = "AniDBRating";
                    sortFieldType = SortType.eDoubleOrFloat;
                    break;
                case GroupFilterSorting.EpisodeAddedDate:
                    sortColumn = "EpisodeAddedDate";
                    sortFieldType = SortType.eDateTime;
                    break;
                case GroupFilterSorting.EpisodeAirDate:
                    sortColumn = "LatestEpisodeAirDate";
                    sortFieldType = SortType.eDateTime;
                    break;
                case GroupFilterSorting.EpisodeWatchedDate:
                    sortColumn = "WatchedDate";
                    sortFieldType = SortType.eDateTime;
                    break;
                case GroupFilterSorting.GroupName:
                    sortColumn = "GroupName";
                    sortFieldType = SortType.eString;
                    break;
                case GroupFilterSorting.SortName:
                    sortColumn = "SortName";
                    sortFieldType = SortType.eString;
                    break;
                case GroupFilterSorting.MissingEpisodeCount:
                    sortColumn = "MissingEpisodeCount";
                    sortFieldType = SortType.eInteger;
                    break;
                case GroupFilterSorting.SeriesAddedDate:
                    sortColumn = "Stat_SeriesCreatedDate";
                    sortFieldType = SortType.eDateTime;
                    break;
                case GroupFilterSorting.SeriesCount:
                    sortColumn = "AllSeriesCount";
                    sortFieldType = SortType.eInteger;
                    break;
                case GroupFilterSorting.UnwatchedEpisodeCount:
                    sortColumn = "UnwatchedEpisodeCount";
                    sortFieldType = SortType.eInteger;
                    break;
                case GroupFilterSorting.UserRating:
                    sortColumn = "Stat_UserVoteOverall";
                    sortFieldType = SortType.eDoubleOrFloat;
                    break;
                case GroupFilterSorting.Year:
                    if (sortDirection == GroupFilterSortDirection.Asc)
                        sortColumn = "Stat_AirDate_Min";
                    else
                        sortColumn = "Stat_AirDate_Max";

                    sortFieldType = SortType.eDateTime;
                    break;
                default:
                    sortColumn = "GroupName";
                    sortFieldType = SortType.eString;
                    break;
            }


            return new SortPropOrFieldAndDirection(sortColumn, sortDescending, sortFieldType);
        }
        */
    }
}