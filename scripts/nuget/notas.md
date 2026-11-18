# Publicacion de paquetes MotorDsl en nuget.org

## Que usar segun el caso

| Quiero… | Herramienta | Donde corre |
|---|---|---|
| **Publicar los 9 paquetes** | `tag-next-version.sh` -> dispara `cd-nuget.yml` | Linux, sin SDK |
| Publicar sin pasar por CI | `publish-motordsl-nuget.bat` | **Solo Windows o macOS** |
| Solo compilar, sin publicar | `build-motordsl-nuget.bat` | Solo Windows o macOS |

**Por que el `.bat` no se porto a `.sh`.** `dotnet pack` de un proyecto multi-TFM exige
compilar todos sus TFM, y el workload `ios` no existe para Linux: `dotnet workload install ios`
responde *"Workload ID ios isn't supported on this platform"* y el build corta con `NETSDK1178`.
Un port a `.sh` solo podria empaquetar 7 de 9 — quedarian afuera `MotorDsl.Bluetooth` y
`MotorDsl.Maui` —, o sea que seria estrictamente peor que el `.bat`. La ruta completa desde
Linux es el workflow, que reparte el pack entre el runner Linux y uno macOS.

**Que aporta `tag-next-version.sh`.** Lo unico que `cd-nuget.yml` no hace: elegir el numero de
version. Calcula la version unificada consultando nuget.org, verifica que el tag no exista ya
—las versiones publicadas son inmutables— y lo crea. No necesita el SDK: solo `curl`, `python3`
y `git`.

```bash
./scripts/nuget/tag-next-version.sh --dry-run    # ver que version saldria
./scripts/nuget/tag-next-version.sh              # calcular, confirmar y pushear el tag
./scripts/nuget/tag-next-version.sh 1.0.14       # forzar una version concreta
```

---

Script: `publish-motordsl-nuget.bat`

## Que hace

1. Resuelve la API key de nuget.org desde `MOTORDSL_NUGET_API_KEY` (env var)
   o la pide por prompt.
2. Consulta `https://api.nuget.org/v3-flatcontainer/<paquete>/index.json` para
   los **9 paquetes**:
   - `MotorDsl.Core`
   - `MotorDsl.Parser`
   - `MotorDsl.Rendering`
   - `MotorDsl.Extensions`
   - `MotorDsl.Printing.Abstractions`
   - `MotorDsl.Network`
   - `MotorDsl.Bluetooth`
   - `MotorDsl.Maui`
   - `MotorDsl.Pax`

   Calcula `version unificada = max(next(patch) de cada paquete)`. Esto evita
   `NU1605` al consumir desde apps que mezclen varios.
3. Restore + Build Release de las 9 librerias con
   `/p:Version=<unificada> /p:MotorDslVersion=<unificada>`.
4. Restore + `dotnet test` de `MotorDsl.Tests`. Si fallan, aborta antes de
   publicar.
5. `dotnet pack` de las 9 librerias con
   `-p:PackageVersion=<unificada> -p:MotorDslVersion=<unificada>`
   a `./nupkg/`.
6. `dotnet nuget push` a `https://api.nuget.org/v3/index.json` con
   `--skip-duplicate`.
   **Orden de push (por dependencias)**:
   `Printing.Abstractions` → `Core` → `Parser` → `Rendering` → `Extensions` →
   `Network` → `Bluetooth` → `Maui` → `Pax`.
7. Tag git `v<unificada>` y push a origin (dispara el workflow
   `cd-nuget.yml`).
8. Limpia el cache HTTP de NuGet.

## Variable `MotorDslVersion`

El script pasa `-p:MotorDslVersion=<unificada>` adicional a `Version` y
`PackageVersion`. Esto permite que los `.csproj` (e.g. `MotorDsl.Bluetooth`)
referencien internamente esa versión cuando declaren PackageReferences entre
ellos en escenarios futuros sin tener que parsear `Version` en MSBuild.

## Requisitos

- .NET SDK 10 instalado (`dotnet --version`), con las workloads `ios` y `android`
  (`MotorDsl.Pax` es `net10.0-android`).
