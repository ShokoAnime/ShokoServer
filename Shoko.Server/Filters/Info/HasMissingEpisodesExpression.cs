using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasMissingEpisodesExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This passes if any of the anime have missing episodes from any known release group";

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.MissingEpisodes > 0;
    }

    protected bool Equals(HasMissingEpisodesExpression other)
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

        return Equals((HasMissingEpisodesExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasMissingEpisodesExpression left, HasMissingEpisodesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasMissingEpisodesExpression left, HasMissingEpisodesExpression right)
    {
        return !Equals(left, right);
    }
}
