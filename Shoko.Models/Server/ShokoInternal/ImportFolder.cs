namespace Shoko.Models.Server
{
    public class ImportFolder
    {
        public int ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }
        public string ImportFolderName { get; set; }
        public string ImportFolderLocation { get; set; }
        public int IsWatched { get; set; }
        public int IsDropSource { get; set; }
        public int IsDropDestination { get; set; }
       
    }
}
