using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class HasTraktLinkExpression : FilterExpression<bool>
{
    public override string HelpDescription => "This condition passes if any of the anime have a Trakt link";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return false;
    }

    protected bool Equals(HasTraktLinkExpression other)
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

        return Equals((HasTraktLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTraktLinkExpression left, HasTraktLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTraktLinkExpression left, HasTraktLinkExpression right)
    {
        return !Equals(left, right);
    }
}
