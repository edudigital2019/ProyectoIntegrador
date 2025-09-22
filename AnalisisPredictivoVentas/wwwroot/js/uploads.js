

(() => {
    const dz = document.getElementById('dropzone');
    const input = document.getElementById('fileInput');
    const fileInfo = document.getElementById('fileInfo');
    const form = document.getElementById('formUpload');
    const btnPlantilla = document.getElementById('btnPlantilla');
    const MAX_MB = 25;

    const bytes = n => (n / 1024 / 1024).toFixed(2) + ' MB';

    function setFile(file) {
        if (!file) return;
        const name = file.name || '';
        const okExt = name.toLowerCase().endsWith('.csv');
        if (!okExt) {
            alert('Por favor selecciona un archivo .csv');
            input.value = '';
            fileInfo.classList.add('d-none');
            return;
        }
        if (file.size > MAX_MB * 1024 * 1024) {
            alert(`El archivo supera el límite de ${MAX_MB} MB`);
            input.value = '';
            fileInfo.classList.add('d-none');
            return;
        }
        fileInfo.innerHTML = `<i class="bi bi-filetype-csv me-1"></i> <strong>${name}</strong> <span class="text-muted">(${bytes(file.size)})</span>`;
        fileInfo.classList.remove('d-none');
    }

    dz?.addEventListener('click', () => input?.click());

    dz?.addEventListener('dragover', e => { e.preventDefault(); dz.classList.add('dz-hover'); });
    dz?.addEventListener('dragleave', () => dz.classList.remove('dz-hover'));
    dz?.addEventListener('drop', e => {
        e.preventDefault(); dz.classList.remove('dz-hover');
        const file = e.dataTransfer?.files?.[0];
        if (file) { input.files = e.dataTransfer.files; setFile(file); }
    });

    input?.addEventListener('change', () => setFile(input.files?.[0]));

    btnPlantilla?.addEventListener('click', () => {
        const headers = 'Numero,Fecha,AlmacenCodigo,AlmacenNombre,ClienteIdentificacion,ClienteNombre,EmpleadoUsuarioId,EmpleadoNombre,ProductoCodigo,ProductoNombre,Categoria,Cantidad,PrecioUnitario,Descuento,Subtotal,Pagos';
        const row1 = 'V-1001,2025-08-31T10:32:00,ALM01,Principal,0912345678,Juan Perez,u-123,María Lopez,P001,Zapatilla A,Calzado,2,25,0,50,"Efectivo:50|Tarjeta:25"';
        const row2 = 'V-1001,2025-08-31T10:32:00,ALM01,Principal,0912345678,Juan Perez,u-123,María Lopez,P002,Zapatilla B,Calzado,1,30,5,25,"Efectivo:50|Tarjeta:25"';
        const csv = [headers, row1, row2].join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'plantilla_ventas.csv';
        document.body.appendChild(a); a.click(); a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 500);
    });

    form?.addEventListener('submit', e => {
        if (!input?.files || !input.files.length) {
            e.preventDefault();
            alert('Selecciona un archivo CSV antes de enviar.');
        }
    });
})();
