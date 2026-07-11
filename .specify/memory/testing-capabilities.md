# Capacidades de pruebas

**Modo Strict TDD**: habilitado para tareas de comportamiento posteriores a S01
**Detectado**: 2026-07-11

Existen proyectos ejecutables de pruebas unitarias e integración. El runner xUnit v3 se ejecuta con `dotnet test`; los smoke tests HTTP usan `WebApplicationFactory<Program>`.

## Test runner

- Comando: `dotnet test --configuration Debug` (repetir con `Release` en verificación).
- Framework: xUnit v3 `3.2.2`, `Microsoft.NET.Test.Sdk` `18.0.1`, runner Visual Studio `3.1.5`.

## Capas de pruebas

| Capa | Disponible | Herramienta |
| --- | --- | --- |
| Unit | Sí | xUnit v3 |
| Integration | Sí | xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing` |
| E2E | No | — |

## Cobertura

- Disponible: sí, mediante `coverlet.collector`.
- Comando: `dotnet test --collect:"XPlat Code Coverage"`.

## Herramientas de calidad

| Herramienta | Disponible en el repositorio | Comando |
| --- | --- | --- |
| Analyzers/compiler | Sí | `dotnet build --configuration Debug` |
| Nullable/type checking | Sí | compilador C# con nullable y warnings-as-errors |
| Formatter | Sí | `dotnet format --verify-no-changes --no-restore` |

La puerta completa repite build y test en Debug/Release, escanea paquetes vulnerables y ejecuta `git diff --check`.
