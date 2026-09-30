using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using Shoko.Abstractions.Metadata.Converters;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Names one entry of one source: its <see cref="Source"/>, its
///   <see cref="EntityType"/> and the <see cref="ID"/> the source gave it.
///   Written as <c>&lt;source&gt;://&lt;entity type&gt;/&lt;id&gt;</c>, e.g.
///   <c>anidb://series/1</c>.
/// </summary>
[TypeConverter(typeof(MetadataGuidTypeConverter))]
[System.Text.Json.Serialization.JsonConverter(typeof(MetadataGuidJsonConverter))]
[Newtonsoft.Json.JsonConverter(typeof(MetadataGuidNewtonsoftJsonConverter))]
public sealed class MetadataGuid : IEquatable<MetadataGuid>, IComparable<MetadataGuid>, IComparable, IParsable<MetadataGuid>
{
    #region Constants

    /// <summary>
    ///   The most characters an <see cref="ID"/> can have.
    /// </summary>
    public const int MaxIDLength = 128;

    /// <summary>
    ///   The separator between the source and the rest of the text form.
    /// </summary>
    private const string SchemeSeparator = "://";

    #endregion

    #region Fields

    /// <summary>
    ///   The hash code, once worked out, or <c>0</c>.
    /// </summary>
    private int _hashCode;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates an identifier from a source, a kind and an ID.
    /// </summary>
    /// <param name="source">The source, registered or not.</param>
    /// <param name="entityType">The kind of entity, registered or not.</param>
    /// <param name="id">
    ///   The ID the source gave the entry. 1 to 128 characters, with no
    ///   whitespace at either end; slashes are allowed.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/>, <paramref name="entityType"/> or
    ///   <paramref name="id"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="id"/> is empty, too long or has whitespace at an end.
    /// </exception>
    public MetadataGuid(MetadataSource source, MetadataEntityType entityType, string id)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(id);
        if (!IsValidID(id))
            throw new ArgumentException($"\"{id}\" is not a valid metadata ID; it must be 1 to {MaxIDLength} characters, with no whitespace at either end.", nameof(id));

