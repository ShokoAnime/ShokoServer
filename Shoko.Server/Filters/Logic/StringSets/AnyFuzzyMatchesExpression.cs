using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Utils;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class AnyFuzzyMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithSecondStringSelectorParameter, IWithStringParameter
{
    public AnyFuzzyMatchesExpression(FilterExpression<IReadOnlySet<string>> left, FilterExpression<string> right)
    {
        Left = left;
        Right = right;
    }

    public AnyFuzzyMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public AnyFuzzyMatchesExpression() { }

    public FilterExpression<IReadOnlySet<string>> Left { get; set; }
    public FilterExpression<string> Right { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent || (Right?.TimeDependent ?? false);
    public override bool UserDependent => Left.UserDependent || (Right?.UserDependent ?? false);
    public override string HelpDescription => "This condition passes if any of the values in the left selector contain either the right selector or the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        var right = Parameter ?? Right?.Evaluate(filterable, userInfo);
        if (string.IsNullOrEmpty(right)) return !left.Any();
        return left.All(a => a.FuzzyMatches(right));
    }

    protected bool Equals(AnyFuzzyMatchesExpression other)
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

        return Equals((AnyFuzzyMatchesExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Right, Parameter);
    }

    public static bool operator ==(AnyFuzzyMatchesExpression left, AnyFuzzyMatchesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AnyFuzzyMatchesExpression left, AnyFuzzyMatchesExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is AnyFuzzyMatchesExpression exp && Left.IsType(exp.Left) && (Right?.IsType(exp.Right) ?? true);
    }
}
