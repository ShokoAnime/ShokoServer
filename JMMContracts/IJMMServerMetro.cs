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
		Contract_ServerStatus GetServerStatus();

		[OperationContract]
		Contract_JMMUser AuthenticateUser(string username, string password);

		[OperationContract]
		List<Contract_JMMUser> GetAllUsers();

		[OperationContract]
		List<Contract_AnimeGroup> GetAllGroups(int userID);

		[OperationContract]
		List<MetroContract_Anime_Summary> GetAnimeWithNewEpisodes(int maxRecords, int jmmuserID);

		[OperationContract]
		MetroContract_Anime_Detail GetAnimeDetail(int animeID, int jmmuserID, int maxEpisodeRecords);

		[OperationContract]
		MetroContract_Anime_Summary GetAnimeSummary(int animeID);

		[OperationContract]
		List<MetroContract_AniDB_Character> GetCharactersForAnime(int animeID, int maxRecords);

		[OperationContract]
		List<MetroContract_Shout> GetTraktShoutsForAnime(int animeID, int maxRecords);

		[OperationContract]
		List<MetroContract_Shout> GetAniDBRecommendationsForAnime(int animeID, int maxRecords);

		[OperationContract]
		List<MetroContract_Anime_Summary> GetAnimeContinueWatching(int maxRecords, int jmmuserID);

		[OperationContract]
		List<MetroContract_Anime_Summary> GetSimilarAnimeForAnime(int animeID, int maxRecords, int jmmuserID);
	}
}
