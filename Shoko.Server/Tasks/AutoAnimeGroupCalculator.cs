using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using NHibernate;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Tasks
{
    /// <remarks>
    /// This class is NOT thread safe.
    /// </remarks>
    internal class AutoAnimeGroupCalculator
    {
        private static readonly Regex TitleNoiseRegex = new Regex(@"[^\w\s]|\d|gekijouban|the animation|the movie",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly char[] Whitespace = {' ', '\t', '\r', '\n'};

        private readonly ILookup<int, AnimeRelation> _relationMap;
        private readonly Dictionary<int, int> _animeGroupMap = new Dictionary<int, int>();
        private readonly AutoGroupExclude _exclusions;
        private readonly AnimeRelationType _relationsToFuzzyTitleTest;
        private readonly Func<Dictionary<int, RelationNode>, HashSet<RelationEdge>, int> _mainAnimeSelector;

        /// <summary>
        /// Initializes a new <see cref="AutoAnimeGroupCalculator"/> instance.
        /// </summary>
        /// <param name="relationMap">A <see cref="ILookup{TKey,TElement}"/> that maps anime IDs to their relations.</param>
        /// <param name="exclusions">The relation/anime types to ignore when building relation graphs.</param>
        /// <param name="relationsToFuzzyTitleTest">The relationships for which we'll perform title similarity checks for
        /// (If the titles aren't similar enough then the anime will end up in different groups).</param>
        /// <param name="mainAnimeSelectionStrategy">The strategy to use for selecting the "main" anime that will be used
        /// for representing the group.</param>
        /// <exception cref="ArgumentNullException"><paramref name="relationMap"/> is <c>null</c>.</exception>
        public AutoAnimeGroupCalculator(ILookup<int, AnimeRelation> relationMap, AutoGroupExclude exclusions,
            AnimeRelationType relationsToFuzzyTitleTest, MainAnimeSelectionStrategy mainAnimeSelectionStrategy)
        {
            if (relationMap == null)
                throw new ArgumentNullException(nameof(relationMap));

            _relationMap = relationMap;
            _exclusions = exclusions;
            _relationsToFuzzyTitleTest = relationsToFuzzyTitleTest;

            switch (mainAnimeSelectionStrategy)
            {
                case MainAnimeSelectionStrategy.MinAirDate:
                    _mainAnimeSelector = FindSuitableAnimeByMinAirDate;
                    break;
                case MainAnimeSelectionStrategy.Weighted:
                    _mainAnimeSelector = FindSuitableAnimeByWeighting;
                    break;
            }
        }

        /// <summary>
        /// Creates a new <see cref="AutoAnimeGroupCalculator"/> using relationships stored in the database.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <returns>The created <see cref="AutoAnimeGroupCalculator"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public static AutoAnimeGroupCalculator CreateFromServerSettings(ISessionWrapper session)
        {
            string exclusionsSetting = ServerSettings.AutoGroupSeriesRelationExclusions;
            AutoGroupExclude exclusions = AutoGroupExclude.None;
            AnimeRelationType relationsToFuzzyTitleTest = AnimeRelationType.None;
            MainAnimeSelectionStrategy mainAnimeSelectionStrategy = ServerSettings.AutoGroupSeriesUseScoreAlgorithm
                ? MainAnimeSelectionStrategy.Weighted
                : MainAnimeSelectionStrategy.MinAirDate;

            if (!String.IsNullOrEmpty(exclusionsSetting))
            {
                var exclusionTokens = exclusionsSetting
                    .Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();

                exclusions = exclusionTokens
                    .Select(s =>
                    {

                        s = s.Replace(" ", String.Empty);
                        Enum.TryParse(s, true, out AutoGroupExclude exclude);

                        return exclude;
                    })
                    .Aggregate(AutoGroupExclude.None, (exclude, allExcludes) => allExcludes | exclude);

                if (exclusionTokens.Contains("AllowDissimilarTitleExclusion", StringComparer.OrdinalIgnoreCase))
                {
                    relationsToFuzzyTitleTest = AnimeRelationType.SecondaryRelations;
                }
            }

            return Create(session, exclusions, relationsToFuzzyTitleTest, mainAnimeSelectionStrategy);
        }

        /// <summary>
        /// Creates a new <see cref="AutoAnimeGroupCalculator"/> using relationships stored in the database.
        /// </summary>
        /// <param name="session">The NHibernate session.</param>
        /// <param name="exclusions">The relation/anime types to ignore when building relation graphs.</param>
        /// <param name="relationsToFuzzyTitleTest">The relationships for which we'll perform title similarity checks for
        /// (If the titles aren't similar enough then the anime will end up in different groups).</param>
        /// <param name="mainAnimeSelectionStrategy">The strategy to use for selecting the "main" anime that will be used
        /// for representing the group.</param>
        /// <returns>The created <see cref="AutoAnimeGroupCalculator"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
        public static AutoAnimeGroupCalculator Create(ISessionWrapper session,
            AutoGroupExclude exclusions = AutoGroupExclude.SameSetting | AutoGroupExclude.Character,
            AnimeRelationType relationsToFuzzyTitleTest = AnimeRelationType.SecondaryRelations,
            MainAnimeSelectionStrategy mainAnimeSelectionStrategy = MainAnimeSelectionStrategy.MinAirDate)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            var relationshipMap = session.CreateSQLQuery(@"
                SELECT    fromAnime.AnimeID AS fromAnimeId
                        , toAnime.AnimeID AS toAnimeId
                        , fromAnime.AnimeType AS fromAnimeType
                        , toAnime.AnimeType AS toAnimeType
                        , fromAnime.MainTitle AS fromMainTitle
                        , toAnime.MainTitle AS toMainTitle
                        , fromAnime.AirDate AS fromAirDate
                        , toAnime.AirDate AS toAirDate
                        , rel.RelationType AS relationType
                    FROM AniDB_Anime_Relation rel
                        INNER JOIN AniDB_Anime fromAnime
                            ON fromAnime.AnimeID = rel.AnimeID
                        INNER JOIN AniDB_Anime toAnime
                            ON toAnime.AnimeID = rel.RelatedAnimeID")
                .AddScalar("fromAnimeId", NHibernateUtil.Int32)
                .AddScalar("toAnimeId", NHibernateUtil.Int32)
                .AddScalar("fromAnimeType", NHibernateUtil.Int32)
                .AddScalar("toAnimeType", NHibernateUtil.Int32)
                .AddScalar("fromMainTitle", NHibernateUtil.String)
                .AddScalar("toMainTitle", NHibernateUtil.String)
                .AddScalar("fromAirDate", NHibernateUtil.DateTime)
                .AddScalar("toAirDate", NHibernateUtil.DateTime)
                .AddScalar("relationType", NHibernateUtil.String)
                .List<object[]>()
                .Select(r =>
                {
                    var relation = new AnimeRelation
                    {
                        FromId = (int) r[0],
                        ToId = (int) r[1],
                        FromType = (AnimeType) r[2],
                        ToType = (AnimeType) r[3],
                        FromMainTitle = (string) r[4],
                        ToMainTitle = (string) r[5],
                        FromAirDate = (DateTime?) r[6],
                        ToAirDate = (DateTime?) r[7]
                    };

                    switch (((string) r[8]).ToLowerInvariant())
                    {
                        case "full story":
                            relation.RelationType = AnimeRelationType.FullStory;
                            break;
                        case "summary":
                            relation.RelationType = AnimeRelationType.Summary;
                            break;
                        case "parent story":
                            relation.RelationType = AnimeRelationType.ParentStory;
                            break;
                        case "side story":
                            relation.RelationType = AnimeRelationType.SideStory;
                            break;
                        case "prequel":
                            relation.RelationType = AnimeRelationType.Prequel;
                            break;
                        case "sequel":
                            relation.RelationType = AnimeRelationType.Sequel;
                            break;
                        case "alternative setting":
                            relation.RelationType = AnimeRelationType.AlternateSetting;
                            break;
                        case "alternative version":
                            relation.RelationType = AnimeRelationType.AlternateVersion;
                            break;
                        case "same setting":
                            relation.RelationType = AnimeRelationType.SameSetting;
                            break;
                        case "character":
                            relation.RelationType = AnimeRelationType.Character;
                            break;
                        default:
                            relation.RelationType = AnimeRelationType.Other;
                            break;
                    }

                    return relation;
                })
                .ToLookup(k => k.FromId);

            return new AutoAnimeGroupCalculator(relationshipMap, exclusions, relationsToFuzzyTitleTest,
                mainAnimeSelectionStrategy);
        }

        /// <summary>
        /// Gets the ID of the anime represents the group containing the specified <see cref="animeId"/>.
        /// </summary>
        /// <param name="animeId">The ID of the anime to get the group's anime ID for.</param>
        /// <returns>The group's representative anime ID. For anime that don't have any suitable relations,
        /// this will just be <paramref name="animeId"/>.</returns>
        public int GetGroupAnimeId(int animeId)
        {

            // Check to see if the group for the specified anime has been previously calculated
            if (_animeGroupMap.TryGetValue(animeId, out int mainAnimeId))
            {
                return mainAnimeId;
            }

            if (BuildGroupGraph(animeId, out Dictionary<int, RelationNode> nodes, out HashSet<RelationEdge> edges))
            {
                mainAnimeId = _mainAnimeSelector(nodes, edges);

                // Remember main anime ID for all anime in the relation graph
                foreach (int id in nodes.Keys)
                {
                    _animeGroupMap[id] = mainAnimeId;
                }
            }
            else // Specified anime doesn't appear to have any relations
            {
                _animeGroupMap[animeId] = mainAnimeId = animeId;
            }

            return mainAnimeId;
        }

        /// <summary>
        /// Gets the IDs of all anime that is in the same group as the specified anime.
        /// </summary>
        /// <param name="animeId">The ID of the anime to get fellow group member anime IDs from.</param>
        /// <returns>A list containing anime IDs.</returns>
        public IReadOnlyList<int> GetIdsOfAnimeInSameGroup(int animeId)
        {
            int mainAnimeId = GetGroupAnimeId(animeId);
            int[] animeIdsInSameGroup = _animeGroupMap
                .Where(kvp => kvp.Value == mainAnimeId)
                .Select(kvp => kvp.Key)
                .ToArray();

            return animeIdsInSameGroup;
        }

        private static int FindSuitableAnimeByMinAirDate(Dictionary<int, RelationNode> nodes,
            HashSet<RelationEdge> edges)
        {
            int animeId = nodes.Values
                .OrderBy(n => n.AirDate ?? DateTime.MaxValue)
                .Select(n => n.AnimeId)
                .FirstOrDefault();

            return animeId;
        }

        private static int FindSuitableAnimeByWeighting(Dictionary<int, RelationNode> nodes,
            HashSet<RelationEdge> edges)
        {
            var animeRelStats = new List<AnimeRelationStats>(nodes.Count);

            foreach (RelationNode node in nodes.Values)
            {
                var stats = new AnimeRelationStats {AnimeNode = node};
                var visitedNodes = new HashSet<int>();
                var toVisit = new Queue<int>();

                toVisit.Enqueue(node.AnimeId);

                // Calculate the number of sequels this anime has had by traversing the prequel/sequel relationships
                while (toVisit.Count > 0)
                {
                    int animeId = toVisit.Dequeue();

                    if (visitedNodes.Contains(animeId))
                    {
                        continue;
                    }

                    bool hasSequel = false;

                    foreach (RelationEdge edge in edges.Where(
                        e => e.AnimeId2 == animeId && e.RelationType1 == AnimeRelationType.Prequel))
                    {
                        toVisit.Enqueue(edge.AnimeId1);
                        hasSequel = true;
                    }

                    if (hasSequel)
                    {
                        stats.Sequels++;
                    }

                    visitedNodes.Add(animeId);
                }

                // Count the number of various sorts of direct relationships this node has
                foreach (RelationEdge edge in edges)
                {
                    if ((edge.AnimeId1 == node.AnimeId || edge.AnimeId2 == node.AnimeId) &&
                        edge.RelationType1 == AnimeRelationType.AlternateSetting)
                    {
                        stats.AlternateVersions++;
                    }
                    if (edge.AnimeId2 == node.AnimeId && edge.RelationType1 == AnimeRelationType.FullStory)
                    {
                        stats.FullStory++;
                    }
                    if (edge.AnimeId2 == node.AnimeId && edge.RelationType1 == AnimeRelationType.ParentStory)
                    {
                        stats.ParentStory++;
                    }
                }

                animeRelStats.Add(stats);
            }

            // Now that all stats have been collected. Choose the "main" anime for this group.
            // We give highest priority to whichever anime has the highest score.
            // If two or more anime has the same score, then we choose the one that was added to aniDB first
            int mainAnimeId = animeRelStats
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.AnimeNode.AnimeId)
                .Select(s => s.AnimeNode.AnimeId)
                .First();

            return mainAnimeId;
        }

        /// <summary>
        /// Builds a normalized relation graph of the anime directly/indirectly realted to the specified anime ID.
        /// </summary>
        /// <param name="rootAnimeId">The ID of the anime we're building the graph from.</param>
        /// <param name="nodes">Returns the graph nodes (Keyed by anime ID).</param>
        /// <param name="edges">Returns the graph edges.</param>
        /// <returns><c>true</c> if a graph was built; otherwise, <c>false</c>
        /// (e.g. if the specified anime has no relations).</returns>
        private bool BuildGroupGraph(int rootAnimeId, out Dictionary<int, RelationNode> nodes,
            out HashSet<RelationEdge> edges)
        {
            var toVisit = new Queue<int>();
            bool first = true;

            edges = null;
            nodes = null;

            toVisit.Enqueue(rootAnimeId);

            // Traverse the relation graph to build node and edge sets
            while (toVisit.Count > 0)
            {
                int animeId = toVisit.Dequeue();

                foreach (AnimeRelation relation in _relationMap[animeId].Where(ShouldConsiderAnimeRelation))
                {
                    // For the very first relationship we encounter we need to add a relation node for
                    // the anime it is from (i.e. the "anchor" anime we're using as our starting point for graph traversal)
                    if (first)
                    {
                        var startNode = new RelationNode(relation.FromId, relation.FromType, relation.FromAirDate);

                        edges = new HashSet<RelationEdge>();
                        nodes = new Dictionary<int, RelationNode>
                        {
                            [relation.FromId] = startNode
                        };
                        first = false;
                    }

                    var relEdge = new RelationEdge(relation.RelationType, relation.FromId, relation.ToId);

                    edges.Add(relEdge);

                    if (nodes.ContainsKey(relation.ToId))
                    {
                        continue; // We've already visited this anime
                    }

                    var node = new RelationNode(relation.ToId, relation.ToType, relation.ToAirDate);
                    nodes.Add(node.AnimeId, node);
                    toVisit.Enqueue(node.AnimeId);
                }
            }

            return nodes != null;
        }

        /// <summary>
        /// Determines whether or not we should consider using the specified <see cref="AnimeRelation"/>.
        /// </summary>
        /// <remarks>
        /// Relationships such as Prequel/Sequel, Full Story/Summary, Parent Story/Side Story, are automatically
        /// considered (unless in exclusion list). However, if fuzzy title testing is enabled then the other relationship types
        /// are only considered if their main titles "fuzzily" match.
        /// </remarks>
        /// <param name="rel">The <see cref="AnimeRelation"/> to test.</param>
        /// <returns><c>true</c> if the specified <see cref="AnimeRelation"/> should be considered when building
        /// the group graph; otherwise, <c>false</c>.</returns>
        private bool ShouldConsiderAnimeRelation(AnimeRelation rel)
        {
            if (((int) rel.RelationType & (int) _exclusions) != 0)
            {
                return false; // The relation is in the exclusion list, so ignore it
            }
            // Check if we are excluding Movies or OVAs
            if ((_exclusions & AutoGroupExclude.Movie) == AutoGroupExclude.Movie &&
                (rel.FromType == AnimeType.Movie || rel.ToType == AnimeType.Movie)
                || (_exclusions & AutoGroupExclude.Ova) == AutoGroupExclude.Ova &&
                (rel.FromType == AnimeType.OVA || rel.ToType == AnimeType.OVA))
            {
                return false;
            }
            // Are we configured to do a fuzzy title test for this particular relation type? If not, then the relation is immediately allowed
            if ((rel.RelationType & _relationsToFuzzyTitleTest) == 0)
            {
                return true;
            }

            // Perform a very poor man's string metric test.
            // We split both titles up into tokens (basically words) and count the number of times each word
            // appears in both titles (we'll also count characters of matching words).
            // The more words/characters that match, the more likely we'll consider the relationship
            string[] fromTitleTokens = CreateTokensFromTitle(rel.FromMainTitle);
            string[] toTitleTokens = CreateTokensFromTitle(rel.ToMainTitle);
            int matchLen = 0;
            int matches = 0;

            foreach (string fromToken in fromTitleTokens)
            {
                foreach (string toToken in toTitleTokens)
                {
                    if (fromToken.Equals(toToken, StringComparison.InvariantCultureIgnoreCase))
                    {
                        matchLen += fromToken.Length;
                        matches++;
                        break;
                    }
                }
            }

            if (matches == 0)
            {
                return false;
            }

            int shortestTitleLen = Math.Min(fromTitleTokens.Sum(s => s.Length), toTitleTokens.Sum(s => s.Length));
            int minTokenCount = Math.Min(fromTitleTokens.Length, toTitleTokens.Length);

            // Either approximately half the words must match,
            // or the total length of the matched words must equate to 40% or more of the shortest title
            return (matches >= minTokenCount / 2) || matchLen >= Math.Max(1, (int) (shortestTitleLen * 0.4));
        }

        private static string[] CreateTokensFromTitle(string title)
        {
            title = title.Replace('-', ' ');
            title = TitleNoiseRegex.Replace(title, String.Empty);

            return title.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        }

        public AutoGroupExclude Exclusions
        {
            get { return _exclusions; }
        }

        [DebuggerDisplay("{AnimeNode.AnimeId} (Score: {Score})")]
        private sealed class AnimeRelationStats
        {
            public RelationNode AnimeNode;
            public int Sequels;
            public int AlternateVersions;
            public int FullStory;
            public int ParentStory;

            /// <summary>
            /// Gets the calculated score for a particular anime.
            /// The higher the score, the higher the priority this anime should be given.
            /// </summary>
            public int Score
            {
                get
                {
                    int score = 0;

                    switch (AnimeNode.Type)
                    {
                        case AnimeType.TVSeries:
                            score += 3;
                            break;
                        case AnimeType.Web:
                            score += 2;
                            break;
                        case AnimeType.OVA:
                            score += 1;
                            break;
                    }

                    // Anime that have more sequels or side stories will get the highest priority
                    score += Sequels * 2 + AlternateVersions + FullStory + ParentStory * 2;

                    return score;
                }
            }
        }

        [DebuggerDisplay("{AnimeId} ({Type})")]
        private sealed class RelationNode
        {
            public RelationNode(int animeId, AnimeType type, DateTime? airDate)
            {
                AnimeId = animeId;
                Type = type;
                AirDate = airDate;
            }

            public int AnimeId { get; }

            public AnimeType Type { get; }

            public DateTime? AirDate { get; }
        }

        [DebuggerDisplay("{AnimeId1} ({RelationType1}) -> {AnimeId2} ({RelationType2})")]
        private sealed class RelationEdge : IEquatable<RelationEdge>
        {
            public RelationEdge(AnimeRelationType relationType, int fromAnimeId, int toAnimeId)
            {
                // Normalize the relationship so that the directions are consistent (e.g. Prequel/FullStory/ParentStory will
                // always be on the FROM size, and Sequel/Summary/SideStory will always be on the TO side).
                // Doing this means that when added to a HashSet, etc. we'll filter out duplicate/reversed relations
                switch (relationType)
                {
                    case AnimeRelationType.Prequel:
                        AnimeId1 = fromAnimeId;
                        RelationType1 = AnimeRelationType.Prequel;
                        AnimeId2 = toAnimeId;
                        RelationType2 = AnimeRelationType.Sequel;
                        break;
                    case AnimeRelationType.Sequel:
                        AnimeId1 = toAnimeId;
                        RelationType1 = AnimeRelationType.Prequel;
                        AnimeId2 = fromAnimeId;
                        RelationType2 = AnimeRelationType.Sequel;
                        break;
                    case AnimeRelationType.FullStory:
                        AnimeId1 = fromAnimeId;
                        RelationType1 = AnimeRelationType.FullStory;
                        AnimeId2 = toAnimeId;
                        RelationType2 = AnimeRelationType.Summary;
                        break;
                    case AnimeRelationType.Summary:
                        AnimeId1 = toAnimeId;
                        RelationType1 = AnimeRelationType.FullStory;
                        AnimeId2 = fromAnimeId;
                        RelationType2 = AnimeRelationType.Summary;
                        break;
                    case AnimeRelationType.ParentStory:
                        AnimeId1 = fromAnimeId;
                        RelationType1 = AnimeRelationType.ParentStory;
                        AnimeId2 = toAnimeId;
                        RelationType2 = AnimeRelationType.SideStory;
                        break;
                    case AnimeRelationType.SideStory:
                        AnimeId1 = toAnimeId;
                        RelationType1 = AnimeRelationType.ParentStory;
                        AnimeId2 = fromAnimeId;
                        RelationType2 = AnimeRelationType.SideStory;
                        break;
                    default:
                        AnimeId1 = Math.Min(fromAnimeId, toAnimeId);
                        RelationType1 = relationType;
                        AnimeId2 = Math.Max(fromAnimeId, toAnimeId);
                        RelationType2 = relationType;
                        break;
                }
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as RelationEdge);
            }

            public bool Equals(RelationEdge other)
            {
                if (ReferenceEquals(null, other))
                    return false;
                if (ReferenceEquals(this, other))
                    return true;

                return AnimeId1 == other.AnimeId1 && RelationType1 == other.RelationType1
                       && AnimeId2 == other.AnimeId2 && RelationType2 == other.RelationType2;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = AnimeId1;

                    hashCode = (hashCode * 397) ^ (int) RelationType1;
                    hashCode = (hashCode * 397) ^ AnimeId2;
                    hashCode = (hashCode * 397) ^ (int) RelationType2;

                    return hashCode;
                }
            }

            public int AnimeId1 { get; }

            public AnimeRelationType RelationType1 { get; }

            public int AnimeId2 { get; }

            public AnimeRelationType RelationType2 { get; }
        }

        [DebuggerDisplay("{RelationType}: {FromId} [{FromType}] -> {ToId} [{ToType}]")]
        public class AnimeRelation
        {
            public int FromId;
            public AnimeType FromType;
            public string FromMainTitle;
            public DateTime? FromAirDate;
            public int ToId;
            public AnimeType ToType;
            public string ToMainTitle;
            public DateTime? ToAirDate;
            public AnimeRelationType RelationType;
        }

        [Flags]
        public enum AnimeRelationType
        {
            None = 0,
            Other = 1,
            FullStory = 2,
            ParentStory = 4,
            SideStory = 8,
            Prequel = 16,
            Sequel = 32,
            Summary = 64,
            AlternateSetting = 128,
            AlternateVersion = 256, // Haven't seen this relation in my database. Included for completeness sake
            SameSetting = 512,
            Character = 1024,

            SecondaryRelations = AlternateSetting | AlternateVersion | SameSetting | Character | Other
        }
    }

    public enum MainAnimeSelectionStrategy
    {
        MinAirDate,
        Weighted
    }

    [Flags]
    public enum AutoGroupExclude
    {
        None = 0,

        // Relationship types
        Other = AutoAnimeGroupCalculator.AnimeRelationType.Other,
        FullStory = AutoAnimeGroupCalculator.AnimeRelationType.FullStory,
        ParentStory = AutoAnimeGroupCalculator.AnimeRelationType.ParentStory,
        SideStory = AutoAnimeGroupCalculator.AnimeRelationType.SideStory,
        Prequel = AutoAnimeGroupCalculator.AnimeRelationType.Prequel,
        Sequel = AutoAnimeGroupCalculator.AnimeRelationType.Sequel,
        Summary = AutoAnimeGroupCalculator.AnimeRelationType.Summary,
        AlternateSetting = AutoAnimeGroupCalculator.AnimeRelationType.AlternateSetting,
        AlternateVersion = AutoAnimeGroupCalculator.AnimeRelationType.AlternateVersion,
        SameSetting = AutoAnimeGroupCalculator.AnimeRelationType.SameSetting,
        Character = AutoAnimeGroupCalculator.AnimeRelationType.Character,

        // Anime types
        Movie = 0x1000,
        Ova = 0x2000
    }
}