// GimnasioDB Studio - Client Logic (C# .NET 10 + SQL Server)

let currentTab = 'dashboard';
let currentView = 'socios-membresias-activas';
let currentTable = 'Personas';
let currentTablePage = 1;
const currentTablePageSize = 15;
let lastSqlResultData = null;

const viewMeta = {
  'socios-membresias-activas': {
    title: 'vw_SociosMembresiasActivas',
    desc: 'Reporte de socios con contratos de membresía activos, calculando en tiempo real los días restantes de vigencia.',
    endpoint: '/api/vistas/socios-membresias-activas'
  },
  'ocupacion-clases': {
    title: 'vw_OcupacionClases',
    desc: 'Análisis logístico de ocupación de salones con cálculo porcentual de alumnos inscritos sobre el cupo máximo.',
    endpoint: '/api/vistas/ocupacion-clases'
  },
  'recaudacion-mensual': {
    title: 'vw_RecaudacionMensual',
    desc: 'Auditoría analítica de caja mensual agrupando cantidad de transacciones, recaudación total y ticket promedio.',
    endpoint: '/api/vistas/recaudacion-mensual'
  }
};

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

// Initialization
document.addEventListener('DOMContentLoaded', async () => {
  if (window.lucide) lucide.createIcons();

  // Setup today dates on inputs
  const today = new Date().toISOString().split('T')[0];
  const sixtyDaysAgo = new Date(Date.now() - 60 * 86400000).toISOString().split('T')[0];
  const nowIso = new Date().toISOString().slice(0, 16);

  const spFechaDesde = document.getElementById('spFechaDesde');
  const spFechaHasta = document.getElementById('spFechaHasta');
  const spPagoFecha = document.getElementById('spPagoFecha');

  if (spFechaDesde) spFechaDesde.value = sixtyDaysAgo;
  if (spFechaHasta) spFechaHasta.value = today;
  if (spPagoFecha) spPagoFecha.value = nowIso;

  // Key shortcut for SQL Editor
  const sqlInput = document.getElementById('sqlQueryInput');
  if (sqlInput) {
    sqlInput.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
        e.preventDefault();
        executeSqlQuery();
      }
    });
  }

  // Initial load
  await loadDashboard();
  await loadCatalogs();
  await loadTablesList();
});

// Tab Switcher
function switchTab(tabId) {
  currentTab = tabId;

  // Buttons state
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.classList.remove('text-white', 'bg-indigo-600', 'shadow-sm');
    btn.classList.add('text-slate-400', 'hover:text-slate-200', 'hover:bg-slate-800');
  });

  const activeBtn = document.getElementById(`tabBtn-${tabId}`);
  if (activeBtn) {
    activeBtn.classList.add('text-white', 'bg-indigo-600', 'shadow-sm');
    activeBtn.classList.remove('text-slate-400', 'hover:text-slate-200', 'hover:bg-slate-800');
  }

  // Sections visibility
  document.querySelectorAll('.tab-content').forEach(sec => sec.classList.add('hidden'));
  const activeSection = document.getElementById(`tab-${tabId}`);
  if (activeSection) {
    activeSection.classList.remove('hidden');
  }

  if (window.lucide) lucide.createIcons();

  // Lazy tab loader
  if (tabId === 'dashboard') loadDashboard();
  if (tabId === 'vistas') loadView(currentView);
  if (tabId === 'triggers') {
    loadClasesCatalog();
    loadInscripcionesRecientes();
  }
  if (tabId === 'tablas') selectTable(currentTable);
}

// 1. DASHBOARD
async function loadDashboard() {
  try {
    const res = await fetch('/api/dashboard/stats');
    if (!res.ok) throw new Error('Error al conectar con SQL Server');
    const data = await res.json();

    document.getElementById('kpi-socios').textContent = data.totalSocios;
    document.getElementById('kpi-empleados').textContent = data.totalEmpleados;
    document.getElementById('kpi-clases').textContent = data.clasesActivas;
    document.getElementById('kpi-membresias').textContent = data.membresiasActivas;
    document.getElementById('kpi-recaudacion').textContent = formatCurrency(data.recaudacionHistorica);
    document.getElementById('dbLatencyBadge').textContent = `${data.databaseLatencyMs} ms`;
    document.getElementById('info-sql-version').textContent = data.sqlVersion;

    const statusPill = document.getElementById('connectionStatus');
    statusPill.className = "flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-slate-800/80 border border-emerald-500/30 text-xs";
  } catch (err) {
    console.error(err);
    document.getElementById('dbLatencyBadge').textContent = 'Error';
    const statusPill = document.getElementById('connectionStatus');
    statusPill.className = "flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-rose-950/60 border border-rose-500/40 text-xs";
  }
}

