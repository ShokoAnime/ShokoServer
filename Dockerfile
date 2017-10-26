FROM mono:5.4

#MAINTAINER Cayde Dixon <me@cazzar.net>

RUN curl https://bintray.com/user/downloadSubjectPublicKey?username=bintray | apt-key add -
RUN echo "deb http://dl.bintray.com/cazzar/shoko-deps jesse main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y --force-yes libmediainfo0 librhash0 sqlite.interop

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source

ADD https://github.com/NuGet/Home/releases/download/3.3/NuGet.exe .
RUN mono NuGet.exe restore
RUN xbuild /property:Configuration=CLI /property:OutDir=/usr/src/app/build/
RUN rm -rf /usr/src/app/source
RUN rm /usr/src/app/build/System.Net.Http.dll
WORKDIR /usr/src/app/build

VOLUME /root/.shoko/
VOLUME /usr/src/app/build/webui

EXPOSE 8111
ENTRYPOINT mono --debug Shoko.CLI.exe
