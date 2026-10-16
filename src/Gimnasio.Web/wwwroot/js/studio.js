// GimnasioDB - Módulo SQL Studio & Explorador de Tablas (studio.js)

let currentTable = 'Personas';
let currentTablePage = 1;
const currentTablePageSize = 15;
let lastSqlResultData = null;

const queryTemplates = {
  topSocios: `-- Top 5 socios con mayor cantidad de pagos registrados
SELECT TOP 5 
    p.Nombre + ' ' + p.Apellido AS Socio,
    p.Email,
    COUNT(pa.IDPago) AS CantidadPagos,
    SUM(pa.Monto) AS TotalAbonado
FROM Socios s
INNER JOIN Personas p ON s.IDPersona = p.IDPersona
INNER JOIN SocioMembresias sm ON s.IDSocio = sm.IDSocio
INNER JOIN Pagos pa ON sm.IDSocioMembresia = pa.IDSocioMembresia
GROUP BY p.Nombre, p.Apellido, p.Email
ORDER BY TotalAbonado DESC;`,

  clasesOcupadas: `-- Clases con cupo crítico o alta ocupación
SELECT 
    c.IDClase,
    c.Nombre AS Clase,
    c.Horario,
    p.Nombre + ' ' + p.Apellido AS Profesor,
    c.CupoMaximo,
    c.CupoDisponible,
    (c.CupoMaximo - c.CupoDisponible) AS Inscritos
FROM Clases c
INNER JOIN Empleados e ON c.IDProfesor = e.IDEmpleado
INNER JOIN Personas p ON e.IDPersona = p.IDPersona
WHERE c.CupoDisponible <= 5
ORDER BY c.CupoDisponible ASC;`,

  staffProfesores: `-- Staff clasificado por rol y actividades
SELECT 
    e.IDEmpleado,
    p.Nombre + ' ' + p.Apellido AS Empleado,
    p.Email,
    r.NombreRol,
    e.FechaContratacion,
    COUNT(c.IDClase) AS ClasesAsignadas
FROM Empleados e
INNER JOIN Personas p ON e.IDPersona = p.IDPersona
INNER JOIN Roles r ON e.IDRol = r.IDRol
LEFT JOIN Clases c ON e.IDEmpleado = c.IDProfesor
GROUP BY e.IDEmpleado, p.Nombre, p.Apellido, p.Email, r.NombreRol, e.FechaContratacion
ORDER BY r.NombreRol, Empleado;`,

  asistenciasGlobales: `-- Resumen global de presentismo por clase
SELECT 
    c.Nombre AS Clase,
    COUNT(ha.IDAsistencia) AS TotalRegistros,
    SUM(CASE WHEN ha.Presente = 1 THEN 1 ELSE 0 END) AS Asistencias,
    SUM(CASE WHEN ha.Presente = 0 THEN 1 ELSE 0 END) AS Inasistencias
FROM Clases c
LEFT JOIN HistorialAsistencias ha ON c.IDClase = ha.IDClase
GROUP BY c.Nombre
ORDER BY Asistencias DESC;`
};

document.addEventListener('DOMContentLoaded', async () => {
  renderSharedHeader('studio');
  renderSharedFooter();

  const sqlInput = document.getElementById('sqlQueryInput');
  if (sqlInput) {
    sqlInput.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        executeSqlQuery();
      }
    });
  }

  await loadTablesList();
});

function switchStudioMode(mode) {
  const btnSql = document.getElementById('studioBtn-sql');
  const btnTablas = document.getElementById('studioBtn-tablas');
  const secSql = document.getElementById('studioSection-sql');
  const secTablas = document.getElementById('studioSection-tablas');

  if (mode === 'sql') {
    btnSql.className = 'px-3.5 py-1.5 rounded-lg font-medium text-white bg-indigo-600 transition flex items-center gap-1.5 shadow-sm';
    btnTablas.className = 'px-3.5 py-1.5 rounded-lg font-medium text-slate-400 hover:text-white transition flex items-center gap-1.5';
    secSql.classList.remove('hidden');
    secTablas.classList.add('hidden');
  } else {
    btnTablas.className = 'px-3.5 py-1.5 rounded-lg font-medium text-white bg-indigo-600 transition flex items-center gap-1.5 shadow-sm';
    btnSql.className = 'px-3.5 py-1.5 rounded-lg font-medium text-slate-400 hover:text-white transition flex items-center gap-1.5';
    secTablas.classList.remove('hidden');
    secSql.classList.add('hidden');
    selectTable(currentTable);
  }

  if (window.lucide) lucide.createIcons();
}

// 1. SQL STUDIO LIBRE
function setQueryTemplate(key) {
  const sql = queryTemplates[key];
  if (sql) {
    const input = document.getElementById('sqlQueryInput');
    input.value = sql;
  }
}

