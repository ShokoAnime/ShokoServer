using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingVoteExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Name => "Missing Vote";
    public override string HelpDescription => "This condition passes if not all of the anime in the filterable have been voted on.";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.SeriesCount != filterable.SeriesVoteCount;
    }

    protected bool Equals(MissingVoteExpression other)
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

        return Equals((MissingVoteExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingVoteExpression left, MissingVoteExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingVoteExpression left, MissingVoteExpression right)
    {
        return !Equals(left, right);
    }
}
