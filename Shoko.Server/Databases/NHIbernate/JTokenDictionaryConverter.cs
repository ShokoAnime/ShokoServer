using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

#nullable enable
namespace Shoko.Server.Databases.NHibernate;

public class JTokenDictionaryConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType?.FullName switch
        {
            nameof(Dictionary<string, JToken>) => true,
            nameof(String) => true,
            _ => false
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType?.FullName switch
        {
            nameof(String) => true,
            _ => false,
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            null => new Dictionary<string, JToken>(),
            string i => string.IsNullOrEmpty(i)
                ? new Dictionary<string, JToken>()
                : JsonConvert.DeserializeObject<Dictionary<string, JToken>>(i) ?? new Dictionary<string, JToken>(),
            Dictionary<string, JToken> d => d,
            _ => throw new ArgumentException($"DestinationType must be {nameof(String)}.")
        };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => value switch
        {
            null => string.Empty,
            string i => i,
            Dictionary<string, JToken> d => JsonConvert.SerializeObject(d),
            _ => throw new ArgumentException($"DestinationType must be {typeof(Dictionary<string, JToken>).FullName}."),
        };

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary? propertyValues)
        => true;

    #region IUserType Members

    public object Assemble(object cached, object owner)
        => DeepCopy(cached);

    public object DeepCopy(object value)
        => value is Dictionary<string, JToken> d
            ? d.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone())
            : value;

    public object Disassemble(object value)
        => DeepCopy(value);

    public int GetHashCode(object x)
        => x == null ? base.GetHashCode() : x.GetHashCode();

    public bool IsMutable
        => true;

    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
        => ConvertFrom(null, null, NHibernateUtil.String.NullSafeGet(rs, names[0], impl));

    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(Dictionary<string, JToken>));

    public object Replace(object original, object target, object owner)
        => original;

    public Type ReturnedType
        => typeof(Dictionary<string, JToken>);

    public SqlType[] SqlTypes
        => new[] { NHibernateUtil.String.SqlType };

    bool IUserType.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is not Dictionary<string, JToken> dx || y is not Dictionary<string, JToken> dy)
            return false;
        if (dx.Count != dy.Count)
            return false;
        foreach (var (key, value) in dx)
        {
            if (!dy.TryGetValue(key, out var other) || !JToken.DeepEquals(value, other))
                return false;
        }
        return true;
    }

    #endregion
}
