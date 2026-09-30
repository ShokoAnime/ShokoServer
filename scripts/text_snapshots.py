#!/usr/bin/env python3
"""Snapshots of every title and description the API hands out, to compare one server build against another.

Built for the universal text work: the snapshots taken on the baseline commit are the "before", and each later phase
is compared against them. The output comes from a real library, so it belongs outside the repository.

Subcommands (run with --help for their options):

  corpus    Builds the search corpus (200 queries: whole titles, partials, romaji with typos, Japanese fragments,
            single words, synonyms and misses) from a copy of the database. Generate it once and reuse the file, since
            later schemas drop the tables it reads.
  snapshot  Writes, per section, one JSON line per entity or query: the search corpus's results, every filter
            preset's groups and series, a relocation preview of every file, and the text of every series, episode,
            group, TMDB entity and AniDB person over APIv3 and APIv2.
  browse    The scripted browse (every series page, 500 episode pages, every filter preset) and the p50/p95 timings of
            the hot routes, with the server's GC heap sampled before and after through dotnet-counters.
  start     Starts a server on a home, waits until it is ready, and prints how long that took.
  stop      Stops the server started by "start" (only that process).
  compare   Compares two snapshot folders and lists every entity or query whose output differs. Records that depend
            on the date (filters using Today, the airing calendar) are listed apart, and not counted, when the two
            snapshots were taken on different days.

The API key is read from a file (--key-file) and never printed. The server refuses to preview files outside drop
folders, so the copy the server runs on should mark every managed folder as a drop source and destination, with
watching off so nothing on disk is picked up.
"""
import argparse
import concurrent.futures
import hashlib
import json
import os
import random
import re
import signal
import sqlite3
import statistics
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request

SECTIONS = ("search", "filters", "relocation", "series", "episodes", "groups", "tmdb", "people", "v2", "dashboard")

# Records whose output moves with the date on its own: the airing calendar starts today.
TIME_DEPENDENT_KEYS = {"dashboard:AniDBCalendar?showAll=true"}

# The keys that carry text, or say which entity the text belongs to. Everything else is dropped from a snapshot.
TEXT_KEY = re.compile(r"(?i)(name|title|summary|description|overview|^id$|^match$|^exactmatch$|^distance$|^total$)")

# Keys whose names look like text but are not titles or descriptions (site names, image metadata).
SKIPPED_KEYS = {"Links", "Images", "Sizes", "art"}


#region HTTP

class Api:
    def __init__(self, base, key_file, timeout=900):
        self.base = base.rstrip("/")
        with open(key_file) as handle:
            self._key = handle.read().strip()
        self.timeout = timeout
        self.timings = {}
        # How many requests a snapshot section sends at once; browse always sends one at a time, since it times them.
        self.workers = 1
        self._timings_lock = threading.Lock()

    def call(self, path, body=None, method=None, family=None):
        headers = {"Accept": "application/json", "apikey": self._key}
        data = None
        if body is not None:
            data = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(self.base + path, data=data, headers=headers, method=method or ("POST" if body is not None else "GET"))
        started = time.perf_counter()
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                status, payload = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, payload = error.code, error.read()
        elapsed = time.perf_counter() - started
        if family:
            with self._timings_lock:
                self.timings.setdefault(family, []).append(elapsed)
        try:
            parsed = json.loads(payload) if payload else None
        except ValueError:
            parsed = payload.decode("utf-8", "replace")[:500]
        return status, parsed

    def get(self, path, family=None):
        status, parsed = self.call(path, family=family)
        if status != 200:
            return {"status": status, "error": parsed}
        return parsed

    def pages(self, path, page_size, family=None):
        """Every item of a paged APIv3 list, in the order the server returns them."""
        page = 1
        joiner = "&" if "?" in path else "?"
        while True:
            result = self.get(f"{path}{joiner}pageSize={page_size}&page={page}", family=family)
            if not isinstance(result, dict) or "List" not in result:
                yield {"status": "error", "error": result}
                return
            yield from result["List"]
            if page * page_size >= result.get("Total", 0) or not result["List"]:
                return
            page += 1


def q(value):
    return urllib.parse.quote(str(value), safe="")


