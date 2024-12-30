using System;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;

namespace Shoko.Server.Databases.NHIbernate;

//mpiva: PostgreSQL nhibernate does not support directly mapping a bool against an int, like the other DB, this will fix the issue.

public class BoolToIntConverter : TypeConverter, IUserType
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(bool) || sourceType == typeof(bool?);
    }
    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
    {
        return destinationType == typeof(int) || destinationType == typeof(short) || destinationType == typeof(byte) || destinationType == typeof(long) ||
               destinationType == typeof(uint) || destinationType == typeof(ushort) || destinationType == typeof(ulong) || destinationType == typeof(bool) ||
               destinationType == typeof(int?) || destinationType == typeof(short?) || destinationType == typeof(byte?) || destinationType == typeof(long?) ||
               destinationType == typeof(uint?) || destinationType == typeof(ushort?) || destinationType == typeof(ulong?) || destinationType == typeof(bool?);
    }
    public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture,
        object value)
    {
        return value switch
        {
            null => null,
            int i => i != 0,
            short i => i!=0,
            byte i => i != 0,
            long i => i != 0,
            uint i => i != 0,
            ushort i => i != 0,
            ulong i => i != 0,
            bool i => i,
            string i => (i == "1" || i == "true") ? true : false,
            _ => throw new ArgumentException("value should support converting to bool")
        };
    }

    /// <summary>
    /// Converts the given value object to the specified type
    /// </summary>
    /// <param name="context">Ignored</param>
    /// <param name="culture">Ignored</param>
    /// <param name="value">The <see cref="T:System.Object"/> to convert.</param>
    /// <param name="destinationType">The <see cref="T:System.Type"/> to convert the <paramref name="value"/> parameter to.</param>
    /// <returns>
    /// An <see cref="T:System.Object"/> that represents the converted value. The value will be 1 if <paramref name="value"/> is true, otherwise 0
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The <paramref name="destinationType"/> parameter is <see langword="null"/>.</exception>
    /// <exception cref="T:System.NotSupportedException">The conversion could not be performed.</exception>
    public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture,
        object value, Type destinationType)
    {
        bool real = (bool)value;
        if (destinationType == typeof(int))
            return (int)(real ? 1 : 0);
        if (destinationType == typeof(int?))
            return (int?)(real ? 1 : 0);
        if (destinationType == typeof(short))
            return (short)(real ? 1 : 0);
        if (destinationType == typeof(short?))
            return (short?)(real ? 1 : 0);
        if (destinationType == typeof(byte))
            return (byte)(real ? 1 : 0);
        if (destinationType == typeof(long))
            return (long)(real ? 1 : 0);
        if (destinationType == typeof(long?))
            return (long?)(real ? 1 : 0);
        if (destinationType == typeof(uint))
            return (uint)(real ? 1 : 0);
        if (destinationType == typeof(uint?))
            return (uint?)(real ? 1 : 0);
        if (destinationType == typeof(ushort))
            return (ushort)(real ? 1 : 0);
        if (destinationType == typeof(ushort?))
            return (ushort?)(real ? 1 : 0);
        if (destinationType == typeof(ulong))
            return (ulong)(real ? 1 : 0);
        if (destinationType == typeof(ulong?))
            return (ulong?)(real ? 1 : 0);
        if (destinationType == typeof(bool))
            return (bool)real;
        if (destinationType == typeof(bool?))
            return (bool?)real;
        throw new ArgumentException("DestinationType must be an integer");
    }


    /// <summary>
    /// Creates an instance of the Type that this <see cref="T:System.ComponentModel.TypeConverter"/> is associated with (bool)
    /// </summary>
    /// <param name="context">ignored.</param>
    /// <param name="propertyValues">ignored.</param>
    /// <returns>
    /// An <see cref="T:System.Object"/> of type bool. It always returns 'true' for this converter.
    /// </returns>
    public override object CreateInstance(ITypeDescriptorContext context, System.Collections.IDictionary propertyValues)
    {
        return true;
    }


    #region IUserType Members

    /// <summary>
    /// Reconstruct an object from the cacheable representation. At the very least this
    /// method should perform a deep copy if the type is mutable. (optional operation)
    /// </summary>
    /// <param name="cached">the object to be cached</param>
    /// <param name="owner">the owner of the cached object</param>
    /// <returns>
    /// a reconstructed object from the cacheable representation
    /// </returns>
    public object Assemble(object cached, object owner)
    {
        return DeepCopy(cached);
    }

    /// <summary>
    /// Return a deep copy of the persistent state, stopping at entities and at collections.
    /// </summary>
    /// <param name="value">generally a collection element or entity field</param>
    /// <returns>a copy</returns>
    public object DeepCopy(object value)
    {
        return value;
    }

    /// <summary>
    /// Transform the object into its cacheable representation. At the very least this
    /// method should perform a deep copy if the type is mutable. That may not be enough
    /// for some implementations, however; for example, associations must be cached as
    /// identifier values. (optional operation)
    /// </summary>
    /// <param name="value">the object to be cached</param>
    /// <returns>a cacheable representation of the object</returns>
    public object Disassemble(object value)
    {
        return DeepCopy(value);
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <param name="x">The x.</param>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
    /// </returns>
    public int GetHashCode(object x)
    {
        return x == null ? base.GetHashCode() : x.GetHashCode();
    }

    /// <summary>
    /// Are objects of this type mutable?
    /// </summary>
    /// <value></value>
    public bool IsMutable => true;

    /// <summary>
    /// Retrieve an instance of the mapped class from a JDBC resultset.
    /// Implementors should handle possibility of null values.
    /// </summary>
    /// <param name="rs">a IDataReader</param>
    /// <param name="names">column names</param>
    /// <param name="impl"></param>
    /// <param name="owner">the containing entity</param>
    /// <returns></returns>
    /// <exception cref="T:NHibernate.HibernateException">HibernateException</exception>
    public object NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor impl, object owner)
    {
        var rawValue = NHibernateUtil.String.NullSafeGet(rs, names[0], impl);
        return rawValue == null ? null : ConvertFrom(null!, null!, rawValue);
    }

    /// <summary>
    /// Write an instance of the mapped class to a prepared statement.
    /// Implementors should handle possibility of null values.
    /// A multi-column type should be written to parameters starting from index.
    /// </summary>
    /// <param name="cmd">a IDbCommand</param>
    /// <param name="value">the object to write</param>
    /// <param name="index">command parameter index</param>
    /// <param name="session"></param>
    /// <exception cref="T:NHibernate.HibernateException">HibernateException</exception>
    public void NullSafeSet(DbCommand cmd, object value, int index, ISessionImplementor session)
    {
        ((IDataParameter)cmd.Parameters[index]).Value =
            value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(int));
    }

    /// <summary>
    /// During merge, replace the existing (<paramref name="target"/>) value in the entity
    /// we are merging to with a new (<paramref name="original"/>) value from the detached
    /// entity we are merging. For immutable objects, or null values, it is safe to simply
    /// return the first parameter. For mutable objects, it is safe to return a copy of the
    /// first parameter. For objects with component values, it might make sense to
    /// recursively replace component values.
    /// </summary>
    /// <param name="original">the value from the detached entity being merged</param>
    /// <param name="target">the value in the managed entity</param>
    /// <param name="owner">the managed entity</param>
    /// <returns>the value to be merged</returns>
    public object Replace(object original, object target, object owner)
    {
        return original;
    }

    /// <summary>
    /// The type returned by <c>NullSafeGet()</c>
    /// </summary>
    public Type ReturnedType => typeof(int);

    /// <summary>
    /// The SQL types for the columns mapped by this type.
    /// </summary>
    /// <value></value>
    public SqlType[] SqlTypes => new[] { NHibernateUtil.Int32.SqlType };

    /// <summary>
    /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
    /// </summary>
    /// <param name="x">The <see cref="System.Object"/> to compare with this instance.</param>
    /// <param name="y">The y.</param>
    /// <returns>
    ///     <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
    /// </returns>
    bool IUserType.Equals(object x, object y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        return x != null && y != null && x.Equals(y);
    }

    #endregion
}
