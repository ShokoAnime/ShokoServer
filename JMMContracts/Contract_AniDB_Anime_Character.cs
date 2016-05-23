namespace JMMContracts
{
	public class Contract_AniDB_Anime_Character
	{
		public int AniDB_Anime_CharacterID { get; set; }
		public int AnimeID { get; set; }
		public int CharID { get; set; }
		public string CharType { get; set; }
		public string EpisodeListRaw { get; set; }
	}
}
