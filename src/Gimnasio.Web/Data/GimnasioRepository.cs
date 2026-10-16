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
                sm.IDSocioMembresia,
                sm.IDSocio,
                p.Nombre + ' ' + p.Apellido AS Socio,
                p.Email,
                m.Nombre AS PlanContratado,
                m.Precio AS PrecioPlan,
                ISNULL(SUM(pa.Monto), 0) AS TotalPagado,
                CASE 
                    WHEN (m.Precio - ISNULL(SUM(pa.Monto), 0)) < 0 THEN 0 
                    ELSE (m.Precio - ISNULL(SUM(pa.Monto), 0)) 
                END AS SaldoPendiente,
                CASE 
                    WHEN ISNULL(SUM(pa.Monto), 0) >= m.Precio THEN 'Pagada'
                    WHEN ISNULL(SUM(pa.Monto), 0) > 0 THEN 'Parcial'
                    ELSE 'Impaga'
                END AS EstadoPago,
                sm.FechaInicio,
                sm.FechaVencimiento,
                DATEDIFF(day, GETDATE(), sm.FechaVencimiento) AS DiasRestantes
            FROM SocioMembresias sm
            INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Membresias m ON sm.IDMembresia = m.IDMembresia
            LEFT JOIN Pagos pa ON sm.IDSocioMembresia = pa.IDSocioMembresia
            WHERE sm.Activa = 1
            GROUP BY 
                sm.IDSocioMembresia,
                sm.IDSocio,
                p.Nombre,
                p.Apellido,
                p.Email,
                m.Nombre,
                m.Precio,
                sm.FechaInicio,
                sm.FechaVencimiento
            ORDER BY sm.FechaVencimiento ASC;
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

    public async Task<IEnumerable<PagoListItemDto>> GetPagosRecientesAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT TOP (@Limit)
                pa.IDPago,
                pa.IDSocioMembresia,
                s.IDSocio,
                p.Nombre + ' ' + p.Apellido AS Socio,
                m.Nombre AS PlanContratado,
                pa.Monto,
                pa.FechaPago
            FROM Pagos pa
            INNER JOIN SocioMembresias sm ON pa.IDSocioMembresia = sm.IDSocioMembresia
            INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Membresias m ON sm.IDMembresia = m.IDMembresia
            ORDER BY pa.IDPago DESC;
        """;

        return await connection.QueryAsync<PagoListItemDto>(new CommandDefinition(sql, new { Limit = Math.Clamp(limit, 1, 200) }, cancellationToken: ct));
    }

    public async Task EliminarPagoAsync(long idPago, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = "DELETE FROM Pagos WHERE IDPago = @IdPago;";
        int affected = await connection.ExecuteAsync(new CommandDefinition(sql, new { IdPago = idPago }, cancellationToken: ct));
        if (affected == 0)
        {
            throw new KeyNotFoundException($"El comprobante de cobro #{idPago} no existe.");
        }
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
                ISNULL(SUM(pa.Monto), 0) AS TotalPagado,
                CASE 
                    WHEN (m.Precio - ISNULL(SUM(pa.Monto), 0)) < 0 THEN 0 
                    ELSE (m.Precio - ISNULL(SUM(pa.Monto), 0)) 
                END AS SaldoPendiente,
                CASE 
                    WHEN ISNULL(SUM(pa.Monto), 0) >= m.Precio THEN 'Pagada'
                    WHEN ISNULL(SUM(pa.Monto), 0) > 0 THEN 'Parcial'
                    ELSE 'Impaga'
                END AS EstadoPago,
                sm.Activa,
                sm.FechaVencimiento
            FROM SocioMembresias sm
            INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Membresias m ON sm.IDMembresia = m.IDMembresia
            LEFT JOIN Pagos pa ON sm.IDSocioMembresia = pa.IDSocioMembresia
            GROUP BY 
                sm.IDSocioMembresia,
                p.Nombre,
                p.Apellido,
                m.Nombre,
                m.Precio,
                sm.Activa,
                sm.FechaVencimiento
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

    public async Task<IEnumerable<PersonaListItemDto>> GetPersonasAsync(string? busqueda = null, string? tipo = null, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        const string sql = """
            SELECT 
                p.IDPersona,
                p.Nombre,
                p.Apellido,
                p.Email,
                p.Telefono,
                p.FechaNacimiento,
                p.Activo,
                s.IDSocio,
                s.FechaRegistro,
                e.IDEmpleado,
                e.IDRol,
                r.NombreRol,
                e.FechaContratacion,
                CASE 
                    WHEN s.IDSocio IS NOT NULL AND e.IDEmpleado IS NOT NULL THEN 'Socio y Empleado'
                    WHEN s.IDSocio IS NOT NULL THEN 'Socio'
                    WHEN e.IDEmpleado IS NOT NULL THEN 'Empleado'
                    ELSE 'General'
                END AS Tipo
            FROM Personas p
            LEFT JOIN Socios s ON p.IDPersona = s.IDPersona
            LEFT JOIN Empleados e ON p.IDPersona = e.IDPersona
            LEFT JOIN Roles r ON e.IDRol = r.IDRol
            WHERE (@Busqueda IS NULL OR 
                   p.Nombre LIKE '%' + @Busqueda + '%' OR 
                   p.Apellido LIKE '%' + @Busqueda + '%' OR 
                   p.Email LIKE '%' + @Busqueda + '%' OR 
                   p.Telefono LIKE '%' + @Busqueda + '%')
              AND (@Tipo IS NULL OR 
                   (@Tipo = 'socio' AND s.IDSocio IS NOT NULL) OR 
                   (@Tipo = 'empleado' AND e.IDEmpleado IS NOT NULL) OR
                   (@Tipo = 'mixto' AND s.IDSocio IS NOT NULL AND e.IDEmpleado IS NOT NULL))
            ORDER BY p.IDPersona DESC;
        """;

        return await connection.QueryAsync<PersonaListItemDto>(new CommandDefinition(sql, new
        {
            Busqueda = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim(),
            Tipo = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim().ToLowerInvariant()
        }, cancellationToken: ct));
    }

    public async Task<long> CrearPersonaAsync(CrearPersonaRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
        {
            throw new ArgumentException("Nombre y Apellido son campos requeridos.");
        }
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("El Email es un campo requerido.");
        }

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Validar unicidad de email
            const string checkEmailSql = "SELECT COUNT(1) FROM Personas WHERE Email = @Email;";
            int exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkEmailSql, new { request.Email }, (SqlTransaction)transaction, cancellationToken: ct));
            if (exists > 0)
            {
                throw new InvalidOperationException($"El correo '{request.Email}' ya se encuentra registrado.");
            }

            // Insertar en Personas
            const string insertPersonaSql = """
                INSERT INTO Personas (Nombre, Apellido, Email, Telefono, FechaNacimiento, Activo)
                OUTPUT INSERTED.IDPersona
                VALUES (@Nombre, @Apellido, @Email, @Telefono, @FechaNacimiento, 1);
            """;

            long idPersona = await connection.ExecuteScalarAsync<long>(new CommandDefinition(insertPersonaSql, new
            {
                Nombre = request.Nombre.Trim(),
                Apellido = request.Apellido.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                FechaNacimiento = request.FechaNacimiento.Date
            }, (SqlTransaction)transaction, cancellationToken: ct));

            // Si es Socio
            if (request.EsSocio)
            {
                const string insertSocioSql = """
                    INSERT INTO Socios (IDPersona, FechaRegistro)
                    VALUES (@IDPersona, @FechaRegistro);
                """;
                await connection.ExecuteAsync(new CommandDefinition(insertSocioSql, new
                {
                    IDPersona = idPersona,
                    FechaRegistro = (request.FechaRegistro ?? DateTime.Today).Date
                }, (SqlTransaction)transaction, cancellationToken: ct));
            }

            // Si es Empleado
            if (request.EsEmpleado)
            {
                if (!request.IDRol.HasValue)
                {
                    throw new ArgumentException("Debe seleccionar un rol para el empleado.");
                }

                const string insertEmpleadoSql = """
                    INSERT INTO Empleados (IDPersona, IDRol, FechaContratacion)
                    VALUES (@IDPersona, @IDRol, @FechaContratacion);
                """;
                await connection.ExecuteAsync(new CommandDefinition(insertEmpleadoSql, new
                {
                    IDPersona = idPersona,
                    IDRol = request.IDRol.Value,
                    FechaContratacion = (request.FechaContratacion ?? DateTime.Today).Date
                }, (SqlTransaction)transaction, cancellationToken: ct));
            }

            await transaction.CommitAsync(ct);
            return idPersona;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> ToggleActivoPersonaAsync(long personaId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            const string sqlToggle = """
                UPDATE Personas
                SET Activo = CASE WHEN Activo = 1 THEN 0 ELSE 1 END
                OUTPUT INSERTED.Activo
                WHERE IDPersona = @IDPersona;
            """;

            bool nuevoEstado = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sqlToggle, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));

            // Si se desactiva, pausamos las membresias activas del socio
            if (!nuevoEstado)
            {
                const string sqlPausarMembresias = """
                    UPDATE sm
                    SET sm.Activa = 0
                    FROM SocioMembresias sm
                    INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
                    WHERE s.IDPersona = @IDPersona;
                """;
                await connection.ExecuteAsync(new CommandDefinition(sqlPausarMembresias, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));
            }

            await transaction.CommitAsync(ct);
            return nuevoEstado;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task EliminarPersonaAsync(long personaId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Validar dependencias con movimientos transaccionales históricos
        const string checkDependenciesSql = """
            SELECT 
                (SELECT COUNT(1) FROM Pagos pa 
                 INNER JOIN SocioMembresias sm ON pa.IDSocioMembresia = sm.IDSocioMembresia 
                 INNER JOIN Socios s ON sm.IDSocio = s.IDSocio 
                 WHERE s.IDPersona = @IDPersona) AS PagosCount,
                (SELECT COUNT(1) FROM InscripcionesClase ic 
                 INNER JOIN Socios s ON ic.IDSocio = s.IDSocio 
                 WHERE s.IDPersona = @IDPersona) AS InscripcionesCount,
                (SELECT COUNT(1) FROM HistorialAsistencias ha 
                 INNER JOIN Socios s ON ha.IDSocio = s.IDSocio 
                 WHERE s.IDPersona = @IDPersona) AS AsistenciasCount,
                (SELECT COUNT(1) FROM Clases c 
                 INNER JOIN Empleados e ON c.IDProfesor = e.IDEmpleado 
                 WHERE e.IDPersona = @IDPersona) AS ClasesProfesorCount;
        """;

        var dep = await connection.QuerySingleAsync(new CommandDefinition(checkDependenciesSql, new { IDPersona = personaId }, cancellationToken: ct));
        int totalHistorico = (int)dep.PagosCount + (int)dep.InscripcionesCount + (int)dep.AsistenciasCount + (int)dep.ClasesProfesorCount;

        if (totalHistorico > 0)
        {
            throw new InvalidOperationException("La persona posee movimientos históricos vinculados (cobros, asistencias, inscripciones o clases asignadas). Para preservar la trazabilidad contable y operativa, utilice la 'Baja Lógica' (desactivar).");
        }

        // Si no tiene dependencias bloqueantes, eliminamos en cascada ordenada dentro de una transacción
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            // Eliminar contratos de membresía sin pagos
            const string delSocioMembresias = """
                DELETE sm FROM SocioMembresias sm
                INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
                WHERE s.IDPersona = @IDPersona;
            """;
            await connection.ExecuteAsync(new CommandDefinition(delSocioMembresias, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));

            // Eliminar de Socios
            const string delSocios = "DELETE FROM Socios WHERE IDPersona = @IDPersona;";
            await connection.ExecuteAsync(new CommandDefinition(delSocios, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));

            // Eliminar de Empleados
            const string delEmpleados = "DELETE FROM Empleados WHERE IDPersona = @IDPersona;";
            await connection.ExecuteAsync(new CommandDefinition(delEmpleados, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));

            // Eliminar de Personas
            const string delPersonas = "DELETE FROM Personas WHERE IDPersona = @IDPersona;";
            int affected = await connection.ExecuteAsync(new CommandDefinition(delPersonas, new { IDPersona = personaId }, (SqlTransaction)transaction, cancellationToken: ct));

            if (affected == 0)
            {
                throw new KeyNotFoundException($"No se encontró la persona con ID #{personaId}.");
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IEnumerable<RolDto>> GetRolesCatalogAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT MIN(IDRol) AS IDRol, NombreRol 
            FROM Roles 
            GROUP BY NombreRol 
            ORDER BY NombreRol;
        """;
        return await connection.QueryAsync<RolDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<MembresiaPlanDto>> GetPlanesMembresiaAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                MIN(IDMembresia) AS IDMembresia, 
                Nombre, 
                MAX(Precio) AS Precio, 
                MAX(DuracionDias) AS DuracionDias 
            FROM Membresias 
            GROUP BY Nombre 
            ORDER BY Precio ASC;
        """;
        return await connection.QueryAsync<MembresiaPlanDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<IEnumerable<SocioMembresiaDetalleDto>> GetContratosMembresiasAsync(string? busqueda = null, bool? soloActivas = null, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        const string sql = """
            SELECT 
                sm.IDSocioMembresia,
                sm.IDSocio,
                p.Nombre AS NombreSocio,
                p.Apellido AS ApellidoSocio,
                p.Email AS EmailSocio,
                m.IDMembresia,
                m.Nombre AS NombreMembresia,
                m.Precio AS PrecioPlan,
                m.DuracionDias,
                sm.FechaInicio,
                sm.FechaVencimiento AS FechaFin,
                sm.Activa,
                DATEDIFF(day, GETDATE(), sm.FechaVencimiento) AS DiasRestantes,
                COUNT(pa.IDPago) AS CantidadPagos,
                ISNULL(SUM(pa.Monto), 0) AS TotalPagado,
                CASE 
                    WHEN (m.Precio - ISNULL(SUM(pa.Monto), 0)) < 0 THEN 0 
                    ELSE (m.Precio - ISNULL(SUM(pa.Monto), 0)) 
                END AS SaldoPendiente,
                CASE 
                    WHEN ISNULL(SUM(pa.Monto), 0) >= m.Precio THEN 'Pagada'
                    WHEN ISNULL(SUM(pa.Monto), 0) > 0 THEN 'Parcial'
                    ELSE 'Impaga'
                END AS EstadoPago
            FROM SocioMembresias sm
            INNER JOIN Socios s ON sm.IDSocio = s.IDSocio
            INNER JOIN Personas p ON s.IDPersona = p.IDPersona
            INNER JOIN Membresias m ON sm.IDMembresia = m.IDMembresia
            LEFT JOIN Pagos pa ON sm.IDSocioMembresia = pa.IDSocioMembresia
            WHERE (@Busqueda IS NULL OR 
                   p.Nombre LIKE '%' + @Busqueda + '%' OR 
                   p.Apellido LIKE '%' + @Busqueda + '%' OR 
                   p.Email LIKE '%' + @Busqueda + '%' OR 
                   m.Nombre LIKE '%' + @Busqueda + '%')
              AND (@SoloActivas = 0 OR sm.Activa = 1)
            GROUP BY 
                sm.IDSocioMembresia, sm.IDSocio, p.Nombre, p.Apellido, p.Email,
                m.IDMembresia, m.Nombre, m.Precio, m.DuracionDias,
                sm.FechaInicio, sm.FechaVencimiento, sm.Activa
            ORDER BY sm.IDSocioMembresia DESC;
        """;

        return await connection.QueryAsync<SocioMembresiaDetalleDto>(new CommandDefinition(sql, new
        {
            Busqueda = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim(),
            SoloActivas = (soloActivas == true) ? 1 : 0
        }, cancellationToken: ct));
    }

    public async Task<long> RegistrarSocioMembresiaAsync(RegistrarSocioMembresiaRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);

        // Validar existencia de Socio
        const string checkSocioSql = "SELECT COUNT(1) FROM Socios WHERE IDSocio = @IDSocio;";
        int socioExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkSocioSql, new { request.IDSocio }, cancellationToken: ct));
        if (socioExists == 0)
        {
            throw new KeyNotFoundException($"El socio #{request.IDSocio} no existe.");
        }

        // Obtener detalles del plan de membresía
        const string planSql = "SELECT DuracionDias, Precio FROM Membresias WHERE IDMembresia = @IDMembresia;";
        var plan = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(planSql, new { request.IDMembresia }, cancellationToken: ct));
        if (plan == null)
        {
            throw new KeyNotFoundException($"El plan de membresía #{request.IDMembresia} no existe.");
        }

        DateTime fechaInicio = request.FechaInicio.Date;
        DateTime fechaVencimiento = request.FechaFin?.Date ?? fechaInicio.AddDays((int)plan.DuracionDias);
        bool activa = request.Activa ?? true;

        // Iniciar transacción atómica
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            // Validar restricción UNIQUE (IDSocio, IDMembresia, FechaInicio)
            const string checkDupSql = """
                SELECT COUNT(1) FROM SocioMembresias 
                WHERE IDSocio = @IDSocio AND IDMembresia = @IDMembresia AND FechaInicio = @FechaInicio;
            """;
            int dup = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkDupSql, new
            {
                request.IDSocio,
                request.IDMembresia,
                FechaInicio = fechaInicio
            }, (SqlTransaction)transaction, cancellationToken: ct));

            if (dup > 0)
            {
                throw new InvalidOperationException("El socio ya posee un contrato activo para este mismo plan con la misma fecha de inicio.");
            }

            // Insertar contrato en SocioMembresias
            const string insertContratoSql = """
                INSERT INTO SocioMembresias (IDSocio, IDMembresia, FechaInicio, FechaVencimiento, Activa)
                OUTPUT INSERTED.IDSocioMembresia
                VALUES (@IDSocio, @IDMembresia, @FechaInicio, @FechaVencimiento, @Activa);
            """;

            long idSocioMembresia = await connection.ExecuteScalarAsync<long>(new CommandDefinition(insertContratoSql, new
            {
                request.IDSocio,
                request.IDMembresia,
                FechaInicio = fechaInicio,
                FechaVencimiento = fechaVencimiento,
                Activa = activa
            }, (SqlTransaction)transaction, cancellationToken: ct));

            // Si se especificó un pago inicial inmediato
            if (request.RegistrarPagoInicial == true && request.MontoPagoInicial.HasValue && request.MontoPagoInicial.Value > 0)
            {
                const string insertPagoSql = """
                    INSERT INTO Pagos (IDSocioMembresia, Monto, FechaPago)
                    VALUES (@IDSocioMembresia, @Monto, @FechaPago);
                """;
                await connection.ExecuteAsync(new CommandDefinition(insertPagoSql, new
                {
                    IDSocioMembresia = idSocioMembresia,
                    Monto = request.MontoPagoInicial.Value,
                    FechaPago = DateTime.Now
                }, (SqlTransaction)transaction, cancellationToken: ct));
            }

            await transaction.CommitAsync(ct);
            return idSocioMembresia;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> ToggleActivaSocioMembresiaAsync(long socioMembresiaId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            UPDATE SocioMembresias
            SET Activa = CASE WHEN Activa = 1 THEN 0 ELSE 1 END
            OUTPUT INSERTED.Activa
            WHERE IDSocioMembresia = @Id;
        """;

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { Id = socioMembresiaId }, cancellationToken: ct));
    }

    public async Task EliminarSocioMembresiaAsync(long socioMembresiaId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            const string checkSql = "SELECT COUNT(1) FROM SocioMembresias WHERE IDSocioMembresia = @Id;";
            int exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkSql, new { Id = socioMembresiaId }, (SqlTransaction)transaction, cancellationToken: ct));
            if (exists == 0)
            {
                throw new KeyNotFoundException($"El contrato de membresía #{socioMembresiaId} no existe.");
            }

            // Eliminar pagos vinculados al contrato erróneo en cascada atómica
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM Pagos WHERE IDSocioMembresia = @Id;", new { Id = socioMembresiaId }, (SqlTransaction)transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM SocioMembresias WHERE IDSocioMembresia = @Id;", new { Id = socioMembresiaId }, (SqlTransaction)transaction, cancellationToken: ct));

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<long> CrearClaseAsync(CrearClaseRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string checkProfSql = "SELECT COUNT(1) FROM Empleados WHERE IDEmpleado = @IDProfesor;";
        int exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkProfSql, new { request.IDProfesor }, cancellationToken: ct));
        if (exists == 0)
        {
            throw new KeyNotFoundException($"El profesor/empleado #{request.IDProfesor} no existe.");
        }

        const string sql = """
            INSERT INTO Clases (Nombre, Horario, CupoMaximo, CupoDisponible, IDProfesor)
            OUTPUT INSERTED.IDClase
            VALUES (@Nombre, @Horario, @CupoMaximo, @CupoMaximo, @IDProfesor);
        """;

        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            request.Nombre,
            request.Horario,
            request.CupoMaximo,
            request.IDProfesor
        }, cancellationToken: ct));
    }

    public async Task EliminarClaseAsync(long claseId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            const string checkSql = "SELECT COUNT(1) FROM Clases WHERE IDClase = @Id;";
            int exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(checkSql, new { Id = claseId }, (SqlTransaction)transaction, cancellationToken: ct));
            if (exists == 0)
            {
                throw new KeyNotFoundException($"La clase #{claseId} no existe.");
            }

            // Eliminar dependencias en cascada para garantizar atomicidad
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM HistorialAsistencias WHERE IDClase = @Id;", new { Id = claseId }, (SqlTransaction)transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM InscripcionesClase WHERE IDClase = @Id;", new { Id = claseId }, (SqlTransaction)transaction, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM Clases WHERE IDClase = @Id;", new { Id = claseId }, (SqlTransaction)transaction, cancellationToken: ct));

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IEnumerable<ProfesorSelectItemDto>> GetProfesoresCatalogAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT 
                e.IDEmpleado,
                p.Nombre + ' ' + p.Apellido AS NombreCompleto,
                ISNULL(r.NombreRol, 'Personal') AS Rol
            FROM Empleados e
            INNER JOIN Personas p ON e.IDPersona = p.IDPersona
            LEFT JOIN Roles r ON e.IDRol = r.IDRol
            WHERE p.Activo = 1
            ORDER BY p.Apellido, p.Nombre;
        """;
        return await connection.QueryAsync<ProfesorSelectItemDto>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<long> CrearPlanMembresiaAsync(CrearPlanMembresiaRequest request, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO Membresias (Nombre, Precio, DuracionDias)
            OUTPUT INSERTED.IDMembresia
            VALUES (@Nombre, @Precio, @DuracionDias);
        """;
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            request.Nombre,
            request.Precio,
            request.DuracionDias
        }, cancellationToken: ct));
    }

    public async Task ActualizarPrecioPlanAsync(int membresiaId, decimal nuevoPrecio, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(ct);
        const string sql = """
            UPDATE Membresias
            SET Precio = @NuevoPrecio
            WHERE IDMembresia = @Id;
        """;
        int affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = membresiaId,
            NuevoPrecio = nuevoPrecio
        }, cancellationToken: ct));

        if (affected == 0)
        {
            throw new KeyNotFoundException($"El plan de membresía #{membresiaId} no existe.");
        }
    }
}


