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
	public class CommandRequest_WebCacheDeleteXRefAniDBTvDB : CommandRequestImplementation, ICommandRequest
	{
		public int AnimeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Deleting cross ref for Anidb to TvDB from web cache: {0}", AnimeID);
			}
		}

		public CommandRequest_WebCacheDeleteXRefAniDBTvDB()
		{
		}

		public CommandRequest_WebCacheDeleteXRefAniDBTvDB(int animeID)
		{
			this.AnimeID = animeID;
			this.CommandType = (int)CommandRequestType.WebCache_DeleteXRefAniDBTvDB;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
				XMLService.Delete_CrossRef_AniDB_TvDB(AnimeID);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDB: {0}" + ex.ToString(), ex);
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBTvDB{0}", AnimeID);
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
				this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDB", "AnimeID"));
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
