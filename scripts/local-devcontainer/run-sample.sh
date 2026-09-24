#!/usr/bin/env bash
# Compila un sample MAUI a APK dentro de un contenedor efímero, deja el APK en
# OUTPUTs/<Sample>.apk y, si hay un teléfono, lo instala y lo lanza.
#
# Uso:
#   ./run-sample.sh <Sample> [--sin-instalar] [--rid <rid>] [--url <backendBaseUrl>]
#
#   --sin-instalar  Solo genera el APK.
#   --rid <rid>     Un único RID (p. ej. android-arm64). Sin él, el APK lleva las
#                   cuatro ABIs que declara el .csproj.
#   --url <url>     Reescribe backendBaseUrl de Resources/Raw/motordsl-config.json
#                   en la COPIA que compila el contenedor; el árbol no se toca.
#
# Dos servidores de adb no pueden reclamar el mismo USB. Si otro contenedor ya
# corre uno (p. ej. gda-core-app-dev), se instala a través de él; si no, se
# levanta un contenedor efímero con adb propio. ADB_CONTENEDOR=<nombre> fuerza
# uno en particular; ADB_CONTENEDOR=ninguno fuerza el adb propio.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

SAMPLE="${1:?Uso: $0 <Sample> [--sin-instalar] [--rid <rid>] [--url <url>]}"; shift
INSTALAR=1; RID=""; URL=""
while [ "$#" -gt 0 ]; do
    case "$1" in
        --sin-instalar) INSTALAR=0 ;;
        --rid) RID="${2:?falta el RID}"; shift ;;
        --url) URL="${2:?falta la URL}"; shift ;;
        *) echo "Opción desconocida: $1" >&2; exit 2 ;;
    esac
    shift
done

CSPROJ="$REPO_ROOT/samples/$SAMPLE/$SAMPLE.csproj"
[ -f "$CSPROJ" ] || { echo "[ERROR] No existe $CSPROJ" >&2; exit 1; }
APK="$OUT_DIR/$SAMPLE.apk"

echo "============================================================"
echo "  $SAMPLE -- APK Release (contenedor efímero)"
echo "============================================================"

asegurar_imagen
mkdir -p "$OUT_DIR"
rm -f "$APK"

correr_en_contenedor "build" \
    -v "$REPO_ROOT":/src:ro \
    -v "$OUT_DIR":/out \
    -- bash /src/scripts/local-devcontainer/interno/compilar.sh "$SAMPLE" "$RID" "$URL"

[ -f "$APK" ] || { echo "[ERROR] El build terminó sin dejar $APK" >&2; exit 1; }
echo
echo "APK generado: $APK ($(du -h "$APK" | cut -f1))"

[ "$INSTALAR" -eq 1 ] || exit 0

PAQUETE="$(application_id "$CSPROJ")"
echo
echo "==> Instalando $PAQUETE en el teléfono..."

if [ -z "${ADB_CONTENEDOR:-}" ]; then
    ADB_CONTENEDOR="$(contenedor_con_adb)"
    [ -n "$ADB_CONTENEDOR" ] && echo "    El teléfono lo tiene tomado el adb de '$ADB_CONTENEDOR': se instala a través de él."
fi
[ "${ADB_CONTENEDOR:-}" = "ninguno" ] && ADB_CONTENEDOR=""

if [ -n "$ADB_CONTENEDOR" ]; then
    docker cp "$APK" "$ADB_CONTENEDOR:/tmp/$SAMPLE.apk"
    docker cp "$SCRIPT_DIR/interno/instalar.sh" "$ADB_CONTENEDOR:/tmp/motordsl-instalar.sh"
    rc=0
    docker exec "$ADB_CONTENEDOR" bash /tmp/motordsl-instalar.sh "/tmp/$SAMPLE.apk" "$PAQUETE" || rc=$?
    docker exec -u root "$ADB_CONTENEDOR" rm -f "/tmp/$SAMPLE.apk" /tmp/motordsl-instalar.sh || true
    exit "$rc"
fi

correr_en_contenedor "adb" \
    --privileged -v /dev/bus/usb:/dev/bus/usb \
    -v "$OUT_DIR":/out:ro \
    -v "$SCRIPT_DIR/interno":/interno:ro \
    -- bash /interno/instalar.sh "/out/$SAMPLE.apk" "$PAQUETE"
