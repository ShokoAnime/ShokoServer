using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

public interface IRelocationService
{
    /// <summary>
    /// Get the first destination with enough space for the given file, if any
    /// </summary>
    /// <param name="args">The relocation event args</param>
    /// <returns></returns>
    IImportFolder? GetFirstDestinationWithSpace(RelocationEventArgs args);

    /// <summary>
    /// Check if the given import folder has enough space for the given file.
    /// </summary>
    /// <param name="folder">The import folder</param>
    /// <param name="file">The file</param>
    /// <returns></returns>
    bool ImportFolderHasSpace(IImportFolder folder, IVideoFile file);
}
