using System;
using Shoko.Abstractions.Filtering;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters.Files;

public class HasVideoSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    public HasVideoSourceExpression(string parameter)
    {
        Parameter = parameter;
    }
    public HasVideoSourceExpression() { }

    public string Parameter { get; set; }
    public override string HelpDescription => "This condition passes if any of the files have the specified video source";
    public override string[] HelpPossibleParameters => new[]
    {
        "tv",
        "www",
        "dvd",
        "bluray",
        "vhs",
        "camcorder",
        "vcd",
        "ld",
        "unk",
    };

    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo userInfo, DateTime? time)
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
