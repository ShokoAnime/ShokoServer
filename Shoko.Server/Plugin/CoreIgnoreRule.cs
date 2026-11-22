using System.IO;
using System.Linq;
using Shoko.Abstractions.FileSystem;
using Shoko.Abstractions.Video;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

public class CoreIgnoreRule(ISettingsProvider settingsProvider) : IManagedFolderIgnoreRule
{
    public string Name { get; } = "Built-In Ignore Rule";

    public bool ShouldIgnore(IManagedFolder folder, FileSystemEntry entry)
    {
        var exclusions = settingsProvider.GetSettings().Import.ExcludeExpressions;
        var path = entry.Path;
        if (entry.IsDirectory && !Path.EndsInDirectorySeparator(path))
            path += Path.DirectorySeparatorChar;

        return exclusions.Any(r => r.IsMatch(path));
    }
}
