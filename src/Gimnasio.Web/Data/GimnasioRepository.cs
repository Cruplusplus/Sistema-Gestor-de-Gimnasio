using System.Data;
using System.Diagnostics;
using Dapper;
using Gimnasio.Web.Models;
using Microsoft.Data.SqlClient;

namespace Gimnasio.Web.Data;

public sealed class GimnasioRepository(IDbConnectionFactory connectionFactory) : IGimnasioRepository
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "Personas",
        "Roles",
        "Membresias",
        "Socios",
        "Empleados",
        "Clases",
        "SocioMembresias",
        "Pagos",
        "InscripcionesClase",
        "HistorialAsistencias"
    };

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        sw.Stop();
        long latencyMs = sw.ElapsedMilliseconds;

        const string sql = """
            SELECT 
                (SELECT COUNT(*) FROM Socios) AS TotalSocios,
                (SELECT COUNT(*) FROM Empleados) AS TotalEmpleados,
                (SELECT COUNT(*) FROM Clases) AS ClasesActivas,
                (SELECT COUNT(*) FROM SocioMembresias WHERE Activa = 1) AS MembresiasActivas,
                ISNULL((SELECT SUM(Monto) FROM Pagos), 0) AS RecaudacionHistorica,
                @@VERSION AS SqlVersion;
        """;

        var result = await connection.QuerySingleAsync(new CommandDefinition(sql, cancellationToken: ct));

        string fullVersion = (string)result.SqlVersion;
        string shortVersion = fullVersion.Split('\n')[0];

        return new DashboardStatsDto(
            TotalSocios: (int)result.TotalSocios,
            TotalEmpleados: (int)result.TotalEmpleados,
            ClasesActivas: (int)result.ClasesActivas,
            MembresiasActivas: (int)result.MembresiasActivas,
            RecaudacionHistorica: (decimal)result.RecaudacionHistorica,
            DatabaseLatencyMs: latencyMs,
            SqlVersion: shortVersion
        );
    }

    public async Task<IEnumerable<SocioMembresiaActivaDto>> GetSociosMembresiasActivasAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                IDSocio,
                Socio,
                Email,
                PlanContratado,
                FechaInicio,
                FechaVencimiento,
                DiasRestantes
            FROM vw_SociosMembresiasActivas
            ORDER BY DiasRestantes ASC;
        """;

        return await connection.QueryAsync<SocioMembresiaActivaDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<OcupacionClaseDto>> GetOcupacionClasesAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                IDClase,
                Clase,
                Horario,
                Profesor,
                CupoMaximo,
                CupoDisponible,
                AlumnosInscritos,
                PorcentajeOcupacion
            FROM vw_OcupacionClases
            ORDER BY Horario ASC;
        """;

        return await connection.QueryAsync<OcupacionClaseDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<RecaudacionMensualDto>> GetRecaudacionMensualAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                Anio,
                Mes,
                CantidadTransacciones,
                TotalRecaudado,
                TicketPromedio
            FROM vw_RecaudacionMensual
            ORDER BY Anio DESC, Mes DESC;
        """;

        return await connection.QueryAsync<RecaudacionMensualDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<AsistenciaReporteDto>> GetReporteAsistenciasSocioAsync(long socioId, DateTime fechaDesde, DateTime fechaHasta, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("@IDSocio", socioId, DbType.Int64);
        parameters.Add("@FechaDesde", fechaDesde.Date, DbType.Date);
        parameters.Add("@FechaHasta", fechaHasta.Date, DbType.Date);

        var cmd = new CommandDefinition(
            "sp_ReporteAsistenciasSocio",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        return await connection.QueryAsync<AsistenciaReporteDto>(cmd);
    }

    public async Task RegistrarPagoMembresiaAsync(long socioMembresiaId, decimal monto, DateTime fechaPago, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("@IDSocioMembresia", socioMembresiaId, DbType.Int64);
        parameters.Add("@Monto", monto, DbType.Decimal);
        parameters.Add("@FechaPago", fechaPago, DbType.DateTime);

        var cmd = new CommandDefinition(
            "sp_RegistrarPagoMembresia",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct
        );

        await connection.ExecuteAsync(cmd);
    }

    public async Task<long> InscribirSocioClaseAsync(long socioId, long claseId, DateTime fechaInscripcion, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Al insertar en InscripcionesClase, se dispara el trigger trg_Inscripcion_ActualizarCupo
        // Si el cupo está agotado, el trigger lanza RAISERROR y hace ROLLBACK
        const string sql = """
            INSERT INTO InscripcionesClase (IDSocio, IDClase, FechaInscripcion)
            VALUES (@IDSocio, @IDClase, @FechaInscripcion);
            SELECT CAST(SCOPE_IDENTITY() as BIGINT);
        """;

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            IDSocio = socioId,
            IDClase = claseId,
            FechaInscripcion = fechaInscripcion.Date
        }, cancellationToken: ct));
    }

    public async Task CancelarInscripcionClaseAsync(long inscripcionId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Al eliminar de InscripcionesClase, se dispara el trigger trg_Cancelacion_ActualizarCupo restituyendo el cupo
        const string sql = """
            DELETE FROM InscripcionesClase 
            WHERE IDInscripcion = @IDInscripcion;
        """;

        int affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { IDInscripcion = inscripcionId }, cancellationToken: ct));
        if (affected == 0)
        {
            throw new KeyNotFoundException($"No se encontró la inscripción con ID {inscripcionId}.");
        }
    }

    public async Task<IEnumerable<InscripcionDto>> GetInscripcionesRecientesAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT TOP 50
                i.IDInscripcion,
                i.IDSocio,
                p.Nombre + ' ' + p.Apellido AS Socio,
                i.IDClase,
                c.Nombre AS Clase,
                i.FechaInscripcion
            FROM InscripcionesClase i
            INNER JOIN Socios s ON i.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Clases c ON i.IDClase = c.IDClase
            ORDER BY i.IDInscripcion DESC;
        """;

        return await connection.QueryAsync<InscripcionDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<SocioSelectItemDto>> GetSociosCatalogAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                s.IDSocio,
                p.Nombre + ' ' + p.Apellido AS NombreCompleto,
                p.Email
            FROM Socios s
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            ORDER BY p.Apellido, p.Nombre;
        """;

        return await connection.QueryAsync<SocioSelectItemDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<ClaseSelectItemDto>> GetClasesCatalogAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                IDClase,
                Nombre,
                Horario,
                CupoDisponible,
                CupoMaximo
            FROM Clases
            ORDER BY Horario ASC;
        """;

        return await connection.QueryAsync<ClaseSelectItemDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<ContratoSelectItemDto>> GetContratosCatalogAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                sm.IDSocioMembresia,
                p.Nombre + ' ' + p.Apellido AS Socio,
                m.Nombre AS PlanContratado,
                m.Precio AS PrecioPlan,
                sm.Activa,
                sm.FechaVencimiento
            FROM SocioMembresias sm
            INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Membresias m ON sm.IDMembresia = m.IDMembresia
            ORDER BY sm.IDSocioMembresia DESC;
        """;

        return await connection.QueryAsync<ContratoSelectItemDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<TableSummaryDto>> GetTablesSummaryAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                t.TABLE_NAME AS TableName,
                p.rows AS EstimatedRows
            FROM INFORMATION_SCHEMA.TABLES t
            INNER JOIN sys.tables st ON t.TABLE_NAME = st.name
            INNER JOIN sys.partitions p ON st.object_id = p.object_id AND p.index_id IN (0, 1)
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_NAME;
        """;

        return await connection.QueryAsync<TableSummaryDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<TableDataResult> GetTableDataAsync(string tableName, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!AllowedTables.Contains(tableName))
        {
            throw new ArgumentException($"Tabla '{tableName}' no permitida o inexistente.", nameof(tableName));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);
        int offset = (page - 1) * pageSize;

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        string countSql = $"SELECT COUNT(*) FROM [{tableName}];";
        int totalRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, cancellationToken: ct));

        // Obtener la columna de la clave primaria o la primera columna para ORDER BY estable
        string pkSql = $"""
            SELECT TOP 1 COLUMN_NAME 
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_NAME = '{tableName}' AND CONSTRAINT_NAME LIKE 'PK_%';
        """;
        string? pkColumn = await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(pkSql, cancellationToken: ct));

        if (string.IsNullOrEmpty(pkColumn))
        {
            string firstColSql = $"SELECT TOP 1 COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY ORDINAL_POSITION;";
            pkColumn = await connection.QueryFirstAsync<string>(new CommandDefinition(firstColSql, cancellationToken: ct));
        }

        string dataSql = $"""
            SELECT * FROM [{tableName}]
            ORDER BY [{pkColumn}] ASC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
        """;

        await using var command = new SqlCommand(dataSql, connection);
        command.Parameters.AddWithValue("@Offset", offset);
        command.Parameters.AddWithValue("@PageSize", pageSize);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>(columns.Count);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object val = reader.GetValue(i);
                row[columns[i]] = val == DBNull.Value ? null : val;
            }
            rows.Add(row);
        }

        return new TableDataResult(tableName, columns, rows, page, pageSize, totalRows);
    }

    public async Task<DynamicQueryResult> ExecuteDynamicSqlAsync(string sql, int maxRows = 200, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("La consulta SQL no puede estar vacía.");
        }

        maxRows = Math.Clamp(maxRows, 1, 1000);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 30; // 30 segundos límite para consultas interactivas

        var sw = Stopwatch.StartNew();
        await using var reader = await command.ExecuteReaderAsync(ct);

        var columns = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        var rows = new List<Dictionary<string, object?>>();
        bool truncated = false;

        while (await reader.ReadAsync(ct))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }

            var row = new Dictionary<string, object?>(columns.Count);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                object val = reader.GetValue(i);
                row[columns[i]] = val == DBNull.Value ? null : val;
            }
            rows.Add(row);
        }

        sw.Stop();

        return new DynamicQueryResult(
            Columns: columns,
            Rows: rows,
            ExecutionTimeMs: sw.ElapsedMilliseconds,
            TotalRows: rows.Count,
            Truncated: truncated
        );
    }
}
