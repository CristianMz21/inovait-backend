---
description: "Tareas P0-first para el modelo de producción y capacidades escolares"
---

# Tareas: gestión escolar con modelo de producción

**Estado**: planificación y S01 scaffold. Este documento publica el task set estable `production-model-v2.0.0`, con IDs `V2-T001`–`V2-T103`. Existen solución, proyectos y smoke tests; S02 no comenzó y todavía no existen entidades, migraciones ni `database/setup.sql`.

Los IDs `T001`–`T076` del baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e` pertenecen exclusivamente al task set v1 y **no se pueden usar para ejecución actual**. Sus reemplazos, retiros y descomposiciones están en [task-id-supersession.md](../../docs/task-id-supersession.md). La semántica histórica no se considera estable por coincidencia numérica.

## Review Workload Forecast

| Campo | P0 | P1/total |
| --- | ---: | ---: |
| Líneas humanas estimadas | 3.400–5.000 | +1.700–2.500 |
| Scaffold/migraciones generadas aisladas | 900–1.600 | +250–450 |
| Riesgo de superar 400 por cambio monolítico | Alto | Alto |

La estrategia resuelta es `stacked-to-main`: cada slice futuro apunta a `main` después de integrar su dependencia. Cada unidad humana queda en ≤400 líneas; scaffold, lockfiles y migraciones generadas se revisan aislados. `EX-PLAN-2026-07-10` es una excepción aprobada solo para el work unit separado del churn documental previo de planificación; no cubre S01, su registro posterior de evidencia ni slices futuros.

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

```text
main ← S01 ←merge← S02 ←merge← ... ←merge← S12/P0 gate ←merge← S13...S18
       cada PR nuevo vuelve a apuntar a main actualizado; nunca incluye slices no integrados
