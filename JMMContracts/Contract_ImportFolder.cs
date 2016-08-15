namespace JMMContracts
{
    public class Contract_ImportFolder
    {
        public int? ImportFolderID { get; set; }
        public int ImportFolderType { get; set; }
        public string ImportFolderName { get; set; }
        public string ImportFolderLocation { get; set; }
        public int IsDropSource { get; set; }
        public int IsDropDestination { get; set; }
        public int IsWatched { get; set; }
        public int? CloudID { get; set; }
        public byte[] Icon { get; set; }
    }
}