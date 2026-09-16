# Filtering Services

This folder defines the services that evaluate a filter against a collection:
`IFilteringEngine`, `IMetadataFilteringService`, `IFilterPresetManager` and
`IFuzzySearchService`. All four are implemented by the core and registered as
singletons, so a plugin **consumes** them through constructor injection. None of
them is an extension point, and nothing in `Shoko.Abstractions.Filtering` is
discovered through `IPluginManager.GetExports<T>()`.

What a plugin *does* build here are filters: an `IFilter` carrying an expression
tree, handed to the engine per call. `GenericFilter`, `GenericFilterExpression`
and `GenericSortingExpression` in `Filtering/Generic/` exist for exactly that,
and take a plain lambda.

---

## The shape of a filter

```
IFilter
 ├─ ApplyAtSeriesLevel : bool                  evaluate per series, or per group
 ├─ Expression         : IFilterExpression<bool>?   the tree, null means "everything"
 └─ SortingExpression  : ISortingExpression?        ordering, with a .Next fallback chain
```

Every node of the tree evaluates against two objects, and nothing else:

| | |
|---|---|
| `IFilterableInfo` | A flat, precomputed projection of one series or group: names, tags, seasons, episode counts, file sources, languages, paths, image types, cross-reference counts. Always present. |
| `IFilterableUserInfo` | The same idea for one user against that entity: watched counts, votes, user tags, watched dates. Present only when the call was given a user. |

These are deliberately not the metadata interfaces. A *group's* filterable
aggregates every series beneath it, so `Names`, `Seasons`, `AudioLanguages` and
friends are unions, `Shared…` variants are intersections, and a count is a sum.
Plugins consume both interfaces and never implement them.

An expression declares two flags that decide what the engine needs:

- **`UserDependent`** means a user must be passed, or the call throws
  `ArgumentNullException`.
- **`TimeDependent`** means the result can change with the clock. The `time`
  argument stays optional: omitting it evaluates against `DateTime.Now`, in
  local time.

Composite expressions propagate both flags from their children, so an `And` over
a user-dependent leaf is itself user dependent.

---

## Evaluating a filter you built yourself

`IMetadataFilteringService` is the one to reach for first. It wraps the engine
and gives back `IShokoSeries` and `IShokoGroup` rather than ID tuples:

```csharp
public class MyService(IMetadataFilteringService filteringService)
{
    public IReadOnlyList<IShokoSeries> GetUnwatchedBluRays(IUser user)
    {
        var filter = new GenericFilter
        {
            ApplyAtSeriesLevel = true,
            Expression = new GenericFilterExpression(
                // "bluray" is the stored source vocabulary, not a display name:
                // tv, www, dvd, bluray, vhs, camcorder, vcd, ld, film, unk.
                (info, userInfo) => userInfo.UnwatchedEpisodes > 0 && info.VideoSources.Contains("bluray")
            ),
            SortingExpression = new GenericSortingExpression(info => info.SortName),
        };

        // The expression is user dependent (that constructor sets the flag for
        // you), so the user is required here.
        return filteringService.GetAllFilteredSeries(filter, user);
    }
}
```

The `GenericFilterExpression` constructors set `UserDependent` and
`TimeDependent` to match the lambda you pass: `Func<IFilterableInfo, bool>` sets
neither, the two-argument overload sets `UserDependent`, the three-argument one
sets both, and the last overload lets you state them yourself. Getting them
wrong is the usual cause of a `NullReferenceException` inside a handler, since a
handler that reads `userInfo` without the flag set is called with `null`.

The core expression classes in `Filtering/Expressions/` are `IFilterExpression<bool>`
implementations too, so a tree can mix them with your own lambdas:

```csharp
var filter = new GenericFilter
{
    Expression = new AndExpression(
        new InSeasonExpression(2016, YearlySeason.Fall),
        new HasVideoFilesExpression()
    ),
};
```

### `ApplyAtSeriesLevel`

