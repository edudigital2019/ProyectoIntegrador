namespace AnalisisPredictivoVentas.ViewModels
{
    public class ChartsDtos
    {
        public record Series(string name, IEnumerable<decimal> data);
        public record ChartData(IEnumerable<string> categories, IEnumerable<Series> series);
    }
}
