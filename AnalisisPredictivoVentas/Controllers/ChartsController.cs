using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using AnalisisPredictivoVentas.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using static AnalisisPredictivoVentas.ViewModels.ChartsDtos;

namespace AnalisisPredictivoVentas.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChartsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChartsController(AppDbContext db) => _db = db;

    // GET: /api/charts/ventas-por-mes?year=2025&almacenId=1&categoria=Calzado&metodoPago=Efectivo
    [HttpGet("ventas-por-mes")]
    public async Task<ActionResult<ChartData>> VentasPorMes(
        int? year, int? almacenId, string? categoria, string? metodoPago)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = _db.HechosVentas.AsNoTracking().Where(h => h.Anio == y);

        if (almacenId.HasValue) q = q.Where(h => h.AlmacenId == almacenId.Value);
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(h => h.Categoria == categoria);
        if (!string.IsNullOrWhiteSpace(metodoPago)) q = q.Where(h => h.MetodoPago == metodoPago);

        var agregados = await q
            .GroupBy(h => h.Mes)
            .Select(g => new { Mes = g.Key, Neto = g.Sum(x => x.Neto) })
            .ToListAsync();

        var meses = Enumerable.Range(1, 12).ToArray();
        var categorias = meses.Select(m =>
            CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m)).ToList();

        var serie = meses.Select(m => agregados.FirstOrDefault(x => x.Mes == m)?.Neto ?? 0m).ToList();

        var data = new ChartData(categorias, new[] { new Series("Ventas (Neto)", serie) });
        return Ok(data);
    }

    // GET: /api/charts/metodos-pago?year=2025&almacenId=1&categoria=Calzado
    [HttpGet("metodos-pago")]
    public async Task<ActionResult<IEnumerable<string>>> MetodosPago(
        int? year, int? almacenId, string? categoria)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = _db.HechosVentas.AsNoTracking().Where(h => h.Anio == y);

        if (almacenId.HasValue) q = q.Where(h => h.AlmacenId == almacenId.Value);
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(h => h.Categoria == categoria);

        var lista = await q
            .Where(h => h.MetodoPago != null && h.MetodoPago != "")
            .Select(h => h.MetodoPago!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return Ok(lista);
    }

    // GET: /api/charts/almacenes?year=2025&categoria=Calzado
    [HttpGet("almacenes")]
    public async Task<ActionResult<IEnumerable<object>>> Almacenes(
        int? year, string? categoria)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = _db.HechosVentas.AsNoTracking().Where(h => h.Anio == y);
        if (!string.IsNullOrWhiteSpace(categoria)) q = q.Where(h => h.Categoria == categoria);

        var ids = await q.Select(h => h.AlmacenId)
                         .Distinct()
                         .ToListAsync();

        var nombres = await _db.Almacenes.AsNoTracking()
                          .Where(a => ids.Contains(a.Id))
                          .Select(a => new { a.Id, a.Nombre })
                          .ToListAsync();

        var map = nombres.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Nombre) ? null : x.Nombre.Trim()
        );

        var list = ids
            .Select(id => new {
                id,
                label = map.TryGetValue(id, out var nom) && !string.IsNullOrWhiteSpace(nom) ? nom! : $"Almacén {id}"
            })
            .OrderBy(x => x.label)
            .ToList();

        return Ok(list);
    }

    [HttpGet("ventas-por-mes-multi")]
    public async Task<ActionResult<ChartData>> VentasPorMesMulti(
        int? year,
        [FromQuery] int[] almacenIds,
        string? categoria,
        string? metodoPago)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = _db.HechosVentas.AsNoTracking().Where(h => h.Anio == y);

        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(h => h.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(metodoPago))
            q = q.Where(h => h.MetodoPago == metodoPago);

        if (almacenIds != null && almacenIds.Length > 0)
            q = q.Where(h => almacenIds.Contains(h.AlmacenId));

        var agregados = await q
            .GroupBy(h => new { h.Mes, h.AlmacenId })
            .Select(g => new { g.Key.Mes, g.Key.AlmacenId, Neto = g.Sum(x => x.Neto) })
            .ToListAsync();

        var meses = Enumerable.Range(1, 12).ToArray();
        var categorias = meses
            .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m))
            .ToList();

        var series = new List<Series>();

        var ids = (almacenIds != null && almacenIds.Length > 0)
            ? almacenIds.Distinct().OrderBy(x => x)
            : agregados.Select(a => a.AlmacenId).Distinct().OrderBy(x => x);

        foreach (var id in ids)
        {
            var data = meses
                .Select(m => agregados
                    .Where(a => a.AlmacenId == id && a.Mes == m)
                    .Sum(a => a.Neto))
                .ToList();

            series.Add(new Series($"Almacén {id}", data));
        }

        return Ok(new ChartData(categorias, series));
    }

    [HttpGet("ventas-heatmap")]
    public async Task<ActionResult<object>> VentasHeatmap(
        int? year,
        [FromQuery] int[]? almacenIds,
        string? categoria,
        string? metodoPago)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = _db.HechosVentas.AsNoTracking().Where(h => h.Anio == y);

        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(h => h.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(metodoPago))
            q = q.Where(h => h.MetodoPago == metodoPago);

        if (almacenIds != null && almacenIds.Length > 0)
            q = q.Where(h => almacenIds.Contains(h.AlmacenId));

        var agregados = await q
            .GroupBy(h => new { h.Mes, h.AlmacenId })
            .Select(g => new { g.Key.Mes, g.Key.AlmacenId, Neto = g.Sum(x => x.Neto) })
            .ToListAsync();

        var meses = Enumerable.Range(1, 12).ToArray();
        var xCats = meses
            .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m))
            .ToList();

        var yIds = (almacenIds != null && almacenIds.Length > 0)
            ? almacenIds.Distinct().OrderBy(x => x).ToList()
            : agregados.Select(a => a.AlmacenId).Distinct().OrderBy(x => x).ToList();

        var nombresMap = await _db.Almacenes.AsNoTracking()
            .Where(a => yIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nombre);

        var yCats = yIds.Select(id =>
        {
            if (!nombresMap.TryGetValue(id, out var nombre) || string.IsNullOrWhiteSpace(nombre))
                return $"Almacén {id}";
            return nombre.Trim();
        }).ToList();

        var data = new List<object>();
        for (int yIndex = 0; yIndex < yIds.Count; yIndex++)
        {
            var id = yIds[yIndex];
            for (int i = 0; i < meses.Length; i++)
            {
                int mes = meses[i];
                var val = agregados
                    .Where(a => a.AlmacenId == id && a.Mes == mes)
                    .Sum(a => a.Neto);

                data.Add(new object[] { i, yIndex, val });
            }
        }

        var max = agregados.Count > 0 ? agregados.Max(a => a.Neto) : 0m;

        return Ok(new
        {
            xCategories = xCats,
            yCategories = yCats,
            data,
            max
        });
    }

    public record ResumenVentasDto(
        decimal Neto, decimal Bruto, decimal Descuento, int Transacciones,
        decimal TicketPromedio, decimal NetoAnterior, decimal VarYoY, decimal Unidades);

    public record TopItemDto(string Label, decimal Neto, decimal Unidades);

    private IQueryable<HechosVentasModels> Filtrar(
        int year,
        int[]? almacenIds,
        string? categoria,
        string? metodoPago)
    {
        var q = _db.HechosVentas.AsNoTracking();

        q = q.Where(h => h.Anio == year);

        if (almacenIds != null && almacenIds.Length > 0)
            q = q.Where(h => almacenIds.Contains(h.AlmacenId));

        if (!string.IsNullOrWhiteSpace(categoria))
            q = q.Where(h => h.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(metodoPago))
            q = q.Where(h => h.MetodoPago == metodoPago);

        return q;
    }

    private IQueryable<HechosVentasModels> FiltrarPeriodo(
       int year, int? monthFrom, int? monthTo, int[]? almacenIds,
       string? categoria = null, string? metodoPago = null)
    {
        var q = Filtrar(year, almacenIds, categoria, metodoPago);

        if (monthFrom.HasValue) q = q.Where(v => v.Mes >= monthFrom.Value);
        if (monthTo.HasValue) q = q.Where(v => v.Mes <= monthTo.Value);

        return q;
    }

    // GET: /api/charts/resumen-ventas
    [HttpGet("resumen-ventas")]
    public async Task<ActionResult<ResumenVentasDto>> ResumenVentas(
        int? year, [FromQuery] int[]? almacenIds, string? categoria, string? metodoPago)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var q = Filtrar(y, almacenIds, categoria, metodoPago);

        var neto = await q.SumAsync(h => (decimal?)h.Neto) ?? 0m;
        var desc = await q.SumAsync(h => (decimal?)h.Descuento) ?? 0m;
        var bruto = neto + desc;
        var trans = await q.CountAsync();
        var und = await q.SumAsync(h => (decimal?)h.Cantidad) ?? 0m;
        var ticket = trans > 0 ? neto / trans : 0m;

        var qPrev = Filtrar(y - 1, almacenIds, categoria, metodoPago);
        var netoPrev = await qPrev.SumAsync(h => (decimal?)h.Neto) ?? 0m;
        var varYoY = netoPrev == 0 ? 0m : (neto - netoPrev) / netoPrev;

        return Ok(new ResumenVentasDto(neto, bruto, desc, trans, ticket, netoPrev, varYoY, und));
    }

    [HttpGet("top-productos")]
    [ProducesResponseType(typeof(IEnumerable<TopItemDto>), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, Duration = 0)]
    public async Task<ActionResult<IEnumerable<TopItemDto>>> TopProductos(
        [FromQuery] int? year,
        [FromQuery] int limit = 10,
        [FromQuery] int[]? almacenIds = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? metodoPago = null,
        [FromQuery] int? month = null)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = Filtrar(y, almacenIds, categoria, metodoPago);

        if (month is >= 1 and <= 12)
        {
            q = q.Where(h => h.Mes == month);
        }

        var existe = await q.AnyAsync();
        if (!existe) return Ok(Array.Empty<TopItemDto>());

        var data = await (
            from h in q
            join p0 in _db.Productos.AsNoTracking() on h.ProductoId equals p0.Id into jp
            from p in jp.DefaultIfEmpty()
            group new { h, p } by new
            {
                h.ProductoId,
                Nombre = (p != null && !string.IsNullOrWhiteSpace(p.Nombre)) ? p.Nombre : "(Sin producto)"
            } into g
            orderby g.Sum(x => x.h.Neto) descending
            select new TopItemDto(
                g.Key.Nombre,
                g.Sum(x => x.h.Neto),
                g.Sum(x => (decimal?)x.h.Cantidad) ?? 0m
            )
        )
        .Take(limit)
        .ToListAsync();

        return Ok(data);
    }

    [HttpGet("top-almacenes")]
    public async Task<ActionResult<IEnumerable<TopItemDto>>> TopAlmacenes(
        int? year, int limit = 10, string? categoria = null, string? metodoPago = null,
        [FromQuery] int[]? almacenIds = null)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var q = Filtrar(y, almacenIds, categoria, metodoPago);

        var data = await (
            from h in q
            join a0 in _db.Almacenes.AsNoTracking() on h.AlmacenId equals a0.Id into ja
            from a in ja.DefaultIfEmpty()
            group new { h, a } by new
            {
                h.AlmacenId,
                Nombre = (a != null && a.Nombre != null && a.Nombre != "") ? a.Nombre : $"Almacén {h.AlmacenId}"
            } into g
            orderby g.Sum(x => x.h.Neto) descending
            select new TopItemDto(
                g.Key.Nombre,
                g.Sum(x => x.h.Neto),
                g.Sum(x => (decimal?)x.h.Cantidad) ?? 0m
            )
        ).Take(limit).ToListAsync();

        return Ok(data);
    }

    [HttpGet("ventas-por-metodo-pago")]
    public async Task<ActionResult<IEnumerable<object>>> VentasPorMetodoPago(
        int? year,
        [FromQuery] int[]? almacenIds = null,
        string? categoria = null,
        string? metodoPago = null)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var q = Filtrar(y, almacenIds, categoria, metodoPago);

        var data = await q
            .Where(h => h.MetodoPago != null && h.MetodoPago != "")
            .GroupBy(h => h.MetodoPago!)
            .Select(g => new { name = g.Key, y = g.Sum(x => x.Neto) })
            .OrderByDescending(x => x.y)
            .ToListAsync();

        return Ok(data);
    }

    [HttpGet("almacenes-nombres")]
    public async Task<ActionResult<IEnumerable<object>>> AlmacenesConNombres(int? year, string? categoria)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var ids = await _db.HechosVentas.AsNoTracking()
                     .Where(h => h.Anio == y && (string.IsNullOrWhiteSpace(categoria) || h.Categoria == categoria))
                     .Select(h => h.AlmacenId)
                     .Distinct()
                     .ToListAsync();

        var nombres = await _db.Almacenes.AsNoTracking()
                          .Where(a => ids.Contains(a.Id))
                          .Select(a => new { id = a.Id, label = a.Nombre })
                          .ToListAsync();

        var faltantes = ids.Except(nombres.Select(n => n.id))
                           .Select(id => new { id, label = $"Almacén {id}" });

        var list = nombres.Concat(faltantes).OrderBy(x => x.id);
        return Ok(list);
    }

    public record SerieDto(string name, List<decimal> data);

    [HttpGet("top-productos-multi")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, Duration = 0)]
    public async Task<IActionResult> TopProductosMulti(
        [FromQuery] int? year,
        [FromQuery] int limit = 10,
        [FromQuery] int[] almacenIds = null!,
        [FromQuery] int? month = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? metodoPago = null)
    {
        if (almacenIds == null || almacenIds.Length < 2)
            return BadRequest("Se requieren 2 o más almacenes.");

        var y = year ?? DateTime.UtcNow.Year;

        var q = Filtrar(y, almacenIds, categoria, metodoPago);

        if (month is >= 1 and <= 12)
        {
            q = q.Where(h => h.Mes == month);
        }

        q = q.AsNoTracking();

        var almacenesExistentes = await q
            .Select(h => h.AlmacenId)
            .Distinct()
            .ToListAsync();

        var idsValidos = almacenIds.Intersect(almacenesExistentes).Distinct().ToList();
        if (idsValidos.Count < 2)
            return Ok(new { categories = Array.Empty<string>(), series = Array.Empty<object>() });

        var nombresAlmacenes = await _db.Almacenes
            .AsNoTracking()
            .Where(a => idsValidos.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nombre);

        var topGlobal = await (
            from h in q
            group h by h.ProductoId into g
            orderby g.Sum(x => x.Neto) descending
            select new { ProductoId = g.Key, Neto = g.Sum(x => x.Neto) }
        )
        .Take(limit)
        .ToListAsync();

        if (topGlobal.Count == 0)
            return Ok(new { categories = Array.Empty<string>(), series = Array.Empty<object>() });

        var prodIds = topGlobal.Select(t => t.ProductoId).ToArray();

        var nombres = await _db.Productos.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre })
            .ToListAsync();

        var nameMap = nombres.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Nombre) ? "(Sin producto)" : x.Nombre
        );

        var categories = prodIds
            .Select(id => nameMap.TryGetValue(id, out var n) ? n : $"Producto {id}")
            .ToList();

        var series = new List<object>();
        foreach (var almId in idsValidos)
        {
            var dict = await (
                from h in q.Where(h => h.AlmacenId == almId && prodIds.Contains(h.ProductoId))
                group h by h.ProductoId into g
                select new { ProductoId = g.Key, Neto = g.Sum(x => x.Neto) }
            ).ToDictionaryAsync(x => x.ProductoId, x => x.Neto);

            series.Add(new
            {
                name = (nombresAlmacenes.TryGetValue(almId, out var nom) && !string.IsNullOrWhiteSpace(nom))
                       ? nom : $"Almacén {almId}",
                data = prodIds.Select(pid => dict.TryGetValue(pid, out var v) ? v : 0m).ToArray()
            });
        }

        return Ok(new { categories, series });
    }

    public record VentasPorGeneroDto(string Genero, decimal Neto, decimal Unidades, int Tickets);

    [HttpGet("ventas-por-genero")]
    [ProducesResponseType(typeof(IEnumerable<VentasPorGeneroDto>), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, Duration = 0)]
    public async Task<ActionResult<IEnumerable<VentasPorGeneroDto>>> VentasPorGenero(
        [FromQuery] int? year,
        [FromQuery] int[]? almacenIds = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? metodoPago = null,
        [FromQuery] int? month = null)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = Filtrar(y, almacenIds, categoria, metodoPago);
        if (month is >= 1 and <= 12) q = q.Where(h => h.Mes == month);

        var rows = await q
            .Select(h => new { h.Neto, h.Cantidad, h.Genero })
            .ToListAsync();

        static string Normalizar(string? raw)
        {
            var s = (raw ?? "").Trim();
            if (s.Length == 0) return "(Sin género)";
            var dash = s.IndexOf('-');
            if (dash >= 0) s = s[..dash].Trim();

            var up = s.ToUpperInvariant();
            return up switch
            {
                "HOMBRE" => "Hombre",
                "MUJER" => "Mujer",
                "NIÑO" => "Niño",
                "NINO" => "Niño",
                _ => s
            };
        }

        var result = rows
            .Select(r => new
            {
                Genero = Normalizar(r.Genero),
                Neto = r.Neto,
                Unidades = r.Cantidad
            })
            .GroupBy(x => x.Genero)
            .Select(g => new VentasPorGeneroDto(
                Genero: g.Key,
                Neto: g.Sum(z => z.Neto),
                Unidades: g.Sum(z => (decimal?)z.Unidades) ?? 0m,
                Tickets: g.Count()
            ))
            .OrderByDescending(x => x.Neto)
            .ToList();

        return Ok(result);
    }

    public record VentasPorGeneroAlmacenItem(string Name, decimal Y, decimal Unidades, int Tickets);
    public record VentasPorGeneroAlmacenDto(int AlmacenId, string AlmacenNombre, List<VentasPorGeneroAlmacenItem> Series);

    [HttpGet("ventas-por-genero-por-almacen")]
    [ProducesResponseType(typeof(IEnumerable<VentasPorGeneroAlmacenDto>), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None, Duration = 0)]
    public async Task<ActionResult<IEnumerable<VentasPorGeneroAlmacenDto>>> VentasPorGeneroPorAlmacen(
        [FromQuery] int? year,
        [FromQuery] int[]? almacenIds = null,
        [FromQuery] string? categoria = null,
        [FromQuery] string? metodoPago = null,
        [FromQuery] int? month = null)
    {
        var y = year ?? DateTime.UtcNow.Year;

        var q = Filtrar(y, almacenIds, categoria, metodoPago);
        if (month is >= 1 and <= 12) q = q.Where(h => h.Mes == month);

        var rows = await q
            .Select(h => new { h.AlmacenId, h.Neto, h.Cantidad, h.Genero })
            .ToListAsync();

        var ids = rows.Select(r => r.AlmacenId).Distinct().ToList();
        var nombres = await _db.Almacenes
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nombre);
        string Nom(int id) => nombres.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n) ? n : $"Almacén {id}";

        static string Normalizar(string? raw)
        {
            var s = (raw ?? "").Trim();
            if (s.Length == 0) return "(Sin género)";
            var dash = s.IndexOf('-');
            if (dash >= 0) s = s[..dash].Trim();
            var u = s.ToUpperInvariant();
            return u switch
            {
                "HOMBRE" => "Hombre",
                "MUJER" => "Mujer",
                "NIÑO" => "Niño",
                "NINO" => "Niño",
                _ => s
            };
        }

        var porAlmacen = rows
            .GroupBy(r => r.AlmacenId)
            .Select(gAlm =>
            {
                var series = gAlm
                    .GroupBy(r => Normalizar(r.Genero))
                    .Select(g => new VentasPorGeneroAlmacenItem(
                        Name: g.Key,
                        Y: g.Sum(x => x.Neto),
                        Unidades: g.Sum(x => (decimal?)x.Cantidad) ?? 0m,
                        Tickets: g.Count()
                    ))
                    .OrderByDescending(x => x.Y)
                    .ToList();

                return new VentasPorGeneroAlmacenDto(
                    AlmacenId: gAlm.Key,
                    AlmacenNombre: Nom(gAlm.Key),
                    Series: series
                );
            })
            .OrderBy(x => x.AlmacenNombre)
            .ToList();

        return Ok(porAlmacen);
    }

    private record SellerCompareDto(
        int Id, string Nombre,
        decimal NetoA, decimal NetoB, decimal DeltaNeto, decimal PctNeto,
        decimal UndA, decimal UndB, decimal DeltaUnd, decimal PctUnd,
        int TicketsA, int TicketsB);

    private record SellerTrendPoint(int Mes, decimal Neto, decimal Unidades);
    private record SellerTrendDto(int Id, string Nombre, List<SellerTrendPoint> Serie);

    // ===========================================
    // Top vendedores — Periodo A vs B (agrupado por nombre)
    // ===========================================
    [HttpGet("top-vendedores-compare")]
    public async Task<IActionResult> TopVendedoresCompare(
     int yearA, int? monthFromA, int? monthToA,
     int yearB, int? monthFromB, int? monthToB,
     [FromQuery] int[]? almacenIds, string? categoria, string? metodoPago,
     string metric = "neto", int topN = 10, bool excluirDevoluciones = true)
    {
        // 1) Filtrado base por periodo A y B (reusa tu helper)
        var a = FiltrarPeriodo(yearA, monthFromA, monthToA, almacenIds, categoria, metodoPago);
        var b = FiltrarPeriodo(yearB, monthFromB, monthToB, almacenIds, categoria, metodoPago);

        if (excluirDevoluciones)
        {
            a = a.Where(x => x.Neto > 0);
            b = b.Where(x => x.Neto > 0);
        }

        // OPCIÓN: si quieres excluir registros sin vendedor, descomenta:
        // a = a.Where(v => v.EmpleadoId != null);
        // b = b.Where(v => v.EmpleadoId != null);

        // 2) Agrupar por vendedor (incluyendo null como 0 = "Sin vendedor")
        var aggA = await a
            .GroupBy(v => v.EmpleadoId ?? 0)
            .Select(g => new
            {
                Id = g.Key,
                Neto = g.Sum(x => x.Neto),
                // Si tu IQueryable no tiene Cantidad, cambia a 0m:
                Und = g.Sum(x => x.Cantidad), // <-- si no tienes 'Cantidad', usa 0m
                Tic = g.Select(x => x.Numero).Distinct().Count()
            })
            .ToListAsync();

        var aggB = await b
            .GroupBy(v => v.EmpleadoId ?? 0)
            .Select(g => new
            {
                Id = g.Key,
                Neto = g.Sum(x => x.Neto),
                // Igual que arriba:
                Und = g.Sum(x => x.Cantidad), // <-- si no tienes 'Cantidad', usa 0m
                Tic = g.Select(x => x.Numero).Distinct().Count()
            })
            .ToListAsync();

        // 3) Diccionario Id -> Nombre (solo para Id > 0, 0 será "Sin vendedor")
        var ids = aggA.Select(x => x.Id).Concat(aggB.Select(x => x.Id))
                      .Where(id => id > 0)
                      .Distinct()
                      .ToList();

        var idToName = await _db.Empleados.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(
                e => e.Id,
                e => string.IsNullOrWhiteSpace(e.Nombres) ? $"Empleado {e.Id}" : e.Nombres.Trim()
            );

        string Label(int id) => id == 0
            ? "Sin vendedor"
            : (idToName.TryGetValue(id, out var n) ? n : $"Empleado {id}");

        // 4) Unir A y B por Id de vendedor
        var map = new Dictionary<int, (string Nombre,
            decimal NetoA, decimal NetoB, decimal UndA, decimal UndB, int TicA, int TicB)>();

        foreach (var g in aggA)
        {
            var nom = Label(g.Id);
            if (!map.TryGetValue(g.Id, out var v)) v = (nom, 0, 0, 0, 0, 0, 0);
            v.NetoA += g.Neto; v.UndA += g.Und; v.TicA += g.Tic;
            // Asegura nombre “estable” (por si no existía)
            v.Nombre = nom;
            map[g.Id] = v;
        }

        foreach (var g in aggB)
        {
            var nom = Label(g.Id);
            if (!map.TryGetValue(g.Id, out var v)) v = (nom, 0, 0, 0, 0, 0, 0);
            v.NetoB += g.Neto; v.UndB += g.Und; v.TicB += g.Tic;
            v.Nombre = nom;
            map[g.Id] = v;
        }

        // 5) Proyección final
        decimal R2(decimal x) => Math.Round(x, 2);
        var rows = map.Values.Select(v =>
        {
            var dN = v.NetoB - v.NetoA;
            var pN = v.NetoA == 0 ? (v.NetoB > 0 ? 100m : 0m) : dN / v.NetoA * 100m;

            var dU = v.UndB - v.UndA;
            var pU = v.UndA == 0 ? (v.UndB > 0 ? 100m : 0m) : dU / v.UndA * 100m;

            return new
            {
                Nombre = v.Nombre, // <- usa SIEMPRE esta propiedad en el frontend
                NetoA = R2(v.NetoA),
                NetoB = R2(v.NetoB),
                DeltaNeto = R2(dN),
                PctNeto = R2(pN),
                UndA = R2(v.UndA),
                UndB = R2(v.UndB),
                DeltaUnd = R2(dU),
                PctUnd = R2(pU),
                TicketsA = v.TicA,
                TicketsB = v.TicB
            };
        });

        // 6) Orden según métrica elegida
        var ordered = metric.Equals("unidades", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderByDescending(x => x.UndA).ThenByDescending(x => x.NetoA)
            : metric.Equals("tickets", StringComparison.OrdinalIgnoreCase)
                ? rows.OrderByDescending(x => x.TicketsA).ThenByDescending(x => x.NetoA)
                : rows.OrderByDescending(x => x.NetoA).ThenByDescending(x => x.UndA);

        var top = ordered.Take(topN).ToList();
        if (top.Count == 0)
            return Ok(Array.Empty<object>());

        return Ok(top);
    }





    // ===========================================
    // Tendencia mensual de vendedores Top (agrupado por nombre)
    // ===========================================
    [HttpGet("vendedores-trend")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VendedoresTrend(
    int year, int? monthFrom, int? monthTo,
    [FromQuery] int[]? almacenIds,
    string? categoria, string? metodoPago,
    string metric = "neto", int topN = 5, bool excluirDevoluciones = true)
    {
        static string CleanNameFromDb(int id, string? raw)
        {
            var s = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(s)) return $"Empleado {id}";
            var u = s.ToUpperInvariant();
            if (u == "UNDEFINED" || u == "NULL" || u == "(NULL)" || u == "N/A") return $"Empleado {id}";
            return s;
        }

        var q = FiltrarPeriodo(year, monthFrom, monthTo, almacenIds, categoria, metodoPago);
        if (excluirDevoluciones) q = q.Where(x => x.Neto > 0);
        q = q.Where(v => v.EmpleadoId != null);

        var aggTotal = await q
            .GroupBy(v => v.EmpleadoId!.Value)
            .Select(g => new { Id = g.Key, Neto = g.Sum(x => x.Neto), Und = g.Sum(x => x.Cantidad) })
            .ToListAsync();

        var topIds = (metric.ToLower() == "unidades")
            ? aggTotal.OrderByDescending(x => x.Und).ThenByDescending(x => x.Neto).Take(topN).Select(x => x.Id).ToList()
            : aggTotal.OrderByDescending(x => x.Neto).ThenByDescending(x => x.Und).Take(topN).Select(x => x.Id).ToList();

        if (topIds.Count == 0) return Ok(Array.Empty<object>());

        var idToName = await _db.Empleados.AsNoTracking()
            .Where(e => topIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => CleanNameFromDb(e.Id, e.Nombres));

        var mensual = await q.Where(v => topIds.Contains(v.EmpleadoId!.Value))
            .GroupBy(v => new { Id = v.EmpleadoId!.Value, v.Mes })
            .Select(g => new { g.Key.Id, g.Key.Mes, Neto = g.Sum(x => x.Neto), Und = g.Sum(x => x.Cantidad) })
            .ToListAsync();

        int from = monthFrom ?? 1;
        int to = monthTo ?? 12;

        var series = topIds.Select(id => new
        {
            Id = id,
            Nombre = (idToName.TryGetValue(id, out var n) ? n : $"Empleado {id}"),
            Serie = Enumerable.Range(from, to - from + 1)
                .Select(m =>
                {
                    var row = mensual.FirstOrDefault(x => x.Id == id && x.Mes == m);
                    return new { Mes = m, Neto = row?.Neto ?? 0m, Unidades = row?.Und ?? 0m };
                })
                .ToList()
        });

        return Ok(series);
    }



    public record EmpleadoAlmacenDto(int AlmacenId, string Almacen, decimal Neto, decimal Unidades, int Tickets);

    [HttpGet("empleado-por-almacen")]
    [ProducesResponseType(typeof(IEnumerable<EmpleadoAlmacenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EmpleadoPorAlmacen(
        int year, int? monthFrom, int? monthTo,
        [FromQuery] int[]? almacenIds,
        int empleadoId,
        string? categoria, string? metodoPago,
        bool excluirDevoluciones = true)
    {
        var q = FiltrarPeriodo(year, monthFrom, monthTo, almacenIds, categoria, metodoPago)
            .Where(x => x.EmpleadoId == empleadoId);
        if (excluirDevoluciones) q = q.Where(x => x.Neto > 0);

        var agg = await q
            .GroupBy(x => x.AlmacenId)
            .Select(g => new { AlmacenId = g.Key, Neto = g.Sum(x => x.Neto), Und = g.Sum(x => x.Cantidad), Tickets = g.Select(x => x.Numero).Distinct().Count() })
            .ToListAsync();

        var nombres = await _db.Almacenes.AsNoTracking()
            .Where(a => agg.Select(x => x.AlmacenId).Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Nombre);

        var list = agg
            .Select(x => new EmpleadoAlmacenDto(
                x.AlmacenId,
                (nombres.TryGetValue(x.AlmacenId, out var n) && !string.IsNullOrWhiteSpace(n)) ? n : $"Almacén {x.AlmacenId}",
                x.Neto, x.Und, x.Tickets))
            .OrderByDescending(x => x.Neto)
            .ToList();

        return Ok(list);
    }
}
