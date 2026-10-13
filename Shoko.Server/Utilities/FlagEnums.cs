using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Shoko.Server.Utilities;

/// <summary>
///   Reads and writes a <c>[Flags]</c> enum as the list of its single-bit
///   members, the way the JSON converters and the schema describe one.
/// </summary>
/// <remarks>
///   A member with one bit set is an entry. Zero and members combining
///   several bits are only accepted on read, expanded into their bits, and
///   bits no entry names are refused.
/// </remarks>
internal static class FlagEnums
{
    #region Shape

    private static readonly ConcurrentDictionary<Type, FlagEnumShape> _shapes = new();

    /// <summary>
    ///   Whether the type is an enum marked with <see cref="FlagsAttribute"/>.
    /// </summary>
    /// <param name="type">The type, which is not unwrapped.</param>
    /// <returns><c>true</c> for a flags enum.</returns>
    public static bool IsFlagEnum(Type? type)
        => type is { IsEnum: true } && type.IsDefined(typeof(FlagsAttribute), false);

    /// <summary>
    ///   Whether the type, or the type a <see cref="Nullable{T}"/> wraps, is a
    ///   flags enum.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="enumType">The flags enum, when it is one.</param>
    /// <returns><c>true</c> for a flags enum or a nullable one.</returns>
    public static bool IsFlagEnum(Type? type, out Type enumType)
    {
        enumType = type is null ? null! : Nullable.GetUnderlyingType(type) ?? type;
        return IsFlagEnum(enumType);
    }

    /// <summary>
    ///   The single-bit members of a flags enum, in declaration order, the
    ///   first member declared for each bit only.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<FieldInfo> GetEntries(Type type)
        => GetShape(type).Entries.Select(x => x.Field).ToList();

    /// <summary>
    ///   Whether a member of a flags enum is one of its entries.
    /// </summary>
    /// <param name="field">The member.</param>
    /// <returns><c>true</c> when it is the first member declared for its bit.</returns>
    public static bool IsEntry(FieldInfo field)
        => GetShape(field.DeclaringType!).Entries.Any(x => x.Field == field);

    /// <summary>
    ///   The name a member is written as.
    /// </summary>
    /// <param name="field">The member.</param>
    /// <param name="isNewtonsoftJson">
    ///   Whether Newtonsoft writes it, honouring <see cref="EnumMemberAttribute"/>,
    ///   rather than System.Text.Json, honouring
    ///   <see cref="JsonStringEnumMemberNameAttribute"/>.
    /// </param>
    /// <returns>The name.</returns>
    public static string GetWireName(FieldInfo field, bool isNewtonsoftJson)
    {
        var name = isNewtonsoftJson
            ? field.GetCustomAttribute<EnumMemberAttribute>()?.Value
            : field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name;
        return string.IsNullOrEmpty(name) ? field.Name : name;
    }

