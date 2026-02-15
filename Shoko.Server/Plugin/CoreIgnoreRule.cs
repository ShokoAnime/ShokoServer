
using System.IO;
using System.Linq;
using Shoko.Abstractions.Video;
using Shoko.Server.Settings;

namespace Shoko.Server.Plugin;

public class CoreIgnoreRule(ISettingsProvider settingsProvider) : IManagedFolderIgnoreRule
{
    public string Name { get; } = "Built-In Ignore Rule";

    public bool ShouldIgnore(IManagedFolder folder, FileSystemInfo fileInfo)
    {
        if (fileInfo is not FileInfo) return false;
        var exclusions = settingsProvider.GetSettings().Import.ExcludeExpressions;
        return exclusions.Any(r => r.IsMatch(fileInfo.FullName));
    }
}
