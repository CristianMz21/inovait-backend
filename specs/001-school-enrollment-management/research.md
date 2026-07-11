# Investigación técnica: gestión escolar

## Resultado

Se seleccionan .NET 10 LTS y EF Core 10 sobre SQL Server 2022, Controllers y una estructura de tres proyectos de producción. Las decisiones priorizan estabilidad instalada, claridad para evaluación y pruebas reales de integridad relacional.

## Evidencia del entorno y soporte

El 2026-07-10 se ejecutaron únicamente comandos de inspección:

```text
dotnet --list-sdks
8.0.128
10.0.109

dotnet --list-runtimes
Microsoft.AspNetCore.App 8.0.28
Microsoft.AspNetCore.App 10.0.9
Microsoft.NETCore.App 8.0.28
Microsoft.NETCore.App 10.0.9
```

Microsoft clasifica .NET 10 como LTS en soporte activo hasta 2028-11-14; .NET 8 está en mantenimiento y finaliza 2026-11-10. EF Core 10 también es LTS, requiere .NET 10 y tiene soporte hasta noviembre de 2028. No se selecciona .NET 11 porque es preview ni se instala SDK alguno.

## Decisiones

### Plataforma y versiones

**Decisión**: SDK `10.0.109`, TFM `net10.0`, C# 14, ASP.NET Core/runtime `10.0.9`, `Microsoft.AspNetCore.OpenApi` `10.0.9` y EF Core SQL Server `10.0.9`.

**Fundamento**: son versiones estables instaladas y el último parche disponible del canal 10.0 al consultar las fuentes; mantienen framework y paquetes Microsoft en el mismo parche. .NET 10 conserva más de dos años de soporte activo para una entrega nueva.

**Alternativas consideradas**: .NET 8/EF 8 están instalados y son LTS, pero ya están en mantenimiento y próximos al fin de soporte. Versiones 11 preview quedan descartadas por estabilidad. `global.json` deberá fijar `10.0.109` en implementación futura.

### Controllers frente a Minimal APIs

**Decisión**: Controllers con `[ApiController]`, DTOs por feature y servicios inyectados.

**Fundamento**: Microsoft recomienda Minimal APIs para proyectos nuevos, pero reconoce Controllers para aplicaciones con lógica compleja y validación MVC. En este caso, 15 operaciones, cuatro clases de error, DTOs amplios y revisión por evaluador se benefician de acciones agrupadas, atributos y nombres visibles. La pequeña diferencia de rendimiento no es relevante para el volumen evaluable.

**Alternativas consideradas**: Minimal APIs reducen boilerplate y son el valor predeterminado oficial, pero exigirían route groups, filtros y metadatos distribuidos; no mejoran el dominio ni el plazo de un día.

### Estructura de proyectos

**Decisión**: `Inovait.Api`, `Inovait.Core` e `Inovait.Infrastructure`, más proyectos separados de pruebas unitarias e integración.

**Fundamento**: Core reúne Domain y Application en carpetas sin mezclar EF ni HTTP. Infrastructure implementa puertos específicos por feature y consultas; Api compone DI y contrato. Se evitan capas vacías con límites comprobables.

**Alternativas consideradas**: cuatro proyectos `Api/Application/Domain/Infrastructure` ofrecen aislamiento máximo, pero agregan referencias y ceremonia para una jornada. Un proyecto único mezcla responsabilidades; dos proyectos provocan que persistencia dependa de tipos HTTP o que Api contenga EF.

### Acceso a datos y transacciones

**Decisión**: EF Core directo dentro de Infrastructure mediante puertos orientados a casos de uso; sin `Generic Repository`, MediatR ni CQRS. Operaciones de inscripción y contratos usan una única transacción async. La detección de superposición usa aislamiento `Serializable` y un índice por docente/escuela/fechas.

**Fundamento**: EF ya implementa Unit of Work y seguimiento. Un repositorio genérico ocultaría capacidades SQL y añadiría código sin necesidad. `Serializable` protege la lectura de intervalos frente a inserciones concurrentes; las restricciones únicas siguen siendo la defensa final para duplicados.

**Alternativas consideradas**: trigger de superposición rechazado por lógica oculta, dificultad de mapear `ProblemDetails` y duplicación con aplicación. Stored procedures y CQRS se descartan por alcance.

### SQL Server y modelado temporal

**Decisión**: SQL Server 2022, EF Core SQL Server 10.0.9, `DateOnly`/`date`, sin agregados persistidos. `AcademicYear` es tabla. `SchoolSector` y `TeacherContractStatus` son enums de cadena cerrados con `CHECK` SQL y enums OpenAPI.

