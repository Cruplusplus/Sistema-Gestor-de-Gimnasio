// GimnasioDB - Módulo de Operaciones & Clases (operaciones.js)

let currentSubTab = 'cobros';
let contratosCache = [];

document.addEventListener('DOMContentLoaded', async () => {
  renderSharedHeader('operaciones');
  renderSharedFooter();

  // Fechas por defecto
  const today = new Date().toISOString().split('T')[0];
  const sixtyDaysAgo = new Date(Date.now() - 60 * 86400000).toISOString().split('T')[0];
  const nowIso = new Date().toISOString().slice(0, 16);

  const spFechaDesde = document.getElementById('spFechaDesde');
  const spFechaHasta = document.getElementById('spFechaHasta');
  const spPagoFecha = document.getElementById('spPagoFecha');

  if (spFechaDesde) spFechaDesde.value = sixtyDaysAgo;
  if (spFechaHasta) spFechaHasta.value = today;
  if (spPagoFecha) spPagoFecha.value = nowIso;

  // Cargar catálogos y tablas iniciales
  await loadCatalogs();
  await loadContratosVigentes();
  await loadHistorialPagos();
});

function switchOperacionSubTab(subTabId) {
  currentSubTab = subTabId;

  // Actualizar botones
  ['cobros', 'inscripciones', 'asistencias', 'caja'].forEach(id => {
    const btn = document.getElementById(`opTabBtn-${id}`);
    const section = document.getElementById(`opSection-${id}`);
    if (btn && section) {
      if (id === subTabId) {
        btn.className = 'px-3 py-1.5 rounded-lg font-medium text-white bg-indigo-600 transition flex items-center gap-1.5 whitespace-nowrap shadow-sm';
        section.classList.remove('hidden');
      } else {
        btn.className = 'px-3 py-1.5 rounded-lg font-medium text-slate-400 hover:text-white transition flex items-center gap-1.5 whitespace-nowrap';
        section.classList.add('hidden');
      }
    }
  });

  if (window.lucide) lucide.createIcons();

  if (subTabId === 'cobros') {
    loadContratosVigentes();
    loadCatalogs();
    loadHistorialPagos();
  }
  if (subTabId === 'inscripciones') {
    loadClasesCatalog();
    loadInscripcionesRecientes();
  }
  if (subTabId === 'caja') loadRecaudacionMensual();
}

