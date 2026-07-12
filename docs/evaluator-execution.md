# Instrucciones del evaluador técnico

> **Estado actual**: 103/103 tareas completas, 0 pendientes; `production-model-v2.0.0` está cerrado (S01–S18). Existen el modelo completo de 14 tablas/5 triggers (cadena de cuatro migraciones EF: `InitialP0ProductionModel`, `AddP0DatabaseProtections`, `AddP1TeachingModel`, `AddP1DatabaseProtections`), `database/setup.sql` con paridad verificada contra la BD migrada, los 15 `operationId` mapeados en runtime (`IT-OPENAPI`), y los cuatro reportes/historia P1 (`getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`, `getStudentHistory`) sobre el host P0 ya entregado. `./scripts/run-p0-tests.sh` termina `P0 GATE PASSED: 37/37` y `./scripts/run-p1-tests.sh` termina `P1 GATE PASSED: 13/13`. Las dispensas `EX-INTEGRITY-2026-07-11` quedaron registradas en S08, S12, S13A/S13B y S14–S16/S17; el resto de los slices cerró sin excepción. El empaquetado/paquete final fuera del repositorio que menciona V2-T103 no se ejecutó (requiere autorización explícita no otorgada).

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

Para S07, [`docs/generated-manifests/s07.txt`](generated-manifests/s07.txt) clasifica exactamente las cuatro rutas producidas por EF: migration inicial, ambos designers y snapshot. Tres quedaron en `130e642`; el designer de la migración manual quedó junto al cierre `a629a71`. El gate inmutable S07 usa el rango completo `0f777fb...a629a71`, excluye esas cuatro rutas declaradas y cuenta únicamente la migración manual y las dos rutas de pruebas.

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
| S07 (V2-T044–V2-T046) | `0f777fbd17417b42351013c2477623808e55ce1f` | `130e642c053e02211268a407ac4dfd2746fc0363` | `a629a712bf7f3b7a7d994c3cec42a4391d28a0e2` | PASS: manifest exacto de cuatro rutas generadas y 384 líneas humanas excluyéndolas, sin excepción; S07 cerrado. |
| S08 (V2-T047–V2-T051) | `1f2c246bce3b8e08c3dddb7ee36a284a9a5682eb` | `1f2c246bce3b8e08c3dddb7ee36a284a9a5682eb` | `dbf431ecadf4733160b8dba00638bde794c18e0d` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: sin salida generada/manifest; gate humano inmutable mide `1976` líneas humanas (bundle `50ba6ed` aislado: 1556), supera 400; registrado por reconciliación S08–S11 autorizada por el usuario (completitud sobre tamaño de slice); S08 cerrado. |
| S09 (V2-T052–V2-T057) | `86678088959216952592203345ba016d6356ceba` | `86678088959216952592203345ba016d6356ceba` | `5bb861ec9b228f102c16459038748166f5481f94` | PASS: sin salida generada, 365 líneas humanas, dentro del límite sin excepción; S09 cerrado. |
| S10 (V2-T058–V2-T061) | `52cbb518f5f47aee8fda12e097cdbfb83f97bfb4` | `52cbb518f5f47aee8fda12e097cdbfb83f97bfb4` | `3212b4ee2f1ec4070fdb1b70d6ee823483fd5b57` | PASS: sin salida generada, 99 líneas humanas, dentro del límite sin excepción; S10 cerrado. |
| S11 (V2-T062–V2-T067) | `5eb43965448010d6ee86acbed9ff7133738f9e82` | `5eb43965448010d6ee86acbed9ff7133738f9e82` | `21833491b5df294e776ad7439a8a06c5143d08c9` | PASS: sin salida generada, 274 líneas humanas, dentro del límite sin excepción; S11 cerrado. |
| S12 (V2-T068–V2-T075) | `e96edefd6a0df37f238b7c5b9ff2fbf188eb069d` | `e96edefd6a0df37f238b7c5b9ff2fbf188eb069d` | `4cf41d055eb1df8f23d13178469c4ecf9a777e34` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: sin salida generada/manifest; gate humano inmutable mide `1150` líneas (`008f4eb`=13, `6cc187c`=420, `f322442`=422, `4cf41d0`=295), supera 400; walkthroughs P0 (33 pruebas) y `run-p0-tests.sh` 37/37 en verde; S12 cerrado. |
| S13A (V2-T076–V2-T080) | `23dbe3efd730a4a89ba98f5e0b9e3bff6f03b437` | `23dbe3efd730a4a89ba98f5e0b9e3bff6f03b437` | `33c024154d579137524e3849c12d12de0b5c5b51` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: sin salida generada; gate humano inmutable mide `673` líneas, supera 400; modelo P1 (`Subject`/`TeachingAssignment`/`ClassSchedule`) y validación transaccional de escuela/contención temporal en un solo work unit `33c0241`. |
| S13B (V2-T081–V2-T083) | `33c024154d579137524e3849c12d12de0b5c5b51` | `f884d720d48d8f921df57f39b379caeab1f564b5` (solo salida generada) | `95ea7e3e9295111d25806f6e2b17aa79ae25cb57` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: manifest exacto de cuatro rutas (`docs/generated-manifests/s13.txt`, incluido el Designer manual, precedente S07) excluido del conteo; gate humano inmutable mide `814` líneas, supera 400; migración manual `AddP1DatabaseProtections` y extensión de `database/setup.sql`/`SetupSqlParityTestsP1` a 14 tablas en `95ea7e3`. |
| S13C (V2-T084–V2-T087) | `95ea7e3e9295111d25806f6e2b17aa79ae25cb57` | `95ea7e3e9295111d25806f6e2b17aa79ae25cb57` | `40429aa88d24e4b06a5407d644cba0f4d0981534` | PASS: sin salida generada, `115` líneas humanas, dentro del límite de 400 y sin excepción; `listSubjects` end-to-end en `40429aa`; S13 cerrado. |
| S14–S16 (V2-T088–V2-T096) | `18cdc7909ce9bee6bfb97246d9f517caf85ab426` | `18cdc7909ce9bee6bfb97246d9f517caf85ab426` | `c86cc845ce5ae1bf98e447c1debc61174c583f93` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: sin salida generada; gate humano inmutable mide `826` líneas, supera 400; los tres reportes (`getAgeDistribution`, `getDistinctTeacherCountsBySector`, `getTopSchoolsByEnrollment`) en un solo work unit `c86cc84`. |
| S17 (V2-T097–V2-T099) | `c86cc845ce5ae1bf98e447c1debc61174c583f93` | `c86cc845ce5ae1bf98e447c1debc61174c583f93` | `e8d404648fdd9a257b50abb8b4a3cdbb94f14b93` | PASS con dispensa `EX-INTEGRITY-2026-07-11`: sin salida generada; gate humano inmutable mide `460` líneas, supera 400; `getStudentHistory` end-to-end en `e8d4046`; S17 cerrado. |
| S18 (V2-T100–V2-T103) | `e8d404648fdd9a257b50abb8b4a3cdbb94f14b93` | `e8d404648fdd9a257b50abb8b4a3cdbb94f14b93` | `015cc6aa04d1e674c17e7785e4a3eb952629675e` | PASS: sin salida generada, `375` líneas humanas, dentro del límite de 400 y sin excepción; `IT-OPENAPI` y `run-p1-tests.sh` en `015cc6a`; S18 cerrado; proyecto completo. |

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
11. `chore: add initial P0 production migration` (`130e642c053e02211268a407ac4dfd2746fc0363`): scaffold inicial, designer y snapshot EF de 11 tablas; tres de las cuatro rutas del manifest S07.
12. `feat: add P0 database protections and migration evidence` (`a629a712bf7f3b7a7d994c3cec42a4391d28a0e2`): migration manual, designer generado y seis evidencias S07; el gate excluye las cuatro rutas generadas del manifest y cuenta 384 líneas humanas.
13. `test: scope contract identity check to canonical bundle` (`008f4eb`), `feat: add reproducible database setup script` (`6cc187c`), `test: verify setup.sql parity, seeds, and index topology` (`f322442`) y `feat: add P0 evidence runner and local SQL Server compose` (`4cf41d055eb1df8f23d13178469c4ecf9a777e34`): contenido S12 completo — `database/setup.sql`, `SetupSqlParityTests.cs`, `RelationalIndexTests.cs`, `scripts/run-p0-tests.sh` y `compose.yaml`; sin salida generada.
14. `docs: update S12 local eval and run instructions` (`4de4f4e`), `docs: close S12 walkthrough and gate` (`6ff0535`) y `docs: align S12 human head to current commit` (`b16c5cb`, contabilidad corregida en la unidad documental siguiente): unidades documentales de V2-T072 y cierre de gate; registran evidencia sin mover `HUMAN_HEAD`.

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

