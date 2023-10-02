using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasTagExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasTagExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasTagExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;
    public override string HelpDescription => "This passes if any of the anime have a tag that matches the parameter";

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.Tags.Contains(Parameter);
    }

    protected bool Equals(HasTagExpression other)
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

        return Equals((HasTagExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasTagExpression left, HasTagExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasTagExpression left, HasTagExpression right)
    {
        return !Equals(left, right);
    }
}
