namespace Shoko.Server.Filters.Info;

public class IsFinishedExpression : FilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.IsFinished;
    }
}
