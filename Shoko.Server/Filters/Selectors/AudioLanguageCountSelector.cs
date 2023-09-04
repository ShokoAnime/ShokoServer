using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class AudioLanguageCountSelector : FilterExpression<int>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override int Evaluate(IFilterable f) => f.AudioLanguages.Count;
}
