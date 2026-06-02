using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the files of specified release group name.
/// </summary>
public class HasReleaseGroupNameExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasReleaseGroupNameExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasReleaseGroupNameExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => false;

    /// <inheritdoc/>
    public override bool UserDependent => false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the files of specified release group name";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? now)
    {
        return Parameter is not null && filterable.ReleaseGroupNames.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasReleaseGroupNameExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasReleaseGroupNameExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
