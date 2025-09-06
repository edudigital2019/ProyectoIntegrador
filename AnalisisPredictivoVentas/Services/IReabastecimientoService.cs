using AnalisisPredictivoVentas.ViewModels;

namespace AnalisisPredictivoVentas.Services
{
    public interface IReabastecimientoService
    {
        Task<List<SugerenciaCompraVm>> CalcularAsync(int ventanaSemanas = 12, int? filtroAlmacenId = null);
    }

}
