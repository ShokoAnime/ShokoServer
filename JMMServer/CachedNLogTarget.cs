using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Targets;
using System.Windows; 

namespace JMMServer
{
	[Target("InternalCache")] 
	public sealed class CachedNLogTarget : TargetWithLayout 
	{
		public CachedNLogTarget()
        {
        }
 
        protected override void Write(LogEventInfo logEvent) 
        { 
            string logMessage = this.Layout.Render(logEvent);
			

			//MainWindow.AddLogEntry(logMessage);
        } 

	}
}
