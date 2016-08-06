using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.API.Model
{
    class QueueInfo
    {
        public int count { get; set; }
        public string state { get; set; }
        public bool isrunning { get; set; }
        public bool ispause { get; set; }
    }
}
