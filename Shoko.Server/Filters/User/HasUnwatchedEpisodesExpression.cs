namespace Shoko.Server.Filters.User;

public class HasUnwatchedEpisodesExpression : UserDependentFilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => true;

    public override bool Evaluate(UserDependentFilterable filterable)
    {
        return filterable.UnwatchedEpisodes > 0;
    }

    protected bool Equals(HasUnwatchedEpisodesExpression other)
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

        return Equals((HasUnwatchedEpisodesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasUnwatchedEpisodesExpression left, HasUnwatchedEpisodesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasUnwatchedEpisodesExpression left, HasUnwatchedEpisodesExpression right)
    {
        return !Equals(left, right);
    }
}
