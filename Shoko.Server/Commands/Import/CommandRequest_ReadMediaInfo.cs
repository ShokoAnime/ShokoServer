using System;
using System.Xml;
using Force.DeepCloner;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_ReadMediaInfo : CommandRequest
    {
        public virtual int VideoLocalID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority4;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.ReadingMedia,
            extraParams = new[] {VideoLocalID.ToString()}
        };

        public CommandRequest_ReadMediaInfo()
        {
        }

        public CommandRequest_ReadMediaInfo(int vidID)
        {
            VideoLocalID = vidID;
            CommandType = (int) CommandRequestType.ReadMediaInfo;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Reading Media Info for File: {0}", VideoLocalID);


            try
            {
                SVR_VideoLocal vlocal = Repo.VideoLocal.GetByID(VideoLocalID);
                SVR_VideoLocal_Place place = vlocal?.GetBestVideoLocalPlace();
                if (place == null)
                {
                    logger.Error("Cound not find Video: {0}", VideoLocalID);
                    return;
                }

                SVR_VideoLocal local = place.VideoLocal.DeepClone();
                place.RefreshMediaInfo(local);
                using (var upd = Repo.VideoLocal.BeginAddOrUpdate(()=> Repo.VideoLocal.GetByID(VideoLocalID)))
                {
                    if (upd.Original != null)
                    {
                        local.DeepCloneTo(upd.Entity);
                        upd.Commit(true);
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_ReadMediaInfo: {0} - {1}", VideoLocalID, ex);
            }
        }


        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_ReadMediaInfo_{VideoLocalID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
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
                VideoLocalID = int.Parse(
                    TryGetProperty(docCreator, "CommandRequest_ReadMediaInfo", "VideoLocalID"));
            }

            return true;
        }
    }
}