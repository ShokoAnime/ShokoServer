namespace Shoko.Server.Filters.Selectors;

public class SubtitleLanguageCountSelector : FilterExpression<int>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override int Evaluate(Filterable f)
    {
        return f.SubtitleLanguages.Count;
    }
}