def fan_out(api, work, items):
    """Runs `work` over the items with up to `api.workers` requests in flight, and returns the results in the items'
    order, so the snapshot files come out the same however many workers ran."""
    items = list(items)
    if api.workers <= 1 or len(items) <= 1:
        return [work(item) for item in items]
    with concurrent.futures.ThreadPoolExecutor(max_workers=api.workers) as pool:
        return list(pool.map(work, items))

#endregion

#region Pruning

def prune(value):
    """Keeps the text-bearing keys of a response, and the ID that says whose text it is."""
    if isinstance(value, list):
        return [prune(item) for item in value]
    if not isinstance(value, dict):
        return value
    return _prune_dict(value) or {}


def _prune_dict(value):
    kept = {}
    for key, item in value.items():
        if key in SKIPPED_KEYS:
            continue
        if key == "IDs" and isinstance(item, dict):
            kept[key] = {"ID": item.get("ID")}
        elif TEXT_KEY.search(key):
            kept[key] = item
        elif isinstance(item, dict):
            if inner := _prune_dict(item):
                kept[key] = inner
        elif isinstance(item, list) and any(isinstance(i, dict) for i in item):
            inner = [_prune_dict(i) if isinstance(i, dict) else None for i in item]
            if any(inner):
                kept[key] = inner
    return kept


def digest(value):
    return hashlib.sha1(json.dumps(value, sort_keys=True, ensure_ascii=False).encode()).hexdigest()


class Writer:
    """One JSON line per record, keyed so two snapshots can be matched up."""

    def __init__(self, folder, section):
        self.path = os.path.join(folder, f"{section}.jsonl")
        self.handle = open(self.path, "w", encoding="utf-8")
        self.count = 0

    def write(self, key, value):
        self.handle.write(json.dumps({"key": key, "value": value}, ensure_ascii=False, sort_keys=True) + "\n")
        self.count += 1

    def close(self):
        self.handle.close()
        return self.count

#endregion

#region Corpus

def build_corpus(db_path, seed):
    """200 queries drawn from the AniDB titles, reproducible for a given database and seed."""
    db = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    rows = db.execute("SELECT AnimeID, Title, Language, TitleType FROM AniDB_Anime_Title ORDER BY AniDB_Anime_TitleID").fetchall()
    db.close()
    rng = random.Random(seed)
    # TitleType: 1 main, 3 official, 2 synonym, 4 short (stored as text or number depending on the schema).
    kind = lambda row: str(row[3]).lower()
    main = [r for r in rows if kind(r) in ("1", "main")]
    official_en = [r for r in rows if kind(r) in ("3", "official") and r[2] in ("en", "EN")]
    japanese = [r for r in rows if r[2] in ("ja", "JA") and len(r[1]) >= 3]
    synonyms = [r for r in rows if kind(r) in ("2", "4", "synonym", "short")]

    def typo(text):
        if len(text) < 5:
            return text
        i = rng.randrange(1, len(text) - 2)
        return [text[:i] + text[i + 1:], text[:i] + text[i + 1] + text[i] + text[i + 2:], text[:i] + "e" + text[i + 1:]][rng.randrange(3)]

    corpus = []
    corpus += [("title", r[1]) for r in rng.sample(main, min(50, len(main)))]
    corpus += [("english", r[1]) for r in rng.sample(official_en, min(30, len(official_en)))]
    for r in rng.sample(main, min(30, len(main))):
        words = r[1].split()
        corpus.append(("partial", words[0] if len(words) > 1 and len(words[0]) >= 3 else r[1][:rng.randint(4, 8)]))
    corpus += [("typo", typo(r[1])) for r in rng.sample(main, min(30, len(main)))]
    for r in rng.sample(japanese, min(20, len(japanese))):
        start = rng.randrange(0, max(1, len(r[1]) - 2))
        corpus.append(("japanese", r[1][start:start + rng.randint(2, 3)]))
    words = sorted({w.lower().strip(":,.!?") for r in official_en for w in r[1].split() if len(w) >= 4})
    corpus += [("word", w) for w in rng.sample(words, min(20, len(words)))]
    corpus += [("synonym", r[1]) for r in rng.sample(synonyms, min(10, len(synonyms)))]
    corpus += [("miss", "".join(rng.choice("qxzjvk") for _ in range(rng.randint(5, 9)))) for _ in range(10)]
    return [{"kind": k, "query": v} for k, v in corpus]

#endregion

