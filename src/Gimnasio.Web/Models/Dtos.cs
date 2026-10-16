namespace Gimnasio.Web.Models;

public sealed record SocioMembresiaActivaDto(
    long IDSocio,
    string Socio,
    string Email,
    string PlanContratado,
    DateTime FechaInicio,
    DateTime FechaVencimiento,
    int DiasRestantes
);

public sealed record OcupacionClaseDto(
    long IDClase,
    string Clase,
    DateTime Horario,
    string Profesor,
    int CupoMaximo,
    int CupoDisponible,
    int AlumnosInscritos,
    decimal PorcentajeOcupacion
);

public sealed record RecaudacionMensualDto(
    int Anio,
    int Mes,
    int CantidadTransacciones,
    decimal TotalRecaudado,
    decimal TicketPromedio
);

public sealed record AsistenciaReporteDto(
    DateTime FechaAsistencia,
    string Clase,
    DateTime HorarioClase,
    string EstadoAsistencia
);

public sealed record SocioSelectItemDto(
    long IDSocio,
    string NombreCompleto,
    string Email
);

public sealed record ClaseSelectItemDto(
    long IDClase,
    string Nombre,
    DateTime Horario,
    int CupoDisponible,
    int CupoMaximo
);

public sealed record ContratoSelectItemDto(
    long IDSocioMembresia,
    string Socio,
    string PlanContratado,
    decimal PrecioPlan,
    bool Activa,
    DateTime FechaVencimiento
);

public sealed record InscripcionDto(
    long IDInscripcion,
    long IDSocio,
    string Socio,
    long IDClase,
    string Clase,
    DateTime FechaInscripcion
);

public sealed record RegistrarPagoRequest(
    long IDSocioMembresia,
    decimal Monto,
    DateTime? FechaPago
);

public sealed record InscribirSocioRequest(
    long IDSocio,
    long IDClase,
    DateTime? FechaInscripcion
);

public sealed record ExecuteSqlQueryRequest(
    string Sql,
    int? MaxRows
);

public sealed record TableSummaryDto(
    string TableName,
    long EstimatedRows
);

public sealed record DynamicQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    long ExecutionTimeMs,
    int TotalRows,
    bool Truncated
);

public sealed record TableDataResult(
    string TableName,
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int Page,
    int PageSize,
    int TotalRows
);

public sealed record DashboardStatsDto(
    int TotalSocios,
    int TotalEmpleados,
    int ClasesActivas,
    int MembresiasActivas,
    decimal RecaudacionHistorica,
    long DatabaseLatencyMs,
    string SqlVersion
);

public sealed record PersonaListItemDto(
    long IDPersona,
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    DateTime FechaNacimiento,
    bool Activo,
    long? IDSocio,
    DateTime? FechaRegistro,
    long? IDEmpleado,
    int? IDRol,
    string? NombreRol,
    DateTime? FechaContratacion,
    string Tipo
);

public sealed record CrearPersonaRequest(
    string Nombre,
    string Apellido,
    string Email,
    string? Telefono,
    DateTime FechaNacimiento,
    bool EsSocio,
    DateTime? FechaRegistro,
    bool EsEmpleado,
    int? IDRol,
    DateTime? FechaContratacion
);

public sealed record RolDto(
    int IDRol,
    string NombreRol
);

public sealed record MembresiaPlanDto(
    int IDMembresia,
    string Nombre,
    decimal Precio,
    int DuracionDias
);

public sealed record RegistrarSocioMembresiaRequest(
    long IDSocio,
    int IDMembresia,
    DateTime FechaInicio,
    DateTime? FechaFin,
    bool? Activa,
    bool? RegistrarPagoInicial,
    decimal? MontoPagoInicial,
    string? MetodoPagoInicial
);

public sealed record SocioMembresiaDetalleDto(
    long IDSocioMembresia,
    long IDSocio,
    string NombreSocio,
    string ApellidoSocio,
    string EmailSocio,
    int IDMembresia,
    string NombreMembresia,
    decimal PrecioPlan,
    int DuracionDias,
    DateTime FechaInicio,
    DateTime FechaFin,
    bool Activa,
    int DiasRestantes,
    int CantidadPagos,
    decimal TotalPagado
);

