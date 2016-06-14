using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using System.IO;
using JMMServer.Repositories;
using JMMContracts;
using JMMFileHelper;
using AniDBAPI;
using System.Xml;
using JMMServer.WebCache;
using System.Collections.Specialized;
using System.Threading;
using System.Globalization;
using System.Configuration;

namespace JMMServer.Commands.AniDB 
{
    [Serializable]
    public class CommandRequest_GetEpisode : CommandRequestImplementation, ICommandRequest
    {
        public int EpisodeID { get; set; }

		public CommandRequestPriority DefaultPriority
		{
			get { return CommandRequestPriority.Priority4; }
		}

		public string PrettyDescription
		{
			get
			{
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(JMMServer.Properties.Resources.Command_GetEpisodeList, EpisodeID);
			}
		}

		public CommandRequest_GetEpisode()
		{
		}

        public CommandRequest_GetEpisode(int epID)
		{
            this.EpisodeID = epID;
            this.CommandType = (int)CommandRequestType.AniDB_GetEpisodeUDP;
			this.Priority = (int)DefaultPriority;

			GenerateCommandID();
		}

		public override void ProcessCommand()
		{
            logger.Info("Get AniDB episode info: {0}", EpisodeID);

			
			try
			{
                // we don't use this command to update episode info
                // we actually use it to update the cross ref info instead
                // and we only use it for the "Other Episodes" section of the FILE command
                // because that field doesn't tell you what anime it belongs to

                CrossRef_File_EpisodeRepository repCrossRefs = new CrossRef_File_EpisodeRepository();
                List<CrossRef_File_Episode> xrefs = repCrossRefs.GetByEpisodeID(EpisodeID);
                if (xrefs.Count == 0) return;

                Raw_AniDB_Episode epInfo = JMMService.AnidbProcessor.GetEpisodeInfo(EpisodeID);
                if (epInfo != null)
				{
                    AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

                    foreach (CrossRef_File_Episode xref in xrefs)
                    {
                        int oldAnimeID = xref.AnimeID;
                        xref.AnimeID = epInfo.AnimeID;
                        repCrossRefs.Save(xref);


                        AnimeSeries ser = repSeries.GetByAnimeID(oldAnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(oldAnimeID);

                        ser = repSeries.GetByAnimeID(epInfo.AnimeID);
                        if (ser != null)
                            ser.QueueUpdateStats();
                        StatsCache.Instance.UpdateUsingAnime(epInfo.AnimeID);
                    }
				}
				
			}
			catch (Exception ex)
			{
                logger.Error("Error processing CommandRequest_GetEpisode: {0} - {1}", EpisodeID, ex.ToString());
				return;
			}
		}

		/// <summary>
		/// This should generate a unique key for a command
		/// It will be used to check whether the command has already been queued before adding it
		/// </summary>
		public override void GenerateCommandID()
		{
            this.CommandID = string.Format("CommandRequest_GetEpisode_{0}", this.EpisodeID);
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
                this.EpisodeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetEpisode", "EpisodeID"));
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
