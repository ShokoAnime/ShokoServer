using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.User;

public class HasUserVotesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override bool Evaluate(IFilterable filterable) => filterable.HasVotes;
}
