using System;

namespace Shoko.Abstractions.Filtering.Expressions.Info;

/// <summary>
/// This condition passes if any of the anime have finished
/// </summary>
public class IsFinishedExpression : FilterExpression<bool>
{
    /// <inheritdoc/>
    public override bool TimeDependent => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This condition passes if any of the anime have finished";

    /// <inheritdoc/>
    public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        return filterable.IsFinished;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(IsFinishedExpression other)
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

        return Equals((IsFinishedExpression)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
