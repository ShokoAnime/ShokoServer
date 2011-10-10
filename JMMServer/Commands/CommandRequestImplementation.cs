using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NLog;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
	public abstract class CommandRequestImplementation
	{
		protected static Logger logger = LogManager.GetCurrentClassLogger();

		// ignoring the base properties so that when we serialize we only get the properties
		// defined in the concrete class

		[XmlIgnore]
		public int CommandRequestID { get; set; }

		[XmlIgnore]
		public int Priority { get; set; }

		[XmlIgnore]
		public int CommandType { get; set; }

		[XmlIgnore]
		public string CommandID { get; set; }

		[XmlIgnore]
		public string CommandDetails { get; set; }

		[XmlIgnore]
		public DateTime DateTimeUpdated { get; set; }

		/// <summary>
		/// Inherited classes to provide the implemenation of how to process this command
		/// </summary>
		public abstract void ProcessCommand();

		public abstract void GenerateCommandID();

		public abstract bool LoadFromDBCommand(CommandRequest cq);

		public abstract CommandRequest ToDatabaseObject();

		public string ToXML()
		{
			XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
			ns.Add("", "");

			XmlSerializer serializer = new XmlSerializer(this.GetType());
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = true; // Remove the <?xml version="1.0" encoding="utf-8"?>

			StringBuilder sb = new StringBuilder();
			XmlWriter writer = XmlWriter.Create(sb, settings);
			serializer.Serialize(writer, this, ns);

			return sb.ToString();
		}

		public void Save()
		{
			CommandRequestRepository repCR = new CommandRequestRepository();
			CommandRequest crTemp = repCR.GetByCommandID(this.CommandID);
			if (crTemp != null)
			{
				// we will always mylist watched state changes
				// this is because the user may be toggling the status in the client, and we need to process
				// them all in the order they were requested
				if (CommandType != (int)CommandRequestType.AniDB_UpdateWatchedUDP)
				{
					logger.Trace("Command already in queue with identifier so skipping: {0}", this.CommandID);
					return;
				}
			}

			CommandRequest cri = this.ToDatabaseObject();
			repCR.Save(cri);

			if (CommandType == (int)CommandRequestType.HashFile)
				JMMService.CmdProcessorHasher.NotifyOfNewCommand();
			else if (CommandType == (int)CommandRequestType.ImageDownload)
				JMMService.CmdProcessorImages.NotifyOfNewCommand();
			else
				JMMService.CmdProcessorGeneral.NotifyOfNewCommand();
		}

		protected string TryGetProperty(XmlDocument doc, string keyName, string propertyName)
		{
			try
			{
				string prop = doc[keyName][propertyName].InnerText.Trim();
				return prop;
			}
			catch (Exception ex)
			{
				//BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
				//BaseConfig.MyAnimeLog.Write("Error in XMLService.TryGetProperty: {0}-{1}", Utils.GetParentMethodName(), ex.ToString());
				//BaseConfig.MyAnimeLog.Write("keyName: {0}, propertyName: {1}", keyName, propertyName);
				//BaseConfig.MyAnimeLog.Write("---------------------------------------------------------------");
			}

			return "";
		}
	}
}
