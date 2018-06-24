FROM mono:5.12

#MAINTAINER Cayde Dixon <me@cazzar.net>
ENV PUID=1000 \
    PGID=100

RUN curl https://bintray.com/user/downloadSubjectPublicKey?username=bintray | apt-key add -
RUN echo "deb http://dl.bintray.com/cazzar/shoko-deps jesse main" | tee -a /etc/apt/sources.list
RUN echo "deb http://ftp.debian.org/debian jessie-backports main" | tee -a /etc/apt/sources.list

RUN apt update && apt install -y --force-yes libmediainfo0 librhash0 sqlite.interop jq unzip && apt install -t jessie-backports gosu

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source
RUN mv /usr/src/app/source/dockerentry.sh /dockerentry.sh

ADD https://github.com/NuGet/Home/releases/download/3.3/NuGet.exe .
RUN mono NuGet.exe restore
RUN xbuild /property:Configuration=CLI /property:OutDir=/usr/src/app/build/
RUN rm -rf /usr/src/app/source
RUN rm /usr/src/app/build/System.Net.Http.dll

WORKDIR /usr/src/app/build/webui
#RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '. | map(select(.prerelease==false)) | .[0].assets[0].browser_download_url') -o latest.zip
RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '.[0].assets[0].browser_download_url') -o latest.zip
RUN unzip -o latest.zip
RUN rm latest.zip

WORKDIR /usr/src/app/build

VOLUME /home/shoko/.shoko/
VOLUME /usr/src/app/build/webui

HEALTHCHECK --start-period=5m CMD curl -H "Content-Type: application/json" -H 'Accept: application/json' 'http://localhost:8111/v1/Server' || exit 1

EXPOSE 8111

ENTRYPOINT /bin/bash /dockerentry.sh 
