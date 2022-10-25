using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Server.Utilities.AVDump;

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

    public static List<string> GetEd2ks(string result)
    {
        try
        {
            var xml = "<Files>\n" + string.Join("\n", result.Split("\n").Where(a => a.Trim().StartsWith("<"))) + "\n</Files>";
            var doc = XDocument.Parse(xml);
            var fileInfos = doc.Elements().FirstOrDefault()!.Elements();
            var ed2ks = fileInfos.Select(a =>
                    $"ed2k://|file|{Path.GetFileName(a.Element("Path")?.Value)}|{a.Element("Size")?.Value}|{a.Element("HashProvider")?.Element("ED2K")?.Value}|/")
                .ToList();
            return ed2ks;
        }
        catch (Exception e)
        {
            // ignore
            return new List<string>();
        }
    }
}
