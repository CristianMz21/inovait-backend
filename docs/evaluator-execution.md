# Instrucciones del evaluador técnico

## Pre-requisitos de verificación

- .NET SDK: `10.0.109` (usando `global.json`), C# `14` y target `net10.0`.
- `dotnet` y `git` disponibles desde terminal.
- SQL Server 2022 o contenedor disponible para escenarios de integración cuando el slice lo requiere.
- Dependencias instaladas vía `dotnet restore` desde la raíz del repositorio.

## Mapa de slices (estrategia `stacked-to-main`)

- `S01`: bootstrap base (solución/proyectos, referencias, smoke tests)
- `S02`: normalización + auditoría + harness genérico
- `S03`: catálogos P0 + singleton/código estable
- `S04`: `Person` y roles duales
- `S05`: `ClassGroup` y `Enrollment`
- `S06`: `TeacherContract`
- `S07`: migración scaffold + migración de protecciones SQL
- `S08–S11`: catálogos API y US1–US3
- `S12`: `database/setup.sql`, paridad SQL y puerta P0

Cada slice mantiene el orden de dependencia definido en `tasks.md` y debe respetar el gate humano (`≤400` líneas humanas, salvo `size:exception` explícito).

La excepción `EX-PLAN-2026-07-10` fue aprobada por mantenimiento únicamente para el work unit separado que integra el churn documental de planificación ya existente en esta rama. No cubre S01, código, configuración, pruebas, el registro documental posterior de evidencia ni slices posteriores, y no cambia el límite del gate.

## Variables por slice

Para cada slice, antes de ejecutar el gate:

- `SLICE_BASE`: commit inmutable de `main` inmediatamente anterior al slice.
- `HUMAN_BASE`: último commit que contiene exclusivamente salida generada declarada; si el slice no tiene salida generada, es igual a `SLICE_BASE`.
- `HUMAN_HEAD`: commit inmutable que termina la personalización humana del slice, antes de cualquier work unit documental posterior.
- `GENERATED_MANIFEST`: archivo versionado con una ruta exacta por línea. Debe coincidir con todas las rutas modificadas entre `SLICE_BASE` y `HUMAN_BASE`; si una de esas plantillas se edita o elimina después, solo ese delta pertenece al rango humano.

Los tres valores deben ser SHAs completos, no nombres móviles de ramas. El gate se ejecuta después de crear los commits del slice y antes de merge; un worktree sin commits no tiene todavía bases reproducibles y no puede registrar un PASS real. El rango termina siempre en `HUMAN_HEAD`, nunca en `HEAD`, para que un work unit documental posterior no cambie evidencia S01 ya cerrada.

Para S01, [`docs/generated-manifests/s01.txt`](generated-manifests/s01.txt) contiene exactamente las 16 rutas producidas por `dotnet new` y `dotnet sln add`: solución, proyectos y archivos template, incluidos `Class1.cs` y `UnitTest1.cs`. Esas rutas ocupan un commit generado aislado en su estado original. Si una plantilla se modifica o elimina después, ese delta queda en el commit humano y se cuenta; una ruta manual como `.editorconfig`, `scripts/check-human-lines.py`, `SmokeTests.cs` o `HumanLineGateTests.cs` nunca pertenece al manifest.

Ejemplo de verificación canónica:

```bash
GENERATED_MANIFEST=docs/generated-manifests/s01.txt

git diff --name-only "$SLICE_BASE" "$HUMAN_BASE" -- | LC_ALL=C sort > /tmp/inovait-generated.actual
LC_ALL=C sort "$GENERATED_MANIFEST" > /tmp/inovait-generated.expected
diff -u /tmp/inovait-generated.expected /tmp/inovait-generated.actual

git diff --numstat "$HUMAN_BASE"..."$HUMAN_HEAD" -- | ./scripts/check-human-lines.py
```

`check-human-lines.py` consume directamente cada fila de `git diff --numstat`, suma additions+deletions, rechaza filas binarias/no clasificadas y devuelve estado `1` cuando el total supera 400. Prueba reproducible del fallo:

```bash
set +e
printf '401\t0\tsynthetic-over-budget.cs\n' | ./scripts/check-human-lines.py
status=$?
set -e
test "$status" -eq 1
```

La salida esperada incluye `401` y `human gate failed: 401 > 400`; el último `test` debe devolver `0`.

## Baselines registrados para el trabajo actual

| Unidad | `SLICE_BASE` | `HUMAN_BASE` | `HUMAN_HEAD` | Estado |
| --- | --- | --- | --- | --- |
| Planificación (`EX-PLAN-2026-07-10`) | `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` | N/A | `<PLANNING_DOC_SHA>` | Excepción aprobada solo para este work unit documental; no es un PASS del gate S01. |
| S01 | `<PLANNING_DOC_SHA>` | `<GENERATED_SCAFFOLD_SHA>` | `<S01_HUMAN_SHA>` | PENDIENTE: reemplazar placeholders únicamente después de crear los commits y ejecutar el gate real. |

