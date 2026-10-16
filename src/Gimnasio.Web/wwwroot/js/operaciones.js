// GimnasioDB - Módulo de Operaciones & Clases (operaciones.js)

let currentSubTab = 'cobros';

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

  // Cargar catálogos iniciales
  await loadCatalogs();
  await loadContratosVigentes();
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

  if (subTabId === 'cobros') loadContratosVigentes();
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

  tbody.innerHTML = '<tr><td colspan="5" class="p-3 text-center text-slate-500">Cargando contratos...</td></tr>';

  try {
    const res = await fetch('/api/vistas/socios-membresias-activas');
    const data = await res.json();

    if (countBadge) countBadge.textContent = `${data.length} vigentes`;

    if (data.length === 0) {
      tbody.innerHTML = '<tr><td colspan="5" class="p-3 text-center text-slate-500">No hay contratos activos registrados.</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(item => {
      let badgeColor = 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
      if (item.diasRestantes <= 0) badgeColor = 'bg-rose-500/10 text-rose-400 border-rose-500/20';
      else if (item.diasRestantes <= 7) badgeColor = 'bg-amber-500/10 text-amber-400 border-amber-500/20';

      return `
        <tr class="hover:bg-slate-800/40">
          <td class="p-2.5">
            <span class="font-medium text-white block">${escapeHtml(item.socio)}</span>
            <span class="text-[11px] text-slate-500">${escapeHtml(item.email)}</span>
          </td>
          <td class="p-2.5 text-indigo-300 font-medium">${escapeHtml(item.planContratado)}</td>
          <td class="p-2.5 text-slate-400 font-mono">${formatDate(item.fechaInicio)}</td>
          <td class="p-2.5 text-slate-400 font-mono">${formatDate(item.fechaVencimiento)}</td>
          <td class="p-2.5">
            <span class="px-2 py-0.5 rounded text-[11px] font-bold border ${badgeColor}">
              ${item.diasRestantes > 0 ? `${item.diasRestantes} días` : 'Vencido'}
            </span>
          </td>
        </tr>
      `;
    }).join('');

  } catch (err) {
    console.error(err);
    tbody.innerHTML = `<tr><td colspan="5" class="p-3 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

async function runRegistrarPago(e) {
  e.preventDefault();
  const contratoId = document.getElementById('spContratoSelect').value;
  const monto = parseFloat(document.getElementById('spPagoMonto').value);
  const fecha = document.getElementById('spPagoFecha').value;

  const statusBox = document.getElementById('spPagoStatus');
  statusBox.className = 'p-3 rounded-lg text-xs border bg-slate-950 text-slate-400 border-slate-700';
  statusBox.textContent = 'Procesando el cobro en caja...';
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
      statusBox.innerHTML = `<strong>Error al procesar:</strong><br>${escapeHtml(data.message || 'Error desconocido')}`;
      return;
    }

    const socioLabel = document.getElementById('spContratoSelect')?.selectedOptions[0]?.text || 'Socio';
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

    loadContratosVigentes();
    loadCatalogs();
  } catch (err) {
    statusBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-600 text-rose-200';
    statusBox.innerHTML = `<strong>Fallo de conexión:</strong> ${err.message}`;
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

    const spSocioSelect = document.getElementById('spSocioSelect');
    const triggerSocioSelect = document.getElementById('triggerSocioSelect');

    const sociosOptions = socios.map(s => 
      `<option value="${s.idSocio}">#${s.idSocio} - ${escapeHtml(s.nombreCompleto)} (${escapeHtml(s.email)})</option>`
    ).join('');

    if (spSocioSelect) spSocioSelect.innerHTML = sociosOptions;
    if (triggerSocioSelect) triggerSocioSelect.innerHTML = sociosOptions;

    const contratoSelect = document.getElementById('spContratoSelect');
    if (contratoSelect) {
      contratoSelect.innerHTML = contratos.map(c => 
        `<option value="${c.idSocioMembresia}">Contrato #${c.idSocioMembresia} - ${escapeHtml(c.socio)} (${escapeHtml(c.planContratado)}: ${formatCurrency(c.precioPlan)})</option>`
      ).join('');

      if (contratos.length > 0) {
        document.getElementById('spPagoMonto').value = contratos[0].precioPlan;
      }
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