// 2. VISTAS DE NEGOCIO
async function loadView(viewName) {
  currentView = viewName;
  const meta = viewMeta[viewName];
  if (!meta) return;

  // Update tabs UI
  ['socios-membresias-activas', 'ocupacion-clases', 'recaudacion-mensual'].forEach(name => {
    const btn = document.getElementById(`subviewBtn-${name}`);
    if (btn) {
      if (name === viewName) {
        btn.className = 'px-3 py-1.5 rounded-lg font-medium text-white bg-indigo-600 transition';
      } else {
        btn.className = 'px-3 py-1.5 rounded-lg font-medium text-slate-400 hover:text-white transition';
      }
    }
  });

  document.getElementById('viewTitle').textContent = meta.title;
  document.getElementById('viewDescription').textContent = meta.desc;

  const thead = document.getElementById('viewTableHead');
  const tbody = document.getElementById('viewTableBody');
  const rowCountBadge = document.getElementById('viewRowCount');
  const execTimeBadge = document.getElementById('viewExecTime');

  rowCountBadge.textContent = 'Consultando vista en SQL Server...';
  tbody.innerHTML = '<tr><td colspan="8" class="p-4 text-center text-slate-500">Cargando...</td></tr>';

  const t0 = performance.now();
  try {
    const res = await fetch(meta.endpoint);
    const data = await res.json();
    const t1 = performance.now();

    execTimeBadge.textContent = `${Math.round(t1 - t0)} ms`;
    rowCountBadge.textContent = `${data.length} registros obtenidos`;

    if (data.length === 0) {
      thead.innerHTML = '';
      tbody.innerHTML = '<tr><td colspan="8" class="p-4 text-center text-slate-500">Sin datos en esta vista.</td></tr>';
      return;
    }

    // Dynamic headers based on view
    if (viewName === 'socios-membresias-activas') {
      thead.innerHTML = `
        <tr>
          <th class="p-3">ID Socio</th>
          <th class="p-3">Socio</th>
          <th class="p-3">Email</th>
          <th class="p-3">Plan Contratado</th>
          <th class="p-3">Fecha Inicio</th>
          <th class="p-3">Vencimiento</th>
          <th class="p-3">Días Restantes</th>
        </tr>
      `;
      tbody.innerHTML = data.map(item => {
        let badgeColor = 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
        if (item.diasRestantes <= 0) badgeColor = 'bg-rose-500/10 text-rose-400 border-rose-500/20';
        else if (item.diasRestantes <= 7) badgeColor = 'bg-amber-500/10 text-amber-400 border-amber-500/20';

        return `
          <tr class="hover:bg-slate-800/40 transition">
            <td class="p-3 font-mono text-slate-400">#${item.idSocio}</td>
            <td class="p-3 font-medium text-white">${escapeHtml(item.socio)}</td>
            <td class="p-3 text-slate-400">${escapeHtml(item.email)}</td>
            <td class="p-3 text-indigo-300 font-medium">${escapeHtml(item.planContratado)}</td>
            <td class="p-3 text-slate-400">${formatDate(item.fechaInicio)}</td>
            <td class="p-3 text-slate-400">${formatDate(item.fechaVencimiento)}</td>
            <td class="p-3">
              <span class="px-2 py-0.5 rounded text-[11px] font-bold border ${badgeColor}">
                ${item.diasRestantes > 0 ? `${item.diasRestantes} días` : 'Vencido'}
              </span>
            </td>
          </tr>
        `;
      }).join('');
    } else if (viewName === 'ocupacion-clases') {
      thead.innerHTML = `
        <tr>
          <th class="p-3">Clase</th>
          <th class="p-3">Horario</th>
          <th class="p-3">Profesor</th>
          <th class="p-3">Inscritos / Cupo</th>
          <th class="p-3">Vacantes</th>
          <th class="p-3">Ocupación %</th>
        </tr>
      `;
      tbody.innerHTML = data.map(item => {
        const pct = item.porcentajeOcupacion;
        let barColor = 'bg-emerald-500';
        if (pct >= 85) barColor = 'bg-rose-500';
        else if (pct >= 50) barColor = 'bg-amber-500';

        return `
          <tr class="hover:bg-slate-800/40 transition">
            <td class="p-3 font-semibold text-white">${escapeHtml(item.clase)}</td>
            <td class="p-3 text-slate-400">${formatDateTime(item.horario)}</td>
            <td class="p-3 text-slate-300">${escapeHtml(item.profesor)}</td>
            <td class="p-3 font-mono text-slate-200">${item.alumnosInscritos} / ${item.cupoMaximo}</td>
            <td class="p-3 font-mono ${item.cupoDisponible <= 2 ? 'text-rose-400 font-bold' : 'text-slate-300'}">
              ${item.cupoDisponible} vacantes
            </td>
            <td class="p-3">
              <div class="flex items-center gap-2">
                <div class="w-24 bg-slate-800 rounded-full h-2 overflow-hidden">
                  <div class="${barColor} h-2 rounded-full" style="width: ${Math.min(100, pct)}%"></div>
                </div>
                <span class="font-mono text-[11px] text-slate-300">${pct}%</span>
              </div>
            </td>
          </tr>
        `;
      }).join('');
    } else if (viewName === 'recaudacion-mensual') {
      thead.innerHTML = `
        <tr>
          <th class="p-3">Período (Año - Mes)</th>
          <th class="p-3">Transacciones Realizadas</th>
          <th class="p-3">Recaudación Total</th>
          <th class="p-3">Ticket Promedio</th>
        </tr>
      `;
      tbody.innerHTML = data.map(item => `
        <tr class="hover:bg-slate-800/40 transition">
          <td class="p-3 font-semibold text-white">${item.anio} - Mes ${item.mes}</td>
          <td class="p-3 font-mono text-slate-300">${item.cantidadTransacciones} cobros</td>
          <td class="p-3 font-mono text-emerald-400 font-bold">${formatCurrency(item.totalRecaudado)}</td>
          <td class="p-3 font-mono text-indigo-300">${formatCurrency(item.ticketPromedio)}</td>
        </tr>
      `).join('');
    }

  } catch (err) {
    console.error(err);
    rowCountBadge.textContent = 'Error al cargar';
    tbody.innerHTML = `<tr><td colspan="8" class="p-4 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

// 3. PROCEDIMIENTOS ALMACENADOS
async function runSpAsistencias(e) {
  e.preventDefault();
  const socioId = document.getElementById('spSocioSelect').value;
  const desde = document.getElementById('spFechaDesde').value;
  const hasta = document.getElementById('spFechaHasta').value;

  if (!socioId) return;

  const resultsBox = document.getElementById('spAsistenciasResults');
  const tbody = document.getElementById('spAsistenciasBody');
  resultsBox.classList.remove('hidden');
  tbody.innerHTML = '<tr><td colspan="4" class="p-3 text-center text-slate-400">Consultando procedimiento...</td></tr>';

  try {
    const res = await fetch(`/api/procedimientos/asistencias-socio?socioId=${socioId}&desde=${desde}&hasta=${hasta}`);
    const data = await res.json();

    if (data.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="p-3 text-center text-slate-500">No se encontraron asistencias en el rango seleccionado.</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(item => {
      const isPresent = item.estadoAsistencia.toUpperCase() === 'PRESENTE';
      const badge = isPresent 
        ? '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">PRESENTE</span>'
        : '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20">AUSENTE</span>';

      return `
        <tr class="hover:bg-slate-800/40">
          <td class="p-2 font-mono text-slate-300">${formatDate(item.fechaAsistencia)}</td>
          <td class="p-2 font-medium text-white">${escapeHtml(item.clase)}</td>
          <td class="p-2 text-slate-400">${formatDateTime(item.horarioClase)}</td>
          <td class="p-2">${badge}</td>
        </tr>
      `;
    }).join('');
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="4" class="p-3 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

async function runSpRegistrarPago(e) {
  e.preventDefault();
  const contratoId = document.getElementById('spContratoSelect').value;
  const monto = parseFloat(document.getElementById('spPagoMonto').value);
  const fecha = document.getElementById('spPagoFecha').value;

  const statusBox = document.getElementById('spPagoStatus');
  statusBox.className = 'p-3 rounded-lg text-xs border bg-slate-950 text-slate-400 border-slate-700';
  statusBox.textContent = 'Ejecutando sp_RegistrarPagoMembresia en transacción...';
  statusBox.classList.remove('hidden');

  try {
    const res = await fetch('/api/procedimientos/registrar-pago', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        idSocioMembresia: parseInt(contratoId),
        monto: monto,
        fechaPago: fecha ? new Date(fecha).toISOString() : null
      })
    });

    const data = await res.json();

    if (!res.ok) {
      statusBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-600 text-rose-200';
      statusBox.innerHTML = `<strong>Error en la transacción SQL Server:</strong><br>${escapeHtml(data.message || 'Error desconocido')}`;
      return;
    }

    statusBox.className = 'p-3 rounded-lg text-xs border bg-emerald-950/60 border-emerald-600 text-emerald-200';
    statusBox.innerHTML = `<strong>¡Transacción Confirmada (COMMIT)!</strong><br>${escapeHtml(data.message)}<br>Monto: ${formatCurrency(data.monto)}`;
    
    // Refresh catalogs and dashboard
    loadDashboard();
    loadCatalogs();
  } catch (err) {
    statusBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-600 text-rose-200';
    statusBox.innerHTML = `<strong>Fallo de conexión:</strong> ${err.message}`;
  }
}

// 4. TRIGGERS EN VIVO
async function runInscripcionTrigger(e) {
  e.preventDefault();
  const socioId = document.getElementById('triggerSocioSelect').value;
  const claseId = document.getElementById('triggerClaseSelect').value;

  const alertBox = document.getElementById('triggerResultAlert');
  alertBox.className = 'p-3 rounded-lg text-xs border bg-slate-950 text-slate-400 border-slate-700';
  alertBox.textContent = 'Insertando en InscripcionesClase (Disparando trigger)...';
  alertBox.classList.remove('hidden');

  try {
    const res = await fetch('/api/triggers/inscribir', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        idSocio: parseInt(socioId),
        idClase: parseInt(claseId)
      })
    });

    const data = await res.json();

    if (!res.ok) {
      // Cupo agotado o error del trigger
      alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/70 border-rose-500 text-rose-200 space-y-1';
      alertBox.innerHTML = `
        <div class="font-bold text-rose-400 flex items-center gap-1">
          <i data-lucide="shield-alert" class="w-4 h-4"></i> RAISERROR / ROLLBACK TRANSACTION:
        </div>
        <div>${escapeHtml(data.message)}</div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    alertBox.className = 'p-3 rounded-lg text-xs border bg-emerald-950/70 border-emerald-500 text-emerald-200 space-y-1';
    alertBox.innerHTML = `
      <div class="font-bold text-emerald-400 flex items-center gap-1">
        <i data-lucide="check-circle" class="w-4 h-4"></i> Trigger Ejecutado con Éxito:
      </div>
      <div>${escapeHtml(data.message)}</div>
    `;

    if (window.lucide) lucide.createIcons();

    // Actualizar clases y lista de inscripciones
    loadClasesCatalog();
    loadInscripcionesRecientes();
  } catch (err) {
    alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/70 border-rose-500 text-rose-200';
    alertBox.textContent = `Error: ${err.message}`;
  }
}

