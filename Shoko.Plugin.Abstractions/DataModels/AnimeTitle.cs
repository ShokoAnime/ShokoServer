namespace Shoko.Plugin.Abstractions.DataModels
{
    public class AnimeTitle
    {
        public string Language { get; set; }
        public string Title { get; set; }
        public TitleType Type { get; set; }
    }

    public enum TitleType
    {
        None = 0,
        Main = 1,
        Official = 2,
        Short = 3,
        Synonym = 4
    }
}