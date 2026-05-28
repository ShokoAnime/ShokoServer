using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if any of the files have the specified video source
/// </summary>
public class HasVideoSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasVideoSourceExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasVideoSourceExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the files have the specified video source";

    /// <inheritdoc/>
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
        "film",
        "unk",
    };

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.VideoSources.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasVideoSourceExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj.GetType() != GetType())
            return false;

        return Equals((HasVideoSourceExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
