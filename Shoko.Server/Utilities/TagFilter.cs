using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;

// ReSharper disable StringLiteralTypo
// ReSharper disable StaticMemberInGenericType
// ReSharper disable IdentifierTypo

namespace Shoko.Server
{
    public static class TagFilter
    {
        public static readonly TagFilter<string> String = new(s => s.ToLowerInvariant(), s => s.ToLowerInvariant());

        [Flags]
        public enum Filter : long
        {
            None = 0,
            AnidbInternal = 1 << 0,
            ArtStyle = 1 << 1,
            Source = 1 << 2,
            Misc = 1 << 3,
            Plot = 1 << 4,
            Setting = 1 << 5,
            Programming = 1 << 6,
            Genre = 1 << 7,

            // This should always be last, if we get that many categories, then we should redesign this
            Invert = 1 << 31,
        }

        public static readonly HashSet<string> TagBlacklistAniDBHelpers = new()
        {
            // AniDB tags that don't help with anything
            "asia",
            "awards",
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
            "original work",
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

        public static readonly HashSet<string> TagBlacklistGenre = new()
        {
            // tags that generally define what a series is about or the theme of it
            "18 restricted",
            "action",
            "adventure",
            "biopunk",
            "comedy",
            "commercial",
            "contemporary fantasy",
            "cyberpunk",
            "daily life",
            "dieselpunk",
            "fairy tale",
            "fantasy",
            "folklore",
            "gaslamp fantasy",
            "hard science fiction",
            "heroic fantasy",
            "high fantasy",
            "horror",
            "isekai",
            "kodomo",
            "merchandising show",
            "music",
            "mystery",
            "neo-noir",
            "parody",
            "photography",
            "romance",
            "satire",
            "school life",
            "science fiction",
            "seinen",
            "shoujo",
            "shounen",
            "soft science fiction",
            "speculative fiction",
            "sports",
            "steampunk",
            "strategy",
            "superhero",
            "survival",
            "tragedy",
            "vanilla series",
            "violence",
        };

        public static readonly HashSet<string> TagBlacklistProgramming = new()
        {
            // Tags that involve how or where it aired, or any awards it got
            "animax taishou",
            "anime no chikara",
            "anime no me",
            "animeism",
            "anisun",
            "broadcast cropped to 4-3",
            "chinese production",
            "comicfesta anime zone",
            "crowdfunded",
            "crunchyroll anime awards",
            "discontinued", // debating putting this elsewhere
            "ganime",
            "jump super anime tour",
            "miracle comic prize",
            "multi-anime projects",
            "newtype anime award",
            "noitamina",
            "oofuji noburou award",
            "original in english",
            "perpetual ongoing",
            "remastered version available",
            "sekai meisaku gekijou",
            "sentai",
            "sino-japanese co-production",
            "south korean production",
            "ultra super anime time",
            "wakate animator ikusei project",
        };

        public static readonly HashSet<string> TagBlacklistSetting = new()
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

        public static readonly HashSet<string> TagBlackListSource = new()
        {
            // tags containing the source of series
            "4-koma",
            "4-koma manga",
            "action game",
            "american derived",
            "biographical film",
            "cartoon",
            "comic book",
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

        public static readonly HashSet<string> TagBlackListArtStyle = new()
        {
            // tags that focus on art style
            "3d cg animation",
            "3d cg closing",
            "alternating animation style",
            "art nouveau",
            "black and white",
            "cast-free",
            "cel-shaded animation",
            "cgi",
            "chibi ed",
            "episodic",
            "experimental animation",
            "flash animation",
            "frame story",
            "Improvised Dialogue",
            "live-action closing",
            "live-action imagery",
            "narration",
            "no dialogue",
            "off-model animation",
            "omnibus format",
            "panels that require pausing",
            "photographic backgrounds",
            "product placement",
            "puppetmation",
            "recycled animation",
            "repeated frames",
            "slide show animation",
            "slow motion",
            "stereoscopic imaging",
            "stereoscopic imaging",
            "stop motion",
            "thick line animation",
            "vignette scenes",
            "vignetted picture",
            "walls of text",
            "watercolour style",
            "widescreen transition",
        };

        public static readonly HashSet<string> TagBlackListUsefulHelpers = new()
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

        public static readonly HashSet<string> TagBlackListPlotSpoilers = new()
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

        /// <summary>
        /// Filters tags based on settings specified in flags
        ///        0b00000001 : Hide AniDB Internal Tags
        ///        0b00000010 : Hide Art Style Tags
        ///        0b00000100 : Hide Source TransactionHelper.Work Tags
        ///        0b00001000 : Hide Useful Miscellaneous Tags
        ///        0b00010000 : Hide Plot Spoiler Tags
        ///        0b00100000 : Hide Settings Tags
        /// </summary>
        /// <param name="tag">the tag to check</param>
        /// <param name="flags">the <see cref="TagFilter.Filter"/> flags</param>
        /// <returns>true if the tag would be removed</returns>
        public static bool IsTagBlackListed(string tag, Filter flags)
        {
            tag = tag.Trim().ToLowerInvariant();
            bool inverted = flags.HasFlag(Filter.Invert);
            if (flags.HasFlag(Filter.ArtStyle))
            {
                if (TagBlackListArtStyle.Contains(tag)) return inverted ^ true;

                if (tag.Contains("censor")) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Source)) // if source excluded
            {
                if (tag.Equals("original work")) return true;
                if (TagBlackListSource.Contains(tag)) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Misc))
            {
                if (tag.StartsWith("preview")) return inverted ^ true;

                if (TagBlackListUsefulHelpers.Contains(tag)) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Plot))
            {
                if (tag.StartsWith("plot") || tag.EndsWith(" dies") || tag.EndsWith(" end") ||
                    tag.EndsWith(" ending")) return inverted ^ true;

                if (TagBlackListPlotSpoilers.Contains(tag)) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Setting))
            {
                if (TagBlacklistSetting.Contains(tag)) return inverted ^ true;
                if (tag.EndsWith("period")) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Programming))
            {
                if (TagBlacklistProgramming.Contains(tag)) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.Genre))
            {
                if (TagBlacklistGenre.Contains(tag)) return inverted ^ true;
            }

            if (flags.HasFlag(Filter.AnidbInternal))
            {
                if (TagBlacklistAniDBHelpers.Contains(tag)) return inverted ^ true;

                if (tag.StartsWith("predominantly")) return inverted ^ true;
                if (tag.StartsWith("adapted into")) return inverted ^ true;

                if (tag.StartsWith("weekly")) return inverted ^ true;

                if (tag.Contains("to be") || tag.Contains("need"))
                {
                    if (tag.EndsWith("improved") || tag.EndsWith("improving") || tag.EndsWith("improvement")) return inverted ^ true;

                    if (tag.EndsWith("deleting") || tag.EndsWith("deleted")) return inverted ^ true;

                    if (tag.EndsWith("removing") || tag.EndsWith("removed")) return inverted ^ true;

                    if (tag.EndsWith("merging") || tag.EndsWith("merged")) return inverted ^ true;

                    // to be moved to ..., so contains
                    if (tag.Contains("moving") || tag.Contains("moved")) return inverted ^ true;

                    // contains is slower, so try the others first
                    if (tag.Contains("split")) return inverted ^ true;
                }

                if (tag.Contains("old animetags")) return inverted ^ true;

                if (tag.Contains("missing")) return inverted ^ true;
            }

            return inverted ^ false;
        }
    }

    public class TagFilter<T> where T : class
    {
        private readonly Dictionary<string, T> _tagRenameDictionary = new(1);
        private readonly Dictionary<string, T> _tagReplacementDictionary = new(1);
        private readonly Func<T, string> _nameSelector;

        public TagFilter(Func<string, T> lookup, Func<T, string> nameSelector)
        {
            _nameSelector = nameSelector;

            T GetTag(string name) => lookup(name) ?? (typeof(T) == typeof(string) ? name as T : (T)Activator.CreateInstance(typeof(T), name));

            var existing = GetTag("original work");
            _tagRenameDictionary.Add("new", existing);

            existing = GetTag("present");
            _tagReplacementDictionary.Add("alternative present", existing);

            existing = GetTag("past");
            _tagReplacementDictionary.Add("alternative past", existing);
        }

        /// <summary>
        /// T needs to have a T(string name) constructor
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public List<T> ProcessTags(TagFilter.Filter flags, IEnumerable<T> input)
        {
            var tags = input.DistinctBy(_nameSelector).ToList();
            ProcessModifications(flags, tags);

            return tags;
        }

        private void ProcessModifications(TagFilter.Filter flags, List<T> tags)
        {
            var toAdd = new HashSet<T>(1);
            var toRemove = new HashSet<T>((int)Math.Ceiling(tags.Count / 2D));
            if (tags.Count > 1)
                tags.AsParallel().ForAll(tag => MarkTagsForRemoval(tag, flags, toRemove, toAdd));
            else if (tags.Count == 1)
                MarkTagsForRemoval(tags[0], flags, toRemove, toAdd);

            foreach (var tag in toRemove)
            {
                // Skip if we want to both remove and add the tag
                if (toAdd.Contains(tag))
                    continue;

                tags.Remove(tag);
            }

            tags.AddRange(toAdd.Where(tag => !toRemove.Contains(tag)));

            // Add the "original work" tag if no source tags are present and we either want to only include the source tags or want to not exclude the source tags.
            var nameToTagDictionary = tags.ToDictionary(_nameSelector, t => t);
            if (flags.HasFlag(TagFilter.Filter.Source) == flags.HasFlag(TagFilter.Filter.Invert) && !nameToTagDictionary.Any(tag => TagFilter.TagBlackListSource.Contains(tag.Key)))
            {
                // cheap way to lookup original work tag
                if (_tagRenameDictionary.TryGetValue("new", out var existing))
                    tags.Add(existing);
            }
        }

        private void MarkTagsForRemoval(T sourceTag, TagFilter.Filter flags, HashSet<T> toRemove, HashSet<T> toAdd)
        {
            var sourceName = _nameSelector(sourceTag);
            string tag = sourceName.Trim().ToLowerInvariant();
            if (TagFilter.IsTagBlackListed(tag, flags))
            {
                lock (toRemove)
                {
                    toRemove.Add(sourceTag);
                }
            }
            else
            {
                if (_tagRenameDictionary.TryGetValue(tag, out var newTag))
                {
                    lock (toRemove)
                    {
                        toRemove.Add(sourceTag);
                    }

                    lock (toAdd)
                    {
                        toAdd.Add(newTag);
                    }
                }

                if (_tagReplacementDictionary.TryGetValue(tag, out newTag))
                {
                    lock (toRemove)
                    {
                        toRemove.Add(newTag);
                    }
                }
            }
        }
    }
}