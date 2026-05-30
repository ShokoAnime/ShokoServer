#pragma warning disable CS0618
using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Video.Services;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have the files of specified release provider name
/// </summary>
public class HasReleaseProviderNameExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasReleaseProviderNameExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasReleaseProviderNameExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <inheritdoc/>
    public override bool TimeDependent => false;

    /// <inheritdoc/>
    public override bool UserDependent => false;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have the files of specified release provider name";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => ISystemService.StaticServices.GetRequiredService<IVideoReleaseService>().GetStoredReleaseProviderNames().ToArray();

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? now)
    {
        return Parameter is not null && filterable.ReleaseProviderNames.Contains(Parameter);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasReleaseProviderNameExpression other)
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

        return Equals((HasReleaseProviderNameExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
