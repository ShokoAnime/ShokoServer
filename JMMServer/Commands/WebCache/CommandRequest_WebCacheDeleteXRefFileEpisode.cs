using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.WebCache;
using System.Xml;

namespace JMMServer.Commands
{
	public class CommandRequest_WebCacheDeleteXRefFileEpisode : CommandRequestImplementation, ICommandRequest
	{
		public string Hash { get; set; }
		public int EpisodeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Deleting cross ref for file to episode to web cache: {0}-{1}", Hash, EpisodeID);
			}
		}

		public CommandRequest_WebCacheDeleteXRefFileEpisode()
		{
		}

		public CommandRequest_WebCacheDeleteXRefFileEpisode(string hash, int aniDBEpisodeID)
		{
			this.Hash = hash;
			this.EpisodeID = aniDBEpisodeID;
			this.CommandType = (int)CommandRequestType.WebCache_DeleteXRefFileEpisode;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
                JMMServer.Providers.Azure.AzureWebAPI.Delete_CrossRefFileEpisode(Hash);
			}
			catch (Exception ex)
			{
				logger.Error("Error processing CommandRequest_WebCacheDeleteXRefFileEpisode: {0}-{1} - {2}", Hash, EpisodeID, ex.ToString());
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheDeleteXRefFileEpisode-{0}-{1}", Hash, EpisodeID);
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
				this.Hash = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "Hash");
				this.EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefFileEpisode", "EpisodeID"));
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
