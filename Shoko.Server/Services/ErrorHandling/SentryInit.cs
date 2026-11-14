using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using NHibernate.Exceptions;
using Sentry;
using Sentry.AspNetCore;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Plugin;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Services.ErrorHandling;

public static class SentryInit
{
    public static IServiceCollection AddSentryConfig(this IServiceCollection services, ISettingsProvider settingsProvider)
    {
        var settings = settingsProvider.GetSettings();

        // Only try to set up Sentry if the user DID NOT OPT __OUT__.
        if (settings.SentryOptOut || !Constants.SentryDsn.StartsWith("https://"))
            return services;

        services.AddSentry();

        return services;
    }

    public static IWebHostBuilder UseSentryConfig(this IWebHostBuilder webHost, ISettingsProvider settingsProvider)
    {
        var settings = settingsProvider.GetSettings();

        // Only try to set up Sentry if the user DID NOT OPT __OUT__.
        if (settings.SentryOptOut || !Constants.SentryDsn.StartsWith("https://"))
            return webHost;

        // Get the release and extra info from the assembly.
        var versionInfo = PluginManager.GetVersionInformation();

        // Only initialize the SDK if we're not on a debug build.
        //
        // If the release channel is not set or if it's set to "stable" or "dev" then
        // it's considered to be a debug build.
        if (versionInfo.Channel is not ReleaseChannel.Stable and not ReleaseChannel.Dev)
            return webHost;

        return webHost.UseSentry(Action);

        void Action(SentryAspNetCoreOptions opts)
        {
            // Assign the DSN key and release version.
            opts.Dsn = Constants.SentryDsn;
            opts.Environment = versionInfo.Channel is ReleaseChannel.Stable ? "stable" : "dev";
            opts.Release = versionInfo.Version.ToSemanticVersioningString();

            // Conditionally assign the extra info if they're included in the assembly.
            if (versionInfo.SourceRevision is { Length: > 0 } gitCommit) opts.DefaultTags.Add("commit", gitCommit);
            if (versionInfo.ReleaseTag is { Length: > 0 } gitTag) opts.DefaultTags.Add("commit.tag", gitTag);

            opts.SampleRate = 0.5f;

            opts.SetBeforeSend(BeforeSentrySend);
        }
    }

    private static readonly HashSet<Type> _ignoredEvents = new() {
        typeof(FileNotFoundException),
        typeof(DirectoryNotFoundException),
        typeof(UnauthorizedAccessException),
        typeof(HttpRequestException)
    };

    private static readonly HashSet<Type> _includedEvents = new()
    {
        typeof(InvalidOperationException),
        typeof(NullReferenceException),
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
        typeof(IndexOutOfRangeException),
        typeof(GenericADOException),
        typeof(UnexpectedUDPResponseException)
    };

    /// <summary>
    ///   The plugins whose failures are reported: the ones the Shoko team
    ///   ships. Legacy Plex, TMDB, AniList, the Offline Importer and WebAOM.
    /// </summary>
    internal static readonly IReadOnlySet<Guid> FirstPartyPluginIDs = new HashSet<Guid>
    {
        new("d5b194ef-6f9d-455b-beb3-b23af899bb21"),
        new("85d0c34f-240f-4da3-8512-d0b45118a9f4"),
        new("e73c1e23-111a-4cf0-bab8-054b738c5101"),
        new("72a6ff39-2bff-534c-9216-d03cd38e7346"),
        new("8c8d75f2-cd0c-5c0b-9a9e-8c2c6d3e5f1a"),
    };

    private static SentryEvent? BeforeSentrySend(SentryEvent arg)
    {
        var ex = arg.Exception;
        if (ex is not null && !ThrownByReportedCode(ex)) return null;
        if (ExceptionAllowed(ex)) return arg;
        if (arg.Logger is not null && arg.Level >= SentryLevel.Fatal) return arg;

        return null;
    }

