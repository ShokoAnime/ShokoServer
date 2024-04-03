#!/bin/bash

echo "Started Shoko Server bootstrapping process…"

# Set variable for the UID and GID based on env, else use default values
PUID=${PUID:-1000}
PGID=${PGID:-100}

[ $PUID -eq 0 ] && [ $PGID -eq 0 ] && echo "You really shouldn't be doing this..." && dotnet /usr/src/app/build/Shoko.CLI.dll

GROUP="shokogroup"
USER="shoko"

if [ $(getent group $GROUP) ]; then #if group exists
    if [ $(getent group $GROUP | cut -d: -f3) -ne $PGID ]; then #if gid of said group doesn't match
        groupmod -g "$PGID" $GROUP
        REDO_PERM=1
    fi
else
    groupadd -o -g "$PGID" $GROUP
fi

if [ $(getent passwd $USER) ]; then
    if [ $(getent passwd $USER | cut -d: -f3) -ne $PUID ]; then
        usermod -u "$PUID" $USER
        REDO_PERM=1
    fi
    [ $(id -g $USER) -ne $PGID ] && usermod -g "$PGID" $USER
else
    echo "Adding user $USER and changing ownership of /home/shoko and all it's sub-directories…"
    useradd  -N -o -u "$PUID" -g "$PGID" -d /home/shoko $USER

    mkdir -p /home/shoko/.shoko/
    chown -R $USER:$GROUP /home/shoko
fi

if [ $REDO_PERM ]; then
    echo "Changing ownership of /home/shoko and all it's sub-directories…"
    chown -R $PUID:$PGID /home/shoko/
fi

# Set owership of shoko files to shoko user
chown -R $USER:$GROUP /usr/src/app/build/
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

# set umask to specified value if defined
if [[ ! -z "${UMASK}" ]]; then
     umask "${UMASK}"
fi

echo "
-------------------------------------
User uid:    $(id -u $USER)
User gid:    $(id -g $USER)
UMASK set:    $(umask)
-------------------------------------
"

# Go and run the server
exec gosu $USER:$GROUP /usr/src/app/build/Shoko.CLI
