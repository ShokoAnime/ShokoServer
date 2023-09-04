namespace Shoko.Server.Filters.User;

public class MissingPermanentUserVotesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => true;

    public override bool Evaluate(UserDependentFilterable filterable)
    {
        return filterable.MissingPermanentVotes;
    }
}
