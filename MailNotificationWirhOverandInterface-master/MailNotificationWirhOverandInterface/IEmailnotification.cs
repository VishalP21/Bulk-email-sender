using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailNotificationWirhOverandInterface
{
    public interface IEmailnotification
    {
        public void SendNotification(string to, string messge);
    }
}
