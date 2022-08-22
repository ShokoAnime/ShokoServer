using System;
using System.ComponentModel;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using System.Data;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Databases.TypeConverters
{
    public class TitleTypeConverter : TypeConverter, IUserType
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            // any integer type is accepted. No fractional types like float/double.
            return sourceType.FullName switch
            {
                "Shoko.Plugin.Abstractions.DataModels.TitleType" => true,
                _ => false,
            };
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            // any integer type is accepted. No fractional types like float/double.
            switch(destinationType.FullName)
            {
                case "System.String":
                case "System.Int32":
                case "System.Int64":
                    return true;
                default:
                    return false;
            }
        }

        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            return value switch
            {
                null => TitleType.None,
                long i => (TitleType)i,
                int i => (TitleType)i,
                string s => s.GetTitleType(),
                _ => throw new ArgumentException("DestinationType must be string or int"),
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
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if(value == null) throw new ArgumentNullException(nameof(value), @"Value can't be null");

            if(value is not TitleType t) throw new ArgumentException(@"Value isn't of type TitleType", nameof(value));

            return destinationType.FullName switch
            {
                "System.String" => t.GetString(),
                "System.Int32" => (int)t,
                "System.Int64" => (long)t,
                _ => throw new ArgumentException("DestinationType must be string or int"),
            };
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
            return x == null? base.GetHashCode() : x.GetHashCode();
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
            ((IDataParameter)cmd.Parameters[index]).Value = value == null ? DBNull.Value : ConvertTo(null, null, value, typeof(string));
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
        public Type ReturnedType => typeof(string);

        /// <summary>
        /// The SQL types for the columns mapped by this type.
        /// </summary>
        /// <value></value>
        public SqlType[] SqlTypes => new[] { NHibernateUtil.String.SqlType };

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
            if(ReferenceEquals(x, y))
            {
                return true;
            }
            return x != null && y != null && x.Equals(y);
        }
        #endregion

    }
}