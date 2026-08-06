#!/bin/bash

echo "Started Shoko Server bootstrapping process…"

# Install extra apt packages before the server starts. Space or comma
# separated; unset (the default) does nothing and costs nothing.
#
# This exists for userspace the image cannot reasonably ship for everyone but
# that has to be present before startup — GPU drivers for hardware
# transcoding above all, where the VA driver has to live inside the container
# and match the container's own libva. Installing it later is too late,
# because plugins probe the hardware during startup and cache the result.
#
# Packages land in the container's writable layer, so they survive a restart
# but not a recreate, and are reinstalled on the next start when that happens.
if [ -n "${INSTALL_PACKAGES:-}" ]; then
    PACKAGES=$(echo "$INSTALL_PACKAGES" | tr ',' ' ')
    MISSING=""
    for PACKAGE in $PACKAGES; do
        STATUS=$(dpkg-query -W -f='${Status}' "$PACKAGE" 2>/dev/null)
        [ "$STATUS" = "install ok installed" ] || MISSING="$MISSING $PACKAGE"
    done

    if [ -z "$MISSING" ]; then
        echo "Extra packages already installed:$PACKAGES"
    else
        echo "Installing extra packages:$MISSING"
        # Deliberately not fatal. Losing the whole server to a typo in a
        # package name is worse than starting without the extras, so this
        # warns loudly and carries on.
        if apt-get update && apt-get install -y --no-install-recommends $MISSING; then
            echo "Extra packages installed."
        else
            echo "
-------------------------------------
WARNING: could not install:$MISSING

Starting anyway, without them. Check the package names and that they
exist in this image's apt sources.
-------------------------------------
            "
        fi
    fi
fi

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
    if [ "${NO_CHOWN:-}" != "true" ]; then
        chown $USER:$GROUP /home/shoko
    fi
fi

# Supplementary groups for the user, by name or numeric ID. Space or comma
# separated; unset (the default) does nothing.
#
# This is how you grant access to a passed-through device — /dev/dri for GPU
# transcoding above all, which is owned by the host's `render` group. Use the
# numeric ID from the host (`stat -c '%g' /dev/dri/renderD128`), since group
# names differ between distributions and the kernel only compares numbers.
#
# Docker's own --group-add cannot do this: it adds groups to the container's
# root process, and the groups that survive dropping privileges are the ones
# recorded against the user in /etc/group. A numeric ID with no group behind
# it gets one created, because usermod will not take a bare GID.
if [ -n "${EXTRA_GROUPS:-}" ]; then
    for ENTRY in $(echo "$EXTRA_GROUPS" | tr ',' ' '); do
        if [ -z "$(getent group "$ENTRY")" ]; then
            case "$ENTRY" in
                *[!0-9]*|'')
                    echo "WARNING: no group named '$ENTRY' in this image, and it is not a numeric ID. Skipping."
                    continue
                    ;;
                *)
                    if ! groupadd -o -g "$ENTRY" "shokoextra$ENTRY"; then
                        echo "WARNING: could not create a group for ID $ENTRY. Skipping."
                        continue
                    fi
                    ;;
            esac
        fi

        if usermod -aG "$ENTRY" $USER; then
            echo "Added $USER to group $ENTRY."
        else
            echo "WARNING: could not add $USER to group $ENTRY."
        fi
    done
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

# Enable crash dump generation to SHOKO_HOME for segfault diagnosis.
export DOTNET_DbgMiniDumpType=4
export DOTNET_DbgMiniDumpName=$SHOKO_HOME/coredump.%p.%d.dmp
export DOTNET_EnableCrashReport=1
export DOTNET_CrashReportPath=$SHOKO_HOME/crash.%p.json

# Set ownership of application data to shoko user.
OWNER=$(stat -c '%u:%g' "$SHOKO_HOME" 2>/dev/null)
if [ "$OWNER" != "$PUID:$PGID" ] && [ "${NO_CHOWN:-}" != "true" ]; then
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
Groups:    $(id -Gn $USER | tr ' ' ',')
UMASK set: $(umask)
Directory: \"$SHOKO_HOME\"
-------------------------------------
"

# Allow/disallow the server to be shutdown/restarted from the web interface.
ENABLE_SHUTDOWN=${ENABLE_SHUTDOWN:-false}
ENABLE_RESTART=${ENABLE_RESTART:-true}

ARGS=""
[ "$ENABLE_SHUTDOWN" = "true" ] && ARGS="$ARGS --shutdown-enabled"
[ "$ENABLE_RESTART" = "true" ] && ARGS="$ARGS --restart-enabled"

# Run the server, and restart it if it exits with code 140 (Custom restart exit code).
# Set up signal forwarding to the dotnet process
trap 'kill -TERM $DOTNET_PID 2>/dev/null; exit 143' TERM INT

ulimit -c unlimited

while true; do
  # Deliberately "$USER" and not "$USER:$GROUP": naming a group makes gosu
  # replace the supplementary group list rather than keep it, which silently
  # discards everything EXTRA_GROUPS just added. The user's primary group is
  # already $PGID by this point, set above, so the resulting uid and gid are
  # identical either way.
  gosu $USER /usr/src/app/build/Shoko.CLI $ARGS &
  DOTNET_PID=$!
  wait $DOTNET_PID
  EXIT_CODE=$?
  [ $EXIT_CODE -ne 140 ] && exit $EXIT_CODE
done
