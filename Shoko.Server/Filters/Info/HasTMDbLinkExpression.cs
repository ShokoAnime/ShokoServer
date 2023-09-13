namespace Shoko.Server.Filters.Info;

public class HasTMDbLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.HasTMDbLink;
    }

    protected bool Equals(HasTMDbLinkExpression other)
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

        return Equals((HasTMDbLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTMDbLinkExpression left, HasTMDbLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTMDbLinkExpression left, HasTMDbLinkExpression right)
    {
        return !Equals(left, right);
    }
}
