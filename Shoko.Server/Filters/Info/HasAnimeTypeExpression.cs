using System;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Info;

public class HasAnimeTypeExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasAnimeTypeExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasAnimeTypeExpression() { }

    public string Parameter { get; set; }
    public override bool TimeDependent => false;
    public override bool UserDependent => false;

    public override bool Evaluate(IFilterable filterable)
    {
        return filterable.AnimeTypes.Contains(Parameter);
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

    public static bool operator ==(HasAnimeTypeExpression left, HasAnimeTypeExpression right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(HasAnimeTypeExpression left, HasAnimeTypeExpression right)
    {
        return !Equals(left, right);
    }
}