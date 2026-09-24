# =============================================================================
# Imagen de build Android de los samples de MotorDsl sobre Linux.
#
# Es DISTINTA de .devcontainer/Dockerfile: aquella cubre solo los TFM net10.0 y
# deja net10.0-android fuera a propósito (~4 GB de JDK + SDK de Android). Esta
# trae exactamente eso: SDK .NET 10 + workload maui-android + JDK 17 + SDK de
# Android con adb. La usan los scripts de esta carpeta en contenedores
# efímeros (`docker run --rm`): no se levanta como devcontainer de VS Code.
#
# Corre como root: el servidor de adb necesita root para abrir el nodo USB del
# teléfono (la ACL del nodo no incluye al usuario que compila), y el APK que
# sale a OUTPUTs/ se devuelve con chown al UID del host.
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y --no-install-recommends \
        openjdk-17-jdk-headless \
        unzip curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV JAVA_HOME=/usr/lib/jvm/java-17-openjdk-amd64 \
    ANDROID_HOME=/opt/android-sdk \
    ANDROID_SDK_ROOT=/opt/android-sdk

# --- SDK de Android ---------------------------------------------------------
# API 36 y build-tools 36.0.0 son lo que espera net10.0-android; android-35
# queda porque algunas dependencias AndroidX todavía compilan contra ella.
RUN mkdir -p ${ANDROID_HOME}/cmdline-tools \
    && curl -fsSL -o /tmp/cmdline.zip \
        https://dl.google.com/android/repository/commandlinetools-linux-13114758_latest.zip \
    && unzip -q /tmp/cmdline.zip -d ${ANDROID_HOME}/cmdline-tools \
    && mv ${ANDROID_HOME}/cmdline-tools/cmdline-tools ${ANDROID_HOME}/cmdline-tools/latest \
    && rm /tmp/cmdline.zip

ENV PATH=${ANDROID_HOME}/cmdline-tools/latest/bin:${ANDROID_HOME}/platform-tools:${PATH}

RUN yes | sdkmanager --licenses > /dev/null \
    && sdkmanager --install \
        "platform-tools" \
        "platforms;android-36" \
        "platforms;android-35" \
        "build-tools;36.0.0" \
        "build-tools;35.0.0" \
    && rm -rf ${ANDROID_HOME}/.temp

# --- Workload MAUI ----------------------------------------------------------
# Solo android: el workload ios no existe para Linux, y los .csproj de los
# samples ya declaran net10.0-ios únicamente en Windows/macOS.
RUN dotnet workload install maui-android --skip-sign-check \
    && dotnet nuget locals http-cache --clear

# dotnet-outdated para update-packages.sh.
RUN dotnet tool install --tool-path /opt/dotnet-tools dotnet-outdated-tool
ENV PATH=/opt/dotnet-tools:${PATH}

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

WORKDIR /build