#region Sections

def snap_search(api, out, corpus):
    writer = Writer(out, "search")
    routes = {
        "v3-fuzzy": lambda s: f"/api/v3/Series/Search?query={q(s)}&fuzzy=true&limit=50",
        "v3-exact": lambda s: f"/api/v3/Series/Search?query={q(s)}&fuzzy=false&limit=50",
        "v3-missing": lambda s: f"/api/v3/MissingEpisodes/Series?search={q(s)}&pageSize=0",
        "v3-duplicates": lambda s: f"/api/v3/DuplicateFiles/Series?search={q(s)}&pageSize=0",
        "v3-tmdb-show": lambda s: f"/api/v3/TMDB/Show?search={q(s)}&pageSize=50",
        "v3-tmdb-movie": lambda s: f"/api/v3/TMDB/Movie?search={q(s)}&pageSize=50",
        "v3-tmdb-collection": lambda s: f"/api/v3/TMDB/Movie/Collection?search={q(s)}&pageSize=50",
        "v2-serie-search": lambda s: f"/api/serie/search?query={q(s)}&limit=50&nocast=1&notag=1",
        # Every match: the server stops at the limit in whichever order its threads find them.
        "v2-startswith": lambda s: f"/api/serie/startswith?query={q(s)}&limit=100000&nocast=1&notag=1",
        "v2-search": lambda s: f"/api/search?query={q(s)}&limit=50&nocast=1&notag=1",
        "v2-group-search": lambda s: f"/api/group/search?query={q(s)}&limit=50&nocast=1&notag=1",
    }
    tasks = [(index, entry, route, path) for index, entry in enumerate(corpus) for route, path in routes.items()]
    results = fan_out(api, lambda task: settle_ties(prune(api.get(task[3](task[1]["query"]), family=task[2]))), tasks)
    for (index, entry, route, _), result in zip(tasks, results):
        writer.write(f"{route}:{index:03d}", {"query": entry["query"], "result": result})
    return writer.close()


def settle_ties(value):
    """Orders each run of equally ranked search results (same match and distance) by ID. The server leaves the order
    of a tie to its threads or to hash order, so it can differ from one run to the next."""
    if isinstance(value, dict):
        return {key: settle_ties(item) for key, item in value.items()}
    if not isinstance(value, list):
        return value
    items = [settle_ties(item) for item in value]
    rank = lambda item: tuple(json.dumps(item.get(k)) for k in ("Match", "match", "Distance", "ExactMatch")) if isinstance(item, dict) else None
    if not any(isinstance(item, dict) and ("Match" in item or "match" in item) for item in items):
        return items
    ident = lambda item: json.dumps(item.get("IDs", {}).get("ID") if isinstance(item.get("IDs"), dict) else item.get("id"))
    settled, start = [], 0
    while start < len(items):
        end = start + 1
        while end < len(items) and rank(items[end]) == rank(items[start]):
            end += 1
        settled += sorted(items[start:end], key=ident)
        start = end
    return settled


def filter_ids(api):
    """Every filter preset, directories included, walked from the top."""
    found, stack = [], [f.get("IDs", {}).get("ID") for f in api.pages("/api/v3/Filter?includeEmpty=true&showHidden=true", 100)]
    while stack:
        filter_id = stack.pop()
        if filter_id is None or filter_id in found:
            continue
        found.append(filter_id)
        stack += [f.get("IDs", {}).get("ID") for f in api.pages(f"/api/v3/Filter/{filter_id}/Filter?includeEmpty=true&showHidden=true", 100)]
    return sorted(found)


def is_time_dependent(definition):
    """Whether a filter's conditions compare against today, so its members move with the date."""
    return '"Today"' in json.dumps(definition)


def snap_filters(api, out):
    writer = Writer(out, "filters")

    def read(filter_id):
        pages = api.pages(f"/api/v3/Filter/{filter_id}/Group?includeEmpty=true", 100, family="filter-groups")
        groups = [prune({k: g.get(k) for k in ("IDs", "Name", "SortName", "Size")}) for g in pages]
        # The members, sorted: the series order breaks ties between equal sort keys differently from run to run.
        series = api.get(f"/api/v3/Filter/{filter_id}/Series/OnlyIDs", family="filter-series")
        series = sorted(series) if isinstance(series, list) else series
        definition = api.get(f"/api/v3/Filter/{filter_id}?withConditions=true")
        return {"groups": groups, "series": series, "TimeDependent": is_time_dependent(definition)}

    ids = filter_ids(api)
    for filter_id, record in zip(ids, fan_out(api, read, ids)):
        writer.write(f"filter:{filter_id}", record)
    return writer.close()


