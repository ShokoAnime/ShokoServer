using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///     Missing Links include logic for whether a link should exist
/// </summary>
public class MissingTraktLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime should have a Trakt link but does not have one";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return false;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingTraktLinkExpression other)
    {
        return base.Equals(other);
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

        return Equals((MissingTraktLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
