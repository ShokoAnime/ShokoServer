using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMServer.Entities;

namespace JMMServer.Commands
{
	public interface ICommandRequest
	{
		void ProcessCommand();
		bool LoadFromDBCommand(CommandRequest cq);
		CommandRequestPriority DefaultPriority { get; }
		string PrettyDescription { get; }
	}
}