async function cancelarInscripcion(id) {
  if (!confirm(`¿Deseas dar de baja la inscripción #${id}?\nEsto disparará el trigger trg_Cancelacion_ActualizarCupo y restituirá 1 vacante.`)) {
    return;
  }

  try {
    const res = await fetch(`/api/triggers/cancelar/${id}`, { method: 'DELETE' });
    const data = await res.json();

    if (!res.ok) {
      alert(`Error al cancelar: ${data.message}`);
      return;
    }

    alert(data.message);
    loadClasesCatalog();
    loadInscripcionesRecientes();
  } catch (err) {
    alert(`Error: ${err.message}`);
  }
}

async function loadClasesCatalog() {
  try {
    const res = await fetch('/api/catalogos/clases');
    const clases = await res.json();

    // Update select in trigger tab
    const select = document.getElementById('triggerClaseSelect');
    if (select) {
      select.innerHTML = clases.map(c => 
        `<option value="${c.idClase}">${escapeHtml(c.nombre)} • Vacantes: ${c.cupoDisponible}/${c.cupoMaximo} (${formatDateTime(c.horario)})</option>`
      ).join('');
    }

    // Render cards
    const cardsContainer = document.getElementById('clasesCuposCards');
    if (cardsContainer) {
      cardsContainer.innerHTML = clases.map(c => {
        const pct = Math.round(((c.cupoMaximo - c.cupoDisponible) / c.cupoMaximo) * 100);
        const isFull = c.cupoDisponible <= 0;

        return `
          <div class="p-3 rounded-xl bg-slate-950 border ${isFull ? 'border-rose-500/50 bg-rose-950/10' : 'border-slate-800'} space-y-2">
            <div class="flex items-center justify-between">
              <span class="font-bold text-white text-xs">${escapeHtml(c.nombre)}</span>
              <span class="text-[10px] font-mono px-2 py-0.5 rounded ${isFull ? 'bg-rose-500/20 text-rose-400 border border-rose-500/30' : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'}">
                ${isFull ? 'AGOTADO' : `${c.cupoDisponible} libres`}
              </span>
            </div>
            <div class="text-[11px] text-slate-400 flex justify-between">
              <span>${formatDateTime(c.horario)}</span>
              <span class="font-mono">${c.cupoMaximo - c.cupoDisponible} / ${c.cupoMaximo}</span>
            </div>
            <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="${isFull ? 'bg-rose-500' : 'bg-indigo-500'} h-1.5 rounded-full" style="width: ${pct}%"></div>
            </div>
          </div>
        `;
      }).join('');
    }
  } catch (err) {
    console.error(err);
  }
}

