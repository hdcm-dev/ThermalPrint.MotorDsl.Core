# Guía de Publicación y Consumo de Paquetes NuGet  
**Archivo:** guia-publicacion-nuget_v1.0.md  
**Proyecto:** Motor DSL (Librería de Generación de Documentos)  
**Versión:** v1.0  
**Estado:** Aprobado  
**Fecha:** 2026-04-02  
**Owner:** Arquitectura / DevOps  

---

## 1. Propósito

Este documento describe los pasos necesarios para empaquetar, publicar y consumir los componentes del Motor DSL como paquetes NuGet, permitiendo su reutilización en cualquier proyecto .NET (consola, API, MAUI, Blazor, etc.).

---

## 2. Conceptos fundamentales

### 2.1 ¿Qué es un paquete NuGet?

NuGet es el administrador de paquetes oficial para .NET. Un paquete NuGet (`.nupkg`) es un archivo comprimido que contiene:

- Las DLLs compiladas de la librería
- Metadata (autor, versión, descripción, licencia)
- Declaración de dependencias hacia otros paquetes

Cuando un consumidor instala un paquete, NuGet descarga automáticamente ese paquete **y todas sus dependencias transitivas**.

### 2.2 ¿Qué es una dependencia transitiva?

Si el paquete **A** depende de **B**, y **B** depende de **C**, al instalar **A** se descargan automáticamente **B** y **C**. El consumidor no necesita instalarlos manualmente.

```
MotorDsl.Extensions
  └── MotorDsl.Core
  └── MotorDsl.Parser       → depende de Core
  └── MotorDsl.Rendering    → depende de Core
```

Al instalar `MotorDsl.Extensions`, NuGet trae transitivamente Core, Parser y Rendering.

### 2.3 Estrategias de empaquetado: monolítico vs granular

Existen dos enfoques principales para publicar una librería compuesta por varios proyectos:

| Estrategia | Descripción | Ejemplo de la industria |
|---|---|---|
| **Monolítica** | Un solo paquete con todo | `Newtonsoft.Json` |
| **Granular** | Varios paquetes con dependencias entre sí | `Microsoft.Extensions.DependencyInjection` + `Microsoft.Extensions.DependencyInjection.Abstractions` |

#### Ventajas del enfoque granular
- El consumidor instala solo lo que necesita (ej: solo los contratos/interfaces)
- Menor huella de dependencias en proyectos livianos
- Permite versionado independiente por componente

#### Ventajas del enfoque monolítico
- Menor complejidad operativa (1 versión, 1 publicación)
- El consumidor instala un solo paquete
- No hay problemas de sincronización de versiones entre paquetes

### 2.4 Cuadro comparativo de opciones para este proyecto

| Opción | Paquetes publicados | El consumidor instala | Complejidad |
|---|---|---|---|
| **A) Granular (9 paquetes)** (actual) | `Core`, `Parser`, `Rendering`, `Extensions`, `Printing.Abstractions`, `Network`, `Bluetooth`, `Maui`, `Pax` | `MotorDsl.Extensions` (motor core) o `MotorDsl.Maui` + `MotorDsl.Bluetooth` (apps MAUI) | Alta — versiones sincronizadas |
| **B) 1 paquete** | `MotorDsl` (todo junto) | `MotorDsl` | Baja — 1 versión, 1 artefacto |
| **C) 2 paquetes** | `MotorDsl.Core` (contratos) + `MotorDsl` (todo) | `MotorDsl` | Media — separación contratos/implementación |

### 2.5 Decisión adoptada

Se adopta el **enfoque granular (9 paquetes)**. El punto de entrada depende del escenario de consumo:

- **Motor core** (consola, API, Worker, etc.): instalar `MotorDsl.Extensions`, que trae transitivamente `Core`, `Parser` y `Rendering`, y expone `AddMotorDslEngine()`.
- **Apps MAUI** (impresión térmica Bluetooth + renderers PDF/bitmap/preview): instalar además `MotorDsl.Maui` y `MotorDsl.Bluetooth`. El punto de entrada es `AddMotorDslMaui()` (extiende `MotorDslBuilder`) y `AddBluetoothPrinterTransport()`. `MotorDsl.Maui` arrastra transitivamente `MotorDsl.Printing.Abstractions`.

