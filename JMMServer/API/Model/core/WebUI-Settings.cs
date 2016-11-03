using System;
using System.Collections.Generic;
using System.Linq;

namespace JMMServer.API.Model.core
{
    public class WebUI_Settings
    {
        public string uiTheme { get; set; }
        public bool uiNotifications { get; set; }
        public string otherUpdateChannel { get; set; }
        public int logDelta { get; set; }

        private List<string> channels = new List<string> { "stable", "unstable" };

        public bool Valid()
        {
            if (String.IsNullOrEmpty(uiTheme)) return false;
            bool validChannel = channels.Any(s => otherUpdateChannel.Contains(s));
            if (validChannel == false) return false;
            if (logDelta < 0) { return false; }

            return true;
        }
    }
}
