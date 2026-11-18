  # Pipeline CI/CD — Motor DSL  
**Archivo:** pipeline-ci-cd_v1.0.md  
**Proyecto:** Motor DSL para generación y renderizado de documentos  
**Versión:** v1.0  
**Estado:** Aprobado  
**Fecha:** 2026-03-29  
**Owner:** DevOps  

---

## 1. Propósito

Este documento define el pipeline de Integración Continua y Entrega Continua (CI/CD) del Motor DSL. Su objetivo es automatizar la construcción, validación, empaquetado y publicación del motor como paquete **NuGet**, asegurando calidad, trazabilidad y consistencia en cada versión liberada.

---

## 2. Alcance

El pipeline cubre:

- Build de librerías .NET  
- Ejecución de tests  
- Análisis de calidad  
- Empaquetado como NuGet (.nupkg)  
- Publicación en registry (NuGet local o remoto)  
- Build y publicación de apps Android (APK)  
- Build de app iOS (simulador .app.zip)  
- Scripts de desarrollo local (.bat)  

Fuera de alcance en v1.0:

- Firma de paquetes  
- Escaneo de seguridad avanzado  
- Publicación multi-registry  

---

## 3. Estrategia general

El pipeline se ejecuta ante:

- Pull Requests  
- Push a ramas principales  
- Tags de release  

Flujo de alto nivel:

