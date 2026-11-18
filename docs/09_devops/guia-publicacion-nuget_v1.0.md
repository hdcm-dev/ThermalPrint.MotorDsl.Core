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
| `MotorDsl.Pax` | Transport `pax` para la impresora interna del PAX A910S (envía el PNG de raster-preview), mapeo de errores, impresora simulada y perfil A910S. **El paquete público sale sin el binding NeptuneLite** (§3.4) | `Microsoft.Extensions.DependencyInjection.Abstractions` | → Printing.Abstractions |

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

### 3.4 `MotorDsl.Pax`: el paquete público sale sin binding

`MotorDsl.Pax` tiene dos modos de compilación, que decide la propiedad MSBuild `PaxNeptune`:

| `PaxNeptune` | Cuándo | Qué trae el paquete | Modo Real en el A910S |
|---|---|---|---|
| `true` | Compilación local con el SDK PAX preparado en `src/MotorDsl.Pax/Neptune/sdk/` (`scripts/pax-sdk/preparar-sdk.sh`); se detecta sola | Transport, simulador, perfil A910S **y** el binding NeptuneLite 4.15.00 (jar y `.so`) | Imprime por NeptuneLite |
| `false` | **Todo paquete público**: `cd-nuget.yml` y `scripts/nuget/publish-motordsl-nuget.bat` lo fuerzan con `-p:PaxNeptune=false` | Transport, simulador y perfil A910S, **sin** jar ni `.so` | Responde `Connection` («Avisá a soporte») |

**Por qué:** el jar y las `.so` del SDK PAX no se versionan y no hay licencia de redistribución del material PAX. Forzar `false` en los dos caminos de publicación hace que el contenido del paquete no dependa de si la máquina que empaqueta tiene o no el SDK.

**Consecuencia para el consumidor:** una app que necesite imprimir de verdad en el A910S no puede usar hoy el paquete de nuget.org: compila contra un feed propio (local o privado) con el paquete armado con el binding. El canal del paquete con binding se decide cuando el A910S esté validado en el equipo; hasta entonces no se publica ese paquete.

---

## 4. Prerrequisitos

### 4.1 Herramientas

- .NET SDK 10.0 o superior
- Git (para tags de versionado)

### 4.2 Cuentas y tokens

El destino productivo real es **NuGet.org**.

| Destino | Cuenta requerida | Token / API Key |
|---|---|---|
| **NuGet.org** (destino real) | Cuenta en [nuget.org](https://www.nuget.org) | API Key con scope **Push** para `MotorDsl.*`, expuesta al pipeline como secret `NUGET_API_KEY` |

> Para **consumir** desde nuget.org no se requiere autenticación.

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
| Pax | `MotorDsl.Pax` | Transport de impresion para la impresora interna del PAX A910S (Android, SDK NeptuneLite) para MotorDsl. Envia el PNG de raster-preview; incluye impresora simulada. |

---

## 6. Publicación paso a paso

### 6.1 Publicación local con el script canónico

El flujo completo de publicación está automatizado en `scripts/nuget/publish-motordsl-nuget.bat`, que empaqueta y publica los **9 paquetes** (con `-p:PaxNeptune=false`, §3.4; necesita las workloads `ios` y `android`). Sus pasos: resuelve la API key (`MOTORDSL_NUGET_API_KEY` o prompt), calcula la versión unificada (auto-bump de patch), hace restore + build Release, corre `dotnet test`, empaqueta con `dotnet pack` y hace `dotnet nuget push` a nuget.org con `--skip-duplicate`, y crea/pushea el tag git `v<version>`.

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
dotnet pack src/MotorDsl.Pax/MotorDsl.Pax.csproj -c Release -p:PaxNeptune=false -o ./nupkg
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
3. Publica en **NuGet.org** (`https://api.nuget.org/v3/index.json`) usando `secrets.NUGET_API_KEY`
4. Sube los `.nupkg` como artifacts del workflow

> El step **Publish to NuGet.org** solo corre cuando el ref es un tag `v*` o el run es `workflow_dispatch`.

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

Como alternativa al workflow, se puede publicar manualmente (o usar el script `scripts/nuget/publish-motordsl-nuget.bat`):

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

**Solución:** verificar que la API Key (`NUGET_API_KEY`) tenga scope **Push** para los paquetes `MotorDsl.*` y no esté expirada. Regenerar en nuget.org si corresponde.

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
