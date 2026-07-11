# Contexto de planificación del proyecto

**Detectado**: 2026-07-11

## Estado del repositorio

- Repositorio independiente: `inovait-backend`.
- Rama de trabajo actual: `feat/production-data-model`; la integración objetivo sigue siendo `main`.
- Estado previo a la inicialización: únicamente `README.md` y `.gitignore`; no existían soluciones, proyectos ni código fuente.
- Estado actual: existen `global.json`, `Inovait.slnx`, tres proyectos bajo `src/`, dos proyectos xUnit v3 bajo `tests/` y un host API mínimo con pruebas HTTP. No existen entidades, migraciones ni `database/setup.sql`.
- El repositorio de front-end permanece separado y no forma parte de este contexto de planificación.

## Contexto técnico existente

- La solución usa ASP.NET Core/EF Core `10.0.9`, `net10.0`, C# 14 y warnings-as-errors.
- Los proyectos de pruebas usan xUnit v3 `3.2.2`, `Microsoft.NET.Test.Sdk` `18.0.1` y runner Visual Studio `3.1.5`.
- SDK de .NET disponibles en el entorno:
  - `8.0.128`
  - `10.0.109`
- `global.json` fija SDK `10.0.109` con `latestPatch`.

## Restricciones para las siguientes fases

- Mantener este repositorio independiente del repositorio de front-end.
- Producir especificaciones y planes técnicos en español profesional neutro.
- Mantener los identificadores técnicos en inglés.
- S01 solo autoriza scaffold y smoke behavior; las convenciones de dominio comienzan en S02.
- Mantener cero supresiones de warnings y cero paquetes vulnerables conocidos.
