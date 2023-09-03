using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.SortingSelectors;

public class AudioLanguageCountSortingSelector : SortingExpression<int>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override int Evaluate(IFilterable f) => f.AudioLanguages.Count;
}