def file_ids(db_path):
    db = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    ids = [row[0] for row in db.execute("SELECT VideoLocalID FROM VideoLocal ORDER BY VideoLocalID")]
    db.close()
    return ids


def resolve_preset(api, value):
    """A relocation preset's ID, from its ID or its name; None for the server's default preset."""
    if not value:
        return None
    presets = api.get("/api/v3/Relocation/Preset")
    for preset in presets if isinstance(presets, list) else []:
        if value in (preset.get("ID"), preset.get("Name")):
            return preset.get("ID")
    raise SystemExit(f"no relocation preset named {value}")


def relocation_preview(api, ids, preset, family):
    """Previews moving and renaming the files with a preset, or with the default preset when none is given."""
    if preset:
        return api.call(f"/api/v3/Relocation/Preset/{preset}/Preview?move=true&rename=true", body=ids, family=family)
    return api.call("/api/v3/Relocation/Preview?move=true&rename=true", body={"FileIDs": ids}, family=family)


def snap_relocation(api, out, ids, preset):
    writer = Writer(out, "relocation")
    batches = [ids[start:start + 250] for start in range(0, len(ids), 250)]
    previews = fan_out(api, lambda batch: relocation_preview(api, batch, preset, "relocation-preview"), batches)
    for batch, (status, result) in zip(batches, previews):
        if status != 200 or not isinstance(result, list):
            for file_id in batch:
                writer.write(f"file:{file_id}", {"status": status, "error": result})
            continue
        for item in result:
            writer.write(f"file:{item.get('FileID')}", {k: item.get(k) for k in ("IsSuccess", "ManagedFolderID", "RelativePath", "ErrorMessage")})
    return writer.close()


def snap_series(api, out):
    writer = Writer(out, "series")
    for series in api.pages("/api/v3/Series?includeDataFrom=AniDB,TMDB", 100, family="v3-series-list"):
        writer.write(f"series:{series.get('IDs', {}).get('ID')}", prune(series))
    return writer.close()


def snap_episodes(api, out):
    writer = Writer(out, "episodes")
    path = "/api/v3/Episode?includeMissing=true&includeUnaired=true&includeHidden=true&includeDataFrom=AniDB,TMDB"
    for episode in api.pages(path, 1000, family="v3-episode-list"):
        writer.write(f"episode:{episode.get('IDs', {}).get('ID')}", prune(episode))
    return writer.close()


def snap_groups(api, out):
    writer = Writer(out, "groups")
    for group in api.pages("/api/v3/Group?includeEmpty=true&topLevelOnly=false", 100, family="v3-group-list"):
        writer.write(f"group:{group.get('IDs', {}).get('ID')}", prune(group))
    return writer.close()


def snap_tmdb(api, out):
    """Shows, seasons, movies and collections with every title and overview; episodes with their preferred text and a
    digest of their full lists, which keeps the file small."""
    writer = Writer(out, "tmdb")
    shows = list(api.pages("/api/v3/TMDB/Show?include=Titles,Overviews", 1000, family="v3-tmdb-show-list"))
    for show in shows:
        writer.write(f"show:{show.get('ID')}", prune(show))
    collections = set()
    for movie in api.pages("/api/v3/TMDB/Movie?include=Titles,Overviews", 1000, family="v3-tmdb-movie-list"):
        writer.write(f"movie:{movie.get('ID')}", prune(movie))
        if movie.get("CollectionID"):
            collections.add(movie["CollectionID"])
    # The collection list insists on a search, so the collections are read one by one from the movies' links.
    collection_ids = sorted(collections)
    read_collection = lambda collection_id: prune(api.get(f"/api/v3/TMDB/Movie/Collection/{collection_id}?include=Titles,Overviews"))
    for collection_id, record in zip(collection_ids, fan_out(api, read_collection, collection_ids)):
        writer.write(f"collection:{collection_id}", record)

    def read_show(show):
        records = [(f"season:{season.get('ID')}", prune(season))
                   for season in api.pages(f"/api/v3/TMDB/Show/{show.get('ID')}/Season?include=Titles,Overviews", 100)]
        episodes = f"/api/v3/TMDB/Show/{show.get('ID')}/Episode?includeHidden=true&include=Titles,Overviews"
        records += [(f"episode:{episode.get('ID')}", {
            "Title": episode.get("Title"),
            "Overview": episode.get("Overview"),
            "Titles": digest(episode.get("Titles")),
            "Overviews": digest(episode.get("Overviews")),
        }) for episode in api.pages(episodes, 1000, family="v3-tmdb-show-episodes")]
        return records

    for records in fan_out(api, read_show, shows):
        for key, record in records:
            writer.write(key, record)
    return writer.close()


