using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.IO;
using System.ServiceModel.Web;

namespace JMMContracts
{
	[ServiceContract]
	public interface IJMMServerMetro
	{
		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroups(int userID);

		[OperationContract]
		List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID);

		[OperationContract]
		MetroContract_Anime_Detail GetAnimeDetail(int animeID);

		[OperationContract]
		List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID);
	}
}
