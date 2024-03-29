using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasMissingEpisodesCollectingExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime are missing episodes from a release group that is currently in the collection";
    public override string Name => "Has Missing Episodes (Collecting)";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
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
