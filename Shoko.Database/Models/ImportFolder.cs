using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Database.Models
{
    public class ImportFolder
    {
        [Key] public int ImportFolderID { get; set; }
        [Column("ImportFolderType")] public int Type { get; set; }
        [Column("ImportFolderName"), MaxLength(500)] public string Name { get; set; }
        [Column("ImportFolderLocation")] public string Location { get; set; }
        public bool IsDropSource { get; set; }
        public bool IsDropDestination { get; set; }
        public bool IsWatched { get; set; }
        [ForeignKey(nameof(CloudAccount))] public int? CloudID { get; set; } //can be null.

        public virtual CloudAccount CloudAccount { get; set; }
    }
}