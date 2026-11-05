# OpenTelemetry export

Shoko Server can send its traces, metrics and logs over OTLP to a collector you run, such as Grafana Alloy. It is for your
own observability: nothing is sent anywhere else, no address is built in, and it has nothing to do with Sentry.

Everything is off by default. Setting an endpoint alone exports nothing, since each signal has its own switch.

## Settings

Under `Telemetry` in `settings-server.json`. Every setting needs a restart, except the log level.

| Setting | Default | Environment variable | |
|---|---|---|---|
| `Endpoint` | empty (off) | `SHOKO_OTLP_ENDPOINT` | The collector's base address. No credentials, query or fragment. |
| `Protocol` | `HttpProtobuf` | `SHOKO_OTLP_PROTOCOL` | `HttpProtobuf` (usually port 4318) or `Grpc` (usually port 4317). |
| `Headers` | empty | `SHOKO_OTLP_HEADERS` | `key=value,key=value`, sent with every export. |
| `ExportTimeoutMs` | `5000` | `SHOKO_OTLP_TIMEOUT_MS` | How long one export may take. |
| `ServiceName` | `shoko-server` | `SHOKO_OTLP_SERVICE_NAME` | The `service.name` of everything sent. |
| `TracesEnabled` | `false` | `SHOKO_OTLP_TRACES_ENABLED` | Export spans. |
| `MetricsEnabled` | `false` | `SHOKO_OTLP_METRICS_ENABLED` | Export metrics. |
| `LogsEnabled` | `false` | `SHOKO_OTLP_LOGS_ENABLED` | Export the server's logs from this start on, early start-up lines included. Existing log files are not sent. |
| `TraceSampleRatio` | `1.0` | `SHOKO_OTLP_TRACE_SAMPLE_RATIO` | The share of traces kept, from 0 to 1. |
| `JobTraceSampleRatio` | `1.0` | `SHOKO_OTLP_JOB_TRACE_SAMPLE_RATIO` | The same for queue jobs, each attempt on its own. Job metrics count every job either way. |
| `MetricExportIntervalSeconds` | `60` | `SHOKO_OTLP_METRIC_INTERVAL_SECONDS` | How often metrics are sent. |
| `DatabaseSpansEnabled` | `false` | `SHOKO_OTLP_DB_SPANS_ENABLED` | A span and a duration for every statement. Busy. |
| `CaptureDatabaseStatements` | `true` | `SHOKO_OTLP_DB_STATEMENTS_ENABLED` | Keep each statement's text on its span, such as `SELECT * FROM AniDB_Anime WHERE AnimeID = ?`, with placeholders for its values. Only with `DatabaseSpansEnabled`. |

`Logging.DefaultOtlpLogLevel` (`LOGGING_OTLP_LOG_LEVEL`, Information by default) sets which logs are exported, and applies
at once.

A bad value logs a warning and turns the export off; it never stops the server from starting. The standard
`OTEL_EXPORTER_OTLP_*` environment variables are ignored, so a platform that injects them cannot turn this on or redirect it.

## What is sent

**Spans**

| Name | |
|---|---|
| `shoko.server.startup`, `shoko.server.late_start` | The two stages of starting. |
| API requests, outgoing HTTP calls | ASP.NET Core and `HttpClient` instrumentation. `/signalr` and requests no route took are left out. |
| `shoko.job {JobType}` | One attempt of a queue job, linked to the span that queued it. |
| `shoko.job.queue {JobType}` | A job being queued, with its type, priority and whether it is scheduled, under the span queuing it. Only when there is one, and never its arguments. A job already queued can still report this, as it is reported before the queue drops the duplicate. |
| `anidb.udp {COMMAND}`, `anidb.http` | A request to AniDB, without the wait for its turn. |
| `video.hash`, `video.hash {Provider}` | Hashing a file, and under it each hash provider's work with the hash types it was asked for. |
| `release.search`, `image.download` | Searching for a file's release, fetching an image. |
| `SELECT AnimeSeries`, `INSERT AniDB_Anime`, … | A statement, with `DatabaseSpansEnabled`, carrying its table, database and server. It ends when the statement returns, so reading a large result is not counted. |
| `repository.populate {Entity}`, `repository.save {Entity}`, `repository.delete {Entity}` | A cached repository's work around its statements, with `DatabaseSpansEnabled`. Reads come from memory and get no span of their own: the request or job span they are part of carries `shoko.repository.reads` and `shoko.repository.reads.by_entity` (`AnimeSeries=12,AniDB_Anime=3`). Reads outside any span are only counted in the metric. |
| `cache.lookup` | A lookup in the web UI and server manifest caches (`webui-manifest.json`, …) or the AniDB release cache (`anidb-release`), with `shoko.cache.name` and `shoko.cache.result`: `hit`, `miss`, or `stale` for an expired copy used because the fetch failed. A fetch on a miss is a child span. |

