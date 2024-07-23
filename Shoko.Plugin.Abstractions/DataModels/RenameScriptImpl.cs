using System;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Plugin.Abstractions.DataModels
{
    public class RenameScriptImpl : IRenameScript
    {
        public string Script { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string ExtraData { get; set; } = string.Empty;
    }
}
