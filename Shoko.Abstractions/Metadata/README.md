# Relations and suggestions

Two ways one entity points at another, kept deliberately apart.

| | What it is | Contract |
|---|---|---|
| **Relation** | An authored fact: this is the sequel of that, this is a side story of that | `IRelatedMetadata` |
| **Suggestion** | An opinion: someone who liked this suggested that, or a provider thinks the two resemble each other | `ISuggestedMetadata` |

Relations form a graph that is meant to be walked. They are symmetric, so a
prequel on one side is a sequel on the other, which is what `Reversed` hands
you. Suggestions are none of that: they are one-directional, and most of them
point at something you do not have.

---

## Reading them

Both hang off the entities, so there is no service to resolve and no lookup to
write. Every `ISeries` and `IMovie` has four lists:

```csharp
IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries { get; }
IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies { get; }
IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> Suggestions { get; }
IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> SuggestedBy { get; }
```

`Suggestions` is what this entity suggests. `SuggestedBy` is the same set read
from the other end: the entries in the collection that suggest **it**. There is
no flag to check, because which list you asked from says which end you are on.

Read them from a Shoko series and you get every provider the series is linked
to, one after another. Read them from a provider's own entity and you get only
that provider's, narrowed to that provider's type:

```csharp
foreach (var suggestion in series.Suggestions)
    logger.LogInformation("{Source} suggests {ID}", suggestion.Source, suggestion.SuggestedID);

// The same list, AniDB only, with AniDB's vote counts in reach.
foreach (var suggestion in series.AnidbAnime.Suggestions)
    logger.LogInformation("{Votes} votes, {Rating}% approval", suggestion.TotalVotes, suggestion.ApprovalRating);
```

| Entity | Its suggestions are | Which adds |
|---|---|---|
| `IAnidbAnime` | `IAnidbSuggestion` | `ApprovalVotes`, `TotalVotes` |
| `IAnilistAnime` | `IAnilistSuggestion` | `Rating`, AniList's net score |
| `ITmdbShow` | `ITmdbShowSuggestion` | nothing; TMDB ranks by order alone |
| `ITmdbMovie` | `ITmdbMovieSuggestion` | nothing |

`ISuggestedMetadata<TBase, TSuggested>` is covariant, so an `IAnidbSuggestion`
already **is** an `ISuggestedMetadata<ISeries, ISeries>`. Nothing has to be
converted to put one in a list of the general form.

---

## What a suggestion carries

| Member | Meaning |
|---|---|
| `BaseID` / `SuggestedID` | The provider's own IDs for the two ends. Always there. |
| `Base` / `Suggested` | The entities, when they are in the collection. `Suggested` is usually `null`. |
| `Kind` | `Recommended` ("watch this next") or `Similar` ("this resembles it"). |
| `Order` | The provider's own ranking, best first, starting at `0`. `null` when the provider ranks by approval instead. |
| `ApprovalRating` | A percentage, for a provider that votes. `null` for one that only hands out a list. |
| `Votes` | How many people voted, where the provider says. `null` otherwise. |
| `Source` | `AniDB`, `TMDB` or `AniList`. |

**`Suggested` being `null` is the normal case, not an error.** A suggestion
names an entry in the provider's catalogue, and most of those are not in
anyone's collection. The IDs are always populated, so a client can show the
entry as something to go and find. Anything that walks suggestions should
expect to skip most of them, or show them as absent.

**The two ranking fields are not interchangeable.** AniDB votes on its similar
anime, so it fills `ApprovalRating` and `Votes`. TMDB hands out two ordered
lists and no votes at all, so it fills `Order` only. AniList scores its
recommendations without publishing how many people voted, so it fills `Order`,
leaves `Votes` null, and puts its net score on `IAnilistSuggestion.Rating`,
where it can be negative. Sorting a mixed list means sorting within each source.

---

## Where each provider's suggestions come from

| Provider | What it gives | Kind |
|---|---|---|
| AniDB | Similar anime, voted on by its users | `Similar` |
| AniList | Recommendations, scored by its users | `Recommended` |
| TMDB | Recommendations and similar titles, for shows and for movies | both |

All three ride along on metadata that is fetched anyway, so none of them costs
a request of its own. The exception is AniList past the first page: how far it
reads is a user setting, because each further page is another request.

TMDB movies are linked to Shoko **episodes**, not to a series, so a movie's
suggestions are reached through the episode, or through the series as the union
of its episodes' movies.

### AniList is symmetric, and is merged because of it

AniList holds one undirected recommendation and serves it from both sides with
the same score. Measured against the live API on 2026-09-20: the top five
recommendations on anime 1 each named it back with an identical rating.

So an AniList anime's `Suggestions` merges both stored directions, flipping the
ones stored the other way round, and `SuggestedBy` is the same set with the
ends swapped rather than a narrower one. This is worth doing rather than
merely tidy, because how far `RecommendationDepth` pages means an edge can sit
above the cutoff on one side and below it on the other: merging recovers
entries instead of only deduplicating them. Where the same edge was stored
twice, the direct copy wins, since only it carries this side's own `Order`.

**The other two are not merged, and must not be.** AniDB's similar anime
reciprocate 99.2% of the time, but 12.4% of reciprocal pairs carry different
approval and vote counts, so the reverse entry is a second opinion rather than
a copy. TMDB's recommendations reciprocate at different ranks, and its similar
titles do not reciprocate at all. Both measured on 2026-09-20.

---

## Mistakes that are easy to make

- **Treating a suggestion as a relation.** They are separate contracts on
  purpose. A suggestion is somebody's opinion, and walking it as though it were
  the relation graph produces nonsense.
- **Assuming a suggestion reverses.** In general it does not: whether the other
  entity says anything back, and with what weight, is the provider's business.
  AniList is the one exception, and it is handled for you rather than being
  something to rely on per provider. See above.
- **Assuming `Suggested` resolves.** See above; most do not.
- **Comparing `Votes` across providers.** Only AniDB publishes a voter count.
  A null there means "not published", never "nobody voted".
- **Reading `SuggestedBy` as a complete picture.** It only covers the entries
  in the collection. Whatever the wider catalogue suggests has never been
  fetched, since we only ever ask about entries we hold.
- **Sorting a mixed list by one field.** Sort within a source, or sort by
  `Order` and accept that a source without one lands arbitrarily.
