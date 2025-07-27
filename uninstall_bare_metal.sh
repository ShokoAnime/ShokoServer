#!/bin/bash

set -e

# Variables
USER="shoko"
GROUP="shokogroup"
APP_DIR="/usr/src/app"
SERVICE_FILE="/etc/systemd/system/Shoko-Server.service"

log() { echo -e "\n[INFO] $1"; }

if systemctl is-active --quiet Shoko-Server; then
    log "Stopping Shoko service..."
    systemctl stop Shoko-Server
    systemctl disable Shoko-Server
fi

if [ -f "$SERVICE_FILE" ]; then
    log "Removing service file..."
    rm -f "$SERVICE_FILE"
    systemctl daemon-reexec
fi

if id "$USER" &>/dev/null; then
    log "Deleting user $USER..."
    userdel -r "$USER"
fi

if getent group "$GROUP" &>/dev/null; then
    log "Deleting group $GROUP..."
    groupdel "$GROUP"
fi

if [ -d "$APP_DIR" ]; then
    log "Removing application directory..."
    rm -rf "$APP_DIR"
fi

log "Cleaning up MediaInfo keyring and repo..."
rm -f /usr/share/keyrings/mediainfo.gpg
sed -i '/mediaarea\.net\/repo\/deb\/debian/d' /etc/apt/sources.list
apt-get update

log "Removing installed packages..."
apt-get purge -y git mediainfo librhash-dev dotnet-sdk-8.0
apt-get autoremove -y

log "Uninstallation complete."
