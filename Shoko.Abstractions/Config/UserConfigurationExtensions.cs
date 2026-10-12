using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Config;

/// <summary>
///   Registers the user configurations a plugin keeps per user.
/// </summary>
public static partial class UserConfigurationExtensions
{
    #region Registration

    /// <summary>
    ///   Registers a user configuration your plugin owns, with an optional
    ///   schema. Call it from <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    ///   Only registered configurations can be saved; any can be read or
    ///   deleted. Two registrations of one ID stop start-up.
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the configuration belongs to.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationID">
    ///   The configuration's ID: lowercase kebab-case, e.g. <c>my-viewer</c>,
    ///   at most 64 characters.
    /// </param>
    /// <param name="configure">Names the configuration and gives it a schema, if it has one.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID is not valid, the name is blank, both a schema path and a
    ///   schema were given, or the schema path is empty or has a <c>..</c>
    ///   segment.
    /// </exception>
    public static IServiceCollection AddUserConfiguration<TPlugin>(
        this IServiceCollection services,
        string configurationID,
        Action<UserConfigurationOptions>? configure = null
    )
        where TPlugin : class, IPlugin
    {
        ArgumentNullException.ThrowIfNull(services);
        ValidateID(configurationID);

        var options = new UserConfigurationOptions();
        configure?.Invoke(options);
        ValidateName(configurationID, options);
        if (options.SchemaPath is not null && options.SchemaJson is not null)
            throw new ArgumentException($"The user configuration \"{configurationID}\" was given both a schema path and a schema; give one.", nameof(configure));

        var schemaPath = options.SchemaPath is null ? null : NormalizeSchemaPath(options.SchemaPath);
        services.AddSingleton(new UserConfigurationRegistration(
            typeof(TPlugin),
            configurationID,
            options.Name,
            options.Description,
            null,
            schemaPath,
            options.SchemaJson,
            options.Strict
        ));
        return services;
    }

    /// <summary>
    ///   Registers a user configuration your plugin owns, described by the
    ///   configuration type <typeparamref name="TConfig"/>. Call it from
    ///   <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    ///   The configuration is checked, read and written through the
    ///   configuration system, so the schema, the custom validation and the
    ///   generated UI of <typeparamref name="TConfig"/> all apply, per user.
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the configuration belongs to.</typeparam>
    /// <typeparam name="TConfig">The configuration type it is read and written as.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationID">
    ///   The configuration's ID: lowercase kebab-case, e.g. <c>my-viewer</c>,
    ///   at most 64 characters.
    /// </param>
    /// <param name="configure">
    ///   Names the configuration. A schema path or schema is refused, as the
    ///   schema comes from the type.
    /// </param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID is not valid, the name is blank, or a schema path or schema
    ///   was given.
    /// </exception>
    public static IServiceCollection AddUserConfiguration<TPlugin, TConfig>(
        this IServiceCollection services,
        string configurationID,
        Action<UserConfigurationOptions>? configure = null
    )
        where TPlugin : class, IPlugin
        where TConfig : class, IUserConfiguration
    {
        ArgumentNullException.ThrowIfNull(services);
        ValidateID(configurationID);

        var options = new UserConfigurationOptions();
        configure?.Invoke(options);
        ValidateName(configurationID, options);
        if (options.SchemaPath is not null || options.SchemaJson is not null)
            throw new ArgumentException($"The user configuration \"{configurationID}\" takes its schema from {typeof(TConfig).Name}; give no other.", nameof(configure));

        services.AddSingleton(new UserConfigurationRegistration(
            typeof(TPlugin),
            configurationID,
            options.Name,
            options.Description,
            typeof(TConfig),
            null,
            null,
            true
        ));
        return services;
    }

    #endregion

    #region Validation

    /// <summary>
    ///   Checks a configuration ID: lowercase kebab-case, at most 64 characters.
    /// </summary>
    /// <param name="configurationID">The ID.</param>
    /// <exception cref="ArgumentException">The ID is not valid.</exception>
    internal static void ValidateID(string configurationID)
    {
        if (configurationID is not { Length: > 0 and <= 64 } || !IDRegex().IsMatch(configurationID))
            throw new ArgumentException($"\"{configurationID}\" is not a valid user configurations ID; it must be lowercase kebab-case, at most 64 characters.", nameof(configurationID));
    }

    /// <summary>
    ///   Checks the configuration's display name: <c>null</c>, or not blank.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, for the message.</param>
    /// <param name="options">The options holding the name.</param>
    /// <exception cref="ArgumentException">The name is blank.</exception>
    private static void ValidateName(string configurationID, UserConfigurationOptions options)
    {
        if (options.Name is not null && string.IsNullOrWhiteSpace(options.Name))
            throw new ArgumentException($"The user configuration \"{configurationID}\" was given a blank name; give a name or none.", nameof(options));
    }

    /// <summary>
    ///   Normalizes a schema path: one kind of separator, no empty or
    ///   <c>.</c> segments. A path with a <c>..</c> segment is refused.
    /// </summary>
    /// <param name="path">The path, absolute or relative to the data folder.</param>
    /// <returns>The normalized path.</returns>
    /// <exception cref="ArgumentException">The path is empty or has a <c>..</c> segment.</exception>
    internal static string NormalizeSchemaPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A user configurations schema path cannot be empty.", nameof(path));

        var rooted = Path.IsPathRooted(path);
        var root = rooted ? Path.GetPathRoot(path)! : string.Empty;
        var segments = path[root.Length..].Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is ".."))
            throw new ArgumentException($"The user configuration schema path \"{path}\" cannot have a '..' segment.", nameof(path));

        var kept = segments.Where(segment => segment is not ".").ToArray();
        if (kept.Length is 0)
            throw new ArgumentException($"The user configuration schema path \"{path}\" names no file.", nameof(path));

        return root.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar) + string.Join(Path.DirectorySeparatorChar, kept);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IDRegex();

    #endregion
}

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
