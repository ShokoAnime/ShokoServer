using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Common
{
    /// <summary>
    /// A list with the total count of <typeparamref name="T"/> entries that
    /// match the filter and a sliced or the full list of <typeparamref name="T"/>
    /// entries.
    /// </summary>
    public class ListResult<T>
    {
        /// <summary>
        /// Create a new uninitialised list result.
        /// /// </summary>
        public ListResult()
        {
            Total = 0;
            List = new List<T>();
        }
        
        /// <summary>
        /// Create a new fully initialised list result.
        /// </summary>
        /// <param name="total">Total count</param>
        /// <param name="list">List of <typeparamref name="T"/> entries.</param>
        public ListResult(int total, List<T> list)
        {
            Total = total;
            List = list;
        }

        /// <summary>
        /// Total number of <typeparamref name="T"/> entries that matched the
        /// applied filter.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// A sliced page or the whole list of <typeparamref name="T"/> entries.
        /// </summary>
        public IReadOnlyList<T> List { get; set; }
    }
}
