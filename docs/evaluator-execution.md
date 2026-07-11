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
| S03 | `57d322974c9e177ecfa834ff39a11e6e5c4e7b97` | `57d322974c9e177ecfa834ff39a11e6e5c4e7b97` | `fb4309f52202c93b8b192d7393194089a56f2690` | PASS: sin salida generada, 338 líneas humanas, dentro del límite de 400. |
| S04 | `8136dc0cfde187b39a4c211fdff63de24ec3fb89` | `8136dc0cfde187b39a4c211fdff63de24ec3fb89` | `e43c032c648beda45febcbd4b8fcf4282d0bfdf1` | PASS: sin salida generada, 394 líneas humanas, dentro del límite de 400. |
| S05 modelo (V2-T032–V2-T035) | `43e3635eb91de9902898949ffeda9ed428fe438a` | `43e3635eb91de9902898949ffeda9ed428fe438a` | `b46fc52ccef011fbe9b1d38a014efd98abcea157` | PASS: sin salida generada, 285 líneas humanas; cierre final registrado en la unidad siguiente. |
| S05 workflow (V2-T036–V2-T037) | `100b0e6511c34681823ce7ad7b798da78a38b772` | `100b0e6511c34681823ce7ad7b798da78a38b772` | `f48748febea49a212ddfed983f1d361d416801e2` | PASS: sin salida generada, 400 líneas humanas exactas, dentro del límite sin excepción; S05 cerrado. |
| S06 modelo (V2-T038,V2-T040–V2-T041) | `ea8335496badae0c4de4de81cb61a661a23f8da6` | `ea8335496badae0c4de4de81cb61a661a23f8da6` | `28e25a25546763b268a9565db466b79be6c52de7` | PASS: sin salida generada, 363 líneas humanas; cierre final registrado en la unidad siguiente. |
| S06 workflow (V2-T039,V2-T042–V2-T043) | `f0fdfeea65cde336e1093ba2890b5d713d99fcfe` | `f0fdfeea65cde336e1093ba2890b5d713d99fcfe` | `247794aa41597f5c6d65934e3215a0f99a5d9352` | PASS: sin salida generada, 375 líneas humanas, dentro del límite sin excepción; S06 cerrado. |

### Secuencia exacta ejecutada

1. `docs: finalize production model plan` (`757b552ca3215371c0006d39bf0d0a14fabfdc11`): archivos de planificación bajo `.atl/`, `.specify/memory/`, `README.md`, `docs/` y `specs/`, incluido este documento y el manifest S01. Es el único work unit cubierto por `EX-PLAN-2026-07-10`.
2. `chore: add generated .NET scaffold` (`dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47`): solo las 16 rutas de `docs/generated-manifests/s01.txt`, en el estado producido por el SDK .NET `10.0.109` con `dotnet new`/`dotnet sln add`; el diff del manifest no produjo salida.
3. `feat: customize S01 scaffold and smoke harness` (`5dc32432d489eb342fed0221ff6b545036727b75`): `.editorconfig`, `.gitignore`, `scripts/check-human-lines.py`, deltas humanos de templates, eliminaciones de placeholders y pruebas smoke/HTTP/gate. El rango humano inmutable produjo exactamente `360`.
4. `docs: record S01 gate evidence`: solo este documento y `tasks.md`; registra evidencia sin mover `HUMAN_HEAD` y no usa `EX-PLAN-2026-07-10`.
5. `feat: complete catalog protections and seeding` (`fb4309f52202c93b8b192d7393194089a56f2690`): work unit S03B de 11 rutas exclusivamente bajo `src/` y `tests/`; sin salida generada.
6. `feat: add person identity and role model` (`e43c032c648beda45febcbd4b8fcf4282d0bfdf1`): work unit S04 de 11 rutas exclusivamente bajo `src/` y `tests/`; sin salida generada.
7. `feat: add annual enrollment model` (`b46fc52ccef011fbe9b1d38a014efd98abcea157`): work unit de modelo S05 de 6 rutas exclusivamente bajo `src/` y `tests/`; sin salida generada.
8. `feat: add atomic enrollment workflow` (`f48748febea49a212ddfed983f1d361d416801e2`): work unit S05 de command/puertos/transacción y pruebas de confiabilidad en 6 rutas bajo `src/` y `tests/`; sin salida generada.
9. `feat: add teacher contract model` (`28e25a25546763b268a9565db466b79be6c52de7`): checkpoint de modelo S06 en 5 rutas bajo `src/` y `tests/`; sin salida generada y sin workflow `Serializable`.
10. `feat: add atomic teacher contract workflow` (`247794aa41597f5c6d65934e3215a0f99a5d9352`): work unit S06 de command/puertos/transacción `Serializable` y pruebas de solapamiento/carrera en 5 rutas bajo `src/` y `tests/`; sin salida generada.

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

