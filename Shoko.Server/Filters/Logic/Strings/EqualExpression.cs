using System;

namespace Shoko.Server.Filters.Logic.Strings;

public class EqualExpression : FilterExpression<bool>
{
    public EqualExpression(FilterExpression<string> left, FilterExpression<string> right)
    {
        Left = left;
        Right = right;
    }
    public EqualExpression(FilterExpression<string> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }
    public EqualExpression() { }

    public FilterExpression<string> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);

    public override bool Evaluate(Filterable filterable)
    {
        var left = Left.Evaluate(filterable);
        var right = Parameter ?? Right?.Evaluate(filterable);
        return string.Equals(left, right, StringComparison.InvariantCultureIgnoreCase);
    }

    protected bool Equals(EqualExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Equals(Right, other.Right) && Parameter == other.Parameter;
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

        return Equals((EqualExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(EqualExpression left, EqualExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(EqualExpression left, EqualExpression right)
    {
        return !Equals(left, right);
    }
}
