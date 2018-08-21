#if false //this isn't used
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
	[Serializable]
	public class CommandRequest_GetCharactersCreators : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }
		public bool ForceRefresh { get; set; }

		public CommandRequestPriority DefaultPriority 
		{
			get { return CommandRequestPriority.Priority8; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Getting character and creator info from UDP API for Anime: {0}", AnimeID);
			}
		}

		public CommandRequest_GetCharactersCreators()
		{
		}

		public CommandRequest_GetCharactersCreators(int animeid, bool forced)
		{
			this.AnimeID = animeid;
			this.ForceRefresh = forced;
			this.CommandType = (int)CommandRequestType.AniDB_GetCharsCreators;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			logger.Info("Processing CommandRequest_GetCharactersCreators: {0}", AnimeID);

			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				AniDB_Character_CreatorRepository repCharCreators = new AniDB_Character_CreatorRepository();
				AniDB_Anime anime = null;

				if (ForceRefresh)
				{
					// redownload anime details from http ap so we can get an update character list
					anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(AnimeID, false, false);
				}
				else
					anime = repAnime.GetByAnimeID(AnimeID);

				if (anime == null) return;

				foreach (AniDB_Anime_Character animeChar in anime.AnimeCharacters)
				{
					//MainWindow.anidbProcessor.UpdateCharacterInfo(charref.CharID, false);
					//logger.Trace("Downloading char info: {0}", animeChar.CharID);
					CommandRequest_GetCharacter cmdChar = new CommandRequest_GetCharacter(animeChar.CharID, ForceRefresh);
					cmdChar.Save();

					// for each of the creators for this character
					foreach (AniDB_Character_Seiyuu aac in repCharCreators.GetByCharID(animeChar.CharID))
					{
						CommandRequest_GetCreator cmdCreators = new CommandRequest_GetCreator(aac.SeiyuuID, ForceRefresh);
						cmdCreators.Save();
					}
				}

			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_GetCharactersCreators: {0} - {1}", AnimeID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_GetCharactersCreators_{0}", this.AnimeID);
		}

		public override bool LoadFromDBCommand(CommandRequest cq)
		{
			this.CommandID = cq.CommandID;
			this.CommandRequestID = cq.CommandRequestID;
			this.CommandType = cq.CommandType;
			this.Priority = cq.Priority;
			this.CommandDetails = cq.CommandDetails;
			this.DateTimeUpdated = cq.DateTimeUpdated;

			// read xml to get parameters
			if (this.CommandDetails.Trim().Length > 0)
			{
				XmlDocument docCreator = new XmlDocument();
				docCreator.LoadXml(this.CommandDetails);

				// populate the fields
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetCharactersCreators", "AnimeID"));
				this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetCharactersCreators", "ForceRefresh"));
			}

			return true;
		}

		public override CommandRequest ToDatabaseObject()
		{
			GenerateCommandID();

			CommandRequest cq = new CommandRequest();
			cq.CommandID = this.CommandID;
			cq.CommandType = this.CommandType;
			cq.Priority = this.Priority;
			cq.CommandDetails = this.ToXML();
			cq.DateTimeUpdated = DateTime.Now;

			return cq;
		}
	}
}
#endif