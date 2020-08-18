using System;
using System.IO;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.SampleRenamer
{
    public class SampleRenamer : IRenamer
    {
        private static SampleSettings Settings { get; set; }
        public string Name => "Sample Renamer";
        public void Load()
        {
            // Do Stuff
        }

        public void OnSettingsLoaded(IPluginSettings settings)
        {
            Settings = settings as SampleSettings;
        }

        public void GetFilename(RenameEventArgs args)
        {
            string animeName = args.AnimeInfo.FirstOrDefault()?.Titles
                .FirstOrDefault(a => a.Language.Equals("x-jat") && a.Type == TitleType.Main)?.Title;
            if (string.IsNullOrEmpty(animeName)) args.Result = null;

            string ext = Path.GetExtension(args.FileInfo.Filename);
            string result =
                $"{animeName} - {args.EpisodeInfo.FirstOrDefault()?.Number} [{args.FileInfo.MediaInfo.Video.Height}p {args.FileInfo.MediaInfo.Video.CodecID}].{ext}";

            if (Settings.ApplyPrefix) result = Settings.Prefix + result;
            result = PluginUtilities.ReplaceInvalidPathCharacters(result);
            args.Result = result;
        }

        public void GetDestination(MoveEventArgs args)
        {
            // Leave it blank to defer to the next plugin
        }
    }
}