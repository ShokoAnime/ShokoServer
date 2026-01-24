using Shoko.Models.Enums;

namespace Shoko.Server.Settings;

public class TraktSettings
{
    public bool Enabled { get; set; } = false;

    public string AuthToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string TokenExpirationDate { get; set; } = string.Empty;

    public ScheduledUpdateFrequency SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;
}
