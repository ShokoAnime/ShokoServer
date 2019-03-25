using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server
{
    public static class TagFilter
    {
        public static readonly HashSet<string> TagBlacklistAniDBHelpers = new HashSet<string>
        {
            // AniDB tags that don't help with anything
            "asia",
            "body and host",
            "breasts",
            "cast missing",
            "cast",
            "complete manga adaptation",
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
            "full hd version available",
            "jdrama adaptation",
            "jdrama adaption", // typos really
            "maintenance tags",
            "meta tags",
            "motifs",
            "nevada",
            "origin",
            "place",
            "season",
            "setting",
            "some weird shit goin' on", // these are some grave accents in use...
            "storytelling",
            "tales",
            "target audience",
            "technical aspects",
            "themes",
            "time",
            "translation convention",
            "tropes",
            "ungrouped",
            "unsorted"
        };

        public static readonly HashSet<string> TagBlacklistProgramming = new HashSet<string>
        {
            // Tags that involve how or where it aired, or any awards it got
            "anime no me",
            "animeism",
            "broadcast cropped to 4-3",
            "comicfesta anime zone",
            "crunchyroll anime awards",
            "discontinued", // debating putting this elsewhere
            "jump super anime tour",
            "multi-anime projects",
            "newtype anime award",
            "noitamina",
            "oofuji noburou award",
            "perpetual ongoing",
            "remastered version available",
            "sentai",
            "ultra super anime time",
            "wakate animator ikusei project",
        };

        public static readonly HashSet<string> TagBlacklistSetting = new HashSet<string>
        {
            // Tags that involve the setting, a time or place in which the story occurs.
            // I've seen more that fall under this that AniDB hasn't listed
            "1920s",
            "1960s",
            "africa",
            "akihabara",
            "alternative past",
            "alternative present",
            "americas",
            "ancient rome",
            "australia",
            "autumn",
            "belgium",
            "brazil",
            "canada",
            "casino",
            "chicago",
            "chile",
            "china",
            "circus",
            "cold war",
            "colony dome",
            "countryside",
            "czech republic",
            "desert",
            "dungeon",
            "easter island",
            "egypt",
            "europe",
            "fantasy world",
            "fictional world",
            "fictional location",
            "finland",
            "floating island",
            "france",
            "french revolution",
            "future",
            "germany",
            "han dynasty",
            "hawaii",
            "heaven",
            "hell",
            "high school",
            "hiroshima",
            "historical",
            "hokkaido",
            "hong kong",
            "hospital",
            "ikebukuro",
            "india",
            "island",
            "istanbul",
            "italy",
            "japan",
            "jungle",
            "korea",
            "kyoto",
            "las vegas",
            "london",
            "long time span",
            "mars",
            "medieval",
            "mexico",
            "middle east",
            "middle school",
            "moon",
            "moscow",
            "nagasaki",
            "nara",
            "new york",
            "ocean world",
            "ocean",
            "oceania",
            "okinawa",
            "osaka",
            "other planet",
            "pacific ocean",
            "pakistan",
            "palace",
            "parallel universe",
            "parallel world",
            "paris",
            "past",
            "peru",
            "post-apocalypse",
            "post-apocalyptic",
            "post-war",
            "prague",
            "present",
            "prison planet",
            "prison",
            "real-world location",
            "red-light district",
            "romania",
            "rome",
            "russia",
            "shanghai",
            "shinjuku",
            "shipboard",
            "space colony",
            "space elevator",
            "space",
            "spain",
            "spirit realm",
            "spring",
            "sri lanka",
            "submarine",
            "summer",
            "switzerland",
            "three kingdoms",
            "tibet",
            "tokyo skytree",
            "tokyo tower",
            "tokyo",
            "turkey",
            "underground",
            "underwater",
            "united kingdom",
            "united states",
            "venice",
            "vietnam",
            "virtual world",
            "vladivostok",
            "winter",
            "world war i",
            "world war ii",
            "yokohama",
        };

        public static readonly HashSet<string> TagBlackListSource = new HashSet<string>
        {
            // tags containing the source of series
            "4-koma",
            "4-koma manga",
            "action game",
            "american derived",
            "cartoon",
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
            "original work",
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
            "alternating animation style",
            "cel-shaded animation",
            "cgi",
            "chibi ed",
            "episodic",
            "experimental animation",
            "flash animation",
            "live-action closing",
            "live-action imagery",
            "narration",
            "off-model animation",
            "panels that require pausing",
            "photographic backgrounds",
            "product placement",
            "puppetmation",
            "recycled animation",
            "repeated frames",
            "slide show animation",
            "stereoscopic imaging",
            "thick line animation",
            "vignette scenes",
            "walls of text",
            "watercolour style",
            "widescreen transition"
        };

        public static readonly HashSet<string> TagBlackListUsefulHelpers = new HashSet<string>
        {
            // tags that focus on episode attributes
            "crossover episode",
            "ed variety",
            "half-length episodes",
            "in medias res",
            "long episodes",
            "multi-segment episodes",
            "op and ed sung by characters",
            "op variety",
            "post-credits scene",
            "recap in opening",
            "short episodes",
            "short movie",
            "short stories collection",
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
            "misleading beginning",
            "non-linear",
            "no conclusion", // like the decision of this tag's name
            "only makes sense with original work knowledge", // debating moving this, but it is a spoiler technically
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
            Plot          = 1 << 4,
            Setting       = 1 << 5,
            Programming   = 1 << 6,
        }


        /// <summary>
        /// Filters tags based on settings specified in flags
        ///        0b00000001 : Hide AniDB Internal Tags
        ///        0b00000010 : Hide Art Style Tags
        ///        0b00000100 : Hide Source TransactionHelper.Work Tags
        ///        0b00001000 : Hide Useful Miscellaneous Tags
        ///        0b00010000 : Hide Plot Spoiler Tags
        ///        0b00100000 : Hide Settings Tags
        /// </summary>
        /// <param name="strings">A list of strings [ "meta tags", "elements", "comedy" ]</param>
        /// <param name="flags">the <see cref="TagFilter.Filter"/> flags</param>
        /// <param name="addTags">is it okay to add tags to the list</param>
        /// <returns>the original list with items removed based on rules provided</returns>
        public static List<string> ProcessTags(Filter flags, List<string> strings, bool addTags = true)
        {
            if (strings.Count == 0) return strings;

            List<string> toAdd = new List<string>();

            if (strings.Count == 1)
            {
                if (IsTagBlackListed(strings[0], flags, ref toAdd)) strings.Clear();
                return strings;
            }

            List<string> toRemove = new List<string>((int)Math.Ceiling(strings.Count / 2D));

            var stringsSet = new HashSet<string>(strings);
            strings.AsParallel().ForAll(a => MarkTagsForRemoval(a, flags, ref toRemove, ref toAdd));

            foreach (var a in toRemove) if (stringsSet.Contains(a)) strings.Remove(a);

            if (addTags) toAdd.ForEach(strings.Add);

            return strings;
        }

        private static void MarkTagsForRemoval(string a, Filter flags, ref List<string> toRemove, ref List<string> toAdd)
        {
            if (IsTagBlackListed(a, flags, ref toAdd))
            {
                lock (toRemove)
                {
                    toRemove.Add(a);
                }
            }
            else
            {
                if (!flags.HasFlag(Filter.Setting))
                {
                    if (a.Equals("alternative present"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add("present");
                        }
                    } else if (a.Equals("alternative past"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add("past");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Filters tags based on settings specified in flags
        ///        0b00000001 : Hide AniDB Internal Tags
        ///        0b00000010 : Hide Art Style Tags
        ///        0b00000100 : Hide Source TransactionHelper.Work Tags
        ///        0b00001000 : Hide Useful Miscellaneous Tags
        ///        0b00010000 : Hide Plot Spoiler Tags
        ///        0b00100000 : Hide Settings Tags
        /// </summary>
        /// <param name="a">the tag to check</param>
        /// <param name="flags">the <see cref="TagFilter.Filter"/> flags</param>
        /// <param name="toAdd">tags to add</param>
        /// <returns>true if the tag would be removed</returns>
        public static bool IsTagBlackListed(string a, Filter flags, ref List<string> toAdd)
        {
            string tag = a.Trim().ToLowerInvariant();
            if (flags.HasFlag(Filter.ArtStyle))
            {
                if (TagBlackListArtStyle.Contains(tag)) return true;

                if (tag.Contains("censor")) return true;
            }

            if (flags.HasFlag(Filter.Source)) // if source excluded
            {
                if (TagBlackListSource.Contains(tag)) return true;
            }
            else
            {
                if (tag.Equals("new"))
                {
                    toAdd.Add("Original Work");
                    return true;
                }

                if (tag.Equals("original work")) return true;
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

            if (flags.HasFlag(Filter.Setting))
            {
                if (TagBlacklistSetting.Contains(tag)) return true;
                if (tag.EndsWith("period")) return true;
            }
            
            if (flags.HasFlag(Filter.Programming))
            {
                if (TagBlacklistProgramming.Contains(tag)) return true;
            }

            if (flags.HasFlag(Filter.AnidbInternal))
            {
                if (TagBlacklistAniDBHelpers.Contains(tag)) return true;

                if (tag.StartsWith("predominantly")) return true;
                if (tag.StartsWith("adapted into")) return true;

                if (tag.StartsWith("weekly")) return true;

                if (tag.Contains("to be") || tag.Contains("need"))
                {
                    if (tag.EndsWith("improved") || tag.EndsWith("improving") || tag.EndsWith("improvement")) return true;

                    if (tag.EndsWith("deleting") || tag.EndsWith("deleted")) return true;

                    if (tag.EndsWith("removing") || tag.EndsWith("removed")) return true;

                    if (tag.EndsWith("merging") || tag.EndsWith("merged")) return true;

                    // to be moved to ..., so contains
                    if (tag.Contains("moving") || tag.Contains("moved")) return true;

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