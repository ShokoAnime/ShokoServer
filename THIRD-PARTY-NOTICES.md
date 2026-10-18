# Third-party notices

Shoko Server is MIT licensed (see [LICENSE](LICENSE)). It uses the following
third-party data, code and libraries, each under its own license.

## Data sources

- **TMDB.** This product uses TMDB and the TMDB APIs but is not endorsed,
  certified, or otherwise approved by TMDB. See https://www.themoviedb.org/
- **AniDB.** Anime metadata comes from AniDB, https://anidb.net/. Shoko is not
  affiliated with AniDB.

## Code and binaries in this repository or its builds

| Component | License | Notes |
|---|---|---|
| `Shoko.Server/Utilities/PocoCache.cs` | MIT | Copyright (c) 2016 Máximo Piva. The license text is in the file header. |
| RHash (`Dependencies/*/librhash.dll`, system `librhash` on Linux) | 0BSD | https://github.com/rhash/RHash |
| libcurl 7.47.1 (`Dependencies/*/libcurl.dll`) | curl license (MIT-style) | Copyright Daniel Stenberg and contributors. https://curl.se/docs/copyright.html |
| MediaInfo CLI (downloaded into Windows builds, system `mediainfo` elsewhere) | BSD-2-Clause | Copyright MediaArea.net SARL. https://mediaarea.net/ |

## NuGet packages

Package licenses are taken from each package's NuGet metadata. Each package
ships its own license text and notices, which stay with it in the build output.

**Copyleft.** NHibernate and NHibernate.Driver.MySqlConnector are LGPL-2.1.
Their full license text is included with the packages.

| License | Packages |
|---|---|
| LGPL-2.1-only | NHibernate, NHibernate.Driver.MySqlConnector |
| Apache-2.0 | Magick.NET-Q8-AnyCPU (ImageMagick), Microsoft.AspNetCore.ResponseCaching, SQLitePCLRaw.bundle_e_sqlite3, SQLitePCLRaw.core, SQLitePCLRaw.provider.e_sqlite3 |
| BSD-3-Clause | FluentNHibernate, NLog, NLog.Web.AspNetCore, Polly |
| BSD-2-Clause | NLog.Extensions.Logging |
| CPOL, GPL-3.0-or-later or BSD-2-Clause (your choice) | Trinet.Core.IO.Ntfs |
| Unlicense | MySqlBackup.NET.MySqlConnector |
| MIT | Asp.Versioning.Mvc, Asp.Versioning.Mvc.ApiExplorer, AsyncKeyedLock, Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, DeepCloner, F23.StringSimilarity, JsonDiffPatch.Net, MessagePack, Microsoft.ApplicationInsights.AspNetCore, Microsoft.AspNetCore.Mvc.NewtonsoftJson, Microsoft.AspNetCore.SignalR.Protocols.NewtonsoftJson, Microsoft.Data.SqlClient, Microsoft.Data.Sqlite, Microsoft.EntityFrameworkCore (and .Design, .Sqlite, .SqlServer), Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Hosting.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microting.EntityFrameworkCore.MySql, Mono.Posix.NETStandard, MySqlConnector, Newtonsoft.Json, Nito.AsyncEx, NJsonSchema (and .Annotations, .NewtonsoftJson), Sentry (and .AspNetCore, .Extensions.Logging, .NLog), SharpCompress, SharpZipLib, Swashbuckle.AspNetCore (and .Newtonsoft, .SwaggerUI), System.Security.Cryptography.Pkcs, TaskScheduler, Tmds.DBus.Protocol |
