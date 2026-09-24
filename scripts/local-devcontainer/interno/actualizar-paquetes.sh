#!/usr/bin/env bash
# Corre DENTRO del contenedor de update-packages.sh. Actualiza los MotorDsl.* de
# los dos samples Nuget sobre una copia y devuelve solo los .csproj a /src.
set -euo pipefail

export NUGET_PACKAGES=/cache/nuget

mkdir -p /build/repo
tar -C /src --exclude=./.git --exclude=bin --exclude=obj --exclude=./out \
    --exclude=./scripts/local-devcontainer/OUTPUTs -cf - . | tar -C /build/repo -xf -
cd /build/repo

PROYECTOS=(
    samples/MotorDsl.Nuget.MultaApp/MotorDsl.Nuget.MultaApp.csproj
    samples/MotorDsl.Nuget.Integrated.MultaApp/MotorDsl.Nuget.Integrated.MultaApp.csproj
)

echo "=== Paquetes desactualizados (antes) ==="
for p in "${PROYECTOS[@]}"; do dotnet outdated "$p" || true; done

echo
echo "=== Actualizando MotorDsl.* a la última versión estable ==="
for p in "${PROYECTOS[@]}"; do dotnet outdated "$p" --upgrade --include MotorDsl; done

echo
echo "=== Verificación final ==="
for p in "${PROYECTOS[@]}"; do dotnet outdated "$p" || true; done

# `cp` sobre un archivo existente conserva su dueño: los .csproj siguen siendo del usuario del host.
for p in "${PROYECTOS[@]}"; do
    cmp -s "$p" "/src/$p" || { cp "$p" "/src/$p"; echo "Actualizado: $p"; }
done
