using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.API.Model
{
    public class APIMessage
    {
        public int code { get; set; }
        public string message { get; set; }

        public APIMessage(int _code, string _message)
        {
            code = _code;
            message = _message;
        }
    }
}