// 1. COBROS Y MEMBRESÍAS
async function loadContratosVigentes() {
  const tbody = document.getElementById('contratosTableBody');
  const countBadge = document.getElementById('contratosCountBadge');
  if (!tbody) return;

  tbody.innerHTML = '<tr><td colspan="6" class="p-3 text-center text-slate-500">Cargando contratos...</td></tr>';

  try {
    const res = await fetch('/api/vistas/socios-membresias-activas');
    const data = await res.json();

    if (countBadge) countBadge.textContent = `${data.length} vigentes`;

    if (data.length === 0) {
      tbody.innerHTML = '<tr><td colspan="6" class="p-3 text-center text-slate-500">No hay contratos activos registrados.</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(item => {
      let badgeVenc = 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
      if (item.diasRestantes <= 0) badgeVenc = 'bg-rose-500/10 text-rose-400 border-rose-500/20';
      else if (item.diasRestantes <= 7) badgeVenc = 'bg-amber-500/10 text-amber-400 border-amber-500/20';

      let estadoBadge = '';
      if (item.estadoPago === 'Pagada') {
        estadoBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 inline-flex items-center gap-1"><i data-lucide="check-circle" class="w-3 h-3"></i> Pagada</span>';
      } else if (item.estadoPago === 'Parcial') {
        estadoBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/10 text-amber-400 border border-amber-500/20 inline-flex items-center gap-1"><i data-lucide="clock" class="w-3 h-3"></i> Parcial</span>';
      } else {
        estadoBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20 inline-flex items-center gap-1"><i data-lucide="alert-circle" class="w-3 h-3"></i> Impaga</span>';
      }

      const saldoFormatted = formatCurrency(item.saldoPendiente);
      const abonadoFormatted = formatCurrency(item.totalPagado);
      const precioFormatted = formatCurrency(item.precioPlan);

      let actionBtn = '';
      if (item.saldoPendiente > 0) {
        actionBtn = `
          <button onclick="seleccionarContratoParaCobro(${item.idSocioMembresia})" class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1 shadow-sm transition whitespace-nowrap">
            <i data-lucide="credit-card" class="w-3.5 h-3.5"></i> Cobrar
          </button>
        `;
      } else {
        actionBtn = `
          <button onclick="seleccionarContratoParaCobro(${item.idSocioMembresia})" class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-slate-200 rounded-lg text-xs font-medium flex items-center gap-1 transition whitespace-nowrap" title="Membresía al día. Clic para registrar cobro extra si fuera necesario.">
            <i data-lucide="check" class="w-3.5 h-3.5 text-emerald-400"></i> Al día
          </button>
        `;
      }

      return `
        <tr class="hover:bg-slate-800/40">
          <td class="p-2.5">
            <span class="font-medium text-white block">${escapeHtml(item.socio)}</span>
            <span class="text-[11px] text-slate-500">${escapeHtml(item.email)}</span>
          </td>
          <td class="p-2.5">
            <span class="text-indigo-300 font-medium block">${escapeHtml(item.planContratado)}</span>
            <span class="text-[11px] text-slate-400 font-mono">${precioFormatted}</span>
          </td>
          <td class="p-2.5 font-sans">
            <span class="text-emerald-400 font-semibold font-mono block">${abonadoFormatted}</span>
            <span class="text-[11px] text-slate-400">Saldo: <strong class="${item.saldoPendiente > 0 ? 'text-amber-400 font-semibold' : 'text-slate-500'} font-mono">${saldoFormatted}</strong></span>
          </td>
          <td class="p-2.5">
            ${estadoBadge}
          </td>
          <td class="p-2.5">
            <span class="font-mono text-slate-300 text-[11px] block">${formatDate(item.fechaVencimiento)}</span>
            <span class="px-1.5 py-0.2 rounded text-[10px] font-bold border ${badgeVenc}">
              ${item.diasRestantes > 0 ? `${item.diasRestantes} días` : 'Vencido'}
            </span>
          </td>
          <td class="p-2.5 text-center">
            ${actionBtn}
          </td>
        </tr>
      `;
    }).join('');

    if (window.lucide) lucide.createIcons();
  } catch (err) {
    console.error(err);
    tbody.innerHTML = `<tr><td colspan="6" class="p-3 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

function onSelectContratoCobro() {
  const select = document.getElementById('spContratoSelect');
  const resumenBox = document.getElementById('spContratoResumen');
  const montoInput = document.getElementById('spPagoMonto');
  if (!select || !resumenBox) return;

  const id = parseInt(select.value);
  const contrato = contratosCache.find(c => c.idSocioMembresia === id);
  if (!contrato) {
    resumenBox.classList.add('hidden');
    return;
  }

  resumenBox.classList.remove('hidden');
  document.getElementById('resumenPrecioPlan').textContent = formatCurrency(contrato.precioPlan);
  document.getElementById('resumenTotalPagado').textContent = formatCurrency(contrato.totalPagado);
  document.getElementById('resumenSaldoPendiente').textContent = formatCurrency(contrato.saldoPendiente);

  const badgeContainer = document.getElementById('resumenEstadoBadge');
  if (contrato.estadoPago === 'Pagada') {
    badgeContainer.innerHTML = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 inline-flex items-center gap-1"><i data-lucide="check-circle" class="w-3 h-3"></i> Contrato 100% Abonado (Sin saldo deudor)</span>';
    montoInput.value = '';
    montoInput.placeholder = 'Ingresar monto (opcional / cuota extra)';
  } else if (contrato.estadoPago === 'Parcial') {
    badgeContainer.innerHTML = `<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/10 text-amber-400 border border-amber-500/20 inline-flex items-center gap-1"><i data-lucide="clock" class="w-3 h-3"></i> Pago Parcial Registrado (Resta abonar ${formatCurrency(contrato.saldoPendiente)})</span>`;
    montoInput.value = contrato.saldoPendiente;
  } else {
    badgeContainer.innerHTML = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20 inline-flex items-center gap-1"><i data-lucide="alert-circle" class="w-3 h-3"></i> Impaga (Abono pendiente del 100%)</span>';
    montoInput.value = contrato.saldoPendiente > 0 ? contrato.saldoPendiente : contrato.precioPlan;
  }

  if (window.lucide) lucide.createIcons();
}

