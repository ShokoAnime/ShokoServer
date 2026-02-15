using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

public class HasTmdbLinkExpression : FilterExpression<bool>
{
    public override string Name => "Has TMDB Link";
    public override string HelpDescription => "This condition passes if any of the anime have a TMDB link";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
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
