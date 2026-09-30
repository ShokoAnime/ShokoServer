using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;

// ReSharper disable StringLiteralTypo
// ReSharper disable StaticMemberInGenericType
// ReSharper disable IdentifierTypo

namespace Shoko.Server;

public static class TagFilter
{
    public static readonly TagFilter<string> String = new(s => s, s => s);

    [Flags]
    public enum Filter : ulong
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

        // User tags. won't actually be used in the filter, but having it in this
        // enum makes it easier to send from the clients.
        User = 1L << 30,

        // This should always be last, if we get that many categories, then we should redesign this
        Invert = 1L << 31 // without L Invert is still intiger and it returns after bitshift -2147483648
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
        "description needs improvement",
        "development hell", // :( God Eater
        "dialogue driven", // anidb and their british spellings
        "dynamic",
        "earth",
        "elements",
        "ending",
        "ensemble cast",
        "family life",
        "fast-paced",
        "fetishes",
        "maintenance tags",
        "meta tags",
        "motifs",
        "no english subs available",
        "origin",
        "pic needs improvement",
        "place",
        "pornography",
        "season",
        "setting",
        "some weird shit goin' on", // these are some grave accents in use...
        "source material",
        "staff missing",
        "storytelling",
        "tales",
        "target audience",
        "technical aspects",
        "themes",
        "time",
        "to be moved to character",
        "to be moved to episode",
        "translation convention",
        "tropes",
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
        "violence"
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
        "wakate animator ikusei project"
    };

    public static readonly HashSet<string> TagBlacklistSetting = new()
    {
        // Tags that involve the setting, a time or place in which the story occurs.
        // I've seen more that fall under this that AniDB hasn't listed
        "1920s",
        "1950s",
        "1960s",
        "1990s",
        "africa",
        "akihabara",
        "alternative past",
        "alternative present",
        "americas",
        "ancient rome",
        "antarctic",
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
        "himalayas",
        "hiroshima",
        "historical",
        "hokkaido",
        "hong kong",
        "hospital",
        "iceland",
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
        "nevada",
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
        "singapore",
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
        "yokohama"
    };

    public static readonly HashSet<string> TagBlackListSource = new()
    {
        // tags containing the source of series
        "4-koma",
        "4-koma manga",
        "4-koma manhua",
        "4-koma manhwa",
        "action game",
        "american derived",
        "biographical film",
        "cartoon",
        "cg collection",
        "comic book",
        "erotic game",
        "fan-made",
        "game",
        "korean drama",
        "live-action film",
        "manga",
        "manhua",
        "manhwa",
        // AniDB's old name for "live-action film".
        "movie",
        "novel",
        "original work",
        "picture book",
        "radio programme",
        "remake",
        "rpg",
        "television programme",
        "ultra jump",
        "visual novel",
        "weekly shounen jump",
        "weekly shounen sunday",
        "western animated cartoon",
        "western comics"
    };

    /// <summary>
    ///   The AniDB tag every source material tag sits under: "source material"
    ///   in Shoko (AniDB: "original work").
    /// </summary>
    public const int SourceMaterialParentTagID = 2609;

    /// <summary>
    ///   "erotic game" in both Shoko and AniDB. It marks every game branch tag
    ///   on the same anime as an adult game.
    /// </summary>
    public const int EroticGameTagID = 2803;

    /// <summary>
    ///   AniDB's source material tags by tag ID, with the value each maps to,
    ///   in order of precedence. The same tags <see cref="TagBlackListSource"/>
    ///   lists by name; they are keyed by ID here because AniDB renames tags.
    ///   Comments give Shoko's name, then AniDB's when it differs.
    /// </summary>
    private static readonly (int TagID, SourceMaterial Value)[] _sourceMaterialTags =
    [
        // "original work" (AniDB: "new")
        (2797, SourceMaterial.Original),
        // "manga", "4-koma manga", "Weekly Shounen Jump", "Weekly Shounen Sunday", "Ultra Jump"
        (2798, SourceMaterial.Manga),
        (2805, SourceMaterial.Manga),
        (5863, SourceMaterial.Manga),
        (6442, SourceMaterial.Manga),
        (4250, SourceMaterial.Manga),
        // "novel"
        (2799, SourceMaterial.Novel),
        // "visual novel", "erotic game", "RPG", "action game", "game"
        (2804, SourceMaterial.VisualNovel),
        (EroticGameTagID, SourceMaterial.Eroge),
        (2801, SourceMaterial.VideoGame),
        (2802, SourceMaterial.VideoGame),
        (2800, SourceMaterial.VideoGame),
        // "manhua", "4-koma manhua"
        (6493, SourceMaterial.Manhua),
        (7261, SourceMaterial.Manhua),
        // "manhwa", "4-koma manhwa"
        (5010, SourceMaterial.Manhwa),
        (7260, SourceMaterial.Manhwa),
        // "Western comics"
        (3430, SourceMaterial.Comic),
        // "live-action film", "television programme", "Korean drama"
        (2796, SourceMaterial.LiveAction),
        (6446, SourceMaterial.LiveAction),
        (6640, SourceMaterial.LiveAction),
        // "picture book"
        (7469, SourceMaterial.PictureBook),
        // "radio programme", "CG collection", "American derived", "Western animated cartoon"
        (6453, SourceMaterial.Other),
        (7252, SourceMaterial.Other),
        (4424, SourceMaterial.Other),
        (3714, SourceMaterial.Other),
    ];

    private static readonly Dictionary<int, int> _sourceMaterialPrecedence = _sourceMaterialTags
        .Select((tag, index) => (tag.TagID, index))
        .ToDictionary(tuple => tuple.TagID, tuple => tuple.index);

    /// <summary>
    ///   The source material tag IDs this filter maps, in order of precedence.
    /// </summary>
    public static IReadOnlyList<int> SourceMaterialTagIDs { get; } = _sourceMaterialTags.Select(tag => tag.TagID).ToList();

    /// <summary>
    ///   Picks what an anime was adapted from out of its AniDB tags.
    ///   <see cref="SourceMaterial.Unknown"/> when it has no source material
    ///   tag, since AniDB does not tag every anime.
    /// </summary>
    /// <param name="tags">
    ///   The anime's tags, with the weight each carries on the anime. The
    ///   highest weight wins, then the order of precedence. Spoiler flags do
    ///   not matter.
    /// </param>
    /// <param name="getParentTagID">
    ///   Looks up a tag's parent, so a tag AniDB added under a mapped one maps
    ///   like its parent, and a new tag directly under the source material
    ///   tag maps to <see cref="SourceMaterial.Other"/>. Without it only the
    ///   mapped tags count.
    /// </param>
    /// <returns>
    ///   The source material, or <see cref="SourceMaterial.Eroge"/> in place
    ///   of any game when the anime is also tagged "erotic game".
    /// </returns>
    public static SourceMaterial GetSourceMaterial(IEnumerable<(int TagID, int Weight)> tags, Func<int, int?>? getParentTagID = null)
    {
        var found = false;
        var bestWeight = 0;
        var bestPrecedence = 0;
        var bestValue = SourceMaterial.Unknown;
        var isEroticGame = false;
        foreach (var (tagID, weight) in tags)
        {
            if (!TryGetSourceMaterial(tagID, getParentTagID, out var precedence, out var value))
                continue;

            if (tagID is EroticGameTagID)
                isEroticGame = true;

            if (found && (weight < bestWeight || (weight == bestWeight && precedence >= bestPrecedence)))
                continue;

            found = true;
            bestWeight = weight;
            bestPrecedence = precedence;
            bestValue = value;
        }

        if (isEroticGame && bestValue is SourceMaterial.VisualNovel or SourceMaterial.VideoGame)
            return SourceMaterial.Eroge;

        return bestValue;
    }

    private static bool TryGetSourceMaterial(int tagID, Func<int, int?>? getParentTagID, out int precedence, out SourceMaterial value)
    {
        // Walk up a few levels at most, which also guards against a cycle in
        // bad tag data.
        for (var depth = 0; depth < 5; depth++)
        {
            if (_sourceMaterialPrecedence.TryGetValue(tagID, out precedence))
            {
                value = _sourceMaterialTags[precedence].Value;
                return true;
            }

            if (getParentTagID?.Invoke(tagID) is not { } parentTagID)
                break;

            if (parentTagID is SourceMaterialParentTagID)
            {
                precedence = _sourceMaterialTags.Length;
                value = SourceMaterial.Other;
                return true;
            }

            tagID = parentTagID;
        }

        precedence = 0;
        value = SourceMaterial.Unknown;
        return false;
    }

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
        "widescreen transition"
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
        var inverted = flags.HasFlag(Filter.Invert);

        // Always remove the "original work" tag. It will be added back if it's
        // not supposed to be filtered out and there are no other source
        // material tags.
        if (tag.Equals("original work"))
        {
            return true;
        }

        if (flags.HasFlag(Filter.ArtStyle))
        {
            if (TagBlackListArtStyle.Contains(tag))
            {
                return inverted ^ true;
            }

            if (tag.Contains("censor"))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Source)) // if source excluded
        {
            if (TagBlackListSource.Contains(tag))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Misc))
        {
            if (tag.StartsWith("preview"))
            {
                return inverted ^ true;
            }

            if (TagBlackListUsefulHelpers.Contains(tag))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Plot))
        {
            if (tag.StartsWith("plot") || tag.EndsWith(" dies") || tag.EndsWith(" end") ||
                tag.EndsWith(" ending"))
            {
                return inverted ^ true;
            }

            if (TagBlackListPlotSpoilers.Contains(tag))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Setting))
        {
            if (TagBlacklistSetting.Contains(tag))
            {
                return inverted ^ true;
            }

            if (tag.EndsWith("period"))
            {
                return inverted ^ true;
            }

            if (tag.EndsWith("era"))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Programming))
        {
            if (TagBlacklistProgramming.Contains(tag))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.Genre))
        {
            if (TagBlacklistGenre.Contains(tag))
            {
                return inverted ^ true;
            }
        }

        if (flags.HasFlag(Filter.AnidbInternal))
        {
            if (TagBlacklistAniDBHelpers.Contains(tag))
            {
                return inverted ^ true;
            }

            if (tag.StartsWith("predominantly"))
            {
                return inverted ^ true;
            }

            if (tag.StartsWith("adapted into"))
            {
                return inverted ^ true;
            }

            if (tag.StartsWith("weekly"))
            {
                return inverted ^ true;
            }

            if (tag.Contains("to be") || tag.Contains("need"))
            {
                if (tag.EndsWith("improved") || tag.EndsWith("improving") || tag.EndsWith("improvement"))
                {
                    return inverted ^ true;
                }

                if (tag.EndsWith("deleting") || tag.EndsWith("deleted"))
                {
                    return inverted ^ true;
                }

                if (tag.EndsWith("removing") || tag.EndsWith("removed"))
                {
                    return inverted ^ true;
                }

                if (tag.EndsWith("merging") || tag.EndsWith("merged"))
                {
                    return inverted ^ true;
                }

                // to be moved to ..., so contains
                if (tag.Contains("moving") || tag.Contains("moved"))
                {
                    return inverted ^ true;
                }

                // contains is slower, so try the others first
                if (tag.Contains("split"))
                {
                    return inverted ^ true;
                }
            }

            if (tag.Contains("old animetags"))
            {
                return inverted ^ true;
            }

            if (tag.Contains("missing"))
            {
                return inverted ^ true;
            }
        }

        return inverted ^ false;
    }
}

