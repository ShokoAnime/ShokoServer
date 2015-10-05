namespace JMMModels.Childs
{
    public class Trakt_Image : ImageInfo
    {
        public int? Season { get; set; }
        public string ImageUrl { get; set; }
        public bool IsMovie { get; set; }
    }
}
