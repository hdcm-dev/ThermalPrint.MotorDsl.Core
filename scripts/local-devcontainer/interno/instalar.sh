#!/usr/bin/env bash
# Instala un APK y lanza la app. Corre dentro del contenedor efímero de adb
# (privilegiado, con /dev/bus/usb) o, con ADB_CONTENEDOR, dentro del contenedor
# que ya tiene el teléfono tomado.
#
# Uso: instalar.sh <apk> <paquete>
set -uo pipefail

APK="$1"; PAQUETE="$2"
PROPIO_SERVIDOR=0

# Si no hay servidor de adb corriendo, este script lo arranca y lo baja al
# final, para liberar el USB. Si ya había uno (contenedor dueño), no se toca.
if ! pgrep -x adb >/dev/null 2>&1; then
    PROPIO_SERVIDOR=1
    adb start-server >/dev/null 2>&1
    sleep 2
fi
terminar() { [ "$PROPIO_SERVIDOR" -eq 1 ] && adb kill-server >/dev/null 2>&1; exit "$1"; }

if ! adb devices | awk 'NR>1 && $2=="device" {f=1} END {exit !f}'; then
    echo
    echo "[ERROR] No hay ningún dispositivo Android conectado/autorizado."
    echo " - Conectá un teléfono por USB con depuración USB habilitada y autorizá la PC."
    echo " - Si otro proceso de adb del host tiene el teléfono tomado, cerralo o indicá"
    echo "   el contenedor que lo tiene: ADB_CONTENEDOR=<contenedor> ./run-<Sample>.sh"
    echo
    echo "Estado actual de adb:"
    adb devices
    echo "El APK quedó igual en OUTPUTs/."
    terminar 1
fi

adb devices
salida="$(adb install -r "$APK" 2>&1)"; rc=$?
echo "$salida"
if [ "$rc" -ne 0 ] && echo "$salida" | grep -qE 'INSTALL_FAILED_UPDATE_INCOMPATIBLE|signatures do not match'; then
    echo "==> La versión instalada está firmada con otra clave (p. ej. un build de Windows)."
    echo "    Se desinstala $PAQUETE (se pierden sus datos) y se instala de nuevo."
    adb uninstall "$PAQUETE" >/dev/null
    adb install "$APK"; rc=$?
fi
[ "$rc" -eq 0 ] || { echo "[ERROR] Falló adb install."; terminar 1; }

echo "==> Lanzando $PAQUETE..."
adb shell monkey -p "$PAQUETE" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1 \
    || echo "[AVISO] No se pudo lanzar la app; quedó instalada."
terminar 0
