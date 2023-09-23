using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.User;

public class MissingPermanentUserVotesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => true;

    public override bool Evaluate(IUserDependentFilterable filterable)
    {
        return filterable.MissingPermanentVotes;
    }

    protected bool Equals(MissingPermanentUserVotesExpression other)
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

        return Equals((MissingPermanentUserVotesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingPermanentUserVotesExpression left, MissingPermanentUserVotesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingPermanentUserVotesExpression left, MissingPermanentUserVotesExpression right)
    {
        return !Equals(left, right);
    }
}
