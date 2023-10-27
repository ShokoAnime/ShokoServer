using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Selectors;

public class NameSelector : FilterExpression<string>
{
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This returns the name of a filterable";

    public override string Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return filterable.Name;
    }

    protected bool Equals(NameSelector other)
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

        return Equals((NameSelector)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(NameSelector left, NameSelector right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NameSelector left, NameSelector right)
    {
        return !Equals(left, right);
    }
}
