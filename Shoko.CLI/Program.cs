// ******************************* Module Header *******************************
// Module Name:   Program.cs
// Project:       Shoko.Cli
// 
// MIT License
// 
// Copyright © 2020 Shoko
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// *****************************************************************************

namespace Shoko.Cli
{
	using System;
	using System.IO;
	using System.Reflection;
	using Microsoft.Extensions.Configuration;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using NLog.Web;

	public class Program
	{
		private static IHostBuilder CreateBuilder(string[] args)
		{
			var builder = new HostBuilder();

			builder.UseContentRoot(Directory.GetCurrentDirectory());
			builder.ConfigureHostConfiguration(config =>
			                                   {
				                                   config.AddEnvironmentVariables("DOTNET_");
				                                   if (args != null)
				                                   {
					                                   config.AddCommandLine(args);
				                                   }
			                                   });

			builder.ConfigureAppConfiguration((hostingContext, config) =>
			                                  {
				                                  IHostEnvironment env = hostingContext.HostingEnvironment;

				                                  bool reloadOnChange = hostingContext.Configuration.GetValue("hostBuilder:reloadConfigOnChange", true);

				                                  config.AddJsonFile("appsettings.json", true, reloadOnChange)
				                                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, reloadOnChange);

				                                  if (env.IsDevelopment() && !string.IsNullOrEmpty(env.ApplicationName))
				                                  {
					                                  Assembly appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
					                                  if (appAssembly != null)
					                                  {
						                                  config.AddUserSecrets(appAssembly, true);
					                                  }
				                                  }

				                                  config.AddEnvironmentVariables();

				                                  if (args != null)
				                                  {
					                                  config.AddCommandLine(args);
				                                  }
			                                  })
			       .UseDefaultServiceProvider((context, options) =>
			                                  {
				                                  bool isDevelopment = context.HostingEnvironment.IsDevelopment();
				                                  options.ValidateScopes = isDevelopment;
				                                  options.ValidateOnBuild = isDevelopment;
			                                  })
			       .UseNLog();

			return builder;
		}

		public static IHostBuilder CreateHostBuilder(string[] args)
		{
			string instance = null;
			for (var x = 0; x < args.Length; x++)
			{
				if (!args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase))
				{
					continue;
				}

				if (x + 1 >= args.Length)
				{
					continue;
				}

				instance = args[x + 1];
				break;
			}

			var arguments = new ProgramArguments {Instance = instance};
			return CreateBuilder(args)
				.ConfigureServices((hostContext, services) =>
				                   {
					                   services.AddSingleton(arguments);
					                   services.AddHostedService<Worker>();
				                   });
		}

		public static void Main(string[] args)
		{
			IHost host = CreateHostBuilder(args).Build();
			// here we would do things that need to happen after init, but before shutdown, while blocking the main thread.
			host.Run();
		}
	}
}