### Secuencia exacta propuesta (todavía no ejecutada)

1. `docs: finalize production model plan`: archivos de planificación ya modificados bajo `.atl/`, `.specify/memory/`, `README.md`, `docs/` y `specs/`; incluye este documento y el manifest S01. Es el único work unit cubierto por `EX-PLAN-2026-07-10`. Verificar `git diff --check` y la igualdad OpenAPI antes de fijar `<PLANNING_DOC_SHA>`.
2. `chore: add generated .NET scaffold`: solo las 16 rutas de `docs/generated-manifests/s01.txt`, en el estado producido por `dotnet new`/`dotnet sln add`, sin referencias, paquetes ni personalización. Fijar `<GENERATED_SCAFFOLD_SHA>` y comprobar igualdad exacta del manifest.
3. `feat: customize S01 scaffold and smoke harness`: `.editorconfig`, `.gitignore`, `scripts/check-human-lines.py`, los deltas humanos de `*.csproj`, host/settings/HTTP templates, eliminaciones de `Class1.cs`/`UnitTest1.cs`, `SmokeTests.cs` y `HumanLineGateTests.cs`. Fijar `<S01_HUMAN_SHA>`, ejecutar la puerta técnica y medir únicamente `<GENERATED_SCAFFOLD_SHA>...<S01_HUMAN_SHA>`.
4. `docs: record S01 gate evidence`: solo `docs/evaluator-execution.md` y `specs/001-school-enrollment-management/tasks.md`, después de sustituir los tres placeholders con SHAs reales, copiar salidas verificadas y marcar V2-T010. Es un work unit documental normal y acotado; no usa `EX-PLAN-2026-07-10` y no altera `HUMAN_HEAD`.

### Plantilla de evidencia V2-T010

```text
SLICE_BASE=<PLANNING_DOC_SHA>
HUMAN_BASE=<GENERATED_SCAFFOLD_SHA>
HUMAN_HEAD=<S01_HUMAN_SHA>
GENERATED_MANIFEST=docs/generated-manifests/s01.txt
generated manifest diff=<PENDING PASS/FAIL>
human additions+deletions=<PENDING INTEGER>
restore/build Debug+Release/test Debug+Release/format/vulnerabilities/diff=<PENDING>
OpenAPI tree/checksum=<PENDING>
V2-T010=<PENDING; MUST REMAIN UNCHECKED WITHOUT REAL SHAS>
```

## Evidencia y rollback por slice

- Guardar en `docs/evaluator-execution.md`:
  - Dependencias ejecutadas.
  - Base y cabeza inmutables del gate (`SLICE_BASE`, `HUMAN_BASE`, `HUMAN_HEAD`).
  - Salida generada y evidencias de P0/P1.
  - Resultado de validaciones (restore/build/test y gates).
- Rollback de slice: revertir únicamente el commit aislado de esa unidad para preservar trazabilidad.

## Comandos base por slice

```bash
dotnet restore
dotnet build --no-restore --configuration Debug
dotnet build --no-restore --configuration Release
dotnet test --no-build --no-restore --configuration Debug
dotnet test --no-build --no-restore --configuration Release
dotnet format --verify-no-changes --no-restore
dotnet list package --vulnerable --include-transitive
```

## Evidencia técnica S01 — 2026-07-10

- `dotnet restore`: PASS.
- `dotnet build --no-restore` en Debug y Release: PASS, cero warnings y cero errores.
- `dotnet test --no-build --no-restore` en Debug y Release: PASS; 6 casos unitarios y 2 pruebas HTTP en cada configuración, cero omitidas.
- `dotnet format --verify-no-changes --no-restore`: PASS.
- `dotnet list package --vulnerable --include-transitive`: PASS, ningún paquete vulnerable en los cinco proyectos.
- `git diff --check`: PASS.
- Contrato automatizado/sintético: 400 devuelve `0`; 401 devuelve `1`; entrada malformada, no entera o binaria devuelve `2`. Los cinco casos están automatizados en `HumanLineGateTests.cs` y también fueron ejecutados manualmente.
- Reconstrucción local previa a commits: las 16 plantillas exactas del manifest seguidas por la personalización actual producen 360 líneas humanas. Es una comprobación preparatoria, no un PASS de V2-T010 ni un sustituto de los tres SHAs reales.
- Contrato canónico: árbol sin diferencias/untracked y checksum combinado `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`.
- Gate S01 por SHAs: PENDIENTE; requiere los commits aislados que esta ejecución no está autorizada a crear.

## Notas operativas

- No usar secretos dentro de repositorio.
- No asumir cobertura previa de dominio: antes de `S02` las pruebas de integración son solo smoke.
- No modificar `specs/.../contracts` durante ejecución.
- El bundle YAML canónico es la fuente contractual. S01 no registra generación OpenAPI runtime porque `Microsoft.AspNetCore.OpenApi` `10.0.9` arrastra una dependencia con vulnerabilidad alta.