def snap_people(api, out):
    """AniDB creators and characters (names and descriptions), each series' cast, and each TMDB show's and movie's
    cast, which carries the TMDB people's names and biographies and the character names."""
    writer = Writer(out, "people")
    for creator in api.pages("/api/v3/AniDB/Creator", 1000):
        writer.write(f"anidb-creator:{creator.get('ID')}", prune(creator))
    for character in api.pages("/api/v3/AniDB/Character", 1000):
        writer.write(f"anidb-character:{character.get('ID')}", prune(character))
    series_ids = [series.get("IDs", {}).get("ID") for series in api.pages("/api/v3/Series", 100)]
    read_cast = lambda series_id: prune(api.get(f"/api/v3/Series/{series_id}/Cast", family="v3-series-cast"))
    for series_id, record in zip(series_ids, fan_out(api, read_cast, series_ids)):
        writer.write(f"series-cast:{series_id}", record)
    for kind in ("Show", "Movie"):
        entity_ids = [entity.get("ID") for entity in api.pages(f"/api/v3/TMDB/{kind}", 1000)]
        read_entity = lambda entity_id, kind=kind: prune(api.get(f"/api/v3/TMDB/{kind}/{entity_id}/Cast", family=f"v3-tmdb-{kind.lower()}-cast"))
        for entity_id, record in zip(entity_ids, fan_out(api, read_entity, entity_ids)):
            writer.write(f"tmdb-{kind.lower()}-cast:{entity_id}", record)
    return writer.close()


def snap_v2(api, out):
    writer = Writer(out, "v2")
    for kind, path, size in (("serie", "/api/serie?nocast=1&notag=1&level=0", 500), ("ep", "/api/ep?level=0", 2000)):
        offset = 0
        while True:
            items = api.get(f"{path}&limit={size}&offset={offset}", family=f"v2-{kind}-list")
            if not isinstance(items, list) or not items:
                break
            for item in items:
                writer.write(f"{kind}:{item.get('id')}", prune(item))
            offset += size
    groups = api.get("/api/group?nocast=1&notag=1&level=0", family="v2-group-list")
    for group in groups if isinstance(groups, list) else [groups]:
        writer.write(f"group:{group.get('id') if isinstance(group, dict) else None}", prune(group))
    return writer.close()


def snap_dashboard(api, out):
    writer = Writer(out, "dashboard")
    routes = ("RecentlyAddedEpisodes", "RecentlyAddedSeries", "ContinueWatchingEpisodes", "NextUpEpisodes", "SeriesSummary", "TopTags",
              "AniDBCalendar?showAll=true")
    for route in routes:
        writer.write(f"dashboard:{route}", prune(api.get(f"/api/v3/Dashboard/{route}")))
    return writer.close()

#endregion

#region Browse

def percentile(values, share):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, int(round(share * (len(ordered) - 1))))]


