# Cross-backend schema comparison

The three supported backends each keep their own hand-written DDL in `Shoko.Server/Databases/`, and
nothing forces them to agree. `SchemaTypeParityTests` in `Shoko.Tests` does: same tables, same
columns, same type, width and nullability for every column.

It compares schemas read from the catalogs of real databases — one per backend, migrated from empty.
Nothing is committed, because a recorded schema is a copy that can quietly fall behind the migrations
it claims to describe. The dumps are produced at runtime instead:

- `Shoko.IntegrationTests` → `SchemaSnapshotTests` migrates a database and writes
  `schema-<backend>.json` into the directory named by `SHOKO_SCHEMA_DIR`.
- CI runs that once per backend, publishes each dump, then runs the comparison over all three.
- Without all three dumps the comparison has nothing to compare and skips, rather than passing.

The DDL is not simply replayed instead, because a replay cannot see the whole migration: MySQL
performs some of its through `PREPARE stmt FROM @sqlstmt`, and every backend has migrations written
in C# rather than SQL. Only the migrated database knows the real answer.

## Running it locally

`scripts/compare_schemas.sh` does the whole thing: starts MariaDB and SQL Server in Docker, migrates
all three backends, and runs the comparison.

```bash
scripts/compare_schemas.sh
```

To do it by hand, migrate each backend into the same directory and then point the comparison at it:

```bash
export SHOKO_SCHEMA_DIR=/tmp/shoko-schemas
DB_TYPE=SQLite dotnet test Shoko.IntegrationTests/Shoko.IntegrationTests.csproj -c Release --filter SchemaSnapshotTests
DB_TYPE=MySQL DB_HOST=127.0.0.1 DB_USER=root DB_PASS=root DB_NAME=shoko \
  dotnet test Shoko.IntegrationTests/Shoko.IntegrationTests.csproj -c Release --filter SchemaSnapshotTests
DB_TYPE=SQLServer DB_HOST=127.0.0.1 DB_USER=sa DB_PASS='ShokoTest1!' DB_NAME=shoko \
  dotnet test Shoko.IntegrationTests/Shoko.IntegrationTests.csproj -c Release --filter SchemaSnapshotTests

dotnet test Shoko.Tests/Shoko.Tests.csproj -c Release --filter SchemaTypeParityTests
```

Each backend must start from an empty database, or the dump describes a schema nobody will ever
migrate into. `mediainfo` and `librhash-dev` are needed for the server to boot.
