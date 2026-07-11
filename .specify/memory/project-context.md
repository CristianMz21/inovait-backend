# Contexto de planificación del proyecto

**Detectado**: 2026-07-10

## Estado del repositorio

- Repositorio independiente: `inovait-backend`.
- Rama actual: `main`, con seguimiento de `origin/main`.
- Estado previo a la inicialización: únicamente `README.md` y `.gitignore`; no existían soluciones, proyectos ni código fuente.
- El repositorio de front-end permanece separado y no forma parte de este contexto de planificación.

## Contexto técnico existente

- `README.md` documenta como intención previa un back-end con ASP.NET Core, SQL Server, Entity Framework Core, FluentValidation y xUnit.
- No hay archivos `.sln`, `.slnx`, `.csproj`, `global.json` ni configuración de dependencias que materialicen esa intención.
- SDK de .NET disponibles en el entorno:
  - `8.0.128`
  - `10.0.109`
- La inicialización no selecciona una versión de SDK, runtime, framework ni dependencia. La elección debe resolverse en una fase posterior de especificación y planificación.

## Restricciones para las siguientes fases

- Mantener este repositorio independiente del repositorio de front-end.
- Producir especificaciones y planes técnicos en español profesional neutro.
- Mantener los identificadores técnicos en inglés.
- No generar código de producción, entidades, migraciones, controladores ni pruebas durante las fases exclusivamente de planificación.
- No instalar ni actualizar dependencias sin una decisión técnica explícita y posterior.
