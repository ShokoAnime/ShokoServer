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
namespace Shoko.Server.Databases.TypeConverters;

public class DateOnlyConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType?.FullName switch
        {
            nameof(DateOnly) => true,
            nameof(DateTime) => true,
            nameof(String) => true,
            nameof(Int32) => true,
            _ => false
        };

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType?.FullName switch
        {
            nameof(DateTime) => true,
            nameof(String) => true,
            nameof(Int32) => true,
            _ => false,
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            DateOnly i => i,
            DateTime i => DateOnly.FromDateTime(i),
            int i => DateOnly.FromDayNumber(i),
            string i => DateOnly.Parse(i),
            null => null,
            _ => throw new ArgumentException("DestinationType must be DateOnly")
        };

    public override object ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => destinationType?.FullName switch
        {
            nameof(Int32) => value switch
            {
                DateOnly i => i.DayNumber,
                _ => 0,
            },
            nameof(String) => value switch
            {
                DateOnly i => i.ToLongDateString(),
                _ => DateOnly.MinValue.ToLongDateString(),
            },
            nameof(DateTime) => value switch
            {
                DateOnly i => i.ToDateTime(TimeOnly.MinValue),
                _ => DateTime.UnixEpoch,
            },
            _ => throw new ArgumentException("DestinationType must be DateOnly")
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
        => new[] { NHibernateUtil.DateTime.SqlType };

    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));

    #endregion
}
