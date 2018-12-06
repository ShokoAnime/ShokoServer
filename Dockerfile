#MAINTAINER Cayde Dixon <me@cazzar.net>
FROM microsoft/dotnet:2.2-sdk-alpine AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY . dotnetapp
WORKDIR /app/dotnetapp
RUN apk add --update curl jq unzip
RUN dotnet restore

# copy and publish app and libraries
RUN dotnet publish -c Release -o out ./Shoko.CLI

WORKDIR /app/dotnetapp/Shoko.CLI/out/webui
#RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '. | map(select(.prerelease==false)) | .[0].assets[0].browser_download_url') -o latest.zip
RUN curl -L $(curl https://api.github.com/repos/ShokoAnime/ShokoServer-WebUI/releases | jq -r '.[0].assets[0].browser_download_url') -o latest.zip
RUN unzip -o latest.zip
RUN rm latest.zip

FROM microsoft/dotnet:2.2-runtime-alpine AS runtime
WORKDIR /app
COPY --from=build /app/dotnetapp/Shoko.CLI/out ./
COPY --from=build /app/dotnetapp/Shoko.Server/nlog.config .
RUN apk add --update libmediainfo rhash-libs \
    && rm -rf /var/cache/apk/*

RUN mkdir /home/shoko
RUN chmod 777 /home/shoko

RUN ln -s /usr/lib/librhash.so.0 /app/librhash.so && \
    ln -s /usr/lib/libmediainfo.so.0 /app/libmediainfo.so


USER 1000:100

ENV HOME=/home/shoko
ENTRYPOINT ["dotnet", "Shoko.CLI.dll"]
