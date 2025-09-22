/* wwwroot/js/genero.js — Pasteles de "Ventas por género" por almacén */
document.addEventListener('DOMContentLoaded', () => {
    const base = (window.API_CHARTS_BASE || '/api/charts').replace(/\/+$/, '');

    // Filtros
    const elYear = document.getElementById('year');
    const elMonth = document.getElementById('month');

    // Dropdown almacenes
    const elSummary = document.getElementById('almacenSummary');
    const elFilter = document.getElementById('almacenFilter');
    const elAll = document.getElementById('almacenAll');
    const elChecks = document.getElementById('almacenChecks');

    const grid = document.getElementById('chartsGenerosGrid');

    let almacenes = [];
    let seleccionados = new Set();

    const fmtMoney = (n) => Number(n || 0).toLocaleString('es-EC', { style: 'currency', currency: 'USD' });

    function buildQuery(baseUrl, params) {
        const parts = [];
        Object.entries(params || {}).forEach(([k, v]) => {
            if (v == null) return;
            if (Array.isArray(v)) v.forEach(val => parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(val)}`));
            else {
                const s = String(v).trim();
                if (s !== '') parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(s)}`);
            }
        });
        return parts.length ? `${baseUrl}?${parts.join('&')}` : baseUrl;
    }

    async function fetchAlmacenes() {
        const r = await fetch(`${base}/almacenes-nombres? _t=${Date.now()}`, { cache: 'no-store' });
        if (!r.ok) return [];
        const data = await r.json();
        return data.map(a => ({ id: a.id, nombre: a.label ?? a.nombre ?? String(a.id) }));
    }

    function renderChecks(list) {
        elChecks.innerHTML = '';
        list.forEach(a => {
            const id = `chk-alm-${a.id}`;
            const wrap = document.createElement('div');
            wrap.className = 'form-check';
            wrap.innerHTML = `
        <input class="form-check-input almacen-check" type="checkbox" id="${id}" value="${a.id}" checked>
        <label class="form-check-label" for="${id}">${a.nombre}</label>`;
            elChecks.appendChild(wrap);
        });
        seleccionados = new Set(list.map(a => a.id));
        elAll.checked = true;
        updateSummary();
        bindCheckboxEvents();
    }

    function bindCheckboxEvents() {
        elChecks.querySelectorAll('.almacen-check').forEach(cb => {
            cb.addEventListener('change', () => {
                const id = Number(cb.value);
                if (cb.checked) seleccionados.add(id); else seleccionados.delete(id);
                elAll.checked = (seleccionados.size === almacenes.length);
                updateSummary();
                loadCharts();
            });
        });
    }

    function updateSummary() {
        if (seleccionados.size === 0) { elSummary.textContent = 'Ninguno'; return; }
        if (seleccionados.size === almacenes.length) { elSummary.textContent = 'Todos'; return; }
        const nombres = almacenes.filter(a => seleccionados.has(a.id)).map(a => a.nombre);
        elSummary.textContent = nombres.slice(0, 2).join(', ') + (nombres.length > 2 ? ` +${nombres.length - 2}` : '');
    }

    if (elAll) {
        elAll.addEventListener('change', () => {
            const chks = elChecks.querySelectorAll('.almacen-check');
            seleccionados = new Set();
            chks.forEach(chk => {
                chk.checked = elAll.checked;
                if (elAll.checked) seleccionados.add(Number(chk.value));
            });
            updateSummary();
            loadCharts();
        });
    }

    if (elFilter) {
        elFilter.addEventListener('input', () => {
            const term = elFilter.value.toLowerCase().trim();
            const filtered = term ? almacenes.filter(a => a.nombre.toLowerCase().includes(term)) : almacenes;
            renderChecks(filtered);
            loadCharts();
        });
    }

    async function loadCharts() {
        if (!grid) return;

        // Construye params
        const y = (elYear?.value || '').trim() || new Date().getFullYear();
        const m = (elMonth?.value || '').trim();

        const params = { year: y, _t: Date.now() };
        if (m !== '') params.month = m;
        // Enviar seleccionados si no son todos
        if (seleccionados.size > 0 && seleccionados.size !== almacenes.length) {
            [...seleccionados].forEach(id => params.almacenIds = [...(params.almacenIds || []), id]);
        }

        const url = buildQuery(`${base}/ventas-por-genero-por-almacen`, params);
        console.log('[ventas-por-genero-por-almacen] GET', url);

        const res = await fetch(url, { cache: 'no-store' });
        if (!res.ok) {
            grid.innerHTML = `<div class="alert alert-danger">No se pudo cargar ventas por género.</div>`;
            return;
        }

        const items = await res.json(); // [{ almacenId, almacenNombre, series: [{name,y,unidades,tickets}] }]
        if (!Array.isArray(items) || items.length === 0) {
            grid.innerHTML = `<div class="alert alert-info">Sin datos para los filtros.</div>`;
            return;
        }

        // Render grid
        grid.innerHTML = '';
        items.forEach(it => {
            const col = document.createElement('div');
            col.className = 'col-12 col-md-6 col-lg-4'; // 1-2-3 por fila
            const chartId = `pie-gen-${it.almacenId}`;
            col.innerHTML = `
        <div class="card shadow-sm h-100">
          <div class="card-body">
            <h6 class="card-title mb-3">${it.almacenNombre}</h6>
            <div id="${chartId}" style="height:320px;"></div>
          </div>
        </div>`;
            grid.appendChild(col);

            Highcharts.chart(chartId, {
                chart: { type: 'pie' },
                title: { text: null },
                plotOptions: {
                    pie: { innerSize: '60%', dataLabels: { enabled: true, format: '{point.name}: {point.percentage:.1f}%' } }
                },
                tooltip: {
                    useHTML: true,
                    pointFormatter: function () {
                        const raw = it.series.find(x => x.name === this.name) || { unidades: 0, tickets: 0 };
                        return `<span style="color:${this.color}">●</span> ${this.name}<br/>
                    Ventas: <b>${fmtMoney(this.y)}</b><br/>
                    Unidades: <b>${Number(raw.unidades || 0).toLocaleString('es-EC')}</b><br/>
                    Tickets: <b>${Number(raw.tickets || 0).toLocaleString('es-EC')}</b>`;
                    }
                },
                series: [{ name: 'Ventas', data: it.series.map(s => ({ name: s.name, y: s.y })) }],
                credits: { enabled: false }
            });
        });
    }

    if (elYear) elYear.addEventListener('change', loadCharts);
    if (elMonth) elMonth.addEventListener('change', loadCharts);

    (async () => {
        almacenes = await fetchAlmacenes();
        if (almacenes.length) renderChecks(almacenes);
        await loadCharts();
    })();
});
