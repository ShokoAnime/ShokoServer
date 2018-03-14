#!/bin/bash

# Set variable for the UID and GID based on env, else use default values
PUID=${PUID:-1000}
PGID=${PGID:-100}

groupadd -o -g "$PGID" shokogroup
useradd  -o -u "$PUID" -d /home/shoko shoko

usermod -G shokogroup shoko

mkdir -p /home/shoko
chown -R shoko:shokogroup /home/shoko

mkdir -p /.shoko/

# Set owership of shoko files to shoko user
chown -R shoko:shokogroup /usr/src/app/build/

echo "
-------------------------------------
User uid:    $(id -u shoko)
User gid:    $(id -g shoko)
-------------------------------------
"

# Go and run the server 
exec gosu shoko:shokogroup mono --debug /usr/src/app/build/Shoko.CLI.exe