function seleccionarContratoParaCobro(idContrato) {
  const select = document.getElementById('spContratoSelect');
  if (select) {
    select.value = idContrato;
    onSelectContratoCobro();
    const form = document.getElementById('formRegistrarPago');
    if (form) {
      form.scrollIntoView({ behavior: 'smooth', block: 'center' });
      const montoInput = document.getElementById('spPagoMonto');
      if (montoInput) {
        montoInput.focus();
        montoInput.select();
      }
    }
  }
}

async function runRegistrarPago(e) {
  e.preventDefault();
  const contratoId = parseInt(document.getElementById('spContratoSelect').value);
  const monto = parseFloat(document.getElementById('spPagoMonto').value);
  const fecha = document.getElementById('spPagoFecha').value;

  if (isNaN(contratoId) || contratoId <= 0) {
    alert('Por favor selecciona un contrato de socio válido.');
    return;
  }

  const contrato = contratosCache.find(c => c.idSocioMembresia === contratoId);
  if (contrato && contrato.saldoPendiente <= 0) {
    if (!confirm(`El contrato #${contratoId} de ${contrato.socio} ya figura 100% abonado ($${contrato.totalPagado} pagados).\n\n¿Estás seguro de registrar un cobro adicional por ${formatCurrency(monto)}?`)) {
      return;
    }
  }

  const statusBox = document.getElementById('spPagoStatus');
  statusBox.className = 'p-3 rounded-lg text-xs border bg-slate-950 text-slate-400 border-slate-700';
  statusBox.textContent = 'Procesando el cobro en caja...';
  statusBox.classList.remove('hidden');

  try {
    const res = await fetch('/api/procedimientos/registrar-pago', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        idSocioMembresia: contratoId,
        monto: monto,
        fechaPago: fecha ? new Date(fecha).toISOString() : null
      })
    });

    const data = await res.json();

    if (!res.ok) {
      statusBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-600 text-rose-200';
      statusBox.innerHTML = `<strong>Error al procesar:</strong><br>${escapeHtml(data.message || data.detail || 'Error desconocido')}`;
      return;
    }

    const socioLabel = contrato ? `${contrato.socio} (${contrato.planContratado})` : 'Socio';
    statusBox.className = 'p-3 rounded-lg text-xs border bg-emerald-950/60 border-emerald-600 text-emerald-200';
    statusBox.innerHTML = `
      <div class="flex items-center justify-between gap-2">
        <div>
          <strong>¡Cobro registrado exitosamente!</strong><br>
          Monto abonado: ${formatCurrency(data.monto)}
        </div>
        <button type="button" onclick="mostrarComprobantePago('REC-${data.contratoId}-${Date.now().toString().slice(-4)}', '${data.fecha}', '${escapeHtml(socioLabel)}', 'Cuota de Membresía', ${data.monto})" class="px-3 py-1 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold flex items-center gap-1 shadow-sm transition">
          <i data-lucide="printer" class="w-3.5 h-3.5"></i> Ver Recibo
        </button>
      </div>
    `;
    
    if (window.lucide) lucide.createIcons();

    await Promise.all([
      loadContratosVigentes(),
      loadCatalogs(),
      loadHistorialPagos()
    ]);
  } catch (err) {
    statusBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-600 text-rose-200';
    statusBox.innerHTML = `<strong>Fallo de conexión:</strong> ${err.message}`;
  }
}

