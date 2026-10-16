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
}

