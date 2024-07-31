using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTmdbLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Name => "Has TMDB Link";
    public override string HelpDescription => "This condition passes if any of the anime have a TMDB link";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.HasTmdbLink;
    }

    protected bool Equals(HasTmdbLinkExpression other)
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

        return Equals((HasTmdbLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTmdbLinkExpression left, HasTmdbLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTmdbLinkExpression left, HasTmdbLinkExpression right)
    {
        return !Equals(left, right);
    }
}
