using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class RenameScriptImpl : IRenameScript
    {
        public string Name { get; set; }
        public string Script { get; set; }

        public string Type { get; set; }

        public string ExtraData { get; set; }
    }
}
