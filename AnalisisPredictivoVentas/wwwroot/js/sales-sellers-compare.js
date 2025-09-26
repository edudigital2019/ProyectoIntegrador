// wwwroot/js/sales-sellers-compare.js
(function () {
    const base = (window.API_CHARTS_BASE || '/api/charts').replace(/\/+$/, '');
    let lastCompare = [];

    const el = {
        yearA: document.getElementById('yearA'),
        mFromA: document.getElementById('mFromA'),
        mToA: document.getElementById('mToA'),
        yearB: document.getElementById('yearB'),
        mFromB: document.getElementById('mFromB'),
        mToB: document.getElementById('mToB'),
        metric: document.getElementById('metric'),
        topN: document.getElementById('topN'),
        btn: document.getElementById('btnAplicar'),
        btnYoY: document.getElementById('btnYoY'),
        btnExport: document.getElementById('btnExport'),
        sum: document.getElementById('almacenSummary'),
        filter: document.getElementById('almacenFilter'),
        all: document.getElementById('almacenAll'),
        checks: document.getElementById('almacenChecks'),
        tblBody: document.querySelector('#tblCompare tbody'),
        kpiTopNombre: document.getElementById('kpiTopNombre'),
        kpiTopValor: document.getElementById('kpiTopValor'),
        kpiTopDelta: document.getElementById('kpiTopDelta'),
        mdlTitle: document.getElementById('mdlTitle'),
    };

    // ---- Helpers ----
    function fmtMoney(v) {
        return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 })
            .format(Number.isFinite(+v) ? +v : 0);
    }
    function fmtInt(v) { return new Intl.NumberFormat().format(Number.isFinite(+v) ? +v : 0); }
    function fmtPct(v) { const n = Number.isFinite(+v) ? +v : 0; return `${n.toFixed(2)}%`; }

    // Nombre robusto en cualquier payload (lower/Upper/label, con fallback por id)
    function getName(o) {
        const id = (o && (o.id ?? o.Id)) ?? null;
        const name = o?.nombre ?? o?.Nombre ?? o?.name ?? o?.Name ?? o?.label ?? o?.Label;
        const text = (name ?? (id != null ? `Empleado ${id}` : 'Empleado'));
        return String(text).trim();
    }

    // <<< NUEVO: consolidar por nombre para evitar duplicados por almacén
    // Reemplaza la función existente por esta:
    function consolidateByName(data) {
        if (!Array.isArray(data)) return [];
        const map = new Map();

        for (const r of data) {
            // nombre seguro
            const nombre = getName(r) || 'Empleado';
            const key = String(nombre).trim().toUpperCase();

            // coerciones SIEMPRE numéricas (evita NaN)
            const nA = Number(r?.netoA ?? r?.NetoA ?? 0) || 0;
            const nB = Number(r?.netoB ?? r?.NetoB ?? 0) || 0;
            const uA = Number(r?.undA ?? r?.UndA ?? 0) || 0;
            const uB = Number(r?.undB ?? r?.UndB ?? 0) || 0;
            const tA = Number(r?.ticketsA ?? r?.TicketsA ?? 0) || 0;
            const tB = Number(r?.ticketsB ?? r?.TicketsB ?? 0) || 0;

            const cur = map.get(key) || { nombre, netoA: 0, netoB: 0, undA: 0, undB: 0, ticketsA: 0, ticketsB: 0 };
            cur.netoA += nA; cur.netoB += nB;
            cur.undA += uA; cur.undB += uB;
            cur.ticketsA += tA; cur.ticketsB += tB;
            map.set(key, cur);
        }

        const out = [...map.values()].map(o => {
            const dN = o.netoB - o.netoA;
            const pN = o.netoA === 0 ? (o.netoB > 0 ? 100 : 0) : (dN / o.netoA * 100);
            const dU = o.undB - o.undA;
            const pU = o.undA === 0 ? (o.undB > 0 ? 100 : 0) : (dU / o.undA * 100);
            return {
                nombre: String(o.nombre).trim() || 'Empleado',
                netoA: +o.netoA, netoB: +o.netoB,
                deltaNeto: +dN, pctNeto: +pN,
                undA: +o.undA, undB: +o.undB,
                deltaUnd: +dU, pctUnd: +pU,
                ticketsA: +o.ticketsA, ticketsB: +o.ticketsB
            };
        });

        return out;
    }


    async function fetchAlmacenes() {
        try {
            let res = await fetch(`${base}/almacenes-nombres`);
            if (!res.ok) res = await fetch(`${base}/almacenes`);
            if (!res.ok) throw new Error('No se pudo cargar almacenes');

            const data = await res.json();
            const items = (data || []).map(x => ({
                id: x.id ?? x.Id ?? x.almacenId ?? x.AlmacenId,
                label: x.label ?? x.Label ?? x.nombre ?? x.Nombre
            })).filter(x => x.id != null);

            el.checks.innerHTML = items.map(it => `
              <div class="form-check">
                <input class="form-check-input almacen-check" type="checkbox" value="${it.id}" id="alm_${it.id}">
                <label class="form-check-label" for="alm_${it.id}">${it.label}</label>
              </div>`).join('');

            el.all.addEventListener('change', () => {
                document.querySelectorAll('.almacen-check').forEach(c => c.checked = el.all.checked);
                updateSummary();
            });
            el.filter.addEventListener('input', () => {
                const q = (el.filter.value || '').toLowerCase();
                el.checks.querySelectorAll('.form-check').forEach(div => {
                    div.style.display = div.textContent.toLowerCase().includes(q) ? '' : 'none';
                });
            });
            el.checks.addEventListener('change', updateSummary);
        } catch (err) {
            console.error(err);
            el.checks.innerHTML = '<div class="text-muted small">No se pudieron cargar los almacenes.</div>';
        }
    }

    function getAlmacenIds() {
        const ids = [];
        document.querySelectorAll('.almacen-check').forEach(c => { if (c.checked) ids.push(parseInt(c.value)); });
        return ids;
    }
    function updateSummary() {
        const ids = getAlmacenIds();
        el.sum.textContent = ids.length ? `${ids.length} seleccionado(s)` : 'Ninguno';
    }

    // ---- QS ----
    function qsCompare() {
        const u = new URLSearchParams();
        u.set('yearA', el.yearA.value);
        u.set('monthFromA', el.mFromA.value);
        u.set('monthToA', el.mToA.value);
        u.set('yearB', el.yearB.value);
        u.set('monthFromB', el.mFromB.value);
        u.set('monthToB', el.mToB.value);
        u.set('metric', el.metric.value);
        u.set('topN', el.topN.value);
        getAlmacenIds().forEach(id => u.append('almacenIds', id));
        return u.toString();
    }
    function qsTrend() {
        const u = new URLSearchParams();
        u.set('year', el.yearA.value);
        u.set('monthFrom', el.mFromA.value);
        u.set('monthTo', el.mToA.value);
        u.set('metric', el.metric.value);
        u.set('topN', el.topN.value);
        getAlmacenIds().forEach(id => u.append('almacenIds', id));
        return u.toString();
    }
    function qsDetalle(empleadoId) {
        const u = new URLSearchParams();
        u.set('year', el.yearA.value);
        u.set('monthFrom', el.mFromA.value);
        u.set('monthTo', el.mToA.value);
        u.set('empleadoId', empleadoId);
        getAlmacenIds().forEach(id => u.append('almacenIds', id));
        return u.toString();
    }

    // ---- Charts ----
    function renderCompare(data) {
        const arr = Array.isArray(data) ? data : [];

        // Si no hay datos, muestra un gráfico vacío con aviso leve
        if (arr.length === 0) {
            if (document.getElementById('chartCompare')) {
                Highcharts.chart('chartCompare', {
                    title: { text: 'Top empleados — sin datos para los filtros' },
                    series: [],
                    credits: { enabled: false }
                });
            }
            return;
        }

        const cats = arr.map(getName).map(n => (n && n.length ? n : 'Empleado'));
        const netoA = arr.map(x => Number(x?.netoA ?? x?.NetoA ?? 0) || 0);
        const netoB = arr.map(x => Number(x?.netoB ?? x?.NetoB ?? 0) || 0);
        const undA = arr.map(x => Number(x?.undA ?? x?.UndA ?? 0) || 0);
        const undB = arr.map(x => Number(x?.undB ?? x?.UndB ?? 0) || 0);

        const serieA = el.metric.value === 'unidades' ? undA : netoA;
        const serieB = el.metric.value === 'unidades' ? undB : netoB;

        // Contenedor existente
        const container = document.getElementById('chartCompare');
        if (!container) { console.error('Falta el div #chartCompare'); return; }

        Highcharts.chart('chartCompare', {
            chart: { type: 'column' },
            title: { text: 'Top empleados — Periodo A vs Periodo B' },
            xAxis: { categories: cats, crosshair: true },
            yAxis: { min: 0, title: { text: el.metric.value === 'unidades' ? 'Unidades' : 'Monto neto' } },
            tooltip: { shared: true },
            plotOptions: { column: { pointPadding: 0.1, groupPadding: 0.15 } },
            series: [
                { name: 'Periodo A', data: serieA },
                { name: 'Periodo B', data: serieB }
            ],
            credits: { enabled: false },
            exporting: { enabled: true }
        });
    }


    function renderTrend(series) {
        const meses = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
        const from = parseInt(el.mFromA.value);
        const to = parseInt(el.mToA.value);
        const iFrom = Number.isFinite(from) && from >= 1 && from <= 12 ? from - 1 : 0;
        const iTo = Number.isFinite(to) && to >= 1 && to <= 12 ? to : 12;
        const cats = meses.slice(iFrom, iTo);

        const hcSeries = (series || []).map(s => ({
            name: getName(s),
            data: (s.serie ?? s.Serie ?? []).map(p =>
                el.metric.value === 'unidades'
                    ? +(p.unidades ?? p.Unidades ?? 0)
                    : +(p.neto ?? p.Neto ?? 0)
            )
        }));

        Highcharts.chart('chartTrend', {
            chart: { type: 'line' },
            title: { text: 'Tendencia mensual — Top-N (Periodo A)' },
            xAxis: { categories: cats },
            yAxis: { title: { text: el.metric.value === 'unidades' ? 'Unidades' : 'Monto neto' } },
            tooltip: { shared: true },
            series: hcSeries,
            credits: { enabled: false },
            exporting: { enabled: true }
        });
    }

    function renderDetalle(nombre, items) {
        el.mdlTitle.textContent = `Detalle por almacén — ${nombre}`;
        const cats = (items || []).map(x => x.almacen ?? x.Almacen);
        const data = (items || []).map(x => +(x.neto ?? x.Neto ?? 0));
        Highcharts.chart('chartDetalleAlmacen', {
            chart: { type: 'column' },
            title: { text: null },
            xAxis: { categories: cats },
            yAxis: { title: { text: 'Monto neto' } },
            series: [{ name: 'Neto', data }],
            credits: { enabled: false },
            exporting: { enabled: true }
        });
        const modal = new bootstrap.Modal(document.getElementById('mdlDetalle'));
        modal.show();
    }

    // ---- KPIs + Tabla ----
    function renderKPIs(data) {
        if (!data || !data.length) {
            el.kpiTopNombre.textContent = '—';
            el.kpiTopValor.textContent = '—';
            el.kpiTopDelta.textContent = '—';
            return;
        }
        const top = data[0];
        const nombre = getName(top);
        const nA = +(top.netoA ?? top.NetoA ?? 0);
        const nB = +(top.netoB ?? top.NetoB ?? 0);
        const pct = +(top.pctNeto ?? top.PctNeto ?? 0);
        el.kpiTopNombre.textContent = nombre;
        el.kpiTopValor.textContent = `${fmtMoney(nA)} (A) vs ${fmtMoney(nB)} (B)`;
        el.kpiTopDelta.textContent = `Δ ${fmtMoney(nB - nA)} | ${fmtPct(pct)}`;
    }

    function renderTable(data) {
        el.tblBody.innerHTML = (data || []).map(r => {
            const id = r.id ?? r.Id;           // puede venir vacío si agregas "Sin vendedor"
            const hasId = Number.isFinite(id); // solo habilita detalle si hay Id real
            const nombre = getName(r);

            const nA = +(r.netoA ?? r.NetoA ?? 0), nB = +(r.netoB ?? r.NetoB ?? 0);
            const dN = +(r.deltaNeto ?? r.DeltaNeto ?? (nB - nA));
            const pN = +(r.pctNeto ?? r.PctNeto ?? 0);

            const uA = +(r.undA ?? r.UndA ?? 0), uB = +(r.undB ?? r.UndB ?? 0);
            const dU = +(r.deltaUnd ?? r.DeltaUnd ?? (uB - uA));
            const pU = +(r.pctUnd ?? r.PctUnd ?? 0);

            const btn = hasId
                ? `<button class="btn btn-sm btn-outline-primary btn-detalle">Por almacén</button>`
                : `<button class="btn btn-sm btn-outline-secondary" disabled title="No disponible">Por almacén</button>`;

            return `<tr data-id="${hasId ? id : ''}" data-nombre="${nombre}">
                <td>${nombre}</td>
                <td class="text-end">${fmtMoney(nA)}</td>
                <td class="text-end">${fmtMoney(nB)}</td>
                <td class="text-end">${fmtMoney(dN)}</td>
                <td class="text-end">${fmtPct(pN)}</td>
                <td class="text-end">${fmtInt(uA)}</td>
                <td class="text-end">${fmtInt(uB)}</td>
                <td class="text-end">${fmtInt(dU)}</td>
                <td class="text-end">${fmtPct(pU)}</td>
                <td class="text-end">${fmtInt(r.ticketsA ?? r.TicketsA ?? 0)}</td>
                <td class="text-end">${fmtInt(r.ticketsB ?? r.TicketsB ?? 0)}</td>
                <td class="text-end">${btn}</td>
            </tr>`;
        }).join('');
    }

    // CSV con comillas para campos y escape de comillas internas
    function csvCell(v) {
        const s = String(v ?? '');
        return `"${s.replace(/"/g, '""')}"`;
    }
    function exportCsv() {
        if (!lastCompare || !lastCompare.length) return;
        const headers = ['Empleado', 'NetoA', 'NetoB', 'DeltaNeto', 'PctNeto', 'UndA', 'UndB', 'DeltaUnd', 'PctUnd', 'TicketsA', 'TicketsB'];
        const rows = lastCompare.map(r => [
            getName(r),
            (r.netoA ?? r.NetoA ?? 0),
            (r.netoB ?? r.NetoB ?? 0),
            (r.deltaNeto ?? r.DeltaNeto ?? 0),
            (r.pctNeto ?? r.PctNeto ?? 0),
            (r.undA ?? r.UndA ?? 0),
            (r.undB ?? r.UndB ?? 0),
            (r.deltaUnd ?? r.DeltaUnd ?? 0),
            (r.pctUnd ?? r.PctUnd ?? 0),
            (r.ticketsA ?? r.TicketsA ?? 0),
            (r.ticketsB ?? r.TicketsB ?? 0)
        ]);
        const csv = [headers.map(csvCell).join(','), ...rows.map(r => r.map(csvCell).join(','))].join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = `vendedores-compare-${el.yearA.value}-${el.yearB.value}.csv`;
        document.body.appendChild(a); a.click(); a.remove();
    }

    // ---- Carga ----
    async function cargar() {
        try {
            const [cmpRes, trRes] = await Promise.all([
                fetch(`${base}/top-vendedores-compare?${qsCompare()}`),
                fetch(`${base}/vendedores-trend?${qsTrend()}`)
            ]);

            if (cmpRes.ok) {
                const raw = await cmpRes.json();
                // Filtro de saneamiento: ignora items totalmente vacíos
                const sane = Array.isArray(raw) ? raw.filter(x =>
                    (x?.nombre ?? x?.Nombre) != null ||
                    (x?.netoA ?? x?.NetoA ?? x?.netoB ?? x?.NetoB ?? x?.undA ?? x?.UndA ?? x?.undB ?? x?.UndB) != null
                ) : [];

                let consolidated = consolidateByName(sane);
                // Fallback: si por alguna razón quedó vacío pero raw tiene datos, usa raw directamente
                if (consolidated.length === 0 && sane.length > 0) {
                    console.warn('Consolidación vacía; usando datos crudos del API');
                    consolidated = sane.map(x => ({
                        nombre: getName(x),
                        netoA: Number(x?.netoA ?? x?.NetoA ?? 0) || 0,
                        netoB: Number(x?.netoB ?? x?.NetoB ?? 0) || 0,
                        deltaNeto: Number(x?.deltaNeto ?? x?.DeltaNeto ?? ((Number(x?.netoB ?? x?.NetoB ?? 0) || 0) - (Number(x?.netoA ?? x?.NetoA ?? 0) || 0))) || 0,
                        pctNeto: Number(x?.pctNeto ?? x?.PctNeto ?? 0) || 0,
                        undA: Number(x?.undA ?? x?.UndA ?? 0) || 0,
                        undB: Number(x?.undB ?? x?.UndB ?? 0) || 0,
                        deltaUnd: Number(x?.deltaUnd ?? x?.DeltaUnd ?? ((Number(x?.undB ?? x?.UndB ?? 0) || 0) - (Number(x?.undA ?? x?.UndA ?? 0) || 0))) || 0,
                        pctUnd: Number(x?.pctUnd ?? x?.PctUnd ?? 0) || 0,
                        ticketsA: Number(x?.ticketsA ?? x?.TicketsA ?? 0) || 0,
                        ticketsB: Number(x?.ticketsB ?? x?.TicketsB ?? 0) || 0
                    }));
                }

                lastCompare = consolidated;
                console.log('compare consolidated', lastCompare);
                renderCompare(lastCompare);
                renderKPIs(lastCompare);
                renderTable(lastCompare);
            } else {
                console.error('compare error', cmpRes.status, await cmpRes.text());
                lastCompare = [];
                renderCompare([]);
                renderKPIs([]);
                renderTable([]);
            }

            if (trRes.ok) {
                const trendData = await trRes.json();
                renderTrend(trendData);
            } else {
                console.error('trend error', trRes.status, await trRes.text());
                renderTrend([]);
            }
        } catch (err) {
            console.error('fetch error', err);
            lastCompare = [];
            renderCompare([]);
            renderKPIs([]);
            renderTable([]);
            renderTrend([]);
        }
    }


    function setYoY() {
        el.yearB.value = (parseInt(el.yearA.value) - 1).toString();
        el.mFromB.value = el.mFromA.value;
        el.mToB.value = el.mToA.value;
    }

    // ---- Eventos ----
    el.btn.addEventListener('click', cargar);
    el.btnYoY.addEventListener('click', () => { setYoY(); cargar(); });
    el.btnExport.addEventListener('click', exportCsv);

    // Detalle por almacén (sólo si hay Id)
    document.querySelector('#tblCompare').addEventListener('click', async (ev) => {
        const btn = ev.target.closest('.btn-detalle');
        if (!btn) return;
        const tr = btn.closest('tr');
        const idStr = tr.dataset.id;
        if (!idStr) return; // sin Id -> no hay detalle
        const id = parseInt(idStr);
        if (!Number.isFinite(id)) return;
        const nombre = tr.dataset.nombre || 'Empleado';
        const res = await fetch(`${base}/empleado-por-almacen?${qsDetalle(id)}`);
        if (!res.ok) return;
        const data = await res.json();
        renderDetalle(nombre, data);
    });

    // ---- Init ----
    (async function init() {
        await fetchAlmacenes();
        updateSummary();
        await cargar();
    })();
})();
