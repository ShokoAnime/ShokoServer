using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Models.Client
{
    public class CL_Response<T> : CL_Response
    {
        public T Result { get; set; }

    }
    public class CL_Response
    {
        public string ErrorMessage { get; set; }
    }
}
