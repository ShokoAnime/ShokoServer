
namespace Shoko.Server.Filters.Legacy;

public class LegacyGroupFilterSortingCriteria
{
    public int? GroupFilterID { get; set; }

    private CL_GroupFilterSorting sortType = CL_GroupFilterSorting.AniDBRating;

    public CL_GroupFilterSorting SortType
    {
        get => sortType;
        set => sortType = value;
    }

    private CL_GroupFilterSortDirection sortDirection = CL_GroupFilterSortDirection.Asc;

    public CL_GroupFilterSortDirection SortDirection
    {
        get => sortDirection;
        set => sortDirection = value;
    }
}