def counters(tool, pid, seconds=6):
    """The last value of each System.Runtime counter over a few seconds."""
    if not tool or not pid:
        return None
    path = f"/tmp/text-snapshots-counters-{pid}-{time.time_ns()}.json"
    subprocess.run([tool, "collect", "--process-id", str(pid), "--counters", "System.Runtime", "--format", "json", "--refresh-interval", "1",
                    "--duration", f"00:00:{seconds:02d}", "-o", path], check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        with open(path) as handle:
            text = handle.read().rstrip().rstrip(",")
        if not text.endswith("]}"):
            text += "]}"
        events = json.loads(text).get("Events", [])
    except (OSError, ValueError):
        return None
    finally:
        if os.path.exists(path):
            os.remove(path)
    last = {}
    for event in events:
        name = event.get("name", "")
        tags = event.get("tags")
        last[name + (f" [{tags}]" if tags else "")] = event.get("value")
    mebibytes = lambda prefix: round(sum(v for k, v in last.items() if k.startswith(prefix)) / 2 ** 20, 1)
    return {
        "gc_heap_mb": mebibytes("dotnet.gc.last_collection.heap.size"),
        "gc_committed_mb": mebibytes("dotnet.gc.last_collection.memory.committed_size"),
        "working_set_mb": mebibytes("dotnet.process.memory.working_set"),
        "raw": last,
    }


def browse(api, out, corpus, db_path, tool, pid, seed, preset):
    rng = random.Random(seed)
    report = {"before": counters(tool, pid)}
    series_ids = [s.get("IDs", {}).get("ID") for s in api.pages("/api/v3/Series", 100, family="v3-series-list")]
    for series_id in series_ids:
        api.get(f"/api/v3/Series/{series_id}?includeDataFrom=AniDB,TMDB", family="v3-series")
    db = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True)
    episode_ids = [row[0] for row in db.execute("SELECT AnimeEpisodeID FROM AnimeEpisode ORDER BY AnimeEpisodeID")]
    db.close()
    for episode_id in rng.sample(episode_ids, min(500, len(episode_ids))):
        api.get(f"/api/v3/Episode/{episode_id}?includeDataFrom=AniDB,TMDB", family="v3-episode")
    for page in range(1, 51):
        api.get(f"/api/v3/Episode?pageSize=100&page={page}&includeDataFrom=AniDB,TMDB", family="v3-episode-list")
    for filter_id in filter_ids(api):
        api.get(f"/api/v3/Filter/{filter_id}/Group?pageSize=100", family="v3-filter-group")
        definition = api.get(f"/api/v3/Filter/{filter_id}?withConditions=true")
        if isinstance(definition, dict) and not definition.get("IsDirectory"):
            api.call("/api/v3/Filter/Preview/Group?pageSize=100", body=definition, family="v3-filter-preview")
    for entry in corpus[:100]:
        api.get(f"/api/v3/Series/Search?query={q(entry['query'])}&fuzzy=true&limit=50", family="v3-series-search")
        api.get(f"/api/v3/TMDB/Show?search={q(entry['query'])}&pageSize=50", family="v3-tmdb-show-search")
        api.get(f"/api/v3/TMDB/Movie?search={q(entry['query'])}&pageSize=50", family="v3-tmdb-movie-search")
    ids = file_ids(db_path)
    for start in range(0, min(1000, len(ids)), 100):
        relocation_preview(api, ids[start:start + 100], preset, "relocation-preview-100")
    report["after"] = counters(tool, pid)
    report["timings"] = {
        family: {"count": len(values), "p50_ms": round(percentile(values, 0.5) * 1000, 1), "p95_ms": round(percentile(values, 0.95) * 1000, 1),
                 "mean_ms": round(statistics.fmean(values) * 1000, 1)}
        for family, values in sorted(api.timings.items())
    }
    with open(os.path.join(out, "browse.json"), "w") as handle:
        json.dump(report, handle, indent=2)
    return report

#endregion

#region Server

