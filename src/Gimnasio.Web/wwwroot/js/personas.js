// GimnasioDB - Módulo de Personas (personas.js)

let searchTimeout = null;

document.addEventListener('DOMContentLoaded', async () => {
  renderSharedHeader('personas');
  renderSharedFooter();

  const today = new Date().toISOString().split('T')[0];
  const socioFechaRegistro = document.getElementById('socioFechaRegistro');
  const empleadoFechaContratacion = document.getElementById('empleadoFechaContratacion');

  if (socioFechaRegistro) socioFechaRegistro.value = today;
  if (empleadoFechaContratacion) empleadoFechaContratacion.value = today;

  await loadRolesCatalog();
  await loadPersonas();
});

function toggleFormPersona(forceState) {
  const container = document.getElementById('formNuevaPersonaContainer');
  const btnText = document.getElementById('btnToggleFormPersonaText');
  const isHidden = container.classList.contains('hidden');
  const shouldOpen = forceState !== undefined ? forceState : isHidden;

  if (shouldOpen) {
    container.classList.remove('hidden');
    btnText.textContent = 'Cerrar Formulario';
    document.getElementById('personaNombre').focus();
    loadRolesCatalog();
  } else {
    container.classList.add('hidden');
    btnText.textContent = 'Registrar Nueva Persona';
  }
  if (window.lucide) lucide.createIcons();
}

function toggleSocioFields(checked) {
  const container = document.getElementById('socioFieldsContainer');
  if (checked) {
    container.classList.remove('hidden');
  } else {
    container.classList.add('hidden');
  }
}

function toggleEmpleadoFields(checked) {
  const container = document.getElementById('empleadoFieldsContainer');
  if (checked) {
    container.classList.remove('hidden');
    loadRolesCatalog();
  } else {
    container.classList.add('hidden');
  }
}

async function loadRolesCatalog() {
  const select = document.getElementById('empleadoRolSelect');
  if (!select || select.children.length > 1) return;

  try {
    const res = await fetch('/api/catalogos/roles');
    const roles = await res.json();
    select.innerHTML = '<option value="">Selecciona un rol...</option>' + 
      roles.map(r => `<option value="${r.idRol}">${escapeHtml(r.nombreRol)}</option>`).join('');
  } catch (err) {
    console.error('Error cargando roles:', err);
  }
}

function debounceBuscarPersonas() {
  clearTimeout(searchTimeout);
  searchTimeout = setTimeout(() => {
    loadPersonas();
  }, 250);
}

async function loadPersonas() {
  const searchInput = document.getElementById('personasSearchInput');
  const tipoFilter = document.getElementById('personasTipoFilter');
  const countBadge = document.getElementById('personasCountBadge');
  const tbody = document.getElementById('personasTableBody');

  const busqueda = searchInput ? searchInput.value.trim() : '';
  const tipo = tipoFilter ? tipoFilter.value : '';

  countBadge.textContent = 'Buscando registros...';
  tbody.innerHTML = '<tr><td colspan="7" class="p-4 text-center text-slate-500">Cargando directorio...</td></tr>';

  try {
    let url = `/api/personas?`;
    if (busqueda) url += `busqueda=${encodeURIComponent(busqueda)}&`;
    if (tipo) url += `tipo=${encodeURIComponent(tipo)}`;

    const res = await fetch(url);
    const data = await res.json();

    countBadge.textContent = `${data.length} personas registradas`;

    if (data.length === 0) {
      tbody.innerHTML = '<tr><td colspan="7" class="p-4 text-center text-slate-500">No se encontraron personas con los criterios indicados.</td></tr>';
      return;
    }

    tbody.innerHTML = data.map(item => {
      let perfilBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-800 text-slate-300 border border-slate-700">General</span>';
      if (item.tipo === 'Socio y Empleado') {
        perfilBadge = `
          <div class="flex flex-col gap-0.5">
            <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">Socio & Staff</span>
            <span class="text-[10px] text-purple-400 font-medium">${escapeHtml(item.nombreRol || '')}</span>
          </div>
        `;
      } else if (item.tipo === 'Socio') {
        perfilBadge = '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">Socio</span>';
      } else if (item.tipo === 'Empleado') {
        perfilBadge = `<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-purple-500/10 text-purple-300 border border-purple-500/20">Staff: ${escapeHtml(item.nombreRol || '')}</span>`;
      }

      const estadoBadge = item.activo 
        ? '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">Activo</span>'
        : '<span class="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/10 text-rose-400 border border-rose-500/20">Inactivo</span>';

      const toggleActionLabel = item.activo ? 'Desactivar' : 'Reactivar';
      const toggleActionClass = item.activo 
        ? 'text-amber-400 hover:bg-amber-500/10 border-amber-500/30' 
        : 'text-emerald-400 hover:bg-emerald-500/10 border-emerald-500/30';

      return `
        <tr class="hover:bg-slate-800/40 transition">
          <td class="p-3 font-mono text-slate-400">#${item.idPersona}</td>
          <td class="p-3">
            <div class="font-semibold text-white">${escapeHtml(item.nombre)} ${escapeHtml(item.apellido)}</div>
          </td>
          <td class="p-3">
            <div class="text-slate-300">${escapeHtml(item.email)}</div>
            <div class="text-[11px] text-slate-500 font-mono">${item.telefono ? escapeHtml(item.telefono) : 'Sin teléfono'}</div>
          </td>
          <td class="p-3">${perfilBadge}</td>
          <td class="p-3 text-slate-400">${formatDate(item.fechaNacimiento)}</td>
          <td class="p-3">${estadoBadge}</td>
          <td class="p-3 text-right">
            <div class="flex items-center justify-end gap-1.5">
              <button onclick="toggleActivoPersona(${item.idPersona}, '${escapeHtml(item.nombre)} ${escapeHtml(item.apellido)}')" 
                class="px-2.5 py-1 text-[11px] font-medium rounded border ${toggleActionClass} transition">
                ${toggleActionLabel}
              </button>
              <button onclick="eliminarPersona(${item.idPersona}, '${escapeHtml(item.nombre)} ${escapeHtml(item.apellido)}')" 
                class="px-2 py-1 text-[11px] font-medium rounded border border-rose-500/30 text-rose-400 hover:bg-rose-500/20 transition" title="Baja definitiva">
                <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
              </button>
            </div>
          </td>
        </tr>
      `;
    }).join('');

    if (window.lucide) lucide.createIcons();

  } catch (err) {
    console.error(err);
    countBadge.textContent = 'Error';
    tbody.innerHTML = `<tr><td colspan="7" class="p-4 text-center text-rose-400">${err.message}</td></tr>`;
  }
}

