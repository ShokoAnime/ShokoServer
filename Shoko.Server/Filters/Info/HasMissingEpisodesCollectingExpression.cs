using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasMissingEpisodesCollectingExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.MissingEpisodesCollecting > 0;
    }

    protected bool Equals(HasMissingEpisodesCollectingExpression other)
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

        return Equals((HasMissingEpisodesCollectingExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasMissingEpisodesCollectingExpression left, HasMissingEpisodesCollectingExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasMissingEpisodesCollectingExpression left, HasMissingEpisodesCollectingExpression right)
    {
        return !Equals(left, right);
    }
}
