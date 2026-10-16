# Sistema de Gestión de Gimnasio y Club - TPI

## Contexto Academico
Este sistema fue desarrollado en el marco de la materia Base de Datos 2 (Año 2026) de la Tecnicatura Universitaria en Programación - UTN FRGP.

Grupo 17 - Integrantes:
 * Juan Cruz Dominguez Pistoia
 * Lautaro Silva

---

## Descripción del Sistema
Este proyecto consiste en el diseño e implementación de una base de datos relacional para un **Sistema de Gestión de Gimnasio y Club Deportivo**. El sistema está desarrollado bajo el motor **SQL Server sobre contenedores Docker** como entrega obligatoria para la materia **Base de Datos II** de la Tecnicatura Universitaria en Programación (TUP) en la **Universidad Tecnológica Nacional - Facultad Regional General Pacheco (UTN FRGP)**.

El objetivo principal es resolver de forma centralizada y eficiente la problemática operativa, logística y comercial de un complejo deportivo a gran escala, permitiendo administrar:
* **Identidad Unificada de Personas:** Implementación de herencia/especialización para evitar redundancias de contacto, soportando nativamente que un empleado (ej: profesor o recepcionista) sea también socio deportivo del club sin duplicar sus registros.
* **Recursos Humanos (Staff y Roles):** Clasificación del personal bajo un catálogo de roles dinámicos (Profesores de Planta, Personal Trainers, Recepcionistas y Personal de Limpieza).
* **Control Comercial (Membresías y Contratos):** Seguimiento unívoco de los contratos de suscripción activos de los socios, control de vigencias lógicas y alertas de morosidad.
* **Flujo Transaccional Financiero:** Procesamiento de cobros y estados de caja con la flexibilidad de permitir múltiples pagos fraccionados o cuotas por cada membresía.
* **Logística de Clases y Vacantes:** Cronograma automatizado de actividades con cálculo y control dinámico de cupos en tiempo real para evitar la sobreventa de salas.

---

## Modelo de Datos y Decisiones de Arquitectura
El sistema implementa una arquitectura relacional normalizada en **Tercera Forma Normal (3FN)**, aplicando las mejores prácticas recomendadas por la cátedra para el resguardo de la integridad referencial.

### Puntos Clave del Diseño Técnico
1.  **Especialización de Entidades:** Se estructuró la tabla mestre `Personas` vinculada a las tablas dependientes `Socios` y `Empleados` mediante claves foráneas explícitas hacia `IDPersona`, rompiendo acoplamientos rígidos de uno a uno.
2.  **Optimización de Tipos de Datos:** Se redujo el tamaño de almacenamiento en columnas del catálogo utilizando `INT` en `IDRol` e `IDMembresia` para optimizar los índices de búsqueda.
3.  **Precisión Monetaria:** Todas las columnas referentes a flujos de caja y dinero (`Precio` en membresías y `Monto` en pagos) utilizan estrictamente el tipo de dato nativo `MONEY` de SQL Server.
4.  **Claves e Índices lógicos:** Se utiliza una clave subrogada artificial (`IDSocioMembresia`) para agilizar los cruzamientos (`JOINs`), blindando la integridad comercial mediante un `CONSTRAINT UNIQUE` compuesto sobre `(IDSocio, IDMembresia, FechaInicio)` para impedir solapamientos.

---

## Requisitos Técnicos Implementados
Para satisfacer la totalidad de los requisitos mínimos exigidos por la cátedra de la UTN, el sistema incluye los siguientes objetos programados:

### Vistas Complejas (Mínimo 3)
* `vw_SociosMembresiasActivas`: Reporte administrativo de pases vigentes con cálculo matemático de días restantes de entrenamiento.
* `vw_OcupacionClases`: Análisis logístico de ocupación de salones con cálculo porcentual de alumnos inscritos sobre el cupo máximo.
* `vw_RecaudacionMensual`: Auditoría analítica de caja agrupando cantidad de transacciones, recaudación total y ticket promedio por período mensual.

### Procedimientos Almacenados (Mínimo 2)
* `sp_ReporteAsistenciasSocio` *(Reporte Parametrizado)*: Extracción ordenada cronológicamente del presentismo e inasistencias de un socio dentro de un rango de fechas.
* `sp_RegistrarPagoMembresia` *(Acción Transaccional)*: Procesamiento seguro de cobros parciales o cuotas bajo el principio de atomicidad, utilizando bloques de control de transacciones (`BEGIN TRANSACTION / COMMIT / ROLLBACK`) y captura estructurada de excepciones (`TRY...CATCH`) con `RAISERROR`.

### Disparadores / Triggers DML (Mínimo 2)
* `trg_Inscripcion_ActualizarCupo` (`AFTER INSERT`): Decrementa en tiempo real una vacante disponible en la tabla `Clases` al inscribir a un alumno. Cuenta con una validación restrictiva que cancela la operación y ejecuta un `ROLLBACK` si el salón está lleno.
* `trg_Cancelacion_ActualizarCupo` (`AFTER DELETE`): Restituye de forma inmediata e incremental la vacante disponible en la clase ante la cancelación o baja de una reserva.

---

