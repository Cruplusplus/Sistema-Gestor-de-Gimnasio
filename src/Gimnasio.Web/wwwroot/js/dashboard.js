// GimnasioDB - Módulo Dashboard (dashboard.js)

document.addEventListener('DOMContentLoaded', async () => {
  renderSharedHeader('dashboard');
  renderSharedFooter();
  await loadDashboardMetrics();
  await loadDashboardSnapshots();
});

async function loadDashboardMetrics() {
  try {
    const res = await fetch('/api/dashboard/stats');
    if (!res.ok) throw new Error('Error al conectar');
    const data = await res.json();

    document.getElementById('kpi-socios').textContent = data.totalSocios;
    document.getElementById('kpi-empleados').textContent = data.totalEmpleados;
    document.getElementById('kpi-clases').textContent = data.clasesActivas;
    document.getElementById('kpi-membresias').textContent = data.membresiasActivas;
    document.getElementById('kpi-recaudacion').textContent = formatCurrency(data.recaudacionHistorica);
    document.getElementById('info-sql-version').textContent = data.sqlVersion;
    document.getElementById('dash-latency').textContent = `${data.databaseLatencyMs} ms`;

  } catch (err) {
    console.error(err);
  }
}

async function loadDashboardSnapshots() {
  const inscripcionesList = document.getElementById('dashInscripcionesList');
  const contratosList = document.getElementById('dashContratosList');

  // Inscripciones recientes
  if (inscripcionesList) {
    try {
      const res = await fetch('/api/triggers/inscripciones');
      const data = await res.json();

      if (data.length === 0) {
        inscripcionesList.innerHTML = '<div class="text-xs text-slate-500 py-3 text-center">Sin inscripciones recientes.</div>';
      } else {
        inscripcionesList.innerHTML = data.slice(0, 5).map(item => `
          <div class="flex items-center justify-between p-2.5 bg-slate-950/60 rounded-lg border border-slate-800/80 text-xs">
            <div>
              <span class="font-medium text-white block">${escapeHtml(item.socio)}</span>
              <span class="text-[11px] text-indigo-400">${escapeHtml(item.clase)}</span>
            </div>
            <span class="text-[11px] text-slate-400 font-mono">${formatDate(item.fechaInscripcion)}</span>
          </div>
        `).join('');
      }
    } catch {
      inscripcionesList.innerHTML = '<div class="text-xs text-rose-400">Error al cargar actividad.</div>';
    }
  }

  // Membresías activas
  if (contratosList) {
    try {
      const res = await fetch('/api/vistas/socios-membresias-activas');
      const data = await res.json();

      if (data.length === 0) {
        contratosList.innerHTML = '<div class="text-xs text-slate-500 py-3 text-center">Sin membresías vigentes.</div>';
      } else {
        contratosList.innerHTML = data.slice(0, 5).map(item => `
          <div class="flex items-center justify-between p-2.5 bg-slate-950/60 rounded-lg border border-slate-800/80 text-xs">
            <div>
              <span class="font-medium text-white block">${escapeHtml(item.socio)}</span>
              <span class="text-[11px] text-slate-400">${escapeHtml(item.planContratado)}</span>
            </div>
            <span class="text-[11px] font-bold ${item.diasRestantes <= 5 ? 'text-amber-400' : 'text-emerald-400'}">
              ${item.diasRestantes} días rest.
            </span>
          </div>
        `).join('');
      }
    } catch {
      contratosList.innerHTML = '<div class="text-xs text-rose-400">Error al cargar contratos.</div>';
    }
  }

  if (window.lucide) lucide.createIcons();
}
