using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Connectivity;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Connectivity.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// A concrete connectivity monitor definition used for WAN availability checks.
/// </summary>
public class ConnectivityMonitorDefinition : IConnectivityMonitor
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("New Monitor")]
    public string Key
    {
        get => !string.IsNullOrWhiteSpace(Name) ? $"{Name} ({Type}: {Address})" : "New Monitor";
        set { }
    }

    /// <inheritdoc/>
    [Display(Name = "Monitor Name")]
    [DefaultValue("")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc/>
    [Display(Name = "Check Type")]
    [DefaultValue(ConnectivityCheckType.Head)]
    public ConnectivityCheckType Type { get; set; }

    /// <inheritdoc/>
    [Display(Name = "URL")]
    [DefaultValue("")]
    [Url]
    [Required]
    public string Address { get; set; } = string.Empty;
}
