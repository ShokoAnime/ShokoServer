using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

// Source code by Owen Emlen (owene_1998@yahoo.com, owen@binarynorthwest.com)

namespace BinaryNorthwest
{
    /// <summary>
    /// Stores a property or field name that will be used to determine sort order.
    /// Also contains a flag, fSortDescending, for specifying descending sort order.
    /// </summary>
    public class SortPropOrFieldAndDirection
    {
        #region "Constructors"

        public SortPropOrFieldAndDirection()
        {
        }

        public SortPropOrFieldAndDirection(string sPropOrFieldNameToSort)
        {
            sPropertyOrFieldName = sPropOrFieldNameToSort;
        }

        public SortPropOrFieldAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort)
        {
            sPropertyOrFieldName = sPropOrFieldNameToSort;
            fSortDescending = fDescendingSort;
        }

        public SortPropOrFieldAndDirection(string sPropOrFieldNameToSort, SortType sortTyp)
        {
            sPropertyOrFieldName = sPropOrFieldNameToSort;
            sortType = sortTyp;
        }

        public SortPropOrFieldAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort, SortType sortTyp)
        {
            sPropertyOrFieldName = sPropOrFieldNameToSort;
            fSortDescending = fDescendingSort;
            sortType = sortTyp;
        }

        public SortPropOrFieldAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort, SortType sortTyp,
            StringComparison stringComp)
        {
            sPropertyOrFieldName = sPropOrFieldNameToSort;
            fSortDescending = fDescendingSort;
            sortType = sortTyp;
            stringComparison = stringComp;
        }

        #endregion

        /// <summary>
        /// Retrieves a IComparer of type T, depending on whether the instance of this
        /// class (or a derived class) references a property or field
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IComparer<T> GetComparer<T>() where T : class
        {
            if (NameIsPropertyName)
            {
                CompareProperties<T> comp = new CompareProperties<T>(sPropertyOrFieldName, fSortDescending, sortType);
                comp.pi = pi;
                comp.Initialize();
                comp.SetStringComparisonType(stringComparison);
                return comp;
            }
            else
            {
                CompareFields<T> comp = new CompareFields<T>(sPropertyOrFieldName, fSortDescending, sortType);
                comp.fi = fi;
                comp.Initialize();
                comp.SetStringComparisonType(stringComparison);
                return comp;
            }
        }

        #region "Fields and Properties"

        /// <summary>
        /// This virtual property is overridden in the SortPropertyAndDirection and SortFieldAndDirection
        /// (derived) classes.  It indicates whether the "sPropertyOrFieldName" field refers to a 
        /// property name or a field name.
        /// </summary>
        internal virtual bool NameIsPropertyName
        {
            get { return true; }
        }

        public string sPropertyOrFieldName;
        public bool fSortDescending;
        public SortType sortType = SortType.eUsePropertyOrFieldType;
        public StringComparison stringComparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// (Cached PropertyInfo or FieldInfo)
        /// These fields are made available to avoid repeat reflection (GetProperty/GetField) if the caller has 
        /// already obtained the PropertyInfo or FieldInfo class instances for the property/field
        /// </summary>
        public PropertyInfo pi;


        public PropertyDescriptor property;

        public FieldInfo fi;

        #endregion
    }

    /// <summary>
    /// A SortPropOrFieldAndDirection-derived class, SortPropertyAndDirection handles PropertyInfo caching and asc/desc logic
    /// </summary>
    public class SortPropertyAndDirection : SortPropOrFieldAndDirection
    {
        internal override bool NameIsPropertyName
        {
            get { return true; }
        }

        public SortPropertyAndDirection() : base()
        {
        }

        public SortPropertyAndDirection(string sPropOrFieldNameToSort) : base(sPropOrFieldNameToSort)
        {
        }

        public SortPropertyAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort)
            : base(sPropOrFieldNameToSort, fDescendingSort)
        {
        }

        public SortPropertyAndDirection(string sPropOrFieldNameToSort, SortType sortTyp)
            : base(sPropOrFieldNameToSort, sortTyp)
        {
        }

        public SortPropertyAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort, SortType sortTyp)
            : base(sPropOrFieldNameToSort, fDescendingSort, sortTyp)
        {
        }
    }

    /// <summary>
    /// A SortPropOrFieldAndDirection-derived class, SortFieldAndDirection handles FieldInfo caching and asc/desc logic
    /// </summary>
    public class SortFieldAndDirection : SortPropOrFieldAndDirection
    {
        internal override bool NameIsPropertyName
        {
            get { return false; }
        }

        public SortFieldAndDirection() : base()
        {
        }

        public SortFieldAndDirection(string sPropOrFieldNameToSort) : base(sPropOrFieldNameToSort)
        {
        }

        public SortFieldAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort)
            : base(sPropOrFieldNameToSort, fDescendingSort)
        {
        }

        public SortFieldAndDirection(string sPropOrFieldNameToSort, SortType sortTyp)
            : base(sPropOrFieldNameToSort, sortTyp)
        {
        }

        public SortFieldAndDirection(string sPropOrFieldNameToSort, bool fDescendingSort, SortType sortTyp)
            : base(sPropOrFieldNameToSort, fDescendingSort, sortTyp)
        {
        }
    }
}