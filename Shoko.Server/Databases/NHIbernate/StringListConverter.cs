using System;
using System.ComponentModel;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using System.Data;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using System.Globalization;
using System.Collections;
using NHibernate.Mapping;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.Extensions;

#nullable enable
namespace Shoko.Server.Databases.NHibernate;

public class StringListConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType?.FullName switch
        {
            nameof(List<string>) => true,
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
            string i => i.Split("|||").ToList(),
            List<string> l => l,
            _ => throw new ArgumentException($"DestinationType must be {nameof(String)}.")
        };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => value switch
        {
            string i => i,
            List<string> l => l.Join("|||"),
            _ => throw new ArgumentException($"DestinationType must be {typeof(List<string>).FullName}."),
        };

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary? propertyValues)
        => true;

    #region IUserType Members

    public object Assemble(object cached, object owner)
        => DeepCopy(cached);

    public object DeepCopy(object value)
        => value;

    public object Disassemble(object value)
        => DeepCopy(value);

    public int GetHashCode(object x)
        => x == null ? base.GetHashCode() : x.GetHashCode();

    public bool IsMutable
        => true;

    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
        => ConvertFrom(null, null, NHibernateUtil.String.NullSafeGet(rs, names[0], impl));

    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(List<string>));

    public object Replace(object original, object target, object owner)
        => original;

    public Type ReturnedType
        => typeof(List<string>);

    public SqlType[] SqlTypes
        => new[] { NHibernateUtil.String.SqlType };

    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));

    #endregion
}
