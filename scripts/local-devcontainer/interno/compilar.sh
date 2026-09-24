#!/usr/bin/env bash
# Corre DENTRO del contenedor de build. El repo llega montado en /src en solo
# lectura; se copia a /build sin bin/, obj/ ni .git, y se compila la copia. Así
# el árbol del host no recibe artefactos, y al borrarse el contenedor no queda
# nada salvo el APK en /out (= OUTPUTs/).
#
# Uso: compilar.sh <Sample> [<rid>] [<backendBaseUrl>]
set -euo pipefail

SAMPLE="$1"; RID="${2:-}"; URL="${3:-}"

export NUGET_PACKAGES=/cache/nuget
KEYSTORE=/cache/keystore/debug.keystore

echo "==> Copiando el repo a /build/repo (sin bin/, obj/, .git)..."
mkdir -p /build/repo
tar -C /src \
    --exclude=./.git --exclude=./.vs --exclude=./out \
    --exclude=bin --exclude=obj \
    --exclude=./scripts/local-devcontainer/OUTPUTs \
    -cf - . | tar -C /build/repo -xf -

PROYECTO_DIR="/build/repo/samples/$SAMPLE"
CSPROJ="$PROYECTO_DIR/$SAMPLE.csproj"

if [ -n "$URL" ]; then
    CONFIG="$PROYECTO_DIR/Resources/Raw/motordsl-config.json"
    echo "==> backendBaseUrl = $URL (solo en la copia)"
    sed -i "s|\"backendBaseUrl\":[[:space:]]*\"[^\"]*\"|\"backendBaseUrl\": \"$URL\"|" "$CONFIG"
    grep -q "\"backendBaseUrl\": \"$URL\"" "$CONFIG" || { echo "[ERROR] No se pudo patchear $CONFIG" >&2; exit 1; }
fi

# Keystore de firma persistente en el volumen de caché. Con el keystore
# autogenerado por el SDK, cada contenedor efímero firmaría con una clave nueva
# y `adb install -r` sobre el APK anterior fallaría por firma distinta.
if [ ! -f "$KEYSTORE" ]; then
    echo "==> Generando keystore de firma en el volumen de caché..."
    mkdir -p "$(dirname "$KEYSTORE")"
    keytool -genkeypair -v -keystore "$KEYSTORE" -alias androiddebugkey \
        -storepass android -keypass android -keyalg RSA -keysize 2048 \
        -validity 10000 -dname "CN=Android Debug,O=Android,C=US" >/dev/null 2>&1
fi

ARGS=(
    -f net10.0-android
    -c Release
    -p:AndroidPackageFormat=apk
    -p:EmbedAssembliesIntoApk=true
    -p:AndroidKeyStore=true
    -p:AndroidSigningKeyStore="$KEYSTORE"
    -p:AndroidSigningKeyAlias=androiddebugkey
    -p:AndroidSigningKeyPass=android
    -p:AndroidSigningStorePass=android
)
# Sin RID, `dotnet build` arma un APK con las cuatro ABIs de <RuntimeIdentifiers>.
# (`dotnet publish` sin RID resuelve el del host y corta con NU1102: ver cd-android.yml.)
[ -n "$RID" ] && ARGS+=(-p:RuntimeIdentifier="$RID")

echo "==> dotnet build ${ARGS[*]}"
echo "    Tarda varios minutos la primera vez."
dotnet build "$CSPROJ" "${ARGS[@]}"

APK="$(find "$PROYECTO_DIR/bin/Release" -name '*-Signed.apk' | head -1)"
[ -n "$APK" ] || { echo "[ERROR] No se encontró ningún *-Signed.apk" >&2; exit 1; }
echo "==> APK: ${APK#/build/repo/}"

cp "$APK" "/out/$SAMPLE.apk"
chown "$HOST_UID:$HOST_GID" "/out/$SAMPLE.apk"