    /// <summary>
    ///   Whether a failure was thrown by code whose failures are reported: the
    ///   server, its dependencies, or a first-party plugin, rather than a
    ///   third-party plugin. The innermost exception's throw site decides.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><c>false</c> when a third-party plugin threw it.</returns>
    internal static bool ThrownByReportedCode(Exception exception)
    {
        var chain = new List<Exception>();
        for (var current = exception; current is not null; current = current.InnerException)
            chain.Add(current);

        // The innermost exception was thrown first, so its stack is read first.
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var frames = new StackTrace(chain[index], false).GetFrames()
                .Select(frame => frame.GetMethod()?.Module.Assembly)
                .OfType<Assembly>()
                .Select(assembly => (assembly.GetName().Name ?? string.Empty, (AssemblyLoadContext.GetLoadContext(assembly) as PluginLoadContext)?.PluginID));
            if (DecideFromFrames(frames) is { } decision)
                return decision;
        }

        return true;
    }

    /// <summary>
    ///   Decides from the assemblies of a stack's frames, the throw site first,
    ///   whether its failure is reported. The runtime's own frames are skipped.
    ///   The first frame of the server or of a plugin decides: the server's are
    ///   reported, a plugin's only for a first-party plugin. A stack that only
    ///   runs through the server's dependencies is reported.
    /// </summary>
    /// <param name="frames">Each frame's assembly name, and its plugin's ID when a plugin loaded it.</param>
    /// <returns>Whether to report it, or <c>null</c> when no frame decides.</returns>
    internal static bool? DecideFromFrames(IEnumerable<(string AssemblyName, Guid? PluginID)> frames)
    {
        var seenDependency = false;
        foreach (var (assemblyName, pluginID) in frames)
        {
            if (pluginID is { } id)
                return FirstPartyPluginIDs.Contains(id);
            if (IsRuntimeAssembly(assemblyName))
                continue;
            if (assemblyName.StartsWith("Shoko.", StringComparison.Ordinal))
                return true;
            seenDependency = true;
        }

        return seenDependency ? true : null;
    }

    private static bool IsRuntimeAssembly(string assemblyName)
        => assemblyName is "mscorlib" or "netstandard" ||
            assemblyName.StartsWith("System.", StringComparison.Ordinal) || assemblyName == "System" ||
            assemblyName.StartsWith("Microsoft.", StringComparison.Ordinal);

    private static bool ExceptionAllowed(Exception? ex)
    {
        while (true)
        {
            if (ex is null) return false;

            var type = ex.GetType();
            var innerType = ex.InnerException?.GetType();
            if (_ignoredEvents.Contains(type) || (innerType is not null && _ignoredEvents.Contains(innerType))) return false;

            if (type.GetCustomAttribute<SentryIgnoreAttribute>() is not null) return false;

            if (ex is GenericADOException)
            {
                // Error codes: https://www.sqlite.org/rescode.html
                if (ex.InnerException is SqliteException
                    {
                        SqliteErrorCode:
                            4 /* aborted by app */ or
                            8 /* readonly db */ or
                            10 /* disk I/O error */ or
                            11 /* corrupt db */ or
                            13 /* db or fs is full */ or
                            14 /* cannot open file */ or
                            22 /* no LFS support */
                    }) return false;
                if (ex.InnerException is MySqlException { Number: (int)MySqlErrorCode.UnableToConnectToHost }) return false;
            }

            if (_includedEvents.Contains(type)) return true;
            if (type.GetCustomAttribute<SentryIncludeAttribute>() is not null) return true;

            if (ex is WebException webEx)
            {
                if (webEx.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Forbidden }) return false;
                if (webEx.Status == WebExceptionStatus.ConnectFailure) return false;
            }

            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Forbidden }) return false;

            // JobExecutionException no longer exists — unwrap any remaining exception and continue
            if (ex?.InnerException != null) ex = ex.InnerException;
            else return false;
        }
    }
}
