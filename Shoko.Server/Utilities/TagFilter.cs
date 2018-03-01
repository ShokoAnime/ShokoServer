using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server
{
    public static class TagFilter
    {
        // ported from python

        public static readonly HashSet<string> TagBlacklistAniDBHelpers = new HashSet<string>
        {
            // AniDB tags that don't help with anything
            "body and host",
            "breasts",
            "broadcast cropped to 4-3",
            "cast missing",
            "cast",
            "content indicators",
            "delayed 16-9 broadcast",
            "description missing",
            "development hell", // :( God Eater
            "dialogue driven", // anidb and their british spellings
            "dynamic",
            "earth",
            "elements",
            "ending",
            "ensemble cast",
            "family life",
            "fast-paced",
            "fictional world",
            "full hd version available",
            "jdrama adaptation",
            "meta tags",
            "motifs",
            "multi-anime projects",
            "noitamina",
            "origin",
            "past",
            "place",
            "present",
            "season",
            "sentai",
            "setting",
            "some weird shit goin' on", // these are some grave accents in use...
            "storytelling",
            "tales",
            "target audience",
            "technical aspects",
            "television programme",
            "themes",
            "time",
            "translation convention",
            "tropes",
            "ungrouped",
            "unsorted"
        };

        public static readonly HashSet<string> TagBlackListSource = new HashSet<string>
        {
            // tags containing the source of series
            "4-koma",
            "action game",
            "erotic game",
            "fan-made",
            "game",
            "korean drama",
            "manga",
            "manhua",
            "manhwa",
            "movie",
            "new",
            "novel",
            "radio programme",
            "remake",
            "rpg",
            "television programme",
            "ultra jump",
            "visual novel"
        };

        public static readonly HashSet<string> TagBlackListArtStyle = new HashSet<string>
        {
            // tags that focus on art style
            "3d cg animation",
            "3d cg closing",
            "cel-shaded animation",
            "cgi",
            "chibi ed",
            "experimental animation",
            "flash animation",
            "live-action closing",
            "live-action imagery",
            "off-model animation",
            "photographic backgrounds",
            "product placement",
            "recycled animation",
            "slide show animation",
            "thick line animation",
            "vignette scenes",
            "watercolour style",
            "widescreen transition"
        };

        public static readonly HashSet<string> TagBlackListUsefulHelpers = new HashSet<string>
        {
            // tags that focus on episode attributes
            "ed variety",
            "half-length episodes",
            "long episodes",
            "multi-segment episodes",
            "op and ed sung by characters",
            "op variety",
            "post-credits scene",
            "recap in opening",
            "short episodes",
            "short movie",
            "stand-alone movie",
            "subtle op ed sequence change"
        };

        public static readonly HashSet<string> TagBlackListPlotSpoilers = new HashSet<string>
        {
            // tags that could contain story-line spoilers
            "branching story",
            "cliffhangers",
            "colour coded",
            "complex storyline",
            "drastic change in sequel",
            "fillers",
            "first girl wins", // seriously a spoiler
            "incomplete story",
            "inconclusive",
            "inconclusive romantic plot",
            "non-linear",
            "open-ended",
            "room for sequel",
            "sudden change of pace",
            "tone changes",
            "unresolved",
            "unresolved romance"
        };

        [Flags]
        public enum Filter : byte
        {
            AnidbInternal = 1 << 0,
            ArtStyle      = 1 << 1,
            Source        = 1 << 2,
            Misc          = 1 << 3,
            Plot   = 1 << 4,
        }


        /// <summary>
        /// Filters tags based on settings specified in flags
        ///        0b00001 : Hide AniDB Internal Tags
        ///        0b00010 : Hide Art Style Tags
        ///        0b00100 : Hide Source TransactionHelper.Work Tags
        ///        0b01000 : Hide Useful Miscellaneous Tags
        ///        0b10000 : Hide Plot Spoiler Tags
        /// </summary>
        /// <param name="strings">A list of strings [ "meta tags", "elements", "comedy" ]</param>
        /// <param name="flags">the <see cref="TagFilter.Filter"/> flags</param>
        /// <returns>the original list with items removed based on rules provided</returns>
        public static List<string> ProcessTags(Filter flags, List<string> strings, bool addOriginal = true)
        {
            if (strings.Count == 0) return strings;

            bool readdOriginal = true;

            if (strings.Count == 1)
            {
                if (IsTagBlackListed(strings[0], flags, ref readdOriginal)) strings.Clear();
                return strings;
            }

            List<string> toRemove = new List<string>((int)Math.Ceiling(strings.Count / 2D));

            var stringsSet = new HashSet<string>(strings);
            strings.AsParallel().ForAll(a => MarkTagsForRemoval(a, flags, ref toRemove, ref readdOriginal));

            foreach (var a in toRemove) if (stringsSet.Contains(a)) strings.Remove(a);

            if (readdOriginal && addOriginal) strings.Add("Original Work");

            return strings;
        }

        private static void MarkTagsForRemoval(string a, Filter flags, ref List<string> toRemove, ref bool readdOriginal)
        {
            if (IsTagBlackListed(a, flags, ref readdOriginal))
            {
                lock (toRemove)
                {
                    toRemove.Add(a);
                }
            }
        }

        /// <summary>
        /// Filters tags based on settings specified in flags
        ///        0b00001 : Hide AniDB Internal Tags
        ///        0b00010 : Hide Art Style Tags
        ///        0b00100 : Hide Source TransactionHelper.Work Tags
        ///        0b01000 : Hide Useful Miscellaneous Tags
        ///        0b10000 : Hide Plot Spoiler Tags
        /// </summary>
        /// <param name="a">the tag to check</param>
        /// <param name="flags">the <see cref="TagFilter.Filter"/> flags</param>
        /// <returns>true if the tag would be removed</returns>
        public static bool IsTagBlackListed(string a, Filter flags, ref bool readdOriginal)
        {
            string tag = a.Trim().ToLowerInvariant();
            if (flags.HasFlag(Filter.ArtStyle))
            {
                if (TagBlackListArtStyle.Contains(tag)) return true;

                if (tag.Contains("censor")) return true;
            }

            if (flags.HasFlag(Filter.Source)) // if source excluded
            {
                readdOriginal = false;
                if ("original work".Equals(tag)) return true;

                if (TagBlackListSource.Contains(tag)) return true;
            }
            else
            {
                if ("original work".Equals(tag)) return false;

                if (TagBlackListSource.Contains(tag))
                {
                    readdOriginal = false;
                    return false;
                }
            }

            if (flags.HasFlag(Filter.Misc))
            {
                if (tag.StartsWith("preview")) return true;

                if (TagBlackListUsefulHelpers.Contains(tag)) return true;
            }

            if (flags.HasFlag(Filter.Plot))
            {
                if (tag.StartsWith("plot") || tag.EndsWith(" dies") || tag.EndsWith(" end") ||
                    tag.EndsWith(" ending")) return true;

                if (TagBlackListPlotSpoilers.Contains(tag)) return true;
            }

            if (flags.HasFlag(Filter.AnidbInternal))
            {
                if (TagBlacklistAniDBHelpers.Contains(tag)) return true;

                if (tag.StartsWith("predominantly")) return true;

                if (tag.StartsWith("weekly")) return true;

                if (tag.Contains("to be") || tag.Contains("need") || tag.Contains("needs"))
                {
                    if (tag.EndsWith("improved") || tag.EndsWith("improving") || tag.EndsWith("improvement")) return true;

                    if (tag.EndsWith("merging") || tag.EndsWith("merged")) return true;

                    if (tag.EndsWith("deleting") || tag.EndsWith("deleted")) return true;

                    if (tag.EndsWith("moving") || tag.EndsWith("moved")) return true;

                    // contains is slower, so try the others first
                    if (tag.Contains("split")) return true;
                }

                if (tag.Contains("old animetags")) return true;

                if (tag.Contains("missing")) return true;
            }

            return false;
        }
    }
}