using System.Collections.Generic;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors.StringSetSelectors;

public class CreatorIDsSelector : FilterExpression<IReadOnlySet<string>>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns a set of all the creator IDs in a filterable.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override IReadOnlySet<string> Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.CreatorIDs;
    }

    protected bool Equals(CreatorIDsSelector other)
    {
        return base.Equals(other);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((CreatorIDsSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(CreatorIDsSelector left, CreatorIDsSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CreatorIDsSelector left, CreatorIDsSelector right)
    {
        return !Equals(left, right);
    }
}