async function loadInscripcionesRecientes() {
  const tbody = document.getElementById('inscripcionesListBody');
  if (!tbody) return;

  try {
    const res = await fetch('/api/triggers/inscripciones');
    const list = await res.json();

    if (list.length === 0) {
      tbody.innerHTML = '<tr><td colspan="5" class="p-3 text-center text-slate-500">No hay inscripciones registradas todavía.</td></tr>';
      return;
    }

    tbody.innerHTML = list.map(item => `
      <tr class="hover:bg-slate-800/40">
        <td class="p-2 font-mono text-slate-400">#${item.idInscripcion}</td>
        <td class="p-2 font-medium text-white">${escapeHtml(item.socio)}</td>
        <td class="p-2 text-indigo-300">${escapeHtml(item.clase)}</td>
        <td class="p-2 text-slate-400">${formatDate(item.fechaInscripcion)}</td>
        <td class="p-2 text-right">
          <button onclick="cancelarInscripcion(${item.idInscripcion})" class="px-2 py-1 bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 rounded text-[10px] font-medium transition">
            Dar de Baja (Devolver Cupo)
          </button>
        </td>
      </tr>
    `).join('');
  } catch (err) {
    console.error(err);
  }
}

// 5. EXPLORADOR DE TABLAS
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

// 6. SQL STUDIO
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

