using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.SampleRenamer
{
    public class SampleRenamer : IRenamer
    {
        // Be careful when using Nuget (NLog had to be installed for this project).
        // Shoko already has and configures NLog, so it's safe to use, but other things may not be
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static SampleSettings Settings { get; set; }

        // Gets the current filename of the DLL (simplified)
        // Resolves to "Shoko.Plugin.SampleRenamer"
        public string Name => Assembly.GetExecutingAssembly().GetName().Name;

        public void Load()
        {
            // ignore. We are a renamer
        }

        public void OnSettingsLoaded(IPluginSettings settings)
        {
            // Save this for later.
            Settings = settings as SampleSettings;
        }

        public void GetFilename(RenameEventArgs args)
        {
            try
            {
                // The question marks everywhere are called Null Coalescence. It's a shorthand for checking if things exist.

                // Technically, there can be more than one episode, series, and group (https://anidb.net/episode/129141).
                // essentially always, there will be only one.
                
                // get the release group
                string release = args.FileInfo.AniDBFileInfo.ReleaseGroup.Name;
                Logger.Info($"Release Group: {release}");

                // get the anime info
                IAnime animeInfo = args.AnimeInfo.FirstOrDefault();

                // get the romaji title
                string animeName = animeInfo?.Titles
                    .FirstOrDefault(a => a.Language == "x-jat" && a.Type == TitleType.Main)?.Title;

                string titles = string.Join(" | ",
                    animeInfo?.Titles.Select(a => $"Type: {a.Type} Language: {a.Language} Title: {a.Title}"));
                Logger.Info(titles);

                // Filenames must be consistent (because OCD), so cancel and return if we can't make a consistent filename style
                if (string.IsNullOrEmpty(animeName))
                {
                    args.Cancel = true;
                    return;
                }
                Logger.Info($"AnimeName: {animeName}");

                // Get the episode info
                IEpisode episodeInfo = args.EpisodeInfo.First();

                string paddedEpisodeNumber = episodeInfo.Number.PadZeroes(animeInfo.EpisodeCount);
                Logger.Info($"Padded Episode Number: {paddedEpisodeNumber}");

                // get the info about the video stream from the MediaInfo
                IVideoStream videoInfo = args.FileInfo.MediaInfo.Video;

                // Get the extension of the original filename, it includes the .
                string ext = Path.GetExtension(args.FileInfo.Filename);

                // The $ allows building a string with the squiggle brackets
                // build a string like "Boku no Hero Academia - 04 [720p HEVC].mkv"
                string result = $"[{release}] {animeName} - {paddedEpisodeNumber} [{videoInfo.Height}p {videoInfo.Codec}]{ext}";

                // Use the Setting ApplyPrefix and Prefix to determine if we should apply a prefix
                if (Settings.ApplyPrefix && !string.IsNullOrEmpty(Settings.Prefix)) result = Settings.Prefix + result;
                result = result.ReplaceInvalidPathCharacters();

                // Set the result
                args.Result = result;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Unable to get new filename for {args.FileInfo?.Filename}");
            }
        }

        public void GetDestination(MoveEventArgs args)
        {
            try
            {
                // Note: ReplaceInvalidPathCharacters() replaces things like slashes, pluses, etc with Unicode that looks similar

                // Get the first available import folder that is a drop destination
                args.DestinationImportFolder =
                    args.AvailableFolders.First(a => a.DropFolderType.HasFlag(DropFolderType.Destination));

                // Get a group name.
                string groupName = args.GroupInfo.First().Name.ReplaceInvalidPathCharacters();
                Logger.Info($"SeriesName: {groupName}");

                // There are very few cases where no x-jat main (romaji) title is available, but it happens.
                string seriesNameWithFallback = (args.AnimeInfo.First().Titles.FirstOrDefault(a => a.Language == "x-jat" && a.Type == TitleType.Main)
                    ?.Title ?? args.AnimeInfo.First().Titles.First().Title).ReplaceInvalidPathCharacters();
                Logger.Info($"SeriesName: {seriesNameWithFallback}");

                // Use Path.Combine to form subdirectories with the slashes and whatnot handled for you.
                args.DestinationPath = Path.Combine(groupName, seriesNameWithFallback);
            }
            catch (Exception e)
            {
                // Log the error to Server
                Logger.Error(e, $"Unable to get destination for {args.FileInfo?.Filename}");
            }
        }
    }
}