async function loadHistorialPagos() {
  const tbody = document.getElementById('pagosTableBody');
  if (!tbody) return;

  try {
    const res = await fetch('/api/operaciones/pagos?limit=50');
    const pagos = await res.json();

    if (!pagos || pagos.length === 0) {
      tbody.innerHTML = '<tr><td colspan="6" class="p-4 text-center text-slate-500">No se registran cobros en caja recientemente.</td></tr>';
      return;
    }

    tbody.innerHTML = pagos.map(p => `
      <tr class="hover:bg-slate-800/40">
        <td class="p-2.5 font-mono text-indigo-400 font-semibold">REC-${p.idPago}</td>
        <td class="p-2.5 font-medium text-white">${escapeHtml(p.socio)}</td>
        <td class="p-2.5 text-slate-300">${escapeHtml(p.planContratado)}</td>
        <td class="p-2.5 text-slate-400 font-mono text-[11px]">${formatDateTime(p.fechaPago)}</td>
        <td class="p-2.5 text-right font-mono font-bold text-emerald-400">${formatCurrency(p.monto)}</td>
        <td class="p-2.5 text-center">
          <div class="flex items-center justify-center gap-1.5">
            <button onclick="mostrarComprobantePago('REC-${p.idPago}', '${p.fechaPago}', '${escapeHtml(p.socio)}', '${escapeHtml(p.planContratado)}', ${p.monto})" class="p-1 hover:bg-slate-800 text-slate-400 hover:text-indigo-300 rounded transition" title="Ver e imprimir recibo">
              <i data-lucide="printer" class="w-3.5 h-3.5"></i>
            </button>
            <button onclick="anularPago(${p.idPago}, ${p.monto}, '${escapeHtml(p.socio)}')" class="p-1 hover:bg-rose-500/10 text-slate-400 hover:text-rose-400 rounded transition" title="Anular comprobante por error">
              <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
            </button>
          </div>
        </td>
      </tr>
    `).join('');

    if (window.lucide) lucide.createIcons();
  } catch (err) {
    console.error('Error cargando historial de pagos:', err);
    tbody.innerHTML = `<tr><td colspan="6" class="p-4 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

async function anularPago(idPago, monto, socio) {
  if (!confirm(`¿Estás seguro de ANULAR y eliminar el comprobante #REC-${idPago} por ${formatCurrency(monto)} a nombre de ${socio}?\n\nEsta acción eliminará el registro de caja y restaurará el saldo adeudado del socio.`)) {
    return;
  }

  try {
    const res = await fetch(`/api/operaciones/pagos/${idPago}`, { method: 'DELETE' });
    const data = await res.json();
    if (!res.ok) {
      alert(`Error al anular cobro: ${data.message || data.detail}`);
      return;
    }

    alert(data.message || 'Comprobante anulado exitosamente.');
    await Promise.all([
      loadHistorialPagos(),
      loadContratosVigentes(),
      loadCatalogs()
    ]);
    if (currentSubTab === 'caja') loadRecaudacionMensual();
  } catch (err) {
    alert(`Error de red: ${err.message}`);
  }
}

