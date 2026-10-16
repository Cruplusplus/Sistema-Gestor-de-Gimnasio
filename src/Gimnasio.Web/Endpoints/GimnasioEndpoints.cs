using FluentValidation;
using Gimnasio.Web.Data;
using Gimnasio.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gimnasio.Web.Endpoints;

public static class GimnasioEndpoints
{
    public static RouteGroupBuilder MapGimnasioEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        // Dashboard & KPIs
        group.MapGet("/dashboard/stats", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var stats = await repo.GetDashboardStatsAsync(ct);
            return Results.Ok(stats);
        });

        // 3 Vistas Complejas (Requisitos UTN)
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
            IValidator<RegistrarPagoRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            var fechaPago = request.FechaPago ?? DateTime.Now;
            await repo.RegistrarPagoMembresiaAsync(request.IDSocioMembresia, request.Monto, fechaPago, ct);
            return Results.Ok(new
            {
                message = "Pago registrado exitosamente en la transacción de SQL Server.",
                contratoId = request.IDSocioMembresia,
                monto = request.Monto,
                fecha = fechaPago
            });
        });

        // Laboratorio de Triggers (Cupos de Clases)
        group.MapGet("/triggers/inscripciones", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var list = await repo.GetInscripcionesRecientesAsync(ct);
            return Results.Ok(list);
        });

        group.MapPost("/triggers/inscribir", async (
            [FromBody] InscribirSocioRequest request,
            IValidator<InscribirSocioRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            var fecha = request.FechaInscripcion ?? DateTime.Today;
            long idInscripcion = await repo.InscribirSocioClaseAsync(request.IDSocio, request.IDClase, fecha, ct);
            return Results.Ok(new
            {
                idInscripcion,
                message = "Inscripción completada. El trigger 'trg_Inscripcion_ActualizarCupo' descontó 1 vacante automáticamente."
            });
        });

        group.MapDelete("/triggers/cancelar/{id:long}", async (long id, IGimnasioRepository repo, CancellationToken ct) =>
        {
            await repo.CancelarInscripcionClaseAsync(id, ct);
            return Results.Ok(new
            {
                message = $"Inscripción #{id} cancelada. El trigger 'trg_Cancelacion_ActualizarCupo' restituyó 1 vacante a la clase."
            });
        });

        // Catálogos auxiliares para selectores UI
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

        group.MapGet("/catalogos/profesores", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var profesores = await repo.GetProfesoresCatalogAsync(ct);
            return Results.Ok(profesores);
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

        // Gestión de Clases del Complejo
        group.MapPost("/operaciones/clases", async (
            [FromBody] CrearClaseRequest request,
            IValidator<CrearClaseRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            long idClase = await repo.CrearClaseAsync(request, ct);
            return Results.Ok(new
            {
                idClase,
                message = $"Clase '{request.Nombre}' creada y agregada al cronograma exitosamente."
            });
        });

        group.MapDelete("/operaciones/clases/{id:long}", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            await repo.EliminarClaseAsync(id, ct);
            return Results.Ok(new
            {
                idClase = id,
                message = "Clase cancelada y removida del cronograma."
            });
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
            IValidator<CrearPersonaRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            long idPersona = await repo.CrearPersonaAsync(request, ct);
            return Results.Ok(new
            {
                idPersona,
                message = "Persona registrada exitosamente junto a sus clasificaciones."
            });
        });

        group.MapPut("/personas/{id:long}/toggle-activo", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            bool nuevoEstado = await repo.ToggleActivoPersonaAsync(id, ct);
            return Results.Ok(new
            {
                idPersona = id,
                activo = nuevoEstado,
                message = nuevoEstado ? "Persona reactivada exitosamente." : "Persona dada de baja lógica (inactiva)."
            });
        });

        group.MapDelete("/personas/{id:long}", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            await repo.EliminarPersonaAsync(id, ct);
            return Results.Ok(new
            {
                idPersona = id,
                message = "Registro de la persona eliminado permanentemente del sistema."
            });
        });

        // Membresías y Contratos de Socios
        group.MapGet("/membresias/planes", async (IGimnasioRepository repo, CancellationToken ct) =>
        {
            var planes = await repo.GetPlanesMembresiaAsync(ct);
            return Results.Ok(planes);
        });

        group.MapPost("/membresias/planes", async (
            [FromBody] CrearPlanMembresiaRequest request,
            IValidator<CrearPlanMembresiaRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            long idPlan = await repo.CrearPlanMembresiaAsync(request, ct);
            return Results.Ok(new
            {
                idPlan,
                message = $"Plan de membresía '{request.Nombre}' creado exitosamente."
            });
        });

        group.MapPut("/membresias/planes/{id:int}/precio", async (
            int id,
            [FromBody] ActualizarPrecioPlanRequest request,
            IValidator<ActualizarPrecioPlanRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            await repo.ActualizarPrecioPlanAsync(id, request.NuevoPrecio, ct);
            return Results.Ok(new
            {
                idPlan = id,
                nuevoPrecio = request.NuevoPrecio,
                message = "Arancel del plan de membresía actualizado con éxito."
            });
        });

        group.MapGet("/membresias/contratos", async (
            [FromQuery] string? busqueda,
            [FromQuery] bool? soloActivas,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var contratos = await repo.GetContratosMembresiasAsync(busqueda, soloActivas, ct);
            return Results.Ok(contratos);
        });

        group.MapPost("/membresias/contratos", async (
            [FromBody] RegistrarSocioMembresiaRequest request,
            IValidator<RegistrarSocioMembresiaRequest> validator,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            var valResult = await validator.ValidateAsync(request, ct);
            if (!valResult.IsValid)
            {
                return Results.ValidationProblem(valResult.ToDictionary());
            }

            long idSocioMembresia = await repo.RegistrarSocioMembresiaAsync(request, ct);
            return Results.Ok(new
            {
                idSocioMembresia,
                message = "Membresía asignada y registrada correctamente al socio."
            });
        });

        group.MapPut("/membresias/contratos/{id:long}/toggle-activa", async (
            long id,
            IGimnasioRepository repo,
            CancellationToken ct) =>
        {
            bool nuevaActiva = await repo.ToggleActivaSocioMembresiaAsync(id, ct);
            return Results.Ok(new
            {
                idSocioMembresia = id,
                activa = nuevaActiva,
                message = nuevaActiva ? "Membresía del socio reactivada exitosamente." : "Membresía pausada/dada de baja exitosamente."
            });
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
            var data = await repo.GetTableDataAsync(nombre, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, ct);
            return Results.Ok(data);
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

            int maxRows = request.MaxRows ?? 200;
            var result = await repo.ExecuteDynamicSqlAsync(request.Sql, maxRows, ct);
            return Results.Ok(result);
        });

        return group;
    }
}
