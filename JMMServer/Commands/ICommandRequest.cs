using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;
using JMMServerModels.DB.Childs;

namespace JMMServer.Commands
{
	public interface ICommandRequest
	{
		void ProcessCommand();
		CommandRequestPriority DefaultPriority { get; }
		string PrettyDescription { get; }
	}
}
