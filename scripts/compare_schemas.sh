#!/usr/bin/env bash
#
# Migrates a real database of each supported backend and compares the three schemas against each
# other. This is what CI does across its backend matrix; here it runs on one machine, with MariaDB
# and SQL Server in Docker.
#
# Needs docker, the .NET SDK, mediainfo and librhash-dev.

set -euo pipefail

cd "$(dirname "$0")/.."

export SHOKO_SCHEMA_DIR="${SHOKO_SCHEMA_DIR:-$(mktemp -d)}"
MYSQL_PASS=root
MSSQL_PASS='ShokoTest1!'
KEEP="${KEEP_CONTAINERS:-0}"

cleanup() {
    [ "$KEEP" = "1" ] || docker rm -f shoko-schema-maria shoko-schema-mssql >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "==> starting databases"
docker rm -f shoko-schema-maria shoko-schema-mssql >/dev/null 2>&1 || true
docker run -d --name shoko-schema-maria -e MARIADB_ROOT_PASSWORD="$MYSQL_PASS" -e MARIADB_DATABASE=shoko \
    -p 3306:3306 mariadb:lts >/dev/null
docker run -d --name shoko-schema-mssql -e SA_PASSWORD="$MSSQL_PASS" -e ACCEPT_EULA=Y -e MSSQL_PID=Express \
    -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest >/dev/null

echo -n "==> waiting for them to accept connections"
for _ in $(seq 1 60); do
    if docker exec shoko-schema-maria mariadb -uroot -p"$MYSQL_PASS" -e "SELECT 1" >/dev/null 2>&1 &&
       docker exec shoko-schema-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_PASS" -Q "SELECT 1" -No >/dev/null 2>&1; then
        echo " ok"
        break
    fi
    echo -n "."
    sleep 2
done

# Every backend has to start empty, or the dump describes a schema nobody will ever migrate into.
docker exec shoko-schema-maria mariadb -uroot -p"$MYSQL_PASS" \
    -e "DROP DATABASE IF EXISTS shoko; CREATE DATABASE shoko DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;" >/dev/null
docker exec shoko-schema-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_PASS" -No \
    -Q "IF DB_ID('shoko') IS NOT NULL BEGIN ALTER DATABASE shoko SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE shoko; END; CREATE DATABASE shoko" >/dev/null

dump() {
    echo "==> migrating $1"
    DB_TYPE="$1" DB_HOST=127.0.0.1 DB_USER="${2:-}" DB_PASS="${3:-}" DB_NAME=shoko \
        dotnet test Shoko.IntegrationTests/Shoko.IntegrationTests.csproj -c Release \
        --filter "FullyQualifiedName~SchemaSnapshotTests" --nologo
}

dump SQLite
dump MySQL root "$MYSQL_PASS"
dump SQLServer sa "$MSSQL_PASS"

echo "==> comparing"
dotnet test Shoko.Tests/Shoko.Tests.csproj -c Release --filter "FullyQualifiedName~SchemaTypeParityTests" --nologo