// 2. INSCRIPCIONES Y CUPOS DE CLASES
async function runInscripcion(e) {
  e.preventDefault();
  const socioId = document.getElementById('triggerSocioSelect').value;
  const claseId = document.getElementById('triggerClaseSelect').value;

  const alertBox = document.getElementById('triggerResultAlert');
  alertBox.className = 'p-3 rounded-lg text-xs border bg-slate-950 text-slate-400 border-slate-700';
  alertBox.textContent = 'Procesando inscripción y reservando vacante...';
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
      alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/70 border-rose-500 text-rose-200 space-y-1';
      alertBox.innerHTML = `
        <div class="font-bold text-rose-400 flex items-center gap-1">
          <i data-lucide="shield-alert" class="w-4 h-4"></i> No fue posible inscribir:
        </div>
        <div>${escapeHtml(data.message)}</div>
      `;
      if (window.lucide) lucide.createIcons();
      return;
    }

    alertBox.className = 'p-3 rounded-lg text-xs border bg-emerald-950/70 border-emerald-500 text-emerald-200 space-y-1';
    alertBox.innerHTML = `
      <div class="font-bold text-emerald-400 flex items-center gap-1">
        <i data-lucide="check-circle" class="w-4 h-4"></i> Inscripción confirmada:
      </div>
      <div>Se reservó la vacante para el alumno de forma exitosa.</div>
    `;

    if (window.lucide) lucide.createIcons();

    loadClasesCatalog();
    loadInscripcionesRecientes();
  } catch (err) {
    alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/70 border-rose-500 text-rose-200';
    alertBox.textContent = `Error: ${err.message}`;
  }
}

