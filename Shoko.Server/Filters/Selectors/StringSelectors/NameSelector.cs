using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Selectors.StringSelectors;

public class NameSelector : FilterExpression<string>
{
    public override string HelpDescription => "This returns the name of a filterable";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Selector;

    public override string Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.Name;
    }

    protected bool Equals(NameSelector other)
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

        return Equals((NameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(NameSelector left, NameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NameSelector left, NameSelector right)
    {
        return !Equals(left, right);
    }
}
