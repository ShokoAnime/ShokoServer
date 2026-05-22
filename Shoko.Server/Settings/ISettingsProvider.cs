using System;

#nullable enable
namespace Shoko.Server.Settings;

public interface ISettingsProvider
{
    private static ISettingsProvider? _staticInstance = null;

    /// <summary>
    ///   Get or set the static service provider. DO NOT USE UNLESS ABSOLUTELY
    ///   NECESSARY.
    /// </summary>
    /// <remarks>
    ///   Will be set during startup. DO NOT SET MANUALLY.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   The service provider has already been set.
    /// </exception>
    public static ISettingsProvider Instance
    {
        get => _staticInstance ?? throw new InvalidOperationException("The service provider has not been set.");
        set => _staticInstance = value;
    }

    IServerSettings GetSettings(bool copy = false);
    void SaveSettings(IServerSettings settings);
    void SaveSettings();
    void DebugSettingsToLog();
}
