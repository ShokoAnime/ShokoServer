using System;
using System.Collections;
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
/// Stores one object as a JSON object in a single nullable text column.
/// </summary>
/// <remarks>
/// A property missing from the stored JSON reads as its default and one the
/// type does not know is skipped, so the type can grow without a schema step.
/// A <c>null</c> value is stored as SQL <c>NULL</c>. The type is treated as
/// immutable, so replace the value rather than change it in place.
/// </remarks>
/// <typeparam name="T">The type of the stored object.</typeparam>
public class JsonObjectConverter<T> : TypeConverter, IUserType where T : class
{
    /// <summary>
    /// The settings used for both directions, so a value written by one server
    /// reads back the same on another.
    /// </summary>
    private static readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Culture = CultureInfo.InvariantCulture,
    };

    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType == typeof(string) || sourceType == typeof(T);

    /// <inheritdoc/>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is neither a <see cref="string"/> nor a <typeparamref name="T"/>.</exception>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            null => null,
            string text => string.IsNullOrWhiteSpace(text) ? null : JsonConvert.DeserializeObject<T>(text, _settings),
            T item => item,
            _ => throw new ArgumentException($"SourceType must be {nameof(String)} or {typeof(T).FullName}."),
        };

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="value"/> is neither a <see cref="string"/> nor a <typeparamref name="T"/>.</exception>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => value switch
        {
            null => null,
            string text => text,
            T item => JsonConvert.SerializeObject(item, _settings),
            _ => throw new ArgumentException($"SourceType must be {nameof(String)} or {typeof(T).FullName}."),
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
        => false;

    /// <inheritdoc/>
    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
        => ConvertFrom(null, null, NHibernateUtil.String.NullSafeGet(rs, names[0], impl));

    /// <inheritdoc/>
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = ConvertTo(null, null, value, typeof(string)) ?? DBNull.Value;

    /// <inheritdoc/>
    public object Replace(object original, object target, object owner)
        => original;

    /// <inheritdoc/>
    public Type ReturnedType
        => typeof(T);

    /// <inheritdoc/>
    /// <remarks>
    /// A CLOB, since SQL Server's driver cuts a plain string parameter to
    /// 4000 characters and a long object is written as invalid JSON.
    /// </remarks>
    public SqlType[] SqlTypes
        => [NHibernateUtil.StringClob.SqlType];

    /// <inheritdoc/>
    bool IUserType.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        // Compare what would be written, not the references.
        return Equals(ConvertTo(null, null, x, typeof(string)), ConvertTo(null, null, y, typeof(string)));
    }

    #endregion
}
