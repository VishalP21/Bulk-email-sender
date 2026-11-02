using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MailNotificationWirhOverandInterface
{
    public interface Ismsnotification
    {
        public void SendNotification(string to, string messge);
    }
}
