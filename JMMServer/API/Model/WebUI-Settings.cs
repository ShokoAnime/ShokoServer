using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMMServer.API.Model
{
    public class WebUI_Settings
    {
        public string theme { get; set; }
        public int log_days { get; set; }

        public bool Valid()
        {
            if (String.IsNullOrEmpty(theme)) return false;
            if (log_days < 0) return false;

            return true;
        }
    }
}
