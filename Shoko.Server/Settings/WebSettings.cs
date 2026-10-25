using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

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
    ///   The origins web pages may call the API from, such as a web client
    ///   served from another host, as <c>http://host:port</c>. Empty, or
    ///   <c>*</c>, allows any origin. The Web UI the server serves itself
    ///   never needs listing.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Allowed Origins")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_CORS_ORIGINS")]
    [List(UniqueItems = true)]
    public List<string> AllowedOrigins { get; set; } = [];

    /// <summary>
    ///   Enable the built-in SignalR hubs available at <c>/signalr</c>.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Enable SignalR")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_API_SIGNALR_ENABLED")]
    [DefaultValue(true)]
    public bool EnableSignalR { get; set; } = true;

    /// <summary>
    /// Enable the API v2 endpoints.
    /// </summary>
    [Badge("Deprecated", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Enable API v2")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_API_V2_ENABLED")]
    [DefaultValue(true)]
    public bool EnableAPIv2 { get; set; } = true;

    /// <summary>
    /// Enable the API v3 endpoints.
    /// </summary>
    [Display(Name = "Enable API v3")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_API_V3_ENABLED")]
    [DefaultValue(true)]
    public bool EnableAPIv3 { get; set; } = true;

    /// <summary>
    /// Allow anonymous file streaming in API v3.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Allow Anonymous File Streaming in API v3")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_API_V3_ALLOW_ANONYMOUS_FILE_STREAMING")]
    [DefaultValue(false)]
    public bool AllowAnonymousFileStreamingInAPIv3 { get; set; } = false;

    /// <summary>
    /// Always use the developer exceptions page, even in production.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Always Use Developer Exceptions")]
    [RequiresRestart]
    [EnvironmentVariable("SHOKO_WEB_DEVELOPER_EXCEPTIONS")]
    [DefaultValue(false)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool AlwaysUseDeveloperExceptions { get; set; } = false;

    /// <summary>
    /// The manifest URL for the Web UI component updates.
    /// </summary>
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Client Manifest URL")]
    [EnvironmentVariable("SHOKO_CLIENT_MANIFEST_URL")]
    [DefaultValue("https://raw.githubusercontent.com/ShokoAnime/Shoko-WebUI/metadata/manifest.json")]
    public string ClientManifestUrl { get; set; } = "https://raw.githubusercontent.com/ShokoAnime/Shoko-WebUI/metadata/manifest.json";

    /// <summary>
    /// The manifest URL for the server updates.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true, Size = DisplayElementSize.Large)]
    [Display(Name = "Server Manifest URL")]
    [EnvironmentVariable("SHOKO_SERVER_MANIFEST_URL")]
    [DefaultValue("https://raw.githubusercontent.com/ShokoAnime/ShokoServer/metadata/manifest.json")]
    public string ServerManifestUrl { get; set; } = "https://raw.githubusercontent.com/ShokoAnime/ShokoServer/metadata/manifest.json";

    /// <summary>
    /// Throttling of failed authentication attempts, shared by the core and every plugin.
    /// </summary>
    [Visibility(Advanced = true)]
    [Display(Name = "Authentication Throttling")]
    public AuthenticationThrottleSettings AuthenticationThrottle { get; set; } = new();
}
