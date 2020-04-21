FROM mono:6.0

#MAINTAINER Cayde Dixon <me@cazzar.net>
ENV PUID=1000 \
    PGID=100  \
    TargetFrameworkDirectory=/usr/lib/mono/

RUN apt-get install apt
RUN apt-get update && apt-get install -y gnupg curl wget

RUN curl https://bintray.com/user/downloadSubjectPublicKey?username=bintray | apt-key add -
RUN echo "deb http://dl.bintray.com/cazzar/shoko-deps jesse main" | tee -a /etc/apt/sources.list
RUN echo "deb http://ftp.debian.org/debian stretch-backports main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y apt-utils libmediainfo0v5 librhash0 sqlite.interop jq unzip libunwind-dev apt-transport-https && apt-get install -y -t stretch-backports gosu

RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 2.2

RUN mkdir -p /usr/src/app/source /usr/src/app/build
COPY . /usr/src/app/source
WORKDIR /usr/src/app/source


#ADD https://github.com/NuGet/Home/releases/download/3.3/NuGet.exe .
RUN nuget restore
RUN msbuild /property:Configuration=CLI /property:OutDir=/usr/src/app/build/
RUN rm /usr/src/app/build/System.Net.Http.dll

FROM mono:6.0
ENV PUID=1000 \
    PGID=100 

RUN apt-get install apt
RUN apt-get update && apt-get install -y gnupg curl wget

RUN curl https://bintray.com/user/downloadSubjectPublicKey?username=bintray | apt-key add -
RUN echo "deb http://dl.bintray.com/cazzar/shoko-deps jesse main" | tee -a /etc/apt/sources.list
RUN echo "deb http://ftp.debian.org/debian stretch-backports main" | tee -a /etc/apt/sources.list

RUN apt-get update && apt-get install -y apt-utils libmediainfo0 librhash0 sqlite.interop jq unzip libunwind-dev apt-transport-https && apt-get install -y -t stretch-backports gosu

WORKDIR /usr/src/app/build
COPY --from=0 /usr/src/app/build .
COPY ./dockerentry.sh /dockerentry.sh

WORKDIR /usr/src/app/build/webui
#RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '. | map(select(.prerelease==false)) | .[0].assets[0].browser_download_url') -o latest.zip
RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '.[0].assets[0].browser_download_url') -o latest.zip
RUN unzip -o latest.zip
RUN rm latest.zip

VOLUME /home/shoko/.shoko/
VOLUME /usr/src/app/build/webui

HEALTHCHECK --start-period=5m CMD curl -H "Content-Type: application/json" -H 'Accept: application/json' 'http://localhost:8111/v1/Server' || exit 1

EXPOSE 8111

ENTRYPOINT /bin/bash /dockerentry.sh 
