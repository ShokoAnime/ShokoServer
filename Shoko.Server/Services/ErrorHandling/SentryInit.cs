﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using NLog;
using Sentry;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

#nullable enable

namespace Shoko.Server.Services.ErrorHandling;

public class SentryInit : IDisposable
{
    private readonly ISettingsProvider _settingsProvider;
    private IDisposable? _sentry;

    public SentryInit(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public void Init()
    {
        if (_sentry is not null)
            return;

        var settings = _settingsProvider.GetSettings();

        // Only try to set up Sentry if the user DID NOT OPT __OUT__.
        if (settings.SentryOptOut || !Constants.SentryDsn.StartsWith("https://"))
            return;

        // Get the release and extra info from the assembly.
        var extraInfo = Utils.GetApplicationExtraVersion();

        // Only initialize the SDK if we're not on a debug build.
        //
        // If the release channel is not set or if it's set to "debug" then
        // it's considered to be a debug build.
        if (extraInfo.TryGetValue("channel", out var environment) && environment != "debug")
            Init(environment, extraInfo);
    }

    private void Init(string environment, Dictionary<string, string> extraInfo)
    {
        _sentry = SentrySdk.Init(opts =>
        {
            // Assign the DSN key and release version.
            opts.Dsn = Constants.SentryDsn;
            opts.Environment = environment;
            opts.Release = Utils.GetApplicationVersion();

            // Conditionally assign the extra info if they're included in the assembly.
            if (extraInfo.TryGetValue("commit", out var gitCommit))
                opts.DefaultTags.Add("commit", gitCommit);
            if (extraInfo.TryGetValue("tag", out var gitTag))
                opts.DefaultTags.Add("commit.tag", gitTag);

            // Append the release channel for the release on non-stable branches.
            if (environment != "stable")
                opts.Release += string.IsNullOrEmpty(gitCommit) ? $"-{environment}" : $"-{environment}-{gitCommit[0..7]}";

            opts.SampleRate = 0.5f;

            opts.BeforeSend += BeforeSentrySend;
        });

        LogManager.Configuration.AddSentry(o =>
        {
            o.MinimumEventLevel = LogLevel.Fatal;
        });
    }

    private readonly List<Type> IgnoredEvents = new List<Type> {
        typeof(FileNotFoundException),
        typeof(DirectoryNotFoundException),
        typeof(UnauthorizedAccessException),
        typeof(HttpRequestException)
    };
    
    private SentryEvent? BeforeSentrySend(SentryEvent arg)
    {
        if (arg.Exception is not null && IgnoredEvents.Contains(arg.Exception.GetType()))
            return null;

        if (arg.Exception is WebException ex)
        {
            if (ex.Response is HttpWebResponse resp && resp.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (ex.Status == WebExceptionStatus.ConnectFailure)
                return null;
        }

        if (arg.Exception?.GetType().GetCustomAttribute<SentryIgnoreAttribute>() is not null)
            return null;
        
        //This should never happen, but this is to be 100% sure
        if (arg.Logger is not null && arg.Level < SentryLevel.Fatal)
            return null;

        return arg;
    }

    public void Dispose()
    {
        _sentry?.Dispose();
    }
}
