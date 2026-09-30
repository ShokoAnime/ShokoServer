using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
///   Keeps the <c>TMDB</c> section of the settings endpoint the shape it had
///   before its auto-link switches moved to the metadata service's own
///   settings and its image switches, counts and language order moved to
///   TMDB's entry in <see cref="ImageSettings.MetadataSources"/>. The keys are
///   added to what the endpoint sends, and a patch of them is sent on to where
///   they live now.
/// </summary>
internal static class LegacyTmdbSettings
{
    #region Keys

    /// <summary>
    ///   The section the keys sit in.
    /// </summary>
    private const string Section = "TMDB";

    /// <summary>
    ///   The switch for linking on its own.
    /// </summary>
    internal const string AutoLink = "AutoLink";

    /// <summary>
    ///   The switch for linking restricted entries on their own.
    /// </summary>
    internal const string AutoLinkRestricted = "AutoLinkRestricted";

    /// <summary>
    ///   The keys that moved to TMDB's entry in the per-source image settings,
    ///   which spells them the same.
    /// </summary>
    private static readonly string[] _imageKeys =
    [
        "ImageLanguageOrder",
        "AutoDownloadBackdrops",
        "MaxAutoBackdrops",
        "AutoDownloadPosters",
        "MaxAutoPosters",
        "AutoDownloadLogos",
        "MaxAutoLogos",
        "AutoDownloadThumbnails",
        "MaxAutoThumbnails",
        "AutoDownloadStaffImages",
        "MaxAutoStaffImages",
        "AutoDownloadStudioImages",
    ];

    /// <summary>
    ///   The order the section had, so the moved keys go back where they were.
    /// </summary>
    private static readonly string[] _order =
    [
        AutoLink,
        AutoLinkRestricted,
        "ConsiderExistingOtherLinks",
        "DownloadAllTitles",
        "DownloadAllOverviews",
        "DownloadAllContentRatings",
        "ImageLanguageOrder",
        "AutoDownloadCrewAndCast",
        "AutoDownloadCollections",
        "AutoDownloadAlternateOrdering",
        "AutoDownloadNetworks",
        "AutoDownloadBackdrops",
        "MaxAutoBackdrops",
        "AutoDownloadPosters",
        "MaxAutoPosters",
        "AutoDownloadLogos",
        "MaxAutoLogos",
        "AutoDownloadThumbnails",
        "MaxAutoThumbnails",
        "AutoDownloadStaffImages",
        "MaxAutoStaffImages",
        "AutoDownloadStudioImages",
    ];

    #endregion

    #region Output

    /// <summary>
    ///   Adds the moved keys to the <c>TMDB</c> section of the serialized
    ///   settings, in the order the section had.
    /// </summary>
    /// <param name="json">The serialized settings.</param>
    /// <param name="settings">The settings.</param>
    /// <param name="decisions">
    ///   What the metadata service keeps about TMDB, or <c>null</c> when it
    ///   keeps nothing yet, which reads as both switches off.
    /// </param>
    /// <param name="serializer">The serializer that wrote <paramref name="json"/>.</param>
    public static void AddTo(JObject json, ServerSettings settings, MetadataSourceSettings? decisions, JsonSerializer serializer)
    {
        var current = json[Section] as JObject ?? new JObject();
        var moved = new Dictionary<string, JToken>(StringComparer.Ordinal)
        {
            [AutoLink] = decisions?.AutoLink ?? false,
            [AutoLinkRestricted] = decisions?.AutoLinkRestricted ?? false,
        };
        var images = JObject.FromObject(settings.Image.GetMetadataSourceSettings(MetadataSource.TMDB), serializer);
        foreach (var key in _imageKeys)
            if (images[key] is { } value)
                moved[key] = value.DeepClone();

        var section = new JObject();
        foreach (var key in _order)
        {
            if (moved.TryGetValue(key, out var value))
                section[key] = value;
            else if (current.Property(key) is { } property)
                section[key] = property.Value.DeepClone();
        }

        foreach (var property in current.Properties().Where(property => section.Property(property.Name) is null))
            section[property.Name] = property.Value.DeepClone();

        json[Section] = section;
    }

