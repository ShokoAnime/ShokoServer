using System;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NLog;
using Shoko.Commons.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Commands.Exceptions;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{

    public abstract class CommandRequestImplementation : ICommandRequest
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // ignoring the base properties so that when we serialize we only get the properties
        // defined in the concrete class

        [XmlIgnore]
        public int CommandRequestID { get; set; }

        [XmlIgnore]
        public int Priority { get; set; }

        [XmlIgnore]
        public int CommandType => (int?)GetType().GetCustomAttribute<CommandAttribute>()?.RequestType ?? -1;

        [XmlIgnore]
        public string CommandID { get; set; }

        [XmlIgnore]
        public string CommandDetails { get; set; }

        [XmlIgnore]
        public DateTime DateTimeUpdated { get; set; }

        [XmlIgnore]
        public bool BubbleExceptions = false;

        /// <summary>
        /// Inherited classes to provide the implemenation of how to process this command
        /// </summary>
        /// <param name="serviceProvider"></param>
        protected abstract void Process(IServiceProvider serviceProvider);

        public void ProcessCommand(IServiceProvider serviceProvider)
        {
            try
            {
                Process(serviceProvider);
            }
            catch (Exception e)
            {
                if (BubbleExceptions)
                    throw;
                logger.Error(e, "Error processing {Type}: {CommandDetails} - {Exception}", GetType().Name, CommandID, e);
            }
        }

        public abstract void GenerateCommandID();

        public abstract bool LoadFromDBCommand(CommandRequest cq);
        public abstract CommandRequestPriority DefaultPriority { get; }
        public abstract QueueStateStruct PrettyDescription { get; }
        public virtual CommandConflict ConflictBehavior { get; } = CommandConflict.Ignore;

        public abstract CommandRequest ToDatabaseObject();

        public string ToXML()
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

        public void Save(bool force = false)
        {
            var commandID = CommandID + (force ? "_Forced" : "");
            var crTemp = RepoFactory.CommandRequest.GetByCommandID(commandID);
            if (crTemp != null)
            {
                switch (ConflictBehavior)
                {
                    case CommandConflict.Replace: RepoFactory.CommandRequest.Delete(crTemp);
                        break;
                    case CommandConflict.Ignore: return;
                    case CommandConflict.Error:
                    default: throw new CommandExistsException {CommandID = commandID};
                }
            }

            var cri = ToDatabaseObject();
            cri.CommandID = commandID;
            RepoFactory.CommandRequest.Save(cri);

            switch (CommandRequestRepository.GetQueueIndex(cri))
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

        protected string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
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