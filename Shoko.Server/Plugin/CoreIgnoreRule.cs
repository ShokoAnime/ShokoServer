using System.IO;
using System.Linq;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

public class CoreIgnoreRule(ISettingsProvider settingsProvider) : IManagedFolderIgnoreRule
{
    public string Name { get; } = "Built-In Ignore Rule";

    public bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileInfo)
    {
        if (fileInfo is null) return false;
        var exclusions = settingsProvider.GetSettings().Import.ExcludeExpressions;
        if (exclusions.Count == 0) return false;

        var path = fileInfo.FullName;
        if (exclusions.Any(r => r.IsMatch(path)))
            return true;

        // Scans hand directories to ignore rules so that answering true prunes the whole
        // subtree, and the documented patterns are written with a trailing separator
        // (see the defaults: `[\\/]\.Trash-\d+[\\/]`). A directory's FullName never ends
        // with one, so without this second check an excluded directory itself would never
        // match and the walk would still descend into it, only pruning one level deeper.
        if (fileInfo is not DirectoryInfo || Path.EndsInDirectorySeparator(path))
            return false;

        return exclusions.Any(r => r.IsMatch(path + Path.DirectorySeparatorChar));
    }
}