## Evidencia técnica S07 — 2026-07-11

- `SLICE_BASE=0f777fbd17417b42351013c2477623808e55ce1f`; `HUMAN_BASE=130e642c053e02211268a407ac4dfd2746fc0363`; `HUMAN_HEAD=a629a712bf7f3b7a7d994c3cec42a4391d28a0e2`.
- `docs/generated-manifests/s07.txt`: cuatro rutas exactas — migration inicial, ambos designers y snapshot. El gate sobre `SLICE_BASE...HUMAN_HEAD` excluyó esas cuatro rutas y contó exclusivamente `AddP0DatabaseProtections.cs` (91), `AuditConcurrencyTests.cs` (119) y `P0DatabaseProtectionTests.cs` (174): salida exacta `384`.
- `ConnectionStrings__InovaitTest` estuvo ausente; Testcontainers usó SQL Server 2022 CU14 y bases limpias.
- Targeted S07: 6/6 PASS — `IT-SCHEMAS-P0`, `IT-IMMUTABILITY`, `IT-SINGLETON`, `IT-REFERENCE-PERMISSIONS`, `IT-AUDIT-UTC-P0`, `IT-ROWVERSION-P0`.
- Migration lifecycle: empty apply, repeat apply, Down seguro frente a rol ajeno (51006), rol propio con miembro, revocación/remoción de miembro, down-to-zero, rechazo de rol ajeno en Up (51005), script idempotente ejecutado dos veces y reapply: PASS.
- Modelo físico: 11 tablas/cuatro schemas exactos, cuatro triggers, seed UTC fijo `2026-01-01`, singleton/startup, marca de propiedad del rol, GRANT/DENY de runtime, siete entidades auditables/rowversion, `Enrollment` creation-only y tres negativos sin auditoría: PASS.
- Filtro `Priority=P0`: 65 unitarias + 29 integración = 94/94 PASS; suites Debug y Release: 71 unitarias + 31 integración = 102/102 en cada configuración; cero fallos/omitidas.
- Restore locked, builds Debug/Release con cero warnings/errores, format, vulnerabilidades, diff, árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- Gate humano inmutable: PASS, salida exacta `384`, dentro del límite 400 y sin excepción.
- V2-T044–V2-T046: PASS. S07 cerrado; V2-T047 inicia S08 bajo ownership del worktree API.

