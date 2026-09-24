#!/usr/bin/env bash
# Actualiza los paquetes MotorDsl.* de los samples Nuget y compila/lanza los
# cinco samples en cadena. Las opciones se pasan a cada run-*.sh
# (p. ej. ./run-All.sh --sin-instalar). Sigue con el próximo si uno falla y al
# final informa cuáles fallaron.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

FALLARON=()
./update-packages.sh || FALLARON+=("update-packages")
for s in MotorDsl.Integrated.MultaApp MotorDsl.MultaApp MotorDsl.Nuget.Integrated.MultaApp \
         MotorDsl.Nuget.MultaApp MotorDsl.SampleApp; do
    "./run-$s.sh" "$@" || FALLARON+=("$s")
done

echo
echo "APKs en $(pwd)/OUTPUTs:"
ls -lh OUTPUTs/*.apk 2>/dev/null || echo "  (ninguno)"
if [ "${#FALLARON[@]}" -gt 0 ]; then
    echo "[ERROR] Fallaron: ${FALLARON[*]}"
    exit 1
fi
