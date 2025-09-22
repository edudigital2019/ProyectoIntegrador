using Microsoft.AspNetCore.Mvc;

namespace AnalisisPredictivoVentas.Controllers
{
    public class SalesController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult General() => View();
        public IActionResult Productos() => View();
        public IActionResult Vendedores() => View();
        public IActionResult Pagos() => View();
        public IActionResult Genero() => View();
    }
}