## Evidencia técnica S08 host y catálogos — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=1f2c246bce3b8e08c3dddb7ee36a284a9a5682eb`; `HUMAN_HEAD=dbf431ecadf4733160b8dba00638bde794c18e0d`; sin salida generada ni manifest S08.
- Composición honesta del rango: `50ba6ede15629624b212e87d44c9da4d02aae2a2` (bundle preexistente de API S08–S11, 1556 líneas humanas), `2a7bdd381c58349bc8a193141f632ba41f23bf66` (chore de configuración de Claude Code, `.claude/settings.json`+`CLAUDE.md`, 98), `6d2d9f9197c570ede01e9fd53e6d6cd3f88c48d8` (chore de registro de skills, 177), `937bf3d7c9626acaeac5a2da5a216dbf90ece6eb` (fix de formato, 15) y `dbf431ecadf4733160b8dba00638bde794c18e0d` (endpoints de salud, 136); la suma simple por commit da 1982, pero `937bf3d` reformatea líneas recién agregadas por `50ba6ed`, así que el diff neto real y verificado del rango completo (`git diff --numstat "$HUMAN_BASE"..."$HUMAN_HEAD" -- | ./scripts/check-human-lines.py`) es la cifra autoritativa: `1976`. Supera 400 y queda registrado bajo la dispensa de integridad autorizada por el usuario `EX-INTEGRITY-2026-07-11` (2026-07-11: completitud/integridad sobre el tamaño de slice para esta reconciliación S08–S11; `EX-PLAN-2026-07-10` no aplica aquí). El bundle `50ba6ed` es anterior al ledger y se reconcilia ahora; la evidencia de S09–S11 permanece pendiente y cerrará en sus propios slices.
- Fixture: Testcontainers con imagen fijada `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`; `ConnectionStrings__InovaitTest` explícitamente ausente del entorno.
- Targeted `Priority=P0&Evidence=IT-CATALOGS`: 3/3 PASS, en `tests/Inovait.IntegrationTests/Api/CatalogEndpointsTests.cs`.
- Filtro `Priority=P0`: 65 unitarias + 49 integración = 114/114 PASS; suites Debug y Release: 71 unitarias + 53 integración = 124/124 en cada configuración; cero fallos/omitidas.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes --no-restore`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos. `git status --porcelain -- specs`: PASS, sin diferencias. Árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- Endpoints de salud `/health/live` y `/health/ready` (probe real `CanConnectAsync`, degradación `503`) agregados en `dbf431e` fuera del contrato OpenAPI, por adición mandatada por el usuario; sin `.WithName`, el conteo de operationId runtime permanece en 10.
- Desviación V2-T048: el fixture conserva `ConnectionStrings__InovaitTest` como fallback externo documentado y exclusivo de pruebas, por directiva del usuario del 2026-07-11; esta directiva sustituye la redacción de retiro original de la tarea sin alterar la ruta Testcontainers primaria.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 51/103 completas y V2-T052 primera pendiente.
- V2-T047–V2-T051: PASS. S08 cerrado; V2-T052 inicia S09.

## Evidencia técnica S09 inscripción atómica — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=86678088959216952592203345ba016d6356ceba`; `HUMAN_HEAD=5bb861ec9b228f102c16459038748166f5481f94`; sin salida generada.
- Composición: un solo commit de tests `5bb861e` (test: cover enrollment identity, context, and atomicity scenarios), 365 líneas humanas — `CreateEnrollmentTests.cs` (234) + `EnrollmentAtomicityTests.cs` (131).
- Fixture: Testcontainers con imagen fijada `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04` vía `SqlServerFixture`; `ConnectionStrings__InovaitTest` explícitamente ausente del entorno.
- Targeted por Evidence ID: `IT-ENR-CREATE` 1/1, `IT-ENR-IDENTITY` 3/3, `IT-ENR-CONTEXT` 6/6, `IT-ENR-ATOMIC` 2/2; combinado en un solo filtro `Evidence=IT-ENR-CREATE|Evidence=IT-ENR-IDENTITY|Evidence=IT-ENR-CONTEXT|Evidence=IT-ENR-ATOMIC`: 12/12 PASS.
- Filtro `Priority=P0`: 65 unitarias + 60 integración = 125/125 PASS.
- Suites Debug y Release completas: 71 unitarias + 64 integración = 135/135 en cada configuración; cero fallos/omitidas.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes --no-restore`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos. `git status --porcelain -- specs`: PASS, sin diferencias. Árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 57/103 completas y V2-T058 primera pendiente.
- Gate humano inmutable `git diff --numstat 86678088959216952592203345ba016d6356ceba...5bb861ec9b228f102c16459038748166f5481f94 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `365`, dentro del límite de 400 y sin excepción.
- V2-T052–V2-T057: PASS. S09 cerrado; V2-T058 inicia S10.

