// GimnasioDB - Funciones y Componentes Compartidos (shared.js)

// Renderiza el encabezado y navegación unificada en todas las páginas
function renderSharedHeader(activePage) {
  const headerContainer = document.getElementById('shared-header');
  if (!headerContainer) return;

  const navItems = [
    { id: 'dashboard', label: 'Panel General', icon: 'layout-dashboard', href: '/' },
    { id: 'personas', label: 'Gestión de Personas', icon: 'users', href: '/personas' },
    { id: 'membresias', label: 'Membresías de Socios', icon: 'id-card', href: '/membresias' },
    { id: 'operaciones', label: 'Operaciones & Clases', icon: 'credit-card', href: '/operaciones' },
    { id: 'studio', label: 'SQL Studio & Tablas', icon: 'terminal', href: '/studio' }
  ];

  headerContainer.innerHTML = `
    <header class="border-b border-slate-800 bg-slate-900/90 backdrop-blur sticky top-0 z-50">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <a href="/" class="flex items-center gap-3 group">
            <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 flex items-center justify-center shadow-lg shadow-indigo-500/20 text-white font-black text-xl group-hover:scale-105 transition">
              G
            </div>
            <div>
              <div class="flex items-center gap-2">
                <h1 class="text-lg font-bold tracking-tight text-white group-hover:text-indigo-300 transition">GimnasioDB</h1>
                <span class="text-[10px] font-semibold tracking-wider uppercase px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">Sistema Activo</span>
              </div>
              <p class="text-xs text-slate-400">Gestión Comercial y Operativa de Socios, Clases y Pagos</p>
            </div>
          </a>
        </div>

        <!-- Health / Connection Pill -->
        <div id="connectionStatus" class="flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-slate-800/80 border border-slate-700 text-xs">
          <span class="relative flex h-2.5 w-2.5">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500"></span>
          </span>
          <span class="text-slate-300 font-medium">Base de Datos Conectada</span>
          <span id="dbLatencyBadge" class="text-slate-500 text-[11px] font-mono">-- ms</span>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 border-t border-slate-800/60 overflow-x-auto flex gap-1 py-1">
        ${navItems.map(item => {
          const isActive = item.id === activePage;
          const activeClass = isActive 
            ? 'text-white bg-indigo-600 shadow-sm' 
            : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800';
          return `
            <a href="${item.href}" class="flex items-center gap-2 px-3.5 py-2 text-xs font-medium rounded-lg transition-all ${activeClass}">
              <i data-lucide="${item.icon}" class="w-4 h-4"></i> ${item.label}
            </a>
          `;
        }).join('')}
      </div>
    </header>
  `;

  if (window.lucide) lucide.createIcons();
  checkConnectionHealth();
}

// Renderiza el pie de página común
function renderSharedFooter() {
  const footerContainer = document.getElementById('shared-footer');
  if (!footerContainer) return;

  footerContainer.innerHTML = `
    <footer class="border-t border-slate-800/80 bg-slate-950 py-4 text-center text-xs text-slate-500 mt-auto">
      <div class="max-w-7xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-2">
        <div>GimnasioDB • Suite de Gestión & Administración © 2026</div>
        <div class="font-mono text-slate-600 text-[11px]">Plataforma Comercial de Alto Rendimiento • ASP.NET Core & Dapper</div>
      </div>
    </footer>
  `;
}

// Verifica la salud de la conexión a SQL Server
async function checkConnectionHealth() {
  const latencyBadge = document.getElementById('dbLatencyBadge');
  const statusPill = document.getElementById('connectionStatus');
  if (!latencyBadge || !statusPill) return;

  try {
    const res = await fetch('/api/dashboard/stats');
    if (!res.ok) throw new Error('Error al conectar');
    const data = await res.json();
    latencyBadge.textContent = `${data.databaseLatencyMs} ms`;
    statusPill.className = "flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-slate-800/80 border border-emerald-500/30 text-xs";
  } catch {
    latencyBadge.textContent = 'Desconectado';
    statusPill.className = "flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-rose-950/60 border border-rose-500/40 text-xs";
  }
}

// Funciones de formateo auxiliares
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
      ? '<span class="text-emerald-400 font-bold">1 (SÍ)</span>' 
      : '<span class="text-rose-400 font-bold">0 (NO)</span>';
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
