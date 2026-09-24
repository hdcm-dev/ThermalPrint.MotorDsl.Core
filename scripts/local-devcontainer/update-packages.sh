#!/usr/bin/env bash
# Sube las <PackageReference> MotorDsl.* de los dos samples Nuget a la última
# versión estable de nuget.org, con dotnet-outdated dentro de un contenedor
# efímero. Trabaja sobre una copia y devuelve solo los dos .csproj, así el
# restore no deja bin/ ni obj/ de root en el árbol.
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

asegurar_imagen
correr_en_contenedor "update" \
    -v "$REPO_ROOT":/src \
    -- bash /src/scripts/local-devcontainer/interno/actualizar-paquetes.sh

echo
echo "Listo. Revisá los .csproj (git diff samples/) y commiteá si los cambios son correctos."
