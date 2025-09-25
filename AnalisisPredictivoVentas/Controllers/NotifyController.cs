using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using AnalisisPredictivoVentas.Services.Email;
using AnalisisPredictivoVentas.ViewModels;

namespace AnalisisPredictivoVentas.Controllers;

[Authorize] // o la policy que uses para importar (p.ej. [Authorize(Policy = "SubirInformacion")])
[Route("notify")]
public class NotifyController : Controller
{
    private readonly IEmailSender _email;
    private readonly MailOptions _opt;

    public NotifyController(IEmailSender email, IOptions<MailOptions> opt)
    {
        _email = email;
        _opt = opt.Value;
    }

    [HttpPost("resultado")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resultado([FromForm] EmailResultadoVm vm)
    {
        // 1) Destinatarios desde appsettings
        var toRaw = _opt.DefaultTo;
        if (string.IsNullOrWhiteSpace(toRaw))
        {
            TempData["MailErr"] = "No hay destinatarios configurados (Mail:DefaultTo).";
            return Redirect(vm.ReturnUrl ?? "/");
        }

        var toList = toRaw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (toList.Count == 0)
        {
            TempData["MailErr"] = "No se encontraron destinatarios válidos en Mail:DefaultTo.";
            return Redirect(vm.ReturnUrl ?? "/");
        }

        // 2) Construcción del HTML con tu ImportResultVm
        var r = vm.Result;
        var culture = new CultureInfo("es-EC");
        string Fm(decimal v) => v.ToString("N2", culture);
        string Fint(int v) => v.ToString("N0", culture);
        string fecha(DateTime? d) => d.HasValue ? d.Value.ToString("yyyy-MM-dd") : "—";

        var rangoFechas = (r.MinFecha.HasValue || r.MaxFecha.HasValue)
            ? $"{fecha(r.MinFecha)} – {fecha(r.MaxFecha)}"
            : "No especificado";

        var sb = new StringBuilder();
        sb.AppendLine("<div style='font-family:Segoe UI,Arial,sans-serif'>");
        sb.AppendLine("<h2 style='margin:0 0 10px'>Resultado de Importación</h2>");
        sb.AppendLine("<div style='background:#0b0e14;color:#fff;padding:14px;border-radius:12px'>");

        sb.AppendLine($"<p><strong>Fecha:</strong> {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
        sb.AppendLine($"<p><strong>Archivo:</strong> {System.Net.WebUtility.HtmlEncode(r.NombreArchivo ?? string.Empty)}</p>");
        sb.AppendLine($"<p><strong>Rango de fechas:</strong> {rangoFechas}</p>");
        sb.AppendLine($"<p><strong>Sucursales afectadas:</strong> {Fint(r.SucursalesAfectadas)}</p>");

        sb.AppendLine("<hr style='border-color:#444;margin:10px 0'>");
        sb.AppendLine("<h3 style='margin:8px 0 6px'>Totales</h3>");
        sb.AppendLine("<ul style='margin:0 0 8px 18px;padding:0'>");
        sb.AppendLine($"  <li><strong>Total procesadas:</strong> {Fint(r.TotalProcesadas)}</li>");
        sb.AppendLine($"  <li><strong>Ventas insertadas:</strong> {Fint(r.VentasInsertadas)}</li>");
        sb.AppendLine($"  <li><strong>Ventas actualizadas:</strong> {Fint(r.VentasActualizadas)}</li>");
        sb.AppendLine($"  <li><strong>Líneas detalle:</strong> {Fint(r.LineasDetalle)}</li>");
        sb.AppendLine($"  <li><strong>Productos upsert:</strong> {Fint(r.ProductosUpsert)}</li>");
        sb.AppendLine($"  <li><strong>Clientes upsert:</strong> {Fint(r.ClientesUpsert)}</li>");
        sb.AppendLine($"  <li><strong>Almacenes upsert:</strong> {Fint(r.AlmacenesUpsert)}</li>");
        sb.AppendLine($"  <li><strong>Métodos de pago upsert:</strong> {Fint(r.MetodosPagoUpsert)}</li>");
        sb.AppendLine("</ul>");

        sb.AppendLine("<h3 style='margin:8px 0 6px'>Montos</h3>");
        sb.AppendLine("<ul style='margin:0 0 8px 18px;padding:0'>");
        sb.AppendLine($"  <li><strong>Total cantidad (unidades):</strong> {Fm(r.TotalCantidad)}</li>");
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
            sb.AppendLine("<div><strong>Errores:</strong>");
            sb.AppendLine("<ul style='margin:6px 0 0 18px'>");
            foreach (var e in r.Errores.Take(50))
                sb.AppendLine($"<li style='color:#fff;font-weight:700'>{System.Net.WebUtility.HtmlEncode(e)}</li>");
            if (r.Errores.Count > 50) sb.AppendLine("<li>… y más</li>");
            sb.AppendLine("</ul></div>");
        }

        sb.AppendLine("</div></div>");
        var html = sb.ToString();

        // 3) Envío
        try
        {
            foreach (var to in toList)
            {
                await _email.SendAsync(new EmailMessage(
                    To: to,
                    Subject: $"{_opt.SubjectPrefix}Resultado de importación {DateTime.Now:yyyy-MM-dd}",
                    HtmlBody: html
                ));
            }
            TempData["MailOk"] = $"Resultado enviado a: {string.Join(", ", toList)}";
        }
        catch (Exception ex)
        {
            TempData["MailErr"] = $"Fallo el envío: {ex.Message}";
        }

        return Redirect(vm.ReturnUrl ?? "/");
    }
}

public class EmailResultadoVm
{
    public string? ReturnUrl { get; set; }
    public ImportResultVm Result { get; set; } = default!;
}
