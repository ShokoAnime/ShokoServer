using System.Linq;


namespace JMMModels.Extensions
{
    public static class AnimeGroupExtensions
    {
        public static bool HasMissingEpisodesAny(this AnimeGroup grp)
        {
            return (grp.MissingEpisodeCount > 0 || grp.MissingEpisodeCountGroups > 0);
        }

        public static bool HasMissingEpisodesGroups(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCountGroups > 0;
        }

        public static bool HasMissingEpisodes(this AnimeGroup grp)
        {
            return grp.MissingEpisodeCountGroups > 0;
        }

        public static bool HasTagId(this AnimeGroup grp, string tagid)
        {
            return grp.Tags.Any(a => a.TagId == tagid);
        }

        public static bool HasTagName(this AnimeGroup grp, string tagname)
        {
            tagname = tagname.ToLowerInvariant();
            return grp.Tags.Any(a => a.Name.ToLowerInvariant() == tagname);
        }

        public static bool HasCustomTagId(this AnimeGroup grp, string tagid)
        {
            return grp.CustomTags.Any(a => a.TagId == tagid);
        }
        public static bool HasCustomTagName(this AnimeGroup grp, string tagname)
        {
            tagname = tagname.ToLowerInvariant();
            return grp.CustomTags.Any(a => a.Name.ToLowerInvariant() == tagname);
        }

        public static bool HasAniDBType(this AnimeGroup grp, string name)
        {
            name = name.ToLowerInvariant();
            return grp.AniDB_Types.Any(a=>a.ToText().ToLowerInvariant()==name);
        }

        public static bool HasAudioLanguage(this AnimeGroup grp, string lang)
        {
            lang = lang.ToLowerInvariant();
            return grp.Languages.Any(a=>a.ToLowerInvariant()==lang);           
        }
        public static bool HasSubtitleLanguage(this AnimeGroup grp, string lang)
        {
            lang = lang.ToLowerInvariant();
            return grp.Subtitles.Any(a => a.ToLowerInvariant() == lang);
        }

        public static bool HasVideoQuality(this AnimeGroup grp, string quality)
        {
            quality = quality.ToLowerInvariant();
            return grp.AvailableVideoQualities.Any(a => a.ToLowerInvariant() == quality);

        }
        public static bool HasReleaseQuality(this AnimeGroup grp, string quality)
        {
            quality = quality.ToLowerInvariant();
            return grp.AvailableReleaseQualities.Any(a => a.ToLowerInvariant() == quality);

        }

        public static float Rating(this AnimeGroup grp)
        {
            return grp.SumSeriesRating/grp.CountSeriesRating;
        }

        public static float TempRating(this AnimeGroup grp)
        {
            return grp.SumSeriesTempRating/grp.CountSeriesRating;
        }


    }
}
