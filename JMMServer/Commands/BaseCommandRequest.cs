using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NLog;
using System.Xml;
using JMMDatabase;
using JMMModels.Childs;
using JMMServer.Repositories;
using JMMServerModels.DB;
using JMMServerModels.DB.Childs;
using Raven.Client;

namespace JMMServer.Commands
{
	public abstract class BaseCommandRequest : CommandRequest
	{
		protected static Logger logger = LogManager.GetCurrentClassLogger();

	    public abstract void ProcessCommand();
        public void Save(IDocumentSession session)
        {
            Store.CommandRequestRepo.Save(this,session);
            if (this.CommandType == CommandRequestType.HashFile)
                JMMService.CmdProcessorHasher.NotifyOfNewCommand();
            else if (CommandType == CommandRequestType.ImageDownload)
                JMMService.CmdProcessorImages.NotifyOfNewCommand();
            else
                JMMService.CmdProcessorGeneral.NotifyOfNewCommand();
        }

	    public void Save()
	    {
	        using (IDocumentSession session = Store.GetSession())
                Save(session);
	    }

    }
}
