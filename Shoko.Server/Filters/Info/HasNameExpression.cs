using System;

namespace Shoko.Server.Filters.Info;

public class HasNameExpression : FilterExpression<bool>
{
    public HasNameExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasNameExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(Filterable filterable)
    {
        return filterable.Name.Equals(Parameter, StringComparison.InvariantCultureIgnoreCase);
    }

    protected bool Equals(HasAnimeTypeExpression other)
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

        return Equals((HasAnimeTypeExpression)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), Parameter);
    }

    public static bool operator ==(HasNameExpression left, HasNameExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasNameExpression left, HasNameExpression right)
    {
        return !Equals(left, right);
    }
}
