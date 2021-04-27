using System.Collections.Generic;

namespace Shoko.Server.Renamer
{
    public class LegacyRenamerSettings
    {
        public List<LegacyScript> Scripts { get; set; }

        public class LegacyScript
        {
            public string ScriptName { get; set; }
            public string Script { get; set; }
            public bool Active { get; set; }
        }
    }
}
