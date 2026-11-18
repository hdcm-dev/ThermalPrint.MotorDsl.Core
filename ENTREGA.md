# Entrega de cambios a este repositorio

Cómo llega un cambio a `main` de la librería MotorDsl (`ThermalPrint.MotorDsl.Core`).

## 1. Delegación del merge

Los cambios entran por pull request con commit de merge (nunca `squash` ni `rebase`), y el agente que conduce la unidad lo fusiona él mismo siguiendo `Knowledge-Conformacion-Pull-Request-Automatico` (IA.Knowledge.Base) 2.0, con la guarda G1–G4.

| Campo | Valor | Origen |
|---|---|---|
| Estado | Merge delegado al agente | Asentado el 2026-11-17 en la Feature 04 (PAX A910S), PR #15 |
| Fecha | 2026-10-25 | Regla del PO para todos los PR del espacio de trabajo; reiterada el 2026-11-17 |
| Decidió | Fernando Filipuzzi, Product Owner | 2026-10-25: «siempre para conformar los pr sigue `IA/IA.Knowledge.Base/Knowledges/Knowledge-Conformacion-Pull-Request-Automatico.md`»; 2026-11-17: «conforma los prs usando este conocimiento … arregla los conflictos … si, segui» |
| Responsable | El agente que conduce la unidad: abre el PR, corre la guarda, fusiona y borra la rama | Conocimiento §2 (actor A) |
| Techo | 30 minutos | Mismo valor que la delegación del repositorio de documentación (`ENTREGA.md`) |
| Intervalo | 20 segundos | Ídem |
| Control G3 | Workflow `CI - Tests` (`.github/workflows/ci.yml`), corre en `pull_request` a `main` | Un PR con base distinta de `main` no tiene corrida: su G3 se apoya en build y tests locales registrados y en la CI del PR final a `main` |

**Excepción y revocación.** Revocable por unidad: un cuestionamiento particular del PO, o una reserva (decisión que no es del agente, criterio que no se cumple, efecto no medido), vuelve el merge manual.

**Publicación.** Un push a `main` corre `cd-nuget.yml` y empaqueta los 9 paquetes, pero **sólo publica en nuget.org con un tag `v*` o un `workflow_dispatch`**. Etiquetar y publicar no está delegado al agente.
