using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class NameSortingSelector : SortingExpression<string>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Evaluate(IFilterable f) => f.Name;
}
