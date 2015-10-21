using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Xml;
using System.IO;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServer.Providers.Azure;
using JMMServer.Commands.Azure;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetAniDBTitles : BaseCommandRequest, ICommandRequest
	{
		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority10; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting AniDB Titles");
			}
		}

		public CommandRequest_GetAniDBTitles()
		{
			this.CommandType = CommandRequestType.AniDB_GetTitles;
			this.Priority = DefaultPriority;
            this.JMMUserId = Store.JmmUserRepo.GetMasterUser().Id;
            this.Id = $"CommandRequest_GetAniDBTitles_{DateTime.Now}";
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetAniDBTitles");

			
			try
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (user == null)
			        return;
			    AniDBAuthorization auth=user.GetAniDBAuthorization();

				bool process = (auth.UserName.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    auth.UserName.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase));

				if (!process) return;

				string url = Constants.AniDBTitlesURL;
				logger.Trace("Get AniDB Titles: {0}", url);

				Stream s = Utils.DownloadWebBinary(url);
				int bytes = 2048;
				byte[] data = new byte[2048];
				StringBuilder b = new StringBuilder();
				UTF8Encoding enc = new UTF8Encoding();

				ICSharpCode.SharpZipLib.GZip.GZipInputStream zis = new ICSharpCode.SharpZipLib.GZip.GZipInputStream(s);

				while ((bytes = zis.Read(data, 0, data.Length)) > 0)
					b.Append(enc.GetString(data, 0, bytes));

				zis.Close();

				AniDB_Anime_TitleRepository repTitles = new AniDB_Anime_TitleRepository();

				string[] lines = b.ToString().Split('\n');
				Dictionary<int, AnimeIDTitle> titles = new Dictionary<int, AnimeIDTitle>();
				foreach (string line in lines)
				{
					if (line.Trim().Length == 0 || line.Trim().Substring(0, 1) == "#") continue;

					string[] fields = line.Split('|');

					int animeID = 0;
					int.TryParse(fields[0], out animeID);
					if (animeID == 0) continue;

					string titleType = fields[1].Trim().ToLower();
					//string language = fields[2].Trim().ToLower();
					string titleValue = fields[3].Trim();



					AnimeIDTitle thisTitle = null;
					if (titles.ContainsKey(animeID))
					{
						thisTitle = titles[animeID];
					}
					else
					{
						thisTitle = new AnimeIDTitle();
						thisTitle.AnimeIDTitleId = 0;
						thisTitle.MainTitle = titleValue;
						thisTitle.AnimeID = animeID;
						titles[animeID] = thisTitle;
					}

					if (!string.IsNullOrEmpty(thisTitle.Titles))
						thisTitle.Titles += "|";

					if (titleType.Equals("1"))
						thisTitle.MainTitle = titleValue;

					thisTitle.Titles += titleValue;
				}

				foreach (AnimeIDTitle aniTitle in titles.Values)
				{
					//AzureWebAPI.Send_AnimeTitle(aniTitle);
					CommandRequest_Azure_SendAnimeTitle cmdAzure = new CommandRequest_Azure_SendAnimeTitle(aniTitle.AnimeID, aniTitle.MainTitle, aniTitle.Titles);
					cmdAzure.Save();
				}
				
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetAniDBTitles: {0}", ex.ToString());
				return;
			}
		}
	}
}
