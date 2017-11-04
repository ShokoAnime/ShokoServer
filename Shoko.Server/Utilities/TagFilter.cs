using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server
{
    public static class TagFilter
    {
        // ported from python
        // ReSharper disable once UnusedMember.Local
        private const int version = 2; // increase with each push/edit

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
            "some weird shit goin` on", // these are some grave accents in use...
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
            // tags containing source of serie
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
        ///    :param flags:
        ///        0b00001 : Hide AniDB Internal Tags
        ///        0b00010 : Hide Art Style Tags
        ///        0b00100 : Hide Source TransactionHelper.Work Tags
        ///        0b01000 : Hide Useful Miscellaneous Tags
        ///        0b10000 : Hide Plot Spoiler Tags
        ///    :param string: A list of strings [ 'meta tags', 'elements', 'comedy' ]
        ///    :return: The list of strings after filtering
        /// </summary>
        /// <returns></returns>
        public static List<string> ProcessTags(Filter flags, List<string> strings, bool addOriginal = true)
        {
            HashSet<string> toRemove = new HashSet<string>();

            bool readdOriginal = true;

            var stringsSet = new HashSet<string>(strings);
            strings.AsParallel().ForAll(a =>
            {
                string tag = a.Trim().ToLowerInvariant();
                if (flags.HasFlag(Filter.ArtStyle))
                {
                    if (TagBlackListArtStyle.Contains(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.Contains("censor"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                }
                if (flags.HasFlag(Filter.Source)) // if source excluded
                {
                    readdOriginal = false;
                    if (TagBlackListSource.Contains(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if ("original work".Equals(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                }
                else
                {
                    if (TagBlackListSource.Contains(tag))
                    {
                        readdOriginal = false;
                        return;
                    }
                    if ("original work".Equals(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                }

                if (flags.HasFlag(Filter.Misc))
                {
                    if (TagBlackListUsefulHelpers.Contains(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.StartsWith("preview"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                }

                if (flags.HasFlag(Filter.Plot))
                {
                    if (TagBlackListPlotSpoilers.Contains(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.StartsWith("plot") || tag.EndsWith(" dies") || tag.EndsWith(" end") ||
                        tag.EndsWith(" ending"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                }

                if (flags.HasFlag(Filter.AnidbInternal))
                {
                    if (TagBlacklistAniDBHelpers.Contains(tag))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.Contains("to be") || tag.Contains("need") || tag.Contains("needs"))
                    {
                        if (tag.Contains("split"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                        if (tag.Contains("improved") || tag.Contains("improving") || tag.Contains("improvement"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                        if (tag.Contains("merging") || tag.Contains("merged"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                        if (tag.Contains("deleting") || tag.Contains("deleted"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                        if (tag.Contains("moving") || tag.Contains("moved"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                        if (tag.Contains("improved") || tag.Contains("improving") || tag.Contains("improvement"))
                        {
                            lock (toRemove)
                            {
                                toRemove.Add(a);
                            }
                            return;
                        }
                    }

                    if (tag.Contains("old animetags"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.Contains("missing"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.StartsWith("predominantly"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                        return;
                    }
                    if (tag.StartsWith("weekly"))
                    {
                        lock (toRemove)
                        {
                            toRemove.Add(a);
                        }
                    }
                }
            });

            foreach (var a in toRemove)
            {
                if (stringsSet.Contains(a)) strings.Remove(a);
            }

            if (readdOriginal && addOriginal) strings.Add("Original Work");

            return strings;
        }
    }
}