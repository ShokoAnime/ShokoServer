using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class SortingNameSortingSelector : SortingExpression<string>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Evaluate(IFilterable f) => f.SortingName;
}
