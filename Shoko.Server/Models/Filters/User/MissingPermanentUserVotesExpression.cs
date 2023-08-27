using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.User;

public class MissingPermanentUserVotesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => true;
    public override bool Evaluate(IUserDependentFilterable filterable) => filterable.MissingPermanentVotes;
}