    /// <summary>
    ///   The name one member is written as on its own, as an entry of the
    ///   list, or the number for a value no member has.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="value">The member's value.</param>
    /// <param name="isNewtonsoftJson">Whether the name is Newtonsoft's.</param>
    /// <returns>The name.</returns>
    public static string GetMemberName(Type type, object value, bool isNewtonsoftJson)
        => Enum.GetName(type, value) is { } name
            ? GetWireName(type.GetField(name)!, isNewtonsoftJson)
            : Convert.ToString(Convert.ChangeType(value, Enum.GetUnderlyingType(type), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)!;

    private static FlagEnumShape GetShape(Type type)
        => _shapes.GetOrAdd(type, CreateShape);

    private static FlagEnumShape CreateShape(Type type)
    {
        var members = type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(x => x.MetadataToken)
            .Select(x => new FlagEnumMember(x, ToBits(type, x.GetValue(null)!)))
            .ToList();
        var entries = new List<FlagEnumMember>();
        var covered = 0UL;
        foreach (var member in members)
        {
            if (!IsSingleBit(member.Bits) || (covered & member.Bits) != 0)
                continue;

            covered |= member.Bits;
            entries.Add(member);
        }

        return new(members, entries, covered);
    }

    private static bool IsSingleBit(ulong bits)
        => bits != 0 && (bits & (bits - 1)) == 0;

    /// <summary>
    ///   The bits of a value, masked to the width of the enum, so a signed
    ///   member never sign-extends into bits the enum does not have.
    /// </summary>
    private static ulong ToBits(Type type, object value)
        => Type.GetTypeCode(Enum.GetUnderlyingType(type)) switch
        {
            TypeCode.SByte => (byte)Convert.ToSByte(value, CultureInfo.InvariantCulture),
            TypeCode.Int16 => (ushort)Convert.ToInt16(value, CultureInfo.InvariantCulture),
            TypeCode.Int32 => (uint)Convert.ToInt32(value, CultureInfo.InvariantCulture),
            TypeCode.Int64 => (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture),
            _ => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
        };

    private sealed record FlagEnumMember(FieldInfo Field, ulong Bits);

    private sealed record FlagEnumShape(IReadOnlyList<FlagEnumMember> Members, IReadOnlyList<FlagEnumMember> Entries, ulong Covered);

    #endregion

    #region Write

    /// <summary>
    ///   The names of the entries a value holds, in declaration order.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="value">The value.</param>
    /// <param name="isNewtonsoftJson">Whether the names are Newtonsoft's.</param>
    /// <returns>The names, empty for zero.</returns>
    /// <exception cref="FormatException">
    ///   The value holds bits no entry names.
    /// </exception>
    public static IReadOnlyList<string> GetNames(Type type, object value, bool isNewtonsoftJson)
    {
        var shape = GetShape(type);
        var bits = ToBits(type, value);
        if ((bits & ~shape.Covered) != 0)
            throw new FormatException($"The value {bits} of {type.Name} holds bits no single-bit member names.");

        return shape.Entries
            .Where(x => (bits & x.Bits) != 0)
            .Select(x => GetWireName(x.Field, isNewtonsoftJson))
            .ToList();
    }

    #endregion

    #region Read

    /// <summary>
    ///   Reads a value from member names, each one an entry or a combined
    ///   member, matched by its written or declared name, ignoring case.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="names">The names.</param>
    /// <param name="isNewtonsoftJson">Whether the names are Newtonsoft's.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">
    ///   A name matches no member, or a member holds bits no entry names.
    /// </exception>
    public static object FromNames(Type type, IEnumerable<string> names, bool isNewtonsoftJson)
    {
        var bits = 0UL;
        foreach (var name in names)
            bits |= ReadName(type, name.Trim(), isNewtonsoftJson);
        return FromBits(type, bits);
    }

    /// <summary>
    ///   Reads a value from the comma-separated text an enum used to be
    ///   written as, or from its number written as text.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="text">The text.</param>
    /// <param name="isNewtonsoftJson">Whether the names are Newtonsoft's.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">
    ///   The text names no member, or holds bits no entry names.
    /// </exception>
    public static object FromText(Type type, string text, bool isNewtonsoftJson)
    {
        var trimmed = text.Trim();
        if (trimmed.Length > 0 && (char.IsAsciiDigit(trimmed[0]) || trimmed[0] is '-') &&
            long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return FromNumber(type, number);

        return FromNames(type, trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), isNewtonsoftJson);
    }

    /// <summary>
    ///   Reads a value from its bits.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="bits">The bits.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">
    ///   Some of the bits are named by no entry.
    /// </exception>
    public static object FromBits(Type type, ulong bits)
    {
        if ((bits & ~GetShape(type).Covered) != 0)
            throw new FormatException($"The value {bits} of {type.Name} holds bits no single-bit member names.");

        return Enum.ToObject(type, bits);
    }

    /// <summary>
    ///   Reads a value from a number.
    /// </summary>
    /// <param name="type">The flags enum.</param>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">
    ///   Some of the bits are named by no entry.
    /// </exception>
    public static object FromNumber(Type type, long number)
        => FromBits(type, ToBits(type, Enum.ToObject(type, number)));

    private static ulong ReadName(Type type, string name, bool isNewtonsoftJson)
    {
        var shape = GetShape(type);
        var member = shape.Members.FirstOrDefault(x => string.Equals(GetWireName(x.Field, isNewtonsoftJson), name, StringComparison.OrdinalIgnoreCase))
            ?? shape.Members.FirstOrDefault(x => string.Equals(x.Field.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new FormatException($"\"{name}\" is not a member of {type.Name}.");
        return member.Bits;
    }

    #endregion
}
