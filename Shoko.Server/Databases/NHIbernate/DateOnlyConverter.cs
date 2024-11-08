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

#nullable enable
namespace Shoko.Server.Databases.NHibernate;

public class DateOnlyConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType?.FullName switch
        {
            "System.DateOnly" => true,
            "System.DateTime" => true,
            "System.String" => true,
            "System.Int32" => true,
            "System.Int64" => true,
            _ => false
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType?.FullName switch
        {
            "System.DateTime" => true,
            "System.String" => true,
            "System.Int32" => true,
            "System.Int64" => true,
            _ => false,
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            DateOnly i => i,
            DateTime i => DateOnly.FromDateTime(i),
            int i => DateOnly.FromDateTime(new(i)),
            long i => DateOnly.FromDateTime(new(i)),
            string i => DateOnly.FromDateTime(DateTime.Parse(i)),
            null => null,
            _ => throw new ArgumentException("DestinationType must be System.DateOnly.")
        };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => destinationType?.FullName switch
        {
            "System.Int32" => value switch
            {
                DateOnly i => (int)i.ToDateTime(TimeOnly.MinValue).Ticks,
                _ => null,
            },
            "System.Int64" => value switch
            {
                DateOnly i => (long)i.ToDateTime(TimeOnly.MinValue).Ticks,
                _ => null,
            },
            "System.String" => value switch
            {
                DateOnly i => i.ToLongDateString(),
                _ => null,
            },
            "System.DateTime" => value switch
            {
                DateOnly i => i.ToDateTime(TimeOnly.MinValue),
                _ => null,
            },
            _ => throw new ArgumentException("DestinationType must be System.Int32, System.Int64, System.String, or System.DateTime."),
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
        => ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(DateTime));

    public object Replace(object original, object target, object owner)
        => original;

    public Type ReturnedType
        => typeof(DateTime);

    public SqlType[] SqlTypes
        => new[] { NHibernateUtil.Date.SqlType };

    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));

    #endregion
}
