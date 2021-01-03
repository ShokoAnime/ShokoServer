using System.IO;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;
using INewRenamer = Shoko.Plugin.Abstractions.IRenamer;

namespace Shoko.Server.Renamer
{
    // TODO make this support renaming, but only if enabled
    [Renamer("GroupAwareRenamer", Description = "Group Aware Sorter (does not support renaming, only moving at this time)")]
    public class GroupAwareRenamer : INewRenamer
    {
        public string GetFilename(RenameEventArgs args) => args.FileInfo.Filename;

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            try
            {
                SVR_AnimeSeries series = video.VideoLocal?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();

                if (series == null) return (null, "Series is null");
                string name = Utils.ReplaceInvalidFolderNameCharacters(series.GetSeriesName());
                if (string.IsNullOrEmpty(name)) return (null, "Unable to get series name");

                var anime = series.GetAnime();
                IImportFolder destFolder = args.AvailableFolders
                    .FirstOrDefault(a => a.Location.Contains("Anime"));
                if (anime.Restricted == 1)
                {
                    destFolder = args.AvailableFolders
                        .FirstOrDefault(a => a.Location.Contains("Hentai"));
                }

                if (destFolder == null)
                    destFolder = args.AvailableFolders.FirstOrDefault(a => a.DropFolderType.HasFlag(DropFolderType.Destination)) ??
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
                throw;
            }
        }
    }
}