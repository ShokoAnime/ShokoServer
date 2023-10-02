using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.User;

public class IsFavoriteExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;
    public override string HelpDescription => "This passes if the filterable is marked as Favorite. This exists for backwards compatibility. Custom Tags are a better way to do this.";

    public override bool Evaluate(IUserDependentFilterable filterable)
    {
        return filterable.IsFavorite;
    }

    protected bool Equals(IsFavoriteExpression other)
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

        return Equals((IsFavoriteExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(IsFavoriteExpression left, IsFavoriteExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(IsFavoriteExpression left, IsFavoriteExpression right)
    {
        return !Equals(left, right);
    }
}
