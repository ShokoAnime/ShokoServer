using System;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime has the available image type.
/// </summary>
public class HasAvailableImageExpression : FilterExpression<bool>, IWithStringParameter
{
    /// <inheritdoc/>
    public HasAvailableImageExpression(string parameter)
        => Parameter = parameter;

    /// <inheritdoc/>
    public HasAvailableImageExpression() { }

    /// <inheritdoc/>
    public string? Parameter { get; set; }

    /// <summary>
    /// The parsed image entity type from the parameter string.
    /// </summary>
    protected ImageEntityType? ImageEntityType => Enum.TryParse<ImageEntityType>(Parameter, true, out var entityType)
        ? entityType
        : null;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime has the available image type.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return ImageEntityType.HasValue && filterable.AvailableImageTypes.Contains(ImageEntityType.Value);
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasAvailableImageExpression other)
    {
        return base.Equals(other) && Parameter == other.Parameter;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((HasAvailableImageExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(base.GetHashCode(), Parameter);
}
