using System.Net;
using System.Net.Mail;

namespace MailNotificationWirhOverandInterface
{
    public class SMTPConfrig
    {
        public string Host { get; set; } = "";

        public int Port { get; set; }

        public bool EnableSsl { get; set; }

        public string FromEmail { get; set; } = "";

        public string FromPassword { get; set; } = "";

        public string FromName { get; set; } = "";

        public virtual void SetEmailConfiguration()
        {
            Host = "smtp.gmail.com";
            Port = 587;
            EnableSsl = true;
            FromPassword = "";
            FromEmail = "";
            FromName = "";
        }
    }
}
