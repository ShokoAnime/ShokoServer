using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Web.Enums;

/// <summary>
///   The pages of a web UI other applications may link to. Each type
///   names the placeholders its template may use.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum WebUILinkType : byte
{
    /// <summary>
    ///   The web UI's front page. No placeholders.
    /// </summary>
    Home = 1,

    /// <summary>
    ///   The administration of the server, such as its settings, plugins and
    ///   users. No placeholders.
    /// </summary>
    Administration = 2,

    /// <summary>
    ///   A group. Placeholder: <c>{groupID}</c>.
    /// </summary>
    Group = 3,

    /// <summary>
    ///   A series. Placeholder: <c>{seriesID}</c>.
    /// </summary>
    Series = 4,

    /// <summary>
    ///   An episode. Placeholders: <c>{episodeID}</c> and <c>{seriesID}</c>.
    /// </summary>
    Episode = 5,

    /// <summary>
    ///   A video. Placeholder: <c>{videoID}</c>.
    /// </summary>
    Video = 6,

    /// <summary>
    ///   The settings page of a configuration, such as a plugin's.
    ///   Placeholder: <c>{configurationID}</c>.
    /// </summary>
    Configuration = 7,

    /// <summary>
    ///   A page a plugin offers through <see cref="Plugin.IPlugin.GetPages"/>.
    ///   Placeholders: <c>{pluginID}</c> and <c>{pageID}</c>, the page's
    ///   <see cref="Plugin.Models.LocalPluginPage.ID"/>.
    /// </summary>
    PluginPage = 8,
}
