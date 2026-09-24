# Scripts local-devcontainer — build y run en Android desde Linux

Versión Linux de [`../local/`](../local/Readme.md). En vez de usar el SDK de
Android del host, compila cada sample dentro de un **contenedor Docker efímero**
que trae .NET 10 + workload `maui-android` + JDK 17 + SDK de Android. El host
solo necesita Docker.

Al terminar, **el contenedor se borra** (`docker run --rm`, y un `trap` lo borra
también si cortás con Ctrl+C). El APK queda en
[`OUTPUTs/<Sample>.apk`](OUTPUTs/), y el árbol del repo no recibe `bin/` ni `obj/`:
el contenedor compila una copia.

---

## 📋 Catálogo

| Script | Equivale a | Qué hace |
|---|---|---|
| `run-MotorDsl.SampleApp.sh` | `run-MotorDsl.SampleApp.bat` | Compila `samples/MotorDsl.SampleApp/`, instala y lanza |
| `run-MotorDsl.MultaApp.sh` | `run-MotorDsl.MultaApp.bat` | Ídem `samples/MotorDsl.MultaApp/` |
| `run-MotorDsl.Integrated.MultaApp.sh` | `run-MotorDsl.Integrated.MultaApp.bat` | Ídem `samples/MotorDsl.Integrated.MultaApp/` |
| `run-MotorDsl.Nuget.MultaApp.sh` | `run-MotorDsl.Nuget.MultaApp.bat` | Ídem `samples/MotorDsl.Nuget.MultaApp/` |
| `run-MotorDsl.Nuget.Integrated.MultaApp.sh` | `run-MotorDsl.Nuget.Integrated.MultaApp.bat` | Ídem `samples/MotorDsl.Nuget.Integrated.MultaApp/` |
| `run-All.sh` | `run-All.bat` | `update-packages.sh` y los cinco samples en cadena |
| `update-packages.sh` | `update-packages.bat` | Sube `MotorDsl.*` de los dos samples Nuget con `dotnet-outdated` |
| `limpiar.sh` | — | Borra contenedores huérfanos; con `--todo`, también el volumen de caché y la imagen |

Internos (no se llaman a mano): `run-sample.sh` (lo que hacen los `run-*.sh`),
`lib.sh`, `Dockerfile` e `interno/` (lo que corre dentro del contenedor).

---

## 🚀 Uso típico

```bash
cd scripts/local-devcontainer
./run-MotorDsl.Nuget.Integrated.MultaApp.sh            # APK + instalar + lanzar
./run-MotorDsl.MultaApp.sh --sin-instalar              # solo el APK
./run-MotorDsl.MultaApp.sh --url http://192.168.0.10:5000
./run-All.sh --sin-instalar                            # los cinco APK
```

| Opción | Efecto |
|---|---|
| `--sin-instalar` | Solo genera el APK en `OUTPUTs/`. |
| `--rid <rid>` | Un único RID (`android-arm64`, `android-arm`…). Sin él, el APK lleva las cuatro ABIs que declara el `.csproj` (~50 MB). |
| `--url <url>` | Reescribe `backendBaseUrl` de `Resources/Raw/motordsl-config.json` **en la copia** que compila el contenedor; tu árbol no se modifica (a diferencia de los `.bat` de `../mobile/`). |

Cada `run-*.sh` hace:

1. Si no existe la imagen `motordsl-android-build:net10`, la arma con el
   `Dockerfile` de esta carpeta (una sola vez, ~5 min, ~6 GB).
2. Levanta un contenedor efímero que copia el repo, compila con
   `dotnet build -c Release -f net10.0-android` (APK firmado, `EmbedAssembliesIntoApk=true`)
   y deja el APK en `OUTPUTs/<Sample>.apk` con tu usuario como dueño.
3. Si no se pasó `--sin-instalar`: instala con `adb install -r` y lanza la app.
   Si no hay teléfono autorizado, muestra ayuda y sale con código 1; el APK
   queda igual en `OUTPUTs/`.

Instalar a mano un APK ya generado:

```bash
adb install -r OUTPUTs/MotorDsl.SampleApp.apk
```

---

## 📱 Cómo llega al teléfono

Dos servidores de adb no pueden tomar el mismo USB. Por eso:

- Si **otro contenedor ya corre un servidor de adb** (por ejemplo
  `gda-core-app-dev`), el script lo detecta y instala **a través de él**
  (`docker cp` del APK + `adb install` ahí adentro; después borra lo copiado).
- Si no, levanta un segundo contenedor efímero, privilegiado y con
  `/dev/bus/usb` montado, cuyo adb corre como root (la ACL del nodo USB no
  incluye al usuario que compila). También se borra al terminar.

Forzar uno u otro: `ADB_CONTENEDOR=<nombre> ./run-….sh` o
`ADB_CONTENEDOR=ninguno ./run-….sh`.

---

## 🗄️ Qué persiste entre corridas

| Qué | Dónde | Para qué |
|---|---|---|
| Imagen `motordsl-android-build:net10` | Docker | SDK + workload: no reinstalarlos en cada corrida |
| Volumen `motordsl-android-cache` | `/cache/nuget` | Caché de paquetes NuGet (MAUI pesa ~1 GB) |
| Volumen `motordsl-android-cache` | `/cache/keystore/debug.keystore` | Firma estable entre corridas |

El keystore existe porque, con el que genera el SDK, cada contenedor efímero
firmaría con una clave nueva y `adb install -r` sobre la versión anterior
fallaría. Si el teléfono tiene una versión firmada con otra clave (por ejemplo
un build de Windows), el script **desinstala y reinstala** (se pierden los datos
de esa app) y lo avisa.

```bash
./limpiar.sh          # solo contenedores que hayan quedado vivos
./limpiar.sh --todo   # además volumen de caché y la imagen
```

---

## 🔄 update-packages.sh

Corre `dotnet-outdated` (ya instalado en la imagen) sobre una copia del repo y
devuelve **solo** los dos `.csproj` de los samples Nuget si cambiaron. Revisar
con `git diff samples/` y commitear.

Mantiene la limitación de `../local/update-packages.bat`: el filtro es
`--include MotorDsl`, así que sube todos los `MotorDsl.*`.

---

## 🐛 Troubleshooting

- **"No hay ningún dispositivo Android conectado/autorizado"**: conectar por USB
  con depuración habilitada y aceptar el prompt en el equipo. Si hay un adb
  **del host** (no de un contenedor) con el teléfono tomado, cerrarlo con
  `adb kill-server`.
- **`INSTALL_FAILED_UPDATE_INCOMPATIBLE`**: lo resuelve el script solo
  (desinstala y reinstala).
- **Cambié el `Dockerfile`**: `docker image rm motordsl-android-build:net10`, y la
  próxima corrida lo rearma.
- **`net10.0-ios`**: no se puede compilar en Linux (el workload `ios` no existe
  para esta plataforma); los `.csproj` de los samples ya lo excluyen fuera de
  Windows/macOS.
