using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters.User;

public class IsFavoriteExpression : FilterExpression
{
    public override bool UserDependent => true;
    public override bool Evaluate(IFilterable filterable) => filterable.IsFavorite;
}
