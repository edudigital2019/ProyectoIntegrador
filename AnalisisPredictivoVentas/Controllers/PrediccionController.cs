using AnalisisPredictivoVentas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AnalisisPredictivoVentas.Controllers
{
    //[Authorize(Roles = "Administrador,ResponsableAlmacen")]
    [AllowAnonymous]
    public class PrediccionController : Controller
    {
        private readonly IReabastecimientoService _svc;
        public PrediccionController(IReabastecimientoService svc) => _svc = svc;

        public async Task<IActionResult> Index(int ventana = 12, int? almacenId = null)
        {
            ViewBag.Ventana = ventana;
            ViewBag.AlmacenId = almacenId;
            var data = await _svc.CalcularAsync(ventana, almacenId);
            return View(data);
        }
    }

}
