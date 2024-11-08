﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using NHibernate.Exceptions;
using Quartz;
using Sentry;
using Sentry.AspNetCore;
using Shoko.Server.Providers.AniDB.UDP.Exceptions;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

#nullable enable

namespace Shoko.Server.Services.ErrorHandling;

public static class SentryInit
{
    public static IWebHostBuilder UseSentryConfig(this IWebHostBuilder webHost)
    {
        var settings = Utils.SettingsProvider.GetSettings();

        // Only try to set up Sentry if the user DID NOT OPT __OUT__.
        if (settings.SentryOptOut || !Constants.SentryDsn.StartsWith("https://"))
            return webHost;

        // Get the release and extra info from the assembly.
        var extraInfo = Utils.GetApplicationExtraVersion();

        // Only initialize the SDK if we're not on a debug build.
        //
        // If the release channel is not set or if it's set to "debug" then
        // it's considered to be a debug build.
        if (!extraInfo.TryGetValue("channel", out var environment) || environment == "debug")
            return webHost;

        return webHost.UseSentry(Action);

        void Action(SentryAspNetCoreOptions opts)
        {
            // Assign the DSN key and release version.
            opts.Dsn = Constants.SentryDsn;
            opts.Environment = environment;
            opts.Release = Utils.GetApplicationVersion();

            // Conditionally assign the extra info if they're included in the assembly.
            if (extraInfo.TryGetValue("commit", out var gitCommit)) opts.DefaultTags.Add("commit", gitCommit);
            if (extraInfo.TryGetValue("tag", out var gitTag)) opts.DefaultTags.Add("commit.tag", gitTag);

            // Append the release channel for the release on non-stable branches.
            if (environment is not "stable" and not "dev") opts.Release += string.IsNullOrEmpty(gitCommit) ? $"-{environment}" : $"-{environment}-{gitCommit[0..7]}";

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
        typeof(JobPersistenceException),
        typeof(InvalidOperationException),
        typeof(NullReferenceException),
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
        typeof(IndexOutOfRangeException),
        typeof(GenericADOException),
        typeof(UnexpectedUDPResponseException)
    };

    private static SentryEvent? BeforeSentrySend(SentryEvent arg)
    {
        var ex = arg.Exception;
        if (ExceptionAllowed(ex)) return arg;
        if (arg.Logger is not null && arg.Level >= SentryLevel.Fatal) return arg;

        return null;
    }

    private static bool ExceptionAllowed(Exception? ex)
    {
        while (true)
        {
            if (ex is null) return false;

            var type = ex.GetType();
            if (_ignoredEvents.Contains(type)) return false;

            if (type.GetCustomAttribute<SentryIgnoreAttribute>() is not null) return false;

            if (_includedEvents.Contains(type)) return true;
            if (type.GetCustomAttribute<SentryIncludeAttribute>() is not null) return true;

            if (ex is WebException webEx)
            {
                if (webEx.Response is HttpWebResponse { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Forbidden }) return false;
                if (webEx.Status == WebExceptionStatus.ConnectFailure) return false;
            }

            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.Forbidden }) return false;

            if (ex is not JobExecutionException jobEx) return false;
            ex = jobEx.InnerException;
        }
    }
}
