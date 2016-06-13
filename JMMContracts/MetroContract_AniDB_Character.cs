namespace JMMContracts
{
    public class MetroContract_AniDB_Character
    {
        public int AniDB_CharacterID { get; set; }
        public int CharID { get; set; }
        public string CharName { get; set; }
        public string CharKanjiName { get; set; }
        public string CharDescription { get; set; }

        public int ImageType { get; set; }
        public int ImageID { get; set; }

        // from AniDB_Anime_Character
        public string CharType { get; set; }

        // Seiyuu
        public int SeiyuuImageType { get; set; }
        public int SeiyuuImageID { get; set; }
        public int SeiyuuID { get; set; }
        public string SeiyuuName { get; set; }
    }
}