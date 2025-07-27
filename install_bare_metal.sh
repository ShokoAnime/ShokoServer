#!/bin/bash

set -euo pipefail

log() { echo -e "\n[INFO] $1"; }
error_exit() { echo -e "\n[ERROR] $1" >&2; exit 1; }

command -v apt-get >/dev/null || error_exit "apt-get not found. Are you on Debian/Ubuntu?"
command -v curl >/dev/null || error_exit "curl not installed."
command -v gpg >/dev/null || error_exit "gpg not installed."

log "Installing dependencies..."
apt-get update && apt-get install -y \
    gnupg curl gosu jq unzip git mediainfo librhash-dev dotnet-sdk-8.0 || \
    error_exit "Dependency installation failed."
    
log "Install .NET SDK and Runtime..."
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0 aspnetcore-runtime-8.0

log "Adding MediaInfo repository..."
curl --retry 3 -O https://mediaarea.net/repo/deb/debian/pubkey.gpg || error_exit "Failed to download GPG key."
gpg --no-default-keyring --keyring ./temp-keyring.gpg --import pubkey.gpg || error_exit "GPG import failed."
gpg --no-default-keyring --keyring ./temp-keyring.gpg \
    --export --output /usr/share/keyrings/mediainfo.gpg || error_exit "GPG export failed."
rm pubkey.gpg
echo "deb [signed-by=/usr/share/keyrings/mediainfo.gpg] https://mediaarea.net/repo/deb/debian/ bookworm main" \
    | tee -a /etc/apt/sources.list
apt-get update && apt-get install -y mediainfo || error_exit "MediaInfo install failed."

log "Creating build directories..."
mkdir -p /usr/src/app/source /usr/src/app/build

log "Cloning ShokoServer repository..."
cd /usr/src/app
[ -d source ] && rm -rf source
git clone https://github.com/ShokoAnime/ShokoServer.git source || error_exit "Git clone failed."
cd source

log "Building the ShokoServer"
COMMIT=$(git rev-parse --short HEAD)
DATE=$(date -u +%Y-%m-%d)
VERSION="5.1.0.99"
CHANNEL="dev"
TAG="latest"
INFOVERSION="channel=$CHANNEL,commit=$COMMIT,tag=$TAG,date=$DATE"

DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build -c=Release -f net8.0 \
    -o=/usr/src/app/build/ -r=linux-arm64 \
    Shoko.CLI/Shoko.CLI.csproj \
    /p:Version="$VERSION" \
    /p:InformationalVersion=\"$INFOVERSION\" \
    /maxcpucount:1 || error_exit "dotnet build failed."

log "Downloading and extracting Web UI..."
mkdir -p /usr/src/app/build/webui
cd /usr/src/app/build/webui
LATEST_URL=$(curl -s https://api.github.com/repos/ShokoAnime/Shoko-WebUI/releases | jq -r '.[0].assets[0].browser_download_url')
[[ "$LATEST_URL" =~ ^https://.*\.zip$ ]] || error_exit "Invalid WebUI download URL."
curl -L "$LATEST_URL" -o latest.zip || error_exit "Failed to download WebUI zip."
unzip -o latest.zip || error_exit "Unzip failed."
rm latest.zip

log "Setting up user and group..."
PUID=1000
PGID=100
GROUP="shokogroup"
USER="shoko"

if getent group $GROUP > /dev/null; then
    [ $(getent group $GROUP | cut -d: -f3) -ne $PGID ] && groupmod -g "$PGID" $GROUP
else
    groupadd -o -g "$PGID" $GROUP
fi

if getent passwd $USER > /dev/null; then
    [ $(id -u $USER) -ne $PUID ] && usermod -u "$PUID" $USER
    [ $(id -g $USER) -ne $PGID ] && usermod -g "$PGID" $USER
else
    useradd -N -o -u "$PUID" -g "$PGID" -d /home/shoko $USER
    mkdir -p /home/shoko/.shoko/
    chown -R $USER:$GROUP /home/shoko
fi

chown -R $USER:$GROUP /usr/src/app/build/

[ -d /root/.shoko ] && error_exit "/root/.shoko exists. Please move it to /home/shoko/.shoko"

log "Creating Systemd service..."
cat <<EOF > /etc/systemd/system/Shoko-Server.service
[Unit]
Description=Shoko Server Service
After=network.target

[Service]
Type=simple
User=shoko
Group=shokogroup
WorkingDirectory=/usr/src/app/build
ExecStart=/usr/src/app/build/Shoko.CLI
Restart=on-failure

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reexec
systemctl daemon-reload
systemctl enable Shoko-Server || error_exit "Failed to enable service."

log "Starting Shoko-Server..."
systemctl start Shoko-Server && log "Service Started" || error_exit "Failed to start service."
log "Checking Server Status..."
[ "$(
    curl -s \
    -H "Content-Type: application/json" \
    -H 'Accept: application/json' \
    'http://localhost:8111/api/v3/Init/Status' \
     | jq -r '.State')" = "Started" ] && log "Server Running" || error_exit "Server Not Running"

log "Cleanup..."
rm -rf /usr/src/app/source && log "Source directory deleted" || error_exit "Failed to delete source directory in /usr/src/app/source."
