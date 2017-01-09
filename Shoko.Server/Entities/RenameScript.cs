using Shoko.Models;

namespace Shoko.Server.Entities
{
    public class RenameScript
    {
        public int RenameScriptID { get; private set; }
        public string ScriptName { get; set; }
        public string Script { get; set; }
        public int IsEnabledOnImport { get; set; }

        public override string ToString()
        {
            return string.Format("RenameScript: {0}", ScriptName);
        }

        public Contract_RenameScript ToContract()
        {
            Contract_RenameScript contract = new Contract_RenameScript();

            contract.RenameScriptID = RenameScriptID;
            contract.ScriptName = ScriptName;
            contract.Script = Script;
            contract.IsEnabledOnImport = IsEnabledOnImport;

            return contract;
        }
    }
}