    #endregion

    #region Input

    /// <summary>
    ///   Points the operations on the moved keys at where they live now, before
    ///   the patch is applied. The image keys go to TMDB's entry in the
    ///   per-source image settings, made from the shared defaults if it has
    ///   none. The auto-link switches are not in the server settings, so their
    ///   operations are taken out of the patch and returned, to be set once the
    ///   rest is saved.
    /// </summary>
    /// <param name="patch">The patch, changed in place.</param>
    /// <param name="settings">The settings the patch is applied to.</param>
    /// <param name="decisions">What the metadata service keeps about TMDB, for <c>test</c> operations.</param>
    /// <param name="modelState">Where to add an error for an operation that can't be done.</param>
    /// <returns>The auto-link switches to set, by key.</returns>
    public static IReadOnlyDictionary<string, bool> Translate(JsonPatchDocument<ServerSettings> patch, ServerSettings settings, MetadataSourceSettings? decisions, ModelStateDictionary modelState)
    {
        var switches = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var operation in patch.Operations.ToList())
        {
            if (FindSwitch(operation.path) is { } key)
            {
                var path = operation.path!;
                patch.Operations.Remove(operation);
                var current = key is AutoLink ? decisions?.AutoLink ?? false : decisions?.AutoLinkRestricted ?? false;
                switch (operation.OperationType)
                {
                    case OperationType.Add or OperationType.Replace when ReadBool(operation.value) is { } value:
                        switches[key] = value;
                        break;
                    case OperationType.Test when ReadBool(operation.value) is { } value:
                        if (value != (switches.TryGetValue(key, out var pending) ? pending : current))
                            modelState.TryAddModelError(path, $"The current value of \"{path}\" is not equal to the test value.");
                        break;
                    default:
                        modelState.TryAddModelError(path, $"\"{path}\" can only be set to true or false.");
                        break;
                }

                continue;
            }

            if (FindSwitch(operation.from) is not null)
            {
                modelState.TryAddModelError(operation.from!, $"\"{operation.from}\" can't be moved or copied.");
                patch.Operations.Remove(operation);
                continue;
            }

            operation.path = PointToImages(operation.path, settings);
            operation.from = PointToImages(operation.from, settings);
        }

        return switches;
    }

    /// <summary>
    ///   Gets the auto-link switch a path names, if it names one.
    /// </summary>
    private static string? FindSwitch(string? path)
        => Split(path) is [var section, var key] && string.Equals(section, Section, StringComparison.OrdinalIgnoreCase)
            ? string.Equals(key, AutoLink, StringComparison.OrdinalIgnoreCase)
                ? AutoLink
                : string.Equals(key, AutoLinkRestricted, StringComparison.OrdinalIgnoreCase) ? AutoLinkRestricted : null
            : null;

    /// <summary>
    ///   Rewrites a path into a moved image key to the same key in TMDB's
    ///   entry of the per-source image settings, adding the entry if needed.
    /// </summary>
    private static string? PointToImages(string? path, ServerSettings settings)
    {
        if (Split(path) is not [var section, var key, .. var rest] ||
            !string.Equals(section, Section, StringComparison.OrdinalIgnoreCase) ||
            !_imageKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            return path;

        var sources = settings.Image.MetadataSources;
        var index = sources.FindIndex(entry => entry.Source == MetadataSource.TMDB);
        if (index is -1)
        {
            var entry = JObject.FromObject(settings.Image.MetadataSourceDefaults).ToObject<MetadataSourceImageSettings>()!;
            entry.Source = MetadataSource.TMDB;
            sources.Add(entry);
            index = sources.Count - 1;
        }

        return string.Join('/', ["", nameof(ServerSettings.Image), nameof(ImageSettings.MetadataSources), index.ToString(), key, .. rest]);
    }

    private static string[]? Split(string? path)
        => string.IsNullOrEmpty(path) || path[0] is not '/' ? null : path[1..].Split('/');

    private static bool? ReadBool(object? value)
        => value switch
        {
            bool flag => flag,
            JValue { Type: JTokenType.Boolean } token => token.Value<bool>(),
            _ => null,
        };

    #endregion
}
