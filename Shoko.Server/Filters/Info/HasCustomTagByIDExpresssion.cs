using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasCustomTagByIDExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasCustomTagByIDExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasCustomTagByIDExpression() { }

    public string Parameter { get; set; }
    public override string HelpDescription => "This condition passes if any of the anime have a specified custom tag by ID";

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
    {
        return filterable.CustomTagIDs.Contains(Parameter);
    }

    protected bool Equals(HasCustomTagByIDExpression other)
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

        return Equals((HasCustomTagByIDExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasCustomTagByIDExpression left, HasCustomTagByIDExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasCustomTagByIDExpression left, HasCustomTagByIDExpression right)
    {
        return !Equals(left, right);
    }
}
