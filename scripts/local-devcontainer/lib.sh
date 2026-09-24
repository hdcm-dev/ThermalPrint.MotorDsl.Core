#!/usr/bin/env bash
# Funciones comunes de los scripts de local-devcontainer. Se incluye con `source`.
#
# Cada corrida levanta un contenedor efímero (`docker run --rm`) con un nombre
# propio, y un trap lo borra también si el script se corta con Ctrl+C. Lo único
# que persiste entre corridas es:
#   - la imagen $IMAGEN (SDK de Android + workload, ~6 GB; se arma una vez), y
#   - el volumen $VOLUMEN: caché de paquetes NuGet y el keystore de firma.
# Ambos se borran con ./limpiar.sh --todo.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
OUT_DIR="$SCRIPT_DIR/OUTPUTs"

IMAGEN="${MOTORDSL_IMAGEN:-motordsl-android-build:net10}"
VOLUMEN="${MOTORDSL_VOLUMEN:-motordsl-android-cache}"

CONTENEDORES_VIVOS=()

limpiar_contenedores() {
    local c
    for c in "${CONTENEDORES_VIVOS[@]:-}"; do
        [ -n "$c" ] && docker rm -f "$c" >/dev/null 2>&1 || true
    done
}
trap limpiar_contenedores EXIT
trap 'exit 130' INT TERM

asegurar_imagen() {
    if ! docker image inspect "$IMAGEN" >/dev/null 2>&1; then
        echo "==> La imagen $IMAGEN no existe; se arma (tarda la primera vez)..."
        docker build -t "$IMAGEN" "$SCRIPT_DIR"
    fi
}

# correr_en_contenedor <sufijo> [opciones docker...] -- <comando...>
correr_en_contenedor() {
    local nombre="motordsl-$1-$$"; shift
    local opts=()
    while [ "$#" -gt 0 ] && [ "$1" != "--" ]; do opts+=("$1"); shift; done
    shift
    CONTENEDORES_VIVOS+=("$nombre")
    docker run --rm --name "$nombre" \
        -e HOST_UID="$(id -u)" -e HOST_GID="$(id -g)" \
        -v "$VOLUMEN":/cache \
        "${opts[@]}" \
        "$IMAGEN" "$@"
}

# ApplicationId del .csproj, para lanzar la app después de instalarla.
application_id() {
    sed -n 's:.*<ApplicationId>\(.*\)</ApplicationId>.*:\1:p' "$1" | head -1
}

# Primer contenedor corriendo que tenga un servidor de adb (`adb ... fork-server`).
contenedor_con_adb() {
    local c
    for c in $(docker ps --format '{{.Names}}'); do
        case "$c" in motordsl-*) continue ;; esac
        if docker top "$c" 2>/dev/null | grep -q 'adb .*fork-server'; then
            echo "$c"; return
        fi
    done
}
