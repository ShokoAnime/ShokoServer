using Shoko.Server.Settings.Attributes;

namespace Shoko.Server.Settings;

public class LogRotatorSettings
{
    [EnvironmentConfig("LOG_ENABLED")]
    public bool Enabled { get; set; } = true;

    [EnvironmentConfig("LOG_ZIP")]
    public bool Zip { get; set; } = true;

    [EnvironmentConfig("LOG_DELETE")]
    public bool Delete { get; set; } = true;

    [EnvironmentConfig("LOG_DELETE_DAYS")]
    public string Delete_Days { get; set; } = "";
}