## Evidencia técnica S10 consulta de inscritos — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=52cbb518f5f47aee8fda12e097cdbfb83f97bfb4`; `HUMAN_HEAD=3212b4ee2f1ec4070fdb1b70d6ee823483fd5b57`; sin salida generada.
- Composición: un solo commit `3212b4e` (refactor: extract AgeCalculator to Core and add UT-AGE coverage), 99 líneas humanas — `AgeCalculator.cs` extraído a `src/Inovait.Core/Features/Enrollments/` con comportamiento bit-idéntico al cálculo previamente embebido en `EnrollmentReadService.cs`, más `AgeCalculatorTests.cs` (68 líneas, 15 casos nuevos).
- Fixture: Testcontainers con imagen fijada `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`; `ConnectionStrings__InovaitTest` explícitamente ausente del entorno.
- Targeted `Evidence=UT-AGE|Evidence=IT-ENR-FILTER`: 15 unitarias (`UT-AGE` nuevo: límites 3/7/8/12/13 día-previo/cumpleaños, cumpleaños exacto, 29 de febrero → 1 de marzo en años no bisiestos, fecha anterior al nacimiento con años negativos sin excepción) + 1 integración (`IT-ENR-FILTER` pre-existente en `CreateEnrollmentTests.cs`, re-verificado; el criterio original lo ubicaba en `ListEnrollmentsTests.cs`, desviación de layout documentada) = 16/16 PASS.
- Filtro `Priority=P0`: 80 unitarias + 60 integración = 140/140 PASS.
- Suites Debug y Release completas: 86 unitarias + 64 integración = 150/150 en cada configuración; cero fallos/omitidas.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes --no-restore`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos. `git status --porcelain -- specs`: PASS, sin diferencias. Árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 61/103 completas y V2-T062 primera pendiente.
- Gate humano inmutable `git diff --numstat 52cbb518f5f47aee8fda12e097cdbfb83f97bfb4...3212b4ee2f1ec4070fdb1b70d6ee823483fd5b57 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `99`, dentro del límite de 400 y sin excepción.
- V2-T058–V2-T061: PASS. S10 cerrado; V2-T062 inicia S11.

