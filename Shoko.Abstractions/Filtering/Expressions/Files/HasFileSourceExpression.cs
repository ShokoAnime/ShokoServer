using System;
using System.Linq;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Video.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Files;

/// <summary>
/// This condition passes if any of the files have the specified source type
/// </summary>
public class HasFileSourceExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasFileSourceExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasFileSourceExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The parsed release source from the parameter string.
    /// </summary>
    protected ReleaseSource? ReleaseSource => Enum.TryParse<ReleaseSource>(Parameter, true, out var source)
        ? source
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the files have the specified source type";

    /// <inheritdoc/>
    public override string[] HelpPossibleParameters => Enum.GetValues<ReleaseSource>().Select(x => x.ToString()).ToArray();

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return ReleaseSource.HasValue && filterable.FileSourceCounts[ReleaseSource.Value] > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasFileSourceExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasFileSourceExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
