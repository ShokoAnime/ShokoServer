using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.StringSets;

public class AllRegexMatchesExpression : FilterExpression<bool>, IWithStringSetSelectorParameter, IWithStringParameter
{
    public AllRegexMatchesExpression(FilterExpression<IReadOnlySet<string>> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
    }

    public AllRegexMatchesExpression() { }

    public FilterExpression<IReadOnlySet<string>> Left { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This condition passes if all of the values in the left selector matches the regex provided in the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        var left = Left.Evaluate(filterable, userInfo, time);
        if (string.IsNullOrEmpty(Parameter)) return !left.Any();
        var regex = new Regex(Parameter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return left.All(a => regex.IsMatch(a));
    }

    protected bool Equals(AllRegexMatchesExpression other)
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

        return Equals((AllRegexMatchesExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(AllRegexMatchesExpression left, AllRegexMatchesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AllRegexMatchesExpression left, AllRegexMatchesExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is AllRegexMatchesExpression exp && Left.IsType(exp.Left);
    }
}
