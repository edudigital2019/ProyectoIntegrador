using AnalisisPredictivoVentas.Data;
using AnalisisPredictivoVentas.Models;
using AnalisisPredictivoVentas.ViewModels;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AnalisisPredictivoVentas.Import
{
    public class ImportService : IImportService
    {
        private readonly AppDbContext _db;
        public ImportService(AppDbContext db) => _db = db;

        public async Task<ImportResultVm> ImportarAsync(string fileName, Stream contenido)
        {
            var result = new ImportResultVm { NombreArchivo = fileName };
            ArchivoVentasDto? dto;

            using var ms = new MemoryStream();
            await contenido.CopyToAsync(ms);
            var text = Encoding.UTF8.GetString(ms.ToArray());

            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                dto = ParseJson(text);
            else if (fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                dto = ParseXml(text);
            else if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                dto = ParseCsv(text); // CSV con separador |
            else
            {
                result.Errores.Add("Formato no soportado. Use .json, .xml o .csv");
                return result;
            }

            if (dto is null || dto.Ventas.Count == 0)
            {
                result.Errores.Add("No se encontraron ventas válidas en el archivo.");
                return result;
            }

            result.TotalProcesadas = dto.Ventas.Count;

            decimal totalBruto = 0m, totalNeto = 0m, totalDesc = 0m, totalCant = 0m, totalPagos = 0m;
            int lineas = 0;
            DateTime? minFecha = null, maxFecha = null;
            var sucursales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tocadas = new HashSet<int>();

            // ===== Procesamiento por venta (no se detiene ante errores)
            int idxLinea = 0;
            foreach (var v in dto.Ventas)
            {
                idxLinea++;

                try
                {
                    // ===== Almacén (requerido)
                    if (v.Almacen is null || string.IsNullOrWhiteSpace(v.Almacen.Codigo))
                    {
                        result.Errores.Add($"Línea {idxLinea} (Num={(v.Numero ?? "(null)")}, Suc=?): Almacén requerido.");
                        continue; // sin almacén no se puede continuar
                    }

                    var almacen = await UpsertAlmacenAsync(v.Almacen, result);
                    var sucursalKey = string.IsNullOrWhiteSpace(almacen.Codigo) ? almacen.Id.ToString() : almacen.Codigo!;
                    sucursales.Add(sucursalKey);

                    // ===== Numero (si falta → generar)
                    if (string.IsNullOrWhiteSpace(v.Numero))
                    {
                        v.Numero = GenerarNumeroFallback(sucursalKey, idxLinea);
                        result.Errores.Add($"Línea {idxLinea} (Suc={sucursalKey}): Numero vacío → asignado automáticamente '{v.Numero}'.");
                    }

                    // Período (min/max)
                    if (v.Fecha != default)
                    {
                        minFecha = !minFecha.HasValue || v.Fecha < minFecha ? v.Fecha : minFecha;
                        maxFecha = !maxFecha.HasValue || v.Fecha > maxFecha ? v.Fecha : maxFecha;
                    }

                    // ===== Cliente (opcional)
                    Cliente? cliente = null;
                    if (v.Cliente is not null)
                        cliente = await UpsertClienteAsync(v.Cliente, result);

                    // ===== Empleado (opcional)
                    Empleado? empleado = null;
                    if (v.Empleado is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(v.Empleado.UsuarioIdIdentity))
                            empleado = await _db.Empleados.FirstOrDefaultAsync(e => e.UsuarioIdIdentity == v.Empleado.UsuarioIdIdentity);

                        if (empleado is null && !string.IsNullOrWhiteSpace(v.Empleado.Nombre))
                        {
                            empleado = await _db.Empleados.FirstOrDefaultAsync(e =>
                                e.Nombres == v.Empleado.Nombre && e.AlmacenId == almacen.Id);

                            if (empleado is null)
                            {
                                empleado = new Empleado
                                {
                                    UsuarioIdIdentity = v.Empleado.UsuarioIdIdentity ?? Guid.NewGuid().ToString("N"),
                                    Nombres = v.Empleado.Nombre,
                                    AlmacenId = almacen.Id
                                };
                                _db.Empleados.Add(empleado);
                                await _db.SaveChangesAsync();
                            }
                        }
                    }

                    // ===== Cabecera (upsert por Numero + AlmacenId)
                    var cab = await _db.VentasCab
                        .Include(c => c.Detalles)
                        .Include(c => c.Pagos)
                        .FirstOrDefaultAsync(c => c.Numero == v.Numero && c.AlmacenId == almacen.Id);

                    var nueva = false;
                    if (cab is null)
                    {
                        cab = new VentaCab
                        {
                            Fecha = v.Fecha == default ? DateTime.UtcNow : v.Fecha,
                            Numero = v.Numero!, // ya garantizado
                            ClienteId = cliente?.Id,
                            EmpleadoId = empleado?.Id,
                            AlmacenId = almacen.Id,
                            Observacion = v.Observacion
                        };
                        _db.VentasCab.Add(cab);
                        nueva = true;
                    }
                    else
                    {
                        cab.Fecha = v.Fecha == default ? cab.Fecha : v.Fecha;
                        cab.ClienteId = cliente?.Id;
                        cab.EmpleadoId = empleado?.Id;
                        cab.Observacion = v.Observacion;

                        _db.VentasDet.RemoveRange(cab.Detalles);
                        _db.VentasPago.RemoveRange(cab.Pagos);
                    }

                    // ===== Detalles (requeridos)
                    if (v.Detalles is null || v.Detalles.Count == 0)
                        throw new InvalidOperationException("La venta no contiene detalles.");

                    decimal totalVenta = 0m;

                    foreach (var d in v.Detalles)
                    {
                        var prod = await UpsertProductoAsync(d.Producto, result);

                        var brutoLinea = d.PrecioUnitario * d.Cantidad;
                        var netoLinea = d.Subtotal > 0
                            ? d.Subtotal
                            : (d.PrecioUnitario * d.Cantidad - d.Descuento);

                        var det = new VentaDet
                        {
                            ProductoId = prod.Id,
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            Descuento = d.Descuento,
                            Subtotal = netoLinea,
                            Marca = d.Marca,
                            Categoria = d.Categoria ?? d.Producto.Categoria,
                            Genero = d.Genero,
                            Color = d.Color,
                            Referencia = d.Referencia,
                            Talla = d.Talla
                        };

                        cab.Detalles.Add(det);

                        totalVenta += netoLinea;
                        totalBruto += brutoLinea;
                        totalNeto += netoLinea;
                        totalDesc += (brutoLinea - netoLinea);
                        totalCant += d.Cantidad;
                        lineas++;
                    }

                    // ===== Pagos (opcionales)
                    foreach (var p in v.Pagos)
                    {
                        var mp = await UpsertMetodoPagoAsync(p.Metodo, result);
                        cab.Pagos.Add(new VentaPago { MetodoPagoId = mp.Id, Monto = p.Monto });

                        totalPagos += p.Monto;
                        result.PagosPorMetodo[mp.Nombre] = result.PagosPorMetodo.TryGetValue(mp.Nombre, out var acc)
                            ? acc + p.Monto
                            : p.Monto;
                    }

                    cab.Total = totalVenta;

                    // Guarda ESTA venta
                    await _db.SaveChangesAsync();

                    if (nueva) result.VentasInsertadas++;
                    else result.VentasActualizadas++;

                    tocadas.Add(cab.Id);
                }
                catch (DbUpdateException ex)
                {
                    DetachAll();
                    var causa = TraducirDbError(ex);
                    result.Errores.Add($"Línea {idxLinea} (Num={v.Numero ?? "(null)"}, Suc={v.Almacen?.Codigo ?? "?"}): {causa}");
                    continue; // seguir con la siguiente
                }
                catch (Exception ex)
                {
                    DetachAll();
                    result.Errores.Add($"Línea {idxLinea} (Num={v.Numero ?? "(null)"}, Suc={v.Almacen?.Codigo ?? "?"}): {ex.Message}");
                    continue; // seguir con la siguiente
                }
            }

            // ===== Materializar SOLO las ventas correctas
            try
            {
                await MaterializarHechosAsync(tocadas);
            }
            catch (Exception ex)
            {
                result.Errores.Add($"Advertencia: materialización falló: {ex.Message}");
            }

            // ===== Asigna totales calculados
            result.LineasDetalle = lineas;
            result.TotalCantidad = totalCant;
            result.TotalBruto = totalBruto;
            result.TotalDescuento = totalDesc;
            result.TotalNeto = totalNeto;
            result.TotalPagos = totalPagos;
            result.MinFecha = minFecha;
            result.MaxFecha = maxFecha;
            result.SucursalesAfectadas = sucursales.Count;

            return result;
        }

        // ---------- Helpers de error/cleanup ----------

        private static string TraducirDbError(DbUpdateException ex)
        {
            if (ex.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number switch
                {
                    515 => "Valor NULL en columna NOT NULL.",             // Cannot insert the value NULL...
                    2627 => "Clave duplicada (violación de UNIQUE/PK).",   // Violation of UNIQUE KEY constraint
                    2601 => "Índice único duplicado.",
                    _ => $"SQL Error {sqlEx.Number}: {sqlEx.Message}"
                };
            }
            return ex.Message;
        }

        private void DetachAll()
        {
            var entries = _db.ChangeTracker.Entries().ToList();
            foreach (var e in entries)
                if (e.State != EntityState.Detached)
                    e.State = EntityState.Detached;
        }

        private static string GenerarNumeroFallback(string sucursal, int index)
        {
            // Único y legible: Sucursal-YYYYMMDDHHMMSSfff-Idx
            return $"{sucursal}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{index}";
        }

        // ---------- Parsers y resto de helpers (sin cambios de lógica) ----------

        private static ArchivoVentasDto? ParseJson(string json)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            try
            {
                var dto = JsonSerializer.Deserialize<ArchivoVentasDto>(json, opts);
                if (dto is not null && dto.Ventas?.Count > 0) return dto;
            }
            catch { }
            try
            {
                var una = JsonSerializer.Deserialize<VentaDto>(json, opts);
                if (una is not null) return new ArchivoVentasDto { Ventas = new() { una } };
            }
            catch { }
            return null;
        }

        private static ArchivoVentasDto? ParseXml(string xml)
        {
            try
            {
                var x = XDocument.Parse(xml);
                if (x.Root is null) return null;

                if (x.Root.Name.LocalName.Equals("ArchivoVentas", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = new ArchivoVentasDto();

                    var xAlm = x.Root.Element("Almacen");
                    if (xAlm != null)
                        dto.Almacen = new AlmacenDto
                        {
                            Codigo = (string?)xAlm.Element("Codigo") ?? "",
                            Nombre = (string?)xAlm.Element("Nombre") ?? ""
                        };

                    foreach (var xv in x.Root.Element("Ventas")?.Elements("Venta") ?? Enumerable.Empty<XElement>())
                        dto.Ventas.Add(ParseXmlVenta(xv));

                    return dto;
                }
                else if (x.Root.Name.LocalName.Equals("Venta", StringComparison.OrdinalIgnoreCase))
                {
                    return new ArchivoVentasDto { Ventas = new() { ParseXmlVenta(x.Root) } };
                }
            }
            catch { }
            return null;
        }

        private static VentaDto ParseXmlVenta(XElement v)
        {
            var venta = new VentaDto
            {
                Numero = (string?)v.Element("Numero"),
                Fecha = DateTime.TryParse((string?)v.Element("Fecha"), out var f) ? f : DateTime.UtcNow,
                Observacion = (string?)v.Element("Observacion")
            };

            var xCli = v.Element("Cliente");
            if (xCli != null)
                venta.Cliente = new ClienteDto
                {
                    Identificacion = (string?)xCli.Element("Identificacion"),
                    Nombre = (string?)xCli.Element("Nombre") ?? "",
                    Email = (string?)xCli.Element("Email"),
                    Telefono = (string?)xCli.Element("Telefono")
                };

            var xAlm = v.Element("Almacen");
            if (xAlm != null)
                venta.Almacen = new AlmacenDto
                {
                    Codigo = (string?)xAlm.Element("Codigo") ?? "",
                    Nombre = (string?)xAlm.Element("Nombre") ?? ""
                };

            var xEmp = v.Element("Empleado");
            if (xEmp != null)
                venta.Empleado = new EmpleadoDto
                {
                    UsuarioIdIdentity = (string?)xEmp.Element("UsuarioIdIdentity"),
                    Nombre = (string?)xEmp.Element("Nombre") ?? ""
                };

            foreach (var xd in v.Element("Detalles")?.Elements("Detalle") ?? Enumerable.Empty<XElement>())
            {
                var xp = xd.Element("Producto");
                venta.Detalles.Add(new VentaDetDto
                {
                    Producto = new ProductoDto
                    {
                        Codigo = (string?)xp?.Element("Codigo") ?? "",
                        Nombre = (string?)xp?.Element("Nombre") ?? "",
                        Categoria = (string?)xp?.Element("Categoria"),
                        PrecioVenta = decimal.TryParse((string?)xp?.Element("PrecioVenta"), NumberStyles.Any, CultureInfo.InvariantCulture, out var pv) ? pv : null
                    },
                    Cantidad = decimal.TryParse((string?)xd.Element("Cantidad"), NumberStyles.Any, CultureInfo.InvariantCulture, out var c) ? c : 0m,
                    PrecioUnitario = decimal.TryParse((string?)xd.Element("PrecioUnitario"), NumberStyles.Any, CultureInfo.InvariantCulture, out var pu) ? pu : 0m,
                    Descuento = decimal.TryParse((string?)xd.Element("Descuento"), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds) ? ds : 0m,
                    Subtotal = decimal.TryParse((string?)xd.Element("Subtotal"), NumberStyles.Any, CultureInfo.InvariantCulture, out var st) ? st : 0m,
                    Marca = (string?)xp?.Element("Marca") ?? "",
                    Categoria = (string?)xp?.Element("Categoria") ?? "",
                    Genero = (string?)xp?.Element("Genero") ?? "",
                    Color = (string?)xp?.Element("Color") ?? "",
                    Referencia = (string?)xp?.Element("Referencia") ?? "",
                    Talla = (string?)xp?.Element("Talla") ?? ""
                });
            }

            foreach (var xp in v.Element("Pagos")?.Elements("Pago") ?? Enumerable.Empty<XElement>())
            {
                venta.Pagos.Add(new VentaPagoDto
                {
                    Metodo = (string?)xp.Element("Metodo") ?? "Efectivo",
                    Monto = decimal.TryParse((string?)xp.Element("Monto"), NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : 0m
                });
            }

            return venta;
        }

        private static ArchivoVentasDto? ParseCsv(string csvText)
        {
            var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = "|",
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
                HeaderValidated = null,
                PrepareHeaderForMatch = args =>
                {
                    var s = args.Header ?? string.Empty;
                    // quita espacios, signos y NBSP; a MAYÚSCULAS
                    s = Regex.Replace(s, @"[\s\p{P}\u00A0]+", "", RegexOptions.Compiled);
                    return s.ToUpperInvariant();
                }
            };

            using var reader = new StringReader(csvText);
            using var csv = new CsvReader(reader, cfg);

            // Números con . o , (flexible)
            csv.Context.TypeConverterOptionsCache.GetOptions<decimal>().NumberStyles =
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign;

            csv.Context.RegisterClassMap<VentaCsvRowMap>();

            var rows = csv.GetRecords<VentaCsvRow>().ToList();
            if (rows.Count == 0) return null;

            var dto = new ArchivoVentasDto();

            var grupos = rows.GroupBy(r => new { Num = r.NoFacturaVenta ?? "", Alm = r.Sucursal ?? "" });

            foreach (var g in grupos)
            {
                var first = g.First();
                if (string.IsNullOrWhiteSpace(first.Sucursal)) continue;

                var venta = new VentaDto
                {
                    Numero = string.IsNullOrWhiteSpace(first.NoFacturaVenta) ? null : first.NoFacturaVenta,
                    Fecha = ParseFechaFlexible(first.Fecha),
                    Almacen = new AlmacenDto { Codigo = first.Sucursal!, Nombre = first.Sucursal! },
                    Cliente = string.IsNullOrWhiteSpace(first.Cliente) ? null : new ClienteDto { Nombre = first.Cliente! },
                    Empleado = string.IsNullOrWhiteSpace(first.Vendedor) ? null : new EmpleadoDto { Nombre = first.Vendedor! }
                };

                foreach (var r in g)
                {
                    if (string.IsNullOrWhiteSpace(r.Producto)) continue;

                    var neto = r.Neto > 0 ? r.Neto : (r.PrecioVenta * r.Cantidad - r.Descuento);

                    venta.Detalles.Add(new VentaDetDto
                    {
                        Producto = new ProductoDto
                        {
                            Codigo = r.Producto!,
                            Nombre = r.Descripcion ?? "",
                            Categoria = r.Categoria,
                            PrecioVenta = r.PrecioVenta
                        },
                        Cantidad = r.Cantidad,
                        PrecioUnitario = r.PrecioVenta,
                        Descuento = r.Descuento,
                        Subtotal = neto,
                        Marca = string.IsNullOrWhiteSpace(r.Marca) ? null : r.Marca,
                        Categoria = string.IsNullOrWhiteSpace(r.Categoria) ? null : r.Categoria,
                        Genero = string.IsNullOrWhiteSpace(r.Genero) ? null : r.Genero,
                        Color = string.IsNullOrWhiteSpace(r.Color) ? null : r.Color,
                        Referencia = string.IsNullOrWhiteSpace(r.Referencia) ? null : r.Referencia,
                        Talla = string.IsNullOrWhiteSpace(r.Talla) ? null : r.Talla
                    });
                }

                var pagos = g.GroupBy(r => new { r.CodFormaPago, r.DescripcionPago })
                             .Select(x => new
                             {
                                 x.Key.CodFormaPago,
                                 x.Key.DescripcionPago,
                                 Monto = x.Sum(z => z.Neto)
                             })
                             .Where(x => !string.IsNullOrWhiteSpace(x.CodFormaPago) ||
                                         !string.IsNullOrWhiteSpace(x.DescripcionPago));

                foreach (var p in pagos)
                {
                    var metodo = !string.IsNullOrWhiteSpace(p.CodFormaPago)
                        ? p.CodFormaPago!
                        : (p.DescripcionPago ?? "N/A");

                    venta.Pagos.Add(new VentaPagoDto { Metodo = metodo, Monto = p.Monto });
                }

                dto.Ventas.Add(venta);
            }

            return dto;
        }

        private static DateTime ParseFechaFlexible(string? s)
        {
            s ??= "";
            s = s.Trim();
            if (string.IsNullOrEmpty(s)) return DateTime.Today;

            var formatos = new[]
            {
                "dd/MMM/yyyy","dd/MM/yyyy","dd-MMM-yyyy","dd-MMM-yy","dd/MMM/yy","dd/MM/yy"
            };
            var culturas = new[] { new CultureInfo("es-ES"), new CultureInfo("en-US"), CultureInfo.InvariantCulture };

            foreach (var ci in culturas)
            {
                if (DateTime.TryParseExact(s, formatos, ci, DateTimeStyles.None, out var dt))
                    return dt;
            }

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var gen))
                return gen;

            return DateTime.Today;
        }

        private async Task<Producto> UpsertProductoAsync(ProductoDto dto, ImportResultVm res)
        {
            var prod = await _db.Productos.FirstOrDefaultAsync(p => p.Codigo == dto.Codigo);
            if (prod is null)
            {
                prod = new Producto
                {
                    Codigo = dto.Codigo,
                    Nombre = dto.Nombre,
                    Categoria = dto.Categoria,
                    PrecioVenta = dto.PrecioVenta ?? 0
                };
                _db.Productos.Add(prod);
                await _db.SaveChangesAsync();
                res.ProductosUpsert++;
            }
            else
            {
                bool changed = false;
                if (!string.IsNullOrWhiteSpace(dto.Nombre) && prod.Nombre != dto.Nombre)
                { prod.Nombre = dto.Nombre; changed = true; }
                if (dto.Categoria != null && prod.Categoria != dto.Categoria)
                { prod.Categoria = dto.Categoria; changed = true; }
                if (dto.PrecioVenta.HasValue && prod.PrecioVenta != dto.PrecioVenta.Value)
                { prod.PrecioVenta = dto.PrecioVenta.Value; changed = true; }
                if (changed) await _db.SaveChangesAsync();
            }
            return prod;
        }

        private async Task<Cliente> UpsertClienteAsync(ClienteDto dto, ImportResultVm res)
        {
            Cliente? cli = null;
            if (!string.IsNullOrWhiteSpace(dto.Identificacion))
                cli = await _db.Clientes.FirstOrDefaultAsync(c => c.Identificacion == dto.Identificacion);
            if (cli is null)
                cli = await _db.Clientes.FirstOrDefaultAsync(c => c.Nombre == dto.Nombre);

            if (cli is null)
            {
                cli = new Cliente
                {
                    Nombre = dto.Nombre,
                    Identificacion = dto.Identificacion,
                    Email = dto.Email,
                    Telefono = dto.Telefono
                };
                _db.Clientes.Add(cli);
                await _db.SaveChangesAsync();
                res.ClientesUpsert++;
            }
            else
            {
                bool changed = false;
                if (!string.IsNullOrWhiteSpace(dto.Nombre) && cli.Nombre != dto.Nombre)
                { cli.Nombre = dto.Nombre; changed = true; }
                if (!string.IsNullOrWhiteSpace(dto.Email) && cli.Email != dto.Email)
                { cli.Email = dto.Email; changed = true; }
                if (!string.IsNullOrWhiteSpace(dto.Telefono) && cli.Telefono != dto.Telefono)
                { cli.Telefono = dto.Telefono; changed = true; }
                if (changed) await _db.SaveChangesAsync();
            }
            return cli;
        }

        private async Task<Almacen> UpsertAlmacenAsync(AlmacenDto dto, ImportResultVm res)
        {
            var alm = await _db.Almacenes.FirstOrDefaultAsync(a => a.Codigo == dto.Codigo);
            if (alm is null)
            {
                alm = new Almacen { Codigo = dto.Codigo, Nombre = dto.Nombre };
                _db.Almacenes.Add(alm);
                await _db.SaveChangesAsync();
                res.AlmacenesUpsert++;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dto.Nombre) && alm.Nombre != dto.Nombre)
                {
                    alm.Nombre = dto.Nombre;
                    await _db.SaveChangesAsync();
                }
            }
            return alm;
        }

        private async Task<MetodoPago> UpsertMetodoPagoAsync(string nombre, ImportResultVm res)
        {
            if (string.IsNullOrWhiteSpace(nombre)) nombre = "Efectivo";
            var mp = await _db.MetodosPago.FirstOrDefaultAsync(m => m.Nombre == nombre);
            if (mp is null)
            {
                mp = new MetodoPago
                {
                    Nombre = nombre,
                    PermiteVuelto = nombre.Equals("Efectivo", StringComparison.OrdinalIgnoreCase)
                };
                _db.MetodosPago.Add(mp);
                await _db.SaveChangesAsync();
                res.MetodosPagoUpsert++;
            }
            return mp;
        }

        private async Task MaterializarHechosAsync(IEnumerable<int> cabIds)
        {
            var ids = cabIds.Distinct().ToList();
            if (ids.Count == 0) return;

            // TVP dbo.IntList
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            foreach (var id in ids) table.Rows.Add(id);

            var conn = (SqlConnection)_db.Database.GetDbConnection();
            var mustClose = conn.State != ConnectionState.Open;
            if (mustClose) await conn.OpenAsync();

            try
            {
                using var cmd = new SqlCommand("dbo.usp_MaterializarHechosVentas", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                var tvp = new SqlParameter("@CabIds", SqlDbType.Structured)
                {
                    TypeName = "dbo.IntList",
                    Value = table
                };
                cmd.Parameters.Add(tvp);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (mustClose) await conn.CloseAsync();
            }
        }
    }

    // ===== CSV DTOs/Maps =====

    public class VentaCsvRow
    {
        public string Sucursal { get; set; } = "";
        public string NoFacturaVenta { get; set; } = "";
        public string Vendedor { get; set; } = "";
        public string Fecha { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Producto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public decimal Cantidad { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal Descuento { get; set; }
        public string PorcDescuento { get; set; } = "";
        public decimal Neto { get; set; }
        public string DescripcionPago { get; set; } = "";
        public string CodFormaPago { get; set; } = "";
        public string Marca { get; set; } = "";
        public string TipoSubtipo { get; set; } = "";
        public string Genero { get; set; } = "";
        public string Color { get; set; } = "";
        public string Referencia { get; set; } = "";
        public string Talla { get; set; } = "";
    }

    public sealed class VentaCsvRowMap : ClassMap<VentaCsvRow>
    {
        public VentaCsvRowMap()
        {
            Map(m => m.Sucursal).Name("SUCURSAL");
            Map(m => m.NoFacturaVenta).Name("NOFACTURAVENTA");
            Map(m => m.Vendedor).Name("VENDEDOR");
            Map(m => m.Fecha).Name("FECHA");
            Map(m => m.Cliente).Name("CLIENTE");
            Map(m => m.Producto).Name("PRODUCTO");
            Map(m => m.Categoria).Name("CATEGORIA");
            Map(m => m.Descripcion).Name("DESCRIPCION");
            Map(m => m.Cantidad).Name("CANTIDAD");
            Map(m => m.PrecioVenta).Name("PRECIOVENTA");
            Map(m => m.Descuento).Name("DESCUENTO");
            Map(m => m.PorcDescuento).Name("PORDESCUENTO");
            Map(m => m.Neto).Name("NETO");
            Map(m => m.DescripcionPago).Name("DESCRIPCIONPAGO");
            Map(m => m.CodFormaPago).Name("CODFORMAPAGO");
            Map(m => m.Marca).Name("MARCA");
            Map(m => m.TipoSubtipo).Name("TIPOSUBTIPO");
            Map(m => m.Genero).Name("GENERO");
            Map(m => m.Color).Name("COLOR");
            Map(m => m.Referencia).Name("REFERENCIA");
            Map(m => m.Talla).Name("TALLA");
        }
    }
}