> **Regla para el consumidor:** para el motor core, instalar `MotorDsl.Extensions`. Para apps MAUI, instalar `MotorDsl.Maui` y `MotorDsl.Bluetooth` (que traen el resto de forma transitiva).

---

## 3. Arquitectura de paquetes

### 3.1 Diagrama de dependencias

```
┌──────────────────────────────────┐
│       MotorDsl.Extensions        │  ← El consumidor instala ESTE
│  (DI, fluent API, AddMotorDsl)   │
└──────┬──────────┬──────────┬─────┘
       │          │          │
       ▼          ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────────┐
│  .Core   │ │  .Parser │ │  .Rendering  │
│(contratos│ │(JSON DSL │ │(EscPos, Text │
│ modelos) │ │ parser)  │ │  renderers)  │
└──────────┘ └────┬─────┘ └──────┬───────┘
                  │              │
                  ▼              ▼
             ┌──────────────────────┐
             │     MotorDsl.Core    │
             └──────────────────────┘
```

### 3.2 Tabla de paquetes

Se publican **9 paquetes** NuGet:

| Paquete | Contenido | Dependencias NuGet | Dependencias internas |
|---|---|---|---|
| `MotorDsl.Core` | Contratos, modelos, evaluador, layout engine | Ninguna | — |
| `MotorDsl.Parser` | Parser JSON DSL → `DocumentTemplate` | — | → Core |
| `MotorDsl.Rendering` | `EscPosRenderer`, `TextRenderer` | — | → Core |
| `MotorDsl.Extensions` | DI, fluent API `AddMotorDslEngine()` | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Core, Parser, Rendering |
| `MotorDsl.Printing.Abstractions` | Contratos de impresión (`IThermalPrinterService`, `IThermalPrinterTransport`, `IDiagnosticsReportProvider`), `AddMotorDslPrinting()` | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Core |
| `MotorDsl.Network` | Transport de red TCP 9100 (RAW/JetDirect), multiplataforma, `AddNetworkPrinterTransport()` | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Printing.Abstractions |
| `MotorDsl.Bluetooth` | Transport Bluetooth Classic SPP (Android; iOS lanza `PlatformNotSupportedException`), `AddBluetoothPrinterTransport()` | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Printing.Abstractions |
| `MotorDsl.Maui` | Controles MAUI y renderers (PDF, ESC/POS bitmap, SkiaSharp), `AddMotorDslMaui()` | `SkiaSharp.Views.Maui.Controls`, `PdfSharpCore`, `QRCoder` | → Core, Rendering, Extensions, Printing.Abstractions |
| `MotorDsl.Pax` | Transport `pax` para la impresora interna del PAX A910S (envía el PNG de raster-preview), mapeo de errores, impresora simulada y perfil A910S. **Es el contrato `IPaxPrinterDevice`; la implementación real es externa** (§3.4) | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Printing.Abstractions |

> `MotorDsl.Bluetooth` y `MotorDsl.Maui` multi-targetan `net10.0-android;net10.0-ios`; `MotorDsl.Pax` es `net10.0-android`; los demás son `net10.0`.
> Esa diferencia decide dónde se pueden empaquetar: los `net10.0` en cualquier plataforma, esos dos **solo en macOS o Windows**, porque el workload `ios` no existe para Linux, y `MotorDsl.Pax` en cualquier máquina con la workload `android` (en `cd-nuget.yml` va en el job `pack-platform`, `macos-15`).

### 3.3 ¿Qué instala el consumidor?

```bash
dotnet add package MotorDsl.Extensions --version 1.0.0
```

Esto descarga transitivamente: `MotorDsl.Core`, `MotorDsl.Parser`, `MotorDsl.Rendering` y sus dependencias externas (consumo del motor core).

Para una **app MAUI** con impresión térmica, instalar además:

```bash
dotnet add package MotorDsl.Maui
dotnet add package MotorDsl.Bluetooth
```

`MotorDsl.Maui` arrastra transitivamente `MotorDsl.Printing.Abstractions` (y `SkiaSharp`, `PdfSharpCore`, `QRCoder`).

