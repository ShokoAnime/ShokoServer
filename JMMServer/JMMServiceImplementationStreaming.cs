using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using JMMContracts;
using NLog;

namespace JMMServer
{
	public class JMMServiceImplementationStreaming : IJMMServerStreaming
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public Stream Download(string fileName)
		{
			try
			{
				if (!File.Exists(fileName)) return null;

				return File.Open(fileName, FileMode.Open, FileAccess.Read);
			}
			catch (Exception ex)
			{
				logger.ErrorException(ex.ToString(), ex);
				return null;
			}
		}
	}
}
