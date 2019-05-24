using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Shoko.Database.Models
{
    public class RenameScript
    {
        [Key, Column("RenameScriptID")] public int Id { get; set; }
        [Required] public string ScriptName { get; set; }
        [Required, MaxLength(255), Column("RenamerType")] public string Type { get; set; }
        public string Script { get; set; }
        public string ExtraData { get; set; }

        public bool IsEnabledOnImport { get; set; }
    }
}
