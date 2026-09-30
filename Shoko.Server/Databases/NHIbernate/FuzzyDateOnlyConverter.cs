using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
///   Stores a <see cref="FuzzyDateOnly"/> as its ISO 8601 text form in a
///   short text column, and reads text it cannot parse as no date.
/// </summary>
public class FuzzyDateOnlyConverter : TypeConverter, IUserType
{
    #region TypeConverter

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType == typeof(FuzzyDateOnly) || sourceType == typeof(PartialDateOnly) || sourceType == typeof(DateOnly)
            || sourceType == typeof(DateTime) || sourceType == typeof(string);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(FuzzyDateOnly) || destinationType == typeof(string);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            FuzzyDateOnly date => date,
            PartialDateOnly date => new FuzzyDateOnly(date),
            DateOnly date => new FuzzyDateOnly(date),
            DateTime date => new FuzzyDateOnly(DateOnly.FromDateTime(date)),
            string text => FuzzyDateOnly.TryParse(text, out var date) ? date : null,
            null or DBNull => null,
            _ => throw new ArgumentException($"Cannot read a fuzzy date from a {value.GetType().Name}.", nameof(value)),
        };

    /// <inheritdoc/>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => destinationType == typeof(string)
            // The default value has no parts, so it is stored as no date.
            ? value is FuzzyDateOnly date && (date.Year.HasValue || date.Month.HasValue) ? date.ToString() : null
            : throw new ArgumentException("DestinationType must be System.String.", nameof(destinationType));

    #endregion

    #region IUserType

    /// <inheritdoc/>
    public object Assemble(object cached, object owner)
        => DeepCopy(cached);

    /// <inheritdoc/>
    public object DeepCopy(object value)
        => value;

    /// <inheritdoc/>
    public object Disassemble(object value)
        => DeepCopy(value);

    /// <inheritdoc/>
    public int GetHashCode(object x)
        => x == null ? base.GetHashCode() : x.GetHashCode();

    /// <inheritdoc/>
    public bool IsMutable
        => false;

    /// <inheritdoc/>
    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
        => ConvertFrom(null, null, NHibernateUtil.String.NullSafeGet(rs, names[0], impl));

    /// <inheritdoc/>
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = ConvertTo(null, null, value, typeof(string)) ?? (object)DBNull.Value;

    /// <inheritdoc/>
    public object Replace(object original, object target, object owner)
        => original;

    /// <inheritdoc/>
    public Type ReturnedType => typeof(FuzzyDateOnly);

    /// <inheritdoc/>
    public SqlType[] SqlTypes => [NHibernateUtil.String.SqlType];

    /// <inheritdoc/>
    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));

    #endregion
}
