using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FluentNHibernate.MappingModel.Collections;
using JMMServer.Entities;
using Shoko.Models.Server;
using NHibernate;
using NHibernate.Persister.Entity;
using Shoko.Models;
using Shoko.Models.Client;

namespace JMMServer
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
            if (enumDesc == "AniDB Rating") return GroupFilterSorting.AniDBRating;
            if (enumDesc == "Episode Added Date") return GroupFilterSorting.EpisodeAddedDate;
            if (enumDesc == "Episode Air Date") return GroupFilterSorting.EpisodeAirDate;
            if (enumDesc == "Episode Watched Date") return GroupFilterSorting.EpisodeWatchedDate;
            if (enumDesc == "Group Name") return GroupFilterSorting.GroupName;
            if (enumDesc == "Sort Name") return GroupFilterSorting.SortName;
            if (enumDesc == "Missing Episode Count") return GroupFilterSorting.MissingEpisodeCount;
            if (enumDesc == "Series Added Date") return GroupFilterSorting.SeriesAddedDate;
            if (enumDesc == "Series Count") return GroupFilterSorting.SeriesCount;
            if (enumDesc == "Unwatched Episode Count") return GroupFilterSorting.UnwatchedEpisodeCount;
            if (enumDesc == "User Rating") return GroupFilterSorting.UserRating;
            if (enumDesc == "Year") return GroupFilterSorting.Year;


            return GroupFilterSorting.AniDBRating;
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
            if (enumDesc == "Asc") return GroupFilterSortDirection.Asc;
            if (enumDesc == "Desc") return GroupFilterSortDirection.Desc;

            return GroupFilterSortDirection.Asc;
        }

        public static List<string> GetAllSortTypes()
        {
            List<string> cons = new List<string>();

            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.AniDBRating));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAddedDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAirDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeWatchedDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.GroupName));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.MissingEpisodeCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SeriesAddedDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SeriesCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SortName));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.UnwatchedEpisodeCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.UserRating));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.Year));

            cons.Sort();

            return cons;
        }

        public static List<string> GetQuickSortTypes()
        {
            List<string> cons = new List<string>();

            //cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.AniDBRating)); removed for performance reasons
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAddedDate));
            //cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeAirDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.EpisodeWatchedDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.GroupName));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.MissingEpisodeCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SeriesAddedDate));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SeriesCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.SortName));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.UnwatchedEpisodeCount));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.UserRating));
            cons.Add(GetTextForEnum_Sorting(GroupFilterSorting.Year));

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
            catch (Exception ex)
            {
                return DateTime.Today;
            }
        }

        public static string GetDateAsFriendlyString(DateTime aDate)
        {
            return aDate.ToString("dd MMM yyyy", CultureInfo.CurrentCulture);
        }

        public static IEnumerable<CL_AnimeGroup_User> Sort(IEnumerable<CL_AnimeGroup_User> groups, GroupFilter gf)
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
                        return Order(groups, a => a.Stat_AirDate_Min,gfsc.SortDirection,isfirst);
                    return Order(groups, a => a.Stat_AirDate_Max, gfsc.SortDirection, isfirst);
                case GroupFilterSorting.AniDBRating:
                    return Order(groups, a=>a.Stat_AniDBRating,gfsc.SortDirection,isfirst);
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
                default:
                    return Order(groups, a => a.GroupName, gfsc.SortDirection, isfirst);
            }
        }

        private static IOrderedEnumerable<CL_AnimeGroup_User> Order<T>(IEnumerable<CL_AnimeGroup_User> groups, Func<CL_AnimeGroup_User, T> o,
            GroupFilterSortDirection direc, bool isfirst)
        {
            if (isfirst)
            {
                if (direc == GroupFilterSortDirection.Asc)
                    return groups.OrderBy(o);
                return groups.OrderByDescending(o);
            }
            else
            {
                if (direc == GroupFilterSortDirection.Asc)
                    return ((IOrderedEnumerable<CL_AnimeGroup_User>) groups).ThenBy(o);
                return ((IOrderedEnumerable<CL_AnimeGroup_User>)groups).ThenByDescending(o);
            }
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
            string sortColumn = "";
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