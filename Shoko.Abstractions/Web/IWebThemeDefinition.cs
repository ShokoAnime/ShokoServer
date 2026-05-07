using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Web;

/// <summary>
///   Read-only contract for a CSS-based web theme definition.
/// </summary>
/// <remarks>
///   Themes are stored as a pair of files in the <c>themes/</c> directory:
///   a JSON metadata file (<c>{ID}.json</c>) and an optional CSS overrides
///   file (<c>{ID}.css</c>).
/// </remarks>
public interface IWebThemeDefinition
{
    /// <summary>
    ///   The theme ID, inferred from the filename of the theme definition file.
    /// </summary>
    /// <remarks>
    ///   Only JSON files with an alphanumerical filename are recognized as themes.
    /// </remarks>
    string ID { get; }

    /// <summary>
    ///   The file name of the JSON metadata file (<c>{ID}.json</c>).
    /// </summary>
    string JsonFileName { get; }

    /// <summary>
    ///   The file name of the CSS overrides file (<c>{ID}.css</c>).
    /// </summary>
    string CssFileName { get; }

    /// <summary>
    ///   The display name of the theme.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   A short description of the theme, if available.
    /// </summary>
    string? Description { get; }

    /// <summary>
    ///   Author-defined tags for searching the theme.
    /// </summary>
    IReadOnlyList<string> Tags { get; }

    /// <summary>
    ///   The name of the theme author.
    /// </summary>
    string? Author { get; }

    /// <summary>
    ///   The theme version.
    /// </summary>
    Version Version { get; }

    /// <summary>
    ///   The CSS content overrides for the theme.
    /// </summary>
    /// <remarks>
    ///   This content is wrapped inside the <c>.theme-{ID}</c> CSS selector block
    ///   when rendered.
    /// </remarks>
    string? CssContent { get; }

    /// <summary>
    ///   The URL for the theme CSS overrides file, if set.
    /// </summary>
    string? CssUrl { get; }

    /// <summary>
    ///   The URL for the theme definition, used for fetching new versions.
    /// </summary>
    string? UpdateUrl { get; }

    /// <summary>
    ///   Indicates whether this theme is a preview that has not been persisted.
    /// </summary>
    bool IsPreview { get; }

    /// <summary>
    ///   Indicates whether the theme is installed locally on disk.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    ///   The CSS variables defined by the theme.
    /// </summary>
    /// <remarks>
    ///   Sent to the client as CSS custom properties within the theme block,
    ///   not as a JSON dictionary.
    /// </remarks>
    IReadOnlyDictionary<string, string> Values { get; }
}
