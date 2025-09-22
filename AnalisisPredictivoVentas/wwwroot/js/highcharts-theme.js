// wwwroot/js/highcharts-theme.js
(function (H) {
    H.setOptions({
        colors: ['#3b82f6', '#22c55e', '#94a3b8', '#ef4444', '#a855f7', '#06b6d4', '#f59e0b', '#10b981'],
        chart: {
            backgroundColor: '#ffffff',
            spacing: [10, 10, 10, 10],
            style: { fontFamily: 'Inter, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial' }
        },
        title: { style: { fontWeight: 600, fontSize: '18px' } },
        subtitle: { style: { color: '#6c757d' } },
        xAxis: { lineColor: '#e9ecef', tickLength: 0 },
        yAxis: {
            gridLineColor: '#f1f3f5',
            title: { text: 'USD' },
            labels: { formatter() { return this.value.toLocaleString('es-EC'); } }
        },
        tooltip: { shared: true, useHTML: true },
        plotOptions: {
            series: {
                lineWidth: 2,
                animation: { duration: 350 },
                marker: { radius: 4, lineWidth: 1, lineColor: '#fff' },
                states: { hover: { halo: { size: 6 } } },
                dataLabels: { enabled: false }
            },
            areaspline: {
                fillOpacity: 0.35,
                fillColor: {
                    linearGradient: { x1: 0, y1: 0, x2: 0, y2: 1 },
                    stops: [
                        [0, H.color('#3b82f6').setOpacity(0.25).get('rgba')],
                        [1, H.color('#3b82f6').setOpacity(0.02).get('rgba')]
                    ]
                },
                color: '#3b82f6'
            }
        },
        exporting: {
            enabled: true,
            fallbackToExportServer: false,
            buttons: { contextButton: { menuItems: ['viewFullscreen', 'printChart', 'separator', 'downloadPNG', 'downloadJPEG', 'downloadPDF', 'downloadSVG'] } }
        },
        lang: {
            decimalPoint: ',',
            thousandsSep: '.',
            months: ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'],
            shortMonths: ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'],
            weekdays: ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'],
            loading: 'Cargando...',
            viewFullscreen: 'Pantalla completa',
            printChart: 'Imprimir',
            downloadPNG: 'Descargar PNG',
            downloadJPEG: 'Descargar JPEG',
            downloadPDF: 'Descargar PDF',
            downloadSVG: 'Descargar SVG'
        },
        credits: { enabled: false }
    });
})(Highcharts);
