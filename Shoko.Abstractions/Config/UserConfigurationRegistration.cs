using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Config;

/// <summary>
///   A user configuration a plugin registered with
///   <see cref="UserConfigurationExtensions.AddUserConfiguration{TPlugin}(IServiceCollection, string, Action{UserConfigurationOptions}?)"/>,
///   left in the service collection for the server to pick up.
/// </summary>
/// <param name="PluginType">The plugin the configuration belongs to.</param>
/// <param name="ID">The configuration's kebab-case ID.</param>
/// <param name="Name">The configuration's display name, if given.</param>
/// <param name="Description">The configuration's description, if given.</param>
/// <param name="ConfigurationType">The type the schema is generated from, if any.</param>
/// <param name="SchemaPath">The normalized schema path, if any.</param>
/// <param name="SchemaJson">The schema as JSON text, if any.</param>
/// <param name="Strict">Whether a missing or broken schema file refuses saves.</param>
internal sealed record UserConfigurationRegistration(
    Type PluginType,
    string ID,
    string? Name,
    string? Description,
    Type? ConfigurationType,
    string? SchemaPath,
    string? SchemaJson,
    bool Strict
)
{
    /// <summary>
    ///   The display name, or the ID when none was given.
    /// </summary>
    public string DisplayName
        => Name ?? ID;
}