### 3.4 `MotorDsl.Pax`: contrato y punto de extensión

`MotorDsl.Pax` es el **contrato** de la impresora interna del PAX A910S y todo lo que no depende del SDK del fabricante:

| Trae | Qué es |
|---|---|
| `IPaxPrinterDevice` | El contrato entre el transport y la impresora: abrir, `GetStatusAsync` y `PrintAsync` con el código crudo (0 = ok), fallas por excepción como `PaxSdkException` |
| `PaxPrinterTransport` (`Kind = "pax"`) | Envía el PNG de raster-preview y traduce los errores con `PaxErrorMapping` |
| `SimulatedPaxPrinterDevice` | Impresora simulada que vuelca el PNG a disco; para builds de desarrollo |
| `PaxProfiles` | Perfil A910S |
| `AddPaxPrinterTransport()` | Registro en DI, con `PaxPrinterOptions` |

**No trae ningún binario del SDK del fabricante** (ni jar ni `.so`): el `.aar` del paquete sólo lleva el código administrado. La **implementación real** de `IPaxPrinterDevice` es **externa**: la aporta un paquete aparte y se registra por el punto de extensión `PaxPrinterOptions.RealDeviceFactory`:

```csharp
services.AddPaxPrinterTransport(o =>
{
    o.Mode = PaxPrinterMode.Real;
    o.RealDeviceFactory = opciones => new MiDispositivoPax();   // implementa IPaxPrinterDevice
});
```

| Modo | Dispositivo que se usa |
|---|---|
| `Simulated` | `SimulatedPaxPrinterDevice`; `RealDeviceFactory` **nunca** se invoca |
| `Real` con `RealDeviceFactory` | Lo que devuelve la fábrica (se invoca una vez, al resolver el dispositivo) |
| `Real` sin fábrica, o fábrica que devuelve `null` | Conectar responde `Connection` («Avisá a soporte»); la app no se cae |

**Por qué:** el material del SDK del fabricante no trae licencia de redistribución, así que no se versiona ni se publica desde este repositorio. Separar contrato e implementación deja el paquete público libre de ese material y permite a cualquiera escribir su propia implementación de `IPaxPrinterDevice`. Como el contenido del paquete ya no depende de la máquina que empaqueta, no hace falta ninguna propiedad MSBuild para publicarlo.

---

## 4. Prerrequisitos

### 4.1 Herramientas

- .NET SDK 10.0 o superior
- Git (para tags de versionado)

### 4.2 Cuentas y tokens

El destino productivo real es **NuGet.org**.

