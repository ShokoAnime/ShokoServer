using System.Collections.Generic;

namespace Shoko.Server.API.v2.Models.core
{
    class Setting
    {
        public string setting { get; set; }
        public string value { get; set; }
    }

    class Settings
    {
        public List<Setting> settings { get; set; }
    }
}
