using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Web.Services;

/// <summary>
///   Service responsible for managing CSS-based web themes.
/// </summary>
/// <remarks>
///   Themes are stored in the <c>themes/</c> directory relative to the
///   application path. Each theme consists of a JSON metadata file and an
///   optional CSS file with the same base name.
/// </remarks>
public interface IWebThemeService
{
    /// <summary>
    ///   Gets the list of all available themes.
    /// </summary>
    /// <param name="forceRefresh">
    ///   If <c>true</c>, forces a re-read of the themes directory.
    ///   Otherwise, returns cached results for up to 10 minutes.
    /// </param>
    /// <returns>
    ///   The collection of available themes.
    /// </returns>
    IReadOnlyList<IWebThemeDefinition> GetThemes(bool forceRefresh = false);

    /// <summary>
    ///   Gets a specific theme by its ID.
    /// </summary>
    /// <param name="themeId">The theme identifier.</param>
    /// <param name="forceRefresh">
    ///   If <c>true</c>, forces a re-read of the themes directory before
    ///   looking up the theme.
    /// </param>
    /// <returns>
    ///   The theme, or <c>null</c> if not found.
    /// </returns>
    IWebThemeDefinition? GetTheme(string themeId, bool forceRefresh = false);

    /// <summary>
    ///   Removes a theme from the themes directory.
    /// </summary>
    /// <param name="theme">The theme to remove.</param>
    /// <returns>
    ///   <c>true</c> if the theme was removed; <c>false</c> if not found.
    /// </returns>
    bool RemoveTheme(IWebThemeDefinition theme);

    /// <summary>
    ///   Updates an existing theme by fetching the latest definition from its
    ///   <see cref="IWebThemeDefinition.UpdateUrl"/>, including both the JSON definition
    ///   and CSS file.
    /// </summary>
    /// <param name="theme">The theme to update.</param>
    /// <param name="preview">
    ///   If <c>true</c>, validates and returns the updated theme without
    ///   persisting it.
    /// </param>
    /// <param name="cancellationToken">
    ///   A token to cancel the operation.
    /// </param>
    /// <returns>
    ///   The updated theme definition.
    /// </returns>
    /// <exception cref="ValidationException">
    ///   Thrown when the theme has no update URL, the new version is lower
    ///   than the current one, or the response is invalid.
    /// </exception>
    /// <exception cref="HttpRequestException">
    ///   Thrown when the update URL or CSS URL cannot be reached.
    /// </exception>
    Task<IWebThemeDefinition> UpdateThemeOnline(IWebThemeDefinition theme, bool preview = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs a new theme by downloading its JSON definition from a URL.
    /// </summary>
    /// <param name="url">The URL of the theme definition file.</param>
    /// <param name="preview">
    ///   If <c>true</c>, validates and returns the theme without
    ///   persisting it.
    /// </param>
    /// <param name="cancellationToken">
    ///   A token to cancel the operation.
    /// </param>
    /// <returns>
    ///   The installed or previewed theme definition.
    /// </returns>
    /// <exception cref="ValidationException">
    ///   Thrown when the URL is invalid or the theme format is incorrect.
    /// </exception>
    /// <exception cref="HttpRequestException">
    ///   Thrown when the URL cannot be reached.
    /// </exception>
    Task<IWebThemeDefinition> InstallThemeFromUrl(string url, bool preview = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs or updates a theme from a data object.
    /// </summary>
    /// <param name="data">
    ///   The pre-parsed data for the theme definition.
    /// </param>
    /// <param name="fileName">
    ///   The file name (without extension) used as the theme ID.
    /// </param>
    /// <param name="preview">
    ///   If <c>true</c>, validates and returns the theme without
    ///   persisting it.
    /// </param>
    /// <param name="cancellationToken">
    ///   A token to cancel the operation.
    /// </param>
    /// <returns>
    ///   The installed or previewed theme definition.
    /// </returns>
    /// <exception cref="ValidationException">
    ///   Thrown when the file name or theme format is invalid.
    /// </exception>
    Task<IWebThemeDefinition> InstallOrUpdateThemeFromData(WebThemeDefinitionData data, string? fileName = null, bool preview = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs or updates a theme from a JSON string.
    /// </summary>
    /// <param name="content">
    ///   The JSON content of the theme definition.
    /// </param>
    /// <param name="fileName">
    ///   The file name (without extension) used as the theme ID.
    /// </param>
    /// <param name="preview">
    ///   If <c>true</c>, validates and returns the theme without
    ///   persisting it.
    /// </param>
    /// <param name="cancellationToken">
    ///   A token to cancel the operation.
    /// </param>
    /// <returns>
    ///   The installed or previewed theme definition.
    /// </returns>
    /// <exception cref="ValidationException">
    ///   Thrown when the file name or theme format is invalid.
    /// </exception>
    Task<IWebThemeDefinition> InstallOrUpdateThemeFromJson(string content, string fileName, bool preview = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Creates or updates a theme from raw CSS content.
    ///   Creates a minimal theme definition containing only CSS content.
    /// </summary>
    /// <param name="content">The CSS content for the theme.</param>
    /// <param name="fileName">
    ///   The file name (without extension) used as the theme ID.
    /// </param>
    /// <param name="preview">
    ///   If <c>true</c>, validates and returns the theme without
    ///   persisting it.
    /// </param>
    /// <param name="cancellationToken">
    ///   A token to cancel the operation.
    /// </param>
    /// <returns>
    ///   The created or previewed theme definition.
    /// </returns>
    /// <exception cref="ValidationException">
    ///   Thrown when the file name is invalid or content is empty.
    /// </exception>
    Task<IWebThemeDefinition> CreateOrUpdateThemeFromCss(string content, string fileName, bool preview = false, CancellationToken cancellationToken = default);
}
