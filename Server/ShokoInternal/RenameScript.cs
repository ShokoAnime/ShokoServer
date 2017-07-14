using Microsoft.SqlServer.Server;

namespace Shoko.Models.Server
{
    public class RenameScript
    {
        public RenameScript()
        {
        }
        public int RenameScriptID { get; set; }
        public string ScriptName { get; set; }
        public string Script { get; set; }
        public int IsEnabledOnImport { get; set; }
        public string RenamerType { get; set; }
        public string ExtraData { get; set; }

    }
}