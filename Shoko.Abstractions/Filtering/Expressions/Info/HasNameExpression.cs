using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if the name of the series or group equals the name specified. This exists for legacy compatibility and should not be used.
/// </summary>
public class HasNameExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasNameExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasNameExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if the name of the series or group equals the name specified. This exists for legacy compatibility and should not be used.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.Name.Equals(Parameter, StringComparison.InvariantCultureIgnoreCase);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAnimeTypeExpression other)
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

        return Equals((HasAnimeTypeExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
