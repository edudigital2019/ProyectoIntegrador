namespace AnalisisPredictivoVentas.ViewModels;

public class EmailResultadoVm
{
    public string? ReturnUrl { get; set; }

    public ImportResultVm Result { get; set; } = default!;
}