**Metrics**

| Meter | Instruments |
|---|---|
| `Shoko.QueueProcessor` | `shoko.queue.job.duration`, `shoko.queue.jobs.waiting`, `.blocked`, `.scheduled`, `.executing`, `shoko.queue.workers.limit` |
| `Shoko.Server.AniDB` | `shoko.anidb.request.duration`, `shoko.anidb.rate_limit.wait.duration`, `shoko.anidb.bans` (an empty UDP reply is assumed to be a ban, so this can count lost connections) |
| `Shoko.Server` | `shoko.video.hash.duration`, `shoko.video.hash.size`, `shoko.release.search.duration`, `shoko.image.download.duration`, `shoko.suspension.active`, `shoko.cache.lookups` (by `shoko.cache.name` and `shoko.cache.result`) |
| `Shoko.Server.Database` | `db.client.operation.duration` and `shoko.repository.reads` (by `shoko.repository.entity` and `shoko.repository.found`), with `DatabaseSpansEnabled`. The repositories hold every row, so a read that found nothing means no such row, not a trip to the database. |
| Built in | `System.Runtime`, `System.Net.Http`, `System.Net.NameResolution`, `MySqlConnector`, and ASP.NET Core's hosting, Kestrel, routing, diagnostics, connections, authentication and authorization meters |

Plugins following the `Shoko.*` naming convention are exported too.

## What is kept out

- No `traceparent` goes to AniDB, TMDB, Plex or anything else the server calls.
- Spans and metrics hold no file paths, job arguments, UDP arguments or exception messages. Failures report the exception's type.
- Outgoing URLs keep their query, so a span shows what was asked for, such as `httpapi?client=animeplugin&request=anime&aid=1`. A parameter whose name suggests a secret or a person (`pass`, `user`, `api_key`, `token`, `session` and the like) has its value hidden; a secret under a name that does not say so stays. Incoming requests keep their route and path, with the values of their query redacted.
- Known secret parameters are hidden in statements and log messages.
- Statements carry placeholders rather than values: string and number literals in hand-written SQL are replaced too. Double-quoted text is left alone, so a MySQL statement writing a value in double quotes keeps it.
- `Settings.*` log lines are not exported.

Logs are exported as written, apart from those secrets. A stack trace in a log is not scrubbed, so treat your collector as
you would your log files.

## Grafana Alloy

Point the server at Alloy:

```json
{
  "Telemetry": {
    "Endpoint": "http://alloy:4318",
    "TracesEnabled": true,
    "MetricsEnabled": true,
    "LogsEnabled": true
  }
}
```

And have Alloy pass it on, here to anything taking OTLP over HTTP:

```alloy
otelcol.receiver.otlp "shoko" {
  http {
    endpoint = "0.0.0.0:4318"
  }

  output {
    traces  = [otelcol.processor.batch.shoko.input]
    metrics = [otelcol.processor.batch.shoko.input]
    logs    = [otelcol.processor.batch.shoko.input]
  }
}

otelcol.processor.batch "shoko" {
  output {
    traces  = [otelcol.exporter.otlphttp.backend.input]
    metrics = [otelcol.exporter.otlphttp.backend.input]
    logs    = [otelcol.exporter.otlphttp.backend.input]
  }
}

otelcol.exporter.otlphttp "backend" {
  client {
    endpoint = "http://otel-backend:4318"
  }
}
```
