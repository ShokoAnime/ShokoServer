using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingVoteExpression : FilterExpression<bool>
{
    public override bool UserDependent => true;
    public override string Name => "Missing Vote";
    public override string HelpDescription => "This condition passes if not all of the anime in the filterable have been voted on.";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.SeriesCount != userInfo.SeriesVoteCount;
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
