using AnalisisPredictivoVentas.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AnalisisPredictivoVentas.Models;

namespace AnalisisPredictivoVentas.Controllers
{
    // Si aún no activas Identity, quita el atributo Authorize
    //[Authorize(Roles = "Administrador,ResponsableAlmacen")]
    //[AllowAnonymous]
    [Authorize(Roles = $"{Roles.Administrador},{Roles.ResponsableCarga}")]
    public class UploadsController : Controller
    {
        private readonly IImportService _import;
        public UploadsController(IImportService import) => _import = import;

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
            var res = await _import.ImportarAsync(archivo.FileName, s);
            return View("Resultado", res);
        }
    }

}
