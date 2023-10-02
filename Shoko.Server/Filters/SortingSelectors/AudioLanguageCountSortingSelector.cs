using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class AudioLanguageCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by how many distinct audio languages are present in all of the files in a filterable";

    public override object Evaluate(IFilterable f)
    {
        return f.AudioLanguages.Count;
    }
}
