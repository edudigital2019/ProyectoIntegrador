// wwwroot/js/sales-general.js
(function () {
    function init() {
        // ---- refs UI ----
        const $year = document.getElementById('year');
        const $cat = document.getElementById('categoria');
        const $mp = document.getElementById('metodoPago');
        const $btn = document.getElementById('btnAplicar');
        const $btnClear = document.getElementById('btnLimpiar');

        // Dropdown de almacenes (checkboxes)
        const $almBtn = document.getElementById('almacenDropdownBtn');
        const $almFilter = document.getElementById('almacenFilter');
        const $almAll = document.getElementById('almacenAll');
        const $almChecks = document.getElementById('almacenChecks');
        const $almSummary = document.getElementById('almacenSummary');

        // ---- utils ----
        const debounce = (fn, delay = 250) => {
            let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); };
        };

        const q = (base, params) => {
            const parts = [];
            for (const [k, v] of Object.entries(params || {})) {
                if (v == null) continue;
                if (Array.isArray(v)) {
                    v.filter(x => String(x).trim() !== '')
                        .forEach(val => parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(val)}`));
                } else {
                    const s = String(v).trim();
                    if (s !== '') parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(s)}`);
                }
            }
            return parts.length ? `${base}?${parts.join('&')}` : base;
        };

        const fmtMoney = (n) => Number(n || 0).toLocaleString('es-EC', { style: 'currency', currency: 'USD' });
        const fmtInt = (n) => Number(n || 0).toLocaleString('es-EC');

        const getSelectedAlmacenes = () =>
            Array.from(document.querySelectorAll('.chk-alm:checked')).map(cb => cb.value);

        function renderAlmacenSummary() {
            if (!$almBtn || !$almSummary) return;
            const ids = getSelectedAlmacenes();
            let txt = '(todos)';
            if (ids.length === 1) txt = document.querySelector(`label[for="chk-alm-${ids[0]}"]`)?.textContent || `Almacén ${ids[0]}`;
            else if (ids.length > 1) txt = `${ids.length} seleccionados`;
            $almBtn.textContent = txt;
            $almSummary.textContent = txt;
        }

        // ---- cargar listas ----
        async function cargarAlmacenes({ preserve = true } = {}) {
            if (!$almChecks) return;
            const prev = preserve ? new Set(getSelectedAlmacenes()) : new Set();

            const url = q('/api/charts/almacenes-nombres', {
                year: $year?.value,
                categoria: $cat?.value,
                _t: Date.now()
            });

            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // [{id,label}]

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
                await renderAll();
            }, 250);

            document.querySelectorAll('.chk-alm').forEach(cb => {
                cb.addEventListener('change', () => {
                    renderAlmacenSummary();
                    refreshDebounced();
                });
            });

            renderAlmacenSummary();
        }

        async function cargarMetodosPago({ preserve = true } = {}) {
            if (!$mp) return;
            const prev = preserve ? String($mp.value ?? '') : '';

            const sel = getSelectedAlmacenes();
            const almParam = (sel.length >= 2) ? '' : (sel[0] || '');

            const url = q('/api/charts/metodos-pago', {
                year: $year?.value,
                almacenId: almParam,             // si hay 2+, pedimos global (sin almacén)
                categoria: $cat?.value,
                _t: Date.now()
            });

            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // ["Efectivo", ...]
            $mp.innerHTML = `<option value="">(todos)</option>` +
                data.map(x => `<option value="${x}">${x}</option>`).join('');
            if (preserve && data.some(x => String(x) === prev)) $mp.value = prev;
        }

        // Buscar dentro del dropdown
        if ($almFilter) {
            $almFilter.addEventListener('input', () => {
                const term = $almFilter.value.trim().toLowerCase();
                document.querySelectorAll('#almacenChecks .form-check').forEach(row => {
                    const label = row.querySelector('.form-check-label')?.textContent?.toLowerCase() ?? '';
                    row.style.display = label.includes(term) ? '' : 'none';
                });
            });
        }

        // Seleccionar todos
        if ($almAll) {
            const refreshDebouncedAll = debounce(async () => {
                await cargarMetodosPago({ preserve: true });
                await renderAll();
            }, 250);

            $almAll.addEventListener('change', () => {
                const check = $almAll.checked;
                document.querySelectorAll('.chk-alm').forEach(cb => cb.checked = check);
                renderAlmacenSummary();
                refreshDebouncedAll();
            });
        }

        // ---- KPI ----
        async function cargarKpis() {
            const url = q('/api/charts/resumen-ventas', {
                year: $year?.value,
                almacenIds: getSelectedAlmacenes(),
                categoria: $cat?.value,
                metodoPago: $mp?.value,
                _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const d = await res.json();

            const $kN = document.getElementById('kpiNeto');
            const $kT = document.getElementById('kpiTicket');
            const $kU = document.getElementById('kpiUnd');
            const $kY = document.getElementById('kpiYoY');

            if ($kN) $kN.textContent = fmtMoney(d.neto);
            if ($kT) $kT.textContent = fmtMoney(d.ticketPromedio);
            if ($kU) $kU.textContent = fmtInt(d.unidades);

            if ($kY) {
                const yoy = Number(d.varYoY || 0);
                $kY.textContent = (yoy * 100).toFixed(1) + '%';
                $kY.classList.toggle('text-success', yoy >= 0);
                $kY.classList.toggle('text-danger', yoy < 0);
            }
        }

        // ---- Charts ----
        function sumSeriesByMonth(series) {
            if (!Array.isArray(series) || !series.length) return [];
            const len = Math.max(...series.map(s => (s.data || []).length));
            const out = Array.from({ length: len }, (_, i) =>
                series.reduce((acc, s) => acc + (Number(s.data?.[i] || 0)), 0)
            );
            return out;
        }

        async function chartTrend() {
            const container = document.getElementById('chartGeneralTrend');
            if (!container) return;

            const sel = getSelectedAlmacenes();
            const y = Number($year?.value) || new Date().getFullYear();

            // Si hay almacenes seleccionados, usamos el multi y totalizamos para mostrar "Total selección"
            if (sel.length >= 1) {
                const urlMulti = q('/api/charts/ventas-por-mes-multi', {
                    year: y, almacenIds: sel, categoria: $cat?.value, metodoPago: $mp?.value, _t: Date.now()
                });
                const resM = await fetch(urlMulti, { cache: 'no-store' });
                if (!resM.ok) return;
                const dm = await resM.json();

                const cats = dm.categories || dm.Categories || [];
                const seriesSrv = dm.series || dm.Series || [];
                const dataSum = sumSeriesByMonth(seriesSrv);

                Highcharts.chart(container, {
                    chart: { type: 'areaspline' },
                    title: { text: 'Tendencia mensual (selección)' },
                    xAxis: { categories: cats },
                    yAxis: { title: { text: 'USD' } },
                    series: [{ name: 'Total selección', data: dataSum, type: 'areaspline' }]
                });
                return;
            }

            // Si no hay almacenes seleccionados: global (sin almacenId)
            const url = q('/api/charts/ventas-por-mes', {
                year: y, categoria: $cat?.value, metodoPago: $mp?.value, _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const d = await res.json();

            Highcharts.chart(container, {
                chart: { type: 'areaspline' },
                title: { text: 'Tendencia mensual (global)' },
                xAxis: { categories: d.categories || [] },
                yAxis: { title: { text: 'USD' } },
                series: d.series || []
            });
        }

        async function chartMetodos() {
            const container = document.getElementById('chartMetodosPago');
            if (!container) return;

            const url = q('/api/charts/ventas-por-metodo-pago', {
                year: $year?.value,
                almacenIds: getSelectedAlmacenes(),
                categoria: $cat?.value,
                _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // [{name, y}]

            Highcharts.chart(container, {
                chart: { type: 'pie' },
                title: { text: 'Métodos de pago' },
                plotOptions: {
                    pie: { innerSize: '60%', dataLabels: { enabled: true, format: '{point.name}: {point.percentage:.1f}%' } }
                },
                tooltip: {
                    pointFormatter() {
                        return `<span style="color:${this.color}">●</span> ${this.name}: <b>${fmtMoney(this.y)}</b><br/>`;
                    }
                },
                series: [{ name: 'Ventas', data }]
            });
        }

        async function chartTopAlmacen() {
            const container = document.getElementById('chartTopAlmacen');
            if (!container) return;

            const url = q('/api/charts/top-almacenes', {
                year: $year?.value,
                almacenIds: getSelectedAlmacenes(),
                categoria: $cat?.value,
                metodoPago: $mp?.value,
                limit: 10,
                _t: Date.now()
            });
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const data = await res.json(); // [{label, neto, unidades}]

            const cats = data.map(x => x.label);
            const vals = data.map(x => x.neto);

            Highcharts.chart(container, {
                chart: { type: 'bar' },
                title: { text: 'Top almacenes por ventas' },
                xAxis: { categories: cats },
                yAxis: { title: { text: 'USD' } },
                tooltip: {
                    formatter() {
                        const y = this.y ?? 0;
                        return `<b>${this.x}</b><br/>Ventas: <b>${fmtMoney(y)}</b>`;
                    }
                },
                series: [{ name: 'Neto', data: vals }]
            });
        }

        // ---- orquestador ----
        async function renderAll() {
            await cargarKpis();
            await chartTrend();
            await chartMetodos();
            await chartTopAlmacen();
        }

        // ---- eventos ----
        if ($btn) $btn.addEventListener('click', renderAll);

        if ($btnClear) {
            $btnClear.addEventListener('click', async () => {
                if ($year) $year.value = new Date().getFullYear();
                if ($cat) $cat.value = '';
                if ($mp) $mp.value = '';
                if ($almAll) $almAll.checked = false;
                document.querySelectorAll('.chk-alm').forEach(cb => cb.checked = false);
                renderAlmacenSummary();
                await cargarAlmacenes({ preserve: false });
                await cargarMetodosPago({ preserve: false });
                await renderAll();
            });
        }

        if ($year) $year.addEventListener('change', debounce(async () => {
            await cargarAlmacenes({ preserve: true });
            await cargarMetodosPago({ preserve: true });
            await renderAll();
        }, 200));

        if ($cat) $cat.addEventListener('change', debounce(async () => {
            await cargarAlmacenes({ preserve: true });
            await cargarMetodosPago({ preserve: true });
            await renderAll();
        }, 200));

        if ($mp) $mp.addEventListener('change', debounce(renderAll, 150));

        document.addEventListener('visibilitychange', async () => {
            if (!document.hidden) {
                await cargarAlmacenes({ preserve: true });
                await cargarMetodosPago({ preserve: true });
            }
        });

        // ---- init ----
        (async () => {
            await cargarAlmacenes();
            await cargarMetodosPago();
            await renderAll();
        })();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
