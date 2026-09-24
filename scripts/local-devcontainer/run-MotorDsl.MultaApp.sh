#!/usr/bin/env bash
# Compila samples/MotorDsl.MultaApp a APK en un contenedor efímero y lo lanza en el teléfono.
# Opciones: ver run-sample.sh (--sin-instalar, --rid, --url).
exec "$(dirname "${BASH_SOURCE[0]}")/run-sample.sh" MotorDsl.MultaApp "$@"
