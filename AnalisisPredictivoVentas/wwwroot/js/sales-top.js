/* sales-top.js — Top productos con:
   - Resumen “Filtros aplicados” arriba del gráfico
   - Botón Limpiar (reset a año actual + todos los almacenes, mes vacío)
   - Cambio de tipo de gráfico: bar/column/pie (donut)
   - Modo comparativo (≥2 almacenes) -> siempre columnas (agrupadas)
   - Anti-cache (_t) y manejo de “sin datos”
*/

document.addEventListener('DOMContentLoaded', () => {
    const base = (window.API_CHARTS_BASE || '/api/charts').replace(/\/+$/, '');

    // UI
    const elSummary = document.getElementById('almacenSummary');
    const elFilter = document.getElementById('almacenFilter');
    const elAll = document.getElementById('almacenAll');
    const elChecks = document.getElementById('almacenChecks');
    const elTopN = document.getElementById('topN');
    const elMonth = document.getElementById('month');
    const elYear = document.getElementById('year');
    const elApply = document.getElementById('btnAplicar');
    const elClear = document.getElementById('btnLimpiar');
    const elSpin = document.getElementById('chartSpinner');
    const elChartType = document.getElementById('chartType');
    const elFiltersSummary = document.getElementById('filtersSummary');

    const chartDivId = 'chartTopProductos';

    if (!elSummary || !elFilter || !elAll || !elChecks) {
        console.error('IDs del dropdown de almacén no encontrados.');
        return;
    }

    // Si el input de año está vacío al inicio, set al año actual
    if (elYear && (elYear.value ?? '').trim() === '') {
        elYear.value = String(new Date().getFullYear());
    }

    let almacenes = [];            // [{id, nombre}]
    let seleccionados = new Set(); // ids seleccionados

    // Utils
    const currency = (val) => (val ?? 0).toLocaleString('es-EC', { style: 'currency', currency: 'USD' });
    const MES = [, 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

    const getYearRaw = () => (elYear?.value ?? '').trim();
    const getMonthRaw = () => (elMonth?.value ?? '').trim();
    const getTopN = () => {
        const n = parseInt(elTopN?.value ?? '10', 10);
        return Number.isFinite(n) && n >= 1 ? n : 10;
    };

    const buildQuery = (baseUrl, params) => {
        const parts = [];
        Object.entries(params || {}).forEach(([k, v]) => {
            if (v == null) return;
            if (Array.isArray(v)) {
                v.forEach(val => {
                    const s = String(val).trim();
                    if (s !== '') parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(s)}`);
                });
            } else {
                const s = String(v).trim();
                if (s !== '') parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(s)}`);
            }
        });
        return parts.length ? `${baseUrl}?${parts.join('&')}` : baseUrl;
    };

    const info = (container, msg) => { container.innerHTML = `<div class="alert alert-info">${msg}</div>`; };

    function updateFiltersSummary() {
        const year = getYearRaw();
        const month = getMonthRaw();
        const cantAlm = seleccionados.size;
        const partes = [];
        if (year) partes.push(`Año ${year}`);
        partes.push(`Mes ${month ? MES[Number(month)] : 'Todos'}`);
        partes.push(cantAlm === almacenes.length ? 'Almacenes: Todos' :
            cantAlm === 0 ? 'Almacenes: Ninguno' : `Almacenes: ${cantAlm} seleccionado(s)`);
        const tipo = elChartType?.value || 'bar';
        const tipoTxt = tipo === 'bar' ? 'Barras' : tipo === 'column' ? 'Columnas' : 'Donut';
        partes.push(`Gráfico: ${tipoTxt}`);
        if (elFiltersSummary) elFiltersSummary.textContent = `Filtros aplicados: ${partes.join(' · ')}`;
    }

    // Data
    async function fetchAlmacenes() {
        try {
            const url = buildQuery(`${base}/almacenes-nombres`, { _t: Date.now() });
            const r = await fetch(url, { cache: 'no-store' });
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            const data = await r.json();
            return data.map(a => ({ id: a.id, nombre: a.label ?? a.nombre ?? String(a.id) }));
        } catch {
            const url2 = buildQuery(`${base}/almacenes`, { _t: Date.now() });
            const r2 = await fetch(url2, { cache: 'no-store' });
            if (!r2.ok) throw new Error(`HTTP ${r2.status} (fallback almacenes)`);
            const data2 = await r2.json();
            return data2.map(a => ({
                id: a.id ?? a.almacenId ?? a.AlmacenId,
                nombre: a.nombre ?? a.Nombre ?? String(a.id ?? a.almacenId ?? a.AlmacenId)
            }));
        }
    }

    async function fetchTopProductos() {
        const year = getYearRaw();
        const month = getMonthRaw();
        const limit = getTopN();

        const params = { year, limit, _t: Date.now() };
        if (month !== '') params.month = month;

        if (seleccionados.size > 0 && seleccionados.size !== almacenes.length) {
            params.almacenIds = [...seleccionados];
        }

        const url = buildQuery(`${base}/top-productos`, params);
        console.log('[TopProductos] GET', url);
        const r = await fetch(url, { cache: 'no-store' });
        if (!r.ok) throw new Error(`HTTP ${r.status} en top-productos`);
        return r.json(); // array
    }

    async function fetchTopProductosMulti() {
        const year = getYearRaw();
        const month = getMonthRaw();
        const limit = getTopN();
        const sel = [...seleccionados];

        const params = { year, limit, almacenIds: sel, _t: Date.now() };
        if (month !== '') params.month = month;

        const url = buildQuery(`${base}/top-productos-multi`, params);
        console.log('[TopProductosMulti] GET', url);
        const r = await fetch(url, { cache: 'no-store' });
        if (!r.ok) throw new Error(`HTTP ${r.status} en top-productos-multi`);
        return r.json(); // { categories:[], series:[] }
    }

    // Dropdown almacenes
    function renderChecks(list) {
        elChecks.innerHTML = '';
        list.forEach(a => {
            const id = `chk-alm-${a.id}`;
            const wrap = document.createElement('div');
            wrap.className = 'form-check almacen-item';
            wrap.dataset.nombre = (a.nombre || '').toLowerCase();
            wrap.innerHTML = `
        <input class="form-check-input almacen-check" type="checkbox" id="${id}" value="${a.id}" checked>
        <label class="form-check-label" for="${id}">${a.nombre}</label>`;
            elChecks.appendChild(wrap);
        });
        seleccionados = new Set(list.map(a => a.id)); // todos
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
                updateFiltersSummary();
                loadChart();
            });
        });
    }

    function updateSummary() {
        if (seleccionados.size === 0) { elSummary.textContent = 'Ninguno'; return; }
        if (seleccionados.size === almacenes.length) { elSummary.textContent = 'Todos'; return; }
        const nombres = almacenes.filter(a => seleccionados.has(a.id)).map(a => a.nombre);
        elSummary.textContent = nombres.slice(0, 2).join(', ') + (nombres.length > 2 ? ` +${nombres.length - 2}` : '');
    }

    function wireEvents() {
        elAll.addEventListener('change', () => {
            const chks = elChecks.querySelectorAll('.almacen-check');
            seleccionados = new Set();
            chks.forEach(chk => {
                chk.checked = elAll.checked;
                if (elAll.checked) seleccionados.add(Number(chk.value));
            });
            updateSummary();
            updateFiltersSummary();
            loadChart();
        });

        elFilter.addEventListener('input', () => {
            const term = elFilter.value.toLowerCase().trim();
            elChecks.querySelectorAll('.almacen-item').forEach(div => {
                const nombre = div.dataset.nombre || '';
                div.style.display = (!term || nombre.includes(term)) ? '' : 'none';
            });
        });

        if (elTopN) {
            elTopN.addEventListener('change', () => {
                const v = parseInt(elTopN.value, 10);
                if (!Number.isFinite(v) || v < 1) elTopN.value = '10';
                updateFiltersSummary();
                loadChart();
            });
        }

        if (elMonth) elMonth.addEventListener('change', () => { updateFiltersSummary(); loadChart(); });

        if (elYear) {
            elYear.addEventListener('change', () => { updateFiltersSummary(); loadChart(); });
            elYear.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); updateFiltersSummary(); loadChart(); } });
        }

        if (elChartType) {
            elChartType.addEventListener('change', () => {
                // Si hay 2+ almacenes, forzamos columnas (comparativo)
                if (seleccionados.size >= 2 && elChartType.value === 'pie') {
                    // feedback simple
                    elChartType.value = 'column';
                }
                updateFiltersSummary();
                loadChart();
            });
        }

        if (elApply) elApply.addEventListener('click', () => { updateFiltersSummary(); loadChart(); });

        if (elClear) elClear.addEventListener('click', () => resetFilters());
    }

    // Reset filtros: año actual, mes vacío, topN 10, todos los almacenes
    function resetFilters() {
        const yNow = new Date().getFullYear();
        if (elYear) elYear.value = String(yNow);
        if (elMonth) elMonth.value = ''; // “Todos”
        if (elTopN) elTopN.value = '10';
        if (elChartType) elChartType.value = 'bar';
        if (elFilter) elFilter.value = '';

        // seleccionar todos
        elAll.checked = true;
        seleccionados = new Set(almacenes.map(a => a.id));
        elChecks.querySelectorAll('.almacen-check').forEach(chk => chk.checked = true);

        updateSummary();
        updateFiltersSummary();
        loadChart();
    }

    // Chart
    async function loadChart() {
        const container = document.getElementById(chartDivId);
        if (!container) return;

        try {
            if (elSpin) elSpin.style.display = 'block';

            const year = getYearRaw();
            const month = getMonthRaw();
            const tipo = (elChartType?.value || 'bar');

            // Comparativo: si hay 2+ almacenes, siempre columnas agrupadas
            if (seleccionados.size >= 2) {
                const d = await fetchTopProductosMulti();
                const noSeries = !Array.isArray(d.series) || d.series.length === 0;
                const noCats = !Array.isArray(d.categories) || d.categories.length === 0;

                if (noSeries || noCats) { info(container, 'Sin datos para los filtros (año/mes).'); return; }

                const titulo = `Top productos ${year}${month ? ` — Mes ${MES[Number(month)]}` : ''} (comparativo por almacén)`;

                Highcharts.chart(container, {
                    chart: { type: 'column' },
                    title: { text: titulo },
                    xAxis: { categories: d.categories, title: { text: null } },
                    yAxis: { title: { text: 'Ventas (neto)' } },
                    plotOptions: { column: { pointPadding: 0.1, borderWidth: 0, grouping: true } },
                    tooltip: {
                        shared: true,
                        formatter() {
                            const lines = this.points.map(p => `<span style="color:${p.color}">●</span> ${p.series.name}: <b>${currency(p.y)}</b>`);
                            return `<b>${this.x}</b><br/>${lines.join('<br/>')}`;
                        }
                    },
                    series: d.series,
                    credits: { enabled: false }
                });
                return;
            }

            // Consolidado (1 almacén o “Todos”)
            const data = await fetchTopProductos();
            if (!Array.isArray(data) || data.length === 0) { info(container, 'Sin datos para los filtros (año/mes).'); return; }

            const categorias = data.map(d => d.label ?? d.nombre ?? d.producto ?? 'N/D');
            const valores = data.map(d =>
                (typeof d.neto === 'number') ? d.neto
                    : (typeof d.total === 'number') ? d.total
                        : (typeof d.Total === 'number') ? d.Total
                            : 0
            );

            const titulo = `Top productos ${year}${month ? ` — Mes ${MES[Number(month)]}` : ''}`;

            if (tipo === 'pie') {
                const seriePie = categorias.map((name, i) => ({ name, y: valores[i] ?? 0 }));
                Highcharts.chart(container, {
                    chart: { type: 'pie' },
                    title: { text: titulo },
                    plotOptions: { pie: { innerSize: '60%', dataLabels: { enabled: true, format: '{point.name}: {point.percentage:.1f}%' } } },
                    tooltip: { pointFormatter() { return `<span style="color:${this.color}">●</span> ${this.name}: <b>${currency(this.y)}</b>`; } },
                    series: [{ name: 'Ventas', data: seriePie }],
                    credits: { enabled: false }
                });
            } else {
                Highcharts.chart(container, {
                    chart: { type: tipo === 'column' ? 'column' : 'bar' },
                    title: { text: titulo },
                    xAxis: { categories: categorias, title: { text: null } },
                    yAxis: { title: { text: 'Ventas (neto)' } },
                    tooltip: {
                        useHTML: true,
                        formatter: function () {
                            const item = data[this.point.index] || {};
                            const u = item.unidades ?? item.Unidades ?? 0;
                            return `<b>${this.key}</b><br/>Neto: ${currency(this.y)}<br/>Unidades: ${u}`;
                        }
                    },
                    series: [{ name: 'Ventas', data: valores }],
                    credits: { enabled: false }
                });
            }

        } catch (err) {
            console.error('Error loadChart:', err);
            container.innerHTML = `<div class="alert alert-danger">No se pudo cargar Top productos.</div>`;
        } finally {
            if (elSpin) elSpin.style.display = 'none';
        }
    }

    // Init
    (async function init() {
        try {
            almacenes = await fetchAlmacenes();
            if (!almacenes.length) throw new Error('Lista de almacenes vacía');
            renderChecks(almacenes);
            wireEvents();
            updateFiltersSummary();
            await loadChart();
        } catch (err) {
            console.error('Init Top Productos error:', err);
            elChecks.innerHTML = `<div class="text-danger">No se pudieron cargar los almacenes.</div>`;
            const container = document.getElementById(chartDivId);
            if (container) container.innerHTML = `<div class="alert alert-danger">Error inicializando el gráfico.</div>`;
        }
    })();
});
