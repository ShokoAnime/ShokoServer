using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JMMContracts;
using JMMServer.Repositories;
namespace JMMServer.Entities
{
	public class LogMessage
	{
		public int LogMessageID { get; private set; }
		public string LogType { get; set; }
		public string LogContent { get; set; }
		public DateTime LogDate { get; set; }

		public override string ToString()
		{
			return string.Format("Log: {0} - {1} - {2}", LogDate, LogType, LogContent);
		}

		public Contract_LogMessage ToContract()
		{
			Contract_LogMessage contract = new Contract_LogMessage();

			contract.LogMessageID = this.LogMessageID;
			contract.LogType = this.LogType;
			contract.LogContent = this.LogContent;
			contract.LogDate = this.LogDate;

			return contract;
		}
	}
}