async function cancelarInscripcion(id) {
  if (!confirm(`¿Deseas dar de baja la inscripción #${id}?\nSe liberará la vacante correspondiente en la clase.`)) {
    return;
  }

  try {
    const res = await fetch(`/api/triggers/cancelar/${id}`, { method: 'DELETE' });
    const data = await res.json();

    if (!res.ok) {
      alert(`Error al cancelar: ${data.message}`);
      return;
    }

    alert("Inscripción dada de baja. La vacante fue liberada.");
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

    const select = document.getElementById('triggerClaseSelect');
    if (select) {
      select.innerHTML = clases.map(c => 
        `<option value="${c.idClase}">${escapeHtml(c.nombre)} • Vacantes: ${c.cupoDisponible}/${c.cupoMaximo} (${formatDateTime(c.horario)})</option>`
      ).join('');
    }

    const cardsContainer = document.getElementById('clasesCuposCards');
    if (cardsContainer) {
      cardsContainer.innerHTML = clases.map(c => {
        const pct = Math.round(((c.cupoMaximo - c.cupoDisponible) / c.cupoMaximo) * 100);
        const isFull = c.cupoDisponible <= 0;

        return `
          <div class="p-3 rounded-xl bg-slate-950 border ${isFull ? 'border-rose-500/50 bg-rose-950/10' : 'border-slate-800'} space-y-2">
            <div class="flex items-center justify-between">
              <span class="font-bold text-white text-xs">${escapeHtml(c.nombre)}</span>
              <div class="flex items-center gap-1.5">
                <span class="text-[10px] font-mono px-2 py-0.5 rounded ${isFull ? 'bg-rose-500/20 text-rose-400 border border-rose-500/30' : 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'}">
                  ${isFull ? 'COMPLETO' : `${c.cupoDisponible} libres`}
                </span>
                <button type="button" onclick="eliminarClase(${c.idClase}, '${escapeHtml(c.nombre)}')" class="p-1 rounded text-slate-500 hover:text-rose-400 hover:bg-rose-500/10 transition" title="Cancelar / Eliminar clase">
                  <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
                </button>
              </div>
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

      if (window.lucide) lucide.createIcons();
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
            Dar de Baja
          </button>
        </td>
      </tr>
    `).join('');
  } catch (err) {
    console.error(err);
  }
}

// 3. ASISTENCIAS
async function runBuscarAsistencias(e) {
  e.preventDefault();
  const socioId = document.getElementById('spSocioSelect').value;
  const desde = document.getElementById('spFechaDesde').value;
  const hasta = document.getElementById('spFechaHasta').value;

  if (!socioId) return;

  const tbody = document.getElementById('spAsistenciasBody');
  const countBadge = document.getElementById('asistenciasCountBadge');
  tbody.innerHTML = '<tr><td colspan="4" class="p-3 text-center text-slate-400">Consultando asistencias...</td></tr>';

  try {
    const res = await fetch(`/api/procedimientos/asistencias-socio?socioId=${socioId}&desde=${desde}&hasta=${hasta}`);
    const data = await res.json();

    if (countBadge) countBadge.textContent = `${data.length} clases encontradas`;

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

// 4. RESUMEN DE CAJA
async function loadRecaudacionMensual() {
  const tbody = document.getElementById('recaudacionTableBody');
  if (!tbody) return;

  tbody.innerHTML = '<tr><td colspan="4" class="p-4 text-center text-slate-500">Cargando resumen de caja...</td></tr>';

  try {
    const res = await fetch('/api/vistas/recaudacion-mensual');
    const data = await res.json();

    if (data.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="p-4 text-center text-slate-500">No hay movimientos de caja registrados.</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(item => `
      <tr class="hover:bg-slate-800/40 transition">
        <td class="p-3 font-semibold text-white">${item.anio} - Mes ${item.mes}</td>
        <td class="p-3 font-mono text-slate-300">${item.cantidadTransacciones} cobros</td>
        <td class="p-3 font-mono text-emerald-400 font-bold">${formatCurrency(item.totalRecaudado)}</td>
        <td class="p-3 font-mono text-indigo-300">${formatCurrency(item.ticketPromedio)}</td>
      </tr>
    `).join('');
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="4" class="p-4 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

// Catálogos para selectors
async function loadCatalogs() {
  try {
    const [sociosRes, contratosRes] = await Promise.all([
      fetch('/api/catalogos/socios'),
      fetch('/api/catalogos/contratos')
    ]);

    const socios = await sociosRes.json();
    const contratos = await contratosRes.json();
    contratosCache = contratos || [];

    const spSocioSelect = document.getElementById('spSocioSelect');
    const triggerSocioSelect = document.getElementById('triggerSocioSelect');

    const sociosOptions = socios.map(s => 
      `<option value="${s.idSocio}">#${s.idSocio} - ${escapeHtml(s.nombreCompleto)} (${escapeHtml(s.email)})</option>`
    ).join('');

    if (spSocioSelect) spSocioSelect.innerHTML = sociosOptions;
    if (triggerSocioSelect) triggerSocioSelect.innerHTML = sociosOptions;

    const contratoSelect = document.getElementById('spContratoSelect');
    if (contratoSelect) {
      const prevVal = contratoSelect.value;
      contratoSelect.innerHTML = '<option value="">-- Seleccionar Contrato a Cobrar --</option>' +
        contratos.map(c => {
          const saldo = formatCurrency(c.saldoPendiente);
          const estado = c.estadoPago ? c.estadoPago.toUpperCase() : 'ACTIVO';
          return `<option value="${c.idSocioMembresia}">Contrato #${c.idSocioMembresia} - ${escapeHtml(c.socio)} | ${escapeHtml(c.planContratado)} (Saldo: ${saldo} - [${estado}])</option>`;
        }).join('');

      if (prevVal && contratos.some(c => c.idSocioMembresia === parseInt(prevVal))) {
        contratoSelect.value = prevVal;
      } else if (contratos.length > 0) {
        // Seleccionar por defecto el primero que tenga saldo pendiente
        const conDeuda = contratos.find(c => c.saldoPendiente > 0);
        contratoSelect.value = conDeuda ? conDeuda.idSocioMembresia : contratos[0].idSocioMembresia;
      }
      onSelectContratoCobro();
    }

    loadClasesCatalog();

  } catch (err) {
    console.error('Error cargando catálogos:', err);
  }
}

// 4. ALTA Y GESTIÓN DE CLASES
function toggleFormNuevaClase(forceState) {
  const container = document.getElementById('formNuevaClaseContainer');
  const btnText = document.getElementById('btnTextNuevaClase');
  const isHidden = container.classList.contains('hidden');
  const shouldOpen = forceState !== undefined ? forceState : isHidden;

  if (shouldOpen) {
    container.classList.remove('hidden');
    btnText.textContent = 'Cerrar Formulario';
    loadProfesoresCatalog();
    const now = new Date();
    now.setHours(now.getHours() + 1);
    now.setMinutes(0, 0, 0);
    const localIso = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    const inputHorario = document.getElementById('nuevaClaseHorario');
    if (inputHorario) inputHorario.value = localIso;
    document.getElementById('nuevaClaseNombre').focus();
  } else {
    container.classList.add('hidden');
    btnText.textContent = 'Nueva Clase';
  }
  if (window.lucide) lucide.createIcons();
}

async function loadProfesoresCatalog() {
  const select = document.getElementById('nuevaClaseProfesor');
  if (!select || select.children.length > 1) return;

  try {
    const res = await fetch('/api/catalogos/profesores');
    const profesores = await res.json();
    select.innerHTML = '<option value="">-- Seleccionar Profesor --</option>' +
      profesores.map(p => `<option value="${p.idEmpleado}">${escapeHtml(p.nombreCompleto)} (${escapeHtml(p.rol)})</option>`).join('');
  } catch (err) {
    console.error('Error cargando profesores:', err);
  }
}

async function guardarNuevaClase(e) {
  e.preventDefault();
  const nombre = document.getElementById('nuevaClaseNombre').value.trim();
  const idProfesor = parseInt(document.getElementById('nuevaClaseProfesor').value);
  const horario = document.getElementById('nuevaClaseHorario').value;
  const cupoMaximo = parseInt(document.getElementById('nuevaClaseCupo').value);

  const btn = document.getElementById('btnSubmitNuevaClase');
  btn.disabled = true;

  try {
    const res = await fetch('/api/operaciones/clases', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        nombre,
        idProfesor,
        horario: new Date(horario).toISOString(),
        cupoMaximo
      })
    });

    const data = await res.json();
    if (!res.ok) {
      alert(`Error al crear la clase: ${data.message || data.detail || 'Verifica los campos'}`);
      return;
    }

    alert(data.message || 'Clase creada exitosamente.');
    document.getElementById('formCrearClase').reset();
    toggleFormNuevaClase(false);
    loadClasesCatalog();
  } catch (err) {
    alert(`Error de conexión: ${err.message}`);
  } finally {
    btn.disabled = false;
  }
}

async function eliminarClase(idClase, nombre) {
  if (!confirm(`¿Estás seguro de que deseas cancelar la clase "${nombre}" (#${idClase})?\nSe eliminarán las reservas e inscripciones asociadas para liberar el cronograma.`)) {
    return;
  }

  try {
    const res = await fetch(`/api/operaciones/clases/${idClase}`, { method: 'DELETE' });
    const data = await res.json();
    if (!res.ok) {
      alert(`Error al cancelar la clase: ${data.message || data.detail}`);
      return;
    }
    alert(data.message || 'Clase cancelada exitosamente.');
    loadClasesCatalog();
    loadInscripcionesRecientes();
  } catch (err) {
    alert(`Error de red: ${err.message}`);
  }
}

// 5. COMPROBANTE DIGITAL DE PAGO
function mostrarComprobantePago(reciboNum, fecha, socioNombre, concepto, monto) {
  const modal = document.getElementById('modalComprobantePago');
  if (!modal) return;

  document.getElementById('reciboNumero').textContent = reciboNum || 'REC-0001';
  document.getElementById('reciboFecha').textContent = formatDateTime(fecha || new Date().toISOString());
  document.getElementById('reciboSocio').textContent = socioNombre || 'Cliente Registrado';
  document.getElementById('reciboConcepto').textContent = concepto || 'Cobro de Cuota / Membresía';
  document.getElementById('reciboMonto').textContent = formatCurrency(monto || 0);

  modal.classList.remove('hidden');
  if (window.lucide) lucide.createIcons();
}

function cerrarModalComprobante() {
  const modal = document.getElementById('modalComprobantePago');
  if (modal) modal.classList.add('hidden');
}

function imprimirComprobante() {
  window.print();
}