## Estructura de Scripts del Repositorio
El repositorio está modularizado en archivos independientes para facilitar el control de versiones y evitar conflictos de mezcla (`merge conflicts`):

```text
├── 📄 README.md                    # Documentación y descripción del TPI
├── 📄 Gimnasio.slnx                # Solución .NET
├── 📁 Creacion de la Base de Datos
│   ├── 📜 Creacion de BBDD.sql     # Estructura de tablas y restricciones (DDL)
│   ├── 📜 Insercion de Datos.sql   # Carga masiva de socios, empleados y clases (DML)
│   ├── 📜 Vistas.sql               # Definición de las 3 abstracciones de lectura
│   ├── 📜 Procedimientos.sql       # Lógica programada transaccional y de reportes
│   └── 📜 Triggers.sql             # Automatización logística de control de cupos
├── 📁 Documentacion
│   ├── 📄 TPI - Base de Datos II - Grupo 17.pdf
│   └── 📄 TPI_Gimnasio_DER.png     # Diagrama de Entidad-Relación formal
└── 📁 src
    └── 📁 Gimnasio.Web             # Aplicación Web ASP.NET Core & Dapper
        ├── 📁 Data                 # Factoría de conexiones y repositorios de alto rendimiento
        ├── 📁 Endpoints            # Minimal APIs RESTful
        ├── 📁 Models               # Records y DTOs inmutables
        └── 📁 wwwroot              # SPA interactiva (Tailwind CSS, Lucide Icons, Vanilla JS)
```

---

## Entorno Web de Consultas: `Gimnasio.Web` (C# .NET)

Para interactuar de forma intuitiva y visual con el motor de base de datos en Docker, se implementó una aplicación web moderna desarrollada en **ASP.NET Core (.NET 10)** y micro-ORM **Dapper**.

### Demostración de Conocimiento en SQL y C# .NET Moderno

1. **Acceso a Datos de Alto Rendimiento:**
   - **Micro-ORM Dapper + ADO.NET (`Microsoft.Data.SqlClient`)**: Conexiones asincrónicas no bloqueantes (`await using`, `IAsyncDisposable`), consultas parametrizadas contra SQL Injection y mapeo directo a memoria de alta velocidad sin la sobrecarga de un ORM pesado.
   - **Patrón Factory (`IDbConnectionFactory`)**: Desacopla la creación y configuración del connection pooling de SQL Server mediante Inyección de Dependencias.
2. **C# Moderno (C# 12 / 13):**
   - **Records inmutables** para DTOs de entrada y salida (`SocioMembresiaActivaDto`, `OcupacionClaseDto`, `RecaudacionMensualDto`, etc.).
   - **Primary Constructors** y **Collection Expressions**.
   - **Minimal APIs** agrupadas limpiamente mediante `RouteGroupBuilder` con Typed Results.
3. **Manejo Estructurado de Excepciones SQL:**
   - Captura precisa de excepciones `SqlException`, identificando números de error y mensajes de `RAISERROR` devueltos tanto por Stored Procedures como por Triggers (ej: validación de vacantes agotadas o contrato inexistente).
4. **Módulos del Entorno Web:**
   - 📊 **Dashboard & KPIs:** Monitoreo en vivo del servidor SQL en Docker (ping de conexión en milisegundos, conteo dinámico de socios, empleados, clases, contratos y recaudación acumulada).
   - 👁️ **Vistas de Negocio:** Visualización interactiva de `vw_SociosMembresiasActivas` (con cálculo de días de gracia/morosidad), `vw_OcupacionClases` (con barras porcentuales de ocupación de salas) y `vw_RecaudacionMensual` (auditoría financiera).
   - ⚡ **Procedimientos Almacenados:** Formularios para invocar `sp_ReporteAsistenciasSocio` (reporte con filtros de socio y fechas) y `sp_RegistrarPagoMembresia` (demostración de transacción ACID con `BEGIN TRAN`, `COMMIT` y captura de rollback).
   - 🎯 **Laboratorio de Triggers en Tiempo Real:** Interfaz para inscribir y dar de baja socios en clases, observando cómo `trg_Inscripcion_ActualizarCupo` y `trg_Cancelacion_ActualizarCupo` actualizan el stock de vacantes en SQL Server de manera atómica, y visualizando la cancelación con `ROLLBACK` cuando no hay cupo.
   - 🗃️ **Explorador de Tablas (Data Browser):** Navegador para consultar cualquiera de las 10 tablas base con paginación optimizada vía `OFFSET ... FETCH NEXT ROWS`.
   - 💻 **SQL Studio Libre:** Consola para escribir y ejecutar sentencias SQL arbitrarias, midiendo el tiempo de respuesta del motor en ms y permitiendo exportar resultados a CSV.

### Cómo Ejecutar la Aplicación Web

1. Asegúrate de tener el contenedor de SQL Server corriendo:
   ```bash
   docker ps
   # Nombre: SQLServer2025, puerto mapeado: 1433
   ```
2. Iniciar el servidor web de .NET:
   ```bash
   dotnet run --project src/Gimnasio.Web --urls "http://localhost:5150"
   ```
3. Abrir en cualquier navegador:
   ```text
   http://localhost:5150
   ```
