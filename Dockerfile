FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

ARG version
ARG channel
ARG commit
ARG tag
ARG date

#MAINTAINER Cayde Dixon <me@cazzar.net>

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source

RUN dotnet build -c=Release -o=/usr/src/app/build/ Shoko.CLI/Shoko.CLI.csproj "/p:Version=\"${version}\",InformationalVersion=\"channel=${channel},commit=${commit},tag=${tag},date=${date}\""

FROM mcr.microsoft.com/dotnet/aspnet:6.0
ENV PUID=1000 \
    PGID=100 \
    LANG=C.UTF-8 \
    LC_CTYPE=C.UTF-8 \
    LC_ALL=C.UTF-8

RUN apt-get update && apt-get install -y gnupg curl

RUN curl https://mediaarea.net/repo/deb/debian/pubkey.gpg | apt-key add -
RUN echo "deb https://mediaarea.net/repo/deb/debian/ buster main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y apt-utils gosu jq unzip mediainfo librhash-dev

WORKDIR /usr/src/app/build
COPY --from=build /usr/src/app/build .
COPY ./dockerentry.sh /dockerentry.sh

WORKDIR /usr/src/app/build/webui
RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/Shoko-WebUI/releases/latest | jq -r '.assets[0].browser_download_url') -o latest.zip
RUN unzip -o latest.zip
RUN rm latest.zip

VOLUME /home/shoko/.shoko/

HEALTHCHECK --start-period=5m CMD curl -H "Content-Type: application/json" -H 'Accept: application/json' 'http://localhost:8111/v1/Server' || exit 1

EXPOSE 8111

ENTRYPOINT /bin/bash /dockerentry.sh
