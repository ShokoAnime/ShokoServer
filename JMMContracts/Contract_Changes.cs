using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts
{
    public class Contract_Changes<T>
    {
        public List<T> ChangedItems { get; set; } = new List<T>();
        public List<int> RemovedItems { get; set; } = new List<int>();
        public DateTime LastChange { get; set; }
    }
}
