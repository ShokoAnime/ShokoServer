using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using SharpCompress.Common;
using SharpCompress.Readers;
using Shoko.Commons.Utils;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.AVDump;

namespace Shoko.Server;

public static class AVDumpHelper
{
    public static void ConfigureServices(IServiceCollection services)
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        services.AddSingleton<AVDump3Handler>();
    }

    private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        var execAssembly = Assembly.GetExecutingAssembly();
        var folderPath = Path.GetDirectoryName(execAssembly.Location);
        if (string.IsNullOrEmpty(folderPath)) return null;
        var assemblyPath = Path.Combine(folderPath, new AssemblyName(args.Name).Name + ".dll");
        if (!File.Exists(assemblyPath))
        {
            assemblyPath = Path.Combine(folderPath, "AVDump3", new AssemblyName(args.Name).Name + ".dll");
            if (!File.Exists(assemblyPath)) return null;
        }
        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly;
    }

    private static List<string> GetEd2ks(string result)
    {
        try
        {
            var xml = "<Files>\n" + string.Join("\n", result.Split("\n").Where(a => a.Trim().StartsWith("<"))) + "\n</Files>";
            var doc = new XDocument(xml);
            return doc.Elements().Select(a => a.Value).ToList();
        }
        catch (Exception e)
        {
            // ignore
            return new List<string>();
        }
    }
}
