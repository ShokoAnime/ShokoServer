namespace Shoko.Server.Filters.Info;

public class HasTraktLinkExpression : FilterExpression<bool>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.HasTraktLink;
    }

    protected bool Equals(HasTraktLinkExpression other)
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

        return Equals((HasTraktLinkExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(HasTraktLinkExpression left, HasTraktLinkExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTraktLinkExpression left, HasTraktLinkExpression right)
    {
        return !Equals(left, right);
    }
}
