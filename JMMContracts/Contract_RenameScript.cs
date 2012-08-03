using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMContracts
{
	public class Contract_RenameScript
	{
		public int? RenameScriptID { get; set; }
		public string ScriptName { get; set; }
		public string Script { get; set; }
		public int IsEnabledOnImport { get; set; }
	}
}
