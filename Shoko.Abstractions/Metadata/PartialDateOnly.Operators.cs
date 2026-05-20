using System;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Metadata;

/// <inheritdoc/>
public readonly partial struct PartialDateOnly
{
    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left == right);

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (left is null)
            return right is not null;

        return left.Value.CompareTo(right) < 0;
    }

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, PartialDateOnly? right)
    {
        if (right is null)
            return left is not null;

        return right.Value.CompareTo(left) < 0;
    }

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left > right);

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, PartialDateOnly? right)
        => !(left < right);

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateTime right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateTime right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateTime right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateTime right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateTime right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateTime right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, DateTime right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, DateTime right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, DateTime right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, DateTime right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, DateTime right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, DateTime right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateTime? right)
        => left == FromDateTime(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateTime? right)
        => !(left == FromDateTime(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateTime? right)
        => left < FromDateTime(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateTime? right)
        => left > FromDateTime(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateTime? right)
        => !(left > FromDateTime(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateTime? right)
        => !(left < FromDateTime(right));

    /// <inheritdoc />
    public static bool operator ==(DateTime left, PartialDateOnly right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime left, PartialDateOnly right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime left, PartialDateOnly right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime left, PartialDateOnly right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime? left, PartialDateOnly right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime? left, PartialDateOnly right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateTime left, PartialDateOnly? right)
        => FromDateTime(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateTime left, PartialDateOnly? right)
        => !(FromDateTime(left) < right);

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateOnly right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateOnly right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateOnly right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateOnly right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateOnly right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateOnly right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly? left, DateOnly right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly? left, DateOnly right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly? left, DateOnly right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly? left, DateOnly right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly? left, DateOnly right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly? left, DateOnly right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(PartialDateOnly left, DateOnly? right)
        => left == FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator !=(PartialDateOnly left, DateOnly? right)
        => !(left == FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator <(PartialDateOnly left, DateOnly? right)
        => left < FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator >(PartialDateOnly left, DateOnly? right)
        => left > FromDateOnly(right);

    /// <inheritdoc />
    public static bool operator <=(PartialDateOnly left, DateOnly? right)
        => !(left > FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator >=(PartialDateOnly left, DateOnly? right)
        => !(left < FromDateOnly(right));

    /// <inheritdoc />
    public static bool operator ==(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly left, PartialDateOnly right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly left, PartialDateOnly right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly? left, PartialDateOnly right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly? left, PartialDateOnly right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static bool operator ==(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) == right;

    /// <inheritdoc />
    public static bool operator !=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) == right);

    /// <inheritdoc />
    public static bool operator <(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) < right;

    /// <inheritdoc />
    public static bool operator >(DateOnly left, PartialDateOnly? right)
        => FromDateOnly(left) > right;

    /// <inheritdoc />
    public static bool operator <=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) > right);

    /// <inheritdoc />
    public static bool operator >=(DateOnly left, PartialDateOnly? right)
        => !(FromDateOnly(left) < right);

    /// <inheritdoc />
    public static TimeSpan operator -(PartialDateOnly left, PartialDateOnly right)
        => left.ToDateTime() - right.ToDateTime();

    /// <inheritdoc />
    public static TimeSpan operator -(DateTime left, PartialDateOnly right)
        => left - right.ToDateTime();

    /// <inheritdoc />
    public static TimeSpan operator -(PartialDateOnly left, DateTime right)
        => left.ToDateTime() - right;
}