`true` evaluates the expression once per series. `false` evaluates it once per
group, and the result then expands to every series in each matching group. This
changes what the expression sees (a group's aggregate rather than one series)
and what a hit means, so pick it before writing the expression, not after.
`GenericFilter` defaults to series level; a stored `IFilterPreset` defaults to
group level.

---

## `InSeasonExpression`, and why a series has more than one season

`InSeasonExpression(int year, YearlySeason season)` is the worked example worth
reading, because it shows the shape of the data underneath:

```csharp
public override bool Evaluate(IFilterableInfo filterable, IFilterableUserInfo? userInfo, DateTime? time)
    => filterable.Seasons.Contains((Year, Season));
```

`IFilterableInfo.Seasons` is built from `IWithYearlySeasons.YearlySeasons`,
which is a **list**, not a single value: every season between a series' air date
and its effective end date. A two-cour show is in two of them, and a long-runner
is in every season it aired through. So "Fall 2016" is not "series that
*premiered* in Fall 2016", it is "series that were *airing* in Fall 2016", and
one series legitimately answers `true` for dozens of seasons.

The expression is `TimeDependent` because a still-airing series keeps gaining
seasons as the clock moves. It takes its year through `IWithNumberParameter` and
its season through `IWithSecondStringParameter` (parsed case-insensitively from
the enum name), which is how the API surfaces a two-parameter expression.

The Web UI's seasonal view is built on exactly this: it posts an ad-hoc filter
containing an `InSeason` condition to `POST /api/v3/Filter/Preview/Series` and
renders the answer, without ever storing a preset. That path is available to any
consumer, and `IMetadataFilteringService` is the in-process equivalent.

---

## Filter presets

`IFilterPresetManager` is CRUD over the stored presets plus discovery of the
available expression and sorting types:

| Member | Notes |
|---|---|
| `GetTopLevelPresets()`, `GetPresetById(int)`, `GetPresetsByParentPreset(IFilterPreset)` | Reads. The parent lookup answers empty unless the preset is a directory with a positive ID, so it never enumerates children for a synthesised preset. |
| `CreatePreset(FilterPresetData)` | Throws `KeyNotFoundException` for an unknown parent ID. |
| `UpdatePreset(IFilterPreset, FilterPresetUpdateData)` | Throws `KeyNotFoundException` if the preset is not in the database. Only the properties you actually set are applied. |
| `DeletePreset(IFilterPreset)` | No-op if it is already gone. |
| `GetAvailableFilterExpressions(FilterExpressionGroup?)`, `GetAvailableSortingExpressions()` | Help metadata for every discovered expression, optionally scoped to one `FilterExpressionGroup`. |
| `GetHelpForFilterType<T>()` / `GetHelpForFilterType(Type)` | The generic overload throws `ArgumentException` when the type yields no help; the `Type` overload returns `null`. Same split for the sorting pair. |

`FilterPresetUpdateData` tracks which properties were assigned rather than
treating `null` as "clear". Setting `Expression` to `null` clears it, while
leaving it alone keeps whatever is stored, and `ExpressionSet`, `SortingSet` and
`ParentFilterIDSet` are how the manager tells the two apart. Read a value from a
`FilterPresetUpdateData` only after you have set it, since the getters report the
pending value, not the stored one.

### A preset can only hold core expressions

`CreatePreset` and `UpdatePreset` reject any expression or sorting expression
whose type does not live in the `Shoko.Abstractions` assembly, with an
`ArgumentException`. A `FilterExpression<bool>` subclass defined in your plugin
therefore cannot be persisted in a preset, whatever the discovery machinery
below suggests. The reason is visible in the storage format: a preset's
expression is serialised by bare type name and rebound by scanning the loaded
assemblies, so a preset holding a type from a plugin the user later removes
would silently fail to rebind.

The supported route for logic of your own is the one at the top of this page:
build a `GenericFilter` per call and evaluate it. Nothing is stored, nothing
breaks when your plugin is uninstalled, and you keep full C#.

If you do define a `FilterExpression` subclass anyway, know what it actually
gets you. It is picked up by the server's expression scan, which walks every
loaded assembly, so it appears in `GetAvailableFilterExpressions()` and in the
v3 API's `Filter/Expressions` listing. Two consequences follow. It needs a
public parameterless constructor, because the scan instantiates it with
`Activator.CreateInstance` to read its help metadata. And its **short name must
be unique across every loaded assembly**: the v3 layer keys expressions by class
name with the `Expression`, `Function` or `Selector` suffix trimmed, in a
dictionary that throws on a duplicate key, so shipping a `HasTagExpression` of
your own breaks filtering for the whole server. Prefix the class name with
something of yours.

---

## `IFilteringEngine` directly

Use the engine when IDs are all you need, or when you are filtering many filters
at once. It returns `(GroupID, SeriesID)` tuples, either flat
(`EvaluateFilterWithTuples`) or grouped by parent group ID
(`EvaluateFilterWithGrouping`).

`BatchPrepareFiltersWithTuples` / `BatchPrepareFiltersWithGrouping` take a list
of filters and build the filterable projection for the whole collection **once**,
sharing it across all of them. The returned dictionary is lazy: nothing is
evaluated until you read an entry, so a caller that renders only the visible
filters pays for only those. Directory presets are skipped and have no entry at
all, so index it defensively.

Four things to build around:

- **Evaluation is parallel.** The engine fans the collection out with
  `AsParallel()`, so an expression is called concurrently from several threads
  for different entities. Keep handlers pure: no shared mutable state, no
  ordering assumptions, no I/O worth waiting on. A filter is evaluated over
  every series or group in the collection, so a handler that costs a
  millisecond costs seconds overall.
- **A throwing expression matches nothing.** The engine catches per entity, logs
  the exception against the filter and treats it as a non-match. A handler that
  dereferences a null will not surface as an error, it will surface as an empty
  or suspiciously short list.
- **Materialisation is capped at two filters at a time** by an internal
  semaphore. A batch of lazy entries read in a tight loop serialises there
  rather than piling up.
- **Visibility is applied for you.** When a user is passed, entities the user is
  not allowed to see (`IUser.IsAllowedToSee`, driven by their restricted tags)
  are dropped before the expression runs. With no user, nothing is hidden, so
  pass the user whenever the result is going to be shown to one.

`IMetadataFilteringService` adds group-shaped reads on top:
`GetAllFilteredGroups`, `GetTopLevelFilteredGroups`, `GetFilteredSubGroups` and
`GetAllFilteredGroupsWithChains` return `FilteredGroupResult`, which carries the
resolved group, every chain of group IDs from top level down to the match, and
the set of series IDs that matched inside that scope. `GetFilteredSeriesInGroup`
scopes a series read to one group, optionally recursing into sub-groups. The
service's `Engine` property hands you the raw engine when you need it.

Every read takes a `CancellationToken`, checked between stages, so pass
`HttpContext.RequestAborted` from a controller of yours.

---

## `IFuzzySearchService`

Fuzzy name matching, used by `HasFuzzyNameExpression` and
`FuzzyNameRelevanceSortingSelector`. Latin input goes through culture-normalised
trigram indexing, other scripts (CJK, Arabic, Cyrillic and so on) fall back to
substring matching.

- `FuzzyMatchesAnyName(query, names)` answers whether anything matched.
- `FuzzyScoreAnyName(query, names)` returns
  `(isNotExact, index, distance, lengthDiff)?`, or `null` for no match. The
  tuple is ordered so that a plain ascending sort puts the best matches first.
- `InvalidateCache()` clears the internal score cache. The core already calls it
  when settings are saved, which is when the preferred language order can
  change, so a plugin rarely needs to.

Both expressions match against `IFilterableInfo.PreferredNames` rather than
`Names`: a narrower set (English, Japanese, Romaji, Chinese, Korean, Unknown,
plus the server's preferred language order) that avoids false positives from
obscure-language synonyms. Match against the same set if you call the service
yourself, or your results will not line up with the built-in expressions.

---

For how a plugin is discovered, constructed and registered, see the
[plugin overview](../../README.md).
