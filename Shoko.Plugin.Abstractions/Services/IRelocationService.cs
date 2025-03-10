using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Relocation service.
/// </summary>
public interface IRelocationService
{
    /// <summary>
    /// Get the first destination with enough space for the given file, if any.
    /// </summary>
    /// <param name="args">The relocation event args.</param>
    /// <returns></returns>
    IManagedFolder? GetFirstDestinationWithSpace(RelocationEventArgs args);

    /// <summary>
    /// Check if the given managed folder has enough space for the given file.
    /// </summary>
    /// <param name="folder">The managed folder.</param>
    /// <param name="file">The file</param>
    /// <returns></returns>
    bool ManagedFolderHasSpace(IManagedFolder folder, IVideoFile file);

    /// <summary>
    /// Get the location of the folder that contains a file for the latest (airdate) episode in the current collection.
    /// </summary>
    /// <remarks>
    /// Will only look for files in managed folders of type <see cref="DropFolderType.Excluded"/> or <see cref="DropFolderType.Destination"/>.
    /// </remarks>
    /// <param name="args">The relocation event args.</param>
    /// <returns></returns>
    public (IManagedFolder ManagedFolder, string RelativePath)? GetExistingSeriesLocationWithSpace(RelocationEventArgs args);
}