```text
Commit → Build → Tests → Quality → Pack → Publish (NuGet)
````

---

## 4. Disparadores (triggers)

### 4.1 Pull Request

Se ejecuta:

* Build
* Tests
* Análisis estático

**Objetivo:** prevenir errores antes del merge.

---

### 4.2 Push a develop/main

Se ejecuta:

* Build completo
* Tests
* Empaquetado (sin publicar)

**Objetivo:** validar continuamente el estado del motor.

---

### 4.3 Tag de versión

Se ejecuta:

* Build release
* Tests completos
* Generación de paquete NuGet versionado
* Publicación en registry

**Ejemplo de tag**

```text
v1.2.0
```

---

## 5. Etapas del pipeline

---

### 5.1 Stage: Checkout

**Objetivo:** obtener el código fuente.

**Pasos**

* Clonar repositorio
* Restaurar dependencias
* Validar versión desde tag o build

---

### 5.2 Stage: Build

**Objetivo:** compilar las librerías del motor.

El conjunto real de librerías publicables son **9 paquetes**:

* MotorDsl.Core
* MotorDsl.Parser
* MotorDsl.Rendering
* MotorDsl.Extensions
* MotorDsl.Printing.Abstractions
* MotorDsl.Network
* MotorDsl.Bluetooth
* MotorDsl.Maui
* MotorDsl.Pax (`net10.0-android`; contrato `IPaxPrinterDevice` y simulador, la implementación real es externa: ver [guia-publicacion-nuget_v1.0.md](guia-publicacion-nuget_v1.0.md) §3.4)

> El workflow `cd-nuget.yml` publica los **9** paquetes repartiendo el `pack` en dos runners: los 6 `net10.0` en `ubuntu-latest` y `MotorDsl.Bluetooth` / `MotorDsl.Maui` / `MotorDsl.Pax` en `macos-15`. La separación es estructural: `dotnet pack` de un proyecto multi-TFM exige compilar todos sus TFM y el workload `ios` no existe para Linux. Antes publicaba solo las 4 librerías core y el resto dependía del script local `scripts/nuget/publish-motordsl-nuget.bat`.

**Criterio de éxito**

* Build sin errores

---

### 5.3 Stage: Tests

**Objetivo:** validar calidad funcional.

**Incluye**

* Tests unitarios
* Tests de integración del pipeline DSL

**Reglas**

* Cobertura mínima: 70%
* Fallo en tests → pipeline falla

---

### 5.4 Stage: Análisis de calidad

**Objetivo:** detectar problemas de código.

**Incluye**

* Análisis estático
* Code smells
* Convenciones de naming
* Validación de arquitectura (si aplica)

**Regla**

* Issues críticos → pipeline falla

---

### 5.5 Stage: Empaquetado (Pack)

**Objetivo:** generar el paquete NuGet.

**Salida**

* Un `.nupkg` por paquete (no existe un paquete monolítico `MotorDsl`)

**Comando base**

```bash
dotnet pack -c Release -p:PackageVersion=<ver> -p:MotorDslVersion=<ver> -o ./nupkg
```

**Ejemplo de nombres** (un archivo por PackageId real)

```text
MotorDsl.Core.<ver>.nupkg
MotorDsl.Parser.<ver>.nupkg
MotorDsl.Rendering.<ver>.nupkg
MotorDsl.Extensions.<ver>.nupkg
```

---

### 5.6 Stage: Publicación (Publish NuGet)

**Objetivo:** distribuir el paquete.

**Condición**

* Solo en tags de versión

**Destino**

* **NuGet.org** (https://api.nuget.org/v3/index.json) — producción

**Condición adicional:** solo en tags `v*` (ej. `v1.0.2`)

**Credencial:** Trusted Publishing de nuget.org. El job `publish` tiene `permissions: id-token: write`; el paso `NuGet/login@v1` canjea el token OIDC de GitHub por una API key temporal (1 hora) y el push la toma de `steps.nuget-login.outputs.NUGET_API_KEY`. No hay secreto de API key en el repositorio (detalle y campos de la política en [guia-publicacion-nuget_v1.0.md](guia-publicacion-nuget_v1.0.md) §4.3)

**Metadatos requeridos en cada csproj:**
`<PackageId>`, `<Authors>`, `<Description>`, `<PackageTags>`, `<PackageLicenseExpression>`, `<RepositoryUrl>`

**Comando base** (un push por paquete; ejemplo con Core)

```bash
dotnet nuget push ./nupkg/MotorDsl.Core.<ver>.nupkg --api-key "$NUGET_API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate
```

---

### 5.7 Stage: CD Android

**Trigger:** push main o tag v*

- Genera APK de SampleApp y MultaApp
- Artifact: *-APK con retención 7 días

---

### 5.8 Stage: CD iOS (simulador)

**Trigger:** push main (en paths `samples/MotorDsl.SampleApp/**` o `samples/MotorDsl.MultaApp/**`)

- SampleApp y MultaApp en workflows separados
- Genera .app.zip para simulador iossimulator-arm64
- Artifact: *.app.zip con retención 1 día
- Runner requerido: `macos-15`, Xcode 26.0

---

## 6. Gates de calidad

El pipeline falla si ocurre cualquiera de los siguientes:

* Error de compilación
* Tests fallidos
* Cobertura < 70%
* Issues críticos de análisis
* Fallo en empaquetado

---

## 7. Versionado en el pipeline

Se utiliza **Semantic Versioning (SemVer)**.

La versión se determina por:

1. Tag git (principal)
2. Número de build (fallback)

**Ejemplos**

* v1.0.0 → versión oficial
* v1.1.0 → nueva funcionalidad
* v1.1.1 → bugfix

Fallback:

```text
1.1.0-build.45
```

---

## 8. Variables de entorno

Variables mínimas:

* NUGET_API_KEY (sólo dentro del paso de push, tomada de la salida de `NuGet/login`; no es un secreto del repositorio)
* NUGET_SOURCE
* BUILD_CONFIGURATION
* VERSION

**Reglas**

* Nunca hardcodear API keys
* En CI, credenciales de corta vida por OIDC (Trusted Publishing) antes que secretos de larga vida

---

## 9. Estrategia de rollback

En caso de error en publicación:

* No sobrescribir versiones existentes (inmutabilidad)
* Publicar nueva versión corregida (PATCH)
* Mantener historial de versiones

**Ejemplo**

```text
1.1.0 (fallo)
→ 1.1.1 (corregido)
```

---

## 10. Observabilidad del pipeline

Se debe registrar:

* Tiempo de build
* Tiempo de tests
* Cobertura de tests
* Versiones publicadas
* Frecuencia de releases

---

## 11. Ejemplo simplificado (pseudo YAML)

build:
test:
pack:
publish:
```text
.github/workflows/
├── ci.yml                       ← tests en cada PR y push (ubuntu-latest)
├── cd-android.yml               ← APK SampleApp + MultaApp (ubuntu-latest)
├── cd-ios-sampleapp.yml         ← .app.zip SampleApp, iossimulator-arm64 (macos-15)
├── cd-ios-multaapp.yml          ← .app.zip MultaApp, iossimulator-arm64 (macos-15)
├── cd-ios-multaapp.nuget.yml    ← .app.zip MultaApp (consumo NuGet), iossimulator-arm64 (macos-15)
└── cd-nuget.yml                 ← NuGet.org en tags v*, Trusted Publishing (ubuntu-latest)
```

---

## 12. Riesgos

| Riesgo                | Impacto | Mitigación                   |
| --------------------- | ------- | ---------------------------- |
| Versionado incorrecto | Alta    | uso de tags SemVer           |
| Publicación duplicada | Alta    | versiones inmutables         |
| Tests insuficientes   | Alta    | cobertura mínima obligatoria |
| API key expuesta      | Crítica | Trusted Publishing: key temporal de 1 hora, sin secreto guardado |

---

## 13. Evolución futura

Implementado en v1.0 (actualización):

- ✅ Publicación automática en NuGet.org (cd-nuget.yml con Trusted Publishing, sin API key guardada)
- ✅ Conjunto de 9 paquetes: MotorDsl.Core, Parser, Rendering, Extensions, Printing.Abstractions, Network, Bluetooth, Maui, Pax (publicados por `cd-nuget.yml` o por `publish-motordsl-nuget.bat`, con versión unificada)
- ✅ Soporte iOS: cd-ios-sampleapp.yml, cd-ios-multaapp.yml y cd-ios-multaapp.nuget.yml
- ✅ Samples de integración NuGet: MotorDsl.Nuget.MultaApp y MotorDsl.Nuget.Integrated.MultaApp

Planeado para v2.x:

- Firma de paquetes
- Validación de compatibilidad (breaking changes)
- Escaneo de seguridad (SCA)
- Generación automática de changelog
- Multi-target frameworks
- Firma de APK con keystore propio
- Publicación en Google Play (AAB)
- Scripts .bat para desarrollo local Windows

---

## 14. Control de cambios

| Versión | Fecha      | Autor  | Descripción                             |
| ------- | ---------- | ------ | --------------------------------------- |
| v1.0    | 2026-03-29 | DevOps | Pipeline inicial para publicación NuGet |
| v1.1    | 2026-04-02 | DevOps | Publicación real en NuGet.org (NUGET_API_KEY), cd-ios-multaapp.yml, samples MotorDsl.Nuget.MultaApp / MotorDsl.Nuget.Integrated.MultaApp |
| v1.2    | 2026-11-18 | Agente (Feature 04) | Noveno paquete `MotorDsl.Pax` en `pack-platform`, sin binding NeptuneLite |
| v1.3    | 2026-11-18 | Agente (Feature 04) | `MotorDsl.Pax` es contrato y simulador; `cd-nuget.yml` ya no pasa `-p:PaxNeptune=false` |
| v1.4    | 2026-11-18 | Agente (Feature 04) | `cd-nuget.yml` publica por Trusted Publishing (`NuGet/login@v1`, `id-token: write`); se retira el secreto de API key |

---
