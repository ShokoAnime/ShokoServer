#!/bin/bash

echo "Started Shoko Server bootstrapping process…"

# Set variable for the UID and GID based on env, else use default values
PUID=${PUID:-1000}
PGID=${PGID:-100}

GROUP="shokogroup"
USER="shoko"

# Well-known users.
if [ "$PUID" -eq 0 ]; then
    USER="root"
fi

# Well-known groups.
if [ "$PGID" -eq 0 ]; then
    GROUP="root"
elif [ "$PGID" -eq 100 ]; then
    GROUP="users"
fi

# Create or update group.
if [ $(getent group $GROUP) ]; then
    if [ $(getent group $GROUP | cut -d: -f3) -ne $PGID ]; then
        groupmod -g "$PGID" $GROUP
    fi
else
    groupadd -o -g "$PGID" $GROUP
fi

# Create or update user.
if [ $(getent passwd $USER) ]; then
    if [ $(getent passwd $USER | cut -d: -f3) -ne $PUID ]; then
        usermod -u "$PUID" $USER
    fi
    [ $(id -g $USER) -ne $PGID ] && usermod -g "$PGID" $USER
else
    echo "Adding user $USER and changing ownership of /home/shoko and all it's sub-directories…"
    useradd  -N -o -u "$PUID" -g "$PGID" -d /home/shoko $USER

    mkdir -p /home/shoko/
    chown $USER:$GROUP /home/shoko
fi

# Make sure SHOKO_HOME directory is correctly set.
SHOKO_HOME=${SHOKO_HOME:-/home/shoko/.shoko/Shoko.CLI}
if [ "$PUID" -eq 0 ]; then
    if [ "$SHOKO_HOME" == "/home/shoko/.shoko/Shoko.CLI" ]; then
        echo "Error: Cannot use default SHOKO_HOME directory when running as root (PUID=0)."
        echo "Please set a custom SHOKO_HOME directory."
        exit 1
    fi
fi
if [ ! -d "$SHOKO_HOME" ]; then
    if [ "$SHOKO_HOME" == "/home/shoko/.shoko/Shoko.CLI" ]; then
        echo "Creating default SHOKO_HOME directory: $SHOKO_HOME"
        mkdir -p "$SHOKO_HOME"
    else
        echo "Error: SHOKO_HOME directory ($SHOKO_HOME) does not exist!"
        exit 1
    fi
fi

# Set ownership of application data to shoko user.
OWNER=$(stat -c '%u:%g' "$SHOKO_HOME" 2>/dev/null)
if [ "$OWNER" != "$PUID:$PGID" ]; then
    echo "Changing ownership of /home/shoko and all it's sub-directories…"
    chown -R $PUID:$PGID /home/shoko/
fi

# Set ownership of shoko files to shoko user
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
User ID:   $(id -u $USER)
Group ID:  $(id -g $USER)
UMASK set: $(umask)
Directory: \"$SHOKO_HOME\"
-------------------------------------
"

# Go and run the server
exec gosu $USER:$GROUP /usr/src/app/build/Shoko.CLI
