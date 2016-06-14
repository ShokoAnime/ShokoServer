using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts
{
    public class Contract_CrossRef_CustomTag
    {
        public int? CrossRef_CustomTagID { get;  set; }
        public int CustomTagID { get; set; }
        public int CrossRefID { get; set; }
        public int CrossRefType { get; set; }
    }
}
