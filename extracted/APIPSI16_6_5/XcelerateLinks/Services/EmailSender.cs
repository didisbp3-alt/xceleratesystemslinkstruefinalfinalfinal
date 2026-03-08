using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Logging;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    
    private readonly string _gmailAddress = "lucasluxportugal@gmail.com";
    private readonly string _gmailPassword = "kgthaobqrynxsvlq";
    private readonly string _displayName = "Xcelerate Links";

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var message = new MailMessage(_gmailAddress, to, subject, htmlBody);
            message.IsBodyHtml = true;
            message.From = new MailAddress(_gmailAddress, _displayName);

            using var smtp = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(_gmailAddress, _gmailPassword),
                EnableSsl = true
            };

            await smtp.SendMailAsync(message);
            _logger.LogInformation("Sent email to {To} with subject {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", to);
            throw;
        }
    }
}