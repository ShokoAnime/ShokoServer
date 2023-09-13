namespace Shoko.Server.Filters.User;

public class HasPermanentUserVotesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override bool Evaluate(UserDependentFilterable filterable)
    {
        return filterable.HasPermanentVotes;
    }

    protected bool Equals(HasPermanentUserVotesExpression other)
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

        return Equals((HasPermanentUserVotesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasPermanentUserVotesExpression left, HasPermanentUserVotesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasPermanentUserVotesExpression left, HasPermanentUserVotesExpression right)
    {
        return !Equals(left, right);
    }
}
