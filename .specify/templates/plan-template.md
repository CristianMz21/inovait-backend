# Plan de implementación: [FEATURE]

**Rama**: `[###-feature-name]` | **Fecha**: [DATE] | **Especificación**: [link]

**Entrada**: especificación en `/specs/[###-feature-name]/spec.md`

**Nota**: `/speckit-plan` completa esta plantilla. No autoriza implementación.

## Resumen

[Requisito principal, prioridad P0 o P1 y enfoque técnico resumido]

## Contexto técnico

**Lenguaje/versión**: [versión concreta o NEEDS CLARIFICATION]

**Dependencias principales**: [dependencias justificadas o NEEDS CLARIFICATION]

**Persistencia**: [SQL Server u otra decisión justificada]

**Pruebas**: [runner, capas disponibles y estrategia por riesgo]

**Plataforma objetivo**: [entorno del evaluador o NEEDS CLARIFICATION]

**Tipo de proyecto**: [web-service u otro tipo]

**Objetivos de rendimiento**: [objetivo medible o N/A con justificación]

**Restricciones**: [plazo, alcance, seguridad y reproducibilidad]

**Escala/alcance**: [volumen esperado y límite del MVP]

## Control constitucional

*PUERTA: DEBE aprobarse antes de investigación y repetirse después del diseño.*

| Control | Evidencia requerida | Estado |
| --- | --- | --- |
| P0 antes de P1 | Los tres recorridos P0 están delimitados; los reportes P1 no los bloquean | [PASS/FAIL] |
| Simplicidad | No incluye capacidades ni patrones prohibidos; toda complejidad tiene justificación | [PASS/FAIL] |
| Integridad histórica | El modelo conserva `Enrollment` y `TeacherContract`; los borrados son restrictivos | [PASS/FAIL] |
| Fechas | Fechas de negocio sin hora; sellos técnicos opcionales en UTC | [PASS/FAIL] |
| Validación y errores | Reglas asignadas a API, aplicación, dominio o base de datos; usa `ProblemDetails` | [PASS/FAIL] |
| Trazabilidad | Requisito → escenario → modelo/operación → tarea → prueba | [PASS/FAIL] |
| Contrato entre repositorios | OpenAPI del backend canónico; impacto en `inovait-frontend` documentado | [PASS/FAIL] |
| Pruebas por riesgo | Reglas de negocio críticas cubiertas sin metas artificiales de cobertura | [PASS/FAIL] |
| Accesibilidad | El impacto UI contempla accesibilidad y diseño adaptable cuando corresponde | [PASS/FAIL/N/A] |
| Entrega evaluable | Setup, script mínimo, modelo ER y datos ficticios tienen ruta de validación | [PASS/FAIL] |
| Seguridad de datos | No hay secretos ni datos personales reales en artefactos o ejemplos | [PASS/FAIL] |

Un `FAIL` bloquea el avance. Solo una excepción constitucional documentada puede continuar.

## Estructura del proyecto

### Documentación de esta feature

```text
specs/[###-feature]/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Código fuente

```text
[Árbol concreto del repositorio seleccionado durante planificación; no crear directorios aquí]
```

**Decisión de estructura**: [Estructura mínima elegida y motivo]

## Trazabilidad de diseño

| Requirement ID | Scenario ID | Modelo/Restricción | Operación OpenAPI | Prueba prevista |
| --- | --- | --- | --- | --- |
| [REQ-001] | [SCN-001] | [elemento] | [operationId] | [capa y objetivo] |

## Impacto entre repositorios

| Decisión compartida | Backend canónico | Acción en `inovait-frontend` | Compatibilidad/versión |
| --- | --- | --- | --- |
| [nombre o formato] | [schema/operationId] | [adaptación requerida] | [decisión] |

## Seguimiento de complejidad

> Completar SOLO si existe una excepción al control constitucional.

| Excepción | Por qué es necesaria | Alternativa simple rechazada | Riesgo para el MVP |
| --- | --- | --- | --- |
| [excepción] | [necesidad verificable] | [motivo] | [impacto y mitigación] |
