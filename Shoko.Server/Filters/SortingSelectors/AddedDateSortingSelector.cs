using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class AddedDateSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by the date that a filterable was created";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.AddedDate;
    }
}
