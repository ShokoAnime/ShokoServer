using System;

namespace Shoko.Server.Filters.Files;

public class HasVideoSourceExpression : FilterExpression<bool>
{
    public HasVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.VideoSources.Contains(Parameter);
    }

    protected bool Equals(HasVideoSourceExpression other)
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

        return Equals((HasVideoSourceExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasVideoSourceExpression left, HasVideoSourceExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasVideoSourceExpression left, HasVideoSourceExpression right)
    {
        return !Equals(left, right);
    }
}
