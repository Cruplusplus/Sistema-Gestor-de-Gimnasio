// GimnasioDB - Módulo de Membresías y Contratos (membresias.js)

let planesCache = [];
let debounceTimer = null;

document.addEventListener('DOMContentLoaded', async () => {
  renderSharedHeader('membresias');
  renderSharedFooter();

  // Fecha de inicio por defecto: Hoy
  const today = new Date().toISOString().split('T')[0];
  const inputFechaInicio = document.getElementById('inputFechaInicio');
  if (inputFechaInicio) inputFechaInicio.value = today;

  await Promise.all([
    cargarPlanes(),
    cargarSociosSelect()
  ]);

  await cargarContratos();
});

// Control del formulario desplegable
function toggleFormAsignacion(forceState) {
  const container = document.getElementById('formAsignacionContainer');
  const btnText = document.getElementById('btnToggleText');
  const isHidden = container.classList.contains('hidden');
  const shouldOpen = forceState !== undefined ? forceState : isHidden;

  if (shouldOpen) {
    container.classList.remove('hidden');
    btnText.textContent = 'Cerrar Formulario';
    document.getElementById('selectSocio').focus();
  } else {
    container.classList.add('hidden');
    btnText.textContent = 'Asignar Membresía a Socio';
  }
  if (window.lucide) lucide.createIcons();
}

// Carga del catálogo de planes
async function cargarPlanes() {
  const selectPlan = document.getElementById('selectPlan');
  const planesGrid = document.getElementById('planesGrid');
  const cantPlanesBadge = document.getElementById('cantPlanesBadge');

  try {
    const res = await fetch('/api/membresias/planes');
    if (!res.ok) throw new Error('Error al consultar planes');
    planesCache = await res.json();

    if (cantPlanesBadge) cantPlanesBadge.textContent = `${planesCache.length} planes activos`;

    // Llenar combo select
    if (selectPlan) {
      selectPlan.innerHTML = '<option value="">-- Seleccionar Plan de Membresía --</option>' +
        planesCache.map(p => `
          <option value="${p.idMembresia}">
            ${escapeHtml(p.nombre)} (${p.duracionDias} días) • ${formatCurrency(p.precio)}
          </option>
        `).join('');
    }

    // Renderizar tarjetas de planes
    if (planesGrid) {
      if (planesCache.length === 0) {
        planesGrid.innerHTML = '<div class="col-span-4 p-4 text-center text-slate-500">No hay planes configurados en el catálogo.</div>';
        return;
      }

      planesGrid.innerHTML = planesCache.map(p => `
        <div class="bg-slate-900 border border-slate-800 rounded-xl p-4 flex flex-col justify-between hover:border-indigo-500/40 hover:bg-slate-900/90 transition group shadow-sm">
          <div>
            <div class="flex items-center justify-between mb-2">
              <span class="p-2 rounded-lg bg-indigo-500/10 text-indigo-400 group-hover:scale-105 transition">
                <i data-lucide="award" class="w-4 h-4"></i>
              </span>
              <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-800 text-slate-400 border border-slate-700">
                ${p.duracionDias} Días
              </span>
            </div>
            <h4 class="font-bold text-white text-sm mb-1 group-hover:text-indigo-300 transition">${escapeHtml(p.nombre)}</h4>
            <div class="text-xl font-extrabold text-emerald-400 font-mono mb-2">${formatCurrency(p.precio)}</div>
          </div>
          <button type="button" onclick="seleccionarPlanDirecto(${p.idMembresia})" class="w-full mt-3 flex items-center justify-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-indigo-600 text-slate-300 hover:text-white transition border border-slate-700 hover:border-indigo-500">
            <i data-lucide="user-check" class="w-3.5 h-3.5"></i> Asignar a Socio
          </button>
        </div>
      `).join('');

      if (window.lucide) lucide.createIcons();
    }
  } catch (err) {
    console.error(err);
    if (planesGrid) planesGrid.innerHTML = `<div class="col-span-4 p-4 text-center text-rose-400 text-xs">Error cargando planes: ${err.message}</div>`;
  }
}

