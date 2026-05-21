using System;
using System.IO;
using System.Threading;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Utilities;

public static partial class Utils
{
    private static readonly AsyncLocal<ISettingsProvider?> s_settingsProviderOverride = new();

    private static IServiceProvider? _serviceContainer;

    public static IServiceProvider ServiceContainer
    {
        get => _serviceContainer!;
        set => _serviceContainer = value;
    }

    private static ISettingsProvider? _settingsProvider;

    public static ISettingsProvider SettingsProvider
    {
        get => s_settingsProviderOverride.Value ?? _settingsProvider!;
        set => _settingsProvider = value;
    }

    internal static IDisposable PushSettingsProviderOverride(ISettingsProvider settingsProvider)
        => new ScopedOverride<ISettingsProvider?>(s_settingsProviderOverride, settingsProvider);

    public static string GetDistinctPath(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        return string.IsNullOrEmpty(parent) ? fullPath : Path.Combine(Path.GetFileName(parent), Path.GetFileName(fullPath));
    }

    private sealed class ScopedOverride<T> : IDisposable
    {
        private readonly AsyncLocal<T?> _slot;
        private readonly T? _previousValue;
        private bool _disposed;

        public ScopedOverride(AsyncLocal<T?> slot, T value)
        {
            _slot = slot;
            _previousValue = slot.Value;
            _slot.Value = value;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _slot.Value = _previousValue;
            _disposed = true;
        }
    }
}
