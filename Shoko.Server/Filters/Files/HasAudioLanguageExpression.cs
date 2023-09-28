using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasAudioLanguageExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasAudioLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasAudioLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.AudioLanguages.Contains(Parameter);
    }

    protected bool Equals(HasAudioLanguageExpression other)
    {
        return base.Equals(other) && string.Equals(Parameter, other.Parameter, StringComparison.InvariantCultureIgnoreCase);
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

        return Equals((HasAudioLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Parameter, StringComparer.InvariantCultureIgnoreCase);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(HasAudioLanguageExpression left, HasAudioLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasAudioLanguageExpression left, HasAudioLanguageExpression right)
    {
        return !Equals(left, right);
    }
}
