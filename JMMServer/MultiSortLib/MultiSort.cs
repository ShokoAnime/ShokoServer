using System;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

// Source code by Owen Emlen (owene_1998@yahoo.com, owen@binarynorthwest.com)
// http://www.braintechllc.com/owen.aspx, http://www.binarynorthwest.com

namespace BinaryNorthwest
{
    /// <summary>
    /// Determines how a property or field value is treated for comparison. For instance,
    /// if you compare the strings "9" and "10", "9" will be placed AFTER "10".  However,
    /// if you specify Integer sorting, "9" and "10" will be converted to numeric values
    /// prior to comparison, resulting in "9" being placed BEFORE "10" (usually a good thing)
    /// </summary>
    public enum SortType
    {
        eUsePropertyOrFieldType = 0,
        eString = 1,
        eDoubleOrFloat = 2,
        eDateTime = 3,
        eByte = 4,
        eInteger = 5,
        eLong = 6
    }

    /// <summary>
    /// The sorting methods contained in the static class "Sorting" can be used to sort lists of classes 
    /// (of any class type) using the value of properties and fields contained within the class.  
    /// NOTE: Only property and field types are supported at the moment.
    /// </summary>
    public static class Sorting
    {
        
        /// <summary>
        /// Sorts a given list, in-place, using the specified sortBy (comparison) criterion.  
        /// This static method is thread-safe -- but only if you remember to lock the list elsewhere 
        /// if/when you use or modify the same instance of the list in another thread).  
        /// Those who don't need to worry about threading issues can remove the lock() statement.
        /// </summary>
        /// <typeparam name="T">The business object type to be sorted</typeparam>
        /// <param name="ListToBeSorted">The list to be sorted, in place</param>
        /// <param name="sortBy">Criteria describing the field or property name used in sorting the list 
        /// (and the direction of the sort)
        /// </param>
        public static void SortInPlace<T>(List<T> ListToBeSorted, SortPropOrFieldAndDirection sortBy) where T : class
        {
            try
            {
                // Retrieve an IComparer that contains logic for sorting this specific business object
                // type by the specified criteria
                IComparer<T> compare = sortBy.GetComparer<T>();
                lock (ListToBeSorted) { ListToBeSorted.Sort(compare); }
            }
            catch (Exception ex)
            {
                throw new Exception("Error trying to sort list of " + typeof(T).Name + " using " +
                  (sortBy.NameIsPropertyName ? "property " : "field ") + sortBy.sPropertyOrFieldName, ex);
            }
        }

