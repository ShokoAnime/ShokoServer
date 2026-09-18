using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Newtonsoft.Json;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
/// Stores a list as a JSON array in a single text column.
/// </summary>
/// <remarks>
/// <see cref="StringListConverter"/> stores a <c>|||</c> delimited string,
/// which neither round-trips values containing the delimiter nor carries
/// anything but strings, so columns documented as JSON arrays use this instead.
/// </remarks>
/// <typeparam name="T">The type of the items in the list.</typeparam>
public class JsonListConverter<T> : TypeConverter, IUserType
{
    /// <summary>
    /// The settings used for both directions, so a value written by one server
    /// reads back the same on another.
    /// </summary>
    private static readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Include,
        DefaultValueHandling = DefaultValueHandling.Include,
        Culture = CultureInfo.InvariantCulture,
    };

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType == typeof(string) || sourceType == typeof(List<T>);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is neither a <see cref="string"/> nor a <see cref="List{T}"/>.</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            null => new List<T>(),
            string text => string.IsNullOrWhiteSpace(text) ? new List<T>() : JsonConvert.DeserializeObject<List<T>>(text, _settings) ?? new List<T>(),
            List<T> list => list,
            _ => throw new ArgumentException($"SourceType must be {nameof(String)} or {typeof(List<T>).FullName}."),
        };

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is neither a <see cref="string"/> nor a <see cref="List{T}"/>.</exception>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => value switch
        {
            null => "[]",
            string text => text,
            List<T> list => JsonConvert.SerializeObject(list, _settings),
            _ => throw new ArgumentException($"SourceType must be {nameof(String)} or {typeof(List<T>).FullName}."),
        };

    /// <inheritdoc/>
    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary? propertyValues)
        => true;

    #region IUserType Members

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
    // Hashed on the serialized form, to match the Equals below.
    public int GetHashCode(object x)
        => x == null ? base.GetHashCode() : ConvertTo(null, null, x, typeof(string))?.GetHashCode() ?? 0;

    /// <inheritdoc/>
    public bool IsMutable
        => true;

    /// <inheritdoc/>
    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
        => ConvertFrom(null, null, NHibernateUtil.String.NullSafeGet(rs, names[0], impl));

    /// <inheritdoc/>
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(string));

    /// <inheritdoc/>
    public object Replace(object original, object target, object owner)
        => original;

    /// <inheritdoc/>
    public Type ReturnedType
        => typeof(List<T>);

    /// <inheritdoc/>
    public SqlType[] SqlTypes
        => [NHibernateUtil.String.SqlType];

    /// <inheritdoc/>
    bool IUserType.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        // Compare what would be written, not the list references.
        return Equals(ConvertTo(null, null, x, typeof(string)), ConvertTo(null, null, y, typeof(string)));
    }

    #endregion
}
