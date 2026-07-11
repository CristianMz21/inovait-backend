# Arquitectura planificada

## Decisión

El backend será un monolito modular REST en tres proyectos de producción. El repositorio `inovait-backend` es canónico para dominio, persistencia, OpenAPI y planificación de `database/setup.sql`; `inovait-frontend` permanece independiente y consume el contrato versionado.

```text
inovait-frontend
      │ HTTPS + camelCase JSON + ProblemDetails
      ▼
Inovait.Api ──► Inovait.Core ◄── Inovait.Infrastructure ──► SQL Server
 Controllers    Domain/Features      EF/config/query stores
      │                                      │
      └──────── composition root/DI ─────────┘
```

## Límites de proyectos

| Proyecto | Responsabilidad | No contiene |
| --- | --- | --- |
| `Inovait.Api` | Controllers, request/response DTOs, model binding, ProblemDetails, OpenAPI, CORS local, DI | entidades EF, reglas SQL, lógica de reportes |
| `Inovait.Core` | Entidades, enums, normalización, edad, intervalos, estado efectivo y servicios/puertos por feature | ASP.NET, EF Core, SQL Server |
| `Inovait.Infrastructure` | `DbContext`, configuraciones, migraciones, seeds, transacciones y consultas proyectadas | DTOs HTTP, controllers |

Cuatro proyectos (`Api/Application/Domain/Infrastructure`) son correctos para un sistema mayor, pero separar Application y Domain deja capas pequeñas y aumenta referencias. Dos o uno mezclan límites o generan acoplamiento circular. Tres es el mínimo que conserva dependencia `Api → Core`, `Infrastructure → Core` y composición desde Api.

## Organización por feature

```text
src/Inovait.Api/
├── Features/{Catalogs,Enrollments,TeacherContracts,Reports}/
│   ├── *Controller.cs
│   └── Contracts/*.cs
├── Errors/
└── Program.cs
src/Inovait.Core/
├── Domain/{Catalogs,Enrollments,Teachers,Teaching}/
└── Features/{Catalogs,Enrollments,TeacherContracts,Reports}/
src/Inovait.Infrastructure/
├── Persistence/{Configurations,Migrations,Seed}/
└── Features/{Catalogs,Enrollments,TeacherContracts,Reports}/
```

Cada feature expone un servicio de caso de uso y un puerto específico cuando necesita persistencia. No hay buses, handlers uno-a-uno, `Generic Repository` ni abstracciones CRUD.

## Flujo HTTP

1. Controller valida forma/tipos y recibe `CancellationToken`.
2. Servicio Core normaliza identidad y valida invariantes puras.
3. Puerto Infrastructure carga referencias y valida combinaciones.
4. Escrituras P0 ejecutan una transacción async; lecturas usan proyección sin tracking.
5. Excepciones de negocio tipadas se traducen centralmente a `ProblemDetails`; no se exponen trazas o SQL.
6. Controller devuelve DTO, nunca entidad.

## Atomicidad y concurrencia

- `createEnrollment`: una transacción resuelve/crea `Student` y crea `Enrollment`. Los índices únicos protegen identidad y año frente a carreras; colisiones se traducen a 409.
- `createTeacherContracts`: valida toda la solicitud, abre transacción `Serializable`, consulta intervalos del docente/escuelas con índice de soporte, inserta todos o ninguno y confirma. Una escuela repetida se detecta antes de SQL.
- Un trigger para superposiciones se rechaza: escondería lógica de aplicación, dificultaría pruebas y errores y no elimina la necesidad de validación previa. `CHECK` no puede comparar filas.

## Contrato y errores

`contracts/openapi.yaml` es la fuente canónica de diseño. Actualmente el bundle está sin seguimiento: el HEAD `ce160e9...` no lo contiene. Apply queda bloqueado hasta obtener autorización explícita, crear un baseline versionado y registrar su commit+checksum; el frontend deberá rechazar un contrato untracked, dirty o divergente. ASP.NET Core generará un documento para comprobar divergencias, pero no redefine rutas ni schemas. Todas las operaciones declaran `security: []`; no se registra autenticación.

Para lecturas no existe una entidad adicional de “oferta académica”: cualquier School/Grade/AcademicYear existente es un contexto válido y la ausencia de ClassGroup devuelve vacío. Solo una escritura que suministra un ClassGroup ajeno al contexto produce `422`.

## Estado contractual

Persistido: `Confirmed|Cancelled`. Efectivo para fecha `d`: `Cancelled` si está cancelado; de lo contrario `Upcoming` si `d < StartDate`, `Expired` si `EndDate < d`, y `Effective` en otro caso. Para períodos, reportes filtran `Confirmed` e intersección inclusiva. Nunca se almacena “actual”. El valor predeterminado de “hoy” proviene de `IBusinessDateProvider.Today`, implementado con la fecha calendario UTC y reemplazable en pruebas; `asOfDate` explícito evita ambigüedad en evaluación.

## CORS, seguridad y datos

CORS solo admite orígenes locales explícitos en configuración, métodos GET/POST y cabeceras necesarias; no usa comodín con credenciales. Connection strings llegan por variables de entorno. Logging estructurado omite documentos y valores sensibles. Todos los seeds son ficticios.

## Entrega por prioridad

P0 incluye infraestructura mínima, 8 tablas, cinco catálogos, inscripción, filtro conjunto, contratos multiescuela, `database/setup.sql`, pruebas críticas y guía del evaluador. P1 condicional agrega Subject, TeachingAssignment, ClassSchedule, `listSubjects`, reportes, historia y seeds especializados solo tras la puerta ejecutable. Quedan diferidos: paginación, CRUD administrativo, autenticación, UI OpenAPI de terceros, observabilidad avanzada y escalado distribuido.
