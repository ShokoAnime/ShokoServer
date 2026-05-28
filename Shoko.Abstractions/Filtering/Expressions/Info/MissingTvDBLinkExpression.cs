using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
///   This condition passes if any of the anime should have a TvDB link but does not have one.
/// </summary>
// TODO: REMOVE THIS FILTER EXPRESSION SOMETIME IN THE FUTURE AFTER THE LEGACY FILTERS ARE REMOVED!!1!
public class MissingTvDBLinkExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool Deprecated => true;

    /// <inheritdoc/>
    public override string Name => "Missing TvDB Link";

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime should have a TvDB link but does not have one";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return false;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(MissingTvDBLinkExpression other)
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

        return Equals((MissingTvDBLinkExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
