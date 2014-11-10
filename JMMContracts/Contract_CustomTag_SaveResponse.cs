using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts
{
    public class Contract_CustomTag_SaveResponse
    {
        public string ErrorMessage { get; set; }
        public Contract_CustomTag CustomTag { get; set; }
    }
}
