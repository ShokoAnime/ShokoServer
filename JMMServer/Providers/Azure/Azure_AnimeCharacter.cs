namespace JMMServer.Providers.Azure
{
    public class AnimeCharacter
    {
        // In Summary
        public int CharID { get; set; }
        public string CharType { get; set; }
        public string CharImageURL { get; set; }
        public string CharName { get; set; }

        // In Detail
        public string CharDescription { get; set; }
        public string CharKanjiName { get; set; }
        public int SeiyuuID { get; set; }
        public string SeiyuuName { get; set; }
        public string SeiyuuImageURL { get; set; }
    }
}