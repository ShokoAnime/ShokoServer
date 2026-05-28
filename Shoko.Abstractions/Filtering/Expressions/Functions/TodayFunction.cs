using System;

namespace Shoko.Abstractions.Filtering.Expressions.Functions;

/// <summary>
/// This returns the current date, at midnight (00:00:00.0000)
/// </summary>
public class TodayFunction : FilterExpression<DateTime?>
{
    /// <inheritdoc/>
    public override bool TimeDependent => true;

    /// <inheritdoc/>
    public override string HelpDescription => "This returns the current date, at midnight (00:00:00.0000)";

    /// <inheritdoc/>
    public override FilterExpressionGroup Group => FilterExpressionGroup.Function;

    /// <inheritdoc/>
    public override DateTime? Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    {
        ArgumentNullException.ThrowIfNull(time);
        return time.Value!.Date;
    }

    /// <inheritdoc cref="Equals(object)"/>
    protected bool Equals(TodayFunction other)
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

        return Equals((TodayFunction)obj);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetType().FullName!.GetHashCode();
    }
}
