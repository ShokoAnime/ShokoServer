using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have any video files locally.
/// </summary>
public class HasVideoFilesExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have any video files locally.";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.VideoFiles > 0;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(HasVideoFilesExpression other)
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

        return Equals((HasVideoFilesExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
