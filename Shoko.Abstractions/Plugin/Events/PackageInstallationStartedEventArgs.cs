
namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when the package installation process is started.
/// </summary>
public class PackageInstallationStartedEventArgs : PackageInstallationEventArgs
{
    /// <summary>
    ///   Gets or sets a value indicating whether the event should be canceled.
    /// </summary>
    public bool Cancel { get; set; }
}
