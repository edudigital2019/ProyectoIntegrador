using AnalisisPredictivoVentas.ViewModels;

namespace AnalisisPredictivoVentas.Import
{
    public interface IImportService
    {
        Task<ImportResultVm> ImportarAsync(string fileName, Stream contenido);
    }

}
