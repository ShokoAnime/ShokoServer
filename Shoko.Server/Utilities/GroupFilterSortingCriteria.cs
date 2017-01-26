using Shoko.Models;
using Shoko.Models.Enums;

namespace Shoko.Server
{
    public class GroupFilterSortingCriteria
    {
        public int? GroupFilterID { get; set; }

        private GroupFilterSorting sortType = GroupFilterSorting.AniDBRating;

        public GroupFilterSorting SortType
        {
            get { return sortType; }
            set { sortType = value; }
        }

        private GroupFilterSortDirection sortDirection = GroupFilterSortDirection.Asc;

        public GroupFilterSortDirection SortDirection
        {
            get { return sortDirection; }
            set { sortDirection = value; }
        }
    }
}