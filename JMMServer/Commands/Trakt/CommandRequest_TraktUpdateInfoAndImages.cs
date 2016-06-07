using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.TraktTV;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktUpdateInfoAndImages : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktUpdateInfoAndImages()
        {
        }

        public CommandRequest_TraktUpdateInfoAndImages(string traktID)
        {
            TraktID = traktID;
            CommandType = (int)CommandRequestType.Trakt_UpdateInfoImages;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public string TraktID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_UpdateTraktData, TraktID);
            }
        }


        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktUpdateInfoAndImages: {0}", TraktID);

            try
            {
                TraktTVHelper.UpdateAllInfoAndImages(TraktID, false);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktUpdateInfoAndImages: {0} - {1}", TraktID,
                    ex.ToString());
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                TraktID = TryGetProperty(docCreator, "CommandRequest_TraktUpdateInfoAndImages", "TraktID");
            }

            return true;
        }


        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TraktUpdateInfoAndImages{0}", TraktID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}