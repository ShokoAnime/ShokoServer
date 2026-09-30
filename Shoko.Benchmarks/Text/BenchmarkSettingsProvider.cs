using Shoko.Server.Settings;

namespace Benchmarks.Text;

/// <summary>
///   Hands out one fixed <see cref="ServerSettings"/>, for code that reads the settings singleton.
/// </summary>
/// <param name="settings">The settings.</param>
public sealed class BenchmarkSettingsProvider(ServerSettings settings) : ISettingsProvider
{
    public IServerSettings GetSettings(bool copy = false) => settings;

    public void SaveSettings(IServerSettings settings) { }

    public void SaveSettings() { }

    public void DebugSettingsToLog() { }
}
