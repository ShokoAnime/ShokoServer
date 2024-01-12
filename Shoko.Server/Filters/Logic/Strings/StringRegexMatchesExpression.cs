using System;
using System.Text.RegularExpressions;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Logic.Strings;

public class StringRegexMatchesExpression : FilterExpression<bool>, IWithStringSelectorParameter, IWithStringParameter
{
    private readonly Regex _regex;
    public StringRegexMatchesExpression(FilterExpression<string> left, string parameter)
    {
        Left = left;
        Parameter = parameter;
        try
        {
            _regex = new Regex(parameter, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        catch { /* ignore */ }
    }

    public StringRegexMatchesExpression() { }

    public FilterExpression<string> Left { get; set; }
    public string Parameter { get; set; }
    public override bool TimeDependent => Left.TimeDependent;
    public override bool UserDependent => Left.UserDependent;
    public override string HelpDescription => "This condition passes if the left selector matches the regular expression given in the parameter";
    public override FilterExpressionGroup Group => FilterExpressionGroup.Logic;

    public override bool Evaluate(IFilterable filterable, IFilterableUserInfo userInfo)
    {
        var left = Left.Evaluate(filterable, userInfo);
        if (string.IsNullOrEmpty(left) || _regex == null)
        {
            return false;
        }

        return _regex.IsMatch(left);
    }

    protected bool Equals(StringRegexMatchesExpression other)
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

        return Equals((StringRegexMatchesExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Left, Parameter);
    }

    public static bool operator ==(StringRegexMatchesExpression left, StringRegexMatchesExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(StringRegexMatchesExpression left, StringRegexMatchesExpression right)
    {
        return !Equals(left, right);
    }

    public override bool IsType(FilterExpression expression)
    {
        return expression is StringRegexMatchesExpression exp && Left.IsType(exp.Left);
    }
}