## Evidencia técnica S11 contratos multiescuela — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=5eb43965448010d6ee86acbed9ff7133738f9e82`; `HUMAN_HEAD=21833491b5df294e776ad7439a8a06c5143d08c9`; sin salida generada.
- Composición: un solo commit `2183349` (test: cover teacher contract date ranges and concurrency races), 274 líneas humanas — `TeacherContractConcurrencyTests.cs` nuevo (131 líneas, 2 facts) y `TeacherContractEndpointsTests.cs` ampliado con `IT-CON-DATES` (143 líneas, 4 facts nuevos sobre el archivo ya existente del bundle `50ba6ed`).
- Fixture: Testcontainers con imagen fijada `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`; `ConnectionStrings__InovaitTest` explícitamente ausente del entorno.
- Targeted `Evidence=IT-CON-MULTI|Evidence=IT-CON-DATES|Evidence=IT-CON-LIST|Evidence=IT-PROBLEMS|Evidence=IT-OPENAPI-P0|FullyQualifiedName~TeacherContractConcurrency`: 19/19 PASS — `IT-CON-MULTI` 1/1, `IT-CON-LIST` 1/1, `IT-CON-DATES` 4/4 (rango+abierto multiescuela, 422 `invalid_date_range`+400 `invalid_request` sin persistir, 404 teacher/school sin persistir, lote multiescuela con ítem inválido sin resultados parciales), `IT-PROBLEMS` 10/10 (9 en `InvalidRequestTests.cs` + 1 en `P0OpenApiTests.cs`), `IT-OPENAPI-P0` 1/1, `TeacherContractConcurrencyTests` (sin Evidence ID, filtrado por FQN) 2/2.
- Filtro `Priority=P0`: 80 unitarias + 66 integración = 146/146 PASS.
- Suites Debug y Release completas: 86 unitarias + 70 integración = 156/156 en cada configuración; cero fallos/omitidas.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes --no-restore`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos. `git status --porcelain -- specs`: PASS, sin diferencias. Árbol OpenAPI y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS (tree byte-idéntico desde `8aa8097`).
- OpenSpec strict/show/status, `gentle-ai sdd-status` y drift de 103 task lines: PASS, 67/103 completas y V2-T068 primera pendiente.
- Gate humano inmutable `git diff --numstat 5eb43965448010d6ee86acbed9ff7133738f9e82...21833491b5df294e776ad7439a8a06c5143d08c9 -- | ./scripts/check-human-lines.py`: PASS, salida exacta `274`, dentro del límite de 400 y sin excepción.
- Nota honesta: el path de conflicto rowversion puro (`DbUpdateConcurrencyException`) no es alcanzable por HTTP en esta etapa (solo existen create+list); la evidencia de `TeacherContractConcurrencyTests.cs` cubre el mapeo 409 por la maquinaria de reintentos SERIALIZABLE de `EfTeacherContractWorkflow`, que produce el mismo mapeo ProblemDetails. El manifest P0 de `docs/testing-strategy.md` no asigna Evidence ID a V2-T063.
- V2-T062–V2-T067: PASS. S11 cerrado; V2-T068 inicia S12.

## Evidencia técnica S12 puerta P0 — 2026-07-11

- `SLICE_BASE=HUMAN_BASE=e96edefd6a0df37f238b7c5b9ff2fbf188eb069d`
- `HUMAN_HEAD=4cf41d055eb1df8f23d13178469c4ecf9a777e34`
- `GENERATED_MANIFEST` no aplica; el rango completo S12 mide `1150` líneas humanas (> 400), registrado bajo la dispensa `EX-INTEGRITY-2026-07-11` autorizada por el usuario (integridad y completitud sobre tamaño de slice; sin sub-slices fabricados).
- Entorno real validado (walkthrough 2026-07-11 21:21 -05):
  - `docker compose -f compose.yaml up -d --wait` (imagen 2022-CU14, puerto 14333) healthy en ~12 s, con `MSSQL_SA_PASSWORD` generado in-shell y jamás escrito a archivo.
  - `database/setup.sql` aplicado dos veces sobre `InovaitWalkthrough` (exit 0/0, 11 tablas — idempotencia verificada) y fixtures ficticios vía sqlcmd (ClassGroup, segunda School, Teacher).
  - API ejecutada desde el dll Release compilado en `4cf41d0` (identidad de binario verificada con probe 404 a una ruta P1 inexistente en ese commit), con `ConnectionStrings__InovaitDatabase` efímera (incluye `TrustServerCertificate=True` solo contra el certificado autofirmado del contenedor; nunca configuración versionada).
- `scripts/run-p0-tests.sh` (P0):
  - Parsed 37 evidence IDs del manifiesto (`docs/testing-strategy.md`) y 37 IDs validados.
  - `run-p0-tests.sh`: `P0 GATE PASSED: 37/37` con `151 tests` en total (`Priority=P0`).
- Walkthroughs P0 HTTP reales contra la API viva:
  - W1 inscripción atómica: `createEnrollment` → `201` (studentReused false, edad 10); repetición idéntica → `409` `application/problem+json` código `enrollment_conflict`; `listEnrollments` → `200` con exactamente 1 fila.
  - W2 consulta: los cinco catálogos → `200` en orden canónico; filtros acumulativos de class-groups → `200`; contexto válido sin grupos → `200 []`; tiempos calentados informativos 11–24 ms.
  - W3 contratación multiescuela: `createTeacherContracts` sobre dos escuelas → `201` (2 contratos Confirmed/Effective); listas por docente y escuela → `200`; reintento solapado → `409` `teacher_contract_conflict`; rango inválido → `422` `invalid_date_range`; escuela inexistente → `404` `school_not_found`; JSON malformado → `400` `invalid_request`; leak-scan de todos los cuerpos sin detalles SQL/internos.
  - Teardown: API detenida, `docker compose down -v` sin contenedores/volúmenes residuales ni archivos espurios en `git status`.
- Validaciones base en `4cf41d0`: `dotnet restore`, builds Debug/Release (0 warnings), suites completas Debug/Release `161/161` (86 unitarias + 75 integración) sin omitidas, `dotnet format` y scan de vulnerables en PASS.
- `git diff --numstat e96edefd6a0df37f238b7c5b9ff2fbf188eb069d...4cf41d055eb1df8f23d13178469c4ecf9a777e34 -- | ./scripts/check-human-lines.py` devolvió `1150` (estado `1`), registrado bajo `EX-INTEGRITY-2026-07-11`.
- Las unidades documentales `4de4f4e` (docs V2-T072), `6ff0535` (cierre de ledger) y `b16c5cb` no mueven `HUMAN_HEAD`.
- La cadena de conexión del walkthrough (`TrustServerCertificate=True` contra el certificado autofirmado del contenedor) fue una variable de entorno efímera, nunca configuración versionada; misma política que el fixture Testcontainers.
- V2-T068–V2-T075: PASS bajo la dispensa `EX-INTEGRITY-2026-07-11`; la puerta P0 habilita V2-T076 (P1).

## Evidencia técnica S13 modelo P1 — 2026-07-12

- Tres sub-gates humanos inmutables:
  - S13A: `SLICE_BASE=HUMAN_BASE=23dbe3efd730a4a89ba98f5e0b9e3bff6f03b437`, `HUMAN_HEAD=33c024154d579137524e3849c12d12de0b5c5b51`; `git diff --numstat ... | ./scripts/check-human-lines.py` devolvió `673` (estado `1`), registrado bajo `EX-INTEGRITY-2026-07-11`.
  - S13B: `SLICE_BASE=33c024154d579137524e3849c12d12de0b5c5b51`, `HUMAN_BASE=f884d720d48d8f921df57f39b379caeab1f564b5` (solo salida generada), `HUMAN_HEAD=95ea7e3e9295111d25806f6e2b17aa79ae25cb57`; el conteo excluye las cuatro rutas de `docs/generated-manifests/s13.txt` (incluido el Designer manual, mismo precedente que S07) y midió `814` (estado `1`), registrado bajo `EX-INTEGRITY-2026-07-11`.
  - S13C: `SLICE_BASE=HUMAN_BASE=95ea7e3e9295111d25806f6e2b17aa79ae25cb57`, `HUMAN_HEAD=40429aa88d24e4b06a5407d644cba0f4d0981534`; `git diff --numstat ... | ./scripts/check-human-lines.py` devolvió `115` (estado `0`), dentro del límite de 400, sin excepción.
- `docs/generated-manifests/s13.txt`: cuatro rutas exactas — migración generada `AddP1TeachingModel` (Designer + cs) y snapshot en `f884d72`, más el Designer manual de `AddP1DatabaseProtections` que viaja junto al commit humano `95ea7e3` (mismo precedente documentado en S07: el Designer de una migración manual es generado aunque acompañe al commit humano).
- Fixture: Testcontainers con imagen fijada `mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04`; `ConnectionStrings__InovaitTest` explícitamente ausente del entorno (`env | rg InovaitTest` sin salida).
- Targeted `Evidence=UT-ASSIGNMENT|Evidence=IT-ASSIGNMENT-PERIOD|Evidence=IT-AUDIT-UTC-P1|Evidence=IT-ROWVERSION-P1|Evidence=IT-SQL-SCRIPT-P1|Evidence=IT-SEED-P1|Evidence=IT-INDEXES-P1|Evidence=IT-LIST-SUBJECTS|Evidence=IT-CATALOG-MUTABILITY-S03`: 26/26 PASS — `UT-ASSIGNMENT` 9/9, `IT-ASSIGNMENT-PERIOD` 6/6, `IT-AUDIT-UTC-P1` 1/1, `IT-ROWVERSION-P1` 1/1, `IT-SQL-SCRIPT-P1` 2/2 (`P1DatabaseProtectionTests`+`SetupSqlParityTestsP1`), `IT-SEED-P1` 1/1, `IT-INDEXES-P1` 3/3, `IT-LIST-SUBJECTS` 2/2, `IT-CATALOG-MUTABILITY-S03` 1/1; `CreateTeachingAssignmentHandlerTests` (sin Evidence ID, filtrado por FQN) 11/11.
- `scripts/run-p0-tests.sh`: `P0 GATE PASSED: 37/37` con `151 tests` en total (`Priority=P0`, 80 unitarias + 71 integración) contra el mundo de 14 tablas; evidencia P0 intacta tras el refactor P1.
- Suites Debug y Release completas en `40429aa`: 197/197 en cada configuración (106 unitarias + 91 integración), cero fallos/omitidas.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos. Árbol de contratos sin diferencias/untracked y checksum `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS (tree byte-idéntico desde `8aa8097`). `openspec validate school-enrollment-management --strict`: PASS.
- OpenSpec strict/show/status y drift de 103 task lines: PASS, 87/103 completas y V2-T088 primera pendiente.
- V2-T076–V2-T087: PASS. S13A y S13B bajo dispensa `EX-INTEGRITY-2026-07-11`; S13C sin excepción. S13 cerrado; S14/V2-T088 abre.

