using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Matdo.Web.Services;

/// <summary>Versendet E-Mails über SMTP (Konfiguration aus <see cref="JsonConfigService"/>).</summary>
public class EmailSender
{
    private readonly JsonConfigService _config;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(JsonConfigService config, ILogger<EmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = _config.Current.Smtp;
        if (!smtp.Enabled || string.IsNullOrWhiteSpace(smtp.Host))
        {
            _logger.LogInformation("SMTP deaktiviert – E-Mail an {To} (\"{Subject}\") wird übersprungen.", toAddress, subject);
            return false;
        }

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        msg.To.Add(new MailboxAddress(toName, toAddress));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secure = smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(smtp.Host, smtp.Port, secure, ct);
            if (!string.IsNullOrWhiteSpace(smtp.User))
                await client.AuthenticateAsync(smtp.User, smtp.Password, ct);
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "E-Mail-Versand an {To} fehlgeschlagen.", toAddress);
            return false;
        }
    }
}