public class TagFilter<T> where T : class
{
    private readonly Func<T, string> _nameSelector;
    private readonly Func<string, T?> _lookup;
    private readonly Func<string, T?> _ctor;

    public TagFilter(Func<string, T?> lookup, Func<T, string> nameSelector, Func<string, T?>? ctor = null)
    {
        _nameSelector = nameSelector;
        // explicit delegate prevents a warning
        _ctor = ctor ?? (typeof(T) == typeof(string) ? new Func<string, T?>(name => name as T) : name => Activator.CreateInstance(typeof(T), name) as T);
        _lookup = lookup;
    }

    private string? GetTagName(T tag)
    {
        return _nameSelector(tag)?.ToLowerInvariant();
    }

    private T? GetTag(string name)
    {
        return _lookup(name) ?? _ctor(name);
    }

    /// <summary>
    /// T needs to have a T(string name) constructor
    /// </summary>
    /// <param name="flags"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    public List<T> ProcessTags(TagFilter.Filter flags, IEnumerable<T> input)
    {
        var tags = input.DistinctBy(GetTagName).ToList();
        ProcessModifications(flags, tags);

        return tags;
    }

    private void ProcessModifications(TagFilter.Filter flags, List<T> tags)
    {
        var toRemove = new ConcurrentBag<T>();
        switch (tags.Count)
        {
            case 1:
                MarkTagsForRemoval(tags[0], flags, toRemove);
                break;
            case >= 50:
                tags.AsParallel().ForAll(tag => MarkTagsForRemoval(tag, flags, toRemove));
                break;
            default:
                tags.ForEach(tag => MarkTagsForRemoval(tag, flags, toRemove));
                break;
        }

        foreach (var tag in toRemove)
            while (tags.Remove(tag)) { }

        // Add the _original work_ tag if no source tags are present and we either want to only include the source tags or want to not exclude the source tags.
        // evaluates like an xor because of how invert works
        var includeSource = flags.HasFlag(TagFilter.Filter.Source) == flags.HasFlag(TagFilter.Filter.Invert);
        var addOriginal = includeSource && !tags.Select(GetTagName).Any(tag => tag is not null && TagFilter.TagBlackListSource.Contains(tag));
        var addSourceMaterial = flags.HasFlag(TagFilter.Filter.AnidbInternal) != flags.HasFlag(TagFilter.Filter.Invert);
        if (addOriginal)
        {
            tags.Add(GetTag("original work")!);
            var includeHelpers = flags.HasFlag(TagFilter.Filter.AnidbInternal) == flags.HasFlag(TagFilter.Filter.Invert);
            if (includeHelpers && !tags.Select(GetTagName).Contains("source material") && addSourceMaterial) tags.Add(GetTag("source material")!);
        }
    }

    private void MarkTagsForRemoval(T sourceTag, TagFilter.Filter flags, ConcurrentBag<T> toRemove)
    {
        var sourceName = GetTagName(sourceTag);
        if (!TagFilter.IsTagBlackListed(sourceName!, flags)) return;

        toRemove.Add(sourceTag);
    }
}
