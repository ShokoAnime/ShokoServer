using Shoko.Models.Enums;

namespace Shoko.Server.Filters.Legacy;

public class GroupFilterSortingCriteria
{
    public int? GroupFilterID { get; set; }

    private GroupFilterSorting sortType = GroupFilterSorting.AniDBRating;

    public GroupFilterSorting SortType
    {
        get => sortType;
        set => sortType = value;
    }

    private GroupFilterSortDirection sortDirection = GroupFilterSortDirection.Asc;

    public GroupFilterSortDirection SortDirection
    {
        get => sortDirection;
        set => sortDirection = value;
    }
}
