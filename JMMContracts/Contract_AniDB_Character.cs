namespace Shoko.Models
{
    public class Contract_AniDB_Character
    {
        public int AniDB_CharacterID { get; set; }
        public int CharID { get; set; }
        public string PicName { get; set; }
        public string CreatorListRaw { get; set; }
        public string CharName { get; set; }
        public string CharKanjiName { get; set; }
        public string CharDescription { get; set; }

        // from AniDB_Anime_Character
        public string CharType { get; set; }

        public Contract_AniDB_Seiyuu Seiyuu { get; set; }
        public Contract_AniDBAnime Anime { get; set; }
    }
}