using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTmdbAutoLinkingDisabledExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Name => "Has TMDB Auto Linking Disabled";
    public override string HelpDescription => "This condition passes if any of the anime has TMDB auto-linking disabled";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.HasTmdbAutoLinkingDisabled;
    }

    protected bool Equals(HasTmdbAutoLinkingDisabledExpression other)
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

        return Equals((HasTmdbAutoLinkingDisabledExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTmdbAutoLinkingDisabledExpression left, HasTmdbAutoLinkingDisabledExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTmdbAutoLinkingDisabledExpression left, HasTmdbAutoLinkingDisabledExpression right)
    {
        return !Equals(left, right);
    }
}