```

| Slice | Resultado autónomo | Humano estimado | Generado aislado |
| --- | --- | ---: | ---: |
| S01 | scaffold y runner base | 300–380 (reconstrucción actual: 360) | 209 (template reproducido) |
| S02 | normalización, auditoría, concurrencia y fixture SQL | 280–400 | 0 |
| S03 | catálogos P0, singleton e inmutabilidad | 320–400 | 0 |
| S04 | Person y roles duales | 280–390 | 0 |
| S05 | ClassGroup/Enrollment e índices | 300–400 | 0 |
| S06 | TeacherContract, cancelación y solapamiento | 320–400 | 0 |
| S07 | migración `InitialP0ProductionModel` | 0–80 | 300–650 |
| S08 | host y catálogos P0 | 280–400 | 0 |
| S09 | US1 inscripción atómica | 320–400 | 0 |
| S10 | US2 consulta de inscritos | 240–340 | 0 |
| S11 | US3 contratos multiescuela | 320–400 | 0 |
| S12 | setup SQL, paridad, runner y puerta P0 | 300–400 | SQL revisado separado |
| S13 | modelo/migración P1 | 300–400 | 200–400 |
| S14–S17 | una capacidad P1 por slice | 260–400 cada uno | 0 |
| S18 | hardening y entrega | 260–400 | 0 |

### Gate humano obligatorio por slice

Cada slice fija como SHAs inmutables `SLICE_BASE` (commit previo al slice), `HUMAN_BASE` (último commit exclusivamente generado, o `SLICE_BASE` si no existe) y `HUMAN_HEAD` (commit final de personalización humana del slice). `GENERATED_MANIFEST` enumera, una ruta exacta por línea, únicamente la salida de template/CLI presente en `SLICE_BASE..HUMAN_BASE`; puede incluir una plantilla que luego se modifique o elimine, porque ese delta pertenece a `HUMAN_BASE...HUMAN_HEAD`. Antes de merge se ejecuta, sin estimaciones manuales:

```bash
git diff --name-only "$SLICE_BASE" "$HUMAN_BASE" | LC_ALL=C sort > /tmp/inovait-generated.actual
LC_ALL=C sort "$GENERATED_MANIFEST" > /tmp/inovait-generated.expected
diff -u /tmp/inovait-generated.expected /tmp/inovait-generated.actual
git diff --numstat "$HUMAN_BASE"..."$HUMAN_HEAD" -- | ./scripts/check-human-lines.py
```

El primer `diff` prueba que solo las rutas generadas declaradas quedaron fuera del conteo humano. El segundo comando consume `git diff --numstat` y cuenta `additions + deletions` de la personalización entre los dos SHAs fijados, incluidas configuración, SQL/migrations manuales, pruebas y documentación del propio slice. Un commit documental posterior no mueve `HUMAN_HEAD` ni invalida evidencia ya obtenida. Un resultado mayor a 400 bloquea el merge y el slice siguiente hasta dividir el trabajo o registrar un `size:exception` explícito. `EX-PLAN-2026-07-10` pertenece exclusivamente al work unit documental previo de planificación y no se hereda por S01 ni por su registro posterior de evidencia.

| Slice | Gate pre-merge |
| --- | --- |
| S01–S06 | `V2-T010`, `V2-T019`, `V2-T026`, `V2-T031`, `V2-T037`, `V2-T043` |
| S07–S12 | `V2-T046`, `V2-T051`, `V2-T057`, `V2-T061`, `V2-T067`, `V2-T075` |
| S13–S18 | `V2-T087`, `V2-T090`, `V2-T093`, `V2-T096`, `V2-T099`, `V2-T103` |

Fallbacks definidos antes de apply, cada sub-slice sujeto al mismo gate:

- **S03**: S03A `V2-T020`–`V2-T023`, gate en V2-T023 (cinco tablas de catálogo, checks y save behavior); S03B `V2-T024`–`V2-T026`, gate en V2-T026 (seed, startup check y pruebas parciales ejecutables sin acreditar triggers/permisos todavía).
- **S07**: S07A `V2-T044`, gate en V2-T044 (migración generada y manifest); S07B `V2-T045`, gate en V2-T045 (protecciones/permisos manuales); S07C `V2-T046`, gate en V2-T046 (apply/revert y evidencia P0 completa de 11 tablas, triggers, singleton y permisos).
- **S12**: S12A `V2-T068`, gate en V2-T068 (setup transaccional); S12B `V2-T069`–`V2-T070`, gate en V2-T070 (paridad e índices/permisos); S12C `V2-T071`–`V2-T075`, gate en V2-T075 (runner, docs y walkthrough).
- **S13**: S13A `V2-T076`–`V2-T080`, gate en V2-T080 (modelo/reglas P1); S13B `V2-T081`–`V2-T083`, gate en V2-T083 (migraciones y setup/paridad); S13C `V2-T084`–`V2-T087`, gate en V2-T087 (`listSubjects` end-to-end).

## Fase 0: decisiones de planificación

- [x] V2-T001 Verificar en solo lectura la rama `feat/production-data-model` y ausencia de `src/`, `tests/`, solución, proyectos, migraciones y `database/setup.sql` en la raíz; **Dep.** ninguna; **Criterio** cero archivos de implementación.
- [x] V2-T002 [REQ-050] Conservar el baseline OpenAPI `1223630ab99bf1bfaa4f5919fccf5ff539379c8e`: probar primero igualdad del árbol contractual y ausencia de archivos untracked, y después el checksum secundario `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a` con el orden canónico de `quickstart.md`; **Dep.** V2-T001.
- [x] V2-T003 [REQ-051,REQ-063] Resolver `stacked-to-main`, gate humano por slice, presupuesto de 400 líneas, fallbacks S03/S07/S12/S13 y aislamiento de scaffold/lockfiles/migraciones en `plan.md` y este archivo; **Dep.** V2-T001; **Criterio** ningún `size:exception` aprobado.
- [x] V2-T004 Crear `docs/evaluator-execution.md` con prerequisitos, mapa de slices, `SLICE_BASE`/`HUMAN_BASE`/`HUMAN_HEAD`/manifest/gate por slice, evidencia P0, rollback y walkthroughs sin afirmar ejecuciones; **Dep.** V2-T001–V2-T003.

## Fase 1 / S01: primer slice autónomo — scaffold

**Inicio**: repositorio solo con planificación. **Fin**: tres proyectos de producción y dos de pruebas restauran, compilan y ejecutan runner base; no hay entidades ni migraciones. **Rollback**: retirar únicamente la solución creada.

- [x] V2-T005 Crear por CLI `global.json`, `Inovait.slnx`, `src/Inovait.{Api,Core,Infrastructure}/` y `tests/Inovait.{UnitTests,IntegrationTests}/`; **Dep.** V2-T004; **Criterio** salida generada aislada y manifest exacto.
- [x] V2-T006 Configurar referencias sin ciclos, C# 14, nullable y warnings-as-errors en `Inovait.slnx` y `*.csproj`; **Dep.** V2-T005.
- [x] V2-T007 Agregar solo dependencias aprobadas y versiones fijadas en los cinco `*.csproj`; **Dep.** V2-T005; **Criterio** lockfiles, si se generan, quedan antes de `HUMAN_BASE` y en el manifest.
- [x] V2-T008 Configurar `.editorconfig`, `.gitignore` y settings sin secretos en la raíz y `src/Inovait.Api/appsettings*.json`; **Dep.** V2-T005.
- [x] V2-T009 Crear smoke tests base en `tests/Inovait.UnitTests/SmokeTests.cs` y `tests/Inovait.IntegrationTests/SmokeTests.cs`; **Dep.** V2-T006–V2-T008.
- [x] V2-T010 Validar S01 con restore/build/format/test, ejecutar el gate humano obligatorio y registrar `SLICE_BASE`, `HUMAN_BASE`, `HUMAN_HEAD`, manifest y conteo en `docs/evaluator-execution.md`; **Dep.** V2-T009; **Estado** PASS con `SLICE_BASE=757b552ca3215371c0006d39bf0d0a14fabfdc11`, `HUMAN_BASE=dbcdaf7628c1e4dffd89a7c92f4513e2c4c1df47`, `HUMAN_HEAD=5dc32432d489eb342fed0221ff6b545036727b75`, manifest exacto y salida del gate `360`; restore, build/test Debug+Release, format, vulnerabilidades, diff y contrato OpenAPI en PASS; **Criterio** cumplido con conteo ≤400 y sin excepción S01.

## Fase 2 / S02: convenciones de persistencia

- [ ] V2-T011 [P] [REQ-055] Escribir `UT-TEXT-NORMALIZATION` en `tests/Inovait.UnitTests/Domain/TextNormalizerTests.cs`; **Dep.** V2-T010; **Criterio** NFC; whitespace Unicode exterior; tabs/newlines y whitespace interno Unicode colapsados a un espacio; valor requerido whitespace-only rechazado tras normalizar; diacríticos/puntuación preservados e idempotencia.
- [ ] V2-T012 [P] [REQ-057] Escribir `UT-AUDIT-INTERCEPTOR` en `tests/Inovait.UnitTests/Domain/AuditPolicyTests.cs` con dobles de prueba disponibles en S02; **Dep.** V2-T010; **Criterio** comportamiento genérico de reloj, alta/modificación y preservación de creación, sin nombrar entidades de producción ni acreditar `IT-AUDIT-UTC-*`/`IT-ROWVERSION-*` antes de que existan sus tablas.
- [ ] V2-T013 [REQ-055,REQ-057] Crear contratos comunes en `src/Inovait.Core/Domain/Common/{ITextNormalizer,IAuditableEntity}.cs`; **Dep.** V2-T011,V2-T012; **Criterio** el contrato es genérico y la pertenencia exacta de entidades se acredita por etapas en V2-T046 y V2-T087.
- [ ] V2-T014 [REQ-055] Implementar `TextNormalizationInterceptor` en `src/Inovait.Infrastructure/Persistence/Interceptors/TextNormalizationInterceptor.cs`; **Dep.** V2-T013; **Criterio** normaliza/rechaza whitespace Unicode antes de persistir, sin atribuir esa cobertura al CHECK SQL.
- [ ] V2-T015 [REQ-057] Implementar `AuditSaveChangesInterceptor` con `TimeProvider` sobre `IAuditableEntity`, preservando `CreatedAtUtc` y actualizando `UpdatedAtUtc` solo al modificar; **Dep.** V2-T013; **Criterio** S02 prueba comportamiento genérico, no una lista de entidades todavía inexistentes.
- [ ] V2-T016 Crear fixture Testcontainers y fallback externo aislado en `tests/Inovait.IntegrationTests/Infrastructure/SqlServerFixture.cs`; **Dep.** V2-T007,V2-T010.
- [ ] V2-T017 Crear `InovaitDbContext` y registro de interceptors/configuraciones en `src/Inovait.Infrastructure/Persistence/InovaitDbContext.cs` y `DependencyInjection.cs`; **Dep.** V2-T014–V2-T016.
- [ ] V2-T018 [REQ-057] Escribir el harness reutilizable de metadatos/concurrencia en `tests/Inovait.IntegrationTests/Persistence/AuditConcurrencyTests.cs` usando únicamente un modelo probe propiedad del test disponible en S02; **Dep.** V2-T017; **Criterio** prueba defaults, check, update real y rowversion del mecanismo genérico, pero no exige tablas de producción ni produce evidencia `IT-AUDIT-UTC-*`/`IT-ROWVERSION-*`.
- [ ] V2-T019 Validar S02 con unitarias y fixture SQL Server, y ejecutar el gate humano obligatorio; **Dep.** V2-T018; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 3 / S03: catálogos P0 y singleton

- [ ] V2-T020 [P] [REQ-053,REQ-056,REQ-058] Escribir `IT-CATALOG-SCHEMA-S03`, `IT-CATALOG-MUTABILITY-S03` e `IT-CATALOG-SINGLETON-S03` en `tests/Inovait.IntegrationTests/Persistence/CatalogModelTests.cs`; **Dep.** V2-T019; **Criterio** el fixture materializa únicamente `School`, `AcademicYear`, `AcademicConfiguration`, `Grade` y `DocumentType`; prueba schema/keys/checks/seed, save behavior de Code/Sector con cambios de Name permitidos y PK+CHECK+fail-fast del singleton, sin exigir las otras seis tablas P0, triggers SQL, rol runtime ni permisos.
- [ ] V2-T021 [REQ-053,REQ-056–058] Crear `School`, `AcademicYear`, `AcademicConfiguration`, `Grade` y `DocumentType` en `src/Inovait.Core/Domain/Catalogs/`; **Dep.** V2-T020.
- [ ] V2-T022 [REQ-053,REQ-055–058] Configurar las cinco tablas, collation y FK en `src/Inovait.Infrastructure/Persistence/Configurations/`; **Dep.** V2-T021; **Criterio** `School`, `AcademicYear` y `Grade` reciben auditoría/check/rowversion; `AcademicConfiguration` y `DocumentType` no reciben auditoría genérica ni rowversion; cada required string conserva `LEN(TRIM([Column])) > 0`, limitado a vacío/espacios ordinarios directos.
- [ ] V2-T023 [REQ-056] Configurar save behavior y definir SQL de `TR_School_ProtectStableValues`, `TR_AcademicYear_ProtectCode` y `TR_Grade_ProtectCode` para el migration manual de protecciones; **Dep.** V2-T022; **Criterio condicional S03A** ejecutar gate ≤400 antes de merge si se activa el fallback.
- [ ] V2-T024 [REQ-056,REQ-058] Configurar seed Id=1 y definir rol database `[inovait_runtime]`, permisos y `TR_AcademicConfiguration_PreventDelete` para `ProductionCatalogSeed.cs` y el migration manual; **Dep.** V2-T022; **Criterio** `GRANT SELECT ON OBJECT::catalog.DocumentType TO [inovait_runtime]` y `DENY INSERT, UPDATE, DELETE ON OBJECT::catalog.DocumentType TO [inovait_runtime]` explícitos, además de restricciones del singleton.
- [ ] V2-T025 [REQ-058] Implementar comprobación fail-fast del singleton en `src/Inovait.Infrastructure/Persistence/AcademicConfigurationStartupCheck.cs`; **Dep.** V2-T024.
- [ ] V2-T026 Ejecutar contra SQL Server solo `IT-CATALOG-SCHEMA-S03`, `IT-CATALOG-MUTABILITY-S03` e `IT-CATALOG-SINGLETON-S03`, incluidos cambios permitidos de `Name`, y ejecutar el gate humano obligatorio; **Dep.** V2-T023–V2-T025; **Criterio** las tres evidencias verdes usan solo las cinco tablas disponibles en S03 y no acreditan `IT-SCHEMAS-P0`, `IT-IMMUTABILITY`, `IT-SINGLETON` ni `IT-REFERENCE-PERMISSIONS`; gate ≤400 o activar S03A/S03B, sin excepción.

## Fase 4 / S04: Person y roles independientes

- [ ] V2-T027 [P] [REQ-001–006,REQ-054,REQ-055] Escribir `UT-IDENTITY` en `tests/Inovait.UnitTests/Domain/IdentityResolverTests.cs` y `IT-PERSON-COLLATION`, `IT-PERSON-DUAL-ROLE`, `IT-TEXT-CHECKS` en `tests/Inovait.IntegrationTests/Persistence/PersonRoleTests.cs`; **Dep.** V2-T026; **Criterio** la unitaria cubre igualdad conceptual tipo+número, conflicto de nombres/nacimiento y rol dual; SQL directo rechaza `''` y solo U+0020 mediante `LEN(TRIM)>0`, mientras una prueba negativa fija que tab/newline-only puede superar ese CHECK aislado y V2-T011/V2-T014 lo rechaza en aplicación.
- [ ] V2-T028 [REQ-054,REQ-057] Crear `Person`, `Student` y `Teacher` en `src/Inovait.Core/Domain/People/`; **Dep.** V2-T027; **Criterio** `Person` y `Teacher` son auditables/concurrentes; `Student` no tiene auditoría genérica ni rowversion.
- [ ] V2-T029 [REQ-054,REQ-055,REQ-057] Crear configuraciones PK+FK independientes, UQ CI_AS, checks e índice de nombres en `src/Inovait.Infrastructure/Persistence/Configurations/{Person,Student,Teacher}Configuration.cs`; **Dep.** V2-T028; **Criterio** auditoría/check/rowversion solo en `Person` y `Teacher`, nunca en `Student`.
- [ ] V2-T030 [REQ-001–005,REQ-054,REQ-055] Implementar resolución `DocumentType.Code`/`Person` y conflicto de identidad en `src/Inovait.Core/Features/Enrollments/IdentityResolver.cs` y puerto específico; **Dep.** V2-T029.
- [ ] V2-T031 Validar S04 con NFC/case/acento, ambas capas de whitespace, datos discrepantes y una persona con ambos roles, y ejecutar el gate humano obligatorio; **Dep.** V2-T030; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 5 / S05: ClassGroup y Enrollment

- [ ] V2-T032 [P] [US1] [REQ-007–011,REQ-052,REQ-061] Escribir `IT-ENR-ANNUAL`, `IT-NORMAL-FORMS` y assertions parciales de índices Enrollment/ClassGroup en `tests/Inovait.IntegrationTests/Persistence/EnrollmentModelTests.cs`; **Dep.** V2-T031; **Criterio** keys/includes disponibles no declaran `Id` en INCLUDE y comprueban su disponibilidad implícita por PK clustered; el ID canónico `IT-INDEXES-P0` se produce una sola vez en V2-T070 con el modelo P0 completo.
- [ ] V2-T033 [US1] [REQ-007–011] Crear `ClassGroup` y `Enrollment` en `src/Inovait.Core/Domain/Academics/`; **Dep.** V2-T032; **Criterio** `ClassGroup` es auditable/concurrente y `Enrollment` solo tiene `CreatedAtUtc`.
- [ ] V2-T034 [US1] [REQ-008,REQ-009,REQ-052,REQ-057] Configurar `UQ_ClassGroup_Id_AcademicYear_ForEnrollment`, FK compuesto y UNIQUE anual en `ClassGroupConfiguration.cs` y `EnrollmentConfiguration.cs`; **Dep.** V2-T033; **Criterio** auditoría/check/rowversion solo para `ClassGroup`; `Enrollment` solo default de creación.
- [ ] V2-T035 [US1] [REQ-061] Configurar `IX_ClassGroup_AcademicYearId_GradeId_SchoolId` INCLUDE `(Code)` e `IX_Enrollment_ClassGroupId_StudentPersonId` INCLUDE `(AcademicYearId, CreatedAtUtc)`; **Dep.** V2-T034; **Criterio** `Id` no se declara en INCLUDE mientras las PK sigan clustered.
- [ ] V2-T036 [US1] [REQ-003,REQ-007–011] Definir command/puertos/transacción de inscripción en `src/Inovait.Core/Features/Enrollments/`; **Dep.** V2-T034.
- [ ] V2-T037 Validar S05: año divergente falla, no existen School/Grade en Enrollment, el índice coincide en key/include y ejecutar el gate humano obligatorio; **Dep.** V2-T035,V2-T036; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 6 / S06: TeacherContract

- [ ] V2-T038 [P] [US3] [REQ-018–026,REQ-059] Escribir `UT-CONTRACT-CANCELLATION`, `UT-CONTRACT-STATUS` y `UT-CONTRACT-OVERLAP` en `tests/Inovait.UnitTests/Domain/TeacherContractTests.cs`; **Dep.** V2-T031.
- [ ] V2-T039 [P] [US3] [REQ-022,REQ-059,REQ-061] Escribir `IT-CON-CANCELLATION`, `IT-CON-OVERLAP` e índice contractual en `tests/Inovait.IntegrationTests/Persistence/TeacherContractModelTests.cs`; **Dep.** V2-T031; **Criterio** metadata no declara `Id` en INCLUDE y sí prueba su disponibilidad implícita por PK clustered.
- [ ] V2-T040 [US3] [REQ-018–023,REQ-059] Crear `TeacherContract` y transición Confirmed→Cancelled en `src/Inovait.Core/Domain/Staff/TeacherContract.cs`; **Dep.** V2-T038.
- [ ] V2-T041 [US3] [REQ-020–023,REQ-057,REQ-059,REQ-061] Configurar checks, UQ exacto, auditoría/check/rowversion de `TeacherContract` y dos índices cubrientes sin `Id` en INCLUDE en `TeacherContractConfiguration.cs`; **Dep.** V2-T039,V2-T040.
- [ ] V2-T042 [US3] [REQ-022–024] Implementar puertos y transacción `Serializable` en `src/Inovait.Core/Features/TeacherContracts/` y `src/Inovait.Infrastructure/Features/TeacherContracts/`; **Dep.** V2-T041.
- [ ] V2-T043 Validar S06 con estados all-or-none, razón whitespace, fechas y carrera de dos conexiones, y ejecutar el gate humano obligatorio; **Dep.** V2-T042; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 7 / S07: migración P0 generada y aislada

- [ ] V2-T044 [REQ-053–059,REQ-061,REQ-062] Generar `InitialP0ProductionModel` en `src/Inovait.Infrastructure/Persistence/Migrations/` sin editar a mano el scaffold; **Dep.** V2-T026,V2-T031,V2-T037,V2-T043; **Criterio** diff generado aislado, manifest exacto y gate S07A si se activa el fallback.
- [ ] V2-T045 [REQ-056,REQ-058,REQ-062] Crear el migration manual `AddP0DatabaseProtections` solo con cuatro triggers/permisos, incluido `DENY INSERT, UPDATE, DELETE` de `catalog.DocumentType` al rol runtime, y revisar la cadena contra 11 tablas, constraints, índices, seeds y permisos; **Dep.** V2-T044; **Criterio condicional S07B** gate ≤400 antes de merge.
- [ ] V2-T046 Aplicar/revertir/aplicar `InitialP0ProductionModel` + `AddP0DatabaseProtections` sobre SQL Server limpio; producir y ejecutar los ID completos `IT-SCHEMAS-P0`, `IT-IMMUTABILITY`, `IT-SINGLETON`, `IT-REFERENCE-PERMISSIONS` en `tests/Inovait.IntegrationTests/Persistence/P0DatabaseProtectionTests.cs`, completar y ejecutar `IT-AUDIT-UTC-P0`/`IT-ROWVERSION-P0` en `AuditConcurrencyTests.cs`, y ejecutar el gate humano obligatorio; **Dep.** V2-T045; **Criterio de disponibilidad** las 11 tablas, cuatro triggers, rol, GRANT/DENY y seed existen antes de ejecutar; **Criterio positivo** auditoría/check/update real/rowversion exactamente en `School`, `AcademicYear`, `Grade`, `ClassGroup`, `Person`, `Teacher`, `TeacherContract`; **CreatedAt-only** `Enrollment`; **Criterio negativo** `DocumentType`, `Student`, `AcademicConfiguration` sin auditoría genérica, check cronológico ni rowversion; **Gate** ≤400 o activar S07A/S07B/S07C, sin excepción.

## Fase 8 / S08: host y catálogos P0

- [ ] V2-T047 [P] [US1–US3] Escribir `IT-CATALOGS` en `tests/Inovait.IntegrationTests/Api/CatalogEndpointsTests.cs`; **Dep.** V2-T046; **Criterio** cubre los cinco operationIds P0, filtros, órdenes canónicos, 400/404 de catálogo y `200 []`; la evidencia transversal `IT-PROBLEMS` espera hasta V2-T062, cuando pueden existir los 10 endpoints P0.
- [ ] V2-T048 Registrar DbContext, interceptors, startup check, Controllers, CORS, OpenAPI y ProblemDetails en `src/Inovait.Api/Program.cs`; **Dep.** V2-T047.
- [ ] V2-T049 Implementar proyecciones no tracking de School, Grade, AcademicYear, ClassGroup y Teacher vía Person en `src/Inovait.Infrastructure/Features/Catalogs/`; **Dep.** V2-T048.
- [ ] V2-T050 Crear DTOs/controllers de los cinco catálogos P0 en `src/Inovait.Api/Features/Catalogs/`; **Dep.** V2-T049; **Criterio** DocumentTypeId no se expone y JSON no cambia.
- [ ] V2-T051 Validar S08 con cinco operationIds, órdenes y contexto existente sin grupos=`200 []`, y ejecutar el gate humano obligatorio; **Dep.** V2-T050; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 9 / S09: US1 inscripción atómica

- [ ] V2-T052 [P] [US1] [SCN-001–006] Escribir `IT-ENR-CREATE`, `IT-ENR-IDENTITY` e `IT-ENR-CONTEXT` en `tests/Inovait.IntegrationTests/Api/CreateEnrollmentTests.cs`; **Dep.** V2-T051; **Criterio** cubre 201 y alta completa, reutilización equivalente, conflicto 409 sin modificación, fecha futura, 404 de referencia y 422 solo para grupo ajeno al contexto, sin persistencia parcial.
- [ ] V2-T053 [P] [US1] [SCN-004,SCN-007] Escribir `IT-ENR-ATOMIC` en `tests/Inovait.IntegrationTests/Api/EnrollmentAtomicityTests.cs` con rollback y carrera anual; **Dep.** V2-T051.
- [ ] V2-T054 [US1] [REQ-001–011,REQ-054,REQ-055] Implementar servicio Person/Student/Enrollment todo-o-nada en Core e Infrastructure; **Dep.** V2-T052,V2-T053.
- [ ] V2-T055 [US1] Traducir colisiones de identidad/año y concurrencia a `409 ProblemDetails` en `src/Inovait.Api/Errors/`; **Dep.** V2-T054.
- [ ] V2-T056 [US1] Crear request/response y `createEnrollment` en `src/Inovait.Api/Features/Enrollments/`; **Dep.** V2-T055.
- [ ] V2-T057 Validar S09 con SCN-001–007, ausencia de persistencia parcial y gate humano obligatorio; **Dep.** V2-T056; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 10 / S10: US2 consulta de inscritos

- [ ] V2-T058 [US2] [REQ-012–017] [SCN-008–012] Escribir `UT-AGE` en `tests/Inovait.UnitTests/Domain/AgeCalculatorTests.cs` e `IT-ENR-FILTER` en `tests/Inovait.IntegrationTests/Api/ListEnrollmentsTests.cs`; **Dep.** V2-T057; **Criterio** años cumplidos en límites 3/7/8/12/13, cumpleaños y fecha anterior al nacimiento, además de filtros conjuntos y orden HTTP.
- [ ] V2-T059 [US2] [REQ-012–017,REQ-061] Implementar query desde ClassGroup→Enrollment→Student→Person/DocumentType en `src/Inovait.Infrastructure/Features/Enrollments/ListEnrollmentsQuery.cs`; **Dep.** V2-T058.
- [ ] V2-T060 [US2] Crear DTO y `listEnrollments` en `src/Inovait.Api/Features/Enrollments/`; **Dep.** V2-T059.
- [ ] V2-T061 Validar S10: filtros conjuntos, edad, vacío, orden, proyección documental estable y gate humano obligatorio; **Dep.** V2-T060; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 11 / S11: US3 contratos multiescuela

- [ ] V2-T062 [P] [US3] [SCN-013–019] Escribir `IT-CON-MULTI`, `IT-CON-DATES` e `IT-CON-LIST` en `tests/Inovait.IntegrationTests/Api/TeacherContractEndpointsTests.cs`, y `IT-PROBLEMS`/`IT-OPENAPI-P0` en `tests/Inovait.IntegrationTests/Api/P0OpenApiTests.cs`; **Dep.** V2-T061; **Criterio** cubre creación multiescuela atómica, rango/abierto/referencias, listas por docente y escuela con estado/orden, 400/404/409/422 sin detalles internos, bundle canónico de 15 operationIds intacto y exactamente 10 operationIds runtime P0.
- [ ] V2-T063 [P] [US3] [REQ-022–024] Escribir `TeacherContractConcurrencyTests.cs`; **Dep.** V2-T061.
- [ ] V2-T064 [US3] Implementar creación/lista por docente y escuela sobre los puertos de S06 en Infrastructure; **Dep.** V2-T062,V2-T063.
- [ ] V2-T065 [US3] Crear DTOs y tres operaciones contractuales en `src/Inovait.Api/Features/TeacherContracts/`; **Dep.** V2-T064.
- [ ] V2-T066 [US3] Mapear rowversion, solapamiento, cancelación y referencias a ProblemDetails sin filtrar SQL; **Dep.** V2-T065.
- [ ] V2-T067 Validar S11 con SCN-013–019, listas cubiertas, transacción multiescuela y gate humano obligatorio; **Dep.** V2-T066; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 12 / S12: setup, paridad y puerta P0

- [ ] V2-T068 [REQ-045,REQ-049,REQ-053–059,REQ-061,REQ-062] Crear `database/setup.sql` P0 con 11 tablas, schemas, objetos, seeds, singleton y permisos en transacción; **Dep.** V2-T046,V2-T067; **Criterio** crea `[inovait_runtime]` sin login e incluye GRANT SELECT y DENY INSERT/UPDATE/DELETE exactos sobre `catalog.DocumentType`, equivalentes a migración; gate S12A si se activa el fallback.
- [ ] V2-T069 [REQ-049,REQ-056,REQ-062] Escribir `IT-SQL-SCRIPT` e `IT-SEED-P0` en `tests/Inovait.IntegrationTests/Persistence/SetupSqlParityTests.cs`, comparando catálogos `sys.*` y seeds de migración/setup; **Dep.** V2-T068; **Criterio** ambas rutas producen 11 tablas, catálogos con códigos, `DocumentType`, singleton coherente y los mismos GRANT/DENY, con SELECT runtime permitido e INSERT/UPDATE/DELETE denegados.
- [ ] V2-T070 [REQ-061] Producir una sola vez `IT-INDEXES-P0` al completar las pruebas de key/include/filter y FK support del modelo de 11 tablas en `RelationalIndexTests.cs`; **Dep.** V2-T068; **Criterio** ausencia de `Id` en INCLUDE para índices nonclustered, disponibilidad implícita por PK clustered, alerta documental si cambia clustering y gate S12B si se activa el fallback.
- [ ] V2-T071 Escribir `scripts/run-p0-tests.sh` para cargar los 37 IDs del manifest P0 canónico y explícito de `docs/testing-strategy.md`, rechazar IDs repetidos en el manifest y, para cada ID, exigir al menos una prueba descubierta por `dotnet test --list-tests --filter "Priority=P0&Evidence=<ID>"`; solo tras verificar todos los IDs ejecuta la suite completa `Priority=P0`; **Dep.** V2-T069,V2-T070; **Criterio** cada ID del manifest tiene exactamente un productor en V2-T011–V2-T070, cualquier faltante o fallo de ejecución devuelve estado no cero, el piso ≥20 casos queda solo como sanity check secundario y nunca sustituye la verificación exacta; no se exige evidencia P1.
- [ ] V2-T072 Actualizar `README.md`, `quickstart.md` y `docs/evaluator-execution.md` con comandos realmente validados; **Dep.** V2-T071.
- [ ] V2-T073 [REQ-042–045] Validar contrato/runtime: igualdad primaria del árbol OpenAPI, checksum secundario, 10 operationIds P0, errores y cero secretos; **Dep.** V2-T072.
- [ ] V2-T074 Ejecutar paridad, runner y tres walkthroughs P0; registrar evidencia fechada; **Dep.** V2-T073.
- [ ] V2-T075 [REQ-027,REQ-063] Ejecutar el gate humano obligatorio de S12 y emitir puerta P0 y forecast real por slice; **Dep.** V2-T074; **Criterio** ≤400 o activar S12A/S12B/S12C; solo un PASS autoriza V2-T076 y no hay excepción aprobada.

## Fase 13 / S13: modelo P1 condicional

- [ ] V2-T076 [P] [US7] [REQ-040,REQ-041,REQ-060] [SCN-035] Escribir `UT-ASSIGNMENT` e `IT-ASSIGNMENT-PERIOD`; **Dep.** V2-T075.
- [ ] V2-T077 [P] [REQ-056,REQ-057,REQ-061] Escribir la extensión P1 de `AuditConcurrencyTests.cs`, pruebas de Subject/inmutabilidad e `IT-INDEXES-P1`; **Dep.** V2-T075; **Criterio** define assertions para `Subject`/`TeachingAssignment` auditables y `ClassSchedule` CreatedAt-only, pero `IT-AUDIT-UTC-P1`/`IT-ROWVERSION-P1` no se acreditan hasta que V2-T083 materialice las tablas y V2-T087 las ejecute; índices temporales sin `Id` en INCLUDE bajo PK clustered.
- [ ] V2-T078 [US7] Crear `Subject`, `TeachingAssignment` y `ClassSchedule` en Core; **Dep.** V2-T076,V2-T077; **Criterio** auditoría exacta solo en `Subject` y `TeachingAssignment`; `ClassSchedule` solo creación.
- [ ] V2-T079 [US7] [REQ-056,REQ-057,REQ-060,REQ-061] Configurar tres tablas, trigger Subject, rangos e índices/includes en Infrastructure; **Dep.** V2-T078; **Criterio** auditoría/check/rowversion en `Subject`/`TeachingAssignment`, solo `CreatedAtUtc` en `ClassSchedule`, e INCLUDE sin `Id` implícito por PK clustered.
- [ ] V2-T080 [US7] Implementar validación transaccional de escuela y contención temporal en `src/Inovait.Core/Features/TeachingAssignments/`; **Dep.** V2-T079; **Criterio condicional S13A** gate ≤400 antes de merge.
- [ ] V2-T081 Generar migración `AddP1TeachingModel` aislada en `Migrations/`; **Dep.** V2-T080; **Criterio** salida generada y manifest exacto.
- [ ] V2-T082 Crear migration manual `AddP1DatabaseProtections` solo para `TR_Subject_ProtectCode` y seeds P1 ficticios en `Seed/`; **Dep.** V2-T081.
- [ ] V2-T083 Extender `database/setup.sql` y `SetupSqlParityTests.cs` de 11 a 14 tablas y producir `IT-SQL-SCRIPT-P1`/`IT-SEED-P1`; **Dep.** V2-T082; **Criterio** ambas rutas incluyen las tres tablas P1 y el dataset ficticio de límites, empate, multisector e historia múltiple; **Criterio condicional S13B** gate ≤400 antes de merge.
- [ ] V2-T084 [P] [REQ-048,REQ-050] Escribir `IT-LIST-SUBJECTS` HTTP/integración en `tests/Inovait.IntegrationTests/Api/ListSubjectsTests.cs` para `listSubjects`; **Dep.** V2-T083; **Criterio** `200`, orden `name, code, id`, DTO canónico, operationId P1 y contrato OpenAPI sin cambios.
- [ ] V2-T085 Implementar query/use case no-tracking de materias en `src/Inovait.Core/Features/Catalogs/` y `src/Inovait.Infrastructure/Features/Catalogs/ListSubjectsQuery.cs`; **Dep.** V2-T084.
- [ ] V2-T086 Crear mapping DTO y operación controller `listSubjects` en `src/Inovait.Api/Features/Catalogs/`; **Dep.** V2-T085; **Criterio** contrato OpenAPI sin cambios.
- [ ] V2-T087 Validar S13 con 14 tablas, períodos, índices, `IT-SEED-P1`, prueba HTTP `listSubjects` y `IT-AUDIT-UTC-P1`/`IT-ROWVERSION-P1` verdes, y ejecutar el gate humano obligatorio; **Dep.** V2-T086; **Criterio positivo** auditoría/check/update real/rowversion en `Subject` y `TeachingAssignment`; **CreatedAt-only** `ClassSchedule`; **Gate** ≤400 o activar S13A/S13B/S13C, sin excepción.

## Fases 14–17: capacidades P1 condicionales

- [ ] V2-T088 [US4] [REQ-028–032,REQ-046,REQ-048] [SCN-020–023] [BQ-001,BQ-002] Escribir `IT-RPT-AGE` en `AgeDistributionTests.cs`; **Dep.** V2-T087.
- [ ] V2-T089 [US4] Implementar `getAgeDistribution` end-to-end sin agregados; **Dep.** V2-T088.
- [ ] V2-T090 Validar S14 con límites, filtros, ceros y gate humano obligatorio; **Dep.** V2-T089; **Criterio** ≤400 o bloqueo sin excepción.
- [ ] V2-T091 [US5] [REQ-028,REQ-033–035,REQ-047] [SCN-024–027] [BQ-003] Escribir `IT-RPT-SECTOR`, incluidos contratos cancelados; **Dep.** V2-T087.
- [ ] V2-T092 [US5] Implementar `getDistinctTeacherCountsBySector` usando índice School/date; **Dep.** V2-T091.
- [ ] V2-T093 Validar S15 con distinct, ambos sectores, período y gate humano obligatorio; **Dep.** V2-T092; **Criterio** ≤400 o bloqueo sin excepción.
- [ ] V2-T094 [US6] [REQ-028,REQ-036,REQ-037,REQ-048] [SCN-028–030] [BQ-004] Escribir `IT-RPT-TOP`; **Dep.** V2-T087.
- [ ] V2-T095 [US6] Implementar `getTopSchoolsByEnrollment` derivando School desde ClassGroup; **Dep.** V2-T094.
- [ ] V2-T096 Validar S16 con máximo, empates, vacío, orden y gate humano obligatorio; **Dep.** V2-T095; **Criterio** ≤400 o bloqueo sin excepción.
- [ ] V2-T097 [US7] [REQ-028,REQ-038–041,REQ-048] [SCN-031–035] [BQ-005] Escribir `IT-HISTORY` con persona dual y asignaciones temporales; **Dep.** V2-T087.
- [ ] V2-T098 [US7] Implementar `getStudentHistory` end-to-end; **Dep.** V2-T097.
- [ ] V2-T099 Validar S17 con años, múltiples docentes/materias, arrays vacíos y gate humano obligatorio; **Dep.** V2-T098; **Criterio** ≤400 o bloqueo sin excepción.

## Fase 18: cierre

- [ ] V2-T100 Integrar DTOs/controllers de reportes/historia P1 y producir `IT-OPENAPI` validando exactamente 15 operationIds runtime, incluido `listSubjects` ya ejecutable desde V2-T084–V2-T087; **Dep.** V2-T090,V2-T093,V2-T096,V2-T099.
- [ ] V2-T101 Escribir/ejecutar `scripts/run-p1-tests.sh` con fail-on-missing sobre el manifest P1 canónico de `docs/testing-strategy.md`, y ejecutar suite, build y format sin warnings ni supresiones; **Dep.** V2-T100; **Criterio** solo exige evidencia producida en V2-T076–V2-T100, incluidos `IT-SEED-P1`, `IT-AUDIT-UTC-P1` e `IT-ROWVERSION-P1`.
- [ ] V2-T102 [REQ-052–REQ-063] Revalidar 3NF/BCNF, FK compuesto, cero columnas `Normalized*` o equivalentes duplicadas de comparación, índices, triggers, permisos y paridad; **Dep.** V2-T101.
- [ ] V2-T103 Actualizar README, trazabilidad y guía del evaluador con estado real, ejecutar el gate humano obligatorio de S18 y empaquetar fuera del repositorio solo tras autorización; **Dep.** V2-T102; **Criterio** ≤400 o bloqueo sin excepción.

## Dependencias y cobertura

`V2-T001–V2-T004 → S01 V2-T005–V2-T010 → S02 V2-T011–V2-T019 → S03 V2-T020–V2-T026 → S04 V2-T027–V2-T031 → S05/S06 V2-T032–V2-T043 → S07 V2-T044–V2-T046 → S08–S11 V2-T047–V2-T067 → S12 V2-T068–V2-T075 → puerta P0 → S13 V2-T076–V2-T087 → S14–S17 V2-T088–V2-T099 → S18 V2-T100–V2-T103`.

- Task set: `production-model-v2.0.0`; 103 tareas estables. P0: V2-T001–V2-T075; P1 condicional: V2-T076–V2-T099; cierre: V2-T100–V2-T103.
- REQ-053–REQ-063: mapeados en V2-T020–V2-T046, V2-T068–V2-T070, V2-T075–V2-T087 y V2-T102.
- OpenAPI no se modifica: el refactor mantiene códigos documentales en la proyección y los 15 operationIds.

## Cobertura ejecutable de operaciones

| operationId | Prueba | Query/use case | DTO/controller | Gate |
| --- | --- | --- | --- | --- |
| `listSchools`, `listGrades`, `listAcademicYears`, `listClassGroups`, `listTeachers` | V2-T047 | V2-T049 | V2-T050 | V2-T051 |
| `createEnrollment` | V2-T052,V2-T053 | V2-T054 | V2-T055,V2-T056 | V2-T057 |
| `listEnrollments` | V2-T058 | V2-T059 | V2-T060 | V2-T061 |
| `createTeacherContracts`, `listTeacherContracts`, `listTeachersBySchool` | V2-T062,V2-T063 | V2-T064 | V2-T065,V2-T066 | V2-T067 |
| `listSubjects` | V2-T084 | V2-T085 | V2-T086 | V2-T087 |
| `getAgeDistribution` | V2-T088 | V2-T089 | V2-T089 | V2-T090 |
| `getDistinctTeacherCountsBySector` | V2-T091 | V2-T092 | V2-T092 | V2-T093 |
| `getTopSchoolsByEnrollment` | V2-T094 | V2-T095 | V2-T095 | V2-T096 |
| `getStudentHistory` | V2-T097 | V2-T098 | V2-T098 | V2-T099 |
