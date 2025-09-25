using AnalisisPredictivoVentas.Import;
using AnalisisPredictivoVentas.Security;
using AnalisisPredictivoVentas.ViewModels;
using AnalisisPredictivoVentas.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace AnalisisPredictivoVentas.Controllers
{
    [Authorize(Policy = Permisos.SubirInformacion)]
    public class UploadsController : Controller
    {
        private readonly IImportService _import;
        private readonly IEmailSender _email;
        private readonly MailOptions _mailOpt;

        public UploadsController(IImportService import, IEmailSender email, IOptions<MailOptions> mailOpt)
        {
            _import = import;
            _email = email;
            _mailOpt = mailOpt.Value;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(IFormFile archivo)
        {
            if (archivo is null || archivo.Length == 0)
            {
                TempData["Err"] = "Seleccione un archivo .json o .xml";
                return View();
            }

            using var s = archivo.OpenReadStream();

            ImportResultVm res = await _import.ImportarAsync(archivo.FileName, s);

            await EnviarResultadoPorCorreoAsync(res);

            return View("Resultado", res);
        }

        private async Task EnviarResultadoPorCorreoAsync(ImportResultVm r)
        {
            try
            {
                var toRaw = _mailOpt.DefaultTo;
                if (string.IsNullOrWhiteSpace(toRaw)) return;

                var toList = toRaw
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (toList.Count == 0) return;

                var culture = new CultureInfo("es-EC");
                string Fm(decimal v) => v.ToString("N2", culture);
                string Fint(int v) => v.ToString("N0", culture);

                var rangoFechas = (r.MinFecha.HasValue || r.MaxFecha.HasValue)
                    ? $"{(r.MinFecha?.ToString("yyyy-MM-dd") ?? "—")} a {(r.MaxFecha?.ToString("yyyy-MM-dd") ?? "—")}"
                    : "No especificado";

                var sb = new StringBuilder();
                sb.AppendLine("<div style='font-family:Segoe UI,Arial,sans-serif'>");
                sb.AppendLine("<h2 style='margin:0 0 10px'>Resultado de Importación</h2>");
                sb.AppendLine("<div style='background:#0b0e14;color:#fff;padding:14px;border-radius:12px'>");

                sb.AppendLine($"<p><strong>Fecha:</strong> {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
                sb.AppendLine($"<p><strong>Archivo:</strong> {System.Net.WebUtility.HtmlEncode(r.NombreArchivo ?? string.Empty)}</p>");
                sb.AppendLine($"<p><strong>Rango de fechas:</strong> {rangoFechas}</p>");

                sb.AppendLine("<hr style='border-color:#444;margin:10px 0'>");
                sb.AppendLine("<h3 style='margin:8px 0 6px'>Totales</h3>");
                sb.AppendLine("<ul style='margin:0 0 8px 18px;padding:0'>");
                sb.AppendLine($"  <li><strong>Total procesadas:</strong> {Fint(r.TotalProcesadas)}</li>");
                sb.AppendLine($"  <li><strong>Ventas insertadas:</strong> {Fint(r.VentasInsertadas)}</li>");
                sb.AppendLine($"  <li><strong>Ventas actualizadas:</strong> {Fint(r.VentasActualizadas)}</li>");
                sb.AppendLine($"  <li><strong>Líneas detalle:</strong> {Fint(r.LineasDetalle)}</li>");
                sb.AppendLine($"  <li><strong>Métodos de pago upsert:</strong> {Fint(r.MetodosPagoUpsert)}</li>");
                sb.AppendLine($"  <li><strong>Sucursales afectadas:</strong> {Fint(r.SucursalesAfectadas)}</li>");
                sb.AppendLine("</ul>");

                sb.AppendLine("<h3 style='margin:8px 0 6px'>Montos</h3>");
                sb.AppendLine("<ul style='margin:0 0 8px 18px;padding:0'>");
                sb.AppendLine($"  <li><strong>Total cantidad:</strong> {Fm(r.TotalCantidad)}</li>");
                sb.AppendLine($"  <li><strong>Total bruto:</strong> {Fm(r.TotalBruto)}</li>");
                sb.AppendLine($"  <li><strong>Total descuento:</strong> {Fm(r.TotalDescuento)}</li>");
                sb.AppendLine($"  <li><strong>Total neto:</strong> {Fm(r.TotalNeto)}</li>");
                sb.AppendLine($"  <li><strong>Total pagos:</strong> {Fm(r.TotalPagos)}</li>");
                sb.AppendLine("</ul>");

                if (r.PagosPorMetodo?.Count > 0)
                {
                    sb.AppendLine("<h3 style='margin:8px 0 6px'>Pagos por método</h3>");
                    sb.AppendLine("<table style='width:100%;border-collapse:collapse;margin-bottom:8px'>");
                    sb.AppendLine("<thead><tr>");
                    sb.AppendLine("<th style='text-align:left;border-bottom:1px solid #444;padding:6px'>Método</th>");
                    sb.AppendLine("<th style='text-align:right;border-bottom:1px solid #444;padding:6px'>Monto</th>");
                    sb.AppendLine("</tr></thead><tbody>");
                    foreach (var kv in r.PagosPorMetodo.OrderBy(k => k.Key))
                    {
                        sb.AppendLine("<tr>");
                        sb.AppendLine($"<td style='padding:6px'>{System.Net.WebUtility.HtmlEncode(kv.Key)}</td>");
                        sb.AppendLine($"<td style='padding:6px;text-align:right'>{Fm(kv.Value)}</td>");
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</tbody></table>");
                }

                if (r.Errores?.Count > 0)
                {
                    sb.AppendLine("<hr style='border-color:#444;margin:10px 0'>");
                    sb.AppendLine("<div>");
                    sb.AppendLine("<strong>Errores:</strong>");
                    sb.AppendLine("<ul style='margin:6px 0 0 18px'>");
                    foreach (var e in r.Errores.Take(50))
                    {
                        sb.AppendLine($"<li style='color:#fff;font-weight:700'>{System.Net.WebUtility.HtmlEncode(e)}</li>");
                    }
                    sb.AppendLine("</ul>");
                    if (r.Errores.Count > 50)
                        sb.AppendLine("<p style='opacity:.8'>… y más</p>");
                    sb.AppendLine("</div>");
                }

                sb.AppendLine("</div></div>");
                var html = sb.ToString();

                foreach (var to in toList)
                {
                    await _email.SendAsync(new EmailMessage(
                        To: to,
                        Subject: $"{_mailOpt.SubjectPrefix}Resultado de importación {DateTime.Now:yyyy-MM-dd}",
                        HtmlBody: html
                    ));
                }

                TempData["MailOk"] = $"Resultado enviado a: {string.Join(", ", toList)}";
            }
            catch (Exception ex)
            {
                TempData["MailErr"] = $"No se pudo enviar el correo automático: {ex.Message}";
            }
        }
    }
}
