using System;
using System.Xml;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_DeleteXRefAniDBOther)]
    public class CommandRequest_WebCacheDeleteXRefAniDBOther : CommandRequestImplementation
    {
        public int AnimeID { get; set; }
        public int CrossRefType { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDBOther,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_WebCacheDeleteXRefAniDBOther()
        {
        }

        public CommandRequest_WebCacheDeleteXRefAniDBOther(int animeID, CrossRefType xrefType)
        {
            AnimeID = animeID;
            CrossRefType = (int) xrefType;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDBOther(AnimeID, (CrossRefType) CrossRefType);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, 
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDBOther: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDBOther_{AnimeID}_{CrossRefType}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                AnimeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBOther", "AnimeID"));
                CrossRefType =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDBOther",
                        "CrossRefType"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}