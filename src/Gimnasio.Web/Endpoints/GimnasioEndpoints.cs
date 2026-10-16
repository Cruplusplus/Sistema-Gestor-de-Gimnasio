using Gimnasio.Web.Data;
using Gimnasio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gimnasio.Web.Endpoints;

public static class GimnasioEndpoints
{
    public static RouteGroupBuilder MapGimnasioEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // Dashboard & KPIs
        group.MapGet("/dashboard/stats", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            try
            {
                var stats = await repo.GetDashboardStatsAsync(ct);
                return Results.Ok(stats);
            }
            catch (SqlException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Error al consultar SQL Server");
            }
        });

        // 3 Vistas Complejas
        group.MapGet("/vistas/socios-membresias-activas", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var data = await repo.GetSociosMembresiasActivasAsync(ct);
            return Results.Ok(data);
        });

        group.MapGet("/vistas/ocupacion-clases", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var data = await repo.GetOcupacionClasesAsync(ct);
            return Results.Ok(data);
        });

        group.MapGet("/vistas/recaudacion-mensual", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var data = await repo.GetRecaudacionMensualAsync(ct);
            return Results.Ok(data);
        });

        // Procedimientos Almacenados
        group.MapGet("/procedimientos/asistencias-socio", async (
            [FromQuery] long socioId,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var fechaDesde = desde ?? DateTime.Today.AddDays(-60);
            var fechaHasta = hasta ?? DateTime.Today;

            var result = await repo.GetReporteAsistenciasSocioAsync(socioId, fechaDesde, fechaHasta, ct);
            return Results.Ok(result);
        });

        group.MapPost("/procedimientos/registrar-pago", async (
            [FromBody] RegistrarPagoRequest request,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            if (request.Monto <= 0)
            {
                return Results.BadRequest(new { message = "El monto debe ser un valor positivo mayor a cero." });
            }

            try
            {
                var fechaPago = request.FechaPago ?? DateTime.Now;
                await repo.RegistrarPagoMembresiaAsync(request.IDSocioMembresia, request.Monto, fechaPago, ct);
                return Results.Ok(new
                {
                    message = "Pago registrado exitosamente en la transacción de SQL Server.",
                    contratoId = request.IDSocioMembresia,
                    monto = request.Monto,
                    fecha = fechaPago
                });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message, sqlErrorNumber = ex.Number });
            }
        });

        // Laboratorio de Triggers (Cupos de Clases)
        group.MapGet("/triggers/inscripciones", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var list = await repo.GetInscripcionesRecientesAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/triggers/inscribir", async (
            [FromBody] InscribirSocioRequest request,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                var fecha = request.FechaInscripcion ?? DateTime.Today;
                long idInscripcion = await repo.InscribirSocioClaseAsync(request.IDSocio, request.IDClase, fecha, ct);
                return Results.Ok(new
                {
                    idInscripcion,
                    message = "Inscripción completada. El trigger 'trg_Inscripcion_ActualizarCupo' descontó 1 vacante automáticamente."
                });
            }
            catch (SqlException ex)
            {
                // El trigger lanza RAISERROR si no hay cupo
                return Results.BadRequest(new { message = ex.Message, sqlErrorNumber = ex.Number, triggerRolledBack = true });
            }
        });

        group.MapDelete("/triggers/cancelar/{id:long}", async (long id, IGimnasioRepository repo, CancellationToken ct) =>
        {
            try
            {
                await repo.CancelarInscripcionClaseAsync(id, ct);
                return Results.Ok(new
                {
                    message = $"Inscripción {id} cancelada. El trigger 'trg_Cancelacion_ActualizarCupo' restituyó 1 vacante a la clase."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Catálogos para selectors en UI
        group.MapGet("/catalogos/socios", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var socios = await repo.GetSociosCatalogAsync(ct);
            return Results.Ok(socios);
        });

        group.MapGet("/catalogos/clases", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var clases = await repo.GetClasesCatalogAsync(ct);
            return Results.Ok(clases);
        });

        group.MapGet("/catalogos/contratos", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var contratos = await repo.GetContratosCatalogAsync(ct);
            return Results.Ok(contratos);
        });

        group.MapGet("/catalogos/roles", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var roles = await repo.GetRolesCatalogAsync(ct);
            return Results.Ok(roles);
        });

        // Gestión de Personas (Cargas y Bajas con Herencia)
        group.MapGet("/personas", async (
            [FromQuery] string? busqueda,
            [FromQuery] string? tipo,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var personas = await repo.GetPersonasAsync(busqueda, tipo, ct);
            return Results.Ok(personas);
        });

        group.MapPost("/personas", async (
            [FromBody] CrearPersonaRequest request,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                long idPersona = await repo.CrearPersonaAsync(request, ct);
                return Results.Ok(new
                {
                    idPersona,
                    message = "Persona registrada exitosamente junto a sus clasificaciones."
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message, sqlErrorNumber = ex.Number });
            }
        });

        group.MapPut("/personas/{id:long}/toggle-activo", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                bool nuevoEstado = await repo.ToggleActivoPersonaAsync(id, ct);
                return Results.Ok(new
                {
                    idPersona = id,
                    activo = nuevoEstado,
                    message = nuevoEstado ? "Persona reactivada exitosamente." : "Persona dada de baja lógica (inactiva)."
                });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapDelete("/personas/{id:long}", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                await repo.EliminarPersonaAsync(id, ct);
                return Results.Ok(new
                {
                    idPersona = id,
                    message = "Registro de la persona eliminado permanentemente del sistema."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Membresías y Contratos de Socios
        group.MapGet("/membresias/planes", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var planes = await repo.GetPlanesMembresiaAsync(ct);
            return Results.Ok(planes);
        });

        group.MapGet("/membresias/contratos", async (
            [FromQuery] string? busqueda,
            [FromQuery] bool? soloActivas,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var contratos = await repo.GetContratosMembresiasAsync(busqueda, soloActivas ?? false, ct);
            return Results.Ok(contratos);
        });

        group.MapPost("/membresias/contratos", async (
            [FromBody] RegistrarSocioMembresiaRequest request,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                long idSocioMembresia = await repo.RegistrarSocioMembresiaAsync(request, ct);
                return Results.Ok(new
                {
                    idSocioMembresia,
                    message = "Membresía asignada y registrada correctamente al socio."
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message, sqlErrorNumber = ex.Number });
            }
        });

        group.MapPut("/membresias/contratos/{id:long}/toggle-activa", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                bool nuevaActiva = await repo.ToggleActivaSocioMembresiaAsync(id, ct);
                return Results.Ok(new
                {
                    idSocioMembresia = id,
                    activa = nuevaActiva,
                    message = nuevaActiva ? "Membresía del socio reactivada exitosamente." : "Membresía pausada/dada de baja exitosamente."
                });
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // Explorador de Tablas
        group.MapGet("/tablas", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var tablas = await repo.GetTablesSummaryAsync(ct);
            return Results.Ok(tablas);
        });

        group.MapGet("/tablas/{nombre}", async (
            string nombre,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            try
            {
                var data = await repo.GetTableDataAsync(nombre, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, ct);
                return Results.Ok(data);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (SqlException ex)
            {
                return Results.Problem(detail: ex.Message);
            }
        });

        // Consola SQL Libre
        group.MapPost("/sql/execute", async (
            [FromBody] ExecuteSqlQueryRequest request,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Sql))
            {
                return Results.BadRequest(new { message = "Debes ingresar una consulta SQL para ejecutar." });
            }

            try
            {
                int maxRows = request.MaxRows ?? 200;
                var result = await repo.ExecuteDynamicSqlAsync(request.Sql, maxRows, ct);
                return Results.Ok(result);
            }
            catch (SqlException ex)
            {
                return Results.BadRequest(new
                {
                    message = ex.Message,
                    lineNumber = ex.LineNumber,
                    sqlErrorNumber = ex.Number,
                    procedure = ex.Procedure
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return group;
    }
}
