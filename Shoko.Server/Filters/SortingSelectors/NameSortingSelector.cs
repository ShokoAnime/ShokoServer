namespace Shoko.Server.Filters.SortingSelectors;

public class NameSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override string Evaluate(Filterable f)
    {
        return f.Name;
    }
}
