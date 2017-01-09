using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace JMMServer
{
	public class App : System.Windows.Application
	{
		[STAThreadAttribute()]
		public static void Main()
		{
			System.Reflection.Assembly assm = System.Reflection.Assembly.GetExecutingAssembly();
			// check if the app config file exists

			string appConfigPath = assm.Location + ".config";
			string defaultConfigPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assm.Location), "default.config");
			if (!File.Exists(appConfigPath) && File.Exists(defaultConfigPath))
			{
				File.Copy(defaultConfigPath, appConfigPath);
			}

			App app = new App();
			app.Run(new MainWindow());
		}
	}
}
