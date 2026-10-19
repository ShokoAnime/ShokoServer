# Third-party notices

Shoko Server is MIT licensed (see [LICENSE](LICENSE)). It uses the following
third-party data, code and libraries, each under its own license.

## Data sources

- **AniDB.** Anime metadata comes from AniDB, https://anidb.net/, and is
  available under CC BY-NC-SA 4.0
  (https://creativecommons.org/licenses/by-nc-sa/4.0/). Use of AniDB data is
  non-commercial and subject to AniDB's policies (https://anidb.net/policy). Shoko is not
  affiliated with AniDB.
- **TMDB.** This product uses TMDB and the TMDB APIs but is not endorsed,
  certified, or otherwise approved by TMDB. See https://www.themoviedb.org/

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

Rows with an empty license column are MIT.

| License | Package |
|---|---|
| LGPL-2.1-only | NHibernate |
| LGPL-2.1-only | NHibernate.Driver.MySqlConnector |
| Apache-2.0 | Magick.NET-Q8-AnyCPU (ImageMagick) |
| Apache-2.0 | Microsoft.AspNetCore.ResponseCaching |
| Apache-2.0 | SQLitePCLRaw.bundle_e_sqlite3 |
| Apache-2.0 | SQLitePCLRaw.core |
| Apache-2.0 | SQLitePCLRaw.provider.e_sqlite3 |
| BSD-3-Clause | FluentNHibernate |
| BSD-3-Clause | NLog |
| BSD-3-Clause | NLog.Web.AspNetCore |
| BSD-3-Clause | Polly |
| BSD-2-Clause | NLog.Extensions.Logging |
| CPOL, GPL-3.0-or-later or BSD-2-Clause (your choice) | Trinet.Core.IO.Ntfs |
| Unlicense | MySqlBackup.NET.MySqlConnector |
|  | Asp.Versioning.Mvc |
|  | Asp.Versioning.Mvc.ApiExplorer |
|  | AsyncKeyedLock |
|  | Avalonia |
|  | Avalonia.Desktop |
|  | Avalonia.Themes.Fluent |
|  | DeepCloner |
|  | F23.StringSimilarity |
|  | JsonDiffPatch.Net |
|  | MessagePack |
|  | Microsoft.ApplicationInsights.AspNetCore |
|  | Microsoft.AspNetCore.Mvc.NewtonsoftJson |
|  | Microsoft.AspNetCore.SignalR.Protocols.NewtonsoftJson |
|  | Microsoft.Data.SqlClient |
|  | Microsoft.Data.Sqlite |
|  | Microsoft.EntityFrameworkCore |
|  | Microsoft.EntityFrameworkCore.Design |
|  | Microsoft.EntityFrameworkCore.Sqlite |
|  | Microsoft.EntityFrameworkCore.SqlServer |
|  | Microsoft.Extensions.DependencyInjection.Abstractions |
|  | Microsoft.Extensions.Hosting.Abstractions |
|  | Microsoft.Extensions.Logging.Abstractions |
|  | Microting.EntityFrameworkCore.MySql |
|  | Mono.Posix.NETStandard |
|  | MySqlConnector |
|  | Newtonsoft.Json |
|  | Nito.AsyncEx |
|  | NJsonSchema |
|  | NJsonSchema.Annotations |
|  | NJsonSchema.NewtonsoftJson |
|  | Sentry |
|  | Sentry.AspNetCore |
|  | Sentry.Extensions.Logging |
|  | Sentry.NLog |
|  | SharpCompress |
|  | SharpZipLib |
|  | Swashbuckle.AspNetCore |
|  | Swashbuckle.AspNetCore.Newtonsoft |
|  | Swashbuckle.AspNetCore.SwaggerUI |
|  | System.Security.Cryptography.Pkcs |
|  | TaskScheduler |
|  | Tmds.DBus.Protocol |
