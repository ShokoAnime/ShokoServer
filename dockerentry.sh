#!/bin/bash

# Set variable for the UID and GID based on env, else use default values
PUID=${PUID:-1000}
PGID=${PGID:-100}

groupadd -o -g "$PGID" shokogroup
useradd  -o -u "$PUID" -d /home/shoko shoko

usermod -G shokogroup shoko

mkdir -p /home/shoko
chown -R shoko:shokogroup /home/shoko

mkdir -p /home/shoko/.shoko/

# Set Ownership for the shoko directory as well.
chown -R shoko:shokogroup /home/shoko/.shoko/

# Set owership of shoko files to shoko user
chown -R shoko:shokogroup /usr/src/app/build/
if [ -d /root/.shoko ]; then
    echo "
-------------------------------------
OLD SHOKO INSTALL DETECTED

Please change the volume for shoko
OLD directory: /root/.shoko
New directory: /home/shoko/.shoko
-------------------------------------
    "
    exit 1
fi

echo "
-------------------------------------
User uid:    $(id -u shoko)
User gid:    $(id -g shoko)
-------------------------------------
"

# Go and run the server 
exec gosu shoko:shokogroup mono --debug /usr/src/app/build/Shoko.CLI.exe
