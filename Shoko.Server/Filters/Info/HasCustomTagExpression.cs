using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCustomTagExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasCustomTagExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasCustomTagExpression() { }

    public string Parameter { get; set; }
    public override string HelpDescription => "This condition passes if any of the anime have a specified custom tag";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.CustomTags.Contains(Parameter);
    }

    protected bool Equals(HasCustomTagExpression other)
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

        return Equals((HasCustomTagExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasCustomTagExpression left, HasCustomTagExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCustomTagExpression left, HasCustomTagExpression right)
    {
        return !Equals(left, right);
    }
}
