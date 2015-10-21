using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using System.Xml;
using System.IO;
using JMMDatabase;
using JMMDatabase.Extensions;
using JMMModels.Childs;
using JMMServerModels.DB.Childs;
using AniDB_Anime = JMMServer.Entities.AniDB_Anime;


namespace JMMServer.Commands.Azure
{
	public class CommandRequest_Azure_SendAnimeXML : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority10; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Sending anime xml to azure: {AnimeID}";
			}
		}

		public CommandRequest_Azure_SendAnimeXML()
		{
		}

		public CommandRequest_Azure_SendAnimeXML(int animeID)
		{
			this.AnimeID = animeID;
			this.CommandType = CommandRequestType.Azure_SendAnimeXML;
			this.Priority = DefaultPriority;
            this.JMMUserId= Store.JmmUserRepo.GetMasterUser().Id;
            this.Id= $"CommandRequest_Azure_SendAnimeXML_{this.AnimeID}";
		}

		public override void ProcessCommand()
		{
			
			try
			{
			    JMMModels.JMMUser user = Store.JmmUserRepo.Find(JMMUserId).GetUserWithAuth(AuthorizationProvider.AniDB);
			    if (user == null)
			        return;
                AniDBAuthorization auth = user.GetAniDBAuthorization();
                bool process = (auth.UserName.Equals("jonbaby", StringComparison.InvariantCultureIgnoreCase) ||
                    auth.UserName.Equals("jmediamanager", StringComparison.InvariantCultureIgnoreCase));

				if (!process) return;

				AniDB_AnimeRepository rep = new AniDB_AnimeRepository();
				AniDB_Anime anime = rep.GetByAnimeID(AnimeID);
				if (anime == null) return;

				string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
				string filePath = Path.Combine(appPath, "Anime_HTTP");

				if (!Directory.Exists(filePath))
					Directory.CreateDirectory(filePath);

				string fileName = $"AnimeDoc_{AnimeID}.xml";
				string fileNameWithPath = Path.Combine(filePath, fileName);

				string rawXML = "";
				if (File.Exists(fileNameWithPath))
				{
					StreamReader re = File.OpenText(fileNameWithPath);
					rawXML = re.ReadToEnd();
					re.Close();
				}

				AnimeXML xml = new AnimeXML();
				xml.AnimeID = AnimeID;
				xml.AnimeName = anime.MainTitle;
				xml.DateDownloaded = 0;
				xml.Username = ServerSettings.AniDB_Username;
				xml.XMLContent = rawXML;

				AzureWebAPI.Send_AnimeXML(xml);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_Azure_SendAnimeXML: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}
	}
}
