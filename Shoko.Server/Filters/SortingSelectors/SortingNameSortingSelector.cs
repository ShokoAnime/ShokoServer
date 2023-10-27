using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class SortingNameSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by a filterable's name, excluding common words like A, The, etc.";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SortingName;
    }
}
