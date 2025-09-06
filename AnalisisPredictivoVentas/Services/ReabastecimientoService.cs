using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AnalisisPredictivoVentas.Services
{
    public class ReabastecimientoService : IReabastecimientoService
    {
        private readonly AppDbContext _db;
        public ReabastecimientoService(AppDbContext db) => _db = db;

        /// <summary>
        /// Calcula sugerencias de compra basadas en demanda semanal promedio,
        /// desviación estándar y nivel de servicio (modelo base-stock).
        /// </summary>
        /// <param name="ventanaSemanas">Ventana de semanas a analizar (histórico).</param>
        /// <param name="filtroAlmacenId">Si se especifica, filtra ventas por almacén.</param>
        public async Task<List<SugerenciaCompraVm>> CalcularAsync(
            int ventanaSemanas = 12,
            int? filtroAlmacenId = null)
        {
            // 1) Fechas base
            var desde = DateTime.Today.AddDays(-7 * ventanaSemanas);
            var baseline = new DateTime(2000, 1, 3); // lunes arbitrario como base para DateDiffWeek

            // 2) Query base de ventas
            var ventas = _db.VentasDet
                .AsNoTracking()
                .Where(vd => vd.VentaCab.Fecha >= desde);

            if (filtroAlmacenId.HasValue)
                ventas = ventas.Where(vd => vd.VentaCab.AlmacenId == filtroAlmacenId.Value);

            // 3) Agregar por semana y producto (se ejecuta en SQL)
            var baseSemanal = await ventas
                .Select(vd => new
                {
                    vd.ProductoId,
                    WeekBucket = EF.Functions.DateDiffWeek(baseline, vd.VentaCab.Fecha),
                    Cant = vd.Cantidad
                })
                .GroupBy(x => new { x.ProductoId, x.WeekBucket })
                .Select(g => new { g.Key.ProductoId, CantSem = g.Sum(z => z.Cant) })
                .ToListAsync();

            // 4) Estadísticos por producto (en memoria)
            var statsDict = baseSemanal
                .GroupBy(x => x.ProductoId)
                .Select(g =>
                {
                    var data = g.Select(z => (double)z.CantSem).ToList();
                    double mean = data.Count > 0 ? data.Average() : 0.0;
                    double std = data.Count > 1
                        ? Math.Sqrt(data.Sum(v => Math.Pow(v - mean, 2)) / (data.Count - 1))
                        : 0.0;

                    return new { ProductoId = g.Key, Prom = (decimal)mean, Desv = (decimal)std };
                })
                .ToDictionary(x => x.ProductoId, x => x);

            // 5) Catálogo de productos
            var productos = await _db.Productos
                .AsNoTracking()
                .Select(p => new { p.Id, p.Codigo, p.Nombre })
                .ToListAsync();

            // 6) Parámetros por producto
            var pars = await _db.ParametrosAbastecimientos
                .AsNoTracking()
                .Select(p => new { p.ProductoId, p.LeadTimeSemanas, p.CoberturaSemanas, p.NivelServicio })
                .ToDictionaryAsync(
                    p => p.ProductoId,
                    p => new ParAbast(p.LeadTimeSemanas, p.CoberturaSemanas, p.NivelServicio)
                );

            // 7) Z por nivel de servicio (aprox. normal estándar)
            decimal ZFromService(decimal ns) => ns <= 0.90m ? 1.282m
                : ns <= 0.95m ? 1.645m
                : ns <= 0.97m ? 1.880m
                : ns <= 0.98m ? 2.054m
                : ns <= 0.99m ? 2.326m
                : 2.576m;

            // 8) Armar resultado
            var res = new List<SugerenciaCompraVm>();

            foreach (var p in productos)
            {
                statsDict.TryGetValue(p.Id, out var s);
                var prom = s?.Prom ?? 0m;
                var desv = s?.Desv ?? 0m;

                // Si no hay parámetros configurados, usa defaults
                pars.TryGetValue(p.Id, out var par);
                par ??= new ParAbast(LeadTimeSemanas: 1, CoberturaSemanas: 2, NivelServicio: 0.95m);

                var P = (decimal)(par.LeadTimeSemanas + par.CoberturaSemanas);
                var z = ZFromService(par.NivelServicio);

                var demandaProteccion = prom * P;
                var seguridad = z * desv * (decimal)Math.Sqrt((double)P);
                var stockObjetivo = Math.Ceiling(demandaProteccion + seguridad);

                // Por ahora sin inventario disponible: recomendado = stock objetivo
                var recomendado = stockObjetivo;

                res.Add(new SugerenciaCompraVm
                {
                    ProductoId = p.Id,
                    Producto = $"{p.Codigo} - {p.Nombre}",
                    PromedioSemanal = Math.Round(prom, 3),
                    DesvSemanal = Math.Round(desv, 3),
                    VentanaSemanas = baseSemanal.Count(b => b.ProductoId == p.Id),
                    LeadTimeSemanas = par.LeadTimeSemanas,
                    CoberturaSemanas = par.CoberturaSemanas,
                    NivelServicio = par.NivelServicio,
                    Z = z,
                    PeriodoProteccion = P,
                    DemandaProteccion = Math.Round(demandaProteccion, 3),
                    Seguridad = Math.Round(seguridad, 3),
                    StockObjetivo = stockObjetivo,
                    RecomendadoComprar = recomendado
                });
            }

            // 9) Devuelve solo productos con ventas y ordenados por necesidad
            return res
                .Where(x => x.PromedioSemanal > 0)
                .OrderByDescending(x => x.RecomendadoComprar)
                .ToList();
        }

        // Helper tipado para parámetros por producto
        private sealed record ParAbast(int LeadTimeSemanas, int CoberturaSemanas, decimal NivelServicio);
    }
}
