using System.Net;
using System.Net.Mail;

namespace MailNotificationWirhOverandInterface
{
    public class mailNotification : SMTPConfrig, INotification
    {
        public override void SetEmailConfiguration()
        {
            Host = "smtp.gmail.com";
            Port = 587;
            EnableSsl = true;
            FromPassword = "";
            FromEmail = "";
            FromName = "";
        }

        public bool SendNotification(string to, string title, string message1)
        {
            return TrySend(to, title, message1, null, null, null, out _);
        }

        public bool TrySend(
            string to,
            string title,
            string htmlBody,
            byte[]? attachmentBytes,
            string? attachmentName,
            string? attachmentContentType,
            out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(Host) || Port <= 0)
            {
                SetEmailConfiguration();
            }

            if (string.IsNullOrWhiteSpace(FromEmail) || string.IsNullOrWhiteSpace(FromPassword))
            {
                error = "Mail account is missing an email or app password.";
                return false;
            }

            try
            {
                using var smtp = new SmtpClient
                {
                    Host = Host,
                    Port = Port,
                    EnableSsl = EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(FromEmail, FromPassword),
                    Timeout = 30000
                };

                using var message = new MailMessage
                {
                    From = string.IsNullOrWhiteSpace(FromName)
                        ? new MailAddress(FromEmail)
                        : new MailAddress(FromEmail, FromName),
                    Subject = title,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(to);

                if (attachmentBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(attachmentName))
                {
                    var stream = new MemoryStream(attachmentBytes);
                    var attachment = new Attachment(stream, attachmentName, attachmentContentType ?? "application/octet-stream");
                    message.Attachments.Add(attachment);
                }

                smtp.Send(message);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