## Evidencia técnica S14–S16 reportes P1 — 2026-07-12

- Gate combinado (los tres reportes P1 en un solo work unit): `SLICE_BASE=HUMAN_BASE=18cdc7909ce9bee6bfb97246d9f517caf85ab426`, `HUMAN_HEAD=c86cc845ce5ae1bf98e447c1debc61174c583f93`; `git diff --numstat 18cdc7909ce9bee6bfb97246d9f517caf85ab426...c86cc845ce5ae1bf98e447c1debc61174c583f93 -- | ./scripts/check-human-lines.py` devolvió `826` (estado `1`), registrado bajo la dispensa `EX-INTEGRITY-2026-07-11` autorizada por el usuario.
- Composición: `src/Inovait.Api/{Contracts/Reports.cs,Endpoints/ReportEndpoints.cs,Errors/ReportProblems.cs,Program.cs,Reads/ReportReadService.cs}` y `tests/Inovait.IntegrationTests/Api/{AgeDistributionTests.cs,TeacherCountsBySectorTests.cs,TopSchoolsTests.cs}`; sin manifest/salida generada.
- Targeted `Evidence=IT-RPT-AGE|Evidence=IT-RPT-SECTOR|Evidence=IT-RPT-TOP`: 12/12 PASS — `IT-RPT-AGE` 5/5 (buckets 3–7/8–12/≥13, exclusión total de <3, filtros acumulativos school/grade, asOfDate por defecto vía `TimeProvider`/explícito, 404 por referencias inexistentes, 400 por parámetro requerido ausente, 422 `as_of_date_invalid`), `IT-RPT-SECTOR` 4/4 (`COUNT(DISTINCT)` por sector, exclusión íntegra de contratos `Cancelled`, bordes de período inclusivos, período por defecto = fecha actual, zero-fill de ambos sectores), `IT-RPT-TOP` 3/3 (todos los empates en el máximo, orden `school.name`→`school.id`, `[]` para año sin inscripciones).
- `./scripts/run-p0-tests.sh` re-verificado sobre el árbol final: `P0 GATE PASSED: 37/37` (151 pruebas `Priority=P0`). `scripts/run-p1-tests.sh` no existía todavía como archivo en este rango (se agrega recién en S18, `015cc6a`); su corrida real de `P1 GATE PASSED: 13/13` queda registrada en la evidencia S18 siguiente.
- Suites completas verificadas contra el árbol final del cierre (HEAD `015cc6aa04d1e674c17e7785e4a3eb952629675e`): Debug 106 unitarias + 109 integración = 215/215; Release 106 + 109 = 215/215; cero fallidas/omitidas en ambas configuraciones.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores. `dotnet format --verify-no-changes`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos.
- V2-T088–V2-T096: PASS bajo dispensa `EX-INTEGRITY-2026-07-11`. S14, S15 y S16 cerrados en un único work unit; S17/V2-T097 queda habilitado.

