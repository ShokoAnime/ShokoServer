using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Tests.Services;

/// <summary>
///   A settings-shaped configuration for the UI definition builder's tests,
///   laid out as tabs, with its loose members gathered into a section at the
///   end.
/// </summary>
/// <remarks>
///   The builder's tests use it in place of the server's own settings, so they
///   check the builder and never pin the shape the settings happen to have.
/// </remarks>
[Section(DisplaySectionType.Tab, AppendFloatingSectionsAtEnd = true, DefaultSectionName = "Misc.")]
public class UiSettingsFixture : INewtonsoftJsonConfiguration
{
    /// <summary>
    ///   The account the fixture signs in with. This summary is wrapped over
    ///   several lines on purpose, so the builder has indentation and line
    ///   breaks to strip.
    /// </summary>
    /// <remarks>
    ///   The test assembly ships no XML documentation, so the description
    ///   arrives the way a summary would: indented and wrapped.
    /// </remarks>
    [Description("\n        The account the fixture signs in with. This text is wrapped over\n        several lines on purpose, so the builder has breaks to strip.\n    ")]
    public UiSettingsAccount Account { get; set; } = new();

    /// <summary>
    ///   The extensions the fixture loads.
    /// </summary>
    public UiSettingsExtensions Extensions { get; set; } = new();

    /// <summary>
    ///   Whether the fixture says more than it needs to.
    ///
    ///   A second paragraph, kept apart by a blank line.
    /// </summary>
    [Description("\n        Whether the fixture says more than it needs to.\n\n        A second paragraph, kept apart by a blank line.\n    ")]
    public bool Verbose { get; set; }

    /// <summary>
    ///   How long to wait, in seconds.
    /// </summary>
    [Range(1, 60)]
    public int Timeout { get; set; } = 10;
}

/// <summary>
///   An account whose members each carry a section name, with an action pinned
///   to one section and a nested container filed under another.
/// </summary>
[Section(DisplaySectionType.Minimal)]
public class UiSettingsAccount
{
    /// <summary>
    ///   The user name.
    /// </summary>
    [SectionName("Login")]
    public string? Username { get; set; }

    /// <summary>
    ///   The password.
    /// </summary>
    [SectionName("Login")]
    [PasswordPropertyText]
    public string? Password { get; set; }

    /// <summary>
    ///   Tests the login.
    /// </summary>
    [CustomAction(Theme = DisplayColorTheme.Primary, Position = DisplayButtonPosition.Start, SectionName = "Login")]
    public void Test() { }

    /// <summary>
    ///   The host to sign in at.
    /// </summary>
    [SectionName("Server")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    ///   What to dump when signing in fails.
    /// </summary>
    [SectionName("Server")]
    public UiSettingsDump Dump { get; set; } = new();

    /// <summary>
    ///   The lists the account keeps.
    /// </summary>
    public UiSettingsLists Lists { get; set; } = new();
}

/// <summary>
///   A nested container filed under a section name.
/// </summary>
public class UiSettingsDump
{
    /// <summary>
    ///   Whether to dump at all.
    /// </summary>
    public bool Active { get; set; }
}

/// <summary>
///   A nested container of its own.
/// </summary>
public class UiSettingsLists
{
    /// <summary>
    ///   Whether to keep the lists in sync.
    /// </summary>
    public bool Sync { get; set; }
}

/// <summary>
///   A container that names no default section, with loose members and nested
///   containers side by side.
/// </summary>
public class UiSettingsExtensions
{
    /// <summary>
    ///   The extensions that are on, by name.
    /// </summary>
    public Dictionary<string, bool> Enabled { get; set; } = [];

    /// <summary>
    ///   The order to load them in.
    /// </summary>
    [List(UniqueItems = true, Sortable = true)]
    public List<string> Order { get; set; } = [];

    /// <summary>
    ///   How extensions rename things.
    /// </summary>
    public UiSettingsRenaming Renaming { get; set; } = new();

    /// <summary>
    ///   How extensions are updated.
    /// </summary>
    public UiSettingsUpdates Updates { get; set; } = new();
}

/// <summary>
///   A nested container of its own.
/// </summary>
public class UiSettingsRenaming
{
    /// <summary>
    ///   Whether to rename on import.
    /// </summary>
    public bool OnImport { get; set; }
}

/// <summary>
///   A container that is nothing but loose members.
/// </summary>
public class UiSettingsUpdates
{
    /// <summary>
    ///   Whether to look for updates.
    /// </summary>
    public bool AutoSync { get; set; }

    /// <summary>
    ///   Whether to install them.
    /// </summary>
    public bool AutoUpgrade { get; set; }
}
