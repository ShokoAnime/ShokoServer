using Shoko.Models;

namespace Shoko.Models.Server
{
    public class RenameScript
    {
        public RenameScript()
        {
        }
        public int RenameScriptID { get; private set; }
        public string ScriptName { get; set; }
        public string Script { get; set; }
        public int IsEnabledOnImport { get; set; }


    }
}