## Evidencia técnica S17 historial — 2026-07-12

- Gate: `SLICE_BASE=HUMAN_BASE=c86cc845ce5ae1bf98e447c1debc61174c583f93`, `HUMAN_HEAD=e8d404648fdd9a257b50abb8b4a3cdbb94f14b93`; `git diff --numstat c86cc845ce5ae1bf98e447c1debc61174c583f93...e8d404648fdd9a257b50abb8b4a3cdbb94f14b93 -- | ./scripts/check-human-lines.py` devolvió `460` (estado `1`), registrado bajo la dispensa `EX-INTEGRITY-2026-07-11` autorizada por el usuario.
- Composición: `src/Inovait.Api/{Contracts/StudentHistory.cs,Endpoints/StudentHistoryEndpoints.cs,Errors/StudentHistoryProblems.cs,Program.cs,Reads/StudentHistoryReadService.cs}` y `tests/Inovait.IntegrationTests/Api/StudentHistoryTests.cs`; sin manifest/salida generada.
- Targeted `Evidence=IT-HISTORY`: 4/4 PASS — resolución `DocumentType.Code` + número normalizado → `Person` → `Student` con 404 `student_not_found` si no existe rol Student; inscripciones ordenadas `academicYear.StartDate DESC, enrollment.Id ASC`; asignaciones por ClassGroup ordenadas `subject.Name, teacher.LastNames, teacher.FirstNames, assignment.Id` con weekdays ordenados por `Weekday`; persona dual Student+Teacher sin bloquear su propia historia; historial vacío válido (`enrollments: []`).
- `./scripts/run-p0-tests.sh` re-verificado sobre el árbol final: `P0 GATE PASSED: 37/37` (151 pruebas). `scripts/run-p1-tests.sh` todavía no existía en este rango (se agrega en S18, `015cc6a`); su corrida real de 13/13 queda en la evidencia S18 siguiente.
- Suites completas verificadas contra el árbol final del cierre (HEAD `015cc6aa04d1e674c17e7785e4a3eb952629675e`): Debug y Release 215/215 en cada configuración (106 unitarias + 109 integración), cero fallidas/omitidas.
- `dotnet restore`, builds Debug/Release (0 warnings, 0 errores), `dotnet format --verify-no-changes` y `dotnet list package --vulnerable --include-transitive` (0 vulnerables en 5 proyectos): PASS.
- V2-T097–V2-T099: PASS bajo dispensa `EX-INTEGRITY-2026-07-11`. S17 cerrado; S18/V2-T100 queda habilitado.