// Catalogs for Dropdowns
async function loadCatalogs() {
  try {
    const [sociosRes, clasesRes, contratosRes] = await Promise.all([
      fetch('/api/catalogos/socios'),
      fetch('/api/catalogos/clases'),
      fetch('/api/catalogos/contratos')
    ]);

    const socios = await sociosRes.json();
    const clases = await clasesRes.json();
    const contratos = await contratosRes.json();

    // Populate SP Socios select
    const spSocioSelect = document.getElementById('spSocioSelect');
    const triggerSocioSelect = document.getElementById('triggerSocioSelect');

    const sociosOptions = socios.map(s => 
      `<option value="${s.idSocio}">#${s.idSocio} - ${escapeHtml(s.nombreCompleto)} (${escapeHtml(s.email)})</option>`
    ).join('');

    if (spSocioSelect) spSocioSelect.innerHTML = sociosOptions;
    if (triggerSocioSelect) triggerSocioSelect.innerHTML = sociosOptions;

    // Populate SP Contratos select
    const contratoSelect = document.getElementById('spContratoSelect');
    if (contratoSelect) {
      contratoSelect.innerHTML = contratos.map(c => 
        `<option value="${c.idSocioMembresia}">Contrato #${c.idSocioMembresia} - ${escapeHtml(c.socio)} (${escapeHtml(c.planContratado)}: ${formatCurrency(c.precioPlan)})</option>`
      ).join('');

      if (contratos.length > 0) {
        document.getElementById('spPagoMonto').value = contratos[0].precioPlan;
      }
    }

    // Populate classes
    loadClasesCatalog();

  } catch (err) {
    console.error('Error cargando catálogos:', err);
  }
}

// Helpers
function formatCurrency(val) {
  if (val === null || val === undefined) return '$0.00';
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' }).format(val);
}

function formatDate(iso) {
  if (!iso) return '-';
  const d = new Date(iso);
  return d.toLocaleDateString('es-AR');
}

function formatDateTime(iso) {
  if (!iso) return '-';
  const d = new Date(iso);
  return d.toLocaleString('es-AR', { dateStyle: 'short', timeStyle: 'short' });
}

function formatTableCell(val) {
  if (val === null || val === undefined) return '<span class="text-slate-600 italic">NULL</span>';
  if (typeof val === 'boolean') {
    return val 
      ? '<span class="text-emerald-400 font-bold">1 (TRUE)</span>' 
      : '<span class="text-rose-400 font-bold">0 (FALSE)</span>';
  }
  return escapeHtml(String(val));
}

function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/[&<>"']/g, m => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;'
  })[m]);
}
