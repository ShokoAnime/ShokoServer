using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Newtonsoft.Json;
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

public static partial class SentryInit
{
    /// <summary>
    ///   The share of errors reported to the Shoko team's Sentry.
    /// </summary>
    internal const double BuiltInSampleRate = 0.5;

    /// <summary>
    ///   The plugins whose own errors are reported, set when Sentry starts;
    ///   the first-party ones until then.
    /// </summary>
    private static IReadOnlySet<Guid>? _reportedPlugins;

    /// <summary>
    ///   Where and how the server reports its errors.
    /// </summary>
    /// <param name="Dsn">The DSN to report to.</param>
    /// <param name="SampleRate">The share of errors reported.</param>
    /// <param name="ReportedPlugins">The plugins whose own errors are reported, or every plugin's when empty.</param>
    internal sealed record SentryReporting(string Dsn, double SampleRate, IReadOnlySet<Guid> ReportedPlugins);

    public static IServiceCollection AddSentryConfig(this IServiceCollection services, ISettingsProvider settingsProvider)
    {
        if (GetReporting(settingsProvider.GetSettings().Sentry, PluginManager.GetVersionInformation()) is null)
            return services;

        services.AddSentry();

        return services;
    }

    public static IWebHostBuilder UseSentryConfig(this IWebHostBuilder webHost, ISettingsProvider settingsProvider)
    {
        // Get the release and extra info from the assembly.
        var versionInfo = PluginManager.GetVersionInformation();
        if (GetReporting(settingsProvider.GetSettings().Sentry, versionInfo) is not { } reporting)
            return webHost;

        _reportedPlugins = reporting.ReportedPlugins;
        return webHost.UseSentry(Action);

        void Action(SentryAspNetCoreOptions opts)
        {
            // Assign the DSN key and release version.
            opts.Dsn = reporting.Dsn;
            opts.Environment = versionInfo.Channel is ReleaseChannel.Stable ? "stable" : "dev";
            opts.Release = versionInfo.Version.ToSemanticVersioningString();

            // Conditionally assign the extra info if they're included in the assembly.
            if (versionInfo.SourceRevision is { Length: > 0 } gitCommit) opts.DefaultTags.Add("commit", gitCommit);
            if (versionInfo.ReleaseTag is { Length: > 0 } gitTag) opts.DefaultTags.Add("commit.tag", gitTag);

            opts.SampleRate = (float)reporting.SampleRate;

            opts.SetBeforeSend(BeforeSentrySend);
        }
    }

    /// <summary>
    ///   Where and how the server reports, or <c>null</c> when it reports nothing. A usable DSN the admin set is used on
    ///   any build, with the admin's sample rate and plugins, and any other non-empty value turns reporting off. With
    ///   an empty or <c>null</c> DSN, a stable or dev build reports to the built-in DSN with the built-in sample rate
    ///   and the first-party plugins, and a debug build reports nothing.
    /// </summary>
    /// <param name="settings">The Sentry settings.</param>
    /// <param name="versionInfo">The build's version information.</param>
    /// <returns>The reporting, or <c>null</c>.</returns>
    internal static SentryReporting? GetReporting(SentrySettings settings, VersionInformation versionInfo)
    {
        if (settings.Dsn?.Trim() is { Length: > 0 } dsn)
            return IsUsableDsn(dsn)
                ? new(dsn, Math.Clamp(settings.SampleRate, 0, 1), settings.ReportedPlugins.ToHashSet())
                : null;
        if (versionInfo.Channel is not ReleaseChannel.Stable and not ReleaseChannel.Dev)
            return null;
        return IsUsableDsn(Constants.SentryDsn)
            ? new(Constants.SentryDsn, BuiltInSampleRate, FirstPartyPluginIDs)
            : null;
    }

    /// <summary>
    ///   Whether a DSN is one to report to: it starts with <c>http://</c> or
    ///   <c>https://</c>. Any other value turns reporting off.
    /// </summary>
    /// <param name="dsn">The DSN.</param>
    /// <returns><c>true</c> for a usable DSN.</returns>
    internal static bool IsUsableDsn(string dsn)
        => UsableDsnRegex().IsMatch(dsn);

    [GeneratedRegex(@"^https?://")]
    private static partial Regex UsableDsnRegex();

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
    internal static readonly IReadOnlySet<Guid> FirstPartyPluginIDs = JsonConvert.DeserializeObject<List<Guid>>(FirstPartyPluginIDsJson)!.ToHashSet();

    /// <summary>
    ///   <see cref="FirstPartyPluginIDs"/> as a JSON array, the default of
    ///   <see cref="SentrySettings.ReportedPlugins"/>.
    /// </summary>
    internal const string FirstPartyPluginIDsJson =
        """["d5b194ef-6f9d-455b-beb3-b23af899bb21","85d0c34f-240f-4da3-8512-d0b45118a9f4","e73c1e23-111a-4cf0-bab8-054b738c5101","72a6ff39-2bff-534c-9216-d03cd38e7346","8c8d75f2-cd0c-5c0b-9a9e-8c2c6d3e5f1a"]""";

    private static SentryEvent? BeforeSentrySend(SentryEvent arg)
    {
        var ex = arg.Exception;
        if (ex is not null && !ThrownByReportedCode(ex, _reportedPlugins ?? FirstPartyPluginIDs)) return null;
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
    /// <param name="reportedPlugins">The plugins whose own failures are reported, or every plugin's when empty.</param>
    /// <returns><c>false</c> when another plugin threw it.</returns>
    internal static bool ThrownByReportedCode(Exception exception, IReadOnlySet<Guid> reportedPlugins)
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
            if (DecideFromFrames(frames, reportedPlugins) is { } decision)
                return decision;
        }

        return true;
    }

    /// <summary>
    ///   Decides from the assemblies of a stack's frames, the throw site first,
    ///   whether its failure is reported. The runtime's own frames are skipped.
    ///   The first frame of the server or of a plugin decides: the server's are
    ///   reported, a plugin's only for a plugin in <paramref name="reportedPlugins"/>,
    ///   or for any plugin when it is empty. A stack that only runs through the
    ///   server's dependencies is reported.
    /// </summary>
    /// <param name="frames">Each frame's assembly name, and its plugin's ID when a plugin loaded it.</param>
    /// <param name="reportedPlugins">The plugins whose own failures are reported, or every plugin's when empty.</param>
    /// <returns>Whether to report it, or <c>null</c> when no frame decides.</returns>
    internal static bool? DecideFromFrames(IEnumerable<(string AssemblyName, Guid? PluginID)> frames, IReadOnlySet<Guid> reportedPlugins)
    {
        var seenDependency = false;
        foreach (var (assemblyName, pluginID) in frames)
        {
            if (pluginID is { } id)
                return reportedPlugins.Count is 0 || reportedPlugins.Contains(id);
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
