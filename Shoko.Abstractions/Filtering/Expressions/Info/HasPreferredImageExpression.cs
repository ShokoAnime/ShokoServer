using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime has the preferred image type.
/// </summary>
public class HasPreferredImageExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasPreferredImageExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasPreferredImageExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The parsed image entity type from the parameter string.
    /// </summary>
    protected ImageEntityType? ImageEntityType => Enum.TryParse<ImageEntityType>(Parameter, true, out var entityType)
        ? entityType
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime has the preferred image type.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return ImageEntityType.HasValue && filterable.PreferredImageTypes.Contains(ImageEntityType.Value);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasPreferredImageExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasPreferredImageExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
