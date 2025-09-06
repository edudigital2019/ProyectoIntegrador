namespace AnalisisPredictivoVentas.Models
{
    public class ParametrosAbastecimiento
    {
        public int Id { get; set; }
        public int ProductoId { get; set; }
        public Producto Producto { get; set; } = null!;

        // políticas por producto (sin inventario)
        public int LeadTimeSemanas { get; set; } = 1;
        public int CoberturaSemanas { get; set; } = 2;
        public decimal NivelServicio { get; set; } = 0.95m; // 90..99%
        public int? LoteMinimo { get; set; }
        public int? MultiploCompra { get; set; }
    }

}
