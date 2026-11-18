#!/usr/bin/env bash
#
# Calcula la version unificada de los 9 paquetes y crea el tag que dispara la publicacion.
#
# POR QUE ESTE SCRIPT Y NO UN PORT DE publish-motordsl-nuget.bat:
#   El .bat publica los 9 paquetes porque corre en Windows, donde existe el workload `ios`.
#   En Linux ese workload NO EXISTE ("Workload ID ios isn't supported on this platform"), y
#   `dotnet pack` de un proyecto multi-TFM exige compilar todos sus TFM. Un port a .sh solo
#   podria empaquetar 7 de 9: quedarian afuera MotorDsl.Bluetooth y MotorDsl.Maui.
#
#   La publicacion completa la hace `.github/workflows/cd-nuget.yml`, que reparte el pack
#   entre el runner Linux (6 paquetes net10.0) y un runner macOS (los 2 multi-TFM y
#   MotorDsl.Pax, net10.0-android). Lo unico que ese workflow no hace es elegir el numero de
#   version: lo toma del tag.
#
#   MotorDsl.Pax es el contrato IPaxPrinterDevice, el transport y el simulador: no trae
#   binarios del SDK del fabricante y la implementacion real es externa. Ver
#   docs/09_devops/guia-publicacion-nuget_v1.0.md.
#
#   Este script cubre exactamente ese hueco. No necesita el SDK de .NET: solo consulta
#   nuget.org por HTTPS y crea un tag de git. Corre en cualquier Linux, dentro o fuera del
#   devcontainer.
#
# USO:
#   ./scripts/nuget/tag-next-version.sh              # calcula, muestra y pide confirmacion
#   ./scripts/nuget/tag-next-version.sh --dry-run    # solo calcula y muestra
#   ./scripts/nuget/tag-next-version.sh 1.0.14       # fuerza una version concreta
#
set -euo pipefail

PAQUETES=(
    MotorDsl.Core
    MotorDsl.Parser
    MotorDsl.Rendering
    MotorDsl.Extensions
    MotorDsl.Printing.Abstractions
    MotorDsl.Network
    MotorDsl.Bluetooth
    MotorDsl.Maui
    MotorDsl.Pax
)

DRY_RUN=0
VERSION_FORZADA=""
for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        -h|--help) sed -n '2,28p' "$0" | sed 's/^# \?//'; exit 0 ;;
        *) VERSION_FORZADA="$arg" ;;
    esac
done

command -v curl    >/dev/null || { echo "ERROR: falta curl" >&2; exit 1; }
command -v python3 >/dev/null || { echo "ERROR: falta python3" >&2; exit 1; }
command -v git     >/dev/null || { echo "ERROR: falta git" >&2; exit 1; }

# --- Version unificada -------------------------------------------------------------------
# Mismo criterio que get-next-version.ps1 + publish-motordsl-nuget.bat: por cada paquete se
# toma la ultima estable X.Y.Z publicada y se incrementa el patch; la version unificada es el
# MAXIMO de esos candidatos. Los 9 se publican con ese numero para evitar NU1605 (downgrade de
# dependencia) en apps que mezclen varios.
#
# Un paquete que nunca se publico da 404 y cae al fallback 1.0.0. Como 1.0.0 es el menor, no
# altera el maximo: el paquete nuevo sale directamente con la version unificada vigente.
if [[ -n "$VERSION_FORZADA" ]]; then
    VERSION="$VERSION_FORZADA"
    echo "Version forzada por parametro: $VERSION"
else
    echo "Consultando ultimas versiones publicadas en nuget.org ..."
    CANDIDATAS=()
    for p in "${PAQUETES[@]}"; do
        id_lower=$(echo "$p" | tr '[:upper:]' '[:lower:]')
        # El curl se captura aparte y no en un pipe: con `set -o pipefail`, el 404 de un
        # paquete todavia no publicado haria fallar la tuberia entera y dispararia un
        # fallback duplicado. Un cuerpo vacio ya lo resuelve el parser.
        cuerpo=$(curl -sf --max-time 20 \
            "https://api.nuget.org/v3-flatcontainer/${id_lower}/index.json" 2>/dev/null || true)
        siguiente=$(printf '%s' "$cuerpo" | python3 -c '
import sys, json, re
try:
    vs = [v for v in json.load(sys.stdin)["versions"] if re.fullmatch(r"\d+\.\d+\.\d+", v)]
    if not vs: raise ValueError
    m, n, p = map(int, sorted(vs, key=lambda v: tuple(map(int, v.split("."))))[-1].split("."))
    print(f"{m}.{n}.{p+1}")
except Exception:
    print("1.0.0")   # nunca publicado, o respuesta ilegible
')
        printf "   %-32s proxima = %s\n" "$p" "$siguiente"
        CANDIDATAS+=("$siguiente")
    done

    VERSION=$(printf '%s\n' "${CANDIDATAS[@]}" \
      | python3 -c 'import sys; print(max((l.strip() for l in sys.stdin if l.strip()), key=lambda v: tuple(map(int, v.split(".")))))')
fi

TAG="v${VERSION}"
echo
echo "Version unificada: $VERSION   ->   tag $TAG"
echo "Los 9 paquetes se publicaran con esa version."
echo

# --- Verificaciones ----------------------------------------------------------------------
if git rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
    echo "ERROR: el tag $TAG ya existe localmente. Las versiones publicadas son inmutables." >&2
    exit 1
fi
if git ls-remote --exit-code --tags origin "refs/tags/$TAG" >/dev/null 2>&1; then
    echo "ERROR: el tag $TAG ya existe en origin. Las versiones publicadas son inmutables." >&2
    exit 1
fi

RAMA=$(git rev-parse --abbrev-ref HEAD)
if [[ "$RAMA" != "main" ]]; then
    echo "AVISO: estas en la rama '$RAMA', no en main." >&2
fi
if [[ -n "$(git status --porcelain)" ]]; then
    echo "AVISO: hay cambios sin commitear; el tag apuntara al ultimo commit, no a tu working tree." >&2
fi

if [[ $DRY_RUN -eq 1 ]]; then
    echo "--dry-run: no se crea ni se pushea el tag."
    exit 0
fi

read -r -p "Crear y pushear el tag $TAG sobre $(git rev-parse --short HEAD)? [s/N] " RESP
[[ "$RESP" =~ ^[sSyY]$ ]] || { echo "Cancelado."; exit 0; }

git tag -a "$TAG" -m "Release $VERSION"
git push origin "$TAG"

echo
echo "Tag $TAG pusheado."
echo "Dispara cd-nuget.yml, que empaqueta los 9 y los publica en nuget.org:"
echo "  https://github.com/hdcm-dev/ThermalPrint.MotorDsl.Core/actions"