async function executeSqlQuery() {
  const sql = document.getElementById('sqlQueryInput').value.trim();
  const maxRows = parseInt(document.getElementById('sqlMaxRows').value) || 200;
  const btnExec = document.getElementById('btnExecuteSql');
  const errorBox = document.getElementById('sqlErrorBox');
  const errorMsg = document.getElementById('sqlErrorMessage');
  const resultsContainer = document.getElementById('sqlResultsContainer');
  const thead = document.getElementById('sqlResultHead');
  const tbody = document.getElementById('sqlResultBody');
  const rowCount = document.getElementById('sqlRowCount');
  const execTime = document.getElementById('sqlExecutionTime');
  const truncatedNote = document.getElementById('sqlTruncatedNote');
  const btnExport = document.getElementById('btnExportCsv');

  if (!sql) {
    alert('Ingresa una consulta SQL.');
    return;
  }

  errorBox.classList.add('hidden');
  resultsContainer.classList.add('hidden');
  btnExport.classList.add('hidden');
  btnExec.disabled = true;
  btnExec.innerHTML = '<i data-lucide="loader" class="w-4 h-4 animate-spin"></i> Ejecutando...';

  try {
    const res = await fetch('/api/sql/execute', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sql, maxRows })
    });

    const data = await res.json();

    if (!res.ok) {
      errorBox.classList.remove('hidden');
      errorMsg.textContent = data.message || 'Error en la ejecución';
      return;
    }

    lastSqlResultData = data;
    resultsContainer.classList.remove('hidden');
    btnExport.classList.remove('hidden');
    rowCount.textContent = `${data.totalRows} filas`;
    execTime.textContent = `${data.executionTimeMs} ms`;
    
    if (data.truncated) truncatedNote.classList.remove('hidden');
    else truncatedNote.classList.add('hidden');

    if (data.columns.length === 0) {
      thead.innerHTML = '';
      tbody.innerHTML = '<tr><td class="p-3 text-slate-400">Consulta completada sin conjunto de resultados.</td></tr>';
      return;
    }

    thead.innerHTML = `<tr>${data.columns.map(c => `<th class="p-2.5">${escapeHtml(c)}</th>`).join('')}</tr>`;
    tbody.innerHTML = data.rows.map(row => `
      <tr class="hover:bg-slate-800/40">
        ${data.columns.map(col => `<td class="p-2.5 text-slate-300">${formatTableCell(row[col])}</td>`).join('')}
      </tr>
    `).join('');

  } catch (err) {
    errorBox.classList.remove('hidden');
    errorMsg.textContent = `Error de conexión: ${err.message}`;
  } finally {
    btnExec.disabled = false;
    btnExec.innerHTML = '<i data-lucide="play" class="w-4 h-4 fill-current"></i> Ejecutar Consulta';
    if (window.lucide) lucide.createIcons();
  }
}

function exportSqlResultsToCsv() {
  if (!lastSqlResultData || !lastSqlResultData.rows || lastSqlResultData.rows.length === 0) return;

  const cols = lastSqlResultData.columns;
  const rows = lastSqlResultData.rows;

  let csv = cols.map(c => `"${c.replace(/"/g, '""')}"`).join(',') + '\r\n';

  rows.forEach(r => {
    const line = cols.map(c => {
      const val = r[c];
      if (val === null || val === undefined) return '""';
      return `"${String(val).replace(/"/g, '""')}"`;
    }).join(',');
    csv += line + '\r\n';
  });

  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `consulta_gimnasio_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '_')}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

// 2. EXPLORADOR DE TABLAS
async function loadTablesList() {
  try {
    const res = await fetch('/api/tablas');
    const tables = await res.json();
    const select = document.getElementById('tablesSelect');
    if (!select) return;

    select.innerHTML = tables.map(t => 
      `<option value="${t.tableName}">${t.tableName} (~${t.estimatedRows} filas)</option>`
    ).join('');

    if (tables.length > 0) {
      currentTable = tables[0].tableName;
      selectTable(currentTable);
    }
  } catch (err) {
    console.error(err);
  }
}

async function selectTable(tableName) {
  currentTable = tableName;
  currentTablePage = 1;
  await fetchTableData();
}

function changePage(delta) {
  currentTablePage += delta;
  if (currentTablePage < 1) currentTablePage = 1;
  fetchTableData();
}

async function fetchTableData() {
  const badge = document.getElementById('tableInfoBadge');
  const thead = document.getElementById('browserTableHead');
  const tbody = document.getElementById('browserTableBody');
  const indicator = document.getElementById('pageIndicator');
  const btnPrev = document.getElementById('btnPrevPage');
  const btnNext = document.getElementById('btnNextPage');

  badge.textContent = `Cargando ${currentTable}...`;
  tbody.innerHTML = '<tr><td colspan="10" class="p-4 text-center text-slate-500">Cargando datos...</td></tr>';

  try {
    const res = await fetch(`/api/tablas/${currentTable}?page=${currentTablePage}&pageSize=${currentTablePageSize}`);
    const data = await res.json();

    const maxPages = Math.ceil(data.totalRows / currentTablePageSize) || 1;
    badge.textContent = `${data.tableName}: ${data.totalRows} registros totales (Mostrando pág. ${data.page} de ${maxPages})`;
    indicator.textContent = `Pág ${data.page} de ${maxPages}`;
    btnPrev.disabled = data.page <= 1;
    btnNext.disabled = data.page >= maxPages;

    thead.innerHTML = `<tr>${data.columns.map(c => `<th class="p-3">${escapeHtml(c)}</th>`).join('')}</tr>`;

    if (data.rows.length === 0) {
      tbody.innerHTML = '<tr><td colspan="10" class="p-4 text-center text-slate-500">Tabla vacía.</td></tr>';
      return;
    }

    tbody.innerHTML = data.rows.map(row => `
      <tr class="hover:bg-slate-800/40">
        ${data.columns.map(col => `<td class="p-3 text-slate-300">${formatTableCell(row[col])}</td>`).join('')}
      </tr>
    `).join('');

  } catch (err) {
    badge.textContent = 'Error al cargar';
    tbody.innerHTML = `<tr><td colspan="10" class="p-4 text-center text-rose-400">${err.message}</td></tr>`;
  }
}
