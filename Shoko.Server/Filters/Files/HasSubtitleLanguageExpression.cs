using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasSubtitleLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSubtitleLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSubtitleLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.SubtitleLanguages.Contains(Parameter);
    }

    protected bool Equals(HasSubtitleLanguageExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
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

        return Equals((HasSubtitleLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSubtitleLanguageExpression left, HasSubtitleLanguageExpression right)
    {
        return !Equals(left, right);
    }
}
