#!/usr/bin/env bash
# Copia del zip NeptuneLiteApi 4.15.00 (material PAX, no versionado) las piezas que embebe
# MotorDsl.Pax en src/MotorDsl.Pax/Neptune/sdk/ (ignorada por git). Sin esa carpeta,
# MotorDsl.Pax compila igual pero SIN binding (modo Real -> Connection, simulado intacto).
#
# Uso: scripts/pax-sdk/preparar-sdk.sh <ruta a NeptuneLiteApi_V4.15.00_20250606.zip>
#
# Reserva de licencia: el material PAX no trae licencia ni terminos de redistribucion
# (README del material, "Pendientes"). Por eso el jar y las .so NO se versionan en este repo.
set -euo pipefail

ZIP="${1:?Falta la ruta al zip NeptuneLiteApi_V4.15.00_20250606.zip}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEST="$REPO_ROOT/src/MotorDsl.Pax/Neptune/sdk"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

unzip -q "$ZIP" 'libs/NeptuneLiteApi_V4.15.00_20250606.jar' \
    'jniLibs/arm64-v8a/libDeviceConfig.so' 'jniLibs/arm64-v8a/libpaxcanvas.so' \
    'jniLibs/armeabi/libDeviceConfig.so' -d "$TMP"

( cd "$TMP" && sha256sum -c - ) <<'SUMS'
1d25306bfaf91b8d943127049ad65ab556ac82434843d94040de90bc06615f5e  libs/NeptuneLiteApi_V4.15.00_20250606.jar
385aec3dcd4746a1b2e1ea76f715c14ef4419a8a07558f7eb8c355db2683a337  jniLibs/arm64-v8a/libDeviceConfig.so
da9cdc38672b4fe59e47dbf976bec5018fc9e1aac92a376396534a218d5f02b1  jniLibs/arm64-v8a/libpaxcanvas.so
59077389a226c57f986352165a5770e1e5e17ce0fe1334812383ca27b9bf36b9  jniLibs/armeabi/libDeviceConfig.so
SUMS

rm -rf "$DEST"
mkdir -p "$DEST/arm64-v8a" "$DEST/armeabi-v7a"
cp "$TMP/libs/NeptuneLiteApi_V4.15.00_20250606.jar" "$DEST/"
cp "$TMP/jniLibs/arm64-v8a/libDeviceConfig.so" "$TMP/jniLibs/arm64-v8a/libpaxcanvas.so" "$DEST/arm64-v8a/"
# Guia del IDE PAX: armeabi se usa como armeabi-v7a.
cp "$TMP/jniLibs/armeabi/libDeviceConfig.so" "$DEST/armeabi-v7a/"
echo "SDK PAX preparado en $DEST"
