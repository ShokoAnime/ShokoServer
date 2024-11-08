FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG version
ARG channel
ARG commit
ARG tag
ARG date

#MAINTAINER Cayde Dixon <me@cazzar.net>

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source

RUN dotnet build -c=Release -r linux-x64 -f net8.0 -o=/usr/src/app/build/ Shoko.CLI/Shoko.CLI.csproj /p:Version="${version}" /p:InformationalVersion="\"channel=${channel},commit=${commit},tag=${tag},date=${date},\""

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ENV PUID=1000 \
    PGID=100 \
    LANG=C.UTF-8 \
    LC_CTYPE=C.UTF-8 \
    LC_ALL=C.UTF-8

ARG channel

RUN apt-get update && apt-get install -y gnupg curl

RUN curl --retry 3 -O https://mediaarea.net/repo/deb/debian/pubkey.gpg
# This converts the old format gpg key to the new format. The old format cannot be used with [signed-by] in the apt sources.list file.
RUN gpg --no-default-keyring --keyring ./temp-keyring.gpg --import pubkey.gpg && gpg --no-default-keyring --keyring ./temp-keyring.gpg --export --output /usr/share/keyrings/mediainfo.gpg && rm pubkey.gpg
RUN echo "deb [signed-by=/usr/share/keyrings/mediainfo.gpg] https://mediaarea.net/repo/deb/debian/ bookworm main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y apt-utils gosu jq unzip mediainfo librhash-dev

WORKDIR /usr/src/app/build
COPY --from=build /usr/src/app/build .
COPY ./dockerentry.sh /dockerentry.sh

# Create a sub-scope where we navigate to the web ui directory, then download the needed web ui archive, extract it, and remove the archive.
RUN (\
        cd /usr/src/app/build/webui;\
        if [ "${channel:-dev}" = "stable" ]; then\
            curl -L $(curl https://api.github.com/repos/ShokoAnime/Shoko-WebUI/releases/latest | jq -r '.assets[0].browser_download_url') -o latest.zip;\
        else\
            curl -L $(curl https://api.github.com/repos/ShokoAnime/Shoko-WebUI/releases | jq -r '.[0].assets[0].browser_download_url') -o latest.zip;\
        fi;\
        unzip -o latest.zip;\
        rm latest.zip\
    )

VOLUME /home/shoko/.shoko/

HEALTHCHECK --start-period=5m CMD curl -s -H "Content-Type: application/json" -H 'Accept: application/json' 'http://localhost:8111/api/v3/Init/Status' || exit 1

EXPOSE 8111

ENTRYPOINT ["/bin/bash", "/dockerentry.sh"]
