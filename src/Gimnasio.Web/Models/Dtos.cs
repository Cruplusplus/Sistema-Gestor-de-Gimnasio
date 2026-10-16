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
