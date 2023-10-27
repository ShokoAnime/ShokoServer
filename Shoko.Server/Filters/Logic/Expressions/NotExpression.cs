using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Expressions;

public class NotExpression : FilterExpression<bool>, IWithExpressionParameter
{
    public NotExpression(FilterExpression<bool> left)
    {
        Left = left;
    }

    public NotExpression() { }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This passes if the left expression does not pass, e.g. an inverse";

    public FilterExpression<bool> Left { get; set; }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return !Left.Evaluate(filterable, userInfo);
    }

    protected bool Equals(NotExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left);
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

        return Equals((NotExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left);
    }

    public static bool operator ==(NotExpression left, NotExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(NotExpression left, NotExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is NotExpression exp && Left.IsType(exp.Left);
    }
}
