using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class HasTvDBLinkExpression : FilterExpression<bool>
{
    public override string Name => "Has TvDB Link";
    public override string HelpDescription => "This condition passes if any of the anime have a TvDB link";
    public override bool Deprecated => true;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return false;
    }

    protected bool Equals(HasTvDBLinkExpression other)
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

        return Equals((HasTvDBLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTvDBLinkExpression left, HasTvDBLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTvDBLinkExpression left, HasTvDBLinkExpression right)
    {
        return !Equals(left, right);
    }
}
