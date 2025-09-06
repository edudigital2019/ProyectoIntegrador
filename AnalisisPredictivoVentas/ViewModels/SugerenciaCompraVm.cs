namespace AnalisisPredictivoVentas.ViewModels
{
    public class SugerenciaCompraVm
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = "";
        public decimal PromedioSemanal { get; set; }
        public decimal DesvSemanal { get; set; }
        public int VentanaSemanas { get; set; }

        public int LeadTimeSemanas { get; set; }
        public int CoberturaSemanas { get; set; }
        public decimal NivelServicio { get; set; }
        public decimal Z { get; set; }

        public decimal PeriodoProteccion { get; set; }
        public decimal DemandaProteccion { get; set; }
        public decimal Seguridad { get; set; }
        public decimal StockObjetivo { get; set; }
        public decimal RecomendadoComprar { get; set; }
    }

}
