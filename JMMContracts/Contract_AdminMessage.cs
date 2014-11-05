using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMContracts
{
    public class Contract_AdminMessage
    {
        public int AdminMessageId { get; set; }
        public long MessageDate { get; set; }
        public int MessageType { get; set; }
        public string Message { get; set; }
        public string MessageURL { get; set; }
    }
}
