using System;
using System.Diagnostics.CodeAnalysis;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Represents dates with partial information, allowing for year-only or year-month values
/// in addition to full dates. Values range from January 1, 0001 Anno Domini (Common Era)
/// through December 31, 9999 A.D. (C.E.) in the Gregorian calendar.
/// </summary>
public readonly partial struct PartialDateOnly
{

    /// <inheritdoc/>
    TypeCode IConvertible.GetTypeCode()
        => TypeCode.String;

    /// <inheritdoc/>
    string IConvertible.ToString(IFormatProvider? provider)
        => ToString();

    /// <inheritdoc/>
    bool IConvertible.ToBoolean(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    byte IConvertible.ToByte(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    char IConvertible.ToChar(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    DateTime IConvertible.ToDateTime(IFormatProvider? provider)
        => ToDateTime();

    /// <inheritdoc/>
    decimal IConvertible.ToDecimal(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    double IConvertible.ToDouble(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    short IConvertible.ToInt16(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    int IConvertible.ToInt32(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    long IConvertible.ToInt64(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    sbyte IConvertible.ToSByte(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    float IConvertible.ToSingle(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    object IConvertible.ToType(Type conversionType, IFormatProvider? provider)
        => true switch
        {
            _ when conversionType == typeof(PartialDateOnly) => this,
            _ when conversionType == typeof(DateTime) => ToDateTime(),
            _ when conversionType == typeof(DateOnly) => ToDateOnly(),
            _ when conversionType == typeof(string) => ToString(),
            _ => throw new NotImplementedException()
        };

    /// <inheritdoc/>
    ushort IConvertible.ToUInt16(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    uint IConvertible.ToUInt32(IFormatProvider? provider)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    ulong IConvertible.ToUInt64(IFormatProvider? provider)
        => throw new NotImplementedException();
}
