using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace AnalisisPredictivoVentas.Services.Email;

public record EmailMessage(string To, string Subject, string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage msg);
}

public class SmtpEmailSender : IEmailSender
{
    private readonly MailOptions _o;
    public SmtpEmailSender(IOptions<MailOptions> opt) => _o = opt.Value;

    public async Task SendAsync(EmailMessage msg)
    {
        using var client = new SmtpClient(_o.SmtpHost, _o.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_o.SmtpUser, _o.SmtpPass)
        };

        using var mail = new MailMessage
        {
            From = new MailAddress(_o.From, _o.FromName),
            Subject = $"{_o.SubjectPrefix}{msg.Subject}",
            Body = msg.HtmlBody,
            IsBodyHtml = true
        };

        mail.To.Add(msg.To);
        await client.SendMailAsync(mail);
    }
}