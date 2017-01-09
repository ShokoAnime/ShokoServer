using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models
{
    public class Contract_CloudAccount
    {
        public int? CloudID { get; set; }
        public string Provider { get; set; }
        public string Name { get; set; }
        public byte[] Icon { get; set; }
    }
}
