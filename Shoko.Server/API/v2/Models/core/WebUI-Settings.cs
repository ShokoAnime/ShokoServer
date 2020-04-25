using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.API.v2.Models.core
{
    public class WebUI_Settings
    {
        public string uiTheme { get; set; }
        public bool uiNotifications { get; set; }
        public string otherUpdateChannel { get; set; }
        public int logDelta { get; set; }
        public string[] actions;

        private List<string> channels = new List<string> {"stable", "unstable"};

        public bool Valid()
        {
            if (string.IsNullOrEmpty(uiTheme)) return false;
            bool validChannel = channels.Any(s => otherUpdateChannel.Contains(s));
            if (validChannel == false) return false;
            if (logDelta < 0) return false;

            return true;
        }
    }
}