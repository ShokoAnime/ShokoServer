# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG TARGETARCH
ARG version
ARG channel
ARG commit
ARG tag
ARG date

WORKDIR /usr/src/app/source

# Copy only project manifests first so the NuGet restore layer is cached
# independently from source changes. Only the four projects in Shoko.CLI's
# transitive reference chain are needed here — test, bench, and tray projects
# are never referenced by CLI so they do not affect restore.
COPY Shoko.Abstractions/Shoko.Abstractions.csproj Shoko.Abstractions/
COPY Shoko.QueueProcessor/Shoko.QueueProcessor.csproj Shoko.QueueProcessor/
COPY Shoko.Server/Shoko.Server.csproj Shoko.Server/
COPY Shoko.CLI/Shoko.CLI.csproj Shoko.CLI/

# Restore NuGet packages. TARGETARCH is mapped to a .NET RID:
#   amd64 → linux-x64   (CI host platform, native)
#   arm64 → linux-arm64 (cross-compiled from the amd64 build stage)
# FROM --platform=$BUILDPLATFORM keeps this stage on the host so
# cross-compilation is handled by dotnet, not QEMU emulation.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    case "$TARGETARCH" in \
        amd64) RID="linux-x64" ;; \
        arm64) RID="linux-arm64" ;; \
        *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2 && exit 1 ;; \
    esac && \
    dotnet restore -r "$RID" Shoko.CLI/Shoko.CLI.csproj

COPY . .

# Publish. --no-restore skips redundant package resolution against the cache
# populated above. /maxcpucount:1 is applied for arm64 as a workaround for
# https://github.com/dotnet/sdk/issues/2902 (cross-compile CPU contention).
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    case "$TARGETARCH" in \
        amd64) RID="linux-x64" ; EXTRA="" ;; \
        arm64) RID="linux-arm64" ; EXTRA="/maxcpucount:1" ;; \
        *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2 && exit 1 ;; \
    esac && \
    dotnet publish -c Release -r "$RID" -f net10.0 --no-restore \
        -o /usr/src/app/build \
        Shoko.CLI/Shoko.CLI.csproj \
        /p:Version="${version}" \
        /p:InformationalVersion="\"channel=${channel},commit=${commit},tag=${tag},date=${date},\"" \
        $EXTRA

FROM mcr.microsoft.com/dotnet/aspnet:10.0

LABEL org.opencontainers.image.source="https://github.com/ShokoAnime/ShokoServer" \
      org.opencontainers.image.description="Shoko Server" \
      org.opencontainers.image.licenses="MIT"

ENV PUID=1000 \
    PGID=100 \
    LANG=C.UTF-8 \
    LC_CTYPE=C.UTF-8 \
    LC_ALL=C.UTF-8

RUN apt-get update \
    && apt-get install -y --no-install-recommends gnupg curl \
    && curl -fsSL --retry 3 https://mediaarea.net/repo/deb/debian/pubkey.gpg \
        | gpg --no-default-keyring --keyring ./temp-keyring.gpg --import \
    && gpg --no-default-keyring --keyring ./temp-keyring.gpg --export \
        --output /usr/share/keyrings/mediainfo.gpg \
    && rm -f ./temp-keyring.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/mediainfo.gpg] https://mediaarea.net/repo/deb/debian/ bookworm main" \
        > /etc/apt/sources.list.d/mediainfo.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends apt-utils gosu mediainfo librhash-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /usr/src/app/build
COPY --from=build /usr/src/app/build .
COPY ./dockerentry.sh /dockerentry.sh

VOLUME /home/shoko/.shoko/

HEALTHCHECK --start-period=5m \
    CMD curl -sf \
        -H "Content-Type: application/json" \
        -H "Accept: application/json" \
        http://localhost:8111/api/v3/Init/Status || exit 1

EXPOSE 8111

ENTRYPOINT ["/bin/bash", "/dockerentry.sh"]
