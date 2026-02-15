using System;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingPermanentVoteExpression : FilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => true;
    public override string Name => "Missing Permanent Vote";
    public override string HelpDescription => "This condition passes if all of the anime are finished but we do not have permanent votes for all of them.";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.IsFinished && filterable.SeriesCount != userInfo.SeriesPermanentVoteCount;
    }

    protected bool Equals(MissingPermanentVoteExpression other)
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

        return Equals((MissingPermanentVoteExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(MissingPermanentVoteExpression left, MissingPermanentVoteExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MissingPermanentVoteExpression left, MissingPermanentVoteExpression right)
    {
        return !Equals(left, right);
    }
}
