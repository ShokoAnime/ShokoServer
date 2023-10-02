using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTvDBLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This passes if any of the anime have a TvDB link";

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.HasTvDBLink;
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
