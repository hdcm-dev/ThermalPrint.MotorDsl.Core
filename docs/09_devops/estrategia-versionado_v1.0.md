# Estrategia de Versionado  
**Archivo:** estrategia-versionado_v1.0.md  
**Proyecto:** Motor DSL (Librería de Generación de Documentos)  
**Versión:** v1.0  
**Estado:** Aprobado  
**Fecha:** 2026-03-28  
**Owner:** Arquitectura / DevOps  

---

## 1. Propósito

Este documento define la estrategia de versionado de los paquetes NuGet del Motor DSL: el esquema de versiones adoptado, cómo se calcula e inyecta la versión en build/pack, la unificación de versión entre los 9 paquetes y las reglas de etiquetado en git. El objetivo es garantizar trazabilidad, inmutabilidad y consistencia entre los componentes publicados.

---

## 2. Esquema adoptado: SemVer

Se utiliza **Semantic Versioning (SemVer)** con el formato:

```text
MAJOR.MINOR.PATCH
```

| Componente | Ejemplo | Cuándo se incrementa |
|---|---|---|
| MAJOR | `2.0.0` | Cambios incompatibles en la API pública (breaking changes) |
| MINOR | `1.1.0` | Nuevas funcionalidades retrocompatibles |
| PATCH | `1.0.1` | Correcciones de bugs / cambios internos retrocompatibles |
| Preview | `0.0.0-preview.42` | Builds automáticos en `main` (no publicados al feed) |

---

## 3. La versión NO se fija en los `.csproj`

Ninguno de los 9 proyectos empaquetables declara `<Version>` en su `.csproj`. La versión se **inyecta en tiempo de build/pack** mediante propiedades MSBuild:

- `/p:Version=<version>`
- `-p:PackageVersion=<version>`
- `-p:MotorDslVersion=<version>`

Esto permite que el mismo código fuente se publique con cualquier versión calculada por el pipeline, sin tocar archivos fuente.

---

## 4. Auto-bump de patch (`get-next-version.ps1`)

El script `scripts/nuget/get-next-version.ps1` calcula automáticamente la próxima versión de PATCH de cada paquete:

```powershell
param($PackageName, $Fallback = "1.0.0")
```

- Consulta el índice de versiones del paquete en nuget.org
  (`https://api.nuget.org/v3-flatcontainer/<id>/index.json`).
- Toma la última versión publicada e incrementa el PATCH (`X.Y.Z` → `X.Y.(Z+1)`).
- Si el paquete **no existe todavía** en nuget.org, usa el fallback `1.0.0`.

---

## 5. Versión unificada entre los 9 paquetes

Los 9 paquetes (`MotorDsl.Core`, `MotorDsl.Parser`, `MotorDsl.Rendering`, `MotorDsl.Extensions`, `MotorDsl.Printing.Abstractions`, `MotorDsl.Network`, `MotorDsl.Bluetooth`, `MotorDsl.Maui`, `MotorDsl.Pax`) tienen dependencias internas entre sí. Para evitar el error de downgrade **`NU1605`** por dependencias transitivas con versiones distintas, el pipeline de publicación:

1. Calcula la próxima versión de cada paquete con `get-next-version.ps1`.
2. Toma la **versión unificada = `max(next(patch))`** de los 9 paquetes.
3. Inyecta ese mismo número en TODOS los paquetes vía `/p:Version` y `/p:MotorDslVersion`.

De este modo, una publicación siempre libera los 9 paquetes con la misma versión. Un paquete que todavía no existe en nuget.org (hoy `MotorDsl.Pax`) cae al fallback `1.0.0`, que no altera el máximo: sale directamente con la versión unificada vigente.

---

## 6. Versionado en el pipeline

| Trigger | Versión resultante | ¿Se publica al feed? |
|---|---|---|
| Tag `v1.2.3` | `1.2.3` (o la versión unificada calculada) | Sí — push a nuget.org |
| `workflow_dispatch` | versión unificada calculada | Sí — push a nuget.org |
| Push a `main` | `0.0.0-preview.<run_number>` | No — solo artifact descargable |

El step de publicación a nuget.org del workflow `.github/workflows/cd-nuget.yml` solo se ejecuta en tags `v*` o `workflow_dispatch`, con una API key temporal obtenida por Trusted Publishing de nuget.org (`NuGet/login`, sin secreto guardada en el repositorio; ver [guia-publicacion-nuget_v1.0.md](guia-publicacion-nuget_v1.0.md) §4.3). Una versión publicada en nuget.org no se puede republicar: un tag no se reescribe ni se reutiliza.

---

## 7. Etiquetado en git

- Cada release publicado lleva un tag git `v<version>` (ej. `v1.0.0`).
- El script `scripts/nuget/publish-motordsl-nuget.bat` crea el tag `v<version>` y lo pushea a `origin` tras el `dotnet nuget push` exitoso.
- Los tags son la fuente de trazabilidad entre el commit publicado y la versión en nuget.org.

### Reglas

- Cada deploy a QA debe estar asociado a una versión.
- Cada release a PROD debe estar versionado y taggeado (`v<version>`).
- No se permiten versiones no trazables en producción.

---

## 8. Inmutabilidad de versiones

- Las versiones publicadas en nuget.org son **inmutables**: una versión ya publicada no se sobrescribe.
- El push usa `--skip-duplicate`, de modo que reintentos no fallan si la versión ya existe.
- Ante un error en una versión, se publica una nueva versión corregida (incremento de PATCH), nunca se reescribe la anterior.

```text
1.1.0 (fallo)
→ 1.1.1 (corregido)
```

---

## 9. Orden de publicación por dependencias

Por las dependencias internas, el push de los paquetes respeta el orden topológico:

```text
Printing.Abstractions → Core → Parser → Rendering → Extensions → Bluetooth → Maui
```

(Mismo orden usado en `publish-motordsl-nuget.bat`.)

---

## 10. Relación con otros documentos

- [guia-publicacion-nuget_v1.0.md](guia-publicacion-nuget_v1.0.md) — Publicación y consumo de paquetes NuGet
- [pipeline-ci-cd_v1.0.md](pipeline-ci-cd_v1.0.md) — Pipeline CI/CD completo
- [entornos-deploy_v1.0.md](entornos-deploy_v1.0.md) — Entornos de despliegue

---

## 11. Control de cambios

| Versión | Fecha | Autor | Descripción |
|---|---|---|---|
| v1.0 | 2026-03-28 | DevOps | Estrategia inicial de versionado SemVer con versión unificada y auto-bump |
| v1.0 | 2026-11-18 | Agente (Feature 04) | Noveno paquete `MotorDsl.Pax` en la versión unificada |
| v1.0 | 2026-11-18 | Agente (Feature 04) | §6: la publicación usa Trusted Publishing en lugar del secreto de API key |

---