def start(home, cli, base, out, key_file):
    """Starts the server detached, waits for it to report Started, and prints the time taken."""
    os.makedirs(out, exist_ok=True)
    log = open(os.path.join(out, f"server-{time.strftime('%Y%m%d-%H%M%S')}.log"), "w")
    env = dict(os.environ, SHOKO_HOME=home)
    started = time.perf_counter()
    process = subprocess.Popen([cli], env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    with open(os.path.join(out, "server.pid"), "w") as handle:
        handle.write(str(process.pid))
    api = Api(base, key_file, timeout=10)
    state = None
    while process.poll() is None:
        try:
            status, body = api.call("/api/v3/Init/Status")
            state = body.get("State") if isinstance(body, dict) else None
        except OSError:
            state = None
        if state in ("Started", "Failed"):
            break
        time.sleep(0.25)
    elapsed = time.perf_counter() - started
    result = {"pid": process.pid, "state": state if process.poll() is None else f"exited {process.returncode}", "seconds_to_ready": round(elapsed, 2)}
    with open(os.path.join(out, "startup.json"), "a") as handle:
        handle.write(json.dumps(result) + "\n")
    print(json.dumps(result))


def stop(out):
    with open(os.path.join(out, "server.pid")) as handle:
        pid = int(handle.read().strip())
    try:
        os.kill(pid, signal.SIGTERM)
    except ProcessLookupError:
        print(f"{pid} is not running")
        return
    for _ in range(240):
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            print(f"stopped {pid}")
            return
        time.sleep(0.5)
    os.kill(pid, signal.SIGKILL)
    print(f"killed {pid}")

#endregion

#region Compare

def read_meta(folder):
    try:
        with open(os.path.join(folder, "meta.json"), encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError):
        return {}


def time_dependent(path):
    """The keys of a section's records that move with the date."""
    keys = set()
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            value = record["value"]
            if record["key"] in TIME_DEPENDENT_KEYS or (isinstance(value, dict) and value.get("TimeDependent")):
                keys.add(record["key"])
    return keys


def digests(path):
    """Each record's key and a digest of its line; the lines are written with sorted keys, so equal values hash
    equally, and a large section never has to be held in memory whole."""
    records = {}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            records[json.loads(line)["key"]] = hashlib.sha1(line.encode()).digest()
    return records


def values(path, keys):
    found = {}
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            record = json.loads(line)
            if record["key"] in keys:
                found[record["key"]] = record["value"]
    return found


def reordered_only(old_path, new_path, keys):
    """The filter records whose only difference is the order of the same groups. A group view carries no sort value,
    so groups whose sort keys tie cannot be settled by ID as search results are, and the server orders them
    differently from one run to the next."""
    was, now = values(old_path, set(keys)), values(new_path, set(keys))
    found = []
    for key in keys:
        a, b = was.get(key), now.get(key)
        if not (isinstance(a, dict) and isinstance(b, dict)) or {k: v for k, v in a.items() if k != "groups"} != {k: v for k, v in b.items() if k != "groups"}:
            continue
        if sorted(map(json.dumps, a.get("groups") or [])) == sorted(map(json.dumps, b.get("groups") or [])):
            found.append(key)
    return found


def compare(old, new, limit):
    old_date, new_date = read_meta(old).get("date"), read_meta(new).get("date")
    same_day = old_date is not None and old_date == new_date
    if not same_day:
        print(f"taken on {old_date or 'an unknown day'} and {new_date or 'an unknown day'}: date-dependent records are listed but not counted")
    total = 0
    for section in SECTIONS:
        old_path, new_path = os.path.join(old, f"{section}.jsonl"), os.path.join(new, f"{section}.jsonl")
        if not (os.path.exists(old_path) and os.path.exists(new_path)):
            continue
        before, after = digests(old_path), digests(new_path)
        missing = sorted(set(before) - set(after))
        added = sorted(set(after) - set(before))
        changed = sorted(key for key in set(before) & set(after) if before[key] != after[key])
        dated = set() if same_day or section not in ("filters", "dashboard") else time_dependent(old_path) | time_dependent(new_path)
        drifted = [key for key in changed if key in dated]
        changed = [key for key in changed if key not in drifted]
        reordered = reordered_only(old_path, new_path, changed) if section == "filters" else []
        changed = [key for key in changed if key not in reordered]
        total += len(missing) + len(added) + len(changed)
        print(f"{section}: {len(before)} before, {len(after)} after, {len(changed)} changed, {len(missing)} missing, {len(added)} added"
              + (f", {len(drifted)} date-dependent changed (not counted)" if drifted else "")
              + (f", {len(reordered)} reordered only (not counted)" if reordered else ""))
        for key in drifted:
            print(f"  ? {key}")
        for key in reordered:
            print(f"  ? {key} (same groups, tied sort keys ordered differently)")
        shown = set(changed[:limit])
        old_values, new_values = values(old_path, shown), values(new_path, shown)
        for key in changed[:limit]:
            was, now = old_values.get(key), new_values.get(key)
            # Narrow a record down to the fields that differ, so the difference is not cut off.
            if isinstance(was, dict) and isinstance(now, dict):
                fields = sorted(field for field in set(was) | set(now) if was.get(field) != now.get(field))
                was, now = {f: was.get(f) for f in fields}, {f: now.get(f) for f in fields}
            print(f"  ~ {key}")
            print(f"    before: {json.dumps(was, ensure_ascii=False)[:400]}")
            print(f"    after:  {json.dumps(now, ensure_ascii=False)[:400]}")
        for key in missing[:limit]:
            print(f"  - {key}")
        for key in added[:limit]:
            print(f"  + {key}")
    print(f"total differences: {total}")
    return 1 if total else 0

#endregion


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("corpus", help="build the 200-query search corpus from a database copy")
    p.add_argument("--db", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--seed", type=int, default=20260926)

    for name in ("snapshot", "browse"):
        p = sub.add_parser(name)
        p.add_argument("--base", default="http://localhost:8133")
        p.add_argument("--key-file", required=True)
        p.add_argument("--out", required=True)
        p.add_argument("--corpus", required=True)
        p.add_argument("--db", required=True, help="a copy of the server's database, read for file and episode IDs")
        p.add_argument("--relocation-preset", help="the relocation preset (ID or name) to preview with; the default preset when left out")
        if name == "snapshot":
            p.add_argument("--sections", default=",".join(SECTIONS))
            p.add_argument("--workers", type=int, default=8, help="requests in flight at once per section (1 = one at a time)")
        else:
            p.add_argument("--pid", type=int)
            p.add_argument("--counters", help="path to dotnet-counters")
            p.add_argument("--seed", type=int, default=20260926)

    p = sub.add_parser("start")
    p.add_argument("--home", required=True)
    p.add_argument("--cli", required=True, help="path to the Shoko.CLI executable")
    p.add_argument("--base", default="http://localhost:8133")
    p.add_argument("--key-file", required=True)
    p.add_argument("--out", required=True)

    p = sub.add_parser("stop")
    p.add_argument("--out", required=True)

    p = sub.add_parser("compare")
    p.add_argument("old")
    p.add_argument("new")
    p.add_argument("--limit", type=int, default=10)

    args = parser.parse_args()
    if args.command == "corpus":
        corpus = build_corpus(args.db, args.seed)
        with open(args.out, "w", encoding="utf-8") as handle:
            json.dump(corpus, handle, ensure_ascii=False, indent=1)
        print(f"{len(corpus)} queries")
    elif args.command in ("snapshot", "browse"):
        os.makedirs(args.out, exist_ok=True)
        with open(args.corpus, encoding="utf-8") as handle:
            corpus = json.load(handle)
        api = Api(args.base, args.key_file)
        if args.command == "snapshot":
            api.workers = max(1, args.workers)
            # The day the snapshot was taken on, for the records that depend on it, and the build it was taken from.
            version = api.get("/api/v3/Init/Version")
            with open(os.path.join(args.out, "meta.json"), "w", encoding="utf-8") as handle:
                json.dump({"date": time.strftime("%Y-%m-%d"), "server": version.get("Server") if isinstance(version, dict) else version}, handle, indent=2)
        if args.command == "browse":
            report = browse(api, args.out, corpus, args.db, args.counters, args.pid, args.seed, resolve_preset(api, args.relocation_preset))
            print(json.dumps(report["timings"], indent=1))
            return 0
        for section in args.sections.split(","):
            started = time.perf_counter()
            count = {
                "search": lambda: snap_search(api, args.out, corpus),
                "filters": lambda: snap_filters(api, args.out),
                "relocation": lambda: snap_relocation(api, args.out, file_ids(args.db), resolve_preset(api, args.relocation_preset)),
                "series": lambda: snap_series(api, args.out),
                "episodes": lambda: snap_episodes(api, args.out),
                "groups": lambda: snap_groups(api, args.out),
                "tmdb": lambda: snap_tmdb(api, args.out),
                "people": lambda: snap_people(api, args.out),
                "v2": lambda: snap_v2(api, args.out),
                "dashboard": lambda: snap_dashboard(api, args.out),
            }[section]()
            print(f"{section}: {count} records in {time.perf_counter() - started:.0f}s", flush=True)
    elif args.command == "start":
        start(args.home, args.cli, args.base, args.out, args.key_file)
    elif args.command == "stop":
        stop(args.out)
    elif args.command == "compare":
        return compare(args.old, args.new, args.limit)
    return 0


if __name__ == "__main__":
    sys.exit(main())