        /// <summary>
        /// Sorts a given list using multiple sort (comparison) criteria, similar to a SQL "ORDER BY" clause
        /// Example: Specifying the sort/comparison criteria (1) "State" and (2) "Zipcode" using a list of 
        /// address entries will result in a sorted list containing address entries FIRST sorted by state
        /// (starting with the A* states - Alabama, Alaska, etc).  Then, the address entries 
        /// associated with each respective state will be further sorted according to Zipcode.
        /// </summary>
        /// <typeparam name="T">The type of elements in the list to sort</typeparam>
        /// <param name="ListToBeSorted">The original list.  A new, sorted list will be returned.</param>
        /// <param name="rgSortBy">A list of ordered criteria specifying how the list should be sorted.</param>
        /// <returns>A new list containing all elements of ListToBeSorted, sorted by the criteria 
        /// specified in rgSortBy.
        /// </returns>
        public static List<T> MultiSort<T>(List<T> ListToBeSorted, List<SortPropOrFieldAndDirection> rgSortBy) where T : class
        {
            List<T> results;
            // For thread safety, make a copy of the list up front.  Note that you still need to
            // lock the same instance of the list when/if it is modified by other threads.
            lock (ListToBeSorted) { results = new List<T>(ListToBeSorted); }

            if (rgSortBy.Count == 1)
            {
                // if only sorting a single time, just call the basic in-place sorting on our copied "results" list
                SortInPlace<T>(results, rgSortBy[0]);
                return results;
            }

            try
            {
                List<List<T>> rgCopies = new List<List<T>>(1);
                rgCopies.Add(results);
                int sortByCount = rgSortBy.Count;

                // For each criterion in the list of comparison criteria, one or more lists must be sorted. 
                // Each time a list is sorted, one or more sublists may be created.  Each sublist contains
                // items that were deemed to be "equivalent" according to the comparison criterion.
                // Example: After sorting addresses entries by state you may have multiple sublists, 
                // each containing all of the address entries associated with a given state.
                // Note: this is not the most efficient method (especially in terms of memory!), but it
                // is sufficient in most scenarios and is easier to understand than many other 
                // methods of sorting a list using multiple criteria.
                for (int i = 0; i < sortByCount; i++)
                {
                    SortPropOrFieldAndDirection sortBy = rgSortBy[i];
                    if (string.IsNullOrEmpty(sortBy.sPropertyOrFieldName)) throw new Exception(
                        "MultiSort parameter rgSortBy was passed an empty field name in rgSortBy[" + i.ToString() + "]"
                        );

                    // Retrieve an IComparer that contains logic for sorting this specific business object
                    // type by the specified criteria
                    IComparer<T> compare = sortBy.GetComparer<T>();

                    // Sort each sublist using the created IComparer<T>
                    foreach (List<T> lst in rgCopies) { lst.Sort(compare); }

                    if (i < sortByCount - 1)
                    {
                        // Create new sublists by searching for the sorted-by value boundaries/changes
                        // Our "best guess" (i.e. shot in the dark) is that we will create at least 8 sublists 
                        // from the original list.  NOT terribly efficient, but often sufficient.
                        // Some advanced methods involve tracking duplicate values DURING the sort iteself
                        List<List<T>> rgNewCopies = new List<List<T>>(rgCopies.Count * 8);

                        for (int n = 0; n < rgCopies.Count; n++)
                        {
                            List<T> rgList = rgCopies[n];
                            // Be conservative and set the initial sublist capacity to a small number, but
                            // still honor the original list's item count.  (Example: If you are sorting a list
                            // of "Address information" by Zipcode and the list has 32,000 entries, then initialize
                            // each sublist (each of which store all Address information entries with the same Zipcode)
                            // with a capacity of 1000.   32,000 / 32 = 1000
                            List<T> rgSublist = new List<T>(rgList.Count / 32);

                            // Compare items to the item that preceeded it to determine where the "value boundaries" 
                            // are located.  If you will be sorting frequently and do not have cpu cycles to burn :),
                            // a smarter boundary-finding algorithm should be used.  (e.g. determine boundary locations
                            // when comparing elements during the sort routine).  
                            // Another alternative is to take advantage of the fact that the list is sorted and to
                            // use a O(LogN) binary search rather than the (currently) linear O(N) search.
                            for (int j = 0; j < rgList.Count; j++)
                            {
                                T item = rgList[j];
                                if (j > 0)
                                {
                                    // Compare the item to the preceeding item using the same comparison criterion
                                    // used during the sort
                                    T itemprev = rgList[j - 1];

                                    if (compare.Compare(item, itemprev) == 0)
                                    {
                                        // The item had the same property or field value as the preceeding item.  
                                        // Add it on to the same sublist.
                                        rgSublist.Add(item);
                                    }
                                    else
                                    {
                                        // The item did NOT have the same property or field value as the preceeding item.
                                        // "Close up" the previous sublist and start a new one.
                                        rgNewCopies.Add(rgSublist);
                                        rgSublist = new List<T>(rgList.Count / 32);
                                        rgSublist.Add(item);
                                    }
                                }
                                else
                                {
                                    // The first item has no predecessor - just add the item to the first sublist
                                    rgSublist.Add(item);
                                }

                            } // END: for (int j = 0; j < rgList.Count; j++) ... each item in a sublist

                            // Add the last created sublist to our "master list of sublists" :P
                            // It may be that this list has 0 elements in some cases, but this is not a problem
                            rgNewCopies.Add(rgSublist);

                        } // END: for (int n = 0; n < rgCopies.Count; n++) ... each sublist in rgCopies

                        // Move to the next "level" of sublists in preparation for further sorting using the next
                        // sort/comparison criterion
                        rgCopies = rgNewCopies;
                    }

                } // END: for (int i = 0; i < sortByCount; i++) ... each sort by criteria: 


                // reconstruct all resorted sub-sub-sub-sub-sublists into a single, final (flat) results list
                results.Clear();
                foreach (List<T> rgList in rgCopies) { results.AddRange(rgList); }

                return results;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception in MultiSort while sorting a list of " + typeof(T).Name, ex);
            }
        }

        /// <summary>
        /// Converts a System.Type into a type comparison enumeration value that is used
        /// to quickly convert values to a suitable type for comparison.
        /// NOTE: unsigned short/int/long are not yet implemented
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static SortType GetSortTypeEnumForType(Type t)
        {
            if (t == typeof(string)) return SortType.eString;
            else if (t == typeof(DateTime)) return SortType.eDateTime;
            else if (t == typeof(int) || t == typeof(short)) return SortType.eInteger;
            else if (t == typeof(long)) return SortType.eLong;
            else if (t == typeof(bool)) return SortType.eByte;
            else if (t == typeof(double) || t == typeof(float)) return SortType.eDoubleOrFloat;
            else return SortType.eString;
        }
    }    
}