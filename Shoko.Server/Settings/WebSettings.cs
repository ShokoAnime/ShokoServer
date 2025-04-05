using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Configure settings related to the HTTP(S) hosting.
/// </summary>
public class WebSettings
{
    /// <summary>
    /// The port to listen on.
    /// </summary>
    [Display(Name = "Server Port")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_PORT")]
    [DefaultValue(8111)]
    [Range(1, 65535, ErrorMessage = "Server Port must be between 1 and 65535")]
    public ushort Port { get; set; } = 8111;

    /// <summary>
    /// Automagically replace the current web ui with the included version if
    /// the current version is older then the included version.
    /// </summary>
    [Display(Name = "Auto Replace Web UI With Included Version")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEBUI_AUTO_REPLACE")]
    [DefaultValue(true)]
    public bool AutoReplaceWebUIWithIncluded { get; set; } = true;

    /// <summary>
    /// Enable the Web UI. Disabling this will run the server in "headless"
    /// mode.
    /// </summary>
    [Display(Name = "Enable Web UI")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEBUI_ENABLED")]
    [DefaultValue(true)]
    public bool EnableWebUI { get; set; } = true;

    /// <summary>
    /// The public path prefix for where to mount the Web UI.
    /// </summary>
    [Display(Name = "Web UI Prefix")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEBUI_PREFIX")]
    [DefaultValue("webui")]
    public string WebUIPrefix { get; set; } = "webui";

    /// <summary>
    /// The public path formatted from <see cref="WebUIPrefix"/> for where to
    /// mount the Web UI.
    /// </summary>
    [JsonIgnore]
    public string WebUIPublicPath
    {
        get
        {
            var publicPath = WebUIPrefix;
            if (!publicPath.StartsWith('/'))
                publicPath = $"/{publicPath}";
            if (publicPath.EndsWith('/'))
                publicPath = publicPath[..^1];
            return publicPath;
        }
    }

    /// <summary>
    /// A relative path from the <see cref="IApplicationPaths.ProgramDataPath"/>
    /// to where the Web UI is installed, or an absolute path if you have it
    /// somewhere else.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Full)]
    [Display(Name = "Web UI Path")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEBUI_PATH")]
    [DefaultValue("webui")]
    public string WebUIPath { get; set; } = "webui";

    /// <summary>
    /// Enable the Swagger UI.
    /// </summary>
    [Display(Name = "Enable Swagger UI")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_SWAGGER_ENABLED")]
    [DefaultValue(true)]
    public bool EnableSwaggerUI { get; set; } = true;

    /// <summary>
    /// The public path prefix for where to mount the Swagger UI.
    /// </summary>
    [Display(Name = "Swagger UI Prefix")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_SWAGGER_PREFIX")]
    [DefaultValue("swagger")]
    public string SwaggerUIPrefix { get; set; } = "swagger";

    /// <summary>
    /// Always use the developer exceptions page, even in production.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Always Use Developer Exceptions")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEB_DEVELOPER_EXCEPTIONS")]
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool AlwaysUseDeveloperExceptions { get; set; } = false;
}
