using System;

namespace Shoko.Server.Filters.Files;

public class HasSharedAudioLanguageExpression : FilterExpression<bool>
{
    public HasSharedAudioLanguageExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSharedAudioLanguageExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.SharedAudioLanguages.Contains(Parameter);
    }

    protected bool Equals(HasSharedAudioLanguageExpression other)
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

        return Equals((HasSharedAudioLanguageExpression)obj);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(base.GetHashCode());
        hashCode.Add(Parameter, StringComparer.InvariantCultureIgnoreCase);
        return hashCode.ToHashCode();
    }

    public static bool operator ==(HasSharedAudioLanguageExpression left, HasSharedAudioLanguageExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSharedAudioLanguageExpression left, HasSharedAudioLanguageExpression right)
    {
        return !Equals(left, right);
    }
}
