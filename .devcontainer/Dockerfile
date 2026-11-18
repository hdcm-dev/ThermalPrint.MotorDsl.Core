# Entorno de desarrollo de MotorDsl.
#
# Base: mcr.microsoft.com/dotnet/sdk:10.0 (GA). NO se usa
# mcr.microsoft.com/devcontainers/dotnet:1-10.0 porque hoy entrega el SDK
# 10.0.100-rc.2, y el CI (DOTNET_VERSION 10.0.102) trabaja sobre versiones GA.
#
# ALCANCE: cubre lo que Linux puede compilar, que son los TFM `net10.0`:
# Core, Parser, Rendering, Extensions, Printing.Abstractions, Network y Tests.
#
# NO cubre:
#   - net10.0-android -> necesita JDK + Android SDK (~4 GB). Se valida en el
#     workflow cd-android (ubuntu-latest), que instala la workload en el job.
#   - net10.0-ios     -> IMPOSIBLE en Linux: el workload `ios` no existe para
#     esta plataforma ("Workload ID ios isn't supported on this platform"), y
#     el build falla con NETSDK1178. Se valida solo en un runner macOS.
FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG USERNAME=vscode
ARG USER_UID=1000
ARG USER_GID=$USER_UID

# La imagen base corre como root y no trae un usuario de desarrollo. Se crea uno
# no privilegiado para que los archivos generados dentro del contenedor queden
# con el UID del host y no aparezcan como root en el árbol de trabajo.
#
# El GID/UID 1000 ya está tomado en la imagen base (usuario `app`), así que el
# paso es idempotente: reutiliza el grupo si existe y renombra el usuario que
# ocupe ese UID en vez de fallar con "already exists".
RUN if ! getent group $USER_GID >/dev/null; then groupadd --gid $USER_GID $USERNAME; fi \
 && if getent passwd $USER_UID >/dev/null; then \
        usermod --login $USERNAME --home /home/$USERNAME --move-home \
                --shell /bin/bash "$(getent passwd $USER_UID | cut -d: -f1)"; \
    else \
        useradd --uid $USER_UID --gid $USER_GID -m -s /bin/bash $USERNAME; \
    fi

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

USER $USERNAME
WORKDIR /workspace