- API key de nuget.org con scope de push:
  https://www.nuget.org/account/apikeys
- Conexion a internet (para consultar versiones publicadas y para el push).
- Git configurado para push a origin si se quiere taggear.

## Uso

Recomendado (sin que la key quede en pantalla):

```bat
set MOTORDSL_NUGET_API_KEY=oy2x...tu-key...
scripts\nuget\publish-motordsl-nuget.bat
```

Interactivo (la key se ve al tipearla):

```bat
scripts\nuget\publish-motordsl-nuget.bat
```

## Estrategias de versionado (referencia)

El script implementa la opcion **auto-bump de patch**: lee la ultima version
publicada en nuget.org de cada paquete e incrementa el patch
(`1.0.4` -> `1.0.5`). No reescribe los `.csproj`; la version se inyecta en
build/pack via `/p:Version`, `-p:PackageVersion` y `-p:MotorDslVersion`.

Otras opciones disponibles si en el futuro se quiere cambiar el esquema:

- **Manual**: declarar `<Version>X.Y.Z</Version>` en cada `.csproj` y publicar
  tal cual. Si se republica sin tocarlo, el feed devuelve 409
  (`--skip-duplicate` lo absorbe como ADVERTENCIA y no sube nada nuevo).
- **Prerelease por timestamp/commit**:
  `Version = 1.0.0-pre.20260506.1430` o `-ci.<shortsha>`. Se inyecta con
  `/p:VersionSuffix=...` sin tocar el `.csproj`. Cada push genera un paquete
  nuevo y no afecta la version estable.
- **MinVer / Nerdbank.GitVersioning**: versionado derivado de tags de git.
  Mas robusto, requiere agregar un `PackageReference` a cada proyecto y
  taggear releases.

## Notas

- Si un paquete aun no existe en nuget.org, `get-next-version.ps1` devuelve
  el fallback `1.0.0`.
- La indexacion en nuget.org tras un push puede tardar varios minutos
  (especialmente para los paquetes nuevos en su primera publicación).
- Los 9 paquetes se publican con la **misma version unificada** para evitar
  resoluciones cruzadas con `NU1605`.
- El workflow `.github/workflows/cd-nuget.yml` hace lo equivalente desde CI
  usando `secrets.NUGET_API_KEY`. Este script local sirve para publicaciones
  puntuales sin pasar por GitHub Actions.
- `MotorDsl.Bluetooth` y `MotorDsl.Maui` tienen TFMs duales
  (`net10.0-android;net10.0-ios`). El pack genera ambos targets dentro del
  mismo `.nupkg`. Para iOS, el contenido de `MotorDsl.Bluetooth` es estructural:
  todas sus operaciones lanzan `PlatformNotSupportedException` en runtime.
- **Este script solo corre en Windows o macOS.** `dotnet pack` de un proyecto
  multi-TFM exige compilar todos sus TFM, y el workload `ios` no existe para
  Linux: `dotnet workload install ios` responde *"Workload ID ios isn't
  supported on this platform"* y el build corta con `NETSDK1178`. Los otros seis
  paquetes son `net10.0` puro y se empaquetan en cualquier plataforma, y
  `MotorDsl.Pax` (`net10.0-android`) en cualquiera con la workload `android`.
- `MotorDsl.Network` es `net10.0` puro **a proposito**: al no llevar sufijo de
  plataforma, es consumible desde `net10.0-android` y `net10.0-ios` por herencia
  de TFM, y da soporte de impresion en iOS sin necesitar una maquina Apple para
  construirse.
- **`MotorDsl.Pax` es el contrato y el simulador.** Trae `IPaxPrinterDevice`, el transporte,
  el mapeo de errores, la impresora simulada y el perfil A910S, y ningun binario del SDK del
  fabricante. La implementacion real del dispositivo es externa: un paquete aparte implementa
  `IPaxPrinterDevice` y la registra con `PaxPrinterOptions.RealDeviceFactory`. Sin ella, en el
  equipo el modo Real responde `Connection`. Detalle en
  `docs/09_devops/guia-publicacion-nuget_v1.0.md`.
