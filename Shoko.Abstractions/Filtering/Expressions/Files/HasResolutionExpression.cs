using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if any of the files have the specified video resolution
/// </summary>
public class HasResolutionExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasResolutionExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasResolutionExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the files have the specified video resolution";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters =>
    [
        "2160p","1080p","720p","480p","UWHD","UWQHD","1440p","576p","360p","240p"
    ];

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return Parameter is not null && filterable.Resolutions.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasResolutionExpression other)
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

        return Equals((HasResolutionExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
