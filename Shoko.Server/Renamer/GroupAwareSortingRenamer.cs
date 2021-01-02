using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.Renamer
{
    // TODO make this support renaming, but only if enabled
    [Renamer("GroupAwareRenamer", Description = "Group Aware Sorter (does not support renaming, only moving at this time)")]
    public class GroupAwareRenamer : IRenamer
    {
        public string GetFileName(SVR_VideoLocal_Place video) => Path.GetFileName(video.FilePath);

        public string GetFileName(SVR_VideoLocal video)
        {
            return Path.GetFileName(video.GetBestVideoLocalPlace().FilePath);
        }

        public (ImportFolder dest, string folder) GetDestinationFolder(SVR_VideoLocal_Place video)
        {
            try
            {
                SVR_AnimeSeries series = video.VideoLocal?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();

                if (series == null) return (null, "Series is null");
                string name = Utils.ReplaceInvalidFolderNameCharacters(series.GetSeriesName());
                if (string.IsNullOrEmpty(name)) return (null, "Unable to get series name");

                var anime = series.GetAnime();
                ImportFolder destFolder = RepoFactory.ImportFolder.GetAll()
                    .FirstOrDefault(a => a.ImportFolderLocation.Contains("Anime"));
                if (anime.Restricted == 1)
                {
                    destFolder = RepoFactory.ImportFolder.GetAll()
                        .FirstOrDefault(a => a.ImportFolderLocation.Contains("Hentai"));
                }

                if (destFolder == null)
                    destFolder = RepoFactory.ImportFolder.GetAll().FirstOrDefault(a => a.FolderIsDropDestination) ??
                                 RepoFactory.ImportFolder.GetAll().FirstOrDefault();

                string path;
                var group = series.AnimeGroup;
                if (group == null) return (null, "group is null");
                if (group.GetAllSeries().Count == 1)
                {
                    path = name;
                }
                else
                {
                    var groupName = Utils.ReplaceInvalidFolderNameCharacters(group.GroupName);
                    path = Path.Combine(groupName, name);
                }

                return (destFolder, path);
            }
            catch
            {
                return (null, "ERROR");
            }
        }
    }
}