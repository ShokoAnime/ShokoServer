using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Config;

/// <summary>
///   How a user configuration is shown and checked, given to
///   <see cref="UserConfigurationExtensions.AddUserConfiguration{TPlugin}(IServiceCollection, string, Action{UserConfigurationOptions}?)"/>.
/// </summary>
public sealed class UserConfigurationOptions
{
    /// <summary>
    ///   The configuration's display name, e.g. <c>My Viewer</c>. Defaults to its
    ///   ID.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   A short, plain description of the configuration, for display. Optional.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///   A JSON schema file, absolute or relative to the server's data folder,
    ///   without <c>..</c> segments. It is read again when it changes.
    /// </summary>
    public string? SchemaPath { get; set; }

    /// <summary>
    ///   A JSON schema, as JSON text.
    /// </summary>
    public string? SchemaJson { get; set; }

    /// <summary>
    ///   Whether a missing or broken <see cref="SchemaPath"/> file refuses
    ///   saves. When <c>false</c>, saves are accepted unvalidated until the
    ///   file can be read. Defaults to <c>true</c>.
    /// </summary>
    public bool Strict { get; set; } = true;
}
