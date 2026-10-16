using Gimnasio.Web.Models;

namespace Gimnasio.Web.Data;

public interface IGimnasioRepository
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<IEnumerable<SocioMembresiaActivaDto>> GetSociosMembresiasActivasAsync(CancellationToken ct = default);
    Task<IEnumerable<OcupacionClaseDto>> GetOcupacionClasesAsync(CancellationToken ct = default);
    Task<IEnumerable<RecaudacionMensualDto>> GetRecaudacionMensualAsync(CancellationToken ct = default);
    Task<IEnumerable<AsistenciaReporteDto>> GetReporteAsistenciasSocioAsync(long socioId, DateTime fechaDesde, DateTime fechaHasta, CancellationToken ct = default);
    Task RegistrarPagoMembresiaAsync(long socioMembresiaId, decimal monto, DateTime fechaPago, CancellationToken ct = default);
    Task<long> InscribirSocioClaseAsync(long socioId, long claseId, DateTime fechaInscripcion, CancellationToken ct = default);
    Task CancelarInscripcionClaseAsync(long inscripcionId, CancellationToken ct = default);
    Task<IEnumerable<InscripcionDto>> GetInscripcionesRecientesAsync(CancellationToken ct = default);
    Task<IEnumerable<SocioSelectItemDto>> GetSociosCatalogAsync(CancellationToken ct = default);
    Task<IEnumerable<ClaseSelectItemDto>> GetClasesCatalogAsync(CancellationToken ct = default);
    Task<IEnumerable<ContratoSelectItemDto>> GetContratosCatalogAsync(CancellationToken ct = default);
    Task<IEnumerable<TableSummaryDto>> GetTablesSummaryAsync(CancellationToken ct = default);
    Task<TableDataResult> GetTableDataAsync(string tableName, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<DynamicQueryResult> ExecuteDynamicSqlAsync(string sql, int maxRows = 200, CancellationToken ct = default);
    Task<IEnumerable<PersonaListItemDto>> GetPersonasAsync(string? busqueda = null, string? tipo = null, CancellationToken ct = default);
    Task<long> CrearPersonaAsync(CrearPersonaRequest request, CancellationToken ct = default);
    Task<bool> ToggleActivoPersonaAsync(long personaId, CancellationToken ct = default);
    Task EliminarPersonaAsync(long personaId, CancellationToken ct = default);
    Task<IEnumerable<RolDto>> GetRolesCatalogAsync(CancellationToken ct = default);
}
