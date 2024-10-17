using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Expressions;

public class ConstantExpression : FilterExpression<bool>, IWithBoolParameter
{
    public ConstantExpression(bool parameter)
    {
        Parameter = parameter;
    }

    public ConstantExpression() { }

    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This condition passes if the left expression is equal to the right expression or the parameter.";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public bool Parameter { get; set; }

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        return Parameter;
    }

    protected bool Equals(ConstantExpression other)
    {
        return base.Equals(other) && Equals(Parameter, other.Parameter);
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

        return Equals((ConstantExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(ConstantExpression left, ConstantExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ConstantExpression left, ConstantExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is ConstantExpression;
    }
}
