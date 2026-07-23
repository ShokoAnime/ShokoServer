# This Dockerfile expects pre-built artifacts staged into ./publish/<arch>/ before building.
# Compilation happens outside Docker in CI (see .github/workflows/build-daily.yml, docker-build job).
#
# Why outside Docker (current approach):
#   - No SDK image (~800MB) needed at build time — runtime image only.
#   - NuGet restore is cached at the runner level (actions/cache on ~/.nuget/packages),
#     which is faster and shared across all build jobs in the same workflow run.
#   - A single Dockerfile serves both amd64 and arm64; platform differences are
#     handled in the dotnet publish step, not in Docker.
#   - Eliminates Dockerfile.aarch64 and its /maxcpucount:1 workaround.
#
# Why inside Docker would be better (alternative):
#   - Local `docker build` is fully self-contained — no SDK required on the host.
#   - The image is reproducible from source without matching the CI publish flags.
#   - Downside: the CI workflow becomes the source of truth for build flags;
#     local builds require manually replicating the dotnet publish invocation.
#
# To build locally:
#   dotnet publish -c Release -r linux-x64 -f net10.0 --no-self-contained Shoko.CLI \
#     /p:Version="0.0.0.1" \
#     /p:InformationalVersion="\"channel=dev,commit=local,tag=v0,date=$(date -u +%Y-%m-%dT%H:%M:%SZ),\""
#   mkdir -p publish/amd64
#   cp -r Shoko.Server/bin/Release/net10.0/linux-x64/publish/. ./publish/amd64/
#   docker build --build-arg TARGETARCH=amd64 -t shoko:local .
#   (For arm64: use -r linux-arm64, publish/arm64/, and TARGETARCH=arm64.)

FROM mcr.microsoft.com/dotnet/aspnet:10.0

ENV PUID=1000 \
    PGID=100 \
    LANG=C.UTF-8 \
    LC_CTYPE=C.UTF-8 \
    LC_ALL=C.UTF-8

RUN apt-get update \
    && apt-get install -y --no-install-recommends gnupg curl \
    && curl --retry 3 -O https://mediaarea.net/repo/deb/debian/pubkey.gpg \
    && gpg --no-default-keyring --keyring ./temp-keyring.gpg --import pubkey.gpg \
    # This converts the old format gpg key to the new format. The old format cannot be used with [signed-by] in the apt sources.list file.
    && gpg --no-default-keyring --keyring ./temp-keyring.gpg --export --output /usr/share/keyrings/mediainfo.gpg \
    && rm pubkey.gpg \
    && echo "deb [signed-by=/usr/share/keyrings/mediainfo.gpg] https://mediaarea.net/repo/deb/debian/ bookworm main" | tee -a /etc/apt/sources.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends gosu mediainfo librhash-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /usr/src/app/build

# TARGETARCH is provided automatically by BuildKit for multi-platform builds (values: amd64, arm64).
# CI stages pre-built artifacts under publish/amd64/ and publish/arm64/ before docker build.
ARG TARGETARCH
COPY ./publish/${TARGETARCH} .
COPY ./dockerentry.sh /dockerentry.sh

VOLUME /home/shoko/.shoko/

HEALTHCHECK --start-period=5m CMD curl -s -H "Content-Type: application/json" -H "Accept: application/json" "http://localhost:8111/api/v3/Init/Status" || exit 1

EXPOSE 8111

ENTRYPOINT ["/bin/bash", "/dockerentry.sh"]
