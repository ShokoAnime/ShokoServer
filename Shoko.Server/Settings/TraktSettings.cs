using System.ComponentModel;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

public class TraktSettings
{
    public bool Enabled { get; set; } = false;

    public bool AutoLink { get; set; } = false;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string AuthToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string RefreshToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.ReadOnly)]
    public string TokenExpirationDate { get; set; } = string.Empty;

    public ScheduledUpdateFrequency UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

    public ScheduledUpdateFrequency SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

    public bool VipStatus { get; set; } = false;
}
