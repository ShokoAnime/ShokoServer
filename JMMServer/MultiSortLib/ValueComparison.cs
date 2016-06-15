using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

// Source code by Owen Emlen (owene_1998@yahoo.com, owen@binarynorthwest.com)

namespace BinaryNorthwest
{
    /// <summary>
    /// Provides quick access to property values and handles value comparisons. 
    /// Note: This class uses PropertyDescriptor.GetValue(object) to retrieve property information.  
    /// The speed of property retrieval has been greatly improved by the addition of Marc Gravell's code for
    /// HyperPropertyDescriptors, located at http://www.codeproject.com/csharp/HyperPropertyDescriptor.asp
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CompareProperties<T> : IComparer<T> where T : class
    {
        private static Dictionary<int, string> FastEnumLookup;

        public SortType sortType = SortType.eUsePropertyOrFieldType;

        #region "Fields"

        /// <summary>
        /// Stores the cached property info and type, used to retrieve values
        /// Note: Retrieving property values used to involve invoking the property Get accessor
        /// and thus was significantly slower that retrieving a field value.  However, using Marc Gravell's
        /// HyperPropertyDescriptor code, property retrieval is now much faster than retrieval of field values.
        /// </summary>
        public PropertyInfo pi;

        public PropertyDescriptor property;

        internal Type typ;
        internal bool fFoundProperty;
        internal string sPropertyName;
        internal bool fSortDescending;
        internal StringComparison stringComparisonToUse = StringComparison.Ordinal;

        #endregion

        #region "Constructors"

        public CompareProperties(string sPropName, bool fDescendingSort)
        {
            sPropertyName = sPropName;
            fSortDescending = fDescendingSort;
        }

        public CompareProperties(string sPropName, bool fDescendingSort, SortType sortTyp)
        {
            sPropertyName = sPropName;
            fSortDescending = fDescendingSort;
            sortType = sortTyp;
        }

        #endregion

        #region "Manual Overrides"

        /// <summary>
        /// Override the sort type at your own peril.  If any field/property value can't be converted
        /// to the type you specify, an exception will be raised.
        /// </summary>
        /// <param name="sortTyp"></param>
        public void SetOverrideSortType(SortType sortTyp)
        {
            sortType = sortTyp;
        }

        /// <summary>
        /// Default string comparison type is Ordinal.  Using this method, you can specify
        /// other options, such as OrdinalIgnoreCase (for case insensitive comparison), etc
        /// </summary>
        /// <param name="stringComparisonType"></param>
        public void SetStringComparisonType(StringComparison stringComparisonType)
        {
            stringComparisonToUse = stringComparisonType;
        }

        #endregion

        /// <summary>
        /// For speed, a delegate is used when we know the type of value that will be returned
        /// from the GetValue() method
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public delegate int TypeSensitiveCompare(T x, T y);

        public TypeSensitiveCompare DoCompare;

        /// <summary>
        /// Sets up cached PropertyInfo and determines the best delegate to use to compare values
        /// retrieved from that property.
        /// </summary>
        public void Initialize()
        {
            if (fFoundProperty == false)
            {
                fFoundProperty = true;
                if (pi == null)
                {
                    PropertyDescriptorCollection props = TypeDescriptor.GetProperties(typeof(T));
                    property = props[sPropertyName];
                    pi = typeof(T).GetProperty(sPropertyName);

                    if (pi == null)
                    {
                        throw new Exception("Property name " + sPropertyName +
                                            " not found while trying to compare objects of type " + typeof(T).Name);
                    }
                }
                typ = pi.PropertyType;
                // Set up the property comparison delegate to use based on the type of values we will be comparing
                if (sortType == SortType.eUsePropertyOrFieldType)
                {
                    sortType = Sorting.GetSortTypeEnumForType(typ);
                    if (typ == typeof(string))
                    {
                        if (stringComparisonToUse == StringComparison.Ordinal) DoCompare = StringCompareOrdinal;
                        else DoCompare = StringCompare;
                    }
                    else if (typ == typeof(int) && !fSortDescending) DoCompare = CompareInt;
                    else if (typ == typeof(int)) DoCompare = CompareIntDesc;
                    else if (typ == typeof(DateTime)) DoCompare = CompareDates;
                    else if (typ == typeof(long)) DoCompare = CompareTypeSensitive<long>;
                    else if (typ == typeof(double)) DoCompare = CompareTypeSensitive<double>;
                    else if (typ == typeof(float)) DoCompare = CompareTypeSensitive<float>;
                    else if (typ == typeof(short)) DoCompare = CompareTypeSensitive<short>;
                    else if (typ == typeof(byte)) DoCompare = CompareTypeSensitive<byte>;
                    else if (typ == typeof(bool)) DoCompare = CompareTypeSensitive<bool>;
                    else if (typ.BaseType == typeof(Enum))
                    {
                        FastEnumLookup = new Dictionary<int, string>(32);
                        if (fSortDescending)
                        {
                            DoCompare = FastCompareEnumsDesc;
                        }
                        else
                        {
                            DoCompare = FastCompareEnumsAsc;
                        }
                    }
                    else DoCompare = CompareUsingToString;
                }
                else
                {
                    if (sortType == SortType.eString) DoCompare = CompareUsingToString;
                    else if (sortType == SortType.eByte) DoCompare = CompareUsingToByte;
                    else if (sortType == SortType.eDateTime) DoCompare = CompareUsingToDate;
                    else if (sortType == SortType.eInteger) DoCompare = CompareUsingToInt;
                    else if (sortType == SortType.eLong) DoCompare = CompareUsingToInt64;
                    else if (sortType == SortType.eDoubleOrFloat) DoCompare = CompareUsingToDouble;
                    else DoCompare = CompareUsingToString;
                }
            }
        }

        #region "Compare method - handles value retrieval and value comparison"

        /// <summary>
        /// Compare to values, both of which are known to be of type string
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int StringCompare(T x, T y)
        {
            //int nComp = string.Compare((string)pi.GetValue(x, null), (string)pi.GetValue(y, null), stringComparisonToUse);
            int nComp = string.Compare((string) property.GetValue(x), (string) property.GetValue(y),
                stringComparisonToUse);
            return !fSortDescending ? nComp : -nComp;
        }

        public int StringCompareOrdinal(T x, T y)
        {
            //int nComp = string.Compare((string)pi.GetValue(x, null), (string)pi.GetValue(y, null), StringComparison.Ordinal);
            int nComp = string.Compare((string) property.GetValue(x), (string) property.GetValue(y),
                StringComparison.Ordinal);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareIntDesc(T x, T y)
        {
            //int oX = (int)pi.GetValue(x, null);
            //int oY = (int)pi.GetValue(y, null);
            int oX = (int) property.GetValue(x);
            int oY = (int) property.GetValue(y);
            return oX < oY ? 1 : (oX == oY ? 0 : -1);
        }

        public int CompareInt(T x, T y)
        {
            //int oX = (int)pi.GetValue(x, null);
            //int oY = (int)pi.GetValue(y, null);
            int oX = (int) property.GetValue(x);
            int oY = (int) property.GetValue(y);
            return oX < oY ? -1 : (oX == oY ? 0 : 1);
        }

        /// <summary>
        /// Compare to values, checking for null and converting to strings before comparison
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int CompareUsingToString(T x, T y)
        {
            //object oX = pi.GetValue(x, null);
            //object oY = pi.GetValue(y, null);
            object oX = property.GetValue(x);
            object oY = property.GetValue(y);
            int nComp;
            // handle null appropriately only for string sorting
            if (oX == null)
            {
                nComp = oY != null ? -1 : 0;
            }
            else if (oY == null)
            {
                nComp = 1;
            }
            else
            {
                nComp = string.Compare(oX.ToString(), oY.ToString(), stringComparisonToUse);
            }
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToByte(T x, T y)
        {
            //byte oX = Convert.ToByte(pi.GetValue(x, null));
            //byte oY = Convert.ToByte(pi.GetValue(y, null));
            byte oX = Convert.ToByte(property.GetValue(x));
            byte oY = Convert.ToByte(property.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToInt(T x, T y)
        {
            //int oX = Convert.ToInt32(pi.GetValue(x, null));
            //int oY = Convert.ToInt32(pi.GetValue(y, null));
            int oX = Convert.ToInt32(property.GetValue(x));
            int oY = Convert.ToInt32(property.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToInt64(T x, T y)
        {
            //Int64 oX = Convert.ToInt64(pi.GetValue(x, null));
            //Int64 oY = Convert.ToInt64(pi.GetValue(y, null));
            Int64 oX = Convert.ToInt64(property.GetValue(x));
            Int64 oY = Convert.ToInt64(property.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToDouble(T x, T y)
        {
            //double oX = Convert.ToDouble(pi.GetValue(x, null));
            //double oY = Convert.ToDouble(pi.GetValue(y, null));
            double oX = Convert.ToDouble(property.GetValue(x));
            double oY = Convert.ToDouble(property.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToDate(T x, T y)
        {
            //DateTime oX = Convert.ToDateTime(pi.GetValue(x, null));
            //DateTime oY = Convert.ToDateTime(pi.GetValue(y, null));
            DateTime oX = Convert.ToDateTime(property.GetValue(x));
            DateTime oY = Convert.ToDateTime(property.GetValue(y));
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareDates(T x, T y)
        {
            //DateTime oX = (DateTime)pi.GetValue(x, null);
            //DateTime oY = (DateTime)pi.GetValue(y, null);
            DateTime oX = (DateTime) property.GetValue(x);
            DateTime oY = (DateTime) property.GetValue(y);
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareTypeSensitive<T2>(T x, T y) where T2 : IComparable<T2>
        {
            //T2 oX = (T2)pi.GetValue(x, null);
            //T2 oY = (T2)pi.GetValue(y, null);
            T2 oX = (T2) property.GetValue(x);
            T2 oY = (T2) property.GetValue(y);
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        /// <summary>
        /// Faster than comparing enums using .ToString() 
        /// (the Enum.ToString() method appears to be fairly expensive)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int FastCompareEnumsAsc(T x, T y)
        {
            int oX = (int) property.GetValue(x);
            int oY = (int) property.GetValue(y);
            string s1, s2;

            if (!FastEnumLookup.TryGetValue(oX, out s1))
            {
                Enum eX = (Enum) property.GetValue(x);
                s1 = eX.ToString();
                FastEnumLookup.Add(oX, s1);
            }
            if (!FastEnumLookup.TryGetValue(oY, out s2))
            {
                Enum eY = (Enum) property.GetValue(y);
                s2 = eY.ToString();
                FastEnumLookup.Add(oY, s2);
            }
            return s1.CompareTo(s2);
        }

        public int FastCompareEnumsDesc(T x, T y)
        {
            int oX = (int) property.GetValue(x);
            int oY = (int) property.GetValue(y);
            string s1, s2;

            if (!FastEnumLookup.TryGetValue(oX, out s1))
            {
                Enum eX = (Enum) property.GetValue(x);
                s1 = eX.ToString();
                FastEnumLookup.Add(oX, s1);
            }
            if (!FastEnumLookup.TryGetValue(oY, out s2))
            {
                Enum eY = (Enum) property.GetValue(y);
                s2 = eY.ToString();
                FastEnumLookup.Add(oY, s2);
            }
            return s2.CompareTo(s1);
        }

        #endregion

        int IComparer<T>.Compare(T x, T y)
        {
            return DoCompare(x, y);
        }
    }

    /// <summary>
    /// Provides (fairly) quick access to field values and handles value comparisons. 
    /// Note: This class uses FieldInfo.GetValue to retrieve field values.  Speed of field value retrieval is significantly
    /// slower when retrieving the value of protected or private fields (due to a runtime security check when retrieving
    /// protected/private field values).  Also, due to the massive speed improvements in retrieving property values offered 
    /// by Marc Gravell in his article at http://www.codeproject.com/csharp/HyperPropertyDescriptor.asp, 
    /// property value retrieval is currently much faster than retrieving the value of a field.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CompareFields<T> : IComparer<T> where T : class
    {
        private static Dictionary<int, string> FastEnumLookup;

        public SortType sortType = SortType.eUsePropertyOrFieldType;

        #region "Internal Fields"

        /// <summary>
        /// Stores the cached field info and type, used to retrieve field values
        /// Note: Retrieving field values is moderately to significantly faster than 
        /// retrieving property values (even if the property's Get accessor only returns 
        /// the underlying field value).  However, you should make sure that you
        /// aren't bypassing critical logic, error checking, or code safety by directly
        /// accessing field values.
        /// </summary>    
        public FieldInfo fi;

        internal Type typ;
        internal bool fFoundField;
        internal string sFieldName;
        internal bool fSortDescending;
        internal StringComparison stringComparisonToUse = StringComparison.Ordinal;

        #endregion

        #region "Constructors"

        public CompareFields(string sNameOfField, bool fDescendingSort)
        {
            sFieldName = sNameOfField;
            fSortDescending = fDescendingSort;
        }

        public CompareFields(string sNameOfField, bool fDescendingSort, SortType sortTyp)
        {
            sFieldName = sNameOfField;
            fSortDescending = fDescendingSort;
            sortType = sortTyp;
        }

        #endregion

        #region "Manual Overrides"

        /// <summary>
        /// Override the sort type at your own peril.  If any field/property value can't be converted
        /// to the type you specify, an exception will be raised.
        /// </summary>
        /// <param name="sortTyp"></param>
        public void SetOverrideSortType(SortType sortTyp)
        {
            sortType = sortTyp;
        }

        /// <summary>
        /// Default string comparison type is Ordinal.  Using this method, you can specify
        /// other options, such as OrdinalIgnoreCase (for case insensitive comparison), etc
        /// </summary>
        /// <param name="stringComparisonType"></param>
        public void SetStringComparisonType(StringComparison stringComparisonType)
        {
            stringComparisonToUse = stringComparisonType;
        }

        #endregion

        /// <summary>
        /// For speed, a delegate is used when we know the type of value that will be returned
        /// from the GetValue() method
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public delegate int TypeSensitiveCompare(T x, T y);

        public TypeSensitiveCompare DoCompare;

        /// <summary>
        /// Sets up cached FieldInfo and determines the best delegate to use to compare values
        /// retrieved from that field.
        /// </summary>
        public void Initialize()
        {
            if (fFoundField == false)
            {
                fFoundField = true;

                if (fi == null)
                {
                    // You can play around with binding flags if you really want to access nonpublic fields, etc... 
                    // note that there is a significant performance hit on accessing protected and private fields,
                    // since security / permissions are checked every time, from what I can tell.  It's better
                    // just to go through public properties if you're not accessing public fields.
                    // fi = typeof(T).GetField(sFieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    fi = typeof(T).GetField(sFieldName);
                    if (fi == null)
                    {
                        throw new Exception("Field name " + sFieldName +
                                            " not found while trying to compare objects of type " + typeof(T).Name);
                    }
                }
                typ = fi.FieldType;
                if (sortType == SortType.eUsePropertyOrFieldType)
                {
                    sortType = Sorting.GetSortTypeEnumForType(typ);
                    if (typ == typeof(string))
                    {
                        if (stringComparisonToUse == StringComparison.Ordinal) DoCompare = StringCompareOrdinal;
                        else DoCompare = StringCompare;
                    }
                    else if (typ == typeof(int) && !fSortDescending) DoCompare = CompareInt;
                    else if (typ == typeof(int)) DoCompare = CompareIntDesc;
                    else if (typ == typeof(DateTime)) DoCompare = CompareDates;
                    else if (typ == typeof(long)) DoCompare = CompareTypeSensitive<long>;
                    else if (typ == typeof(double)) DoCompare = CompareTypeSensitive<double>;
                    else if (typ == typeof(float)) DoCompare = CompareTypeSensitive<float>;
                    else if (typ == typeof(short)) DoCompare = CompareTypeSensitive<short>;
                    else if (typ == typeof(byte)) DoCompare = CompareTypeSensitive<byte>;
                    else if (typ == typeof(bool)) DoCompare = CompareTypeSensitive<bool>;
                    else if (typ.BaseType == typeof(Enum))
                    {
                        FastEnumLookup = new Dictionary<int, string>(32);
                        if (fSortDescending)
                        {
                            DoCompare = FastCompareEnumsDesc;
                        }
                        else
                        {
                            DoCompare = FastCompareEnumsAsc;
                        }
                    }
                    else DoCompare = CompareUsingToString;
                    // optimize to use the ABOVE path if the property or field type matches
                    // the requested sort type (i.e. below)
                }
                else
                {
                    if (sortType == SortType.eString) DoCompare = CompareUsingToString;
                    else if (sortType == SortType.eByte) DoCompare = CompareUsingToByte;
                    else if (sortType == SortType.eDateTime) DoCompare = CompareUsingToDate;
                    else if (sortType == SortType.eInteger) DoCompare = CompareUsingToInt;
                    else if (sortType == SortType.eLong) DoCompare = CompareUsingToInt64;
                    else if (sortType == SortType.eDoubleOrFloat) DoCompare = CompareUsingToDouble;
                    else DoCompare = CompareUsingToString;
                }
            }
        }

        #region "Compare method - handles retrieval and comparison"

        public int StringCompare(T x, T y)
        {
            int nComp = string.Compare((string) fi.GetValue(x), (string) fi.GetValue(y), stringComparisonToUse);
            return !fSortDescending ? nComp : -nComp;
        }

        public int StringCompareOrdinal(T x, T y)
        {
            int nComp = string.Compare((string) fi.GetValue(x), (string) fi.GetValue(y), StringComparison.Ordinal);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareIntDesc(T x, T y)
        {
            int oX = (int) fi.GetValue(x);
            int oY = (int) fi.GetValue(y);
            return oX < oY ? 1 : (oX == oY ? 0 : -1);
        }

        public int CompareInt(T x, T y)
        {
            int oX = (int) fi.GetValue(x);
            int oY = (int) fi.GetValue(y);
            return oX < oY ? -1 : (oX == oY ? 0 : 1);
        }

        public int CompareUsingToString(T x, T y)
        {
            object oX = fi.GetValue(x);
            object oY = fi.GetValue(y);
            int nComp;
            // handle null appropriately only for string sorting
            if (oX == null)
            {
                nComp = oY != null ? -1 : 0;
            }
            else if (oY == null)
            {
                nComp = 1;
            }
            else
            {
                nComp = string.Compare(oX.ToString(), oY.ToString(), stringComparisonToUse);
            }
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToByte(T x, T y)
        {
            byte oX = Convert.ToByte(fi.GetValue(x));
            byte oY = Convert.ToByte(fi.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToInt(T x, T y)
        {
            int oX = Convert.ToInt32(fi.GetValue(x));
            int oY = Convert.ToInt32(fi.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToInt64(T x, T y)
        {
            Int64 oX = Convert.ToInt64(fi.GetValue(x));
            Int64 oY = Convert.ToInt64(fi.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToDouble(T x, T y)
        {
            double oX = Convert.ToDouble(fi.GetValue(x));
            double oY = Convert.ToDouble(fi.GetValue(y));
            int nComp = oX > oY ? 1 : (oX < oY ? -1 : 0);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareUsingToDate(T x, T y)
        {
            DateTime oX = Convert.ToDateTime(fi.GetValue(x));
            DateTime oY = Convert.ToDateTime(fi.GetValue(y));
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareDates(T x, T y)
        {
            DateTime oX = (DateTime) fi.GetValue(x);
            DateTime oY = (DateTime) fi.GetValue(y);
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        public int CompareTypeSensitive<T2>(T x, T y) where T2 : IComparable<T2>
        {
            T2 oX = (T2) fi.GetValue(x);
            T2 oY = (T2) fi.GetValue(y);
            int nComp = oX.CompareTo(oY);
            return !fSortDescending ? nComp : -nComp;
        }

        /// <summary>
        /// Faster than comparing enums using .ToString() 
        /// (the Enum.ToString() method appears to be fairly expensive)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public int FastCompareEnumsAsc(T x, T y)
        {
            int oX = (int) fi.GetValue(x);
            int oY = (int) fi.GetValue(y);
            string s1, s2;

            if (!FastEnumLookup.TryGetValue(oX, out s1))
            {
                Enum eX = (Enum) fi.GetValue(x);
                s1 = eX.ToString();
                FastEnumLookup.Add(oX, s1);
            }
            if (!FastEnumLookup.TryGetValue(oY, out s2))
            {
                Enum eY = (Enum) fi.GetValue(y);
                s2 = eY.ToString();
                FastEnumLookup.Add(oY, s2);
            }
            return s1.CompareTo(s2);
        }

        public int FastCompareEnumsDesc(T x, T y)
        {
            int oX = (int) fi.GetValue(x);
            int oY = (int) fi.GetValue(y);
            string s1, s2;

            if (!FastEnumLookup.TryGetValue(oX, out s1))
            {
                Enum eX = (Enum) fi.GetValue(x);
                s1 = eX.ToString();
                FastEnumLookup.Add(oX, s1);
            }
            if (!FastEnumLookup.TryGetValue(oY, out s2))
            {
                Enum eY = (Enum) fi.GetValue(y);
                s2 = eY.ToString();
                FastEnumLookup.Add(oY, s2);
            }
            return s2.CompareTo(s1);
        }

        #endregion

        int IComparer<T>.Compare(T x, T y)
        {
            return DoCompare(x, y);
        }
    }
}