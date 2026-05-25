#!/bin/sh
# Volumes named criados pelo Docker pertencem a root e o api user (uid 1654) não consegue
# escrever. SQL Server (uid mssql=10001) também precisa de escrever em /backups.
# Solução: arrancar como root, ajustar permissões dos mount points, depois drop para app.

set -e

chmod 0777 /backups 2>/dev/null || true
chmod 0700 /data/dp-keys 2>/dev/null || true
chown -R app:app /data/dp-keys /data/photos 2>/dev/null || true

exec runuser -u app -- dotnet RepairDesk.API.dll "$@"
