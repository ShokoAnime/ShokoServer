using System.ComponentModel;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.Server;

namespace Shoko.Server.Settings;

public class TraktSettings
{
    public bool Enabled { get; set; } = false;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string AuthToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.Hidden)]
    [PasswordPropertyText]
    public string RefreshToken { get; set; } = string.Empty;

    [Visibility(DisplayVisibility.ReadOnly)]
    public string TokenExpirationDate { get; set; } = string.Empty;

    public ScheduledUpdateFrequency SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;
}
