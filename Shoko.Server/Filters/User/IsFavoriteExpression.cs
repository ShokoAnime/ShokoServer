using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.User;

public class IsFavoriteExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override bool Evaluate(IUserDependentFilterable filterable) => filterable.IsFavorite;
}
