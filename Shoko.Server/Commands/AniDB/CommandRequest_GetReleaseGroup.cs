using System;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetReleaseGroup)]
    public class CommandRequest_GetReleaseGroup : CommandRequestImplementation
    {
        public int GroupID { get; set; }
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = "Getting release group info from UDP API: {0}",
            queueState = QueueStateEnum.GetReleaseInfo,
            extraParams = new[] {GroupID.ToString()}
        };

        public CommandRequest_GetReleaseGroup()
        {
        }

        public CommandRequest_GetReleaseGroup(int grpid, bool forced)
        {
            GroupID = grpid;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            Logger.LogInformation("Processing CommandRequest_GetReleaseGroup: {0}", GroupID);
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();

            try
            {
                var relGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(GroupID);

                if (!ForceRefresh && relGroup != null) return;
                // redownload anime details from http ap so we can get an update character list
                var request = new RequestReleaseGroup { ReleaseGroupID = GroupID };
                var response = request.Execute(handler);

                if (response?.Response == null) return;
                relGroup ??= new AniDB_ReleaseGroup();
                relGroup.GroupID = response.Response.ID;
                relGroup.Rating = (int)(response.Response.Rating * 100);
                relGroup.Votes = response.Response.Votes;
                relGroup.AnimeCount = response.Response.AnimeCount;
                relGroup.FileCount = response.Response.FileCount;
                relGroup.GroupName = response.Response.Name;
                relGroup.GroupNameShort = response.Response.ShortName;
                relGroup.IRCChannel = response.Response.IrcChannel;
                relGroup.IRCServer = response.Response.IrcServer;
                relGroup.URL = response.Response.URL;
                relGroup.Picname = response.Response.Picture;
                RepoFactory.AniDB_ReleaseGroup.Save(relGroup);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error processing CommandRequest_GetReleaseGroup: {0} - {1}", GroupID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetReleaseGroup_{GroupID}";
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
                GroupID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "GroupID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReleaseGroup", "ForceRefresh"));
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