async function guardarNuevaPersona(e) {
  e.preventDefault();

  const nombre = document.getElementById('personaNombre').value.trim();
  const apellido = document.getElementById('personaApellido').value.trim();
  const email = document.getElementById('personaEmail').value.trim();
  const telefono = document.getElementById('personaTelefono').value.trim() || null;
  const fechaNac = document.getElementById('personaFechaNac').value;

  const esSocio = document.getElementById('checkEsSocio').checked;
  const socioFecha = document.getElementById('socioFechaRegistro').value;

  const esEmpleado = document.getElementById('checkEsEmpleado').checked;
  const empleadoRol = document.getElementById('empleadoRolSelect').value;
  const empleadoFecha = document.getElementById('empleadoFechaContratacion').value;

  const alertBox = document.getElementById('personaFormStatusAlert');
  const btnSubmit = document.getElementById('btnGuardarPersona');

  if (esEmpleado && !empleadoRol) {
    alertBox.className = 'p-3 rounded-lg text-xs border bg-amber-950/60 border-amber-500 text-amber-200';
    alertBox.textContent = 'Debe seleccionar un rol para el empleado.';
    alertBox.classList.remove('hidden');
    return;
  }

  btnSubmit.disabled = true;
  btnSubmit.innerHTML = '<i data-lucide="loader" class="w-4 h-4 animate-spin"></i> Guardando...';

  try {
    const payload = {
      nombre,
      apellido,
      email,
      telefono,
      fechaNacimiento: new Date(fechaNac).toISOString(),
      esSocio,
      fechaRegistro: esSocio && socioFecha ? new Date(socioFecha).toISOString() : null,
      esEmpleado,
      idRol: esEmpleado && empleadoRol ? parseInt(empleadoRol) : null,
      fechaContratacion: esEmpleado && empleadoFecha ? new Date(empleadoFecha).toISOString() : null
    };

    const res = await fetch('/api/personas', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    const data = await res.json();

    if (!res.ok) {
      alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-500 text-rose-200';
      alertBox.textContent = data.message || 'Error al guardar la persona.';
      alertBox.classList.remove('hidden');
      return;
    }

    alertBox.className = 'p-3 rounded-lg text-xs border bg-emerald-950/60 border-emerald-500 text-emerald-200';
    alertBox.textContent = `¡Registro exitoso! ${data.message} (ID: #${data.idPersona})`;
    alertBox.classList.remove('hidden');

    document.getElementById('formCrearPersona').reset();
    document.getElementById('checkEsSocio').checked = true;
    toggleSocioFields(true);
    toggleEmpleadoFields(false);

    loadPersonas();

    setTimeout(() => {
      toggleFormPersona(false);
      alertBox.classList.add('hidden');
    }, 1800);

  } catch (err) {
    alertBox.className = 'p-3 rounded-lg text-xs border bg-rose-950/60 border-rose-500 text-rose-200';
    alertBox.textContent = `Error de conexión: ${err.message}`;
    alertBox.classList.remove('hidden');
  } finally {
    btnSubmit.disabled = false;
    btnSubmit.innerHTML = '<i data-lucide="check" class="w-4 h-4"></i> Guardar Persona';
    if (window.lucide) lucide.createIcons();
  }
}

async function toggleActivoPersona(idPersona, nombre) {
  try {
    const res = await fetch(`/api/personas/${idPersona}/toggle-activo`, { method: 'PUT' });
    const data = await res.json();
    if (!res.ok) {
      alert(`Error: ${data.message}`);
      return;
    }
    loadPersonas();
  } catch (err) {
    alert(`Error de red: ${err.message}`);
  }
}

async function eliminarPersona(idPersona, nombre) {
  if (!confirm(`¿Deseas dar de baja definitiva a ${nombre} (#${idPersona})?\nEsta acción no se puede deshacer.`)) {
    return;
  }

  try {
    const res = await fetch(`/api/personas/${idPersona}`, { method: 'DELETE' });
    const data = await res.json();

    if (!res.ok) {
      alert(data.message || 'No se pudo eliminar la persona.');
      return;
    }

    alert(data.message || 'Persona eliminada exitosamente.');
    loadPersonas();
  } catch (err) {
    alert(`Error de red: ${err.message}`);
  }
}
