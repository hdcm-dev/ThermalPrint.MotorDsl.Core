#!/usr/bin/env bash
# Borra contenedores de estos scripts que hayan quedado vivos (no debería
# haberlos: cada corrida se borra sola). Con --todo borra además el volumen de
# caché (paquetes NuGet + keystore de firma) y la imagen.
#
# OJO: sin el keystore, el próximo APK se firma con otra clave y `adb install -r`
# sobre la versión anterior falla; instalar.sh lo resuelve desinstalando antes.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

ids="$(docker ps -aq --filter "name=^motordsl-(build|adb|update)-")"
[ -n "$ids" ] && docker rm -f $ids
echo "Contenedores: limpios."

if [ "${1:-}" = "--todo" ]; then
    docker volume rm -f "$VOLUMEN" >/dev/null && echo "Volumen $VOLUMEN: borrado."
    docker image rm -f "$IMAGEN" >/dev/null 2>&1 && echo "Imagen $IMAGEN: borrada." || true
fi
