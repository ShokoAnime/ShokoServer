namespace Shoko.Server.Filters.Files;

public class HasAudioLanguageExpression : FilterExpression<bool>
{
    public HasAudioLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasAudioLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.AudioLanguages.Contains(Parameter);
    }
}