// Carga de lista de socios para el selector
async function cargarSociosSelect() {
  const selectSocio = document.getElementById('selectSocio');
  if (!selectSocio) return;

  try {
    const res = await fetch('/api/catalogos/socios');
    if (!res.ok) throw new Error('Error al cargar socios');
    const socios = await res.json();

    if (socios.length === 0) {
      selectSocio.innerHTML = '<option value="">No hay socios registrados en el sistema</option>';
      return;
    }

    selectSocio.innerHTML = '<option value="">-- Seleccionar Socio --</option>' +
      socios.map(s => `
        <option value="${s.idSocio}">
          ${escapeHtml(s.nombreCompleto)} (Socio #${s.idSocio} • ${escapeHtml(s.email)})
        </option>
      `).join('');
  } catch (err) {
    console.error(err);
    selectSocio.innerHTML = `<option value="">Error cargando socios</option>`;
  }
}

// Selección directa de plan desde las tarjetas
function seleccionarPlanDirecto(idPlan) {
  toggleFormAsignacion(true);
  const select = document.getElementById('selectPlan');
  if (select) {
    select.value = idPlan;
    onPlanChange();
  }
  const selectSocio = document.getElementById('selectSocio');
  if (selectSocio) selectSocio.focus();
}

// Al cambiar el plan seleccionado en el formulario
function onPlanChange() {
  const select = document.getElementById('selectPlan');
  const planId = parseInt(select.value);
  const hint = document.getElementById('planDetailsHint');
  const inputMonto = document.getElementById('inputMontoPago');

  if (!planId) {
    if (hint) hint.textContent = 'Selecciona un plan para auto-calcular duración y costos.';
    return;
  }

  const plan = planesCache.find(p => p.idMembresia === planId);
  if (plan) {
    if (hint) {
      hint.textContent = `Vigencia estimada: ${plan.duracionDias} días corridos. Arancel sugerido: ${formatCurrency(plan.precio)}.`;
    }
    if (inputMonto && (!inputMonto.value || inputMonto.value == '0')) {
      inputMonto.value = plan.precio;
    }
    recalcularFechaFin();
  }
}

// Recalcular la fecha de fin sumando los días del plan a la fecha de inicio
function recalcularFechaFin() {
  const select = document.getElementById('selectPlan');
  const planId = parseInt(select.value);
  const inputFechaInicio = document.getElementById('inputFechaInicio');
  const inputFechaFin = document.getElementById('inputFechaFin');

  if (!planId || !inputFechaInicio || !inputFechaInicio.value) return;

  const plan = planesCache.find(p => p.idMembresia === planId);
  if (!plan) return;

  const [y, m, d] = inputFechaInicio.value.split('-').map(Number);
  const fechaFinDate = new Date(y, m - 1, d);
  fechaFinDate.setDate(fechaFinDate.getDate() + plan.duracionDias);

  const yyyy = fechaFinDate.getFullYear();
  const mm = String(fechaFinDate.getMonth() + 1).padStart(2, '0');
  const dd = String(fechaFinDate.getDate()).padStart(2, '0');

  if (inputFechaFin) inputFechaFin.value = `${yyyy}-${mm}-${dd}`;
}

// Mostrar/ocultar campos de pago inicial
function togglePagoInicialFields(checked) {
  const container = document.getElementById('pagoInicialContainer');
  const inputMonto = document.getElementById('inputMontoPago');
  if (!container) return;

  if (checked) {
    container.classList.remove('hidden');
    const select = document.getElementById('selectPlan');
    const planId = parseInt(select.value);
    const plan = planesCache.find(p => p.idMembresia === planId);
    if (plan && (!inputMonto.value || inputMonto.value == '0')) {
      inputMonto.value = plan.precio;
    }
    inputMonto.focus();
  } else {
    container.classList.add('hidden');
  }
}

