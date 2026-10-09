using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Shoko.Server.Utilities;

/// <summary>
///   Reads and writes a type that implements <see cref="IParsable{TSelf}"/>
///   of itself as text, the way the JSON converters and the schema describe
///   one.
/// </summary>
/// <remarks>
///   The value is written with its invariant <c>ToString()</c> and read with
///   <c>TryParse</c>. Types of the <c>System</c> namespaces keep what the
///   serializers already do with them.
/// </remarks>
internal static class ParsableTypes
{
    #region Shape

    private delegate bool TryParseText(string text, [NotNullWhen(true)] out object? value);

    private static readonly ConcurrentDictionary<Type, TryParseText> _parsers = new();

    private static readonly MethodInfo _tryParseMethod = typeof(ParsableTypes)
        .GetMethod(nameof(TryParseGeneric), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    ///   Whether the type is parsable from text and described as text: it
    ///   implements <see cref="IParsable{TSelf}"/> of itself, has no
    ///   <see cref="TypeConverterAttribute"/> and is not of a <c>System</c>
    ///   namespace.
    /// </summary>
    /// <param name="type">The type, which is not unwrapped.</param>
    /// <returns><c>true</c> for a parsable type.</returns>
    public static bool IsParsable(Type? type)
        => type is { IsInterface: false, IsEnum: false, IsPrimitive: false, ContainsGenericParameters: false } &&
            type.Namespace?.StartsWith("System", StringComparison.Ordinal) is not true &&
            type.GetCustomAttribute<TypeConverterAttribute>(true) is null &&
            type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IParsable<>) && x.GetGenericArguments()[0] == type);

    /// <summary>
    ///   Whether a JSON converter writes the type, or the type a
    ///   <see cref="Nullable{T}"/> wraps, as text: it is parsable and declares
    ///   no JSON converter of its own for the serializer.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="isNewtonsoftJson">
    ///   Whether Newtonsoft writes it, honouring its own
    ///   <see cref="Newtonsoft.Json.JsonConverterAttribute"/>, rather than
    ///   System.Text.Json, honouring
    ///   <see cref="System.Text.Json.Serialization.JsonConverterAttribute"/>.
    /// </param>
    /// <param name="parsableType">The parsable type, when it is one.</param>
    /// <returns><c>true</c> when the converter takes the type.</returns>
    public static bool IsConverted(Type? type, bool isNewtonsoftJson, out Type parsableType)
    {
        parsableType = type is null ? null! : Nullable.GetUnderlyingType(type) ?? type;
        if (!IsParsable(parsableType))
            return false;

        return isNewtonsoftJson
            ? parsableType.GetCustomAttribute<Newtonsoft.Json.JsonConverterAttribute>(true) is null
            : parsableType.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>(true) is null;
    }

    #endregion

    #region Write

    /// <summary>
    ///   The text a value is written as.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>
    ///   Its invariant <see cref="IFormattable.ToString(string, IFormatProvider)"/>,
    ///   or its <c>ToString()</c>.
    /// </returns>
    public static string ToText(object value)
        => value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;

    #endregion

    #region Read

    /// <summary>
    ///   Parses text as a parsable type, with the invariant culture.
    /// </summary>
    /// <param name="type">The parsable type.</param>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    /// <exception cref="FormatException">The text is not a value of the type.</exception>
    public static object FromText(Type type, string text)
    {
        var parser = _parsers.GetOrAdd(type, static x => _tryParseMethod.MakeGenericMethod(x).CreateDelegate<TryParseText>());
        return parser(text, out var value)
            ? value
            : throw new FormatException($"\"{text}\" is not a valid {type.Name}.");
    }

    private static bool TryParseGeneric<T>(string text, [NotNullWhen(true)] out object? value) where T : IParsable<T>
    {
        if (T.TryParse(text, CultureInfo.InvariantCulture, out var result) && result is not null)
        {
            value = result;
            return true;
        }

        value = null;
        return false;
    }

    #endregion
}
