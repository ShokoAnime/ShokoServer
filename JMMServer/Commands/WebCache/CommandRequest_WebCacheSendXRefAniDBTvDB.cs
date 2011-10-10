using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Repositories;
using JMMServer.Entities;
using JMMServer.WebCache;
using System.Xml;
using JMMServer.Providers.TvDB;

namespace JMMServer.Commands
{
	public class CommandRequest_WebCacheSendXRefAniDBTvDB : CommandRequestImplementation, ICommandRequest
	{
		public int CrossRef_AniDB_TvDBID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority9; }
		}

		public string PrettyDescription
		{
			get
			{
				return string.Format("Sending cross ref for Anidb to TvDB from web cache: {0}", CrossRef_AniDB_TvDBID);
			}
		}

		public CommandRequest_WebCacheSendXRefAniDBTvDB()
		{
		}

		public CommandRequest_WebCacheSendXRefAniDBTvDB(int xrefID)
		{
			this.CrossRef_AniDB_TvDBID = xrefID;
			this.CommandType = (int)CommandRequestType.WebCache_SendXRefAniDBTvDB;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
			
			try
			{
				CrossRef_AniDB_TvDBRepository repCrossRef = new CrossRef_AniDB_TvDBRepository();
				CrossRef_AniDB_TvDB xref = repCrossRef.GetByID(CrossRef_AniDB_TvDBID);
				if (xref == null) return;

				TvDB_SeriesRepository repSeries = new TvDB_SeriesRepository();
				TvDB_Series tvSeries = repSeries.GetByTvDBID(xref.TvDBID);
				if (tvSeries == null)
					tvSeries = TvDBHelper.GetSeriesInfoOnline(xref.TvDBID);

				string seriesName = "";
				if (tvSeries != null) seriesName = tvSeries.SeriesName;

				XMLService.Send_CrossRef_AniDB_TvDB(xref, seriesName);
			}
			catch (Exception ex)
			{
				logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBTvDB: {0}" + ex.ToString(), ex);
				return;
			}
		}

		public override void GenerateCommandID()
		{
			this.CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBTvDB{0}", CrossRef_AniDB_TvDBID);
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
				this.CrossRef_AniDB_TvDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTvDB", "CrossRef_AniDB_TvDBID"));
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