// Guardar nueva asignación de contrato
async function guardarAsignacionMembresia(e) {
  e.preventDefault();

  const alertContainer = document.getElementById('alertContainer');
  const btnSubmit = document.getElementById('btnSubmitMembresia');

  const idSocioVal = document.getElementById('selectSocio').value;
  const idMembresiaVal = document.getElementById('selectPlan').value;
  const fechaInicioVal = document.getElementById('inputFechaInicio').value;
  const fechaFinVal = document.getElementById('inputFechaFin').value;
  const registrarPago = document.getElementById('checkPagoInicial').checked;
  const montoPagoVal = document.getElementById('inputMontoPago').value;
  const metodoPagoVal = document.getElementById('selectMetodoPago').value;

  if (!idSocioVal || !idMembresiaVal || !fechaInicioVal || !fechaFinVal) {
    showAlert('danger', 'Por favor completa todos los campos requeridos (Socio, Plan, Fecha de Inicio y Fin).');
    return;
  }

  const payload = {
    idSocio: parseInt(idSocioVal),
    idMembresia: parseInt(idMembresiaVal),
    fechaInicio: new Date(fechaInicioVal + 'T00:00:00').toISOString(),
    fechaFin: new Date(fechaFinVal + 'T23:59:59').toISOString(),
    registrarPagoInicial: registrarPago,
    montoPagoInicial: registrarPago && montoPagoVal ? parseFloat(montoPagoVal) : null,
    metodoPagoInicial: registrarPago ? metodoPagoVal : null
  };

  btnSubmit.disabled = true;
  btnSubmit.innerHTML = '<i data-lucide="loader-2" class="w-4 h-4 animate-spin"></i> Registrando Contrato...';
  if (window.lucide) lucide.createIcons();

  try {
    const res = await fetch('/api/membresias/contratos', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();

    if (!res.ok) {
      showAlert('danger', data.message || 'Error al asignar la membresía.');
      return;
    }

    showAlert('success', `¡Membresía registrada con éxito! Contrato #${data.idSocioMembresia}.`);

    // Resetear formulario
    document.getElementById('formRegistrarMembresia').reset();
    document.getElementById('checkPagoInicial').checked = false;
    togglePagoInicialFields(false);
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('inputFechaInicio').value = today;

    // Recargar tabla de contratos
    await cargarContratos();

    setTimeout(() => {
      toggleFormAsignacion(false);
      hideAlert();
    }, 2200);

  } catch (err) {
    showAlert('danger', `Error de conexión: ${err.message}`);
  } finally {
    btnSubmit.disabled = false;
    btnSubmit.innerHTML = '<i data-lucide="check" class="w-4 h-4"></i> Confirmar Contrato';
    if (window.lucide) lucide.createIcons();
  }
}

// Cargar tabla de contratos con filtros y estadísticas
async function cargarContratos() {
  const tbody = document.getElementById('tablaContratosBody');
  const busqueda = document.getElementById('filtroContratosBusqueda')?.value || '';
  const soloActivas = document.getElementById('filtroSoloActivas')?.checked || false;

  const statTotal = document.getElementById('statTotalContratos');
  const statActivos = document.getElementById('statContratosActivos');
  const statPorVencer = document.getElementById('statPorVencer');
  const statRecaudado = document.getElementById('statRecaudadoTotal');

  try {
    const url = `/api/membresias/contratos?busqueda=${encodeURIComponent(busqueda)}&soloActivas=${soloActivas}`;
    const res = await fetch(url);
    if (!res.ok) throw new Error('Error al consultar contratos');
    const data = await res.json();

    // Actualizar estadísticas rápidas
    if (statTotal) statTotal.textContent = data.length;
    let activosCount = 0;
    let porVencerCount = 0;
    let totalCobradoSuma = 0;

    data.forEach(item => {
      if (item.activa) activosCount++;
      if (item.activa && item.diasRestantes >= 0 && item.diasRestantes <= 7) porVencerCount++;
      totalCobradoSuma += (item.totalPagado || 0);
    });

    if (statActivos) statActivos.textContent = activosCount;
    if (statPorVencer) statPorVencer.textContent = porVencerCount;
    if (statRecaudado) statRecaudado.textContent = formatCurrency(totalCobradoSuma);

    if (data.length === 0) {
      tbody.innerHTML = `
        <tr>
          <td colspan="6" class="px-4 py-8 text-center text-slate-500">
            No se encontraron contratos de membresía con los filtros seleccionados.
          </td>
        </tr>
      `;
      return;
    }

    tbody.innerHTML = data.map(item => {
      // Badge de vigencia / estado
      let estadoBadge = '';
      if (!item.activa) {
        estadoBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-slate-700">Pausada / Inactiva</span>';
      } else if (item.diasRestantes < 0) {
        estadoBadge = `<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/10 text-rose-400 border border-rose-500/30">Vencida hace ${Math.abs(item.diasRestantes)} días</span>`;
      } else if (item.diasRestantes <= 7) {
        estadoBadge = `<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/10 text-amber-400 border border-amber-500/30">Vence en ${item.diasRestantes} días</span>`;
      } else {
        estadoBadge = `<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">Vigente (${item.diasRestantes} días)</span>`;
      }

      const toggleActionLabel = item.activa ? 'Pausar' : 'Reactivar';
      const toggleActionClass = item.activa 
        ? 'text-amber-400 hover:bg-amber-500/10 border-amber-500/30' 
        : 'text-emerald-400 hover:bg-emerald-500/10 border-emerald-500/30';

      return `
        <tr class="hover:bg-slate-800/40 transition">
          <td class="px-4 py-3">
            <div class="font-semibold text-white">${escapeHtml(item.nombreSocio)} ${escapeHtml(item.apellidoSocio)}</div>
            <div class="text-[11px] text-slate-400">${escapeHtml(item.emailSocio)} • <span class="font-mono text-slate-500">Socio #${item.idSocio}</span></div>
          </td>
          <td class="px-4 py-3">
            <div class="font-medium text-slate-200 flex items-center gap-1.5">
              <i data-lucide="award" class="w-3.5 h-3.5 text-indigo-400"></i> ${escapeHtml(item.nombreMembresia)}
            </div>
            <div class="text-[11px] text-slate-500">${item.duracionDias} días de vigencia nominal</div>
          </td>
          <td class="px-4 py-3">
            <div class="text-slate-300 font-mono text-[11px]">${formatDate(item.fechaInicio)} &rarr; ${formatDate(item.fechaFin)}</div>
          </td>
          <td class="px-4 py-3">
            ${estadoBadge}
          </td>
          <td class="px-4 py-3 text-right">
            <div class="font-bold text-white font-mono">${formatCurrency(item.totalPagado)}</div>
            <div class="text-[10px] text-slate-500">${item.cantidadPagos} pago(s) registrado(s)</div>
          </td>
          <td class="px-4 py-3 text-center">
            <div class="flex items-center justify-center gap-1.5">
              <button onclick="toggleActivaContrato(${item.idSocioMembresia}, ${item.activa})" 
                class="px-2.5 py-1 text-[11px] font-medium rounded border ${toggleActionClass} transition">
                ${toggleActionLabel}
              </button>
              <a href="/operaciones" title="Registrar Cobro" 
                class="px-2 py-1 text-[11px] font-medium rounded border border-indigo-500/30 text-indigo-300 hover:bg-indigo-500/20 transition flex items-center gap-1">
                <i data-lucide="receipt" class="w-3.5 h-3.5"></i> Cobros
              </a>
            </div>
          </td>
        </tr>
      `;
    }).join('');

    if (window.lucide) lucide.createIcons();
  } catch (err) {
    console.error(err);
    tbody.innerHTML = `
      <tr>
        <td colspan="6" class="px-4 py-6 text-center text-rose-400 text-xs">
          Error al cargar los contratos: ${err.message}
        </td>
      </tr>
    `;
  }
}

// Activar / Pausar contrato
async function toggleActivaContrato(idSocioMembresia, estadoActual) {
  const accion = estadoActual ? 'pausar / dar de baja temporalmente' : 'reactivar';
  if (!confirm(`¿Estás seguro de que deseas ${accion} el contrato #${idSocioMembresia}?`)) {
    return;
  }

  try {
    const res = await fetch(`/api/membresias/contratos/${idSocioMembresia}/toggle-activa`, { method: 'PUT' });
    const data = await res.json();
    if (!res.ok) {
      showAlert('danger', data.message || 'Error al modificar el estado del contrato.');
      return;
    }
    showAlert('success', data.message);
    await cargarContratos();
  } catch (err) {
    showAlert('danger', `Error de red: ${err.message}`);
  }
}

// Búsqueda en tiempo real con debounce
function debounceBuscarContratos() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    cargarContratos();
  }, 300);
}

// Helpers de alertas
function showAlert(type, message) {
  const container = document.getElementById('alertContainer');
  if (!container) return;

  const isSuccess = type === 'success';
  container.className = isSuccess 
    ? 'p-3 rounded-lg text-xs border bg-emerald-950/60 border-emerald-500 text-emerald-200 shadow-sm'
    : 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-500 text-rose-200 shadow-sm';
  
  container.innerHTML = `
    <div class="flex items-center justify-between">
      <span>${escapeHtml(message)}</span>
      <button onclick="hideAlert()" class="text-slate-400 hover:text-white ml-2">&times;</button>
    </div>
  `;
  container.classList.remove('hidden');
}

function hideAlert() {
  const container = document.getElementById('alertContainer');
  if (container) container.classList.add('hidden');
}
