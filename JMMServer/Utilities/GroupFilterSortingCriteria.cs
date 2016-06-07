namespace JMMServer
{
    public class GroupFilterSortingCriteria
    {
        public int? GroupFilterID { get; set; }

        public GroupFilterSorting SortType { get; set; } = GroupFilterSorting.AniDBRating;

        public GroupFilterSortDirection SortDirection { get; set; } = GroupFilterSortDirection.Asc;
    }
}