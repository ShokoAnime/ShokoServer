using System;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NHibernate;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Commands
{
    public abstract class CommandRequest
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // ignoring the base properties so that when we serialize we only get the properties
        // defined in the concrete class

        [XmlIgnore]
        public virtual int CommandRequestID { get; set; }

        [XmlIgnore]
        public virtual int Priority { get; set; }

        [XmlIgnore]
        public virtual int CommandType { get; set; }

        [XmlIgnore]
        public virtual string CommandID { get; set; }

        [XmlIgnore]
        public virtual string CommandDetails { get; set; }

        [XmlIgnore]
        public virtual DateTime DateTimeUpdated { get; set; }

        [XmlIgnore]
        public abstract CommandRequestPriority DefaultPriority { get; }

        [XmlIgnore]
        public abstract QueueStateStruct PrettyDescription { get; }

        [XmlIgnore]
        public virtual CommandLimiterType CommandLimiterType => CommandLimiterType.None;

        /// <summary>
        /// Inherited classes to provide the implemenation of how to process this command
        /// </summary>
        public virtual void ProcessCommand()
        {
        }

        public virtual void GenerateCommandID()
        {
        }

        public virtual bool InitFromDB(CommandRequest cq)
        {
            return false;
        }

        public virtual string ToXML()
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", string.Empty);

            XmlSerializer serializer = new XmlSerializer(GetType());
            XmlWriterSettings settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true // Remove the <?xml version="1.0" encoding="utf-8"?>
            };
            StringBuilder sb = new StringBuilder();
            XmlWriter writer = XmlWriter.Create(sb, settings);
            serializer.Serialize(writer, this, ns);

            return sb.ToString();
        }

        public virtual void Save(ISession session)
        {
            CommandRequest crTemp = RepoFactory.CommandRequest.GetByCommandID(CommandID);
            if (crTemp != null)
            {
                // we will always mylist watched state changes
                // this is because the user may be toggling the status in the client, and we need to process
                // them all in the order they were requested
                if (CommandType == (int) CommandRequestType.AniDB_UpdateWatchedUDP)
                    RepoFactory.CommandRequest.Delete(crTemp);
                else
                    return;
            }

            CommandDetails = ToXML();
            DateTimeUpdated = DateTime.Now;
            RepoFactory.CommandRequest.SaveWithOpenTransaction(session, this);

            switch (CommandRequestRepository.GetQueueIndex(this))
            {
                case 0:
                    ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                    break;
                case 1:
                    ShokoService.CmdProcessorHasher.NotifyOfNewCommand();
                    break;
                case 2:
                    ShokoService.CmdProcessorImages.NotifyOfNewCommand();
                    break;
            }
        }

        public virtual void Save()
        {
            CommandRequest crTemp = RepoFactory.CommandRequest.GetByCommandID(CommandID);
            if (crTemp != null)
            {
                // we will always mylist watched state changes
                // this is because the user may be toggling the status in the client, and we need to process
                // them all in the order they were requested
                if (CommandType == (int) CommandRequestType.AniDB_UpdateWatchedUDP)
                    RepoFactory.CommandRequest.Delete(crTemp);
                else
                    return;
            }

            CommandDetails = ToXML();
            DateTimeUpdated = DateTime.Now;
            RepoFactory.CommandRequest.Save(this);

            switch (CommandRequestRepository.GetQueueIndex(this))
            {
                case 0:
                    ShokoService.CmdProcessorGeneral.NotifyOfNewCommand();
                    break;
                case 1:
                    ShokoService.CmdProcessorHasher.NotifyOfNewCommand();
                    break;
                case 2:
                    ShokoService.CmdProcessorImages.NotifyOfNewCommand();
                    break;
            }
        }

        protected virtual string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
        {
            try
            {
                string prop = doc?[keyName]?[propertyName]?.InnerText.Trim() ?? string.Empty;
                return prop;
            }
            catch
            {
                //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
                //BaseConfig.MyAnimeLog.Write("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
                //BaseConfig.MyAnimeLog.Write("keyName: {0}, propertyName: {1}", keyName, propertyName);
                //BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
            }

            return string.Empty;
        }
    }
}