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
| Planificación (`EX-PLAN-2026-07-10`) | `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` | N/A | `757b552ca3215371c0006d39bf0d0a14fabfdc11` | Excepción aprobada solo para este work unit documental; no es un PASS del gate S01. |
| S01 | `757b552ca3215371c0006d39bf0d0a14fabfdc11` | `dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47` | `5dc32432d489eb342fed0221ff6b545036727b75` | PASS: manifest exacto y 360 líneas humanas, dentro del límite de 400. |
| S02 | `1d627eba7acc46aed404e5d4bd818766a855adbb` | `1d627eba7acc46aed404e5d4bd818766a855adbb` | `0ecac062e5ef08114b0777fde53082f98840444a` | PASS: sin salida generada, 253 líneas humanas, dentro del límite de 400. |

### Secuencia exacta ejecutada

1. `docs: finalize production model plan` (`757b552ca3215371c0006d39bf0d0a14fabfdc11`): archivos de planificación bajo `.atl/`, `.specify/memory/`, `README.md`, `docs/` y `specs/`, incluido este documento y el manifest S01. Es el único work unit cubierto por `EX-PLAN-2026-07-10`.
2. `chore: add generated .NET scaffold` (`dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47`): solo las 16 rutas de `docs/generated-manifests/s01.txt`, en el estado producido por el SDK .NET `10.0.109` con `dotnet new`/`dotnet sln add`; el diff del manifest no produjo salida.
3. `feat: customize S01 scaffold and smoke harness` (`5dc32432d489eb342fed0221ff6b545036727b75`): `.editorconfig`, `.gitignore`, `scripts/check-human-lines.py`, deltas humanos de templates, eliminaciones de placeholders y pruebas smoke/HTTP/gate. El rango humano inmutable produjo exactamente `360`.
4. `docs: record S01 gate evidence`: solo este documento y `tasks.md`; registra evidencia sin mover `HUMAN_HEAD` y no usa `EX-PLAN-2026-07-10`.

### Evidencia V2-T010

```text
SLICE_BASE=757b552ca3215371c0006d39bf0d0a14fabfdc11
HUMAN_BASE=dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47
HUMAN_HEAD=5dc32432d489eb342fed0221ff6b545036727b75
GENERATED_MANIFEST=docs/generated-manifests/s01.txt
generated manifest diff=PASS (exactly 16 paths; no diff output)
human additions+deletions=360
human gate output=360
restore=PASS
build Debug=PASS (0 warnings, 0 errors)
build Release=PASS (0 warnings, 0 errors)
test Debug=PASS (6 unit, 2 integration, 0 failed, 0 skipped)
test Release=PASS (6 unit, 2 integration, 0 failed, 0 skipped)
format verify=PASS
vulnerable package scan=PASS (0 vulnerable packages in 5 projects)
git diff --check=PASS
OpenAPI tree=PASS (no diff or untracked contract files)
OpenAPI checksum=802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a  -
V2-T010=PASS
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
- Manifest generado: PASS; `SLICE_BASE..HUMAN_BASE` contiene exactamente las 16 rutas declaradas y `diff -u` no produjo salida.
- Gate S01 inmutable: `git diff --numstat "dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47"..."5dc32432d489eb342fed0221ff6b545036727b75" -- | ./scripts/check-human-lines.py` devolvió estado `0` y salida exacta `360`.
- Contrato canónico: árbol sin diferencias/untracked y checksum combinado `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`.
- Gate S01 por SHAs: PASS; V2-T010 completada con 360 líneas humanas, sin excepción de tamaño.

## Evidencia técnica S02 — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=1d627eba7acc46aed404e5d4bd818766a855adbb`; no existe salida generada ni manifest S02.
- `HUMAN_HEAD=0ecac062e5ef08114b0777fde53082f98840444a`; la documentación posterior de evidencia no mueve este SHA inmutable.
- Testcontainers ejecutó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` (`sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74`); el fixture relacional no usó el fallback externo.
- Casos relacionales ejecutados: `ProductionRegistrations_ResolveAndConnectToSqlServer` y `ProbeModel_UsesDefaultsCheckRealUpdatesAndRowVersion`.
- El probe propiedad del test comprobó defaults SQL UTC, CHECK mediante error 547, update real con preservación de creación, normalización y conflicto `DbUpdateConcurrencyException` por `rowversion`.
- Los casos relacionales pertenecen a `Priority=P0` pero deliberadamente no publican un ID `Evidence`: S02 no acredita prematuramente `IT-AUDIT-UTC-P0/P1` ni `IT-ROWVERSION-P0/P1`.
- `dotnet restore`: PASS.
- Build Debug y Release: PASS, cero warnings y cero errores.
- Tests Debug y Release: PASS en cada configuración; 22 unitarios + 5 integración, cero fallos y cero omitidos.
- Filtro `Priority=P0`: PASS; 16 unitarios + 3 integración, incluidos los dos casos relacionales reales.
- `dotnet format --verify-no-changes --no-restore`: PASS.
- `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos.
- `git diff --check 1d627eba7acc46aed404e5d4bd818766a855adbb...0ecac062e5ef08114b0777fde53082f98840444a`: PASS.
- Gate inmutable `git diff --numstat 1d627eba7acc46aed404e5d4bd818766a855adbb...0ecac062e5ef08114b0777fde53082f98840444a -- | ./scripts/check-human-lines.py`: PASS, salida exacta `253`, dentro del límite de 400 y sin excepción.
- V2-T017, V2-T018 y V2-T019: PASS. S02 cerrado; S03 queda habilitado.

## Notas operativas

- No usar secretos dentro de repositorio.
- No asumir cobertura previa de dominio: antes de `S02` las pruebas de integración son solo smoke.
- No modificar `specs/.../contracts` durante ejecución.
- El bundle YAML canónico es la fuente contractual. S01 no registra generación OpenAPI runtime porque `Microsoft.AspNetCore.OpenApi` `10.0.9` arrastra una dependencia con vulnerabilidad alta.
