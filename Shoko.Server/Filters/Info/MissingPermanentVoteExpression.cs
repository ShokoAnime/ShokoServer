using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

/// <summary>
///     Missing Vote Expression
/// </summary>
public class MissingPermanentVoteExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string Name => "Missing Permanent Vote";
    public override string HelpDescription => "This condition passes if all of the anime are finished but we do not have permanent votes for all of them.";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.IsFinished && filterable.SeriesCount != filterable.SeriesPermanentVoteCount;
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
