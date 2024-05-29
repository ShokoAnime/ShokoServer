using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class AnyRegexMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringParameter
{
    public AnyRegexMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public AnyRegexMatchesExpression() { }

    public FilterExpression<IReadOnlySet<string>> Left { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This condition passes if any of the values in the left selector matches the regex provided in the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        if (string.IsNullOrEmpty(Parameter)) return !left.Any();
        var regex = new Regex(Parameter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return left.Any(a => regex.IsMatch(a));
    }

    protected bool Equals(AnyRegexMatchesExpression other)
    {
        return base.Equals(other) && Equals(Left, other.Left) && Parameter == other.Parameter;
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

        return Equals((AnyRegexMatchesExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(AnyRegexMatchesExpression left, AnyRegexMatchesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AnyRegexMatchesExpression left, AnyRegexMatchesExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is AnyRegexMatchesExpression exp && Left.IsType(exp.Left);
    }
}
