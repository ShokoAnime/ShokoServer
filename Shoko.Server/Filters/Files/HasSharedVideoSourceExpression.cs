using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasSharedVideoSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasSharedVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasSharedVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.SharedVideoSources.Contains(Parameter);
    }

    protected bool Equals(HasSharedVideoSourceExpression other)
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

        return Equals((HasSharedVideoSourceExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasSharedVideoSourceExpression left, HasSharedVideoSourceExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasSharedVideoSourceExpression left, HasSharedVideoSourceExpression right)
    {
        return !Equals(left, right);
    }
}
