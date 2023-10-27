using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.SortingSelectors;

public class SubtitleLanguageCountSortingSelector : SortingExpression
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This sorts by how many distinct subtitle languages are present in all of the files in a filterable";

    public override object Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SubtitleLanguages.Count;
    }
}
