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

    // ---- Helpers UI ----
    function fmtMoney(v) { return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 2 }).format(+v || 0); }
    function fmtInt(v) { return new Intl.NumberFormat().format(+v || 0); }
    function fmtPct(v) { return `${(+v || 0).toFixed(2)}%`; }

    async function fetchAlmacenes() {
        let res = await fetch(`${base}/almacenes-nombres`);
        if (!res.ok) res = await fetch(`${base}/almacenes`);
        const data = await res.json();
        const items = data.map(x => ({ id: x.id ?? x.Id ?? x.almacenId ?? x.AlmacenId, label: x.label ?? x.Label ?? x.nombre ?? x.Nombre })).filter(x => x.id != null);
        el.checks.innerHTML = items.map(it => `
      <div class="form-check">
        <input class="form-check-input almacen-check" type="checkbox" value="${it.id}" id="alm_${it.id}">
        <label class="form-check-label" for="alm_${it.id}">${it.label}</label>
      </div>
    `).join('');
        el.all.addEventListener('change', () => { document.querySelectorAll('.almacen-check').forEach(c => c.checked = el.all.checked); updateSummary(); });
        el.filter.addEventListener('input', () => {
            const q = el.filter.value.toLowerCase();
            el.checks.querySelectorAll('.form-check').forEach(div => { div.style.display = div.textContent.toLowerCase().includes(q) ? '' : 'none'; });
        });
        el.checks.addEventListener('change', updateSummary);
    }

    function getAlmacenIds() { const ids = []; document.querySelectorAll('.almacen-check').forEach(c => { if (c.checked) ids.push(parseInt(c.value)); }); return ids; }
    function updateSummary() { const ids = getAlmacenIds(); el.sum.textContent = ids.length ? `${ids.length} seleccionado(s)` : 'Ninguno'; }

    // ---- QS ----
    function qsCompare() { const u = new URLSearchParams(); u.set('yearA', el.yearA.value); u.set('monthFromA', el.mFromA.value); u.set('monthToA', el.mToA.value); u.set('yearB', el.yearB.value); u.set('monthFromB', el.mFromB.value); u.set('monthToB', el.mToB.value); u.set('metric', el.metric.value); u.set('topN', el.topN.value); getAlmacenIds().forEach(id => u.append('almacenIds', id)); return u.toString(); }
    function qsTrend() { const u = new URLSearchParams(); u.set('year', el.yearA.value); u.set('monthFrom', el.mFromA.value); u.set('monthTo', el.mToA.value); u.set('metric', el.metric.value); u.set('topN', el.topN.value); getAlmacenIds().forEach(id => u.append('almacenIds', id)); return u.toString(); }
    function qsDetalle(empleadoId) { const u = new URLSearchParams(); u.set('year', el.yearA.value); u.set('monthFrom', el.mFromA.value); u.set('monthTo', el.mToA.value); u.set('empleadoId', empleadoId); getAlmacenIds().forEach(id => u.append('almacenIds', id)); return u.toString(); }

    // ---- Charts ----
    function renderCompare(data) {
        const cats = data.map(x => x.nombre ?? x.Nombre);
        const netoA = data.map(x => +(x.netoA ?? x.NetoA ?? 0));
        const netoB = data.map(x => +(x.netoB ?? x.NetoB ?? 0));
        const undA = data.map(x => +(x.undA ?? x.UndA ?? 0));
        const undB = data.map(x => +(x.undB ?? x.UndB ?? 0));
        const serieA = el.metric.value === 'unidades' ? undA : netoA;
        const serieB = el.metric.value === 'unidades' ? undB : netoB;

        Highcharts.chart('chartCompare', {
            chart: { type: 'column' },
            title: { text: 'Top empleados — Periodo A vs Periodo B' },
            xAxis: { categories: cats, crosshair: true },
            yAxis: { min: 0, title: { text: el.metric.value === 'unidades' ? 'Unidades' : 'Monto neto' } },
            tooltip: { shared: true },
            plotOptions: { column: { pointPadding: 0.1, groupPadding: 0.15 } },
            series: [{ name: 'Periodo A', data: serieA }, { name: 'Periodo B', data: serieB }],
            credits: { enabled: false },
            exporting: { enabled: true }
        });
    }

    function renderTrend(series) {
        const meses = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
        const hcSeries = series.map(s => ({ name: s.nombre ?? s.Nombre, data: (s.serie ?? s.Serie ?? []).map(p => el.metric.value === 'unidades' ? (p.unidades ?? p.Unidades ?? 0) : (p.neto ?? p.Neto ?? 0)) }));
        const from = parseInt(el.mFromA.value), to = parseInt(el.mToA.value); const cats = meses.slice(from - 1, to);
        Highcharts.chart('chartTrend', { chart: { type: 'line' }, title: { text: 'Tendencia mensual — Top-N (Periodo A)' }, xAxis: { categories: cats }, yAxis: { title: { text: el.metric.value === 'unidades' ? 'Unidades' : 'Monto neto' } }, tooltip: { shared: true }, series: hcSeries, credits: { enabled: false }, exporting: { enabled: true } });
    }

    function renderDetalle(nombre, items) {
        el.mdlTitle.textContent = `Detalle por almacén — ${nombre}`;
        const cats = items.map(x => x.almacen ?? x.Almacen);
        const data = items.map(x => +(x.neto ?? x.Neto ?? 0));
        Highcharts.chart('chartDetalleAlmacen', {
            chart: { type: 'column' },
            title: { text: null },
            xAxis: { categories: cats },
            yAxis: { title: { text: 'Monto neto' } },
            series: [{ name: 'Neto', data }],
            credits: { enabled: false }
        });
        const modal = new bootstrap.Modal(document.getElementById('mdlDetalle'));
        modal.show();
    }

    // ---- KPIs + Tabla ----
    function renderKPIs(data) {
        if (!data || !data.length) { el.kpiTopNombre.textContent = '—'; el.kpiTopValor.textContent = '—'; el.kpiTopDelta.textContent = '—'; return; }
        const top = data[0];
        const nombre = top.nombre ?? top.Nombre;
        const netoA = +(top.netoA ?? top.NetoA ?? 0);
        const netoB = +(top.netoB ?? top.NetoB ?? 0);
        const pct = +(top.pctNeto ?? top.PctNeto ?? 0);
        el.kpiTopNombre.textContent = nombre;
        el.kpiTopValor.textContent = `${fmtMoney(netoA)} (A) vs ${fmtMoney(netoB)} (B)`;
        el.kpiTopDelta.textContent = `Δ ${fmtMoney(netoB - netoA)} | ${fmtPct(pct)}`;
    }

    function renderTable(data) {
        el.tblBody.innerHTML = (data || []).map(r => {
            const id = r.id ?? r.Id; const nombre = r.nombre ?? r.Nombre;
            const nA = +(r.netoA ?? r.NetoA ?? 0), nB = +(r.netoB ?? r.NetoB ?? 0);
            const dN = +(r.deltaNeto ?? r.DeltaNeto ?? (nB - nA)), pN = +(r.pctNeto ?? r.PctNeto ?? 0);
            const uA = +(r.undA ?? r.UndA ?? 0), uB = +(r.undB ?? r.UndB ?? 0);
            const dU = +(r.deltaUnd ?? r.DeltaUnd ?? (uB - uA)), pU = +(r.pctUnd ?? r.PctUnd ?? 0);
            const tA = +(r.ticketsA ?? r.TicketsA ?? 0), tB = +(r.ticketsB ?? r.TicketsB ?? 0);
            return `<tr data-id="${id}" data-nombre="${nombre}">
        <td>${nombre}</td>
        <td class="text-end">${fmtMoney(nA)}</td>
        <td class="text-end">${fmtMoney(nB)}</td>
        <td class="text-end">${fmtMoney(dN)}</td>
        <td class="text-end">${fmtPct(pN)}</td>
        <td class="text-end">${fmtInt(uA)}</td>
        <td class="text-end">${fmtInt(uB)}</td>
        <td class="text-end">${fmtInt(dU)}</td>
        <td class="text-end">${fmtPct(pU)}</td>
        <td class="text-end">${fmtInt(tA)}</td>
        <td class="text-end">${fmtInt(tB)}</td>
        <td class="text-end"><button class="btn btn-sm btn-outline-primary btn-detalle">Por almacén</button></td>
      </tr>`;
        }).join('');
    }

    function exportCsv() {
        if (!lastCompare || !lastCompare.length) return;
        const headers = ['Empleado', 'NetoA', 'NetoB', 'DeltaNeto', 'PctNeto', 'UndA', 'UndB', 'DeltaUnd', 'PctUnd', 'TicketsA', 'TicketsB'];
        const rows = lastCompare.map(r => [
            (r.nombre ?? r.Nombre),
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
        const csv = [headers.join(','), ...rows.map(r => r.join(','))].join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = `vendedores-compare-${el.yearA.value}-${el.yearB.value}.csv`;
        document.body.appendChild(a); a.click(); a.remove();
    }

    // ---- Carga ----
    async function cargar() {
        const [cmpRes, trRes] = await Promise.all([
            fetch(`${base}/top-vendedores-compare?${qsCompare()}`),
            fetch(`${base}/vendedores-trend?${qsTrend()}`)
        ]);
        if (cmpRes.ok) { lastCompare = await cmpRes.json(); renderCompare(lastCompare); renderKPIs(lastCompare); renderTable(lastCompare); }
        if (trRes.ok) { renderTrend(await trRes.json()); }
    }

    function setYoY() { el.yearB.value = (parseInt(el.yearA.value) - 1).toString(); el.mFromB.value = el.mFromA.value; el.mToB.value = el.mToA.value; }

    // ---- Eventos ----
    el.btn.addEventListener('click', cargar);
    el.btnYoY.addEventListener('click', () => { setYoY(); cargar(); });
    el.btnExport.addEventListener('click', exportCsv);

    // Delegación para botones "Por almacén"
    document.querySelector('#tblCompare').addEventListener('click', async (ev) => {
        const btn = ev.target.closest('.btn-detalle');
        if (!btn) return;
        const tr = btn.closest('tr');
        const id = parseInt(tr.dataset.id);
        const nombre = tr.dataset.nombre;
        const res = await fetch(`${base}/empleado-por-almacen?${qsDetalle(id)}`);
        if (!res.ok) return; const data = await res.json();
        renderDetalle(nombre, data);
    });

    // ---- Init ----
    (async function init() { await fetchAlmacenes(); updateSummary(); await cargar(); })();
})();