| Destino | Cuenta requerida | Token / API Key |
|---|---|---|
| **NuGet.org** (destino real) | Cuenta en [nuget.org](https://www.nuget.org) dueña de los paquetes `MotorDsl.*` | **Ninguna guardada.** El pipeline usa *Trusted Publishing* (§4.3) y recibe una API key temporal por ejecución. Sólo la publicación manual desde una máquina local (§6.5) usa una API key propia con scope **Push** |

> Para **consumir** desde nuget.org no se requiere autenticación.

### 4.3 Trusted Publishing (publicación desde GitHub Actions sin API key)

`cd-nuget.yml` publica con [Trusted Publishing de nuget.org](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing): no hay API key de larga vida en los secretos del repositorio.

1. El job `publish` declara `permissions: id-token: write` (además de `contents: read`), y GitHub le emite un token OIDC firmado con los datos del repositorio y del workflow.
2. El paso `NuGet/login@v1` canjea ese token en nuget.org (`https://www.nuget.org/api/v2/token`) y devuelve en su salida `NUGET_API_KEY` una API key **temporal: vale 1 hora**, y cada token da una sola key. Por eso el paso corre inmediatamente antes del push.
3. `dotnet nuget push` usa `${{ steps.nuget-login.outputs.NUGET_API_KEY }}`.

nuget.org emite la key sólo si el token coincide con una **política de Trusted Publishing** creada en nuget.org (usuario → *Trusted Publishing*) por el dueño de los paquetes:

| Campo de la política | Valor para este repositorio |
|---|---|
| Owner | La cuenta de nuget.org dueña de `MotorDsl.*`; es el `user` que recibe `NuGet/login` en el workflow (nombre de perfil, **no** el email) |
| Repository Owner / Repository | `hdcm-dev` / `ThermalPrint.MotorDsl.Core` |
| Workflow File | `cd-nuget.yml` (sólo el nombre, sin `.github/workflows/`) |
| Environment | Vacío: el job no usa environments de GitHub |
| Scopes | Publicar paquetes nuevos y versiones nuevas, con el glob que cubra `MotorDsl.*` |

Consecuencias:

- **Renombrar `cd-nuget.yml`, mover el repositorio o agregarle un `environment` al job** deja de coincidir con la política: hay que actualizarla en nuget.org antes de etiquetar.
- La key temporal no se guarda ni se rota: vence sola.
- Un push a `main` no pide key: el login y el push corren sólo con tag `v*` o `workflow_dispatch`.

---

## 5. Metadata NuGet en los proyectos

### 5.1 Estado actual

Los **7 `.csproj` ya tienen metadata de paquete completa**: `PackageId`, `Authors`, `Description`, `PackageTags`, `PackageLicenseExpression`, `RepositoryUrl` y `PackageReadmeFile`. No hace falta agregarla; esta sección documenta los valores reales en uso.

Valores compartidos (presentes en cada proyecto empaquetable):

```xml
<PropertyGroup>
  <Authors>Fernando Rafael Filipuzzi</Authors>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <RepositoryUrl>https://github.com/Aplicada-Streaming/PrintThermal_Motor_Maui</RepositoryUrl>
  <PackageTags>maui;thermal;escpos;dsl;printing</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>
```

> **Excepciones de los valores compartidos:**
> - `MotorDsl.Bluetooth` usa `Authors = UTN FRP TUP Aplicada 2025`.
> - `MotorDsl.Maui` agrega `;skia;pdf` a `PackageTags` (`maui;thermal;escpos;dsl;printing;skia;pdf`).

La versión NO se fija en el `.csproj`: se inyecta en build/pack vía `/p:Version`, `-p:PackageVersion` y `-p:MotorDslVersion` (ver [estrategia-versionado_v1.0.md](estrategia-versionado_v1.0.md)).

### 5.2 `PackageId` y `Description` por paquete (valores reales)

| Paquete | `PackageId` | `Description` |
|---|---|---|
| Core | `MotorDsl.Core` | Motor DSL para documentos e impresión térmica ESC/POS. Parser, Evaluator, LayoutEngine y contratos |
| Parser | `MotorDsl.Parser` | Parser JSON DSL a DocumentTemplate |
| Rendering | `MotorDsl.Rendering` | Renderers: EscPosRenderer, TextRenderer |
| Extensions | `MotorDsl.Extensions` | Integración con DI y fluent API para el Motor DSL de impresión térmica |
| Printing.Abstractions | `MotorDsl.Printing.Abstractions` | Contratos de impresión térmica e inyección de dependencias |
| Network | `MotorDsl.Network` | Transport de impresión por red TCP (puerto 9100 / RAW / JetDirect) para MotorDsl. Multiplataforma: Android, iOS y escritorio |
| Bluetooth | `MotorDsl.Bluetooth` | Transport Bluetooth Classic SPP (Android). iOS no soportado (lanza `PlatformNotSupportedException`) |
| Maui | `MotorDsl.Maui` | Controles MAUI y renderers (PDF, ESC/POS bitmap, SkiaSharp) para MotorDsl |
| Pax | `MotorDsl.Pax` | Transport de impresion para la impresora interna del PAX A910S (Android) para MotorDsl: contrato IPaxPrinterDevice, transport que envia el PNG de raster-preview, mapeo de errores e impresora simulada. La implementacion real del dispositivo es externa (RealDeviceFactory). |

---

## 6. Publicación paso a paso

### 6.1 Publicación local con el script canónico

El flujo completo de publicación está automatizado en `scripts/nuget/publish-motordsl-nuget.bat`, que empaqueta y publica los **9 paquetes** (necesita las workloads `ios` y `android`). Sus pasos: resuelve la API key (`MOTORDSL_NUGET_API_KEY` o prompt), calcula la versión unificada (auto-bump de patch), hace restore + build Release, corre `dotnet test`, empaqueta con `dotnet pack` y hace `dotnet nuget push` a nuget.org con `--skip-duplicate`, y crea/pushea el tag git `v<version>`.

Para una prueba manual de empaquetado de los 9 proyectos:

```bash
# Desde la raíz del repositorio
dotnet pack src/MotorDsl.Core/MotorDsl.Core.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Parser/MotorDsl.Parser.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Rendering/MotorDsl.Rendering.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Extensions/MotorDsl.Extensions.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Printing.Abstractions/MotorDsl.Printing.Abstractions.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Bluetooth/MotorDsl.Bluetooth.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Network/MotorDsl.Network.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Maui/MotorDsl.Maui.csproj -c Release -o ./nupkg
dotnet pack src/MotorDsl.Pax/MotorDsl.Pax.csproj -c Release -o ./nupkg
```

Verificar el contenido:

```bash
ls ./nupkg/
# MotorDsl.Core.<ver>.nupkg
# MotorDsl.Parser.<ver>.nupkg
# MotorDsl.Rendering.<ver>.nupkg
# MotorDsl.Extensions.<ver>.nupkg
# MotorDsl.Printing.Abstractions.<ver>.nupkg
# MotorDsl.Bluetooth.<ver>.nupkg
# MotorDsl.Network.<ver>.nupkg
# MotorDsl.Maui.<ver>.nupkg
# MotorDsl.Pax.<ver>.nupkg
```

### 6.2 Publicación en NuGet.org (automática vía CI/CD)

El workflow `.github/workflows/cd-nuget.yml` se encarga automáticamente. Los pasos son:

#### Paso 1 — Crear un tag de versión

```bash
git tag v1.0.0
git push origin v1.0.0
```

#### Paso 2 — El workflow se ejecuta automáticamente

1. Ejecuta la suite de tests (`dotnet test`)
2. Empaqueta los proyectos con la versión del tag (ej: `1.0.0`)
3. Pide una API key temporal por Trusted Publishing (`NuGet/login`, §4.3) y publica en **NuGet.org** (`https://api.nuget.org/v3/index.json`) con `--skip-duplicate`
4. Sube los `.nupkg` como artifacts del workflow

> Los steps **NuGet login** y **Publish to NuGet.org** sólo corren cuando el ref es un tag `v*` o el run es `workflow_dispatch`.
>
> nuget.org **no permite republicar** una versión ya publicada: un tag que publicó a medias no se reintenta con el mismo número sin diagnosticar; `--skip-duplicate` saltea los paquetes que ya subieron.

#### Paso 3 — Verificar

- En [nuget.org](https://www.nuget.org) → buscar `MotorDsl.*`: deben aparecer los paquetes publicados
- En el workflow run → Artifacts: descargar los `.nupkg`

### 6.3 Publicación manual vía workflow_dispatch

1. Ir a GitHub → Actions → **CD NuGet - Publish MotorDsl**
2. Click en **Run workflow**
3. El workflow ejecuta los mismos pasos de pack + push a nuget.org que con un tag

### 6.4 Versiones preview (push a `main`)

En un push a `main`, el workflow calcula la versión `0.0.0-preview.<run_number>`, verifica que estén los 9 `.nupkg` y los sube como artifacts, pero **no los publica** al feed (el step de publish exige tag `v*` o `workflow_dispatch`). Útil para pruebas internas.

### 6.5 Publicación manual desde la máquina local

Como alternativa al workflow, se puede publicar manualmente (o usar el script `scripts/nuget/publish-motordsl-nuget.bat`). Es la única vía que usa una API key de larga vida, y esa key queda en la máquina de quien publica, nunca en el repositorio:

#### Paso 1 — Obtener API Key

1. Ir a [nuget.org/account/apikeys](https://www.nuget.org/account/apikeys)
2. Crear una API Key con scope **Push** para los paquetes `MotorDsl.*`
3. Copiar la key (solo se muestra una vez)

#### Paso 2 — Publicar

```bash
dotnet nuget push ./nupkg/*.nupkg \
  --api-key <TU_API_KEY> \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

---

## 7. Consumo desde un proyecto .NET

### 7.1 Configurar el feed

Los paquetes se publican en **nuget.org**, que es el feed por defecto de NuGet: **no se necesita `nuget.config` ni autenticación** para consumirlos.

El `NuGet.config` del repositorio define exactamente dos fuentes (tras `<clear />`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local-nupkg" value="./nupkg" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
```

> La fuente `local-nupkg` (`./nupkg`) sirve para probar paquetes empaquetados localmente antes de publicarlos.

### 7.2 Instalar el paquete

```bash
dotnet add package MotorDsl.Extensions --version 1.0.0
```

Esto agrega al `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="MotorDsl.Extensions" Version="1.0.0" />
</ItemGroup>
```

### 7.3 Uso en código

#### Aplicación .NET con DI (API, Worker, MAUI, etc.)

```csharp
// Program.cs o MauiProgram.cs
using MotorDsl.Extensions;

builder.Services.AddMotorDslEngine();
```

#### Uso directo (aplicación consola sin DI)

El renderer NO consume el `DocumentTemplate` ni los datos directamente: opera sobre un `LayoutedDocument` (producto del `LayoutEngine`) y un `DeviceProfile`, y devuelve un `RenderResult` cuyo `byte[]` está en `result.Output`. Por eso el armado manual del pipeline es: parsear → evaluar → aplicar layout → renderizar.

```csharp
using MotorDsl.Parser;
using MotorDsl.Rendering;
using MotorDsl.Core.Evaluators;
using MotorDsl.Core.Layout;
using MotorDsl.Core.Models;

var profile = new DeviceProfile("thermal_58mm", 32, "escpos");

// Pipeline manual: Parse → Evaluate → Layout → Render
var parser = new DslParser();
var template = parser.Parse(jsonDsl);

var evaluator = new Evaluator();
var evaluated = evaluator.EvaluateTemplate(template, data);

var layoutEngine = new LayoutEngine();
LayoutedDocument layouted = layoutEngine.ApplyLayout(evaluated, profile);

var renderer = new EscPosRenderer();
RenderResult result = renderer.Render(layouted, profile);
byte[] output = (byte[])result.Output!;   // byte[] ESC/POS
```

> En la práctica conviene usar `IDocumentEngine.Render(jsonDsl, data, profile)` (ver 7.4), que orquesta todo el pipeline internamente.

### 7.4 Ejemplo mínimo funcional (consola)

```csharp
using MotorDsl.Extensions;
using MotorDsl.Core.Contracts;
using MotorDsl.Core.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddMotorDslEngine();
var provider = services.BuildServiceProvider();

// Resolver el motor y renderizar (DSL crudo + datos + DeviceProfile)
var engine = provider.GetRequiredService<IDocumentEngine>();
var profile = new DeviceProfile("thermal_58mm", 32, "escpos");
var result = engine.Render(jsonTemplate, data, profile);
byte[] output = (byte[])result.Output!;   // ESC/POS en RenderResult.Output
```

---

## 8. Versionado

El proyecto sigue la estrategia de versionado semántico (SemVer) definida en [estrategia-versionado_v1.0.md](estrategia-versionado_v1.0.md).

### Formato

```
MAJOR.MINOR.PATCH
```

| Cambio | Ejemplo | Cuándo |
|---|---|---|
| MAJOR | `2.0.0` | Cambios incompatibles en la API |
| MINOR | `1.1.0` | Nuevas funcionalidades, retrocompatibles |
| PATCH | `1.0.1` | Correcciones de bugs |
| Preview | `0.0.0-preview.42` | Builds automáticos en `main` (no publicados al feed) |

### ¿Cómo se determina la versión en el workflow?

| Trigger | Versión resultante | ¿Se publica al feed? |
|---|---|---|
| Tag `v1.2.3` | `1.2.3` | Sí |
| `workflow_dispatch` con input `1.2.3` | `1.2.3` | Sí |
| Push a `main` | `0.0.0-preview.<run_number>` | No (solo artifact) |

---

## 9. Troubleshooting

### 9.1 Error de autenticación al publicar en NuGet.org

```
error: Response status code does not indicate success: 401 (Unauthorized)
```

**Desde el workflow** (falla el paso `NuGet login` o el push con 401/403): la política de Trusted Publishing no coincide con el token. Verificar en nuget.org que la política esté activa y que repositorio, dueño y `Workflow File` (`cd-nuget.yml`) coincidan; que el job no tenga un `environment` que la política no declara; que el job `publish` conserve `id-token: write`; y que el `user` de `NuGet/login` sea el nombre de perfil del dueño de la política. La key temporal vence a la hora: si el push corre mucho después del login, falla.

**Desde la máquina local** (§6.5): verificar que la API key tenga scope **Push** para los paquetes `MotorDsl.*` y no esté expirada. Regenerar en nuget.org si corresponde.

### 9.2 Paquete no aparece en el feed

- Los paquetes pueden tardar unos minutos en indexarse y validarse en nuget.org
- Verificar en [nuget.org](https://www.nuget.org) que el paquete fue publicado
- Si usó `--skip-duplicate` y la versión ya existía, no se sobrescribe (diseño intencional)

### 9.3 Versión duplicada

```
error: Conflict - The feed already contains 'MotorDsl.Core 1.0.0'
```

**Solución:** incrementar la versión. Las versiones publicadas son inmutables por diseño. Crear un nuevo tag (ej: `v1.0.1`).

### 9.4 El paquete no trae las dependencias transitivas

Verificar que los `.csproj` usen `<ProjectReference>` (no `<Reference>`). El SDK de .NET convierte automáticamente las `ProjectReference` en dependencias del paquete NuGet al hacer `dotnet pack`.

### 9.5 Error `dotnet pack` falla con `--no-build`

El paso `--no-build` requiere que se haya hecho `dotnet build -c Release` previamente. Si falla, ejecutar sin `--no-build`:

```bash
dotnet pack src/MotorDsl.Core/MotorDsl.Core.csproj -c Release -o ./nupkg
```

---

## 10. Resumen rápido

```
Publicar:
  git tag v1.0.0 && git push origin v1.0.0
  → El workflow cd-nuget.yml hace el resto

Consumir:
  dotnet add package MotorDsl.Extensions --version 1.0.0
  → En código: services.AddMotorDslEngine();
```

---

## 11. Referencias

- [pipeline-ci-cd_v1.0.md](pipeline-ci-cd_v1.0.md) — Pipeline CI/CD completo
- [estrategia-versionado_v1.0.md](estrategia-versionado_v1.0.md) — Estrategia de versionado SemVer
- [entornos-deploy_v1.0.md](entornos-deploy_v1.0.md) — Entornos de despliegue
- [Documentación oficial NuGet](https://learn.microsoft.com/en-us/nuget/) — Microsoft Docs
- [Publicar paquetes en NuGet.org](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package) — Microsoft Docs

---

## 12. Control de cambios

| Versión | Fecha | Autor | Descripción |
|---|---|---|---|
| v1.0 | 2026-04-02 | Arquitectura / DevOps | Guía inicial de publicación y consumo NuGet |
| v1.0 | 2026-11-18 | Agente (Feature 04, PAX A910S) | Noveno paquete `MotorDsl.Pax`: conteos de 8 a 9, fila en §3.2 y §5.2, nueva §3.4 (el paquete público sale sin binding, `-p:PaxNeptune=false`), `Network` y `Pax` en la prueba manual de §6.1 |
| v1.0 | 2026-11-18 | Agente (Feature 04, contrato) | §3.4 reescrita como «contrato y punto de extensión»: `MotorDsl.Pax` no trae binarios del SDK y la implementación real es externa (`RealDeviceFactory`); se retira `PaxNeptune` de §6.1 y de la descripción de §5.2 |
| v1.0 | 2026-11-18 | Agente (Feature 04, Trusted Publishing) | `cd-nuget.yml` publica por Trusted Publishing (`NuGet/login@v1`, `id-token: write`): §4.2, §4.3 nueva, §6.2, §6.5 y §9.1; ya no hay secreto de API key en el repositorio |