## Evidencia técnica S03 — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=57d322974c9e177ecfa834ff39a11e6e5c4e7b97`; `HUMAN_HEAD=fb4309f52202c93b8b192d7393194089a56f2690`; sin manifest/salida generada.
- Testcontainers usó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` (`sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74`), sin fallback externo.
- `IT-CATALOG-SCHEMA-S03`, `IT-CATALOG-MUTABILITY-S03` e `IT-CATALOG-SINGLETON-S03`: 3/3 PASS; concurrencia y rollback/cleanup/retry de seed: 2/2 PASS.
- Seed exacto Id=1/timestamps, recuperación vacía/parcial, conflicto 51010, dos callers independientes sin deadlock/duplicados, fallo inyectado 51020 con rollback y retry en la misma sesión: PASS.
- Triggers case-sensitive, rol/permisos runtime, singleton anti-delete y startup configurado positivo/negativo: PASS contra cinco tablas catalog.
- Suites Debug y Release: 22 unitarias + 10 integración, cero fallos/omitidas; filtro `Priority=P0`: 16 unitarias + 8 integración = 24/24 PASS.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff y OpenAPI: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 26/103 completas y V2-T027 primera pendiente.
- Gate inmutable `git diff --numstat 57d322974c9e177ecfa834ff39a11e6e5c4e7b97...fb4309f52202c93b8b192d7393194089a56f2690 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `338`.
- V2-T020 y V2-T023–V2-T026: PASS; S03 cerrado. S04/V2-T027 queda habilitado.

## Evidencia técnica S04 — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=8136dc0cfde187b39a4c211fdff63de24ec3fb89`; `HUMAN_HEAD=e43c032c648beda45febcbd4b8fcf4282d0bfdf1`; sin manifest/salida generada.
- Testcontainers usó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` (`sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74`), con fallback externo deshabilitado.
- `UT-IDENTITY`: 9/9 PASS; `IT-PERSON-COLLATION`, `IT-PERSON-DUAL-ROLE` e `IT-TEXT-CHECKS`: 3/3 PASS contra SQL Server real.
- Identidad compuesta por tipo+número, NFC/case/acento, conflicto de nombres/nacimiento, fecha futura, roles duales, checks de whitespace por frontera, PK/FK/auditoría/rowversion e índices exactos: PASS.
- Suites Debug y Release: 31 unitarias + 13 integración = 44/44 en cada configuración; filtro `Priority=P0`: 25 unitarias + 11 integración = 36/36 PASS; cero fallos/omitidas.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff y OpenAPI/checksum: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 31/103 completas y V2-T032 primera pendiente.
- Gate inmutable `git diff --numstat 8136dc0cfde187b39a4c211fdff63de24ec3fb89...e43c032c648beda45febcbd4b8fcf4282d0bfdf1 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `394`.
- V2-T027–V2-T031: PASS; S04 cerrado. V2-T032 permanece pendiente y S05 queda habilitado.

## Evidencia técnica S05 modelo — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=43e3635eb91de9902898949ffeda9ed428fe438a`; `HUMAN_HEAD=b46fc52ccef011fbe9b1d38a014efd98abcea157`; sin manifest/salida generada.
- Testcontainers usó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` (`sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74`), con fallback externo deshabilitado.
- `IT-ENR-ANNUAL` e `IT-NORMAL-FORMS`: 2/2 PASS; `EnrollmentModelTests`: 3/3 PASS contra SQL Server real.
- Unicidad Student+año, historia multianual, rechazo de año divergente por FK compuesto, ausencia de School/Grade/estado derivado en Enrollment, normalización/check de ClassGroup, asignación exacta de auditoría y nombres/keys/includes/clustering: PASS.
- Suites Debug y Release: 31 unitarias + 16 integración = 47/47 en cada configuración; filtro `Priority=P0`: 25 unitarias + 14 integración = 39/39 PASS; cero fallos/omitidas.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff y OpenAPI/checksum: PASS.
- Checkpoint documental previo al workflow: OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines en PASS con 35 tareas completas; el estado vigente se registra en la evidencia siguiente.
- Gate inmutable `git diff --numstat 43e3635eb91de9902898949ffeda9ed428fe438a...b46fc52ccef011fbe9b1d38a014efd98abcea157 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `285`.
- V2-T032–V2-T035: PASS. El cierre de V2-T036–V2-T037 se registra en la evidencia siguiente.

## Evidencia técnica S05 workflow y cierre — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=100b0e6511c34681823ce7ad7b798da78a38b772`; `HUMAN_HEAD=f48748febea49a212ddfed983f1d361d416801e2`; sin manifest/salida generada.
- Testcontainers usó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` (`sha256:c1aa8afe9b06eab64c9774a4802dcd032205d1be785b1fd51e1c0151e7586b74`), con `ConnectionStrings__InovaitTest` explícitamente ausente.
- Targeted V2-T036: 13 unitarias + 6 integración SQL = 19/19 PASS; cubre cada resultado/error canónico, identidad compuesta, alta/reuso de Student, anualidad, rollback/cancelación, context cleanup, carrera sincronizada con conexiones independientes, retry real y agotamiento de tres intentos.
- V2-T037: año divergente rechazado por FK compuesto, `Enrollment` sin School/Grade, índice `IX_Enrollment_ClassGroupId_StudentPersonId` con key/include exactos e Id implícito por PK clustered: PASS.
- Suites Debug y Release: 44 unitarias + 19 integración = 63/63 en cada configuración; filtro `Priority=P0`: 38 unitarias + 17 integración = 55/55 PASS; cero fallos/omitidas.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff, árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 37/103 completas y V2-T038 primera pendiente.
- Gate inmutable `git diff --numstat 100b0e6511c34681823ce7ad7b798da78a38b772...f48748febea49a212ddfed983f1d361d416801e2 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `400`, sin excepción.
- V2-T036 y V2-T037: PASS. S05 cerrado; V2-T038 inicia S06.

