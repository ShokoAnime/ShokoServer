FROM mono:5.0.0

# Maintainer Cayde Dixon <me@cazzar.net>

RUN apt-get update && apt-get install -y --force-yes libmediainfo0

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source
ADD https://github.com/NuGet/Home/releases/download/3.3/NuGet.exe .
RUN mono NuGet.exe restore
RUN xbuild /property:Configuration=CLI /property:OutDir=/usr/src/app/build/
RUN rm -rf /usr/src/app/source
WORKDIR /usr/src/app/build

VOLUME /root/.shoko/
EXPOSE 8111
ENTRYPOINT mono Shoko.CLI.exe
