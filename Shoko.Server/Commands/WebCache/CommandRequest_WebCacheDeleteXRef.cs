using System;
using System.Security.Policy;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_DeleteXRefAniDB)]
    public class CommandRequest_WebCacheDeleteXRef : CommandRequestImplementation
    {
        public int AniDBID { get; set; }
        public string Provider { get; set; }
        public string ProviderID { get; set; }
        public int PID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheDeleteXRefAniDB,
            extraParams = new[] {AniDBID.ToString(), Provider }
        };

        public CommandRequest_WebCacheDeleteXRef()
        {
        }

        public CommandRequest_WebCacheDeleteXRef(int aniDBID, string provider, string providerID)
        {
            AniDBID = aniDBID;
            Provider = provider;
            ProviderID = providerID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                AzureWebAPI.Delete_CrossRefAniDB(AniDBID, Provider, ProviderID);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDB: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheDeleteXRefAniDB{AniDBID}_{Provider}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            try
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
                    AniDBID = int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDB", "AniDBID"));
                    Provider = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDB", "Provider");
                    ProviderID = TryGetProperty(docCreator, "CommandRequest_WebCacheDeleteXRefAniDB", "ProviderID");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheDeleteXRefAniDB: {0}" + ex);
                return true;
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