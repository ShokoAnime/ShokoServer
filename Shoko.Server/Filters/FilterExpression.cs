using System.Runtime.Serialization;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class FilterExpression : IFilterExpression
{
    public int FilterExpressionID { get; set; }
    [IgnoreDataMember] public virtual bool TimeDependent => false;
    [IgnoreDataMember] public virtual bool UserDependent => false;

    protected virtual bool Equals(FilterExpression other)
    {
        return true;
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

        return Equals((FilterExpression)obj);
    }

    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }

    public static bool operator ==(FilterExpression left, FilterExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(FilterExpression left, FilterExpression right)
    {
        return !Equals(left, right);
    }
}

public abstract class FilterExpression<T> : FilterExpression, IFilterExpression<T>
{
    public abstract T Evaluate(Filterable f);
}
