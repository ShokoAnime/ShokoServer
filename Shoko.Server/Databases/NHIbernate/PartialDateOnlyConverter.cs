#nullable enable
using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Extensions;

namespace Shoko.Server.Databases.NHibernate;

public class PartialDateOnlyConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type? sourceType)
        => sourceType?.FullName switch
        {
            "Shoko.Abstractions.Metadata.PartialDateOnly" => true,
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
            "Shoko.Abstractions.Metadata.PartialDateOnly" => true,
            "System.String" => true,
            _ => false,
        };

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object? value)
        => value switch
        {
            PartialDateOnly i => i,
            DateOnly i => new PartialDateOnly(i),
            DateTime i => new PartialDateOnly(i),
            int i => new DateTime(i).ToDateOnly(),
            long i => new DateTime(i).ToDateOnly(),
            string i => PartialDateOnly.Parse(i),
            null => null,
            _ => throw new ArgumentException("DestinationType must be System.DateOnly.")
        };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type? destinationType)
        => destinationType?.FullName switch
        {
            "System.String" => value switch
            {
                PartialDateOnly i => i.ToString(),
                _ => null,
            },
            _ => throw new ArgumentException("DestinationType must be System.String."),
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
        => ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(string));

    public object Replace(object original, object target, object owner)
        => original;

    public Type ReturnedType => typeof(PartialDateOnly);

    public SqlType[] SqlTypes => [NHibernateUtil.String.SqlType];

    bool IUserType.Equals(object x, object y)
        => ReferenceEquals(x, y) || (x != null && y != null && x.Equals(y));

    #endregion
}
