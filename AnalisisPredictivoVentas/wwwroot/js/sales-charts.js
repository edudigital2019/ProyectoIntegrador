// wwwroot/js/sales-charts.js
(function () {
    function init() {
        // ---------- referencias de UI ----------
        const $year = document.getElementById('year');
        const $cat = document.getElementById('categoria');
        const $mp = document.getElementById('metodoPago');
        const $btn = document.getElementById('btnAplicar');
        const $btnClear = document.getElementById('btnLimpiar');

        // Dropdown con checkboxes (si existe)
        const $almBtn = document.getElementById('almacenDropdownBtn');
        const $almFilter = document.getElementById('almacenFilter');
        const $almAll = document.getElementById('almacenAll');
        const $almChecks = document.getElementById('almacenChecks');
        const $almSummary = document.getElementById('almacenSummary');

        // Fallback: <select multiple id="almacen"> (si no hay dropdown con checks)
        const $almSelect = document.getElementById('almacen');
        const hasCheckboxUI = !!$almChecks; // true si tenemos el dropdown con checks

        // ---------- utils ----------
        const debounce = (fn, delay = 250) => {
            let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
        };

        function buildQuery(base, params) {
            const parts = [];
            for (const [k, v] of Object.entries(params)) {
                if (v === undefined || v === null) continue;
                if (Array.isArray(v)) {
                    v.filter(x => String(x).trim() !== '')
                        .forEach(val => parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(val)}`));
                } else {
                    const s = String(v).trim();
                    if (s !== '') parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(s)}`);
                }
            }
            return parts.length ? `${base}?${parts.join('&')}` : base;
        }

        function getSelectedAlmacenes() {
            if (hasCheckboxUI) {
                return Array.from(document.querySelectorAll('.chk-alm:checked')).map(cb => cb.value);
            }
            if ($almSelect) {
                return Array.from($almSelect.selectedOptions).map(o => o.value).filter(v => v !== '');
            }
            return [];
        }

        function renderAlmacenSummary() {
            if (!$almBtn || !$almSummary) return;
            const ids = getSelectedAlmacenes();
            let txt = '(todos)';
            if (ids.length === 1) txt = `Almacén ${ids[0]}`;
            else if (ids.length > 1) txt = `${ids.length} seleccionados`;
            $almBtn.textContent = txt;
            $almSummary.textContent = txt;
        }

        // ---------- carga de listas ----------
        async function cargarAlmacenes({ preserve = true } = {}) {
            const prev = preserve ? new Set(getSelectedAlmacenes()) : new Set();

            const url = buildQuery('/api/charts/almacenes', {
                year: $year?.value,
                categoria: $cat?.value,
                _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // [{id,label}]

            if (hasCheckboxUI && $almChecks) {
                $almChecks.innerHTML = data.map(x => `
          <div class="form-check">
            <input class="form-check-input chk-alm" type="checkbox" value="${x.id}" id="chk-alm-${x.id}">
            <label class="form-check-label" for="chk-alm-${x.id}">${x.label}</label>
          </div>
        `).join('');

                if (prev.size) {
                    document.querySelectorAll('.chk-alm').forEach(cb => { if (prev.has(cb.value)) cb.checked = true; });
                }

                const refreshDebounced = debounce(async () => {
                    await cargarMetodosPago({ preserve: true });
                    await renderVentasPorMes();
                    await renderHeatmap();
                }, 250);

                document.querySelectorAll('.chk-alm').forEach(cb => {
                    cb.addEventListener('change', () => {
                        renderAlmacenSummary();
                        refreshDebounced();
                    });
                });

                renderAlmacenSummary();
            } else if ($almSelect) {
                $almSelect.innerHTML = `<option value="">(todos)</option>` +
                    data.map(x => `<option value="${x.id}">${x.label}</option>`).join('');
                if (prev.size) Array.from($almSelect.options).forEach(opt => { if (prev.has(opt.value)) opt.selected = true; });
                $almSelect.onchange = debounce(async () => {
                    await cargarMetodosPago({ preserve: true });
                    await renderVentasPorMes();
                    await renderHeatmap();
                }, 250);
            }
        }

        async function cargarMetodosPago({ preserve = true } = {}) {
            const prev = preserve ? String($mp?.value ?? '') : '';
            const sel = getSelectedAlmacenes();
            const almParam = (sel.length >= 2) ? '' : (sel[0] || '');

            const url = buildQuery('/api/charts/metodos-pago', {
                year: $year?.value,
                almacenId: almParam,
                categoria: $cat?.value,
                _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // ["Efectivo", ...]
            if (!$mp) return;
            $mp.innerHTML = `<option value="">(todos)</option>` +
                data.map(x => `<option value="${x}">${x}</option>`).join('');
            if (preserve && data.some(x => String(x) === prev)) $mp.value = prev;
        }

        // buscar en el dropdown
        if ($almFilter) {
            $almFilter.addEventListener('input', () => {
                const term = $almFilter.value.trim().toLowerCase();
                document.querySelectorAll('#almacenChecks .form-check').forEach(row => {
                    const label = row.querySelector('.form-check-label')?.textContent?.toLowerCase() ?? '';
                    row.style.display = label.includes(term) ? '' : 'none';
                });
            });
        }

        // seleccionar todos
        if ($almAll) {
            const refreshDebouncedAll = debounce(async () => {
                await cargarMetodosPago({ preserve: true });
                await renderVentasPorMes();
                await renderHeatmap();
            }, 250);

            $almAll.addEventListener('change', () => {
                const check = $almAll.checked;
                document.querySelectorAll('.chk-alm').forEach(cb => cb.checked = check);
                renderAlmacenSummary();
                refreshDebouncedAll();
            });
        }

        // ---------- helpers de datos ----------
        function mediaMovil3(arr) {
            const r = [];
            for (let i = 0; i < arr.length; i++) {
                const seg = arr.slice(Math.max(0, i - 2), i + 1);
                const prom = seg.reduce((a, b) => a + (b || 0), 0) / seg.length;
                r.push(Number(prom.toFixed(2)));
            }
            return r;
        }

        let chartVentas = null;
        let chartHeat = null;

        // ---------- render gráfico principal ----------
        async function renderVentasPorMes() {
            const container = document.getElementById('chartVentasMes');
            if (!container) { console.warn('#chartVentasMes no existe'); return; }

            const seleccion = getSelectedAlmacenes();
            const y = Number($year?.value) || new Date().getFullYear();

            // MODO COMPARATIVO (2+ almacenes)
            if (seleccion.length >= 2) {
                const url = buildQuery('/api/charts/ventas-por-mes-multi', {
                    year: y,
                    almacenIds: seleccion,
                    categoria: $cat?.value,
                    metodoPago: $mp?.value,
                    _t: Date.now()
                });

                const res = await fetch(url, { cache: 'no-store' });
                if (!res.ok) { console.error('HTTP', res.status, await res.text()); return; }
                const d = await res.json();
                const categories = d.categories || d.Categories || [];
                const seriesSrv = d.series || d.Series || [];
                const series = seriesSrv.map((s, i) => ({
                    name: s.name || s.Name || `Serie ${i + 1}`,
                    type: 'spline',
                    data: s.data || s.Data || [],
                    marker: { radius: 3 },
                    lineWidth: 2
                }));

                if (!chartVentas) {
                    chartVentas = Highcharts.chart(container, {
                        chart: { zoomType: 'x' },
                        title: { text: 'Ventas por mes (comparativa por almacén)' },
                        subtitle: { text: `Año ${y}` },
                        xAxis: { categories },
                        yAxis: { title: { text: 'USD' } },
                        tooltip: {
                            shared: true,
                            formatter() {
                                const lines = this.points.map(p => {
                                    const val = (p.y ?? 0).toLocaleString('es-EC', { style: 'currency', currency: 'USD' });
                                    return `<span style="color:${p.color}">●</span> ${p.series.name}: <b>${val}</b>`;
                                });
                                return `<b>${this.x}</b><br/>${lines.join('<br/>')}`;
                            }
                        },
                        series
                    });
                } else {
                    chartVentas.xAxis[0].setCategories(categories, false);
                    while (chartVentas.series.length) chartVentas.series[0].remove(false);
                    series.forEach(s => chartVentas.addSeries(s, false));
                    chartVentas.yAxis[0].removePlotLine('avgLine');
                    chartVentas.redraw();
                }
                return;
            }

            // MODO 1 ALMACÉN (área + MM3 + año anterior + promedio)
            const common = {
                almacenId: seleccion[0] || '',
                categoria: $cat?.value,
                metodoPago: $mp?.value
            };

            const urlActual = buildQuery('/api/charts/ventas-por-mes', { year: y, ...common, _t: Date.now() });
            const res = await fetch(urlActual, { cache: 'no-store' });
            if (!res.ok) { console.error('HTTP', res.status, await res.text()); return; }
            const d = await res.json();
            const categories = d.categories || d.Categories || [];
            const seriesSrv = d.series || d.Series || [];
            const currData = seriesSrv?.[0]?.data ?? [];

            // Año anterior
            let prevData = [];
            try {
                const urlPrev = buildQuery('/api/charts/ventas-por-mes', { year: y - 1, ...common, _t: Date.now() });
                const resPrev = await fetch(urlPrev, { cache: 'no-store' });
                if (resPrev.ok) {
                    const dPrev = await resPrev.json();
                    const sPrev = dPrev.series || dPrev.Series || [];
                    prevData = sPrev?.[0]?.data ?? [];
                }
            } catch { /* noop */ }

            const avg = currData.length ? (currData.reduce((a, b) => a + (b || 0), 0) / currData.length) : 0;
            const mm3 = mediaMovil3(currData);

            const serieActual = { name: `Ventas ${y}`, type: 'areaspline', data: currData };
            const serieMM3 = { name: 'Media móvil (3m)', type: 'spline', data: mm3 };
            const seriePrev = { name: `Ventas ${y - 1}`, type: 'spline', data: prevData, dashStyle: 'ShortDash' };

            if (!chartVentas) {
                chartVentas = Highcharts.chart(container, {
                    chart: { zoomType: 'x' },
                    title: { text: 'Ventas por mes' },
                    subtitle: { text: 'Neto (USD)' },
                    xAxis: { categories },
                    yAxis: {
                        title: { text: 'USD' },
                        plotLines: [{
                            id: 'avgLine', color: '#20c997', width: 2, value: avg, dashStyle: 'ShortDash',
                            label: {
                                text: `Promedio: ${avg.toLocaleString('es-EC', { style: 'currency', currency: 'USD' })}`,
                                align: 'right', x: -10, style: { color: '#20c997' }
                            }
                        }]
                    },
                    tooltip: {
                        shared: true,
                        formatter() {
                            const v = this.points[0]?.y ?? 0;
                            const val = v.toLocaleString('es-EC', { style: 'currency', currency: 'USD', minimumFractionDigits: 2 });
                            return `<b>${this.x}</b><br/>Ventas: <b>${val}</b>`;
                        }
                    },
                    series: [serieActual, serieMM3, seriePrev]
                });
            } else {
                chartVentas.xAxis[0].setCategories(categories, false);
                while (chartVentas.series.length) chartVentas.series[0].remove(false);
                [serieActual, serieMM3, seriePrev].forEach(s => chartVentas.addSeries(s, false));

                const yAxis = chartVentas.yAxis[0];
                const existing = (yAxis.plotLinesAndBands || []).find(pl => pl.id === 'avgLine');
                if (existing) existing.destroy();
                yAxis.addPlotLine({
                    id: 'avgLine', color: '#20c997', width: 2, value: avg, dashStyle: 'ShortDash',
                    label: {
                        text: `Promedio: ${avg.toLocaleString('es-EC', { style: 'currency', currency: 'USD' })}`,
                        align: 'right', x: -10, style: { color: '#20c997' }
                    }
                });

                chartVentas.redraw();
            }
        }

        // ---------- heatmap opcional ----------
        async function renderHeatmap() {
            const container = document.getElementById('chartHeatmap');
            if (!container) return; // si no existe, no hacemos nada

            const sel = getSelectedAlmacenes();
            const y = Number($year?.value) || new Date().getFullYear();

            const url = buildQuery('/api/charts/ventas-heatmap', {
                year: y,
                almacenIds: sel,
                categoria: $cat?.value,
                metodoPago: $mp?.value,
                _t: Date.now()
            });

            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) { console.error('HTTP', res.status, await res.text()); return; }
            const d = await res.json();

            const colorStops = [
                [0, '#f1f5f9'],
                [0.5, '#60a5fa'],
                [1, '#1d4ed8']
            ];

            chartHeat = Highcharts.chart(container, {
                chart: { type: 'heatmap' },
                title: { text: null },
                xAxis: { categories: d.xCategories || [] },
                yAxis: { categories: d.yCategories || [], title: null },
                colorAxis: { min: 0, max: d.max || null, stops: colorStops },
                legend: { align: 'right', layout: 'vertical', verticalAlign: 'top', y: 25, symbolHeight: 180 },
                tooltip: {
                    formatter() {
                        const val = (this.point.value || 0).toLocaleString('es-EC', { style: 'currency', currency: 'USD' });
                        return `<b>${this.series.yAxis.categories[this.point.y]}</b><br/>
                    ${this.series.xAxis.categories[this.point.x]}: <b>${val}</b>`;
                    }
                },
                series: [{
                    name: 'Ventas netas',
                    borderWidth: 1,
                    data: d.data || [],
                    dataLabels: {
                        enabled: true,
                        formatter() {
                            const v = this.point.value || 0;
                            if (v >= 1000000) return (v / 1000000).toFixed(1) + 'M';
                            if (v >= 1000) return (v / 1000).toFixed(0) + 'k';
                            return Math.round(v);
                        },
                        style: { fontSize: '11px', fontWeight: '600', textOutline: 'none', color: '#0f172a' }
                    }
                }]
            });
        }

        // ---------- eventos ----------
        if ($btn) {
            $btn.addEventListener('click', async () => {
                await cargarAlmacenes({ preserve: true });
                await cargarMetodosPago({ preserve: true });
                await renderVentasPorMes();
                await renderHeatmap();
            });
        }

        if ($btnClear) {
            $btnClear.addEventListener('click', async () => {
                if ($year) $year.value = new Date().getFullYear();
                if ($cat) $cat.value = '';
                if ($mp) $mp.value = '';
                if (hasCheckboxUI) {
                    if ($almAll) $almAll.checked = false;
                    document.querySelectorAll('.chk-alm').forEach(cb => cb.checked = false);
                    renderAlmacenSummary();
                } else if ($almSelect) {
                    Array.from($almSelect.options).forEach(o => o.selected = false);
                }
                await cargarAlmacenes({ preserve: false });
                await cargarMetodosPago({ preserve: false });
                await renderVentasPorMes();
                await renderHeatmap();
            });
        }

        if ($year) $year.addEventListener('change', debounce(async () => { await cargarAlmacenes({ preserve: true }); await cargarMetodosPago({ preserve: true }); await renderVentasPorMes(); await renderHeatmap(); }, 150));
        if ($cat) $cat.addEventListener('change', debounce(async () => { await cargarAlmacenes({ preserve: true }); await cargarMetodosPago({ preserve: true }); await renderVentasPorMes(); await renderHeatmap(); }, 150));
        if ($almSelect && !hasCheckboxUI) $almSelect.addEventListener('change', debounce(async () => { await cargarMetodosPago({ preserve: true }); await renderVentasPorMes(); await renderHeatmap(); }, 150));

        document.addEventListener('visibilitychange', async () => {
            if (!document.hidden) {
                await cargarAlmacenes({ preserve: true });
                await cargarMetodosPago({ preserve: true });
            }
        });

        // ---------- init ----------
        (async () => {
            await cargarAlmacenes();
            await cargarMetodosPago();
            await renderVentasPorMes();
            await renderHeatmap(); // si no existe #chartHeatmap, no hace nada
        })();
    }

    // Ejecutar tras DOM listo (evita Highcharts #13 si scripts no tienen defer)
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
