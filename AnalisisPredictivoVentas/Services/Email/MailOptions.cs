namespace AnalisisPredictivoVentas.Services.Email;

public class MailOptions
{
    public string From { get; set; } = default!;
    public string FromName { get; set; } = "APV";
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = default!;
    public string SmtpPass { get; set; } = default!;
    public string SubjectPrefix { get; set; } = "";
    public string? DefaultTo { get; set; }
}
