FROM mcr.microsoft.com/dotnet/core/sdk:3.1

#MAINTAINER Cayde Dixon <me@cazzar.net>
ENV PUID=1000 \
    PGID=100

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source

RUN dotnet build -c=Release -o=/usr/src/app/build/ Shoko.CLI/Shoko.CLI.csproj

FROM mcr.microsoft.com/dotnet/core/sdk:3.1
ENV PUID=1000 \
    PGID=100 \
    LANG=C.UTF-8 \
    LC_CTYPE=C.UTF-8 \
    LC_ALL=C.UTF-8
    
RUN curl https://mediaarea.net/repo/deb/debian/pubkey.gpg | apt-key add -
RUN echo "deb https://mediaarea.net/repo/deb/debian/ buster main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y apt-utils gosu jq unzip libmediainfo0v5 librhash-dev libunwind-dev mono-runtime libmono-system-xml-linq4.0-cil

WORKDIR /usr/src/app/build
COPY --from=0 /usr/src/app/build .
COPY ./dockerentry.sh /dockerentry.sh

WORKDIR /usr/src/app/build/webui
RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '.[0].assets[0].browser_download_url') -o latest.zip
RUN unzip -o latest.zip
RUN rm latest.zip

VOLUME /home/shoko/.shoko/
VOLUME /usr/src/app/build/webui

HEALTHCHECK --start-period=5m CMD curl -H "Content-Type: application/json" -H 'Accept: application/json' 'http://localhost:8111/v1/Server' || exit 1

EXPOSE 8111

ENTRYPOINT /bin/bash /dockerentry.sh 