**Fundamento**: EF 10 soporta SQL Server 2019 en adelante y mejora traducciones `DateOnly`. Una tabla `AcademicYear` permite nombre, límites, FK y un único año actual; un entero libre no cumple REQ-046. Cadenas `Public|Private` y `Confirmed|Cancelled` son legibles y coherentes entre dominio, API y SQL sin catálogos administrables.

**Alternativas consideradas**: entero para año y tablas lookup se rechazan por reglas o complejidad. `bit IsPublic` es compacto pero menos expresivo. Timestamps técnicos se omiten porque ninguna capacidad los necesita.

### Estado contractual efectivo

**Decisión**: persistir solo `Confirmed` o `Cancelled`. Derivar `Upcoming`, `Effective`, `Expired` o `Cancelled` para una fecha: `Cancelled` domina; en `Confirmed`, las fechas determinan los otros tres. Reportes incluyen solo `Confirmed` cuyo intervalo interseca la fecha o período solicitado.

**Fundamento**: evita un estado `Current` obsoleto y mantiene la decisión administrativa separada de vigencia temporal.

**Alternativas consideradas**: persistir `Active/Expired` requiere trabajos de sincronización y puede quedar desactualizado; un booleano pierde semántica.

### OpenAPI y errores

**Decisión**: OpenAPI 3.1.0 modular, rutas `/api/...`, camelCase, `security: []` y `ProblemDetails` con `code` y errores por campo. Cada operación declara solo los estados que puede producir. Los diez YAML del bundle canónico están versionados y son reproducibles desde el baseline `1223630ab99bf1bfaa4f5919fccf5ff539379c8e`; su checksum combinado es `802c13b91bf5c6425d24c540b6841a2abe134e084ea310fc2b7041e32c24a81a`.

**Fundamento**: ASP.NET Core 10 admite generación OpenAPI de primera parte para Controllers y Minimal APIs. La modularidad mantiene unidades revisables menores a 400 líneas.

**Alternativas consideradas**: un único YAML sería más simple de mover, pero sobrecarga revisión. Swagger/OpenAPI de terceros no es necesario para el contrato base.

### Validación de entrada

**Decisión**: model binding y validación integrada de ASP.NET Core para forma/tipos, más validadores explícitos Core para reglas puras. No se incorpora FluentValidation al MVP.

**Fundamento**: las reglas críticas ya necesitan servicios de dominio y comprobaciones SQL; otra biblioteca duplicaría fronteras y dependencia para 15 operaciones.

**Alternativas consideradas**: FluentValidation ofrece una DSL madura y estaba mencionado como intención en README, pero no está instalado ni aporta una regla que el stack integrado no pueda expresar con claridad en una jornada.

### Estrategia de pruebas SQL Server

**Decisión**: xUnit v3 `3.2.2`; unitarias puras; integración con `WebApplicationFactory` `10.0.9` y SQL Server real. P0 usa `Testcontainers.MsSql` `4.13.0` como ruta primaria, un contenedor compartido por colección y base limpia por escenario. Una instancia externa SQL Server 2022 puede documentarse como fallback aislado, pero no repite la puerta obligatoria. Todas las pruebas P0 usan `[Trait("Priority", "P0")]`; un runner lista casos, exige un mínimo explícito y solo entonces los ejecuta.

**Fundamento**: Microsoft recomienda probar contra el motor real y desaconseja EF InMemory; Testcontainers automatiza ciclo de vida y conexión. El listado con conteo evita el falso verde de `dotnet test --filter` cuando descubre cero pruebas.

**Alternativas consideradas**: EF InMemory no soporta transacciones ni restricciones; SQLite difiere en SQL, collation y traducción; LocalDB solo es viable en Windows. Exigir Testcontainers y una base externa como dos gates duplicaría costo sin aumentar evidencia P0.

## Fuentes consultadas

- [Política oficial de soporte .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), actualizada 2026-06-09.
- [Metadatos oficiales de .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/releases.json).
- [APIs de ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0), actualizada 2026-05-05.
- [OpenAPI en ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0), actualizada 2026-03-23.
- [Novedades de EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew), actualizada 2026-06-24.
- [Proveedor EF Core para SQL Server](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/), actualizado 2026-01-12.
- [Estrategia de pruebas EF Core](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy), actualizada 2026-06-26.
- [Pruebas de integración ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0), actualizada 2026-06-26.
- [Módulo SQL Server de Testcontainers](https://dotnet.testcontainers.org/modules/mssql/).
- Índices oficiales de NuGet consultados el 2026-07-10 para confirmar EF/OpenAPI `10.0.9`, Testcontainers `4.13.0` y xUnit v3 `3.2.2`.