        Source = source;
        EntityType = entityType;
        ID = id;
        IsNumericID = IsCanonicalInteger(id);
    }

    /// <summary>
    ///   Creates an identifier from the text of a registered source and a
    ///   registered kind, each a value or an alias, ignoring case.
    /// </summary>
    /// <param name="source">The source's value or alias, e.g. <c>anidb</c>.</param>
    /// <param name="entityType">The kind's value or alias, e.g. <c>series</c>.</param>
    /// <param name="id">The ID the source gave the entry.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="KeyNotFoundException">
    ///   The source or the kind is not registered.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="id"/> is not a valid ID.
    /// </exception>
    public static MetadataGuid For(string source, string entityType, string id)
        => new(MetadataSource.Get(source), MetadataEntityType.Get(entityType), id);

    #endregion

    #region Properties

    /// <summary>
    ///   The source the entry is from.
    /// </summary>
    public MetadataSource Source { get; }

    /// <summary>
    ///   The kind of entity the entry is.
    /// </summary>
    public MetadataEntityType EntityType { get; }

    /// <summary>
    ///   The ID the source gave the entry, as text. Unique only within its
    ///   source and kind.
    /// </summary>
    public string ID { get; }

    /// <summary>
    ///   Whether <see cref="ID"/> is a non-negative integer written the
    ///   canonical way: digits only, with no leading zeros but for <c>0</c>
    ///   itself.
    /// </summary>
    public bool IsNumericID { get; }

    #endregion

    #region Numeric IDs

    /// <summary>
    ///   Reads <see cref="ID"/> as an integer of type
    ///   <typeparamref name="T"/>, if it is numeric and fits.
    /// </summary>
    /// <typeparam name="T">The integer type, e.g. <see cref="int"/>.</typeparam>
    /// <param name="id">The ID as a number, if it could be read.</param>
    /// <returns>
    ///   <c>true</c> if the ID is <see cref="IsNumericID">numeric</see> and in
    ///   range for <typeparamref name="T"/>.
    /// </returns>
    public bool TryGetNumericID<T>(out T id) where T : IBinaryInteger<T>
    {
        if (IsNumericID)
        {
            // Converted through a wide integer, since some types (char) parse
            // text their own way; it fits if it survives the trip back.
            var value = ParseNumericID();
            id = T.CreateSaturating(value);
            if (BigInteger.CreateSaturating(id) == value)
                return true;
        }

        id = T.Zero;
        return false;
    }

    /// <summary>
    ///   Reads <see cref="ID"/> as an integer of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The integer type, e.g. <see cref="int"/>.</typeparam>
    /// <returns>The ID as a number.</returns>
    /// <exception cref="FormatException">
    ///   The ID is not <see cref="IsNumericID">numeric</see>.
    /// </exception>
    /// <exception cref="OverflowException">
    ///   The ID is out of range for <typeparamref name="T"/>.
    /// </exception>
    public T GetNumericID<T>() where T : IBinaryInteger<T>
        => IsNumericID
            ? T.CreateChecked(ParseNumericID())
            : throw new FormatException($"The metadata ID \"{ID}\" is not numeric.");

    /// <summary>
    ///   Reads a <see cref="IsNumericID">numeric</see> <see cref="ID"/> as a
    ///   number of any size.
    /// </summary>
    /// <returns>The ID as a number.</returns>
    private BigInteger ParseNumericID()
        => BigInteger.Parse(ID, NumberStyles.None, CultureInfo.InvariantCulture);

    #endregion

    #region Parsing

    /// <summary>
    ///   Turns the text form into an identifier. The source and the kind are
    ///   read as their own <c>Parse</c> reads them: a value or an alias,
    ///   ignoring case, or any other valid value as an unregistered one.
    /// </summary>
    /// <param name="s">The text, e.g. <c>anidb://series/1</c>.</param>
    /// <param name="provider">Ignored.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="FormatException">
    ///   <paramref name="s"/> is not a valid identifier.
    /// </exception>
    public static MetadataGuid Parse(string s, IFormatProvider? provider = null)
        => TryParse(s, provider, out var guid) ? guid : throw new FormatException($"\"{s}\" is not a valid metadata identifier.");

    /// <summary>
    ///   Turns the text form into an identifier, as
    ///   <see cref="Parse(string, IFormatProvider?)"/> does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Ignored.</param>
    /// <param name="result">The identifier, if the text was valid.</param>
    /// <returns><c>true</c> if the text named an identifier.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out MetadataGuid result)
    {
        result = null;
        if (!TrySplit(s, out var sourceText, out var entityTypeText, out var id))
            return false;

        // Both parts are checked before either is parsed, since parsing keeps
        // any unregistered value it makes, even when the other part is bad.
        if (!(MetadataSource.TryGet(sourceText, out _) || MetadataSource.IsValidValue(MetadataSource.ToCandidateValue(sourceText))) ||
            !(MetadataEntityType.TryGet(entityTypeText, out _) || MetadataEntityType.IsValidValue(MetadataEntityType.ToCandidateValue(entityTypeText))))
            return false;
        if (!MetadataSource.TryParse(sourceText, out var source) || !MetadataEntityType.TryParse(entityTypeText, out var entityType))
            return false;

        result = new(source, entityType, id);
        return true;
    }

    /// <summary>
    ///   Turns the text form into an identifier, as
    ///   <see cref="Parse(string, IFormatProvider?)"/> does, without throwing.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="result">The identifier, if the text was valid.</param>
    /// <returns><c>true</c> if the text named an identifier.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out MetadataGuid result)
        => TryParse(s, null, out result);

    /// <summary>
    ///   Splits the text form at the first <c>://</c> and then at the first
    ///   slash after it, so the ID may hold slashes of its own.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="source">The text before <c>://</c>.</param>
    /// <param name="entityType">The text between <c>://</c> and the slash.</param>
    /// <param name="id">The text after the slash.</param>
    /// <returns>
    ///   <c>true</c> if both separators were found, the source and kind have
    ///   no whitespace, and the ID is valid.
    /// </returns>
    private static bool TrySplit(string? text, out string source, out string entityType, out string id)
    {
        source = entityType = id = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var schemeEnd = text.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        if (schemeEnd <= 0)
            return false;

        var kindStart = schemeEnd + SchemeSeparator.Length;
        var slash = text.IndexOf('/', kindStart);
        if (slash <= kindStart)
            return false;

        source = text[..schemeEnd];
        entityType = text[kindStart..slash];
        id = text[(slash + 1)..];
        return !ContainsWhiteSpace(source) && !ContainsWhiteSpace(entityType) && IsValidID(id);
    }

    private static bool ContainsWhiteSpace(string text)
    {
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
                return true;
        }

        return false;
    }

    private static bool IsValidID(string id)
        => id.Length is > 0 and <= MaxIDLength && !char.IsWhiteSpace(id[0]) && !char.IsWhiteSpace(id[^1]);

    private static bool IsCanonicalInteger(string id)
    {
        if (id is "0")
            return true;
        if (id[0] is '0')
            return false;

        foreach (var character in id)
        {
            if (!char.IsAsciiDigit(character))
                return false;
        }

        return true;
    }

    #endregion

    #region Equality

    /// <summary>
    ///   Checks whether another identifier has the same source value, kind
    ///   value and ID, compared ordinally.
    /// </summary>
    /// <param name="other">The identifier to compare with.</param>
    /// <returns><c>true</c> if the two name the same entry.</returns>
    public bool Equals(MetadataGuid? other)
        => other is not null && Source.Equals(other.Source) && EntityType.Equals(other.EntityType) && string.Equals(ID, other.ID, StringComparison.Ordinal);

    /// <summary>
    ///   Checks whether an object is an identifier naming the same entry.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if the object is an equal identifier.</returns>
    public override bool Equals(object? obj)
        => obj is MetadataGuid other && Equals(other);

    /// <summary>
    ///   Gets a hash code from the source value, kind value and ID.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        // Worked out once, as identifiers key the text and link lookups.
        var hashCode = _hashCode;
        if (hashCode is 0)
            _hashCode = hashCode = HashCode.Combine(Source, EntityType, StringComparer.Ordinal.GetHashCode(ID));
        return hashCode;
    }

    /// <summary>
    ///   Checks whether two identifiers name the same entry.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><c>true</c> if both are <c>null</c> or name the same entry.</returns>
    public static bool operator ==(MetadataGuid? left, MetadataGuid? right)
        => left is null ? right is null : left.Equals(right);

    /// <summary>
    ///   Checks whether two identifiers name different entries.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><c>true</c> if they name different entries.</returns>
    public static bool operator !=(MetadataGuid? left, MetadataGuid? right)
        => !(left == right);

    /// <summary>
    ///   Compares two identifiers by source, then kind, then ID. Numeric IDs
    ///   come first, in numeric order, and the others follow ordinally.
    /// </summary>
    /// <param name="other">The identifier to compare with.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    public int CompareTo(MetadataGuid? other)
    {
        if (other is null)
            return 1;

        var bySource = Source.CompareTo(other.Source);
        if (bySource is not 0)
            return bySource;

        var byEntityType = EntityType.CompareTo(other.EntityType);
        if (byEntityType is not 0)
            return byEntityType;

        // Canonical integers of the same length sort ordinally in numeric order.
        if (IsNumericID != other.IsNumericID)
            return IsNumericID ? -1 : 1;
        if (IsNumericID && ID.Length != other.ID.Length)
            return ID.Length.CompareTo(other.ID.Length);
        return string.CompareOrdinal(ID, other.ID);
    }

    /// <summary>
    ///   Compares with another identifier, as
    ///   <see cref="CompareTo(MetadataGuid?)"/> does.
    /// </summary>
    /// <param name="obj">The identifier to compare with, or <c>null</c>.</param>
    /// <returns>Less than zero, zero or more than zero, as for strings.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="obj"/> is neither <c>null</c> nor an identifier.
    /// </exception>
    int IComparable.CompareTo(object? obj)
        => obj is null or MetadataGuid ? CompareTo(obj as MetadataGuid) : throw new ArgumentException("Can only compare with another metadata identifier.", nameof(obj));

    /// <summary>
    ///   Gets the text form, with the canonical source and kind values, e.g.
    ///   <c>anidb://series/1</c>.
    /// </summary>
    /// <returns>The text form.</returns>
    public override string ToString()
        => $"{Source.Value}{SchemeSeparator}{EntityType.Value}/{ID}";

    #endregion
}