## Evidencia técnica S06 modelo — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=ea8335496badae0c4de4de81cb61a661a23f8da6`; `HUMAN_HEAD=28e25a25546763b268a9565db466b79be6c52de7`; sin manifest/salida generada.
- Testcontainers usó `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`, con `ConnectionStrings__InovaitTest` explícitamente ausente.
- `UT-CONTRACT-CANCELLATION`, `UT-CONTRACT-STATUS` y `UT-CONTRACT-OVERLAP`: 19/19 PASS; prueban transición, límites de estado según inicio/fin/cancelación efectiva e intersección inclusiva pura con fin abierto.
- `TeacherContractModelTests`: 3/3 PASS contra SQL Server real: `IT-CON-CANCELLATION`, helper `ModelEvidence=CONTRACT-EXACT-OPEN-UNIQUE` y metadata de mapping/índices/PK clustered. El helper solo acredita duplicado exacto abierto y escuela distinta; no acredita todos los solapamientos.
- Entidad, dos FK `NoAction`, siete checks, UQ exacto abierto sin filtro, auditoría/rowversion y dos índices con key/include exactos sin `Id` declarado: PASS.
- `IT-CON-OVERLAP` completo no se declara ni se acredita todavía; V2-T039 permanece pendiente hasta integrar detección no exacta/carrera mediante el workflow `Serializable` de V2-T042.
- Suites Debug y Release: 63 unitarias + 22 integración = 85/85 en cada configuración; filtro `Priority=P0`: 57 unitarias + 20 integración = 77/77 PASS; cero fallos/omitidas.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff, árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 40/103 completas y V2-T039 primera pendiente.
- Gate inmutable `git diff --numstat ea8335496badae0c4de4de81cb61a661a23f8da6...28e25a25546763b268a9565db466b79be6c52de7 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `363`, sin excepción.
- V2-T038,V2-T040,V2-T041: PASS. V2-T039 y V2-T042–V2-T043 permanecen pendientes; S06 no está cerrado.

## Evidencia técnica S06 workflow — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=f0fdfeea65cde336e1093ba2890b5d713d99fcfe`; `HUMAN_HEAD=247794aa41597f5c6d65934e3215a0f99a5d9352`; sin manifest/salida generada.
- `ConnectionStrings__InovaitTest` estuvo explícitamente ausente; la evidencia SQL usó Testcontainers con `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` y bases limpias.
- Targeted contractual: 27 unitarias + 6 integración SQL = 33/33 PASS. Los 6 casos `TeacherContractModelTests` completan `IT-CON-CANCELLATION`, `IT-CON-OVERLAP`, exact-open, checks/FK, índices/PK clustered, referencias y cancelación/rollback.
- `IT-CON-OVERLAP`: PASS para período no exacto y abierto, toque inclusivo, mismo docente en escuela distinta y carrera sincronizada con dos `DbConnection` independientes; `Serializable` deja exactamente un contrato y el perdedor reintenta hasta `OverlapConflict`.
- Workflow: PASS para command/resultados/errores canónicos, docente/escuela/rango/selección duplicada, prevalidación multiescuela sin escritura parcial, cancelación con rollback no cancelable y tracker limpio.
- Filtro `Priority=P0`: 65 unitarias + 23 integración = 88/88 PASS; suites Debug y Release: 71 unitarias + 25 integración = 96/96 en cada configuración; cero fallos/omitidas.
- Restore, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff, árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 43/103 completas y V2-T044 primera pendiente.
- Gate inmutable `git diff --numstat f0fdfeea65cde336e1093ba2890b5d713d99fcfe...247794aa41597f5c6d65934e3215a0f99a5d9352 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `375`, sin excepción.
- V2-T039,V2-T042,V2-T043: PASS. S06 cerrado; V2-T044 inicia la migración P0 generada y aislada de S07.

## Notas operativas

- No usar secretos dentro de repositorio.
- No asumir cobertura previa de dominio: antes de `S02` las pruebas de integración son solo smoke.
- No modificar `specs/.../contracts` durante ejecución.
- El bundle YAML canónico es la fuente contractual. S01 no registra generación OpenAPI runtime porque `Microsoft.AspNetCore.OpenApi` `10.0.9` arrastra una dependencia con vulnerabilidad alta.
