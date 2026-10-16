# AGENTS.md - Directrices para Agentes de IA

Este documento define el contexto operativo, roles, convenciones de ingeniería y directrices para cualquier agente de IA o desarrollador que interactúe con el repositorio **UTNFRGP-TUP-BD2-TPI-G17 (GimnasioDB)**.

---

## 1. Contexto del Repositorio y Arquitectura

- **Dominio:** Sistema de Gestión Integral para Gimnasio y Club Deportivo (desarrollado originalmente como Trabajo Práctico Integrador para Base de Datos II, UTN FRGP - TUP).
- **Enfoque de Negocio:** Software de gestión comercial y operativo para clientes reales (administración de socios, staff/empleados, cuotas y pagos, membresías, clases y control de cupos).
- **Stack Tecnológico:**
  - **Base de Datos:** Microsoft SQL Server 2025 Express corriendo en contenedor Docker (`SQLServer2025:1433`).
  - **Backend:** C# .NET 10 (ASP.NET Core Web API, Minimal APIs).
  - **Acceso a Datos:** Micro-ORM Dapper y ADO.NET (`Microsoft.Data.SqlClient`) de ultra-alto rendimiento con `IDbConnectionFactory`.
  - **Frontend:** Single Page Application (SPA) responsiva integrada en `wwwroot` (Tailwind CSS, Lucide Icons, Vanilla JavaScript moderno ES6+).

---

## 2. Convenciones de Desarrollo y Buenas Prácticas

### Backend (C# .NET)
1. **C# Moderno:**
   - Usar `record` para DTOs inmutables de entrada y salida.
   - Primary Constructors en servicios y repositorios.
   - Tipos de datos alineados con SQL Server: `decimal` para `MONEY`, `long` para `BIGINT`, `DateTime` / `DateOnly` para fechas, `bool` para `BIT`.
2. **Acceso a Datos:**
   - Siempre usar consultas parametrizadas (`DynamicParameters` o `@Param`) para prevenir inyección SQL.
   - Uso de conexiones asincrónicas (`await using var connection = await connectionFactory.CreateOpenConnectionAsync()`).
   - Respetar transacciones atómicas (`BEGIN TRANSACTION / COMMIT / ROLLBACK`) y capturar `SqlException` estructuradamente para exponer errores de negocio al cliente.
3. **APIs Limpias:**
   - Minimal APIs en `Endpoints/` organizadas por dominios lógicos con `MapGroup`.
   - Respuestas HTTP semánticas (`Results.Ok`, `Results.BadRequest`, `Results.NotFound`, `Results.Problem`).

### Frontend (SPA Cliente)
1. **Enfoque de Producto Comercial:**
   - La interfaz debe presentarse como un software de gestión para gimnasios de nivel profesional para clientes/usuarios finales.
   - **Evitar** carteles o cuadros educativos que expliquen "qué hace la query", nombres técnicos de scripts del TP o referencias académicas en las pantallas de gestión.
   - Mantener el diseño limpio, funcional, con feedback claro (éxito/error) e iconos intuitivos.
2. **Consistencia Visual:**
   - Dark theme moderno con paleta Slate/Indigo/Emerald.
   - Notificaciones y validaciones claras en formularios.

---

## 3. Comandos Útiles

- **Compilar solución:** `dotnet build`
- **Iniciar aplicación web:** `dotnet run --project src/Gimnasio.Web --urls "http://localhost:5150"`
- **Verificar contenedor de SQL Server:** `docker ps`
- **Revisar logs del servidor:** `docker logs --tail 50 SQLServer2025`
