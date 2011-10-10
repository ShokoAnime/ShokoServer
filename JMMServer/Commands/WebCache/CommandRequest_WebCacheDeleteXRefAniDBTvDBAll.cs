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
	/// <summary>
	/// Used to delete all cross refs for a certain TvDB series
	/// Only used when the Series ID is no longer valid
	/// </summary>
	public class CommandRequest_WebCacheDeleteXRefAniDBTvDBAll : CommandRequestImplementation, ICommandRequest
	{
		public int TvDBSeriesID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Deleting cross ref for TvDB from web cache: {0}", TvDBSeriesID);
			}
		}

		public CommandRequest_WebCacheDeleteXRefAniDBTvDBAll()
		{
		}

		public CommandRequest_WebCacheDeleteXRefAniDBTvDBAll(int tvDBSeriesID)
		{
			this.TvDBSeriesID = tvDBSeriesID;
			this.CommandType = (int)CommandRequestType.WebCache_DeleteXRefTvDB;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
				XMLService.Delete_CrossRef_AniDB_TvDB_All(TvDBSeriesID);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_WebCacheDeleteXRefAniDBTvDBAll: {0}" + ex.ToString(), ex);
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheDeleteXRefAniDBTvDBAll{0}", TvDBSeriesID);
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
				this.TvDBSeriesID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBTvDBAll", "TvDBSeriesID"));
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
