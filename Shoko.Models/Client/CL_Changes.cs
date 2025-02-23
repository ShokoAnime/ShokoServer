using System;
using System.Collections.Generic;

namespace Shoko.Models.Client
{
    public class CL_Changes<T>
    {
        public List<T> ChangedItems { get; set; } = new List<T>();
        public List<int> RemovedItems { get; set; } = new List<int>();
        public DateTime LastChange { get; set; }
    }
}
