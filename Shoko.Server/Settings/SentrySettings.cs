using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.ErrorHandling;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for reporting the server's errors to Sentry. Without a <see cref="Dsn"/> of the admin's own, the server
/// reports to the Shoko team's Sentry with the built-in sample rate and plugins, and only from stable and dev builds.
/// The legacy <c>SENTRY_OPT_OUT</c> environment variable, when true, turns reporting off.
/// </summary>
[Section(DisplaySectionType.Minimal)]
public class SentrySettings
{
    /// <summary>
    /// Where the server reports its errors. Empty or <c>null</c> reports to the Shoko team's Sentry. A DSN starting
    /// with <c>http://</c> or <c>https://</c> reports there instead, from every build, development builds included, and
    /// the settings below apply. Any other value, such as <c>false</c> or <c>off</c>, turns reporting off. This does not
    /// affect the web UI's error reporting.
    /// </summary>
    [Display(Name = "DSN")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_SENTRY_DSN")]
    public string? Dsn { get; set; }

    /// <summary>
    /// The share of errors reported, from 0 to 1. Only applies with a <see cref="Dsn"/> of your own; the Shoko team's
    /// gets half.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.ReadOnly,
        Size = DisplayElementSize.Small,
        ToggleWhenMemberIsSet = nameof(Dsn),
        ToggleOperator = UiConditionOperator.IsNotEmpty,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Sample Rate")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_SENTRY_SAMPLE_RATE")]
    [DefaultValue(SentryInit.BuiltInSampleRate)]
    [Range(0.0, 1.0)]
    public double SampleRate { get; set; } = SentryInit.BuiltInSampleRate;

    /// <summary>
    /// The plugins, by ID, whose own errors are reported. Errors thrown by any other plugin are left out. Only applies
    /// with a <see cref="Dsn"/> of your own; the Shoko team's gets the first-party plugins'. Set through the
    /// environment variable as a JSON array of IDs.
    /// </summary>
    [Visibility(
        Visibility = DisplayVisibility.ReadOnly,
        ToggleWhenMemberIsSet = nameof(Dsn),
        ToggleOperator = UiConditionOperator.IsNotEmpty,
        ToggleVisibilityTo = DisplayVisibility.Visible
    )]
    [Display(Name = "Reported Plugins")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_SENTRY_PLUGINS")]
    [DefaultValue(SentryInit.FirstPartyPluginIDsJson)]
    [List(UniqueItems = true)]
    public List<Guid> ReportedPlugins { get; set; } = [.. SentryInit.FirstPartyPluginIDs];
}
