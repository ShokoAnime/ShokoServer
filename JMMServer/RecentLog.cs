using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using System.Collections.ObjectModel;
using JMMServer.Commands;

namespace JMMServer
{
	public class RecentLog
	{
		/*public static ObservableCollection<string> LogList { get; set; }

		private static RecentLog _instance;
		public static RecentLog Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new RecentLog();
				}
				return _instance;
			}
		}

		public RecentLog()
		{
			LogList = new ObservableCollection<string>();
		}

		public void AddLogEntry(string evt)
		{
			try
			{
				System.Windows.Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, (Action)delegate()
					{
						LogList.Insert(0, evt);
						if (LogList.Count == 1000) LogList.RemoveAt(999);

					});
			}
			catch (Exception ex)
			{
			}
		}*/
	}
}
