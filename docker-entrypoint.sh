#!/bin/sh
# Démarrage du conteneur du site. Le volume /data (journal des lancements, voir fly.toml) est créé par l'hébergeur au nom
# de root : lancé en root, le script le confie à l'utilisateur sans privilèges de l'image .NET ($APP_UID), puis démarre
# le serveur sous cet utilisateur (setpriv) ; lancé sans privilèges, il démarre directement le serveur.
set -e

if [ "$(id -u)" = "0" ]; then
    if [ -d /data ]; then
        chown "$APP_UID:$APP_UID" /data
    fi
    exec setpriv --reuid="$APP_UID" --regid="$APP_UID" --clear-groups dotnet Wh40kSim.Web.dll "$@"
fi

exec dotnet Wh40kSim.Web.dll "$@"
