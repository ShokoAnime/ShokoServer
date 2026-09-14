using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin.Enums;

namespace Shoko.Abstractions.Plugin.Models;

/// <summary>
///   Represents a feature advertised by a plugin to clients, so they can tell
///   what the server supports.
/// </summary>
/// <remarks>
///   A feature is identified by the plugin ID and its <see cref="Name"/>.
///   Anything a client would toggle UI on should be its own feature, while
///   <see cref="Metadata"/> holds the parameters of a feature.
/// </remarks>
public sealed partial class PluginFeature
{
    /// <summary>
    ///   The maximum length of a feature name.
    /// </summary>
    public const int MaxNameLength = 64;

    /// <summary>
    ///   The name of the feature, unique within the plugin. Must be lowercase
    ///   kebab-case, e.g. <c>password-reset</c>, and at most
    ///   <see cref="MaxNameLength"/> characters long, so it can be used as is
    ///   in a URL. Features with an invalid name are dropped.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The version of the contract clients rely on. Bump the major version
    ///   on breaking changes, including changes to the shape of
    ///   <see cref="Metadata"/>, and the minor version on additions. Only the
    ///   major, minor, and build (patch) components are advertised. Defaults
    ///   to <c>1.0.0</c>.
    /// </summary>
    public Version Version { get; init; } = new(1, 0, 0);

    /// <summary>
    ///   Who the feature is advertised to. Defaults to
    ///   <see cref="PluginFeatureVisibility.Authenticated"/>.
    /// </summary>
    public PluginFeatureVisibility Visibility { get; init; } = PluginFeatureVisibility.Authenticated;

    /// <summary>
    ///   Optional parameters of the feature for clients. It is advertised with
    ///   the same <see cref="Visibility"/> as the feature, so never put anything
    ///   in the metadata of an anonymous feature that unauthenticated clients
    ///   shouldn't see.
    /// </summary>
    public JObject? Metadata { get; init; }

    /// <summary>
    ///   Checks whether the feature is valid to advertise.
    /// </summary>
    /// <param name="feature">The feature to check.</param>
    /// <param name="error">The reason the feature is invalid, if it is.</param>
    /// <returns><see langword="true"/> if the feature is valid; otherwise <see langword="false"/>.</returns>
    public static bool IsValid(PluginFeature? feature, out string? error)
    {
        error = feature switch
        {
            null => "The feature is null.",
            { Name: null or "" } => "The feature name is empty.",
            { Name.Length: > MaxNameLength } => $"The feature name is longer than {MaxNameLength} characters.",
            _ when !NameRegex().IsMatch(feature.Name) => "The feature name is not lowercase kebab-case.",
            { Version: null } => "The feature version is null.",
            _ when !Enum.IsDefined(feature.Visibility) => "The feature visibility is not a defined value.",
            _ => null,
        };
        return error is null;
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}
