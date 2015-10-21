using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.WebCache;
using JMMServer.Entities;
using System.Xml;
using JMMServer.Providers.Azure;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands.WebCache
{
	public class CommandRequest_WebCacheDeleteXRefAniDBMAL : BaseCommandRequest, ICommandRequest
	{
		public int AnimeID { get; set; }
		public int StartEpisodeType { get; set; }
		public int StartEpisodeNumber { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return $"Deleting cross ref for Anidb to MAL from web cache: {AnimeID}";
			}
		}

		public CommandRequest_WebCacheDeleteXRefAniDBMAL()
		{
		}

		public CommandRequest_WebCacheDeleteXRefAniDBMAL(int animeID, int epType, int epNumber)
		{
			this.AnimeID = animeID;
			this.StartEpisodeType = epType;
			this.StartEpisodeNumber = epNumber;
			this.CommandType = CommandRequestType.WebCache_DeleteXRefAniDBMAL;
			this.Priority = DefaultPriority;
            this.Id= $"CommandRequest_WebCacheDeleteXRefAniDBMAL{AnimeID}";
		}

		public override void ProcessCommand()
		{
			
			try
			{
                AzureWebAPI.Delete_CrossRefAniDBMAL(AnimeID, StartEpisodeType, StartEpisodeNumber);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBMAL: {0}" + ex.ToString(), ex);
				return;
			}
		}
	}
}
