# Especificación de feature: [FEATURE NAME]

**Rama**: `[###-feature-name]`

**Creada**: [DATE]

**Estado**: Borrador

**Entrada**: descripción del usuario: "$ARGUMENTS"

## Alcance y prioridad *(obligatorio)*

**Prioridad**: [P0 o P1]

**Resultado evaluable**: [valor observable entregado]

**Fuera de alcance**: [capacidades excluidas, incluidos patrones prohibidos aplicables]

> P0 contiene los tres recorridos obligatorios. P1 contiene reportes y solo puede comenzar
> cuando P0 esté completo y validado.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia de usuario 1 - [Título breve] (Prioridad: [P0/P1])

[Recorrido descrito en lenguaje de negocio]

**Motivo de prioridad**: [valor y relación con P0 o P1]

**Prueba independiente**: [acción verificable y resultado observable]

**Escenarios de aceptación**:

1. **Given** [estado inicial], **When** [acción], **Then** [resultado esperado]
2. **Given** [condición límite], **When** [acción], **Then** [error o resultado esperado]

### Casos límite

- [Límite de negocio verificable]
- [Fallo transaccional, de integridad o de contrato pertinente]

## Requisitos *(obligatorio)*

### Requisitos funcionales

- **REQ-001**: El sistema DEBE [capacidad concreta y verificable].
- **REQ-002**: El sistema DEBE [regla de negocio o resultado observable].

### Datos e historia *(obligatorio cuando corresponda)*

- **Entidades**: [conceptos y relaciones sin decidir implementación].
- **Historia**: [qué cambios agregan registros y qué información nunca se sobrescribe].
- **Integridad**: [cardinalidad, unicidad y eliminación restrictiva esperadas].
- **Fechas**: [fechas de negocio sin hora y sellos técnicos opcionales en UTC].

### Validación y errores *(obligatorio)*

| Regla | Frontera responsable | Resultado HTTP observable |
| --- | --- | --- |
| [regla] | [API/aplicación/dominio/base de datos] | [`ProblemDetails` esperado] |

### Contrato entre repositorios *(obligatorio si existe impacto UI)*

- **Backend canónico**: [operación o schema OpenAPI afectado].
- **Nombres/versiones compartidos**: [decisión coordinada].
- **Impacto en `inovait-frontend`**: [consumo, accesibilidad y diseño adaptable].

### Entidades clave *(incluir si la feature maneja datos)*

- **[Entity]**: [significado, atributos relevantes y relaciones].

## Trazabilidad *(obligatorio)*

| Requirement ID | Story/Scenario ID | Evidencia de aceptación | Prioridad |
| --- | --- | --- | --- |
| [REQ-001] | [US1/SCN-001] | [resultado verificable] | [P0/P1] |

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **OUT-001**: [resultado evaluable y tecnológicamente agnóstico].
- **OUT-002**: [resultado de error, integridad o reproducibilidad].

## Supuestos

- [Supuesto explícito que no amplía el alcance].
- [Catálogo o dato inicial ficticio requerido].

## Seguridad y privacidad

- No se usarán secretos ni datos personales reales.
- [Consideración adicional o N/A con justificación].
