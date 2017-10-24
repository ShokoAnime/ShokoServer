using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheSendXRefFileEpisode : CommandRequest
    {
        public virtual int CrossRef_File_EpisodeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefFileEpisode,
            extraParams = new[] {CrossRef_File_EpisodeID.ToString()}
        };

        public CommandRequest_WebCacheSendXRefFileEpisode()
        {
        }

        public CommandRequest_WebCacheSendXRefFileEpisode(int crossRef_File_EpisodeID)
        {
            CrossRef_File_EpisodeID = crossRef_File_EpisodeID;
            CommandType = (int) CommandRequestType.WebCache_SendXRefFileEpisode;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                CrossRef_File_Episode xref = RepoFactory.CrossRef_File_Episode.GetByID(CrossRef_File_EpisodeID);
                if (xref == null) return;

                AzureWebAPI.Send_CrossRefFileEpisode(xref);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_WebCacheSendXRefFileEpisode: {0} - {1}",
                    CrossRef_File_EpisodeID,
                    ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefFileEpisode{CrossRef_File_EpisodeID}";
        }

        public override bool InitFromDB(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                CrossRef_File_EpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefFileEpisode",
                        "CrossRef_File_EpisodeID"));
            }

            return true;
        }
    }
}