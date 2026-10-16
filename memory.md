# MEMORY.md - Memoria Operativa del Proyecto

## Estado Actual del Proyecto
- **Proyecto:** GimnasioDB - Sistema de Gestión de Gimnasio y Club Deportivo.
- **Solución:** `Gimnasio.slnx` con el proyecto web `src/Gimnasio.Web`.
- **Framework:** .NET 10.0 (C# 13), Dapper 2.1.89, Microsoft.Data.SqlClient 7.1.1.
- **Motor BD:** Microsoft SQL Server 2025 Express en Docker (`SQLServer2025:1433`).
  - Base de datos: `GimnasioDB`.
  - Credenciales: usuario `sa`, contraseña `BaseDatos#2`.

---

## Esquema y Objetos de Base de Datos
- **Tablas:**
  - `Personas`: `IDPersona` (PK), `Nombre`, `Apellido`, `Email`, `Telefono`, `FechaNacimiento`, `Activo`.
  - `Socios`: `IDSocio` (PK), `IDPersona` (FK), `FechaRegistro`.
  - `Empleados`: `IDEmpleado` (PK), `IDPersona` (FK), `IDRol` (FK), `FechaContratacion`.
  - `Roles`: `IDRol` (PK), `NombreRol`.
  - `Membresias`: `IDMembresia` (PK), `Nombre`, `Precio` (MONEY), `DuracionDias`.
  - `SocioMembresias`: `IDSocioMembresia` (PK), `IDSocio` (FK), `IDMembresia` (FK), `FechaInicio`, `FechaVencimiento`, `Activa`.
  - `Pagos`: `IDPago` (PK), `IDSocioMembresia` (FK), `Monto` (MONEY), `FechaPago`.
  - `Clases`: `IDClase` (PK), `Nombre`, `IDProfesor` (FK), `Horario`, `CupoMaximo`, `CupoDisponible`.
  - `InscripcionesClase`: `IDInscripcion` (PK), `IDSocio` (FK), `IDClase` (FK), `FechaInscripcion`.
  - `HistorialAsistencias`: `IDAsistencia` (PK), `IDSocio` (FK), `IDClase` (FK), `FechaAsistencia`, `Presente`.
- **Vistas:** `vw_SociosMembresiasActivas`, `vw_OcupacionClases`, `vw_RecaudacionMensual`.
- **Procedimientos Almacenados:** `sp_ReporteAsistenciasSocio`, `sp_RegistrarPagoMembresia`.
- **Triggers:** `trg_Inscripcion_ActualizarCupo` (`AFTER INSERT`), `trg_Cancelacion_ActualizarCupo` (`AFTER DELETE`).

---

## Directivas y Preferencias del Usuario
1. **Enfoque de Producto Comercial:**
   - La aplicación debe percibirse como un software listo para ser entregado a un cliente final de un gimnasio.
   - Eliminar todos los cuadros, banners o avisos que expliquen qué hace la query o que hagan referencias académicas al TP / cátedra.
2. **Componentes Intocables:**
   - Mantener el **SQL Studio Libre** y el **Explorador de Tablas (Data Browser)** exactamente como están.
3. **Nueva Funcionalidad Requerida:**
   - **Sección/Página de Cargas y Bajas para Personas (junto a sus herencias):**
     - Alta integral de una persona con opción de asociarla como Socio (con fecha de registro), Empleado (con rol y fecha de contratación) o ambos.
     - Baja (eliminación física o baja lógica `Activo = 0` / eliminación segura de registros asociados respetando restricciones referenciales).
     - Búsqueda, filtrado y visualización del listado unificado de personas con badges claros de tipo (Socio, Empleado, o Mixto).

---

## Registro de Cambios
- **Versión 1.0 (Completada):** Creación del proyecto ASP.NET Core `Gimnasio.Web`, Minimal APIs, capa de acceso con Dapper y UI SPA con Tailwind. Verificación exitosa de endpoints, vistas, stored procedures y triggers.
- **Versión 1.1 (Completada):** Creación de `agents.md` y `memory.md`, eliminación de cuadros explicativos técnicos en la UI, e implementación del módulo integral de Personas (Cargas y Bajas con soporte de herencia y perfiles, búsqueda y bajas lógicas/definitivas).
- **Versión 1.2 (Completada):** Transición de arquitectura SPA monolítica a Multi-Page Application (MPA) modular con URLs amigables (`/`, `/personas`, `/operaciones`, `/studio`). Separación de HTMLs y scripts JS independientes (`dashboard.js`, `personas.js`, `operaciones.js`, `studio.js`, `shared.js`).

