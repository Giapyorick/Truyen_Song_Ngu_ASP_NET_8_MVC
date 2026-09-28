using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WebTruyenTranh.Helpers
{
    public interface IEmailSenderService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }

    public class EmailSenderService : IEmailSenderService
    {
        private readonly IConfiguration _config;

        public EmailSenderService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var mailSettings = _config.GetSection("MailSettings");
            string fromMail = mailSettings["Mail"] ?? "";
            string displayName = mailSettings["DisplayName"] ?? "Bilingual Manga";
            string password = mailSettings["Password"] ?? "";
            string host = mailSettings["Host"] ?? "smtp.gmail.com";
            int port = int.TryParse(mailSettings["Port"], out int p) ? p : 587;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromMail, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 15000 // 15 giây timeout
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromMail, displayName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}