## Evidencia técnica S18 cierre — 2026-07-12

- Gate: `SLICE_BASE=HUMAN_BASE=e8d404648fdd9a257b50abb8b4a3cdbb94f14b93`, `HUMAN_HEAD=015cc6aa04d1e674c17e7785e4a3eb952629675e`; `git diff --numstat e8d404648fdd9a257b50abb8b4a3cdbb94f14b93...015cc6aa04d1e674c17e7785e4a3eb952629675e -- | ./scripts/check-human-lines.py` devolvió `375` (estado `0`), dentro del límite de 400, sin excepción.
- Composición: `tests/Inovait.IntegrationTests/Api/P1OpenApiTests.cs` y `scripts/run-p1-tests.sh`; sin manifest/salida generada.
- `dotnet restore`: PASS. Builds Debug y Release: PASS, cero warnings y cero errores.
- Suites completas Debug y Release: 215/215 en cada configuración (106 unitarias + 109 integración), cero fallidas/omitidas.
- Targeted `Evidence=IT-RPT-AGE|Evidence=IT-RPT-SECTOR|Evidence=IT-RPT-TOP|Evidence=IT-HISTORY|Evidence=IT-OPENAPI|Evidence=IT-NORMAL-FORMS`: 19/19 PASS — `IT-RPT-AGE` 5/5, `IT-RPT-SECTOR` 4/4, `IT-RPT-TOP` 3/3, `IT-HISTORY` 4/4, `IT-OPENAPI` 2/2, `IT-NORMAL-FORMS` 1/1.
- `dotnet format --verify-no-changes`: PASS. `dotnet list package --vulnerable --include-transitive`: PASS, cero paquetes vulnerables en cinco proyectos.
- `./scripts/run-p0-tests.sh`: `P0 GATE PASSED: 37/37` (151 pruebas `Priority=P0`, 80 unitarias + 71 integración).
- `./scripts/run-p1-tests.sh`: parsed 13 evidence IDs del manifest P1, sin duplicados, discovery `list-tests` por proyecto, `P1 GATE PASSED: 13/13` (54 pruebas: 20 unitarias + 34 integración).
- Árbol de contratos sin diferencias/untracked (`git status --porcelain -- specs/001-school-enrollment-management/contracts` vacío) y checksum combinado `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`: PASS.
- `openspec validate --all --strict`: PASS (1 passed, 0 failed).
- `ConnectionStrings__InovaitTest`: ausente del entorno (`env | rg -i ConnectionStrings` sin salida).
- OpenSpec strict/status y drift de 103 task lines: PASS, 103/103 completas y ninguna pendiente.
- V2-T100–V2-T103: PASS, S18 cerrado sin excepción; proyecto `production-model-v2.0.0` completo, 103/103 tareas. El empaquetado/paquete final fuera del repositorio que menciona V2-T103 NO se ejecuta en este cierre — requiere autorización explícita del usuario, no otorgada.

## Notas operativas

- No usar secretos dentro de repositorio.
- No asumir cobertura previa de dominio: antes de `S02` las pruebas de integración son solo smoke.
- No modificar `specs/.../contracts` durante ejecución.
- El bundle YAML canónico es la fuente contractual. S01 no registra generación OpenAPI runtime porque `Microsoft.AspNetCore.OpenApi` `10.0.9` arrastra una dependencia con vulnerabilidad alta.
