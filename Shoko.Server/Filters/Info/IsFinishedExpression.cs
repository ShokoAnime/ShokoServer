using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class IsFinishedExpression : FilterExpression<bool>
{
    public override bool TimeDependent => true;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if any of the anime have finished";

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.IsFinished;
    }

    protected bool Equals(IsFinishedExpression other)
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

        return Equals((IsFinishedExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(IsFinishedExpression left, IsFinishedExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(IsFinishedExpression left, IsFinishedExpression right)
    {
        return !Equals(left, right);
    }
}
